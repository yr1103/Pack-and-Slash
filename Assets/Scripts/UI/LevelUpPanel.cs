using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업·보물 상자 보상 요청 시 일시정지하고 보상 화면 표시, 완료 버튼은 표시 중인 회차만 처리하고 다음 회차는 레벨업(카드 3장) 우선 후 보물 상자(카드 5장), 모두 끝나면 닫으며 재개
/// 회차마다 후보 선택(Selecting) → 선택 아이템을 Temporary에서 정리(Organizing) 순서로 진행
/// </summary>
public class LevelUpPanel : MonoBehaviour
{
    private const string STORAGE_COUNT_FORMAT = "보관함 {0}/{1}"; // {0} 보관 수, {1} 현재 보관함 한도
    private const string CURRENCY_FORMAT = "보유 {0}"; // {0} 특수 재화 잔액
    private const string SELECTION_COUNT_FORMAT = "선택 {0}/{1}"; // {0} 선택 수, {1} 필요 선택 수
    private const string SELECTION_GUIDE_FORMAT = "보상을 선택하세요! {0}개 중 {1}개를 클릭해 선택하세요."; // {0} 후보 카드 수, {1} 필요 선택 수
    private const string SELECTION_DONE_FORMAT = "{0}개 선택 완료! 인벤토리 정리하기를 눌러 진행하세요."; // {0} 필요 선택 수
    private const string EXPANSION_GUIDE_FORMAT = "확장할 {0}칸을 선택하세요!!"; // {0} 남은 확장 수
    private const float SLOT_CENTER_RATIO = 0.5f; // Temporary 가로 분할 칸의 중심 비율

    [SerializeField] private GameSession _gameSession;
    [SerializeField] private PlayerExperience _playerExperience;
    [SerializeField] private GameObject _panelRoot; // 이 컴포넌트와 다른 오브젝트여야 이벤트 구독이 유지됨
    [SerializeField] private Button _completeButton;
    [SerializeField] private RewardCardUI[] _rewardCards;
    [SerializeField, Min(1)] private int _levelUpCardCount = 3; // 레벨업 회차 표시 카드 수
    [SerializeField, Min(1)] private int _chestCardCount = 5; // 보물 상자 회차 표시 카드 수
    [SerializeField, Min(1)] private int _levelUpMaxSelect = 2; // 레벨업 회차 선택 수
    [SerializeField, Min(1)] private int _chestMaxSelect = 3; // 보물 상자 회차 선택 수
    [SerializeField] private InventoryData[] _rewardPool;
    [SerializeField, Min(0f)] private float _weaponWeight = 50f; // 종류 등장 가중치 (남은 종류끼리 정규화)
    [SerializeField, Min(0f)] private float _passiveWeight = 35f;
    [SerializeField, Min(0f)] private float _expendableWeight = 15f;
    [SerializeField, Min(1)] private int _rerollCost = 2;
    [SerializeField] private Currency _currency;
    [SerializeField] private Button _rerollButton;
    [SerializeField] private Text _currencyText;
    [SerializeField] private Text _selectionCountText;
    [SerializeField] private Text _selectionGuideText; // 선택 단계 상단 안내
    [SerializeField] private Button _confirmButton;
    [SerializeField] private GameObject _selectingGroup; // 리롤·재화·선택 수·선택 완료 버튼 묶음 (카드는 프리팹 구조상 그룹 밖)
    [SerializeField] private Inventory _inventory;
    [SerializeField] private GameObject _temporaryWarning; // Temporary 아이템이 남아 있을 때 표시
    [SerializeField] private Text _storageCountText;
    [SerializeField] private RectTransform _temporaryArea; // InventoryUI의 Temporary 영역과 같은 오브젝트 (배치 좌표 기준)
    [SerializeField] private InventoryItemUI _itemPrefab;
    [SerializeField] private RectTransform _itemLayer;
    [SerializeField] private InventoryUI _inventoryUI;
    [SerializeField] private SellConfirmPopup _sellPopup;
    [SerializeField] private ItemTooltipUI _tooltip; // 생성한 Temporary 아이템 UI에 주입
    [SerializeField, Min(0)] private int _chestExpansionCount = 4; // 보물 상자 회차당 확장 칸 수
    [SerializeField] private Text _expansionGuideText; // null 허용 (미연결 씬은 안내 생략)
    [SerializeField] private PlayerHealth _playerHealth; // 회복 소비 아이템 적용 대상

    private readonly System.Random _random = new System.Random();
    private readonly List<int> _selectedIndices = new List<int>(); // 선택한 카드 인덱스 (선택 순서대로 Temporary 배치)
    private List<InventoryData> _candidates = new List<InventoryData>(); // 인덱스 = 카드 인덱스
    private int _pendingCount; // 아직 처리하지 않은 레벨업 횟수
    private int _pendingChestCount; // 아직 처리하지 않은 보물 상자 보상 횟수
    private bool _isShowingChest; // 현재 표시 중인 회차가 보물 상자인지 (완료 시 차감 대상)
    private RewardPhase _phase;
    private int _remainingExpansionCount; // 현재 상자 회차에서 남은 확장 수 (0이어야 완료 가능)

    /// <summary>레벨업 화면 표시 여부 (열려 있는 동안 ESC 일시정지 무시용)</summary>
    public bool IsOpen => _panelRoot.activeSelf;

    // 카드 수가 설정값보다 적으면 표시 가능한 카드 수까지만 후보 생성
    private int CardCount => Mathf.Min(_isShowingChest ? _chestCardCount : _levelUpCardCount, _rewardCards.Length);

    // 후보가 최대 선택 수보다 적으면 후보 수만큼만 선택
    private int RequiredSelectCount => Mathf.Min(_isShowingChest ? _chestMaxSelect : _levelUpMaxSelect, _candidates.Count);

    private void Awake()
    {
        _panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _playerExperience.LevelChanged += HandleLevelChanged;
        _completeButton.onClick.AddListener(HandleCompleteClicked);
        _confirmButton.onClick.AddListener(HandleConfirmClicked);
        _rerollButton.onClick.AddListener(HandleRerollClicked);
        _inventory.TemporaryCountChanged += RefreshCompleteButton;
        _inventory.Changed += RefreshStorageCount; // 보관 수와 함께 고유 아이템 탈착에 따른 한도 변경도 반영
        _currency.Changed += RefreshCurrency;
        _inventoryUI.ExpansionCellClicked += HandleExpansionCellClicked;

        foreach (RewardCardUI card in _rewardCards)
        {
            card.Clicked += HandleCardClicked;
        }
    }

    private void OnDisable()
    {
        _playerExperience.LevelChanged -= HandleLevelChanged;
        _completeButton.onClick.RemoveListener(HandleCompleteClicked);
        _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        _rerollButton.onClick.RemoveListener(HandleRerollClicked);
        _inventory.TemporaryCountChanged -= RefreshCompleteButton;
        _inventory.Changed -= RefreshStorageCount;
        _currency.Changed -= RefreshCurrency;
        _inventoryUI.ExpansionCellClicked -= HandleExpansionCellClicked;

        foreach (RewardCardUI card in _rewardCards)
        {
            card.Clicked -= HandleCardClicked;
        }
    }

    /// <summary>보물 상자 보상 1회 대기열에 추가 (화면이 닫혀 있으면 열고, 대기 중인 레벨업이 있으면 그 뒤에 표시)</summary>
    public void RequestChestReward()
    {
        _pendingChestCount++;
        if (!IsOpen)
        {
            Open();
        }
    }

    // 한 번에 여러 레벨이 오르면 이벤트가 연속 발생하므로 횟수만 쌓고 화면은 한 번만 연다
    private void HandleLevelChanged(int level)
    {
        _pendingCount++;
        if (!IsOpen)
        {
            Open();
        }
    }

    private void Open()
    {
        _gameSession.Pause();
        StartRound();
        RefreshStorageCount();
        _panelRoot.SetActive(true);
    }

    /// <summary>
    /// 확장 가능한 칸 수 이내로 남은 확장 수를 설정하고, Organizing 단계면 즉시 확장 모드 시작
    /// 보물 상자 외 1칸 확장권 등 다른 확장 출처도 같은 경로로 호출
    /// </summary>
    public void BeginExpansion(int count)
    {
        _remainingExpansionCount = Mathf.Min(count, _inventory.CountExpansionCandidates());

        if (_phase == RewardPhase.Organizing)
            _inventoryUI.SetExpansionMode(_remainingExpansionCount > 0);

        RefreshExpansionGuide();
        RefreshCompleteButton();
    }

    // 확장 모드(Organizing 중 남은 확장 수 있음)일 때만 남은 수 안내 표시
    private void RefreshExpansionGuide()
    {
        if (_expansionGuideText == null)
            return;

        bool isExpanding = _phase == RewardPhase.Organizing && _remainingExpansionCount > 0;
        _expansionGuideText.gameObject.SetActive(isExpanding);

        if (isExpanding)
            _expansionGuideText.text = string.Format(EXPANSION_GUIDE_FORMAT, _remainingExpansionCount);
    }

    // 다음 회차 종류(레벨업 우선)를 정하고 이전 회차 후보·선택을 이어받지 않고 새 후보로 Selecting 시작
    private void StartRound()
    {
        _isShowingChest = _pendingCount == 0;
        _phase = RewardPhase.Selecting;
        BeginExpansion(_isShowingChest ? _chestExpansionCount : 0); // Selecting이므로 확장 모드는 Organizing 진입 시 켜짐
        _selectedIndices.Clear();
        _candidates = GenerateCandidates(null);
        RefreshRewardCards();
        ApplyPhase();
    }

    // 이미 예약된 확장 수를 빼고 남는 칸이 없으면 확장권은 효과가 없으므로 후보 풀에서 제외
    private List<InventoryData> GenerateCandidates(ICollection<InventoryData> previous)
    {
        InventoryData[] pool = _inventory.CountExpansionCandidates() > _remainingExpansionCount
            ? _rewardPool
            : Array.FindAll(_rewardPool, data => data.ExpendableEffectType != ExpendableEffectType.ExpandCell);

        return RewardCandidateGenerator.Generate(pool, CardCount,
            _weaponWeight, _passiveWeight, _expendableWeight, previous, _random);
    }

    // 선택된 카드는 취소, 미선택 카드는 필요 수에 도달하지 않았을 때만 선택
    private void HandleCardClicked(RewardCardUI card)
    {
        if (_phase != RewardPhase.Selecting)
            return;

        int index = Array.IndexOf(_rewardCards, card);

        if (_selectedIndices.Remove(index))
        {
            card.SetSelected(false);
        }
        else if (_selectedIndices.Count < RequiredSelectCount)
        {
            _selectedIndices.Add(index);
            card.SetSelected(true);
        }

        RefreshSelection();
    }

    // 새 후보가 없으면 차감하지 않고, 차감에 성공한 경우에만 후보 교체 (버튼·재화 텍스트는 Currency.Changed로 갱신)
    private void HandleRerollClicked()
    {
        if (_phase != RewardPhase.Selecting || _currency.Balance < _rerollCost)
            return;

        List<InventoryData> candidates = GenerateCandidates(_candidates);

        if (candidates.Count == 0)
            return;

        if (!_currency.TrySpend(_rerollCost))
            return;

        _candidates = candidates;
        _selectedIndices.Clear();
        RefreshRewardCards();
    }

    // 연속 클릭으로 중복 지급되지 않도록 Phase를 먼저 전환한 뒤 소비 아이템은 즉시 적용, 나머지는 Temporary에 생성
    private void HandleConfirmClicked()
    {
        if (_phase != RewardPhase.Selecting || _selectedIndices.Count != RequiredSelectCount)
            return;

        _phase = RewardPhase.Organizing;

        List<InventoryData> rewards = new List<InventoryData>();

        foreach (int index in _selectedIndices)
        {
            InventoryData data = _candidates[index];

            if (data.ItemType == ItemType.Expendables)
            {
                ApplyExpendable(data);
                continue;
            }

            rewards.Add(data);
        }

        for (int i = 0; i < rewards.Count; i++)
        {
            AddToTemporary(rewards[i], i, rewards.Count);
        }

        _selectedIndices.Clear();
        _candidates.Clear();
        RefreshRewardCards(); // 카드는 프리팹 구조상 _selectingGroup 밖이라 후보 0개로 갱신해 모두 비활성화
        ApplyPhase();
    }

    // 소비 아이템은 Temporary에 두지 않고 즉시 적용 (Organizing 전환 후 호출되므로 확장 모드도 바로 켜짐)
    private void ApplyExpendable(InventoryData data)
    {
        switch (data.ExpendableEffectType)
        {
            case ExpendableEffectType.Heal:
                _playerHealth.Heal(data.ExpendableEffectValue);
                break;
            case ExpendableEffectType.ExpandCell:
                BeginExpansion(_remainingExpansionCount + Mathf.RoundToInt(data.ExpendableEffectValue));
                break;
        }
    }

    // Temporary 영역을 가로로 slotCount등분한 slot번째 칸 중심에 회전 0으로 배치 (영역 밖으로 나가지 않게 보정)
    private void AddToTemporary(InventoryData data, int slot, int slotCount)
    {
        Inventory.ItemInstance item = new Inventory.ItemInstance(data);

        Rect area = _temporaryArea.rect;
        Vector2 slotCenter = new Vector2(
            Mathf.Lerp(area.xMin, area.xMax, (slot + SLOT_CENTER_RATIO) / slotCount), area.center.y);
        Vector2 itemSize = _inventoryUI.GetItemSize(_inventory.GetRotatedShape(item, 0));

        // shortcut: Screen Space Overlay 전제(InventoryItemUI와 동일)로 월드 좌표를 화면 좌표로 사용, Camera 캔버스 전환 시 카메라 전달 필요
        Vector2 position = _inventoryUI.ClampToTemporary(
            _temporaryArea.TransformPoint(slotCenter), itemSize, 0, null);

        // UI는 초기화 시 데이터의 영역·좌표로 표시하므로 데이터를 먼저 Temporary로 이동
        _inventory.MoveToTemporary(item, position, 0);

        InventoryItemUI itemUI = Instantiate(_itemPrefab, _itemLayer);
        itemUI.Initialize(item, _inventory, _inventoryUI, _sellPopup, _tooltip);
    }

    // 표시 중인 회차 종류의 횟수만 차감 (표시 중 도착한 다른 종류 요청이 잘못 차감되지 않도록), 남은 회차가 있으면 다음 회차 표시
    private void HandleCompleteClicked()
    {
        // 버튼 비활성화와 별개로 외부 호출 방어
        if (_phase != RewardPhase.Organizing || _inventory.TemporaryCount > 0 || _remainingExpansionCount > 0)
            return;

        if (_isShowingChest)
        {
            _pendingChestCount--;
        }
        else
        {
            _pendingCount--;
        }

        if (_pendingCount == 0 && _pendingChestCount == 0)
        {
            _inventoryUI.SetExpansionMode(false);
            InventoryItemUI.IsDragEnabled = true;
            RefreshExpansionGuide();
            _panelRoot.SetActive(false);
            _gameSession.Resume();
            return;
        }

        StartRound();
    }

    // 해제마다 남은 수 차감, 남은 수가 있어도 후보가 없으면 완료가 막히지 않도록 0으로 정리
    private void HandleExpansionCellClicked(Vector2Int cell)
    {
        if (_phase != RewardPhase.Organizing || _remainingExpansionCount == 0 || !_inventory.TryUnlockCell(cell))
            return;

        _remainingExpansionCount--;

        if (_inventory.CountExpansionCandidates() == 0)
            _remainingExpansionCount = 0;

        if (_remainingExpansionCount == 0)
            _inventoryUI.SetExpansionMode(false);

        RefreshExpansionGuide();
        RefreshCompleteButton();
    }

    // Selecting은 카드 선택만, Organizing은 Temporary 정리·회전·드래그만 가능하도록 UI 전환
    private void ApplyPhase()
    {
        bool isSelecting = _phase == RewardPhase.Selecting;

        _selectingGroup.SetActive(isSelecting);
        _temporaryArea.gameObject.SetActive(!isSelecting);
        _completeButton.gameObject.SetActive(!isSelecting);
        InventoryItemUI.IsDragEnabled = !isSelecting; // hover 툴팁은 유지하고 드래그만 차단
        _inventoryUI.SetRotateEnabled(!isSelecting);
        _inventoryUI.SetExpansionMode(!isSelecting && _remainingExpansionCount > 0);
        RefreshExpansionGuide();

        RefreshCompleteButton();
        RefreshSelection();
        RefreshCurrency(_currency.Balance);
    }

    // Temporary 아이템이 남아 있으면 완료 차단, Selecting 중에는 경고 숨김
    private void RefreshCompleteButton()
    {
        bool isOrganizing = _phase == RewardPhase.Organizing;

        _completeButton.interactable = isOrganizing && _inventory.TemporaryCount == 0 && _remainingExpansionCount == 0;
        _temporaryWarning.SetActive(isOrganizing && _inventory.TemporaryCount > 0);
    }

    private void RefreshStorageCount()
    {
        _storageCountText.text = string.Format(STORAGE_COUNT_FORMAT, _inventory.StorageCount, _inventory.StorageCapacity);
    }

    private void RefreshCurrency(int balance)
    {
        _currencyText.text = string.Format(CURRENCY_FORMAT, balance);
        _rerollButton.interactable = _phase == RewardPhase.Selecting && balance >= _rerollCost;
    }

    private void RefreshSelection()
    {
        _selectionCountText.text = string.Format(SELECTION_COUNT_FORMAT, _selectedIndices.Count, RequiredSelectCount);
        _confirmButton.interactable = _phase == RewardPhase.Selecting && _selectedIndices.Count == RequiredSelectCount;
        _selectionGuideText.text = _selectedIndices.Count < RequiredSelectCount
            ? string.Format(SELECTION_GUIDE_FORMAT, _candidates.Count, RequiredSelectCount)
            : string.Format(SELECTION_DONE_FORMAT, RequiredSelectCount);
    }

    // 후보 수만큼 카드 활성화, 후보·선택 상태 표시
    private void RefreshRewardCards()
    {
        for (int i = 0; i < _rewardCards.Length; i++)
        {
            bool hasCandidate = i < _candidates.Count;
            _rewardCards[i].gameObject.SetActive(hasCandidate);

            if (!hasCandidate)
                continue;

            _rewardCards[i].Bind(_candidates[i]);
            _rewardCards[i].SetSelected(_selectedIndices.Contains(i));
        }

        RefreshSelection();
    }
}

/// <summary>보상 화면 단계 (후보 선택 → 선택 아이템 Temporary 정리)</summary>
public enum RewardPhase
{
    Selecting,
    Organizing,
}

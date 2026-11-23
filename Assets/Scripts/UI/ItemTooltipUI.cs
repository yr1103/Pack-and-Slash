using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 보유 아이템·보상 후보 공통 툴팁 하나를 커서 옆에 표시 (마지막으로 Show한 source만 Hide 가능)
/// </summary>
public class ItemTooltipUI : MonoBehaviour
{
    private const int REWARD_LEVEL = 1; // 보상은 항상 1레벨로 지급
    private const string LEVEL_FORMAT = "Lv.{0}";
    private const string ATTACK_FORMAT = "공격력 {0}";
    private const string EQUIPPED_FORMAT = "장착: {0}";
    private const string UNEQUIPPED_FORMAT = "해제 시: {0}";
    private const string SELL_PRICE_FORMAT = "판매가 {0}";
    private const string UNSELLABLE_TEXT = "판매 불가";
    private const string WEAPON_LABEL = "무기";
    private const string PASSIVE_LABEL = "패시브";
    private const string EXPENDABLE_LABEL = "소비";
    private const string ESSENTIAL_LABEL = "고유";

    private static readonly Vector2 _defaultPivot = new Vector2(0f, 1f); // 커서 오른쪽 아래로 펼침

    [SerializeField] private Inventory _inventory;
    [SerializeField] private RectTransform _tooltipRect; // 툴팁 패널 루트 (활성/비활성 대상, 자신과 같아도 됨)
    [SerializeField] private Image _icon;
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _typeText;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Text _attackText;
    [SerializeField] private GameObject _effectContainer;
    [SerializeField] private Text _equippedEffectText;
    [SerializeField] private Text _unequippedBuffText;
    [SerializeField] private Text _unequippedDebuffText;
    [SerializeField] private Text _sellPriceText;
    [SerializeField] private Vector2 _cursorOffset = new Vector2(16f, -16f); // 커서를 가리지 않도록 띄우는 화면 픽셀 거리

    private object _source; // 현재 표시 요청자, null이면 숨김 상태

    private void Awake()
    {
        _tooltipRect.gameObject.SetActive(false);
    }

    // 패널이 닫혔다 열릴 때 이전 툴팁이 남지 않도록 초기화
    private void OnDisable()
    {
        _source = null;

        // 루트가 자신이면 이미 비활성화 중이라 다시 끄지 않음
        if (_tooltipRect.gameObject != gameObject)
            _tooltipRect.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_source != null)
            UpdatePosition();
    }

    /// <summary>보유 아이템 표시 (현재 레벨·판매가)</summary>
    public void Show(Inventory.ItemInstance item, object source)
    {
        Display(item.Data, item.Level, _inventory.GetSellPrice(item), source);
    }

    /// <summary>보상 후보 표시 (1레벨 기준 판매가)</summary>
    public void Show(InventoryData data, object source)
    {
        Display(data, REWARD_LEVEL, _inventory.GetSellPrice(data, REWARD_LEVEL), source);
    }

    /// <summary>source가 현재 표시 요청자일 때만 숨김 (다른 아이템 Enter 직후 이전 아이템 Exit 무시)</summary>
    public void Hide(object source)
    {
        if (_source != source)
            return;

        _source = null;
        _tooltipRect.gameObject.SetActive(false);
    }

    // 값이 없는 줄은 비활성화해 레이아웃 공간 제거
    private void Display(InventoryData data, int level, int sellPrice, object source)
    {
        _source = source;

        _icon.sprite = data.Icon;
        _nameText.text = data.ItemName;
        _typeText.text = GetTypeLabel(data.ItemType);
        _descriptionText.text = data.Description;

        _levelText.gameObject.SetActive(data.MaxLevel > 1);
        _levelText.text = string.Format(LEVEL_FORMAT, level);

        // shortcut: 레벨별 공격력 계산이 없어 기본 ProjectileDamage 표시, 무기 레벨 성장 구현 시 레벨 반영
        bool isWeapon = data.WeaponDefinition != null;
        _attackText.gameObject.SetActive(isWeapon);
        if (isWeapon)
            _attackText.text = string.Format(ATTACK_FORMAT, data.WeaponDefinition.ProjectileDamage);

        bool hasEquipped = SetEffectLine(_equippedEffectText, EQUIPPED_FORMAT, data.EquippedEffect);
        bool hasBuff = SetEffectLine(_unequippedBuffText, UNEQUIPPED_FORMAT, data.UnequippedBuff);
        bool hasDebuff = SetEffectLine(_unequippedDebuffText, UNEQUIPPED_FORMAT, data.UnequippedDebuff);
        _effectContainer.SetActive(hasEquipped || hasBuff || hasDebuff);

        _sellPriceText.text = data.IsSellable ? string.Format(SELL_PRICE_FORMAT, sellPrice) : UNSELLABLE_TEXT;

        _tooltipRect.gameObject.SetActive(true);

        // 줄 활성 상태가 바뀐 직후 크기를 반영해야 위치 보정이 맞음
        LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);
        UpdatePosition();
    }

    // 값이 비어 있으면 줄을 숨기고 false 반환
    private static bool SetEffectLine(Text text, string format, string value)
    {
        bool hasValue = !string.IsNullOrEmpty(value);
        text.gameObject.SetActive(hasValue);

        if (hasValue)
            text.text = string.Format(format, value);

        return hasValue;
    }

    private static string GetTypeLabel(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Weapon:
                return WEAPON_LABEL;
            case ItemType.Passive:
                return PASSIVE_LABEL;
            case ItemType.Expendables:
                return EXPENDABLE_LABEL;
            default:
                return ESSENTIAL_LABEL;
        }
    }

    // 기본은 커서 오른쪽 아래, 화면을 넘는 축만 커서 반대편으로 뒤집은 뒤 화면 안으로 보정
    // shortcut: Screen Space Overlay 전제(InventoryItemUI와 동일)로 월드 좌표를 화면 좌표로 사용, Camera 캔버스 전환 시 카메라 기준 변환 필요
    private void UpdatePosition()
    {
        if (Mouse.current == null)
            return;

        Vector2 cursor = Mouse.current.position.ReadValue();
        Vector2 size = Vector2.Scale(_tooltipRect.rect.size, _tooltipRect.lossyScale);
        Vector2 pivot = _defaultPivot;
        Vector2 offset = _cursorOffset;

        if (cursor.x + offset.x + size.x > Screen.width)
        {
            pivot.x = 1f; // 오른쪽 끝을 커서에 맞춰 왼쪽으로 펼침
            offset.x = -offset.x;
        }

        if (cursor.y + offset.y - size.y < 0f)
        {
            pivot.y = 0f; // 아래쪽 끝을 커서에 맞춰 위로 펼침
            offset.y = -offset.y;
        }

        Vector2 position = cursor + offset;
        position.x = Mathf.Clamp(position.x, pivot.x * size.x, Screen.width - (1f - pivot.x) * size.x);
        position.y = Mathf.Clamp(position.y, pivot.y * size.y, Screen.height - (1f - pivot.y) * size.y);

        _tooltipRect.pivot = pivot;
        _tooltipRect.position = position;
    }
}

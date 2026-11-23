using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판매 영역에 드롭한 아이템의 판매 확인, 확인 시 판매·재화 지급·아이템 UI 비활성화, 취소 시 변경 없음
/// </summary>
public class SellConfirmPopup : MonoBehaviour
{
    private const string MESSAGE_FORMAT = "{0} Lv.{1}을(를) 판매하시겠습니까? (+{2})"; // {0} 이름, {1} 레벨, {2} 판매가

    [SerializeField] private GameObject _popupRoot; // 전체 화면 블로커 포함, 이 컴포넌트와 다른 오브젝트여야 버튼 리스너가 유지됨
    [SerializeField] private Text _messageText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Inventory _inventory;
    [SerializeField] private Currency _currency;

    private Inventory.ItemInstance _pendingItem;
    private InventoryItemUI _pendingItemUI;

    /// <summary>판매 확인 팝업 표시 여부</summary>
    public bool IsOpen => _popupRoot.activeSelf;

    private void Awake()
    {
        _popupRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _confirmButton.onClick.AddListener(HandleConfirmClicked);
        _cancelButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        _confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        _cancelButton.onClick.RemoveListener(Close);
    }

    /// <summary>판매 확인 팝업 표시, 데이터는 확인 버튼을 누를 때만 변경</summary>
    /// <param name="itemUI">판매 성공 시 비활성화할 표시 오브젝트</param>
    public void Open(Inventory.ItemInstance item, InventoryItemUI itemUI)
    {
        _pendingItem = item;
        _pendingItemUI = itemUI;

        _messageText.text = string.Format(MESSAGE_FORMAT,
            item.Data.ItemName, item.Level, _inventory.GetSellPrice(item));
        _popupRoot.SetActive(true);
    }

    // 판매로 아이템 상태가 바뀌기 전에 가격 확정
    private void HandleConfirmClicked()
    {
        int price = _inventory.GetSellPrice(_pendingItem);

        if (_inventory.TrySell(_pendingItem))
        {
            _currency.Add(price);
            _pendingItemUI.gameObject.SetActive(false);
        }

        Close();
    }

    private void Close()
    {
        _pendingItem = null;
        _pendingItemUI = null;
        _popupRoot.SetActive(false);
    }
}

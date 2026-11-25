using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 보상 후보 카드 1장 표시 (아이콘·이름·종류·선택 색), 클릭 시 Clicked로 통지, 마우스를 올리면 공통 툴팁 표시
/// </summary>
public class RewardCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const string NAME_FORMAT = "{0} Lv.1"; // {0} 아이템 이름, 보상은 항상 1레벨로 지급

    [SerializeField] private Button _button;
    [SerializeField] private Image _background; // 선택 여부에 따라 색 변경
    [SerializeField] private Image _icon;
    [SerializeField] private Text _nameText;
    [SerializeField, FormerlySerializedAs("_descriptionText")] private Text _typeText;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private ItemTooltipUI _tooltip; // 미연결이면 툴팁 없음

    private InventoryData _data;

    /// <summary>카드 클릭 시 자신 전달 (선택 판정은 구독자가 담당)</summary>
    public event Action<RewardCardUI> Clicked;

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClicked);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClicked);

        // 확정·회차 전환으로 비활성화되면 Exit가 오지 않으므로 직접 숨김
        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    /// <summary>후보 아이템 정보 표시</summary>
    public void Bind(InventoryData data)
    {
        _data = data;
        _icon.sprite = data.Icon;
        // 레벨 없는 아이템(소비 등, MaxLevel 1)은 툴팁과 같은 기준으로 레벨 생략
        _nameText.text = data.MaxLevel > 1 ? string.Format(NAME_FORMAT, data.ItemName) : data.ItemName;
        _typeText.text = ItemTooltipUI.GetTypeLabel(data.ItemType);
    }

    /// <summary>선택 상태 색 표시</summary>
    public void SetSelected(bool isSelected)
    {
        _background.color = isSelected ? _selectedColor : _normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null || _data == null)
            return;

        _tooltip.Show(_data, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip != null)
            _tooltip.Hide(this);
    }

    private void HandleClicked()
    {
        Clicked?.Invoke(this);
    }
}

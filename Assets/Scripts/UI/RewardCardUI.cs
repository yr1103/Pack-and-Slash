using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보상 후보 카드 1장 표시 (아이콘·이름·설명·선택 색), 클릭 시 Clicked로 통지
/// </summary>
public class RewardCardUI : MonoBehaviour
{
    private const string NAME_FORMAT = "{0} Lv.1"; // {0} 아이템 이름, 보상은 항상 1레벨로 지급

    [SerializeField] private Button _button;
    [SerializeField] private Image _background; // 선택 여부에 따라 색 변경
    [SerializeField] private Image _icon;
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.85f, 0.3f);

    /// <summary>카드 클릭 시 자신 전달 (선택 판정은 구독자가 담당)</summary>
    public event Action<RewardCardUI> Clicked;

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClicked);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClicked);
    }

    /// <summary>후보 아이템 정보 표시</summary>
    public void Bind(InventoryData data)
    {
        _icon.sprite = data.Icon;
        _nameText.text = string.Format(NAME_FORMAT, data.ItemName);
        _descriptionText.text = data.Description;
    }

    /// <summary>선택 상태 색 표시</summary>
    public void SetSelected(bool isSelected)
    {
        _background.color = isSelected ? _selectedColor : _normalColor;
    }

    private void HandleClicked()
    {
        Clicked?.Invoke(this);
    }
}

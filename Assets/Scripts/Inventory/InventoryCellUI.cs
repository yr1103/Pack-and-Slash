using System;
using UnityEngine;
using UnityEngine.UI;

public class InventoryCellUI : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _itemIcon;

    private Vector2Int _position;

    public void Init(Vector2Int position, Action<Vector2Int> onClick)
    {
        _position = position;
        _button.onClick.AddListener(() => onClick?.Invoke(_position));
    }

    public void Refresh(Inventory.InventoryCell cell)
    {
        if (cell.Occupant == null)
        {
            _itemIcon.enabled = false;
            return;
        }

        _itemIcon.sprite = cell.Occupant.Data.Icon;
        _itemIcon.enabled = true;
    }
}
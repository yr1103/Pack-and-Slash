using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 시작 아이템을 Inventory에 지급하고, Inventory에 배치된 무기 아이템과 WeaponLoadout 장착 무기를 일치시킴
/// </summary>
public class InventoryWeaponSync : MonoBehaviour
{
    [SerializeField] private Inventory _inventory;
    [SerializeField] private WeaponLoadout _loadout;
    [SerializeField] private InventoryItemUI _itemPrefab; // LevelUpPanel과 같은 아이템 UI 프리팹
    [SerializeField] private RectTransform _itemLayer;
    [SerializeField] private InventoryUI _inventoryUI;
    [SerializeField] private SellConfirmPopup _sellPopup;
    [SerializeField] private ItemTooltipUI _tooltip;

    // 배치된 무기 아이템 → 장착 무기 (계속 배치된 동안 같은 WeaponInstance 유지)
    private readonly Dictionary<Inventory.ItemInstance, WeaponInstance> _equippedWeapons =
        new Dictionary<Inventory.ItemInstance, WeaponInstance>();

    private void OnEnable()
    {
        _inventory.Changed += SyncWeapons;
    }

    private void OnDisable()
    {
        _inventory.Changed -= SyncWeapons;
    }

    // Inventory.Awake의 그리드 초기화 이후 배치, 장착은 배치 시 Changed → SyncWeapons 경로로만 처리
    private void Start()
    {
        CharacterDefinition character = _loadout.Character;
        GrantStartItem(character.StartWeaponItem);
        GrantStartItem(character.UniqueItem);
    }

    /// <summary>배치된 무기와 장착 무기를 맞춤 (새 배치는 장착, 배치에서 빠지면 해제, 유지된 무기는 같은 인스턴스에 레벨·스탯만 갱신)</summary>
    public void SyncWeapons()
    {
        HashSet<Inventory.ItemInstance> placedItems = _inventory.GetPlacedItems();

        // 해제보다 먼저 갱신해야 주무기 해제 시 재선정이 갱신된 레벨 기준으로 이뤄짐
        foreach (Inventory.ItemInstance item in placedItems)
        {
            WeaponDefinition definition = item.Data.WeaponDefinition;

            if (item.Data.ItemType != ItemType.Weapon || definition == null)
                continue;

            if (!_equippedWeapons.TryGetValue(item, out WeaponInstance weapon))
            {
                weapon = _loadout.Equip(definition, item.Level);
                _equippedWeapons.Add(item, weapon);
            }

            Inventory.WeaponStats stats = _inventory.GetWeaponStats(item);
            weapon.ApplyStats(item.Level, stats.Damage, stats.FireInterval, stats.SpreadAngle);
        }

        // 순회 중 Dictionary를 수정할 수 없어 모은 뒤 해제
        List<Inventory.ItemInstance> removedItems = new List<Inventory.ItemInstance>();

        foreach (Inventory.ItemInstance item in _equippedWeapons.Keys)
        {
            if (!placedItems.Contains(item))
                removedItems.Add(item);
        }

        foreach (Inventory.ItemInstance item in removedItems)
        {
            _loadout.Unequip(_equippedWeapons[item].InstanceId);
            _equippedWeapons.Remove(item);
        }
    }

    // 활성 영역 밖 위치는 잠금 칸이 포함돼 CanPlace가 실패하므로, 전체 그리드를 y → x 순으로 훑으면 활성 영역 (3,1)부터 훑는 것과 같은 결과
    private void GrantStartItem(InventoryData data)
    {
        Inventory.ItemInstance item = new Inventory.ItemInstance(data); // Lv.1, 회전 0

        for (int y = 0; y < _inventory.GridHeight; y++)
        {
            for (int x = 0; x < _inventory.GridWidth; x++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (!_inventory.CanPlace(item, position, item.Rotation))
                    continue;

                _inventory.PlaceItem(item, position);
                CreateItemUI(item);
                return;
            }
        }

        Debug.LogError($"[InventoryWeaponSync] 시작 아이템 배치 실패 : {data.ItemName}");
    }

    // ItemLayer는 시작 시 비활성 패널 아래라 바로 생성하면 Awake가 실행되지 않아 Initialize에서 _image가 null, 활성인 자신 아래에서 생성(Awake 실행) 후 옮김
    private void CreateItemUI(Inventory.ItemInstance item)
    {
        InventoryItemUI itemUI = Instantiate(_itemPrefab, transform);
        itemUI.transform.SetParent(_itemLayer, false);
        itemUI.Initialize(item, _inventory, _inventoryUI, _sellPopup, _tooltip);
    }
}

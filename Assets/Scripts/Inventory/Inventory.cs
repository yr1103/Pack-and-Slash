using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private const int Width = 10;
    private const int Height = 6;

    /// <summary>보관함 슬롯 수 (슬롯 1칸 = 아이템 1개, Shape 무관)</summary>
    public const int STORAGE_CAPACITY = 5;

    private const int SELL_PRICE_PER_LEVEL = 1; // 판매가 = 레벨 × 이 값 (특수 재화)

    private const float WEAPON_LEVEL_BONUS_PER_LEVEL = 0.1f; // 무기 레벨 배율 = 1 + (레벨 - 1) × 이 값 (복리 아님)
    private const float ADJACENCY_BONUS_PER_LEVEL = 0.1f; // 패시브 인접 효과 = 레벨 × 이 값
    private const int LEVEL3_EFFECT_LEVEL = 3; // Lv.3 특수 효과 활성 최소 레벨
    private const float MAX_FIRE_INTERVAL_REDUCTION = 0.5f; // 발사 간격 감소율 상한 (간격이 0 이하가 되지 않도록)
    private const float MAX_SPREAD_REDUCTION = 0.7f; // 탄퍼짐 감소율 상한

    // 인접 판정 방향 (상하좌우만, 대각선 제외)
    private static readonly Vector2Int[] _adjacentDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    private InventoryCell[,] cells = new InventoryCell[Width, Height];
    private ItemInstance[] _storageSlots = new ItemInstance[STORAGE_CAPACITY];
    private CharacterDefinition _character;

    /// <summary>Temporary 영역에 있는 아이템 수 (1 이상이면 레벨업 완료 차단)</summary>
    public int TemporaryCount { get; private set; }

    /// <summary>보관함에 들어 있는 아이템 수</summary>
    public int StorageCount
    {
        get
        {
            int count = 0;

            foreach (ItemInstance storedItem in _storageSlots)
            {
                if (storedItem != null) count++;
            }

            return count;
        }
    }

    public int GridWidth => Width; // 잠금 칸 포함
    public int GridHeight => Height;

    /// <summary>고유 효과 기준 캐릭터 (null이면 고유 효과 없음), 설정 시 스탯이 바뀌므로 Changed 1회 발생</summary>
    public CharacterDefinition Character
    {
        get => _character;
        set
        {
            _character = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Temporary 영역에 아이템이 들어오거나 나갈 때만, 도착 영역 기록 후 발생 (Temporary 내 좌표 이동은 제외)</summary>
    public event Action TemporaryCountChanged;

    /// <summary>보관함에 아이템이 들어오거나 나갈 때만, 도착 영역 기록 후 발생</summary>
    public event Action StorageCountChanged;

    /// <summary>공개 조작(배치·이동·회전·제거·보관·Temporary 이동·판매·합성·캐릭터 설정)이 성공해 상태가 확정된 뒤 1회 발생, 실패·조회 시 미발생</summary>
    public event Action Changed;

    // 실제 아이템 하나
    public class ItemInstance
    {
        public InventoryData Data;
        public int Level = 1;
        public Vector2Int Position;
        public int Rotation;

        /// <summary>아이템이 현재 있는 영역 (점유·카운트와 함께 바뀌어야 하므로 Inventory API로만 변경)</summary>
        public ItemLocation Location;

        /// <summary>Temporary 영역 기준 로컬 좌표 (Location이 Temporary일 때만 유효)</summary>
        public Vector2 TemporaryPosition;

        /// <summary>Inventory 그리드에 배치되어 셀을 점유한 상태만 true (Storage·Temporary·None은 false)</summary>
        public bool IsPlaced => Location == ItemLocation.Inventory;

        public ItemInstance(InventoryData data)
        {
            Data = data;
        }
    }

    // 인벤토리 한 칸
    public class InventoryCell
    {
        public bool IsAvailable;
        public ItemInstance hasEquipped;
    }

    /// <summary>최종 무기 스탯 (GetWeaponStats 결과, SO 원본과 별개인 값)</summary>
    public readonly struct WeaponStats
    {
        public float Damage { get; }
        public float FireInterval { get; }
        public float SpreadAngle { get; } // 부채꼴 전체 각도(도)
        public bool RandomSpread { get; } // true면 확산각 안 무작위 방향, false면 균등 분배

        public WeaponStats(float damage, float fireInterval, float spreadAngle, bool randomSpread)
        {
            Damage = damage;
            FireInterval = fireInterval;
            SpreadAngle = spreadAngle;
            RandomSpread = randomSpread;
        }
    }

    private void Awake()
    {
        InitializeGrid();
    }

    // 60칸 생성, 중앙 활성화
    private void InitializeGrid()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                cells[x, y] = new InventoryCell();
                // 4x4 활성화
                cells[x, y].IsAvailable =
                    x >= 3 && x <= 6 &&
                    y >= 1 && y <= 4;
            }
        }
    }

    // 회전된 Shape 좌표 계산
    /// <summary>회전 적용 후 최소 좌표가 (0,0)이 되도록 정규화한 Shape (UI 표시 위치 계산에도 사용)</summary>
    public Vector2Int[] GetRotatedShape(
        ItemInstance item, int rotation)
    {
        Vector2Int[] original = item.Data.ItemShape;
        Vector2Int[] result = new Vector2Int[original.Length];

        for (int i = 0; i < original.Length; i++)
        {
            Vector2Int pos = original[i];

            for (int r = 0; r < rotation; r++)
            {
                pos = new Vector2Int(-pos.y, pos.x);
            }

            result[i] = pos;
        }

        // 음수 좌표 정규화
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (Vector2Int pos in result)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.y < minY) minY = pos.y;
        }

        for (int i = 0; i < result.Length; i++)
        {
            result[i] -= new Vector2Int(minX, minY);
        }

        return result;
    }

    // 배치 가능한지 검사
    public bool CanPlace(
        ItemInstance item, Vector2Int position, int rotation)
    {
        if (item == null || item.Data == null ||
            item.Data.ItemShape == null ||
            item.Data.ItemShape.Length == 0)
            return false;

        Vector2Int[] shape = GetRotatedShape(item, rotation);

        foreach (Vector2Int offset in shape)
        {
            int x = position.x + offset.x;
            int y = position.y + offset.y;

            // 배열 범위 검사
            if (x < 0 || x >= Width ||
                y < 0 || y >= Height)
                return false;

            // 잠금 셀 검사
            if (!cells[x, y].IsAvailable)
                return false;

            // 다른 아이템이 점유 중인지 검사
            ItemInstance occupant = cells[x, y].hasEquipped;

            if (occupant != null && occupant != item)
                return false;
        }

        return true;
    }

    // 실제 셀 점유
    private void FillCells(ItemInstance item)
    {
        Vector2Int[] shape =
            GetRotatedShape(item, item.Rotation);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int pos = item.Position + offset;
            cells[pos.x, pos.y].hasEquipped = item;
        }
    }

    // 아이템 최초 배치
    public bool PlaceItem(ItemInstance item, Vector2Int position)
    {
        // Storage·Temporary 아이템은 출발 영역 해제가 필요하므로 TryPlace 사용
        if (item == null || item.Location != ItemLocation.None)
            return false;

        if (!CanPlace(item, position, item.Rotation))
            return false;

        item.Position = position;
        item.Location = ItemLocation.Inventory;

        FillCells(item);
        Changed?.Invoke();
        return true;
    }

    // 기존 점유 셀 해제
    public void RemoveItem(ItemInstance item)
    {
        if (item == null || !item.IsPlaced)
            return;

        ClearCells(item);
        item.Location = ItemLocation.None;
        Changed?.Invoke();
    }

    // 내부 조작용 셀 해제, 중간 상태에서 Changed가 발생하지 않도록 RemoveItem과 분리
    private void ClearCells(ItemInstance item)
    {
        Vector2Int[] shape =
            GetRotatedShape(item, item.Rotation);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int pos = item.Position + offset;
            cells[pos.x, pos.y].hasEquipped = null;
        }
    }

    // 아이템 이동
    public bool MoveItem(ItemInstance item, Vector2Int newPosition)
    {
        if (item == null || !item.IsPlaced)
            return false;

        if (!CanPlace(item, newPosition, item.Rotation))
            return false;

        ClearCells(item);

        item.Position = newPosition;
        FillCells(item);

        Changed?.Invoke();
        return true;
    }

    // 90도 회전
    /// <summary>Inventory는 충돌 검사 후 회전, Temporary는 검사 없이 회전, Storage·None은 실패</summary>
    public bool RotateItem(ItemInstance item)
    {
        if (item == null)
            return false;

        int nextRotation = (item.Rotation + 1) % 4;

        // Temporary는 셀을 점유하지 않아 충돌 검사 없음
        if (item.Location == ItemLocation.Temporary)
        {
            item.Rotation = nextRotation;
            Changed?.Invoke();
            return true;
        }

        if (!item.IsPlaced)
            return false;

        if (!CanPlace(item, item.Position, nextRotation))
            return false;

        // 해제는 기존 회전 기준이라 회전 갱신보다 먼저
        ClearCells(item);

        item.Rotation = nextRotation;
        FillCells(item);

        Changed?.Invoke();
        return true;
    }

    /// <summary>출발 영역과 무관하게 지정 회전으로 Inventory에 배치, 실패 시 원래 영역·위치·회전 유지</summary>
    /// <param name="rotation">배치 후 회전 (Inventory 내 이동이면 기존 회전을 대체)</param>
    public bool TryPlace(ItemInstance item, Vector2Int position, int rotation)
    {
        if (item == null)
            return false;

        // 자기 점유 셀은 허용되므로 Inventory 내 이동도 같은 검사로 충분
        if (!CanPlace(item, position, rotation))
            return false;

        ItemLocation previousLocation = item.Location;

        // 출발 셀 해제는 기존 회전 기준이라 회전 갱신보다 먼저
        ReleaseLocation(item);

        item.Position = position;
        item.Rotation = rotation;
        item.Location = ItemLocation.Inventory;
        FillCells(item);

        NotifyLocationChanged(previousLocation, ItemLocation.Inventory);
        Changed?.Invoke();
        return true;
    }

    /// <summary>첫 빈 보관함 슬롯에 넣고 회전 0으로 초기화, 빈 슬롯이 없으면 원래 영역 상태 유지</summary>
    /// <returns>이미 보관함에 있으면 변화 없이 true</returns>
    public bool TryStore(ItemInstance item)
    {
        if (item == null)
            return false;

        if (item.Location == ItemLocation.Storage)
            return true;

        int slotIndex = Array.IndexOf(_storageSlots, null);

        if (slotIndex < 0)
            return false;

        ItemLocation previousLocation = item.Location;
        ReleaseLocation(item);

        _storageSlots[slotIndex] = item;
        item.Rotation = 0;
        item.Location = ItemLocation.Storage;

        NotifyLocationChanged(previousLocation, ItemLocation.Storage);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Temporary 영역으로 이동하며 회전 저장, 이미 Temporary면 좌표·회전만 갱신</summary>
    /// <param name="temporaryPosition">Temporary 영역 기준 로컬 좌표</param>
    /// <param name="rotation">Temporary 표시 회전 (셀을 점유하지 않아 충돌 검사 없음)</param>
    public void MoveToTemporary(ItemInstance item, Vector2 temporaryPosition, int rotation)
    {
        if (item == null)
            return;

        ItemLocation previousLocation = item.Location;

        if (previousLocation != ItemLocation.Temporary)
        {
            ReleaseLocation(item);

            item.Location = ItemLocation.Temporary;
            TemporaryCount++;
        }

        item.TemporaryPosition = temporaryPosition;
        item.Rotation = rotation;

        NotifyLocationChanged(previousLocation, ItemLocation.Temporary);
        Changed?.Invoke();
    }

    /// <summary>판매 시 지급할 특수 재화 (레벨 × 레벨당 판매가)</summary>
    /// <returns>item 또는 item.Data가 null이면 0 (CanSell과 동일한 null 기준)</returns>
    public int GetSellPrice(ItemInstance item)
    {
        if (item == null)
            return 0;

        return GetSellPrice(item.Data, item.Level);
    }

    /// <summary>보유하지 않은 아이템(보상 후보 등)의 레벨 기준 판매가</summary>
    /// <returns>data가 null이면 0</returns>
    public int GetSellPrice(InventoryData data, int level)
    {
        if (data == null)
            return 0;

        return level * SELL_PRICE_PER_LEVEL;
    }

    /// <summary>판매 가능 여부 (판매 불가 아이템·어느 영역에도 없는 아이템은 false)</summary>
    public bool CanSell(ItemInstance item)
    {
        if (item == null || item.Data == null)
            return false;

        return item.Data.IsSellable && item.Location != ItemLocation.None;
    }

    /// <summary>출발 영역을 해제하고 Location을 None으로 전환, 재화 지급은 호출부 담당 (GetSellPrice 사용)</summary>
    /// <returns>CanSell 실패 시 false, 상태 변화 없음</returns>
    public bool TrySell(ItemInstance item)
    {
        if (!CanSell(item))
            return false;

        ItemLocation previousLocation = item.Location;
        ReleaseLocation(item);

        NotifyLocationChanged(previousLocation, ItemLocation.None);
        Changed?.Invoke();
        return true;
    }

    /// <summary>합성 가능 여부 (동일 Id·동일 Level·Level &lt; MaxLevel, 서로 다른 두 아이템이 모두 영역에 있음), 상태 변경 없음</summary>
    public bool CanMerge(ItemInstance source, ItemInstance target)
    {
        if (source == null || target == null || source.Data == null || target.Data == null)
            return false;

        if (source == target || source.Location == ItemLocation.None || target.Location == ItemLocation.None)
            return false;

        return source.Data.Id == target.Data.Id && source.Level == target.Level && target.Level < target.Data.MaxLevel;
    }

    /// <summary>source를 소비(Location None)하고 target Level +1, target의 영역·위치·회전은 유지</summary>
    /// <returns>CanMerge 실패 시 false, 상태 변화 없음</returns>
    public bool TryMerge(ItemInstance source, ItemInstance target)
    {
        if (!CanMerge(source, target))
            return false;

        ItemLocation previousLocation = source.Location;
        ReleaseLocation(source);
        target.Level++;

        NotifyLocationChanged(previousLocation, ItemLocation.None);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Inventory에 배치된 item의 점유 셀과 상하좌우로 맞닿은 다른 아이템 (회전 반영, 중복 없음)</summary>
    /// <returns>item이 Inventory에 배치되지 않았으면 빈 집합</returns>
    public HashSet<ItemInstance> GetAdjacentItems(ItemInstance item)
    {
        HashSet<ItemInstance> adjacentItems = new HashSet<ItemInstance>();

        if (item == null || item.Data == null || !item.IsPlaced)
            return adjacentItems;

        foreach (Vector2Int offset in GetRotatedShape(item, item.Rotation))
        {
            Vector2Int cell = item.Position + offset;

            foreach (Vector2Int direction in _adjacentDirections)
            {
                Vector2Int neighbor = cell + direction;

                if (neighbor.x < 0 || neighbor.x >= Width ||
                    neighbor.y < 0 || neighbor.y >= Height)
                    continue;

                ItemInstance occupant = cells[neighbor.x, neighbor.y].hasEquipped;

                if (occupant != null && occupant != item)
                    adjacentItems.Add(occupant);
            }
        }

        return adjacentItems;
    }

    /// <summary>Inventory 그리드에 배치된 아이템 (중복 없음, 호출 시점마다 새 집합)</summary>
    public HashSet<ItemInstance> GetPlacedItems()
    {
        HashSet<ItemInstance> placedItems = new HashSet<ItemInstance>();

        foreach (InventoryCell cell in cells)
        {
            if (cell.hasEquipped != null)
                placedItems.Add(cell.hasEquipped);
        }

        return placedItems;
    }

    /// <summary>배치된 무기에 맞닿은 패시브 중 effectType 효과의 레벨 기준 합계 (상한 미적용, 호출 시점마다 재계산)</summary>
    /// <returns>weapon이 배치된 무기가 아니거나 effectType이 None이면 0</returns>
    public float GetAdjacencyBonus(ItemInstance weapon, AdjacencyEffectType effectType)
    {
        if (weapon == null || weapon.Data == null || weapon.Data.ItemType != ItemType.Weapon ||
            effectType == AdjacencyEffectType.None)
            return 0f;

        float bonus = 0f;

        // 같은 패시브가 여러 셀로 닿아도 집합이라 1회만 합산
        foreach (ItemInstance adjacentItem in GetAdjacentItems(weapon))
        {
            if (adjacentItem.Data == null || adjacentItem.Data.ItemType != ItemType.Passive ||
                adjacentItem.Data.AdjacencyEffectType != effectType)
                continue;

            bonus += GetPassiveAdjacencyBonus(adjacentItem.Level);
        }

        return bonus;
    }

    /// <summary>GetAdjacencyBonus에 효과별 상한 적용 (발사 간격 0.5, 탄퍼짐 0.7, 공격력 상한 없음)</summary>
    public float GetEffectiveAdjacencyBonus(ItemInstance weapon, AdjacencyEffectType effectType)
    {
        float bonus = GetAdjacencyBonus(weapon, effectType);

        switch (effectType)
        {
            case AdjacencyEffectType.FireInterval:
                return Mathf.Clamp(bonus, 0f, MAX_FIRE_INTERVAL_REDUCTION);

            case AdjacencyEffectType.Spread:
                return Mathf.Clamp(bonus, 0f, MAX_SPREAD_REDUCTION);

            default:
                return bonus;
        }
    }

    /// <summary>레벨·인접 효과(상한 적용)·캐릭터 고유 효과를 반영한 최종 무기 스탯, 툴팁·전투 공통 계산 경로 (미배치 아이템은 인접 효과 0)</summary>
    /// <returns>weapon·Data·WeaponDefinition 중 하나라도 null이면 모든 값 0</returns>
    public WeaponStats GetWeaponStats(ItemInstance weapon)
    {
        if (weapon == null || weapon.Data == null || weapon.Data.WeaponDefinition == null)
            return default;

        WeaponDefinition definition = weapon.Data.WeaponDefinition;
        float damage = definition.ProjectileDamage * GetWeaponLevelMultiplier(weapon.Level) *
                       (1f + GetEffectiveAdjacencyBonus(weapon, AdjacencyEffectType.Damage)) *
                       (1f + GetUniqueDamageBonus(weapon));
        float fireInterval = definition.FireInterval *
                             (1f - GetEffectiveAdjacencyBonus(weapon, AdjacencyEffectType.FireInterval)) /
                             (1f + GetUniqueAttackSpeedBonus(weapon));

        // 냉각기 인접과 캐릭터 정확도는 같은 탄퍼짐 감소율이라 합친 뒤 상한 적용
        float spreadReduction = Mathf.Clamp(
            GetAdjacencyBonus(weapon, AdjacencyEffectType.Spread) + GetCharacterAccuracyBonus(weapon),
            0f, MAX_SPREAD_REDUCTION);
        float uniqueSpreadAngle = GetUniqueSpreadAngle(weapon);
        bool useUniqueSpread = uniqueSpreadAngle > 0f;
        float spreadAngle = (useUniqueSpread ? uniqueSpreadAngle : definition.SpreadAngle) * (1f - spreadReduction);
        bool randomSpread = useUniqueSpread || definition.RandomSpread;

        return new WeaponStats(damage, fireInterval, spreadAngle, randomSpread);
    }

    /// <summary>현재 캐릭터의 고유 아이템과 같은 데이터의 아이템이 Inventory에 배치되어 있는지 (Storage·Temporary는 해제)</summary>
    /// <returns>Character 또는 UniqueItem이 null이면 false</returns>
    public bool IsUniqueItemEquipped()
    {
        if (_character == null || _character.UniqueItem == null)
            return false;

        foreach (ItemInstance item in GetPlacedItems())
        {
            if (item.Data == _character.UniqueItem)
                return true;
        }

        return false;
    }

    /// <summary>고유 아이템 장착 중 대상 무기 공격력 보너스 (적용 중이 아니면 0)</summary>
    public float GetUniqueDamageBonus(ItemInstance weapon)
    {
        return IsUniqueTargetWeapon(weapon) && IsUniqueItemEquipped() ? _character.EquippedDamageBonus : 0f;
    }

    /// <summary>고유 아이템 해제 중 대상 무기 공격속도 보너스 (적용 중이 아니면 0)</summary>
    public float GetUniqueAttackSpeedBonus(ItemInstance weapon)
    {
        return IsUniqueTargetWeapon(weapon) && !IsUniqueItemEquipped() ? _character.UnequippedAttackSpeedBonus : 0f;
    }

    /// <summary>고유 아이템 해제 중 대상 무기 전체 확산각(도), 0보다 크면 기본 각도 대신 사용하고 랜덤 탄퍼짐 (적용 중이 아니면 0)</summary>
    public float GetUniqueSpreadAngle(ItemInstance weapon)
    {
        return IsUniqueTargetWeapon(weapon) && !IsUniqueItemEquipped() ? _character.UnequippedSpreadAngle : 0f;
    }

    /// <summary>대상 무기 정확도(탄퍼짐 감소율) 합 = 기본 특성 + 해제 중 해제 보너스 (상한 미적용, 대상 외 무기는 0)</summary>
    public float GetCharacterAccuracyBonus(ItemInstance weapon)
    {
        if (!IsUniqueTargetWeapon(weapon))
            return 0f;

        float bonus = _character.BaseAccuracyBonus;

        if (!IsUniqueItemEquipped())
            bonus += _character.UnequippedAccuracyBonus;

        return bonus;
    }

    /// <summary>Lv.3 효과가 활성인(Lv.3 이상·Inventory 배치) 패시브 중 effectType 효과 수치 합 (인접 무관, 상한 미적용)</summary>
    /// <returns>effectType이 None이면 0</returns>
    public float GetLevel3Bonus(Level3EffectType effectType)
    {
        if (effectType == Level3EffectType.None)
            return 0f;

        float bonus = 0f;

        foreach (ItemInstance item in GetPlacedItems())
        {
            if (IsLevel3EffectActive(item) && item.Data.Level3EffectType == effectType)
                bonus += item.Data.Level3EffectValue;
        }

        return bonus;
    }

    // 다른 캐릭터의 고유 효과가 섞이지 않도록 현재 Character의 대상 무기 종류만 비교
    private bool IsUniqueTargetWeapon(ItemInstance weapon)
    {
        return _character != null && weapon != null && weapon.Data != null && weapon.Data.WeaponDefinition != null &&
               weapon.Data.WeaponDefinition.WeaponType == _character.UniqueWeaponType;
    }

    /// <summary>패시브 1개가 주는 인접 효과 수치 (레벨 × 0.1)</summary>
    public static float GetPassiveAdjacencyBonus(int level)
    {
        return level * ADJACENCY_BONUS_PER_LEVEL;
    }

    /// <summary>무기 레벨 공격력 배율 (1 + (레벨 - 1) × 0.1)</summary>
    public static float GetWeaponLevelMultiplier(int level)
    {
        return 1f + (level - 1) * WEAPON_LEVEL_BONUS_PER_LEVEL;
    }

    /// <summary>Lv.3 특수 효과 활성 여부 (무기는 Lv.3 이상, 패시브는 Lv.3 이상이면서 Inventory 배치, 그 외 false)</summary>
    public bool IsLevel3EffectActive(ItemInstance item)
    {
        if (item == null || item.Data == null || item.Level < LEVEL3_EFFECT_LEVEL)
            return false;

        return item.Data.ItemType == ItemType.Weapon ||
               (item.Data.ItemType == ItemType.Passive && item.IsPlaced);
    }

    /// <summary>보관함 슬롯 아이템 (비어 있으면 null)</summary>
    public ItemInstance GetStorageItem(int index)
    {
        return _storageSlots[index];
    }

    /// <summary>아이템이 들어 있는 보관함 슬롯 인덱스 (없으면 -1)</summary>
    public int GetStorageIndex(ItemInstance item)
    {
        return Array.IndexOf(_storageSlots, item);
    }

    // 도착 영역 기록 전 출발 영역 점유 해제 (구독자가 전환 중간 상태를 보지 않도록 이벤트는 NotifyLocationChanged에서 발생)
    private void ReleaseLocation(ItemInstance item)
    {
        switch (item.Location)
        {
            case ItemLocation.Inventory:
                ClearCells(item);
                break;

            case ItemLocation.Storage:
                _storageSlots[GetStorageIndex(item)] = null;
                break;

            case ItemLocation.Temporary:
                TemporaryCount--;
                break;
        }

        item.Location = ItemLocation.None;
    }

    // 도착 영역 기록 후 호출, 출발·도착 중 한쪽만 Temporary·Storage인 경우에만 해당 개수 변화 통지
    private void NotifyLocationChanged(ItemLocation previousLocation, ItemLocation newLocation)
    {
        if (previousLocation == newLocation)
            return;

        if (previousLocation == ItemLocation.Temporary || newLocation == ItemLocation.Temporary)
            TemporaryCountChanged?.Invoke();

        if (previousLocation == ItemLocation.Storage || newLocation == ItemLocation.Storage)
            StorageCountChanged?.Invoke();
    }
}

/// <summary>
/// 아이템이 현재 있는 영역 (None = 생성 후 아직 어디에도 없음)
/// </summary>
public enum ItemLocation
{
    None,
    Inventory,
    Storage,
    Temporary,
}

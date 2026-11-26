using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private const int Width = 10;
    private const int Height = 6;

    // 보관함 슬롯 1칸 = 아이템 1개 (Shape 무관)
    private const int BASE_STORAGE_CAPACITY = 5; // 기본 보관함 한도
    private const int MAX_STORAGE_CAPACITY = 7; // 슬롯 배열 크기 (기본 + 고유 아이템 장착 확장 최대)

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

    // 조끼 전용 칸 (EquippedUnlocksVestCells 캐릭터만 예약: 고유 아이템 장착 중에만 열리고 예약 해제 전까지 확장 후보 제외, 그 외 캐릭터는 일반 칸)
    private static readonly Vector2Int[] _vestCells =
    {
        new Vector2Int(4, 5), new Vector2Int(5, 5),
    };

    private InventoryCell[,] cells = new InventoryCell[Width, Height];
    private ItemInstance[] _storageSlots = new ItemInstance[MAX_STORAGE_CAPACITY];
    private CharacterDefinition _character;
    private bool _isVestReservationReleased; // 조끼 판매로 전용 칸 예약 해제 (이후 일반 확장 칸으로 취급)

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

    /// <summary>현재 보관함 한도 (기본 5 + 고유 아이템 장착 중 캐릭터 보관함 보너스, 슬롯 배열 크기 이하)</summary>
    public int StorageCapacity => GetStorageCapacity(IsUniqueItemEquipped());

    public int GridWidth => Width; // 잠금 칸 포함
    public int GridHeight => Height;

    /// <summary>특성·고유 효과 기준 캐릭터 (null이면 효과 없음), 설정 시 스탯·조끼 전용 칸(예약 초기화)이 바뀌므로 Changed 1회 발생</summary>
    public CharacterDefinition Character
    {
        get => _character;
        set
        {
            _character = value;
            _isVestReservationReleased = false;
            RefreshVestCells();
            Changed?.Invoke();
        }
    }

    /// <summary>Temporary 영역에 아이템이 들어오거나 나갈 때만, 도착 영역 기록 후 발생 (Temporary 내 좌표 이동은 제외)</summary>
    public event Action TemporaryCountChanged;

    /// <summary>보관함에 아이템이 들어오거나 나갈 때만, 도착 영역 기록 후 발생</summary>
    public event Action StorageCountChanged;

    /// <summary>공개 조작(배치·이동·회전·제거·보관·Temporary 이동·판매·합성·캐릭터 설정·칸 해제)이 성공해 상태가 확정된 뒤 1회 발생, 실패·조회 시 미발생</summary>
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
        public int GunCount { get; } // 이 인스턴스의 총기 자루 수 (발사 1회에 자루마다 동시 발사)
        public float KnockbackStrength { get; } // 탄 적중 시 넉백 속도, 0 = 넉백 없음
        public int ProjectilesPerShot { get; } // 자루 하나가 발사 1회에 쏘는 탄 수

        public WeaponStats(float damage, float fireInterval, float spreadAngle, bool randomSpread, int gunCount,
            float knockbackStrength, int projectilesPerShot)
        {
            Damage = damage;
            FireInterval = fireInterval;
            SpreadAngle = spreadAngle;
            RandomSpread = randomSpread;
            GunCount = gunCount;
            KnockbackStrength = knockbackStrength;
            ProjectilesPerShot = projectilesPerShot;
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

    /// <summary>어느 영역에도 없는(None) 아이템을 Inventory에 최초 배치, 고유 아이템 장착 상태에 맞춰 조끼 전용 칸 갱신</summary>
    /// <returns>item이 null·None이 아님·배치 불가 시 false, 상태 변화 없음</returns>
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
        RefreshVestCells();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Inventory에 배치된 아이템의 점유 셀을 해제하고 Location을 None으로 전환, 조끼 전용 칸 갱신</summary>
    /// <returns>item이 null·미배치·고유 아이템 해제 거부 시 false, 상태 변화 없음</returns>
    public bool RemoveItem(ItemInstance item)
    {
        if (item == null || !item.IsPlaced || !CanReleaseUniqueEffect(item))
            return false;

        ClearCells(item);
        item.Location = ItemLocation.None;
        RefreshVestCells();
        Changed?.Invoke();
        return true;
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
        RefreshVestCells();

        NotifyLocationChanged(previousLocation, ItemLocation.Inventory);
        Changed?.Invoke();
        return true;
    }

    /// <summary>현재 한도 안의 첫 빈 보관함 슬롯에 넣고 회전 0으로 초기화, 빈 슬롯이 없거나 고유 아이템 해제 거부 시 원래 영역 상태 유지</summary>
    /// <returns>이미 보관함에 있으면 변화 없이 true</returns>
    public bool TryStore(ItemInstance item)
    {
        if (item == null)
            return false;

        if (item.Location == ItemLocation.Storage)
            return true;

        if (!CanReleaseUniqueEffect(item))
            return false;

        // 고유 아이템 자신을 넣어 장착이 풀리면 줄어든 한도 안의 슬롯만 사용
        int capacity = GetStorageCapacity(IsUniqueItemEquippedExcept(item));
        int slotIndex = Array.IndexOf(_storageSlots, null, 0, capacity);

        if (slotIndex < 0)
            return false;

        ItemLocation previousLocation = item.Location;
        ReleaseLocation(item);

        _storageSlots[slotIndex] = item;
        item.Rotation = 0;
        item.Location = ItemLocation.Storage;
        RefreshVestCells();

        NotifyLocationChanged(previousLocation, ItemLocation.Storage);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Temporary 영역으로 이동하며 회전 저장, 이미 Temporary면 좌표·회전만 갱신</summary>
    /// <param name="temporaryPosition">Temporary 영역 기준 로컬 좌표</param>
    /// <param name="rotation">Temporary 표시 회전 (셀을 점유하지 않아 충돌 검사 없음)</param>
    /// <returns>item이 null이거나 고유 아이템 해제 거부 시 false, 상태 변화 없음</returns>
    public bool MoveToTemporary(ItemInstance item, Vector2 temporaryPosition, int rotation)
    {
        if (item == null || !CanReleaseUniqueEffect(item))
            return false;

        ItemLocation previousLocation = item.Location;

        if (previousLocation != ItemLocation.Temporary)
        {
            ReleaseLocation(item);

            item.Location = ItemLocation.Temporary;
            TemporaryCount++;
        }

        item.TemporaryPosition = temporaryPosition;
        item.Rotation = rotation;
        RefreshVestCells();

        NotifyLocationChanged(previousLocation, ItemLocation.Temporary);
        Changed?.Invoke();
        return true;
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

    /// <summary>출발 영역을 해제하고 Location을 None으로 전환, 재화 지급은 호출부 담당 (GetSellPrice 사용), 조끼를 팔면 전용 칸 예약 해제</summary>
    /// <returns>CanSell 실패·고유 아이템 해제 거부 시 false, 상태 변화 없음</returns>
    public bool TrySell(ItemInstance item)
    {
        if (!CanSell(item) || !CanReleaseUniqueEffect(item))
            return false;

        ItemLocation previousLocation = item.Location;
        ReleaseLocation(item);
        RefreshVestCells();

        // 잠금 반영 후 해제해야 전용 칸이 잠긴 채 확장 후보로 남음, 합성은 같은 Id의 target이 남아 해제 대상 아님
        if (_character != null && _character.EquippedUnlocksVestCells && item.Data == _character.UniqueItem &&
            !HasUniqueItemInstance())
            _isVestReservationReleased = true;

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
    /// <returns>CanMerge 실패·source의 고유 아이템 해제 거부 시 false, 상태 변화 없음</returns>
    public bool TryMerge(ItemInstance source, ItemInstance target)
    {
        if (!CanMerge(source, target) || !CanReleaseUniqueEffect(source))
            return false;

        ItemLocation previousLocation = source.Location;
        ReleaseLocation(source);
        target.Level++;
        RefreshVestCells();

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

    /// <summary>레벨·인접 효과(상한 적용)·캐릭터 특성·고유 아이템 효과를 반영한 최종 무기 스탯, 툴팁·전투 공통 계산 경로 (미배치 아이템은 인접 효과 0)</summary>
    /// <returns>weapon·Data·WeaponDefinition 중 하나라도 null이면 모든 값 0</returns>
    public WeaponStats GetWeaponStats(ItemInstance weapon)
    {
        if (weapon == null || weapon.Data == null || weapon.Data.WeaponDefinition == null)
            return default;

        WeaponDefinition definition = weapon.Data.WeaponDefinition;
        float damage = definition.ProjectileDamage * GetWeaponLevelMultiplier(weapon.Level) *
                       (1f + GetEffectiveAdjacencyBonus(weapon, AdjacencyEffectType.Damage)) *
                       (1f + GetTraitDamageBonus(weapon));
        float fireInterval = definition.FireInterval *
                             (1f - GetEffectiveAdjacencyBonus(weapon, AdjacencyEffectType.FireInterval)) /
                             (1f + GetUniqueAttackSpeedBonus(weapon));

        // 냉각기 인접·캐릭터 정확도·무기 Lv.3 감소는 같은 탄퍼짐 감소율이라 합친 뒤 상한 적용
        float spreadReduction = Mathf.Clamp(
            GetAdjacencyBonus(weapon, AdjacencyEffectType.Spread) + GetCharacterAccuracyBonus(weapon) +
            (IsLevel3EffectActive(weapon) ? definition.Level3SpreadReduction : 0f),
            0f, MAX_SPREAD_REDUCTION);
        float uniqueSpreadAngle = GetUniqueSpreadAngle(weapon);
        bool useUniqueSpread = uniqueSpreadAngle > 0f;
        float spreadAngle = (useUniqueSpread ? uniqueSpreadAngle : definition.SpreadAngle) * (1f - spreadReduction);
        bool randomSpread = useUniqueSpread || definition.RandomSpread;

        int gunCount = IsLevel3EffectActive(weapon) ? definition.Level3GunCount : 1;
        float knockbackStrength = IsLevel3EffectActive(weapon) ? definition.Level3KnockbackStrength : 0f;
        int projectilesPerShot = definition.ProjectilesPerShot + GetTraitProjectileBonus(weapon);

        return new WeaponStats(damage, fireInterval, spreadAngle, randomSpread, gunCount, knockbackStrength,
            projectilesPerShot);
    }

    /// <summary>현재 캐릭터의 고유 아이템과 같은 데이터의 아이템이 Inventory에 배치되어 있는지 (Storage·Temporary는 해제)</summary>
    /// <returns>Character 또는 UniqueItem이 null이면 false</returns>
    public bool IsUniqueItemEquipped()
    {
        return IsUniqueItemEquippedExcept(null);
    }

    // excluded를 뺀 배치 아이템으로 판정, 아이템이 Inventory를 떠난 뒤의 장착 상태를 미리 확인할 때 사용
    private bool IsUniqueItemEquippedExcept(ItemInstance excluded)
    {
        if (_character == null || _character.UniqueItem == null)
            return false;

        foreach (ItemInstance item in GetPlacedItems())
        {
            if (item != excluded && item.Data == _character.UniqueItem)
                return true;
        }

        return false;
    }

    /// <summary>대상 무기 캐릭터 특성 공격력 보너스 (고유 아이템 탈착 무관, 대상 외 무기는 0)</summary>
    public float GetTraitDamageBonus(ItemInstance weapon)
    {
        return IsUniqueTargetWeapon(weapon) ? _character.TraitDamageBonus : 0f;
    }

    /// <summary>대상 무기 캐릭터 특성 발사당 총알 추가 수 (고유 아이템 탈착 무관, 대상 외 무기는 0)</summary>
    public int GetTraitProjectileBonus(ItemInstance weapon)
    {
        return IsUniqueTargetWeapon(weapon) ? _character.TraitProjectileBonus : 0;
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

    /// <summary>대상 무기 정확도(탄퍼짐 감소율) 합 = 특성 + 장착 중 장착 보너스 (상한 미적용, 대상 외 무기는 0)</summary>
    public float GetCharacterAccuracyBonus(ItemInstance weapon)
    {
        if (!IsUniqueTargetWeapon(weapon))
            return 0f;

        float bonus = _character.TraitAccuracyBonus;

        if (IsUniqueItemEquipped())
            bonus += _character.EquippedAccuracyBonus;

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

    /// <summary>사용 가능(잠금 해제) 칸인지 (범위 밖이면 false)</summary>
    public bool IsCellAvailable(Vector2Int cell)
    {
        return IsInGrid(cell) && cells[cell.x, cell.y].IsAvailable;
    }

    /// <summary>확장 후보 칸인지 (잠긴 칸 중 상하좌우에 사용 가능한 칸이 있음, 조끼 전용 칸은 예약 중이면 제외)</summary>
    public bool IsExpansionCandidate(Vector2Int cell)
    {
        if (!IsInGrid(cell) || cells[cell.x, cell.y].IsAvailable)
            return false;

        if (IsVestCell(cell))
            return false;

        foreach (Vector2Int direction in _adjacentDirections)
        {
            if (IsCellAvailable(cell + direction))
                return true;
        }

        return false;
    }

    /// <summary>조끼 전용 칸으로 예약된 칸인지 (EquippedUnlocksVestCells 캐릭터이고 조끼 판매로 예약 해제 전이면 조끼 보관으로 잠긴 동안에도 true, 열림 여부는 IsCellAvailable로 판단)</summary>
    public bool IsVestCell(Vector2Int cell)
    {
        return IsVestReservationActive() && Array.IndexOf(_vestCells, cell) >= 0;
    }

    /// <summary>현재 확장 후보 칸 수</summary>
    public int CountExpansionCandidates()
    {
        int count = 0;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (IsExpansionCandidate(new Vector2Int(x, y)))
                    count++;
            }
        }

        return count;
    }

    /// <summary>확장 후보 칸을 사용 가능으로 해제, 성공 시 Changed 1회 발생</summary>
    /// <returns>후보가 아니면 false, 상태 변화 없음</returns>
    public bool TryUnlockCell(Vector2Int cell)
    {
        if (!IsExpansionCandidate(cell))
            return false;

        cells[cell.x, cell.y].IsAvailable = true;
        Changed?.Invoke();
        return true;
    }

    private static bool IsInGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
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

    private int GetStorageCapacity(bool isUniqueItemEquipped)
    {
        int bonus = isUniqueItemEquipped ? _character.EquippedStorageBonus : 0;
        return Mathf.Min(BASE_STORAGE_CAPACITY + bonus, MAX_STORAGE_CAPACITY);
    }

    /// <summary>item을 Inventory 밖으로 옮겨도 되는지 조회, 상태 변경 없음 (RemoveItem·TryStore·MoveToTemporary·TrySell·TryMerge 공통 해제 거부 기준, UI 거부 안내도 조회)</summary>
    /// <returns>장착 효과가 풀릴 때 잠길 조끼 전용 칸(item 자신 외)·보관함 확장 슬롯에 아이템이 있으면 false</returns>
    public bool CanReleaseUniqueEffect(ItemInstance item)
    {
        if (!item.IsPlaced || !IsUniqueItemEquipped() || IsUniqueItemEquippedExcept(item))
            return true;

        // 예약이 없으면 전용 칸은 잠기지 않는 일반 칸
        if (IsVestReservationActive())
        {
            foreach (Vector2Int cell in _vestCells)
            {
                ItemInstance occupant = cells[cell.x, cell.y].hasEquipped;

                if (occupant != null && occupant != item)
                    return false;
            }
        }

        for (int i = BASE_STORAGE_CAPACITY; i < _storageSlots.Length; i++)
        {
            if (_storageSlots[i] != null)
                return false;
        }

        return true;
    }

    // shortcut: Temporary 아이템은 목록이 없어 미확인, 고유 아이템은 보상 후보 제외로 1개뿐이라 충분 (Temporary 목록이 생기면 포함)
    private bool HasUniqueItemInstance()
    {
        if (IsUniqueItemEquipped())
            return true;

        foreach (ItemInstance storedItem in _storageSlots)
        {
            if (storedItem != null && storedItem.Data == _character.UniqueItem)
                return true;
        }

        return false;
    }

    // 조끼 전용 칸 예약 중인지 (조끼 캐릭터이고 조끼 판매 전)
    private bool IsVestReservationActive()
    {
        return _character != null && _character.EquippedUnlocksVestCells && !_isVestReservationReleased;
    }

    // 예약 중이면 조끼 전용 칸을 현재 장착 상태에 맞춤 (예약이 없으면 확장으로 연 상태 유지), 상태 확정 후 모든 통지(NotifyLocationChanged·Changed)보다 먼저 호출해 중간 상태를 통지하지 않음
    private void RefreshVestCells()
    {
        if (!IsVestReservationActive())
            return;

        bool isUnlocked = IsUniqueItemEquipped();

        foreach (Vector2Int cell in _vestCells)
        {
            cells[cell.x, cell.y].IsAvailable = isUnlocked;
        }
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

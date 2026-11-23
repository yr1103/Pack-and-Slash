using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    private const int Width = 10;
    private const int Height = 6;

    /// <summary>보관함 슬롯 수 (슬롯 1칸 = 아이템 1개, Shape 무관)</summary>
    public const int STORAGE_CAPACITY = 5;

    private const int SELL_PRICE_PER_LEVEL = 1; // 판매가 = 레벨 × 이 값 (특수 재화)

    private InventoryCell[,] cells = new InventoryCell[Width, Height];
    private ItemInstance[] _storageSlots = new ItemInstance[STORAGE_CAPACITY];

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

    /// <summary>Temporary 영역에 아이템이 들어오거나 나갈 때만, 도착 영역 기록 후 발생 (Temporary 내 좌표 이동은 제외)</summary>
    public event Action TemporaryCountChanged;

    /// <summary>보관함에 아이템이 들어오거나 나갈 때만, 도착 영역 기록 후 발생</summary>
    public event Action StorageCountChanged;

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
        return true;
    }

    // 기존 점유 셀 해제
    public void RemoveItem(ItemInstance item)
    {
        if (item == null || !item.IsPlaced)
            return;

        Vector2Int[] shape =
            GetRotatedShape(item, item.Rotation);

        foreach (Vector2Int offset in shape)
        {
            Vector2Int pos = item.Position + offset;
            cells[pos.x, pos.y].hasEquipped = null;
        }

        item.Location = ItemLocation.None;
    }

    // 아이템 이동
    public bool MoveItem(ItemInstance item, Vector2Int newPosition)
    {
        if (item == null || !item.IsPlaced)
            return false;

        if (!CanPlace(item, newPosition, item.Rotation))
            return false;

        RemoveItem(item);

        item.Position = newPosition;
        item.Location = ItemLocation.Inventory;
        FillCells(item);

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
            return true;
        }

        if (!item.IsPlaced)
            return false;

        if (!CanPlace(item, item.Position, nextRotation))
            return false;

        RemoveItem(item);

        item.Rotation = nextRotation;
        item.Location = ItemLocation.Inventory;
        FillCells(item);

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
        return true;
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
                RemoveItem(item);
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

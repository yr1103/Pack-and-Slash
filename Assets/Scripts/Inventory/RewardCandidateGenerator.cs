using System.Collections.Generic;

/// <summary>
/// 보상 후보 목록 생성 (종류별 가중치로 종류 선택 후 종류 내 균등 추첨, 직전 후보와 겹치지 않는 아이템 우선)
/// </summary>
public static class RewardCandidateGenerator
{
    private const int DRAWABLE_TYPE_COUNT = 3; // 추첨 대상 종류 수 (ItemType 정수값 0~2 = Weapon·Passive·Expendables)

    /// <summary>후보 풀에서 서로 다른 아이템을 최대 count개 추첨</summary>
    /// <param name="pool">null·Essential·판매 불가(진화 무기)·가중치 0 이하 종류는 제외, 같은 Id는 처음 것만 사용</param>
    /// <param name="previous">직전 후보 (없으면 null), 여기 없는 아이템을 먼저 뽑고 부족할 때만 포함</param>
    /// <returns>가능한 아이템이 count보다 적으면 가능한 수만큼만 반환</returns>
    public static List<InventoryData> Generate(IReadOnlyList<InventoryData> pool, int count,
        float weaponWeight, float passiveWeight, float expendableWeight,
        ICollection<InventoryData> previous, System.Random random)
    {
        float[] weights = { weaponWeight, passiveWeight, expendableWeight };
        List<InventoryData>[] freshBuckets = CreateBuckets();
        List<InventoryData>[] previousBuckets = CreateBuckets();
        HashSet<int> addedIds = new HashSet<int>();

        foreach (InventoryData data in pool)
        {
            if (data == null || data.ItemType == ItemType.Essential || !data.IsSellable)
                continue;

            int typeIndex = (int)data.ItemType;

            if (weights[typeIndex] <= 0f || !addedIds.Add(data.Id))
                continue;

            bool isPrevious = previous != null && previous.Contains(data);
            (isPrevious ? previousBuckets : freshBuckets)[typeIndex].Add(data);
        }

        List<InventoryData> result = new List<InventoryData>();
        DrawInto(result, freshBuckets, weights, count, random);
        DrawInto(result, previousBuckets, weights, count, random);
        return result;
    }

    // 뽑을 때마다 버킷에서 제거하므로 반복은 버킷 아이템 수 이하로 끝남
    private static void DrawInto(List<InventoryData> result, List<InventoryData>[] buckets, float[] weights,
        int count, System.Random random)
    {
        while (result.Count < count)
        {
            // 남은 아이템이 있는 종류끼리만 가중치 정규화
            float totalWeight = 0f;

            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].Count > 0)
                    totalWeight += weights[i];
            }

            if (totalWeight <= 0f)
                return;

            double roll = random.NextDouble() * totalWeight;
            int typeIndex = 0;

            // 부동소수 오차로 끝까지 가도 마지막 비어 있지 않은 종류가 선택됨
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].Count == 0)
                    continue;

                typeIndex = i;
                roll -= weights[i];

                if (roll < 0)
                    break;
            }

            List<InventoryData> bucket = buckets[typeIndex];
            int index = random.Next(bucket.Count);
            result.Add(bucket[index]);

            // 순서 무관이라 마지막 원소로 덮어쓴 뒤 제거
            bucket[index] = bucket[bucket.Count - 1];
            bucket.RemoveAt(bucket.Count - 1);
        }
    }

    private static List<InventoryData>[] CreateBuckets()
    {
        List<InventoryData>[] buckets = new List<InventoryData>[DRAWABLE_TYPE_COUNT];

        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new List<InventoryData>();
        }

        return buckets;
    }
}

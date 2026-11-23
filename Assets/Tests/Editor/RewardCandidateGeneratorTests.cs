using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 보상 후보 생성 시 Essential·판매 불가 제외, 목록 내 중복 없음, 풀 부족 시 가능한 수만 반환,
/// 직전 후보와 겹치지 않는 아이템 우선, 가중치 0 종류 미등장 검증
/// </summary>
public class RewardCandidateGeneratorTests
{
    private const string IS_SELLABLE_FIELD = "_isSellable";
    private const int SEED_COUNT = 50; // 무작위 추첨 결과를 여러 시드로 반복 확인

    private static readonly Vector2Int[] _singleCellShape = { Vector2Int.zero };

    private readonly List<InventoryData> _createdData = new List<InventoryData>();

    [TearDown]
    public void TearDown()
    {
        foreach (InventoryData data in _createdData)
        {
            Object.DestroyImmediate(data);
        }

        _createdData.Clear();
    }

    [Test]
    public void Generate_ExcludesNullEssentialAndUnsellable()
    {
        InventoryData weapon = CreateData(1, ItemType.Weapon);
        InventoryData essential = CreateData(2, ItemType.Essential);
        InventoryData evolvedWeapon = CreateData(3, ItemType.Weapon);
        SetSellable(evolvedWeapon, false);
        InventoryData[] pool = { weapon, null, essential, evolvedWeapon };

        List<InventoryData> result = Generate(pool, 5, null, new System.Random(0));

        CollectionAssert.AreEqual(new[] { weapon }, result);
    }

    [Test]
    public void Generate_NoDuplicateIdsInList()
    {
        InventoryData pistol = CreateData(1, ItemType.Weapon);
        InventoryData pistolCopy = CreateData(1, ItemType.Weapon); // 같은 Id 다른 에셋
        InventoryData ring = CreateData(2, ItemType.Passive);
        InventoryData[] pool = { pistol, pistol, pistolCopy, ring };

        for (int seed = 0; seed < SEED_COUNT; seed++)
        {
            List<InventoryData> result = Generate(pool, 5, null, new System.Random(seed));

            Assert.AreEqual(2, result.Count);
            Assert.AreNotEqual(result[0].Id, result[1].Id);
        }
    }

    [Test]
    public void Generate_PoolSmallerThanCount_ReturnsAvailableCount()
    {
        InventoryData[] pool = { CreateData(1, ItemType.Weapon), CreateData(2, ItemType.Expendables) };

        List<InventoryData> result = Generate(pool, 5, null, new System.Random(0));

        Assert.AreEqual(2, result.Count);
    }

    [Test]
    public void Generate_EnoughFreshItems_ExcludesAllPrevious()
    {
        InventoryData[] pool = CreatePool(6);
        List<InventoryData> previous = new List<InventoryData> { pool[0], pool[1], pool[2] };

        for (int seed = 0; seed < SEED_COUNT; seed++)
        {
            List<InventoryData> result = Generate(pool, 3, previous, new System.Random(seed));

            Assert.AreEqual(3, result.Count);
            foreach (InventoryData data in result)
            {
                CollectionAssert.DoesNotContain(previous, data);
            }
        }
    }

    [Test]
    public void Generate_NotEnoughFreshItems_FillsWithPrevious()
    {
        InventoryData[] pool = CreatePool(4);
        List<InventoryData> previous = new List<InventoryData> { pool[0], pool[1], pool[2] };

        for (int seed = 0; seed < SEED_COUNT; seed++)
        {
            List<InventoryData> result = Generate(pool, 3, previous, new System.Random(seed));

            Assert.AreEqual(3, result.Count);
            CollectionAssert.Contains(result, pool[3]);
            CollectionAssert.AllItemsAreUnique(result);
        }
    }

    [Test]
    public void Generate_ZeroWeightType_NeverAppears()
    {
        InventoryData[] pool =
        {
            CreateData(1, ItemType.Weapon), CreateData(2, ItemType.Passive),
            CreateData(3, ItemType.Expendables), CreateData(4, ItemType.Expendables),
        };

        for (int seed = 0; seed < SEED_COUNT; seed++)
        {
            List<InventoryData> result = RewardCandidateGenerator.Generate(
                pool, 4, 50f, 35f, 0f, null, new System.Random(seed));

            Assert.AreEqual(2, result.Count);
            foreach (InventoryData data in result)
            {
                Assert.AreNotEqual(ItemType.Expendables, data.ItemType);
            }
        }
    }

    // 기본 가중치 (무기 50, 패시브 35, 소비 15)
    private static List<InventoryData> Generate(InventoryData[] pool, int count,
        ICollection<InventoryData> previous, System.Random random)
    {
        return RewardCandidateGenerator.Generate(pool, count, 50f, 35f, 15f, previous, random);
    }

    // 종류를 섞어 Id 1부터 순서대로 생성
    private InventoryData[] CreatePool(int size)
    {
        ItemType[] types = { ItemType.Weapon, ItemType.Passive, ItemType.Expendables };
        InventoryData[] pool = new InventoryData[size];

        for (int i = 0; i < size; i++)
        {
            pool[i] = CreateData(i + 1, types[i % types.Length]);
        }

        return pool;
    }

    private InventoryData CreateData(int id, ItemType itemType)
    {
        InventoryData data = ScriptableObject.CreateInstance<InventoryData>();
        data.SetData(id, itemType.ToString(), null, itemType, string.Empty, _singleCellShape, 1, null);
        _createdData.Add(data);
        return data;
    }

    // SetData 시그니처를 바꾸지 않도록 직렬화 필드로 설정
    private static void SetSellable(InventoryData data, bool isSellable)
    {
        SerializedObject serializedData = new SerializedObject(data);
        serializedData.FindProperty(IS_SELLABLE_FIELD).boolValue = isSellable;
        serializedData.ApplyModifiedPropertiesWithoutUndo();
    }
}

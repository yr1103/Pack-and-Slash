using NUnit.Framework;

/// <summary>
/// 부유 슬롯 각도가 총 수만큼 원을 균등 분할하고 슬롯 0(주무기)이 정면(0°)인지 검증
/// </summary>
public class WeaponViewSlotTests
{
    private const float TOLERANCE = 0.0001f;

    [TestCase(0, 1, 0f)]
    [TestCase(0, 2, 0f)]
    [TestCase(1, 2, 180f)]
    [TestCase(0, 3, 0f)]
    [TestCase(1, 3, 120f)]
    [TestCase(2, 3, 240f)]
    [TestCase(0, 4, 0f)]
    [TestCase(1, 4, 90f)]
    [TestCase(2, 4, 180f)]
    [TestCase(3, 4, 270f)]
    public void GetSlotAngle_DividesCircleEvenly(int slotIndex, int gunCount, float expectedAngle)
    {
        Assert.AreEqual(expectedAngle, WeaponViewController.GetSlotAngle(slotIndex, gunCount), TOLERANCE);
    }
}

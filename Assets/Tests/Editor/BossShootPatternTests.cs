using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 보스 사격 탄 방향 계산의 1발 중심·360도 균등 분할·부채꼴 양 끝 포함 분할·XZ 정규화 검증
/// </summary>
public class BossShootPatternTests
{
    private const float ANGLE_TOLERANCE = 0.1f; // Vector3.Angle은 거의 같은 방향에서 acos 정밀도 한계로 0.0x도 오차
    private const float LENGTH_TOLERANCE = 0.0001f;

    private static readonly Vector3 _center = new Vector3(1f, 0f, 1f).normalized;

    [Test]
    public void GetSpreadDirection_SingleProjectile_ReturnsCenter()
    {
        Vector3 direction = BossShootPattern.GetSpreadDirection(_center, 0, 1, 0f);

        Assert.AreEqual(0f, Vector3.Angle(_center, direction), ANGLE_TOLERANCE);
    }

    [Test]
    public void GetSpreadDirection_FullCircleEight_EvenlySpacedFromCenter()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = BossShootPattern.GetSpreadDirection(_center, i, 8, 360f);
            // 180도 방향은 SignedAngle 부호가 부동소수 오차로 흔들려 기대 방향과의 사잇각으로 비교
            Vector3 expected = Quaternion.AngleAxis(45f * i, Vector3.up) * _center;

            Assert.AreEqual(0f, Vector3.Angle(expected, direction), ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetSpreadDirection_FanThreeForty_ReturnsMinusZeroPlusTwenty()
    {
        float[] expectedAngles = { -20f, 0f, 20f };
        for (int i = 0; i < expectedAngles.Length; i++)
        {
            Vector3 direction = BossShootPattern.GetSpreadDirection(_center, i, 3, 40f);

            Assert.AreEqual(expectedAngles[i], Vector3.SignedAngle(_center, direction, Vector3.up), ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetSpreadDirection_TiltedInput_ReturnsFlatNormalized()
    {
        Vector3 tilted = new Vector3(3f, 2f, 4f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = BossShootPattern.GetSpreadDirection(tilted, i, 8, 360f);

            Assert.AreEqual(0f, direction.y, LENGTH_TOLERANCE);
            Assert.AreEqual(1f, direction.magnitude, LENGTH_TOLERANCE);
        }
    }
}

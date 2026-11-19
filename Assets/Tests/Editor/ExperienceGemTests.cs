using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 경험치 젬 수집 반경 판정(XZ)과 흩어짐 방향 계산 검증
/// </summary>
public class ExperienceGemTests
{
    private const float COLLECT_RADIUS = 5f;
    private const float SCATTER_ANGLE = 60f;
    private const float TOLERANCE = 0.0001f;
    // Vector3.Angle은 acos 기반이라 0° 근처에서 float 오차가 약 0.02°까지 커짐
    private const float ANGLE_TOLERANCE = 0.05f;
    private const float LEFT_EDGE_RATIO = 0f;
    private const float MIDDLE_RATIO = 0.5f;
    private const float RIGHT_EDGE_RATIO = 1f;

    private static readonly Vector3 _player = Vector3.zero;
    private static readonly Vector3 _inside = new Vector3(1f, 0f, 2f);
    private static readonly Vector3 _outside = new Vector3(4f, 0f, 4f);
    // XZ 거리 정확히 5 (3-4-5)
    private static readonly Vector3 _onEdge = new Vector3(3f, 0f, 4f);
    // XZ는 반경 안이지만 높이 차가 반경보다 큰 젬
    private static readonly Vector3 _highInside = new Vector3(1f, 50f, 2f);
    // 높이 성분이 있는 젬으로 XZ 투영·정규화까지 확인
    private static readonly Vector3 _gem = new Vector3(3f, 2f, 4f);
    // 플레이어와 XZ만 겹치고 높이는 다른 젬
    private static readonly Vector3 _overlappedGem = new Vector3(0f, 0.3f, 0f);
    private static readonly float[] _randomRatios = { 0f, 0.1f, 0.37f, 0.5f, 0.82f, 1f };

    [Test]
    public void IsInCollectRadius_Inside_ReturnsTrue()
    {
        Assert.IsTrue(ExperienceGem.IsInCollectRadius(_inside, _player, COLLECT_RADIUS));
    }

    [Test]
    public void IsInCollectRadius_Outside_ReturnsFalse()
    {
        Assert.IsFalse(ExperienceGem.IsInCollectRadius(_outside, _player, COLLECT_RADIUS));
    }

    [Test]
    public void IsInCollectRadius_ExactlyAtRadius_ReturnsTrue()
    {
        Assert.IsTrue(ExperienceGem.IsInCollectRadius(_onEdge, _player, COLLECT_RADIUS));
    }

    [Test]
    public void IsInCollectRadius_HeightDifference_Ignored()
    {
        Assert.IsTrue(ExperienceGem.IsInCollectRadius(_highInside, _player, COLLECT_RADIUS));
    }

    [Test]
    public void GetScatterDirection_IsFlatAndNormalized()
    {
        for (int i = 0; i < _randomRatios.Length; i++)
        {
            Vector3 direction = ExperienceGem.GetScatterDirection(_gem, _player, SCATTER_ANGLE, _randomRatios[i]);

            Assert.AreEqual(0f, direction.y, TOLERANCE);
            Assert.AreEqual(1f, direction.magnitude, TOLERANCE);
        }
    }

    [Test]
    public void GetScatterDirection_RatioEdgesAndMiddle_MapToAngleEdgesAndAwayDirection()
    {
        Vector3 away = Vector3.ProjectOnPlane(_gem - _player, Vector3.up).normalized;

        Vector3 left = ExperienceGem.GetScatterDirection(_gem, _player, SCATTER_ANGLE, LEFT_EDGE_RATIO);
        Vector3 middle = ExperienceGem.GetScatterDirection(_gem, _player, SCATTER_ANGLE, MIDDLE_RATIO);
        Vector3 right = ExperienceGem.GetScatterDirection(_gem, _player, SCATTER_ANGLE, RIGHT_EDGE_RATIO);

        Assert.AreEqual(-SCATTER_ANGLE, Vector3.SignedAngle(away, left, Vector3.up), ANGLE_TOLERANCE);
        Assert.AreEqual(0f, Vector3.SignedAngle(away, middle, Vector3.up), ANGLE_TOLERANCE);
        Assert.AreEqual(SCATTER_ANGLE, Vector3.SignedAngle(away, right, Vector3.up), ANGLE_TOLERANCE);
    }

    [Test]
    public void GetScatterDirection_AnyRatio_StaysWithinAngle()
    {
        Vector3 away = Vector3.ProjectOnPlane(_gem - _player, Vector3.up).normalized;

        for (int i = 0; i < _randomRatios.Length; i++)
        {
            Vector3 direction = ExperienceGem.GetScatterDirection(_gem, _player, SCATTER_ANGLE, _randomRatios[i]);

            Assert.LessOrEqual(Vector3.Angle(away, direction), SCATTER_ANGLE + ANGLE_TOLERANCE);
        }
    }

    [Test]
    public void GetScatterDirection_OverlappingPlayer_ReturnsFlatNormalized()
    {
        for (int i = 0; i < _randomRatios.Length; i++)
        {
            Vector3 direction = ExperienceGem.GetScatterDirection(_overlappedGem, _player, SCATTER_ANGLE, _randomRatios[i]);

            Assert.AreEqual(0f, direction.y, TOLERANCE);
            Assert.AreEqual(1f, direction.magnitude, TOLERANCE);
        }
    }
}

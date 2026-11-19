using UnityEngine;

/// <summary>
/// 발사 시점에 확정되는 투사체 값 묶음 (풀에서 꺼낼 때마다 새로 전달)
/// </summary>
public readonly struct ProjectileSpawnContext
{
    /// <summary>생성 위치 (총구)</summary>
    public Vector3 SpawnPosition { get; }

    /// <summary>XZ 평면 정규화 이동 방향</summary>
    public Vector3 Direction { get; }

    /// <summary>탄속 (초당 이동 거리)</summary>
    public float Speed { get; }

    /// <summary>적중 1회당 피해량</summary>
    public float Damage { get; }

    /// <summary>최대 비행 거리, 초과 시 반환</summary>
    public float MaxDistance { get; }

    /// <summary>최대 생존 시간(초), 초과 시 반환</summary>
    public float MaxLifetime { get; }

    /// <summary>출력 카메라 화면 밖이면 반환·피해 무효 여부 (권총·미니건 true)</summary>
    public bool RequireCameraVisibility { get; }

    /// <summary>서로 다른 적에게 피해를 줄 수 있는 최대 수, 도달 시 반환</summary>
    public int MaxUniqueTargetsHit { get; }

    public ProjectileSpawnContext(Vector3 spawnPosition, Vector3 direction, float speed, float damage,
        float maxDistance, float maxLifetime, bool requireCameraVisibility, int maxUniqueTargetsHit)
    {
        SpawnPosition = spawnPosition;
        Direction = direction;
        Speed = speed;
        Damage = damage;
        MaxDistance = maxDistance;
        MaxLifetime = maxLifetime;
        RequireCameraVisibility = requireCameraVisibility;
        MaxUniqueTargetsHit = maxUniqueTargetsHit;
    }
}

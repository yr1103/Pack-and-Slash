using UnityEngine;

/// <summary>
/// 장착한 무기 하나의 런타임 상태 (같은 Definition을 쓰는 인스턴스끼리 상태 공유 없음)
/// </summary>
public class WeaponInstance
{
    /// <summary>장착 목록 안에서 고유한 인스턴스 ID</summary>
    public int InstanceId { get; }

    /// <summary>공유 설정 데이터</summary>
    public WeaponDefinition Definition { get; }

    /// <summary>현재 레벨</summary>
    public int Level { get; }

    /// <summary>진화 여부</summary>
    public bool IsEvolved { get; }

    /// <summary>다음 발사까지 남은 시간(초), 0이면 발사 가능</summary>
    public float ShotCooldown { get; private set; }

    /// <summary>현재 잔탄 수</summary>
    public int CurrentAmmo { get; private set; }

    /// <summary>재장전 중 여부</summary>
    public bool IsReloading { get; }

    /// <summary>탄창을 가득 채운 상태로 생성</summary>
    public WeaponInstance(int instanceId, WeaponDefinition definition, int level)
    {
        InstanceId = instanceId;
        Definition = definition;
        Level = level;
        CurrentAmmo = definition.MagazineCapacity;
    }

    /// <summary>발사 쿨다운을 경과 시간만큼 감소 (0 미만으로 내려가지 않음)</summary>
    /// <param name="deltaTime">경과 시간(초)</param>
    public void Tick(float deltaTime)
    {
        ShotCooldown = Mathf.Max(0f, ShotCooldown - deltaTime);
    }

    /// <summary>발사 쿨다운을 Definition의 발사 간격으로 재설정</summary>
    public void ResetShotCooldown()
    {
        ShotCooldown = Definition.FireInterval;
    }

    /// <summary>잔탄 1 소모 (탄창 없는 무기이거나 잔탄이 0이면 무시)</summary>
    public void ConsumeAmmo()
    {
        if (Definition.MagazineCapacity <= 0 || CurrentAmmo <= 0)
        {
            return;
        }

        CurrentAmmo--;
    }
}

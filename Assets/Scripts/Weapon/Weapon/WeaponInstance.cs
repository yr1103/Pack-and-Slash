using System;
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

    /// <summary>현재 레벨 (ApplyStats로만 변경)</summary>
    public int Level { get; private set; }

    /// <summary>진화 여부</summary>
    public bool IsEvolved { get; }

    /// <summary>탄 1발 피해량 (생성 시 Definition 값, ApplyStats로만 변경)</summary>
    public float Damage { get; private set; }

    /// <summary>발사 간격(초) (생성 시 Definition 값, ApplyStats로만 변경)</summary>
    public float FireInterval { get; private set; }

    /// <summary>부채꼴 전체 각도(도) (생성 시 Definition 값, ApplyStats로만 변경)</summary>
    public float SpreadAngle { get; private set; }

    /// <summary>true면 확산각 안 무작위 방향, false면 균등 분배 (생성 시 Definition 값, ApplyStats로만 변경)</summary>
    public bool RandomSpread { get; private set; }

    /// <summary>다음 발사까지 남은 시간(초), 0 이하면 발사 가능 (음수 = 발사 예정 시각을 넘긴 시간)</summary>
    public float ShotCooldown { get; private set; }

    /// <summary>현재 잔탄 수</summary>
    public int CurrentAmmo { get; private set; }

    /// <summary>재장전 중 여부 (탄창 무기의 잔탄이 0이 되면 시작, Tick으로 재장전 시간이 지나면 종료)</summary>
    public bool IsReloading { get; private set; }

    /// <summary>남은 재장전 시간(초), 재장전 중이 아니면 0</summary>
    public float ReloadRemaining { get; private set; }

    /// <summary>이번 프레임 탐색한 표적, 없으면 null (WeaponAttackController가 Update에서 갱신, 다른 컴포넌트는 LateUpdate에서 읽음)</summary>
    public ITargetable CurrentTarget { get; set; }

    /// <summary>잔탄 변경 시 발생 (소모, 재장전 완료)</summary>
    public event Action AmmoChanged;

    /// <summary>재장전 시작 시 발생</summary>
    public event Action ReloadStarted;

    /// <summary>재장전 완료 시 발생</summary>
    public event Action ReloadFinished;

    /// <summary>탄창을 가득 채운 상태로 생성</summary>
    public WeaponInstance(int instanceId, WeaponDefinition definition, int level, bool isEvolved = false)
    {
        InstanceId = instanceId;
        Definition = definition;
        Level = level;
        IsEvolved = isEvolved;
        CurrentAmmo = definition.MagazineCapacity;
        Damage = definition.ProjectileDamage;
        FireInterval = definition.FireInterval;
        SpreadAngle = definition.SpreadAngle;
        RandomSpread = definition.RandomSpread;
    }

    /// <summary>레벨·런타임 스탯 갱신 (남은 쿨다운·잔탄·재장전은 유지, 새 발사 간격은 다음 ScheduleNextShot부터 적용)</summary>
    public void ApplyStats(int level, float damage, float fireInterval, float spreadAngle, bool randomSpread)
    {
        Level = level;
        Damage = damage;
        FireInterval = fireInterval;
        SpreadAngle = spreadAngle;
        RandomSpread = randomSpread;
    }

    /// <summary>발사 쿨다운·재장전 시간을 경과 시간만큼 감소 (쿨다운은 음수까지 내려감)</summary>
    /// <param name="deltaTime">경과 시간(초), 일시정지 대비 호출부가 넘긴 값만 사용</param>
    public void Tick(float deltaTime)
    {
        ShotCooldown -= deltaTime;

        if (!IsReloading)
        {
            return;
        }

        ReloadRemaining -= deltaTime;
        if (ReloadRemaining <= 0f)
        {
            FinishReload();
        }
    }

    /// <summary>다음 발사 예정 시각을 발사 간격만큼 뒤로 미룸 (넘긴 시간을 이월해 FPS와 무관한 연사 속도 유지)</summary>
    public void ScheduleNextShot()
    {
        ShotCooldown += FireInterval;
    }

    /// <summary>넘긴 시간(음수 쿨다운)을 버리고 0으로 되돌림 (표적 없음·재장전 중 몰아 쏘기 방지)</summary>
    public void ClearOverdueShotCooldown()
    {
        ShotCooldown = Mathf.Max(0f, ShotCooldown);
    }

    /// <summary>잔탄 1 소모, 0이 되면 재장전 시작 (탄창 없는 무기이거나 잔탄이 0이면 무시)</summary>
    public void ConsumeAmmo()
    {
        if (Definition.MagazineCapacity <= 0 || CurrentAmmo <= 0)
        {
            return;
        }

        CurrentAmmo--;
        AmmoChanged?.Invoke();

        if (CurrentAmmo <= 0)
        {
            StartReload();
        }
    }

    private void StartReload()
    {
        IsReloading = true;
        ReloadRemaining = Definition.ReloadDuration;
        ReloadStarted?.Invoke();
    }

    private void FinishReload()
    {
        CurrentAmmo = Definition.MagazineCapacity;
        IsReloading = false;
        ReloadRemaining = 0f;
        AmmoChanged?.Invoke();
        ReloadFinished?.Invoke();
    }
}

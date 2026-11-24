using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 전투 진행 상태·생존 시간·처치 수 관리, 클리어·게임 오버 판정과 종료 시 전투 정지, 일시정지·재개 (Time.timeScale 유일 소유자)
/// </summary>
public class GameSession : MonoBehaviour
{
    private const int SECONDS_PER_MINUTE = 60;
    private const string TIME_FORMAT = "{0:00}:{1:00}"; // 분은 두 자리 이상, 초는 두 자리

    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField, Min(0f)] private float _clearTime = 1380f; // 클리어 생존 시간(초), 기본 23:00
    [SerializeField, Min(0f)] private float _endFreezeDelay = 1.5f; // 종료 후 전투 정지까지 대기(초, 사망 연출 시간)

    private float _elapsedTime;
    private int _killCount;
    private SessionState _state;

    /// <summary>세션 진행 상태</summary>
    public enum SessionState
    {
        Playing,
        Cleared,
        GameOver
    }

    public float ElapsedTime => _elapsedTime;
    public int KillCount => _killCount;
    public SessionState State => _state;

    /// <summary>진행 중 여부 (생존 시간 누적·스폰 허용 조건)</summary>
    public bool IsPlaying => _state == SessionState.Playing;

    /// <summary>종료 후 전투 정지 시점에 1회 발생 (결과 패널 구독 지점, 인자는 클리어 여부)</summary>
    public event Action<bool> Ended;

    // 이전 판이 정지(timeScale 0) 상태로 Scene을 다시 불러와도 전투가 멈춘 채 시작되지 않도록 복구
    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        Enemy.Died += HandleEnemyDied;
        _playerHealth.Died += HandlePlayerDied;
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;
        if (IsCleared(_elapsedTime, _clearTime))
        {
            End(SessionState.Cleared);
        }
    }

    private void OnDisable()
    {
        Enemy.Died -= HandleEnemyDied;
        _playerHealth.Died -= HandlePlayerDied;
    }

    // 정지 상태로 타이틀 등 GameSession이 없는 Scene으로 이동해도 정지가 남지 않도록 복구
    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    /// <summary>일시정지 (진행 중일 때만 전투 정지)</summary>
    public void Pause()
    {
        if (IsPlaying)
        {
            Time.timeScale = 0f;
        }
    }

    /// <summary>일시정지 해제 (진행 중일 때만, 종료 후 전투 정지는 풀지 않음)</summary>
    public void Resume()
    {
        if (IsPlaying)
        {
            Time.timeScale = 1f;
        }
    }

    /// <summary>클리어 판정 (생존 시간이 클리어 시간 이상이면 클리어)</summary>
    public static bool IsCleared(float elapsedTime, float clearTime)
    {
        return elapsedTime >= clearTime;
    }

    /// <summary>초를 "mm:ss" 문자열로 변환 (소수점 버림, 분은 두 자리 이상)</summary>
    public static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        return string.Format(TIME_FORMAT, totalSeconds / SECONDS_PER_MINUTE, totalSeconds % SECONDS_PER_MINUTE);
    }

    // 종료 후 사망 연출 중에 죽은 적은 처치 수에 포함하지 않음
    private void HandleEnemyDied(Enemy enemy)
    {
        if (IsPlaying)
        {
            _killCount++;
        }

        if (enemy.ClearsSessionOnDeath)
        {
            End(SessionState.Cleared);
        }
    }

    private void HandlePlayerDied()
    {
        End(SessionState.GameOver);
    }

    // 상태 전환으로 생존 시간·스폰을 즉시 멈추고, 연출 대기 후 전투 정지 (첫 종료만 처리)
    private void End(SessionState endState)
    {
        if (!IsPlaying)
        {
            return;
        }

        _state = endState;
        StartCoroutine(FreezeAfterDelay());
    }

    private IEnumerator FreezeAfterDelay()
    {
        yield return new WaitForSeconds(_endFreezeDelay);

        Time.timeScale = 0f;
        Ended?.Invoke(_state == SessionState.Cleared);
    }
}

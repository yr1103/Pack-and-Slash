using UnityEngine;

/// <summary>
/// 보스 패턴을 배열 순서대로 하나씩 켜고, 패턴 완료 후 간격 동안 추적만 한 뒤 다음 패턴으로 순환
/// </summary>
public class BossPatternSequencer : MonoBehaviour
{
    private const string INVALID_PATTERN_WARNING_FORMAT = "_patterns[{0}]의 {1}은(는) IBossPattern이 아니라 비움";

    // 인터페이스는 Inspector 직렬화가 안 돼 Behaviour로 받고, IBossPattern 여부는 OnValidate에서 편집 시점에 검사
    [SerializeField] private Behaviour[] _patterns; // IBossPattern 구현 컴포넌트를 실행 순서대로 (같은 컴포넌트 중복 가능)
    [SerializeField, Min(0f)] private float _interval = 1.5f; // 패턴 완료 후 다음 패턴까지(초)

    private Enemy _enemy;
    private int _index;
    private float _intervalRemaining;
    private IBossPattern _currentPattern; // null이면 간격 대기 중

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    // 풀 재사용 시 첫 패턴부터 다시 시작
    private void OnEnable()
    {
        foreach (Behaviour pattern in _patterns)
        {
            pattern.enabled = false;
        }

        _index = 0;
        StartPattern();
    }

    // 같은 컴포넌트가 배열에 중복돼도 구독이 쌓이지 않도록 현재 패턴만 구독·해제
    private void OnDisable()
    {
        UnsubscribeCurrent();
    }

    // IBossPattern이 아닌 컴포넌트를 Play 중 캐스트 오류 대신 Inspector 편집 시점에 걸러냄
    private void OnValidate()
    {
        if (_patterns == null)
        {
            return;
        }

        for (int i = 0; i < _patterns.Length; i++)
        {
            if (_patterns[i] != null && !(_patterns[i] is IBossPattern))
            {
                Debug.LogWarningFormat(this, INVALID_PATTERN_WARNING_FORMAT, i, _patterns[i].GetType().Name);
                _patterns[i] = null;
            }
        }
    }

    private void Update()
    {
        // 사망 시 진행 중지 (켜진 패턴의 사망 처리는 각 패턴이 담당)
        if (_currentPattern != null || !_enemy.IsAlive)
        {
            return;
        }

        _intervalRemaining -= Time.deltaTime;
        if (_intervalRemaining <= 0f)
        {
            _index = (_index + 1) % _patterns.Length;
            StartPattern();
        }
    }

    private void StartPattern()
    {
        Behaviour pattern = _patterns[_index];
        _currentPattern = (IBossPattern)pattern;
        _currentPattern.Finished += HandlePatternFinished;
        pattern.enabled = true;
    }

    private void HandlePatternFinished()
    {
        UnsubscribeCurrent();
        _patterns[_index].enabled = false;
        _intervalRemaining = _interval;
    }

    private void UnsubscribeCurrent()
    {
        if (_currentPattern == null)
        {
            return;
        }

        _currentPattern.Finished -= HandlePatternFinished;
        _currentPattern = null;
    }
}

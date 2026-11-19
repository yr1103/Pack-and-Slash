using DamageNumbersPro;
using UnityEngine;

/// <summary>
/// Enemy 피격 연출 (피해마다 재질을 잠시 반짝임 재질로 교체, 피해 숫자 표시)
/// </summary>
public class EnemyHitFeedback : MonoBehaviour
{
    [SerializeField] private Enemy _enemy;
    [SerializeField] private Renderer _renderer; // 외형 자식의 Renderer
    [SerializeField] private Material _flashMaterial;
    [SerializeField] private DamageNumber _damageNumberPrefab;
    [SerializeField] private Transform _numberPoint; // 피해 숫자 생성 위치
    [SerializeField, Min(0f)] private float _flashDuration = 0.1f; // 반짝임 유지 시간(초)

    private Material _originalMaterial;
    private float _flashRemaining;

    // Renderer에 직렬화된 재질 에셋을 읽기만 하므로 다른 컴포넌트 초기화 순서와 무관
    private void Awake()
    {
        _originalMaterial = _renderer.sharedMaterial;
    }

    private void OnEnable()
    {
        _enemy.Damaged += OnDamaged;
    }

    private void Update()
    {
        if (_flashRemaining <= 0f)
        {
            return;
        }

        _flashRemaining -= Time.deltaTime;
        if (_flashRemaining <= 0f)
        {
            _renderer.sharedMaterial = _originalMaterial;
        }
    }

    // 반짝임 도중 풀에 반환되면 재사용 시 반짝임 재질이 남지 않도록 복원
    private void OnDisable()
    {
        _enemy.Damaged -= OnDamaged;

        if (_flashRemaining > 0f)
        {
            _renderer.sharedMaterial = _originalMaterial;
            _flashRemaining = 0f;
        }
    }

    // 반짝이는 중에 다시 맞으면 유지 시간만 갱신, 유지 시간 0이면 복원 시점이 없으므로 반짝임 생략
    private void OnDamaged(float damage)
    {
        if (GameSettings.HitFlashEnabled && _flashDuration > 0f)
        {
            _renderer.sharedMaterial = _flashMaterial;
            _flashRemaining = _flashDuration;
        }

        if (GameSettings.DamageNumberEnabled)
        {
            _damageNumberPrefab.Spawn(_numberPoint.position, damage);
        }
    }
}

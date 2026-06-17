using UnityEngine;

/// <summary>
/// 후각 기믹 — 유독 가스 지대.
/// 가스 안에 머무는 시간이 누적되어 한계를 넘으면 사망한다(빠르게 통과하거나 피해야 함).
/// 해독(Antidote) 열매를 먹은 상태면 면역.
/// _pulsing 이 켜져 있으면 일정 주기로 가스가 켜졌다 꺼지며 타이밍 퍼즐이 된다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GasZone : MonoBehaviour
{
    [Header("독성")]
    [SerializeField] private float _toxicTime = 0.8f;  // 가스 안에서 버틸 수 있는 누적 시간
    [SerializeField] private float _recoverRate = 1.5f; // 가스 밖에서 노출치가 회복되는 속도(배수)

    [Header("맥동(선택)")]
    [SerializeField] private bool _pulsing = false;
    [SerializeField] private float _onDuration = 1.5f;
    [SerializeField] private float _offDuration = 1.2f;
    [SerializeField] private float _phaseOffset = 0f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField] private float _baseAlpha = 0.55f;

    private Collider2D _collider;
    private float _exposure;
    private bool _active = true;
    private float _cycle;
    private bool _playerInside;
    private PlayerFSM _player;
    private PlayerTaste _taste;

    public void Configure(float toxicTime, bool pulsing, float onDuration, float offDuration, float phaseOffset)
    {
        _toxicTime = Mathf.Max(0.05f, toxicTime);
        _pulsing = pulsing;
        _onDuration = Mathf.Max(0.1f, onDuration);
        _offDuration = Mathf.Max(0.1f, offDuration);
        _phaseOffset = phaseOffset;
        _cycle = phaseOffset;
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _cycle = _phaseOffset;
        if (_visual == null) _visual = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (_pulsing)
        {
            _cycle += Time.deltaTime;
            float period = Mathf.Max(0.01f, _onDuration + _offDuration);
            _active = (_cycle % period) < _onDuration;

            if (_collider != null) _collider.enabled = _active;
            SetVisualAlpha(_active ? _baseAlpha : _baseAlpha * 0.12f);
        }

        if (_playerInside && _active && _player != null && !_player.IsDead)
        {
            if (_taste != null && _taste.HasGasImmunity)
            {
                _exposure = 0f;
                return;
            }

            _exposure += Time.deltaTime;
            if (_exposure >= _toxicTime)
                _player.Kill();
        }
        else
        {
            _exposure = Mathf.Max(0f, _exposure - Time.deltaTime * _recoverRate);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerFSM player = other.GetComponentInParent<PlayerFSM>();
        if (player == null) return;

        _player = player;
        _taste = player.GetComponentInParent<PlayerTaste>();
        _playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerFSM>() != null)
            _playerInside = false;
    }

    private void SetVisualAlpha(float a)
    {
        if (_visual == null) return;
        Color c = _visual.color;
        c.a = a;
        _visual.color = c;
    }
}

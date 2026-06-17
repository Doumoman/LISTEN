using UnityEngine;

/// <summary>
/// 미각 기믹 — 열매. 먹으면 PlayerTaste 에 효과가 부여된다.
/// 상황에 맞는 열매를 먹어야 다른 감각 기믹(가스/위장블록/게이트)을 파훼할 수 있다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Fruit : MonoBehaviour
{
    [SerializeField] private TasteKind _kind = TasteKind.Antidote;
    [SerializeField] private float _duration = 8f;
    [SerializeField] private bool _consumeOnTouch = true;
    [SerializeField] private bool _persistent = false;    // true 면 사라지지 않고 재섭취 가능(데모/체크포인트 친화)
    [SerializeField] private float _respawnCooldown = 0f; // >0 이면 일정 시간 후 재생성
    [SerializeField] private SpriteRenderer _visual;
    [SerializeField] private float _bobAmplitude = 0.15f;
    [SerializeField] private float _bobSpeed = 2f;

    private Vector3 _basePosition;
    private float _bobTimer;

    public TasteKind Kind => _kind;

    public void Configure(TasteKind kind, float duration, bool persistent = false)
    {
        _kind = kind;
        _duration = Mathf.Max(0f, duration);
        _persistent = persistent;
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        _basePosition = transform.position;
        if (_visual == null) _visual = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        _bobTimer += Time.deltaTime * _bobSpeed;
        transform.position = _basePosition + Vector3.up * (Mathf.Sin(_bobTimer) * _bobAmplitude);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_consumeOnTouch) return;
        TryConsume(other);
    }

    private void TryConsume(Collider2D other)
    {
        PlayerTaste taste = other.GetComponentInParent<PlayerTaste>();
        if (taste == null) return;

        taste.Eat(_kind, _duration);
        Consume();
    }

    private void Consume()
    {
        if (_persistent)
            return; // 사라지지 않음 — 다시 들어오면 재섭취 가능

        if (_respawnCooldown > 0f)
        {
            SetVisible(false);
            CancelInvoke(nameof(Respawn));
            Invoke(nameof(Respawn), _respawnCooldown);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void Respawn() => SetVisible(true);

    private void SetVisible(bool visible)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = visible;
        if (_visual != null) _visual.enabled = visible;
    }
}

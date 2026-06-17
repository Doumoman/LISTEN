using UnityEngine;

/// <summary>
/// 위장 블록의 정체.
/// Solid  : 진짜 안전한 발판
/// Deadly : 닿으면 죽는 치명 블록
/// Fake   : 발판처럼 보이지만 밟으면 빠지는 허상 블록(콜라이더 없음)
/// </summary>
public enum DisguiseKind { Solid, Deadly, Fake }

/// <summary>
/// 촉각 기믹 — 겉모습이 똑같은 블록들 중 어떤 것이 피격 판정인지 모른다.
/// 닿아봐야(촉각) 정체를 알 수 있다.
///   - Deadly: 닿는 순간 사망(+ 붉게 드러남)
///   - Fake  : 밟으면 콜라이더가 꺼져 그대로 추락
///   - Solid : 안전
/// 광명(Vision) 열매를 먹으면 닿기 전에 정체가 색으로 드러난다(시각 ↔ 촉각 연계).
/// </summary>
public class DisguisedBlock : MonoBehaviour
{
    [SerializeField] private DisguiseKind _kind = DisguiseKind.Solid;

    [Header("References")]
    [SerializeField] private Collider2D _solidCollider; // 솔리드/치명용 단단한 콜라이더
    [SerializeField] private SpriteRenderer _visual;

    [Header("Reveal")]
    [SerializeField] private bool _revealOnTouch = true;
    [SerializeField] private bool _visionRevealsTruth = true;
    [SerializeField] private Color _baseColor = Color.white;
    [SerializeField] private Color _deadlyColor = new Color(1f, 0.32f, 0.32f);
    [SerializeField] private Color _fakeColor = new Color(0.55f, 0.75f, 1f);

    private PlayerTaste _taste;
    private bool _touched;
    private bool _fakeTriggered;

    public DisguiseKind Kind => _kind;
    public void SetKind(DisguiseKind kind) => _kind = kind;

    private void Awake()
    {
        if (_visual == null) _visual = GetComponentInChildren<SpriteRenderer>();
        if (_solidCollider == null) _solidCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        PlayerFSM player = FindFirstObjectByType<PlayerFSM>();
        if (player != null) _taste = player.GetComponent<PlayerTaste>();
        ApplyBaseColor();
    }

    private void Update()
    {
        // 광명 열매를 먹으면 닿기 전에도 정체가 드러난다.
        if (_visionRevealsTruth && _visual != null)
        {
            bool reveal = _touched || (_taste != null && _taste.HasVision);
            _visual.color = reveal ? TruthColor() : _baseColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleTouch(other);
    private void OnTriggerStay2D(Collider2D other) => HandleTouch(other);

    private void HandleTouch(Collider2D other)
    {
        PlayerFSM player = other.GetComponentInParent<PlayerFSM>();
        if (player == null) return;

        _touched = true;

        switch (_kind)
        {
            case DisguiseKind.Deadly:
                if (_revealOnTouch && _visual != null) _visual.color = _deadlyColor;
                player.Kill();
                break;

            case DisguiseKind.Fake:
                if (_fakeTriggered) break;
                _fakeTriggered = true;
                if (_solidCollider != null) _solidCollider.enabled = false; // 발판이 꺼져 추락
                if (_visual != null)
                {
                    Color c = _fakeColor;
                    c.a = 0.25f;
                    _visual.color = c;
                }
                break;

            case DisguiseKind.Solid:
            default:
                break;
        }
    }

    private Color TruthColor()
    {
        switch (_kind)
        {
            case DisguiseKind.Deadly: return _deadlyColor;
            case DisguiseKind.Fake: return _fakeColor;
            default: return _baseColor;
        }
    }

    private void ApplyBaseColor()
    {
        if (_visual != null) _visual.color = _baseColor;
    }
}

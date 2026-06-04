using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class HoldableObject : MonoBehaviour, IHoldable
{
    [SerializeField] private float idleGravity = -25f;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private LayerMask collisionLayer;

    private BoxCollider2D _bc;
    private Transform _holdPoint;
    private Vector2 _velocity;
    private float _throwGravity;
    private LayerMask _throwCollisionLayer;

    private enum State { Idle, Held, Thrown }
    private State _state = State.Idle;

    private readonly float SkinWidth = 0.02f;
    private readonly int RayCount = 3;

    public Transform Transform => transform;

    private void Awake()
    {
        _bc = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        switch (_state)
        {
            case State.Idle:
                UpdateIdle();
                break;
            case State.Held:
                UpdateHeld();
                break;
            case State.Thrown:
                UpdateThrown();
                break;
        }
    }

    // ── Idle: 중력만 적용, 바닥에 서 있음 ──
    private void UpdateIdle()
    {
        if (CheckGround(collisionLayer))
            _velocity.y = Mathf.Max(_velocity.y, 0f);
        else
            _velocity.y += idleGravity * Time.deltaTime;

        _velocity.x = 0f;

        Vector2 delta = _velocity * Time.deltaTime;
        delta = ResolveVertical(delta, collisionLayer);
        transform.position += (Vector3)delta;
    }

    // ── Held: holdPoint 위치 추적 ──
    private void UpdateHeld()
    {
        if (_holdPoint != null)
            transform.position = _holdPoint.position;
    }

    // ── Thrown: 포물선 궤적 + 충돌 감지 ──
    private void UpdateThrown()
    {
        if (CheckGround(_throwCollisionLayer))
            _velocity.y = Mathf.Max(_velocity.y, 0f);
        else
            _velocity.y += _throwGravity * Time.deltaTime;

        Vector2 delta = _velocity * Time.deltaTime;
        delta = ResolveHorizontal(delta, _throwCollisionLayer);
        delta = ResolveVertical(delta, _throwCollisionLayer);
        transform.position += (Vector3)delta;

        // 수평 또는 수직 속도가 충돌로 0이 되면 Idle 전환
        if (Mathf.Abs(_velocity.x) < 0.01f && Mathf.Abs(_velocity.y) < 0.01f)
            TransitionToIdle();
    }

    // ── IHoldable 구현 ──
    public void OnPickedUp(Transform holdPoint)
    {
        _holdPoint = holdPoint;
        _velocity = Vector2.zero;
        _bc.enabled = false;
        _state = State.Held;
    }

    public void OnThrown(Vector2 velocity, float gravity, LayerMask throwCollisionLayer)
    {
        _holdPoint = null;
        _velocity = velocity;
        _throwGravity = gravity;
        _throwCollisionLayer = throwCollisionLayer;
        _bc.enabled = true;
        _state = State.Thrown;
    }

    public void OnDropped()
    {
        _holdPoint = null;
        _velocity = Vector2.zero;
        _bc.enabled = true;
        _state = State.Idle;
    }

    private void TransitionToIdle()
    {
        _velocity = Vector2.zero;
        _state = State.Idle;
    }

    // ── 물리 헬퍼 (PushableObject/PlayerFSM 패턴) ──
    private bool CheckGround(LayerMask layer)
    {
        Vector2 center = (Vector2)transform.position + _bc.offset;
        float halfW = _bc.size.x * 0.5f - SkinWidth;

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.right * Mathf.Lerp(-halfW, halfW, t);
            origin.y -= _bc.size.y * 0.5f - SkinWidth;

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance + SkinWidth, layer);
            if (hit.collider != null) return true;
        }
        return false;
    }

    private Vector2 ResolveVertical(Vector2 delta, LayerMask layer)
    {
        if (Mathf.Abs(delta.y) < 0.0001f) return delta;

        float dir = Mathf.Sign(delta.y);
        float rayLength = Mathf.Abs(delta.y) + SkinWidth;
        float halfW = _bc.size.x * 0.5f - SkinWidth;
        Vector2 center = (Vector2)transform.position + _bc.offset;

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.right * Mathf.Lerp(-halfW, halfW, t);
            origin.y += dir * (_bc.size.y * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up * dir, rayLength, layer);
            if (hit.collider != null)
            {
                delta.y = (hit.distance - SkinWidth) * dir;
                _velocity.y = 0f;
                break;
            }
        }
        return delta;
    }

    private Vector2 ResolveHorizontal(Vector2 delta, LayerMask layer)
    {
        if (Mathf.Abs(delta.x) < 0.0001f) return delta;

        float dir = Mathf.Sign(delta.x);
        float rayLength = Mathf.Abs(delta.x) + SkinWidth;
        float halfH = _bc.size.y * 0.5f - SkinWidth;
        Vector2 center = (Vector2)transform.position + _bc.offset;

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.up * Mathf.Lerp(-halfH, halfH, t);
            origin.x += dir * (_bc.size.x * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dir, rayLength, layer);
            if (hit.collider != null)
            {
                delta.x = (hit.distance - SkinWidth) * dir;
                _velocity.x = 0f;
                break;
            }
        }
        return delta;
    }
}

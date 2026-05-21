using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
public class PlayerFSM : MonoBehaviour
{
    [Header("References")]
    public Animator Anim { get; private set; }
    public Rigidbody2D Rb { get; private set; }
    public BoxCollider2D Bc { get; private set; }

    [SerializeField] private PlayerData _playerData = new PlayerData();
    public PlayerData PlayerData => _playerData;

    [Header("States")]
    private PlayerBaseState _currentState;
    public PlayerBaseState MoveState { get; private set; }
    public PlayerBaseState AirborneState { get; private set; }
    public PlayerBaseState LadderState { get; private set; }
    public PlayerBaseState SneakMoveState { get; private set; }
    public PlayerBaseState PushState { get; private set; }
    public PlayerBaseState KilledState { get; private set; }
    public PlayerBaseState HangState { get; private set; }

    private InputManager _inputManager;

    [Header("Physics")]
    [SerializeField] private Vector2 _velocity; // 속도
    public Vector2 lastDir; // 마지막 방향

    private readonly float SkinWidth = 0.02f; // 콜라이더 겉을 감싸는 얇은 막, 충돌 버그 방지
    private readonly int RayCount = 3; // 플레이어의 콜라이더를 RayCount로 등분해서 Ray를 쏜다

    // 각종 타이머들
    private float _groundedGraceTimer = 0f;
    private float _jumpBufferTimer = 0f;

    [Header("Ledge")]
    public bool ledgeDetected;
    public bool ledgeGrabReady;
    public Vector2 ledgeBeginPos;
    private bool _canGrabLedge = true;
    public bool CanGrabLedge => _canGrabLedge;
    private LedgeDetection _ledgeDetection;

    private void Awake()
    {
        Anim = GetComponent<Animator>();
        Rb = GetComponent<Rigidbody2D>();
        Bc = GetComponent<BoxCollider2D>();

        Rb.bodyType = RigidbodyType2D.Kinematic;
        Rb.gravityScale = 0f;

        _ledgeDetection = GetComponentInChildren<LedgeDetection>();
    }

    private void Start()
    {
        MoveState = new MoveState(this);
        AirborneState = new AirborneState(this);
        LadderState = new LadderState(this);
        SneakMoveState = new SneakMoveState(this);
        PushState = new PushState(this);
        KilledState = new KilledState(this);
        HangState = new HangState(this);

        // ㅡㅡㅡㅡ InputManager에서 이벤트 구독 ㅡㅡㅡㅡ
        _inputManager = SingletonManagers.Input;

        _inputManager.OnMove -= HandleMoveInput;
        _inputManager.OnMove += HandleMoveInput;

        _inputManager.OnLadderMove -= HandleLadderMoveInput;
        _inputManager.OnLadderMove += HandleLadderMoveInput;

        _inputManager.OnJumpPressed -= HandleJump;
        _inputManager.OnJumpPressed += HandleJump;

        _inputManager.OnJumpReleased -= HandleJumpReleased;
        _inputManager.OnJumpReleased += HandleJumpReleased;

        _inputManager.OnSneakPressed -= HandleSneak;
        _inputManager.OnSneakPressed += HandleSneak;

        _inputManager.OnSneakReleased -= HandleSneakReleased;
        _inputManager.OnSneakReleased += HandleSneakReleased;

        lastDir = Vector2.right;
        TransitionTo(MoveState); // 초기 상태
    }

    private void Update()
    {
        // 사망 일괄 처리
        if (_playerData.isDead && _currentState != KilledState)
        {
            TransitionTo(KilledState);
            return;
        }

        // ㅡㅡㅡ Layer 감지 함수들 ㅡㅡㅡ
        CheckGround();
        CheckLadder();
        CheckLedge();
        CheckPushable();
        ApplyLayerPriority(); // 레이어 우선 순위 결정

        // lastDir 갱신 및 스프라이트 좌우 플리핑 (HangState, LadderState 중에는 방향 고정)
        if (_playerData.moveHorizontalInput != Vector2.zero && _currentState != HangState && _currentState != LadderState)
        {
            lastDir = _playerData.moveHorizontalInput;
            Vector2 scale = transform.localScale;
            scale.x = _playerData.moveHorizontalInput.x > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        _currentState?.Update();
        ApplyMovement(); // 실제 움직임 적용
    }

    private void LateUpdate()
    {
        // 1프레임 소비 플래그 초기화
        _playerData.isJumpRequested = false;

        if (_jumpBufferTimer > 0f)
            _jumpBufferTimer -= Time.deltaTime;
    }

    // ㅡㅡ 점프 버퍼 소비 ㅡㅡ
    public bool ConsumeJumpBuffer()
    {
        if (_jumpBufferTimer > 0f)
        {
            _jumpBufferTimer = 0f;
            return true;
        }
        return false;
    }

    // ㅡㅡ 상태 전이 함수 ㅡㅡ
    public void TransitionTo(PlayerBaseState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState?.Enter();
    }

    // ㅡㅡ 스프라이트 좌우 반전을 위한 함수 ㅡㅡ
    public int GetDirectionIndex()
    {
        if (lastDir.x > 0) return 1; // right
        else if (lastDir.x < 0) return -1; // left
        else return 0;
    }

    // ㅡㅡ 이동 관련 헬퍼 함수들 ㅡㅡ
    public void SetVelocity(float x, float y) => _velocity = new Vector2(x, y);
    public void SetVelocityX(float x) => _velocity.x = x;
    public void SetVelocityY(float y) => _velocity.y = y;
    public Vector2 GetVelocity() => _velocity;

    // ── 이동 + 충돌 처리 ──
    private void ApplyMovement()
    {
        Vector2 delta = _velocity * Time.deltaTime;

        delta = ResolveHorizontal(delta); // 좌우 충돌 보정
        delta = ResolveVertical(delta); // 상하 충돌 보정

        transform.position += (Vector3)delta;
    }

    // 이번 프레임에 이동할 수평 거리를 미리 계산하고,
    // 그 방향에 벽이 있는지 확인하고 벽에 달라붙을 만큼만 이동거리를 줄여서 반환하는 함수
    // 만약 isGrounded 상태이고 Ray가 경사를 감지했을때 그 법선 각도가 _maxSlopeAngle보다 작으면 y 좌표를 보정해준다.
    private Vector2 ResolveHorizontal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) < 0.0001f) return delta;

        float dir = Mathf.Sign(delta.x);
        float rayLength = Mathf.Abs(delta.x) + SkinWidth;
        float halfH = Bc.size.y * 0.5f - SkinWidth;
        Vector2 center = (Vector2)transform.position + Bc.offset;

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.up * Mathf.Lerp(-halfH, halfH, t);
            origin.x += dir * (Bc.size.x * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dir, rayLength, _playerData.collisionLayer);
            if (hit.collider != null)
            {
                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                if (_playerData.isGrounded && slopeAngle <= _playerData.maxSlopeAngle && hit.normal.y > 0.001f && _velocity.y <= 0f)
                {
                    // 경사면: 수평 이동을 유지하고 Y축을 보정해 경사를 타고 오름
                    delta.y = -delta.x * (hit.normal.x / hit.normal.y);
                }
                else
                {
                    delta.x = (hit.distance - SkinWidth) * dir;
                    SetVelocityX(0f);
                }
                break;
            }
        }
        return delta;
    }

    // 이번 프레임에 이동할 수직 거리를 미리 계산한다
    // Player가 밞을 수 있는 Layer가 있으면 그에 따라 y좌표를 보정한다
    private Vector2 ResolveVertical(Vector2 delta)
    {
        if (Mathf.Abs(delta.y) < 0.0001f) return delta;

        float dir = Mathf.Sign(delta.y);
        float rayLength = Mathf.Abs(delta.y) + SkinWidth;
        float halfW = Bc.size.x * 0.5f - SkinWidth;
        Vector2 center = (Vector2)transform.position + Bc.offset;

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.right * Mathf.Lerp(-halfW, halfW, t);
            origin.y += dir * (Bc.size.y * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up * dir, rayLength, _playerData.collisionLayer);
            if (hit.collider != null)
            {
                delta.y = (hit.distance - SkinWidth) * dir;
                SetVelocityY(0f);
                break;
            }
        }
        return delta;
    }

    // ── Layer 감지 함수들 ──
    private void CheckGround()
    {
        Vector2 center = (Vector2)transform.position + Bc.offset;
        float halfW = Bc.size.x * 0.5f - SkinWidth;

        // Collider의 width를 RayCount 수 만큼 일정 간격으로 나눠서 수직 아래로 Ray를 쏜다
        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.right * Mathf.Lerp(-halfW, halfW, t);
            origin.y -= Bc.size.y * 0.5f - SkinWidth;

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, _playerData.groundCheckDistance + SkinWidth, _playerData.collisionLayer);
            if (hit.collider != null)
            {
                _groundedGraceTimer = _playerData.groundGraceTime;
                _playerData.isGrounded = true;
                return;
            }
        }

        if (_groundedGraceTimer > 0f)
        {
            _groundedGraceTimer -= Time.deltaTime;
            _playerData.isGrounded = true;
            return;
        }
        _playerData.isGrounded = false;
    }

    private void CheckLadder()
    {
        Vector2 center = (Vector2)transform.position + Bc.offset;
        _playerData.nearLadderCollider = Physics2D.OverlapPoint(center, _playerData.ladderLayer);
        _playerData.isNearLadder = _playerData.nearLadderCollider != null;
    }

    public void CheckLedge()
    {
        Vector2 center = (Vector2)transform.position + Bc.offset;
        float halfW = Bc.size.x * 0.5f - SkinWidth;
    }

    public void CaptureLedge()
    {
        ledgeBeginPos = _ledgeDetection.LedgePosition;
        _canGrabLedge = false;
    }

    public void ResetCanGrabLedge() => _canGrabLedge = true;

    private void CheckPushable()
    {
        if (Mathf.Abs(_playerData.moveHorizontalInput.x) < 0.001f || !_playerData.isGrounded)
        {
            _playerData.isPushing = false;
            _playerData.nearPushableCollider = null;
            return;
        }

        float dir = Mathf.Sign(_playerData.moveHorizontalInput.x);
        float rayLength = SkinWidth + 0.05f;
        float halfH = Bc.size.y * 0.5f - SkinWidth;
        Vector2 center = (Vector2)transform.position + Bc.offset;

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.up * Mathf.Lerp(-halfH, halfH, t);
            origin.x += dir * (Bc.size.x * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dir, rayLength, _playerData.pushableLayer);
            if (hit.collider != null)
            {
                _playerData.nearPushableCollider = hit.collider;
                _playerData.isPushing = true;
                return;
            }
        }

        _playerData.nearPushableCollider = null;
        _playerData.isPushing = false;
    }

    private void ApplyLayerPriority()
    {
        // 현재 추가 우선순위 규칙 없음
    }

    // ── 입력 핸들러 ──
    private void HandleMoveInput(Vector2 dir) => _playerData.moveHorizontalInput = dir;
    private void HandleLadderMoveInput(Vector2 dir) => _playerData.MoveVerticalInput = dir;
    private void HandleJump()
    {
        _playerData.isJumpRequested = true;
        _playerData.isJumpHeld = true;
        _jumpBufferTimer = _playerData.jumpBufferTime;
    }
    private void HandleJumpReleased() => _playerData.isJumpHeld = false;
    private void HandleSneak() => _playerData.isSneakHeld = true;
    private void HandleSneakReleased() => _playerData.isSneakHeld = false;

    private void OnDestroy()
    {
        if (_inputManager == null) return;

        _inputManager.OnMove -= HandleMoveInput;
        _inputManager.OnLadderMove -= HandleLadderMoveInput;

        _inputManager.OnJumpPressed -= HandleJump;
        _inputManager.OnJumpReleased -= HandleJumpReleased;

        _inputManager.OnSneakPressed -= HandleSneak;
        _inputManager.OnSneakReleased -= HandleSneakReleased;
    }
}


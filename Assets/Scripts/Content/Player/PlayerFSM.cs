using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
public class PlayerFSM : MonoBehaviour
{
    [SerializeField] private PlayerData _playerData = new PlayerData();
    public PlayerData PlayerData => _playerData;

    private InputManager _inputManager;

    [Header("References")]
    public Animator Anim { get; private set; }
    public Rigidbody2D Rb { get; private set; }
    public BoxCollider2D Bc { get; private set; }

    [Header("States")]
    private PlayerBaseState _currentState;
    public PlayerBaseState MoveState { get; private set; }
    public PlayerBaseState AirborneState { get; private set; }
    public PlayerBaseState LadderState { get; private set; }
    public PlayerBaseState SneakMoveState { get; private set; }
    public PlayerBaseState PushState { get; private set; }
    public PlayerBaseState KilledState { get; private set; }
    public PlayerBaseState HangState { get; private set; }

    [Header("Ledge Detection")]
    [SerializeField] private Transform _wallCheck;
    [SerializeField] private Transform _ledgeCheck;
    private bool _isTouchingWall;
    private bool _isTouchingLedge;
    private Vector2 _ledgeDownRayStart;

    [Header("Holdable")]
    [SerializeField] private Transform _holdPoint; // 물체가 붙는 위치
    private IHoldable _heldObject;
    private float _throwAngle; // 현재 던지기 각도 (도)
    private bool _interactPressed;

    [Header("Physics")]
    [SerializeField] private Vector2 _moveVelocity; // 사용자 입력으로 인한 속도
    [SerializeField] private Vector2 _externalVelocity; // 외부 관성에 의한 속도
    [SerializeField] private Vector2 _finalVelocity; // _moveVelocity + _externalVelocity 합산 결과
    public Vector2 lastDir; // 플레이어가 바라보는 마지막 방향

    // Lift Boost
    [SerializeField] private Vector2 _liftVelocity; // 플랫폼에 의한 속도
    private float _liftVelocityTimer;

    // 플레이어가 추적하는 MovingPlatform
    private MovingPlatformController _currentPlatform;
    private Vector2 _lastPlatformPosition;

    private float _jumpBufferTimer = 0f;
    private float _coyoteTimer = 0f; // 마지막으로 grounded였던 시점 기준 잔여 coyote 시간

    private readonly float SkinWidth = 0.02f; // 콜라이더 겉을 감싸는 얇은 막, 충돌 버그 방지
    private readonly int RayCount = 3; // 플레이어의 콜라이더를 RayCount로 등분해서 Ray를 쏜다

    private void Awake()
    {
        Anim = GetComponent<Animator>();
        Rb = GetComponent<Rigidbody2D>();
        Bc = GetComponent<BoxCollider2D>();

        Rb.bodyType = RigidbodyType2D.Kinematic;
        Rb.gravityScale = 0f;
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

        _inputManager.OnInteractPressed -= HandleInteract;
        _inputManager.OnInteractPressed += HandleInteract;

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
        ApplyPlatformDelta();
        CheckGround();
        CheckMovingPlatformPushing();
        CheckLadder();
        CheckPushable();
        CheckLedge();
        ApplyLayerPriority(); // 레이어 우선 순위 결정

        // lastDir 갱신 및 스프라이트 좌우 플리핑 (HangState, LadderState 중에는 방향 고정)
        if (_playerData.moveHorizontalInput != Vector2.zero && _currentState != HangState && _currentState != LadderState)
        {
            lastDir = _playerData.moveHorizontalInput;
            Vector2 scale = transform.localScale;
            scale.x = _playerData.moveHorizontalInput.x > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // Coyote 타이머: grounded면 충전, 공중이면 감소 (상태와 무관하게 중앙에서 관리)
        if (_playerData.isGrounded)
            _coyoteTimer = _playerData.coyoteTime;
        else if (_coyoteTimer > 0f)
            _coyoteTimer -= Time.deltaTime;

        // LiftBoost 타이머 감소
        if (_liftVelocityTimer > 0f)
            _liftVelocityTimer -= Time.deltaTime;

        // 공중에서 external X 감쇠, 순간적인 속도를 내기 위함
        if (!_playerData.isGrounded)
            _externalVelocity.x = Mathf.MoveTowards(_externalVelocity.x, 0f, _playerData.liftDecayX * Time.deltaTime);

        // Holdable 던지기 각도 조절
        if (_playerData.isHolding)
        {
            float verticalInput = _playerData.MoveVerticalInput.y;
            if (Mathf.Abs(verticalInput) > 0.001f)
                _throwAngle = Mathf.MoveTowards(_throwAngle, verticalInput > 0f ? 45f : -45f, _playerData.throwAngleSpeed * Time.deltaTime);
            else
                _throwAngle = Mathf.MoveTowards(_throwAngle, 0f, _playerData.throwAngleSpeed * Time.deltaTime);
        }

        _currentState?.Update();
        _finalVelocity = _moveVelocity + _externalVelocity;
        ApplyMovement(); // 실제 움직임 적용
    }

    private void LateUpdate()
    {
        // 입력 플래그가 정확히 1프레임만 살아있게 해준다
        // 해당 프레임의 Update() 로직이 완전히 끝난 후 초기화가 보장된다
        _playerData.isJumpRequested = false;
        _interactPressed = false;

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

    // ㅡㅡ Coyote: 잔여 시간 조회 / 소비 ㅡㅡ
    public bool CoyoteAvailable => _coyoteTimer > 0f;
    public void ConsumeCoyote() => _coyoteTimer = 0f;

    // ㅡㅡ Interact 입력 소비 ㅡㅡ
    public bool ConsumeInteract()
    {
        if (_interactPressed)
        {
            _interactPressed = false;
            return true;
        }
        return false;
    }

    // ㅡㅡ 물체 줍기 / 던지기 ㅡㅡ
    public void PickUp(IHoldable holdable)
    {
        _heldObject = holdable;
        _playerData.isHolding = true;
        _throwAngle = 0f;
        holdable.OnPickedUp(_holdPoint);
    }

    public void Throw()
    {
        if (_heldObject == null) return;

        float rad = _throwAngle * Mathf.Deg2Rad;
        float facingDir = Mathf.Sign(lastDir.x);
        Vector2 direction = new Vector2(Mathf.Cos(rad) * facingDir, Mathf.Sin(rad));
        Vector2 velocity = direction * _playerData.throwSpeed;

        _heldObject.OnThrown(velocity, _playerData.throwGravity, _playerData.collisionLayer);
        _heldObject = null;
        _playerData.isHolding = false;
        _throwAngle = 0f;
    }

    public float ThrowAngle => _throwAngle;

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
    public void SetMoveVelocity(Vector2 v) => _moveVelocity = v;
    public void SetMoveVelocityX(float x) => _moveVelocity.x = x;
    public void SetMoveVelocityY(float y) => _moveVelocity.y = y;
    public Vector2 GetMoveVelocity() => _moveVelocity;
    public Vector2 GetFinalVelocity() => _finalVelocity;


    public void SetExternalVelocity(Vector2 v) => _externalVelocity = v;
    public void SetExternalVelocityX(float x) => _externalVelocity.x = x;
    public void SetExternalVelocityY(float y) => _externalVelocity.y = y;
    public void AddExternalVelocity(Vector2 v) => _externalVelocity += v;
    public Vector2 GetLiftVelocity() => _liftVelocityTimer > 0f ? _liftVelocity : Vector2.zero;

    // ── 이동 + 충돌 처리 ──
    private void ApplyMovement()
    {
        Vector2 delta = _finalVelocity * Time.deltaTime;

        delta = ResolveHorizontal(delta); // 좌우 충돌 보정
        delta = ResolveVertical(delta); // 상하 충돌 보정

        transform.position += (Vector3)delta;
    }

    // 이번 프레임에 이동할 수평 거리를 미리 계산하고,
    // 그 방향에 벽이 있는지 확인하고 벽에 달라붙을 만큼만 이동거리를 줄여서 반환하는 함수
    private Vector2 ResolveHorizontal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) < 0.0001f) return delta;

        float dir = Mathf.Sign(delta.x);
        float rayLength = Mathf.Abs(delta.x) + SkinWidth;
        float halfH = Bc.size.y * 0.5f - SkinWidth;

        Vector2 center = (Vector2)transform.position + Bc.offset;

        float minDistance = Mathf.Infinity; // 여러 Ray 중 가장 가까운 hit 거리를 채택

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.up * Mathf.Lerp(-halfH, halfH, t);
            origin.x += dir * (Bc.size.x * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dir, rayLength, _playerData.collisionLayer);
            if (hit.collider != null && hit.distance < minDistance)
                minDistance = hit.distance;
        }

        if (!float.IsInfinity(minDistance))
        {
            delta.x = (minDistance - SkinWidth) * dir;
            SetMoveVelocityX(0f);
            SetExternalVelocityX(0f);
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

        float minDistance = Mathf.Infinity; // 여러 Ray 중 가장 가까운 hit 거리를 채택

        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.right * Mathf.Lerp(-halfW, halfW, t);
            origin.y += dir * (Bc.size.y * 0.5f - SkinWidth);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up * dir, rayLength, _playerData.collisionLayer);
            if (hit.collider != null && hit.distance < minDistance)
                minDistance = hit.distance;
        }

        if (!float.IsInfinity(minDistance))
        {
            delta.y = (minDistance - SkinWidth) * dir;
            SetMoveVelocityY(0f);
            SetExternalVelocityY(0f);
        }
        return delta;
    }

    // ── MovingPlatform 위치 동기화 ──
    private void ApplyPlatformDelta()
    {
        if (_currentPlatform == null) return;

        Vector2 delta = (Vector2)_currentPlatform.transform.position - _lastPlatformPosition;
        transform.position += (Vector3)delta;
        _lastPlatformPosition = _currentPlatform.transform.position;
    }

    // ── Layer 감지 함수들 ──
    private void CheckGround()
    {
        _playerData.isGrounded = false;

        Vector2 center = (Vector2)transform.position + Bc.offset;
        float halfW = Bc.size.x * 0.5f - SkinWidth;

        Collider2D closestCollider = null;
        float minDistance = Mathf.Infinity; // 여러 Ray 중 가장 가까운(높은) 면을 채택

        // Collider의 width를 RayCount 수 만큼 일정 간격으로 나눠서 수직 아래로 Ray를 쏜다
        for (int i = 0; i < RayCount; i++)
        {
            float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);
            Vector2 origin = center + Vector2.right * Mathf.Lerp(-halfW, halfW, t);
            origin.y -= Bc.size.y * 0.5f - SkinWidth;

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, _playerData.groundCheckDistance + SkinWidth, _playerData.collisionLayer);
            if (hit.collider != null && hit.distance < minDistance)
            {
                minDistance = hit.distance;
                closestCollider = hit.collider;
            }
        }

        // 하나도 못 맞히면 공중 상태 (Platform 추적 해제)
        if (closestCollider == null)
        {
            _currentPlatform = null;
            return;
        }

        _playerData.isGrounded = true;

        // 가장 가까운(높은) 면 기준으로 올라탄 MovingPlatform 결정
        var platform = closestCollider.GetComponentInParent<MovingPlatformController>();
        if (platform != null)
        {
            _liftVelocity = platform.Velocity;
            _liftVelocityTimer = _playerData.liftVelocityRetainTime;

            if (_currentPlatform != platform)
            {
                _currentPlatform = platform;
                _lastPlatformPosition = platform.transform.position;
            }
            _externalVelocity = Vector2.zero; // 외부 관성은 0
        }
        else
        {
            _currentPlatform = null;
            _externalVelocity = Vector2.zero;
        }
    }

    private void CheckLadder()
    {
        Vector2 center = (Vector2)transform.position + Bc.offset;
        _playerData.nearLadderCollider = Physics2D.OverlapPoint(center, _playerData.ladderLayer);
        _playerData.isNearLadder = _playerData.nearLadderCollider != null;
    }

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

    private void CheckMovingPlatformPushing()
    {
        Vector2 center = (Vector2)transform.position + Bc.offset;

        // 수평(axis 0: x) / 수직(axis 1: y) 양축에 대해 좌우/상하로 스윕 감지
        for (int axis = 0; axis < 2; axis++)
        {
            // axis에 수직인 방향으로 콜라이더를 등분해 Ray를 펼친다
            int sweepAxis = 1 - axis;
            float halfSweep = Bc.size[sweepAxis] * 0.5f - SkinWidth;
            float halfThis = Bc.size[axis] * 0.5f - SkinWidth;

            // 이번 프레임 플레이어 이동량 + 플랫폼 이동분 여유까지 덮는 Ray 길이 (상대 접근 거리)
            float rayLength = SkinWidth + Mathf.Abs(_finalVelocity[axis]) * Time.deltaTime + _playerData.platformPushBuffer;

            for (int dir = -1; dir <= 1; dir += 2) // dir은 -1 and 1
            {
                Vector2 rayDir = (axis == 0) ? Vector2.right * dir : Vector2.up * dir;

                float bestCorrection = 0f;   // 여러 Ray 중 가장 깊게 박힌 보정량
                float pushVelocity = 0f;     // 적용할 외부 속도
                bool hasPush = false;        // 미는 플랫폼 감지 여부
                bool hasPenetration = false; // 실제 침투(스냅 필요) 여부

                for (int i = 0; i < RayCount; i++)
                {
                    float t = (RayCount == 1) ? 0.5f : (float)i / (RayCount - 1);

                    Vector2 origin = center;
                    if (axis == 0) origin.y += Mathf.Lerp(-halfSweep, halfSweep, t);
                    else origin.x += Mathf.Lerp(-halfSweep, halfSweep, t);
                    origin[axis] += dir * halfThis;

                    RaycastHit2D hit = Physics2D.Raycast(origin, rayDir, rayLength, _playerData.collisionLayer);
                    if (hit.collider == null) continue;

                    var platform = hit.collider.GetComponentInParent<MovingPlatformController>(); // MovingPlatform 컴포넌트 감지
                    if (platform == null) continue;

                    // 올라타기(riding)는 CheckGround/ApplyPlatformDelta가 담당하므로 이중 적용 방지
                    if (platform == _currentPlatform) continue;

                    // MovingPlatform이 플레이어 방향으로 이동하고 있을 때만 밀기 적용
                    if (platform.Velocity[axis] * dir >= 0f) continue;

                    // 이번 프레임에 실제로 닿을 거리인지 플랫폼 자기 속도 기준으로 게이팅 (느린 플랫폼의 유령 푸시 방지)
                    float contactThreshold = SkinWidth + (Mathf.Abs(platform.Velocity[axis]) + Mathf.Abs(_finalVelocity[axis])) * Time.deltaTime;
                    if (hit.distance > contactThreshold) continue;

                    hasPush = true;

                    // 밀착 보정: 가장 깊게 박힌 Ray 기준으로 플레이어 모서리를 플랫폼 선단 표면에 맞춤
                    float outerEdge = center[axis] + dir * (Bc.size[axis] * 0.5f);
                    float correction = hit.point[axis] - outerEdge;
                    if (Mathf.Sign(correction) == Mathf.Sign(platform.Velocity[axis]) && Mathf.Abs(correction) > Mathf.Abs(bestCorrection))
                    {
                        bestCorrection = correction;
                        pushVelocity = platform.Velocity[axis];
                        hasPenetration = true;
                    }
                    else if (!hasPenetration)
                    {
                        pushVelocity = platform.Velocity[axis]; // 아직 접촉 전이라도 접근 중인 플랫폼 속도 반영
                    }
                }

                if (hasPush)
                {
                    _externalVelocity[axis] = pushVelocity;

                    if (hasPenetration)
                    {
                        Vector3 fix = Vector3.zero;
                        fix[axis] = bestCorrection;
                        transform.position += fix;
                    }
                }
            }
        }
    }

    private void ApplyLayerPriority()
    {
        // 현재 추가 우선순위 규칙 없음
    }

    // ── 모서리(Ledge) 감지 ──
    private void CheckLedge()
    {
        if (_playerData.isHanging) return;

        Vector2 rayDirection = new Vector2(Mathf.Sign(lastDir.x), 0f);

        // wallHit의 hitPoint의 x좌표는 모서리의 x좌표로 확정지을 수 있음
        // 모서리의 y좌표는 확정된 x좌표를 가지고 위에서 아래로 ray를 쏘면 구할 수 있음
        RaycastHit2D wallHit = Physics2D.Raycast(_wallCheck.position, rayDirection, _playerData.ledgeRayDistance, _playerData.ledgeGroundLayer);
        RaycastHit2D ledgeHit = Physics2D.Raycast(_ledgeCheck.position, rayDirection, _playerData.ledgeRayDistance, _playerData.ledgeGroundLayer);

        _isTouchingWall = wallHit.collider != null;
        _isTouchingLedge = ledgeHit.collider != null;

        if (_isTouchingWall && !_isTouchingLedge && _playerData.isFalling)
        {
            _ledgeDownRayStart = new Vector2(wallHit.point.x + (rayDirection.x * _playerData.ledgeDownRayInset), _ledgeCheck.position.y); // 벽 표면에서 살짝 들어간 부분부터 시작
            float downRayLength = _ledgeCheck.position.y - _wallCheck.position.y + _playerData.ledgeDownRayPadding; // 머리 -> 가슴 길이보다 조금 더 길게

            RaycastHit2D cornerHit = Physics2D.Raycast(_ledgeDownRayStart, Vector2.down, downRayLength, _playerData.ledgeGroundLayer);

            if (cornerHit.collider != null)
            {
                Vector2 cornerPosition = new Vector2(wallHit.point.x, cornerHit.point.y);
                GrabLedge(cornerPosition);
            }
        }
    }

    private void GrabLedge(Vector2 cornerPos)
    {
        _playerData.ledgeCornerPos = cornerPos;
        _playerData.ledgeGrabDir = Mathf.Sign(lastDir.x);
        _playerData.isLedgeGrabbed = true;

        SetMoveVelocity(Vector2.zero);
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
    private void HandleInteract() => _interactPressed = true;

    private void OnDestroy()
    {
        if (_inputManager == null) return;

        _inputManager.OnMove -= HandleMoveInput;
        _inputManager.OnLadderMove -= HandleLadderMoveInput;

        _inputManager.OnJumpPressed -= HandleJump;
        _inputManager.OnJumpReleased -= HandleJumpReleased;

        _inputManager.OnSneakPressed -= HandleSneak;
        _inputManager.OnSneakReleased -= HandleSneakReleased;

        _inputManager.OnInteractPressed -= HandleInteract;
    }

    private void OnDrawGizmos()
    {
        if (_wallCheck == null || _ledgeCheck == null) return;

        Vector2 dir = new Vector2(Mathf.Sign(lastDir.x), 0f);
        Gizmos.color = _isTouchingWall ? Color.red : Color.green;
        Gizmos.DrawLine(_wallCheck.position, (Vector2)_wallCheck.position + dir * _playerData.ledgeRayDistance);

        Gizmos.color = _isTouchingLedge ? Color.red : Color.cyan;
        Gizmos.DrawLine(_ledgeCheck.position, (Vector2)_ledgeCheck.position + dir * _playerData.ledgeRayDistance);
    }
}


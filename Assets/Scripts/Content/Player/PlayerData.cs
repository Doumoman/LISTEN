using UnityEngine;

[System.Serializable]
public class PlayerData
{
    [Header("이동 속도")]
    public float moveSpeed = 7f;
    public float sneakSpeed = 2.5f;
    public float ladderSpeed = 4f;
    public float pushSpeed = 2f;

    [Header("점프 / 중력")]
    public float jumpSpeed = 10f; // 점프 속도
    public float jumpMaxHoldTime = 0.3f; // 점프 키 최대 홀드 시간
    public float jumpHoldGravityScale = 0.5f; // 점프 시 가해지는 중력
    public float gravity = -70f; // 실제 중력
    public float maxFallSpeed = -20f; // 낙하 최고 스피드
    public float groundCheckDistance = 0.05f; // 커질수록 ground 감지하는 ray가 길어짐
    public float coyoteTime = 0.1f; // 낙하 후 점프 허용 시간
    public float jumpBufferTime = 0.15f; // 착지 직전 점프 입력 유효 시간

    [Header("매달리기")]
    public Vector2 ledgeOffset;

    [Header("Layer 감지")]
    public LayerMask collisionLayer; // 충돌 처리 대상 레이어 (Ground, Pushable, ... )
    public LayerMask groundLayer;
    public LayerMask ladderLayer;
    public LayerMask pushableLayer;

    [Header("런타임 플래그")]
    public Vector2 moveHorizontalInput; // 입력받은 좌우 이동 방향
    public Vector2 MoveVerticalInput; // 입력받은 상하 이동 방향
    public bool isJumpRequested; // 점프 요구
    public bool isJumpHeld; // 점프 키 홀드 중
    public bool isFalling; // 낙하 중인지
    public bool isSneakHeld; // 엎드리기 키 홀드 중
    public bool isGrounded; // 바닥에 있는지
    public bool isPushing; // 물체를 미는 중인지
    public bool isNearLadder; // 사다리 감지
    public bool isHolding; // 물체를 들고 있는지
    public bool isDead; // 플레이어 사망 여부

    [Header("경사면 감지")]
    public float groundGraceTime = 0.08f;
    public float maxSlopeAngle = 45f;

    [Header("기본 콜라이더")]
    public Vector2 standingColliderSize = new Vector2(0.8f, 1.25f);
    public Vector2 standingColliderOffset = new Vector2(0f, 0f);

    [Header("웅크리기 콜라이더")]
    public Vector2 sneakColliderSize = new Vector2(0.8f, 1.25f);
    public Vector2 sneakColliderOffset = new Vector2(0f, 0.2f);

    [Header("런타임 콜라이더")]
    public Collider2D nearLadderCollider; // 감지된 Ladder 콜라이더
    public Collider2D nearHangerCollider; // 감지된 Hanger 콜라이더
    public Collider2D nearPushableCollider; // 감지된 Pushable 콜라이더
}

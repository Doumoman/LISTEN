using UnityEngine;

public class AirborneState : PlayerBaseState
{
    private const string PLAYER_MOVE = "Player_Move";

    private float jumpHoldTime = 0f;
    private bool isJumping = false;
    private float coyoteTimer = 0f;

    public AirborneState(PlayerFSM fsm) : base(fsm) { }

    public override void Enter()
    {
        jumpHoldTime = 0f;
        data.isFalling = false;

        if (data.isJumpRequested)
        {
            isJumping = true;
            coyoteTimer = 0f;
            fsm.SetVelocityY(data.jumpSpeed);
        }
        else
        {
            isJumping = false;
            coyoteTimer = data.coyoteTime;
        }
    }

    public override void Update()
    {
        Vector2 vel = fsm.GetVelocity();

        // Coyote jump: 낙하로 진입 후 coyoteTime 안에 점프 입력 시 점프 허용
        if (!isJumping && coyoteTimer > 0f)
        {
            coyoteTimer -= Time.deltaTime;
            if (data.isJumpRequested)
            {
                isJumping = true;
                coyoteTimer = 0f;
                jumpHoldTime = 0f;
                fsm.SetVelocityY(data.jumpSpeed);
            }
        }

        float effectiveGravity;
        if (isJumping && data.isJumpHeld && jumpHoldTime < data.jumpMaxHoldTime) // 짧게 점프했을 경우
        {
            effectiveGravity = data.gravity * data.jumpHoldGravityScale;
            jumpHoldTime += Time.deltaTime;
        }
        else // 최대 점프 거리에서 낙하
        {
            isJumping = false;
            effectiveGravity = data.gravity;
        }

        if (vel.y < 0f)
        {
            data.isFalling = true;
        }

        vel.y += effectiveGravity * Time.deltaTime;
        vel.y = Mathf.Max(vel.y, data.maxFallSpeed);
        vel.x = data.moveHorizontalInput.x * data.moveSpeed;

        fsm.SetVelocity(vel.x, vel.y);

        // 착지 → MoveState
        if (data.isGrounded && vel.y <= 0f)
        {
            data.isFalling = false;
            fsm.TransitionTo(fsm.MoveState);
            return;
        }

        // 사다리 감지 + 위 방향키 입력 → LadderState (점프 중에는 재진입 금지)
        if (data.isNearLadder && data.MoveVerticalInput.y > 0.001f && !isJumping)
        {
            fsm.TransitionTo(fsm.LadderState);
            return;
        }

        // 낙하 중이고 LedgeDetection이 모서리를 감지했을 때 → HangState
        if (data.isLedgeGrabbed && data.isFalling)
        {
            data.isLedgeGrabbed = false;
            fsm.TransitionTo(fsm.HangState);
            return;
        }

        PlayAnim();
    }

    public override void Exit() { }

    private void PlayAnim()
    {
        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(PLAYER_MOVE))
            anim.Play(PLAYER_MOVE);
    }
}
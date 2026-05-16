using UnityEngine;

public class AirborneState : PlayerBaseState
{
    private const string PLAYER_MOVE = "Player_Move";

    private float jumpHoldTime = 0f;
    private bool isJumping = false;

    public AirborneState(PlayerFSM fsm) : base(fsm) { }

    public override void Enter()
    {
        jumpHoldTime = 0f;
        data.isFalling = false;

        if (data.jumpRequested)
        {
            isJumping = true;
            fsm.SetVelocityY(data.jumpSpeed);
        }
        else
        {
            isJumping = false;
        }
    }

    public override void Update()
    {
        Vector2 vel = fsm.GetVelocity();

        float effectiveGravity;
        if (isJumping && data.isJumpHeld && jumpHoldTime < data.jumpMaxHoldTime) // 짧게 점프했을 경우
        {
            effectiveGravity = data.gravity * data.jumpHoldGravityScale;
            jumpHoldTime += Time.deltaTime;
        }
        else // 낙하
        {
            isJumping = false;
            effectiveGravity = data.gravity;
        }

        if (vel.y < 0f)
            data.isFalling = true;

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

        // Hangable 감지 → 낙하 중 + 머리가 Hangable 중심을 넘으면 매달리기
        if (data.isFalling && data.isNearHanger)
        {
            float playerHeadY = fsm.transform.position.y + fsm.Bc.size.y * 0.5f;
            if (playerHeadY > data.nearHangerCollider.bounds.center.y)
            {
                fsm.TransitionTo(fsm.HangState);
                return;
            }
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
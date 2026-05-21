using UnityEngine;

public class MoveState : PlayerBaseState
{
    public MoveState(PlayerFSM fsm) : base(fsm) { }

    private const string PLAYER_MOVE = "Player_Move";
    private const string PLAYER_IDLE = "Player_Idle";

    public override void Enter()
    {
        // Jump buffer: 착지 직전에 점프 입력이 있었으면 즉시 점프
        if (fsm.ConsumeJumpBuffer())
        {
            data.isJumpRequested = true;
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }
        fsm.SetVelocity(0f, 0f);
        PlayMoveAnim();
    }

    public override void Update()
    {
        // 점프 입력 -> AirborneState
        if (data.isJumpRequested && data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 낙하 -> AirborneState
        if (!data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 사다리 감지 + 윗 방향키 -> LadderState
        if (data.isNearLadder && data.MoveVerticalInput.y > 0.001f)
        {
            fsm.TransitionTo(fsm.LadderState);
            return;
        }

        // 밀 수 있는 물체 감지 -> PushState
        if (data.isPushing)
        {
            fsm.TransitionTo(fsm.PushState);
            return;
        }

        // 아래 방향키 -> SneakMoveState
        if (data.isSneakHeld)
        {
            fsm.TransitionTo(fsm.SneakMoveState);
            return;
        }

        // 이동 처리
        float targetVel = data.moveHorizontalInput.x * data.moveSpeed;
        fsm.SetVelocity(targetVel, 0f);

        // 애니메이션
        PlayMoveAnim();
    }

    public override void Exit() { }

    private void PlayMoveAnim()
    {
        bool isMoving = Mathf.Abs(data.moveHorizontalInput.x) > 0.001f;
        string target = isMoving ? PLAYER_MOVE : PLAYER_IDLE;

        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(target))
            anim.Play(target);
    }
}
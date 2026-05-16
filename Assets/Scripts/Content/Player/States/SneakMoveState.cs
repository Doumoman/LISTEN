using UnityEngine;

public class SneakMoveState : PlayerBaseState
{
    private const string PLAYER_SNEAK = "Player_Sneak";
    private const string PLAYER_SNEAKIDLE = "Player_SneakIdle";

    private Vector2 _savedColliderSize;
    private Vector2 _savedColliderOffset;

    public SneakMoveState(PlayerFSM fsm) : base(fsm) { }

    public override void Enter()
    {
        fsm.SetVelocity(0f, 0f);
        PlayAnim();
    }

    public override void Update()
    {
        // 엎드리기 키 뗌 -> MoveState
        if (!data.isSneakHeld)
        {
            fsm.TransitionTo(fsm.MoveState);
            return;
        }

        // 점프 입력 -> AirborneState
        if (data.jumpRequested && data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 지면 이탈 -> AirborneState
        if (!data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // TODO: 엎드린 상태에서 상호작용 키 누르면 물체 주움


        // 이동 처리, SneakSpeed 사용
        float horizontalVel = data.moveHorizontalInput.x * data.sneakSpeed;
        fsm.SetVelocity(horizontalVel, 0f);

        PlayAnim();
    }

    public override void Exit()
    {
        fsm.Bc.size = _savedColliderSize;
        fsm.Bc.offset = _savedColliderOffset;
    }

    private void PlayAnim()
    {
        bool isMoving = Mathf.Abs(data.moveHorizontalInput.x) > 0.001f;
        string target = isMoving ? PLAYER_SNEAK : PLAYER_SNEAKIDLE;

        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(target))
            anim.Play(target);
    }
}

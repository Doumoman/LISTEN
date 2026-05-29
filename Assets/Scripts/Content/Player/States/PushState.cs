using UnityEngine;

public class PushState : PlayerBaseState
{
    private IPushable _pushable;

    public PushState(PlayerFSM fsm) : base(fsm) { }

    public override void Enter()
    {
        _pushable = data.nearPushableCollider?.GetComponent<IPushable>();
    }

    public override void Update()
    {
        // 땅에서 벗어나면 → AirborneState
        if (!data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 점프 이탈 (공중으로 나감) → AirborneState
        if (data.isJumpRequested)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // Pushable 감지 해제 → MoveState
        if (!data.isPushing || _pushable == null)
        {
            fsm.TransitionTo(fsm.MoveState);
            return;
        }

        float vel = data.moveHorizontalInput.x * data.pushSpeed;
        fsm.SetMoveVelocity(vel, 0f);
        _pushable.SetHorizontalVelocity(vel);

        // ── 애니메이션 ──
        // TODO
    }

    public override void Exit()
    {
        if (_pushable != null)
            _pushable.SetHorizontalVelocity(0f);
        _pushable = null;
        fsm.SetMoveVelocity(0f, 0f);
    }
}

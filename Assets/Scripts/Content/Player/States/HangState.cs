using UnityEngine;

public class HangState : PlayerBaseState
{
    private const float SnapSpeed = 20f;

    public HangState(PlayerFSM fsm) : base(fsm) { }

    private Vector2 _targetPos;

    public override void Enter()
    {
        fsm.SetVelocity(0f, 0f);
        _targetPos = fsm.ledgeBeginPos + data.ledgeOffset;
    }

    public override void Update()
    {
        Vector3 pos = fsm.transform.position;

        pos.x = Mathf.MoveTowards(pos.x, _targetPos.x, SnapSpeed * Time.deltaTime);
        pos.y = Mathf.MoveTowards(pos.y, _targetPos.y, SnapSpeed * Time.deltaTime);

        fsm.transform.position = pos;
        fsm.SetVelocity(0f, 0f);

        // 점프 → AirborneState
        if (data.isJumpRequested)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }
    }

    public override void Exit()
    {
        fsm.ResetCanGrabLedge();
    }
}

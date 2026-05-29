using System;
using UnityEngine;

public class HangState : PlayerBaseState
{
    private const float SnapSpeed = 20f;

    public HangState(PlayerFSM fsm) : base(fsm) { }

    private Vector2 _targetPos;

    public override void Enter()
    {
        data.isHanging = true;
        data.isFalling = false;
        fsm.SetMoveVelocity(0f, 0f);

        // offset은 바라보는 방향의 반대로 
        _targetPos = data.ledgeCornerPos + new Vector2(data.ledgeOffset.x * (-data.ledgeGrabDir), data.ledgeOffset.y);
    }

    public override void Update()
    {
        Vector3 pos = fsm.transform.position;
        pos.x = Mathf.MoveTowards(pos.x, _targetPos.x, SnapSpeed * Time.deltaTime);
        pos.y = Mathf.MoveTowards(pos.y, _targetPos.y, SnapSpeed * Time.deltaTime);
        fsm.transform.position = pos;
        fsm.SetMoveVelocity(0f, 0f);

        // 점프 → AirborneState
        if (data.isJumpRequested)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }
    }

    public override void Exit()
    {
        data.isHanging = false;
    }
}

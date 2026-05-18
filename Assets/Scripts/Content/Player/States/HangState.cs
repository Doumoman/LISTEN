using UnityEngine;

public class HangState : PlayerBaseState
{
    private const float SnapSpeed = 20f;

    public HangState(PlayerFSM fsm) : base(fsm) { }
    private Collider2D _hangableCollider;
    private float _targetY;

    public override void Enter()
    {
        fsm.SetVelocity(0f, 0f);
        _hangableCollider = data.nearHangerCollider;
        Bounds b = _hangableCollider.bounds;
        _targetY = b.min.y + b.size.y * 0.75f;
    }

    public override void Update()
    {
        // Y 스냅 (MoveTowards로 부드럽게)
        Vector3 pos = fsm.transform.position;
        pos.y = Mathf.MoveTowards(pos.y, _targetY, SnapSpeed * Time.deltaTime);
        fsm.transform.position = pos;

        fsm.SetVelocity(0f, 0f);

        // 점프 → 새 점프로 AirborneState 전환 (점프 초기화 효과)
        // 점프 키로만 HangState 탈출 가능
        if (data.isJumpRequested)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 착지 예외 처리
        if (data.isGrounded)
        {
            fsm.TransitionTo(fsm.MoveState);
            return;
        }

        // Hangable이 감지 범위를 벗어난 경우
        if (!data.isNearHanger)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }
    }

    public override void Exit() { }
}

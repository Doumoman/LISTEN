using UnityEngine;

public class LadderState : PlayerBaseState
{
    private const string PLAYER_LADDER = "Player_Ladder";

    public LadderState(PlayerFSM fsm) : base(fsm) { }
    private Collider2D _ladderCollider;

    public override void Enter()
    {
        fsm.SetMoveVelocity(0f, 0f);
        PlayAnim();

        _ladderCollider = data.nearLadderCollider;

        // 사다리의 x 좌표로 Snapping
        if (data.nearLadderCollider != null)
        {
            Vector3 pos = fsm.transform.position;
            pos.x = _ladderCollider.bounds.center.x;
            fsm.transform.position = pos;
        }
    }

    public override void Update()
    {
        if (data.isNearLadder)
        {
            // 사다리 하단 이탈 (땅에 닿음) → MoveState
            if (data.isGrounded && data.MoveVerticalInput.y <= 0f)
            {
                fsm.TransitionTo(fsm.MoveState);
                return;
            }

            // 사다리 점프 이탈 (공중으로 나감) → AirborneState
            if (data.isJumpRequested)
            {
                fsm.TransitionTo(fsm.AirborneState);
                return;
            }

            // 이동 처리, ladderSpeed 사용
            float verticalVel = data.MoveVerticalInput.y * data.ladderSpeed;

            // 사다리 꼭대기 위로는 움직일 수 없음
            // 사다리를 타고 가야하는 플랫폼이 있다면 사다리를 길게 만들면 된다
            if (_ladderCollider != null && verticalVel > 0f)
            {
                float ladderTop = _ladderCollider.bounds.max.y;
                float nextY = fsm.transform.position.y + verticalVel * Time.deltaTime;
                if (nextY >= ladderTop)
                {
                    Vector3 pos = fsm.transform.position;
                    pos.y = ladderTop;
                    fsm.transform.position = pos;
                    verticalVel = 0f;
                }
            }

            fsm.SetMoveVelocity(0f, verticalVel);

            anim.speed = Mathf.Abs(data.MoveVerticalInput.y) > 0.001f ? 1f : 0f;
            PlayAnim();
        }
        else
        {
            fsm.TransitionTo(fsm.MoveState);
        }
    }

    public override void Exit()
    {
        anim.speed = 1f;
        fsm.SetMoveVelocity(0f, 0f);
    }

    private void PlayAnim()
    {
        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(PLAYER_LADDER))
            anim.Play(PLAYER_LADDER);
    }
}

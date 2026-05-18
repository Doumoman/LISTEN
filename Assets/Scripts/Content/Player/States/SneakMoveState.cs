using UnityEngine;

public class SneakMoveState : PlayerBaseState
{
    private const string PLAYER_SNEAK = "Player_Sneak";
    private const string PLAYER_SNEAKIDLE = "Player_SneakIdle";
    private const float TRANSITION_DURATION = 0.12f;

    private float _colliderT = 0f; // 0 = 서있기, 1 = 웅크리기

    public SneakMoveState(PlayerFSM fsm) : base(fsm) { }

    public override void Enter()
    {
        fsm.SetVelocity(0f, 0f);

        fsm.Bc.size = data.standingColliderSize;
        fsm.Bc.offset = data.standingColliderOffset;

        _colliderT = 0f;

        PlayAnim();
    }

    public override void Update()
    {
        // 엎드리기 키를 누르고 있음 -> SneakMoveState 유지
        if (data.isSneakHeld)
        {
            _colliderT = Mathf.MoveTowards(_colliderT, 1f, Time.deltaTime / TRANSITION_DURATION);
        }
        else
        {
            _colliderT = Mathf.MoveTowards(_colliderT, 0f, Time.deltaTime / TRANSITION_DURATION);
            if (_colliderT <= 0f)
            {
                LerpCollider(0f); // 기존 콜라이더로 Lerp 시켜주고 return을 해야 y좌표 누적 오차가 생기지 않음
                fsm.TransitionTo(fsm.MoveState);
                return;
            }
        }
        LerpCollider(_colliderT); // 콜라이더 부드럽게 변경

        // 점프 → 즉시 AirborneState
        if (data.isJumpRequested && data.isGrounded)
        {
            LerpCollider(0f);
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 지면 이탈 → AirborneState
        if (!data.isGrounded)
        {
            LerpCollider(0f);
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // TODO: 엎드린 상태에서 상호작용 키 누르면 물체 주움


        // 실제 velocity 적용
        float horizontalVel = data.moveHorizontalInput.x * data.sneakSpeed;
        fsm.SetVelocity(horizontalVel, 0f);
        PlayAnim();
    }

    public override void Exit()
    {
        fsm.Bc.size = data.standingColliderSize;
        fsm.Bc.offset = data.standingColliderOffset;
    }

    private void LerpCollider(float t)
    {
        // 콜라이더 변경 전 하단 위치를 현재 물리 위치 기준으로 계산
        float currentBottom = fsm.transform.position.y + fsm.Bc.offset.y - fsm.Bc.size.y * 0.5f;

        fsm.Bc.size = Vector2.Lerp(data.standingColliderSize, data.sneakColliderSize, t);
        fsm.Bc.offset = Vector2.Lerp(data.standingColliderOffset, data.sneakColliderOffset, t);

        // 콜라이더 하단이 현재 지형 위치를 유지하도록 Y 보정
        Vector3 pos = fsm.transform.position;
        pos.y = currentBottom - fsm.Bc.offset.y + fsm.Bc.size.y * 0.5f;
        fsm.transform.position = pos;
    }

    private void PlayAnim()
    {
        bool isMoving = Mathf.Abs(data.moveHorizontalInput.x) > 0.001f;
        string target = isMoving ? PLAYER_SNEAK : PLAYER_SNEAKIDLE;
        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(target))
            anim.Play(target);
    }
}

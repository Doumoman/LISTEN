using UnityEngine;

public class SneakMoveState : PlayerBaseState
{
    private const string PLAYER_SNEAK = "Player_Sneak";
    private const string PLAYER_SNEAKIDLE = "Player_SneakIdle";
    private const float TRANSITION_DURATION = 0.12f;

    private float _groundY;
    private float _colliderT = 0f; // 0 = 서있기, 1 = 웅크리기

    public SneakMoveState(PlayerFSM fsm) : base(fsm) { }

    public override void Enter()
    {
        fsm.SetVelocity(0f, 0f);

        fsm.Bc.size = data.standingColliderSize;
        fsm.Bc.offset = data.standingColliderOffset;

        _groundY = fsm.transform.position.y + data.standingColliderOffset.y - data.standingColliderSize.y * 0.5f;
        _colliderT = 0f;

        PlayAnim();
    }

    public override void Update()
    {
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

        LerpCollider(_colliderT);

        // 점프 → 즉시 AirborneState
        if (data.jumpRequested && data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // 지면 이탈 → 즉시 AirborneState
        if (!data.isGrounded)
        {
            fsm.TransitionTo(fsm.AirborneState);
            return;
        }

        // TODO: 엎드린 상태에서 상호작용 키 누르면 물체 주움

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
        fsm.Bc.size = Vector2.Lerp(data.standingColliderSize, data.sneakColliderSize, t);
        fsm.Bc.offset = Vector2.Lerp(data.standingColliderOffset, data.sneakColliderOffset, t);

        // _groundY 기준으로 Y를 직접 계산
        Vector3 pos = fsm.transform.position;
        pos.y = _groundY - fsm.Bc.offset.y + fsm.Bc.size.y * 0.5f;
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

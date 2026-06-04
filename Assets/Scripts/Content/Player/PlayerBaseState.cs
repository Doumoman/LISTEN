using UnityEngine;

/// <summary>
/// 플레이어 FSM 상태 추상 기반 클래스.
/// </summary>
public abstract class PlayerBaseState
{
    protected PlayerFSM fsm;
    protected PlayerData data;
    protected Animator anim;

    public PlayerBaseState(PlayerFSM fsm)
    {
        this.fsm = fsm;
        data = fsm.PlayerData;
        anim = fsm.Anim;
    }

    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();

    protected void PlayClip(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return;
        if (!anim.GetCurrentAnimatorStateInfo(0).IsName(clipName))
            anim.Play(clipName);
    }
}

public static class AnimClips
{
    public const string Idle = "Player_Idle";
    public const string Move = "Player_Move";
    public const string SneakIdle = "Player_SneakIdle";
    public const string SneakMove = "Player_Sneak";
    public const string Ladder = "Player_Ladder";

    // TODO: Animator 상태명 확인 후 수정 필요
    public const string Airborne = "Airborne";
    public const string Hanging = "Hanging";
    public const string Death = "Death";
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class StoryManager : IManager
{
    // ㅡㅡㅡㅡㅡ Yarn Spinner에서 사용할 커맨드 이름 ex) <<block_player_input>> ㅡㅡㅡㅡㅡ
    private const string BLOCK_PLAYER_INPUT = "block_player_input";
    private const string FADE_IN = "fade_in";
    private const string FADE_OUT = "fade_out";
    private const string WAIT_FOR_INTERACTION = "wait_for_interaction";
    private const string TELEPORT_PLAYER = "teleport_player";

    private DialogueRunner _runner;
    private bool _init = false;

    private string _waitingInteractionName = String.Empty; // 수행되기를 기다리는 상호작용 이름
    private bool _isInteractionCompleted = false; // 상호작용 완료 플래그

    private readonly Dictionary<string, Transform> _teleportPoints = new();

    public void Init()
    {
        if (_init) return;
        _init = true;
    }

    public void Clear() { _teleportPoints.Clear(); }
    public void OnDestroy()
    {
        UnregisterCommands();
        _runner = null;
        _init = false;
    }

    /// <summary>
    /// 씬 바인더에서 DialogueRunner를 주입한다.
    /// </summary>
    public void RegisterRunner(DialogueRunner runner)
    {
        if (_runner != null)
            UnregisterCommands();

        _runner = runner;
        RegisterCommands();
    }

    /// <summary>
    /// 특정 스토리 노드를 시작한다. 인자로 Yarn Script의 타이틀 이름을 넣어준다.
    /// </summary>
    public async YarnTask StartStory(string nodeName, Action onComplete = null)
    {
        if (_runner == null) return;
        if (!_runner.Dialogue.NodeExists(nodeName)) return;

        await _runner.StartDialogue(nodeName);
        onComplete?.Invoke();
    }

    public async YarnTask StopStory()
    {
        if (_runner == null || !_runner.IsDialogueRunning) return;
        _waitingInteractionName = string.Empty;
        _isInteractionCompleted = false;
        await _runner.Stop();
    }

    public bool IsRunning => _runner != null && _runner.IsDialogueRunning; // 대화가 진행중

    private void RegisterCommands()
    {
        if (_runner == null) return;

        _runner.AddCommandHandler(BLOCK_PLAYER_INPUT, (bool isTalking) => { SingletonManagers.Input.SetInputModeUI(isTalking); });
        _runner.AddCommandHandler(FADE_IN, (System.Func<float, IEnumerator>)CmdFadeIn);
        _runner.AddCommandHandler(FADE_OUT, (System.Func<float, IEnumerator>)CmdFadeOut);
        _runner.AddCommandHandler(WAIT_FOR_INTERACTION, (System.Func<string, IEnumerator>)CmdWaitForInteraction);
        _runner.AddCommandHandler(TELEPORT_PLAYER, (System.Action<string>)CmdTeleportPlayer);

        // TODO: 게임 전용 커맨드 등록
        // _runner.AddCommandHandler("커맨드명", (Action<파라미터타입>) CmdXxx);
    }

    private void UnregisterCommands()
    {
        if (_runner == null) return;

        _runner.RemoveCommandHandler(FADE_IN);
        _runner.RemoveCommandHandler(FADE_OUT);
        _runner.RemoveCommandHandler(WAIT_FOR_INTERACTION);
        _runner.RemoveCommandHandler(TELEPORT_PLAYER);

        // TODO: 게임 전용 커맨드 해제
        // _runner.RemoveCommandHandler("커맨드 명");
    }

    /// <summary>
    /// 씬 바인더에서 텔레포트 포인트를 등록한다.
    /// </summary>
    public void RegisterTeleportPoint(string name, Transform point) => _teleportPoints[name] = point;
    public void UnregisterTeleportPoint(string name) => _teleportPoints.Remove(name);

    #region Yarn Spinner에서 사용할 커맨드 함수들

    private IEnumerator CmdFadeIn(float duration) // 화면 페이드 인
    {
        bool done = false;
        SingletonManagers.UI.GetFade().FadeIn(duration, () => done = true);
        yield return new WaitUntil(() => done);
    }

    private IEnumerator CmdFadeOut(float duration) // 화면 페이드 아웃
    {
        bool done = false;
        SingletonManagers.UI.GetFade().FadeOut(duration, () => done = true);
        yield return new WaitUntil(() => done);
    }

    private IEnumerator CmdWaitForInteraction(string interactionName) // 대화 도중 특정 상호작용을 수행할 때 까지 대화 멈춤
    {
        _waitingInteractionName = interactionName;
        _isInteractionCompleted = false;

        SingletonManagers.Input.SetInputModeUI(false); // 플레이어가 이동해서 상호작용할 수 있도록 허용

        yield return new WaitUntil(() => _isInteractionCompleted);

        SingletonManagers.Input.SetInputModeUI(true); // 대화 모드로 복귀

        _waitingInteractionName = string.Empty;
        _isInteractionCompleted = false;
    }

    private void CmdTeleportPlayer(string destinationName) // 플레이어를 등록된 텔레포트 포인트로 순간이동
    {
        if (!_teleportPoints.TryGetValue(destinationName, out var dest))
        {
            Debug.LogWarning($"[StoryManager] 텔레포트 포인트 '{destinationName}'을 찾을 수 없습니다.");
            return;
        }

        var player = UnityEngine.Object.FindFirstObjectByType<PlayerFSM>();
        if (player != null)
            player.transform.position = dest.position;
    }

    #endregion

    /// <summary>
    /// 외부에서 특정 상호작용에 성공하면 이름과 함께 호출하여 상호작용이 완료됐음을 알린다.
    /// </summary>
    public void NotifyInteractionCompleted(string interactionName)
    {
        // 현재 스토리가 기다리고 있는 상호작용 이름과 일치한다면 
        if (_waitingInteractionName == interactionName)
        {
            _isInteractionCompleted = true;
        }
    }

    public Coroutine RunCoroutine(IEnumerator routine)
    {
        return SingletonManagers.Instance.StartCoroutine(routine);
    }

    public void StopCoroutine(Coroutine coroutine)
    {
        if (coroutine != null)
            SingletonManagers.Instance.StopCoroutine(coroutine);
    }
}

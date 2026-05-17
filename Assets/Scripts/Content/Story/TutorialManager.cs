using System;
using UnityEngine;
using Yarn.Unity;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string startNode = "Tutorial_Fisher";
    [SerializeField] private TeleportEntry[] teleportPoints;

    private StoryManager _storyManager;

    private void Start()
    {
        _storyManager = SingletonManagers.Story;
        _storyManager.RegisterRunner(dialogueRunner);

        foreach (var tp in teleportPoints) // 텔레포트 포인트들을 등록
            _storyManager.RegisterTeleportPoint(tp.name, tp.point);

        _= _storyManager.StartStory(startNode);
        // SingletonManagers.Input.SetInputModeUI(true);
    }

    private void OnDestroy()
    {
        foreach (var tp in teleportPoints)
            _storyManager.UnregisterTeleportPoint(tp.name);
    }

    // 애니메이션 이벤트·트리거 등 외부에서 챕터 특정 상호작용 완료 알림
    public void NotifyInteraction(string interactionName) =>
        _storyManager.NotifyInteractionCompleted(interactionName);
}

[Serializable]
public class TeleportEntry
{
    public string name;
    public Transform point;
}
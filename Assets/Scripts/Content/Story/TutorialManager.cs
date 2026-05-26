using NUnit.Framework.Constraints;
using System;
using UnityEngine;
using Yarn.Unity;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private const string startNode = "Tutorial_Narration";
    [SerializeField] private TeleportEntry[] teleportPoints;

    private StoryManager _storyManager;

    private void Start()
    {
        _storyManager = SingletonManagers.Story;
        _storyManager.RegisterRunner(dialogueRunner);

        foreach (var tp in teleportPoints) // 텔레포트 포인트들을 등록
            _storyManager.RegisterTeleportPoint(tp.name, tp.point);

        _ = _storyManager.StartStory(startNode);
    }

    private void OnDestroy()
    {
        foreach (var tp in teleportPoints)
            _storyManager.UnregisterTeleportPoint(tp.name);
    }

    public void NotifyInteraction(string interactionName) => _storyManager.NotifyInteractionCompleted(interactionName);



}

[Serializable]
public class TeleportEntry
{
    public string name;
    public Transform point;
}
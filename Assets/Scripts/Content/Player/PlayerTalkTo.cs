using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerTalkTo : MonoBehaviour
{
    [SerializeField] private LayerMask npcLayer;
    [SerializeField] private float detectionRange = 1.5f;

    private InputManager _inputManager;
    private BoxCollider2D _bc;
    private Collider2D _nearNPC;

    private void Start()
    {
        _bc = GetComponent<BoxCollider2D>();
        _inputManager = SingletonManagers.Input;

        _inputManager.OnTalkPressed -= HandleTalk;
        _inputManager.OnTalkPressed += HandleTalk;
    }

    private void Update()
    {
        CheckNPC();
    }

    private void CheckNPC()
    {
        Vector2 center = (Vector2)transform.position + _bc.offset;
        _nearNPC = Physics2D.OverlapCircle(center, detectionRange, npcLayer);
    }

    private void HandleTalk()
    {
        if (_nearNPC == null) return;

        var npc = _nearNPC.GetComponent<NPCInteractable>();
        if (npc == null) return;

        var story = SingletonManagers.Story;

        if (story.IsRunning)
        {
            story.NotifyInteractionCompleted(npc.interactionName);
        }
        else if (!string.IsNullOrEmpty(npc.dialogueNode))
        {
            // 메인 스토리가 끝난 뒤 다시 말을 걸 때 독립 노드 실행
            SingletonManagers.Input.SetInputModeUI(true);
            _ = story.StartStory(npc.dialogueNode, onComplete: () =>
                SingletonManagers.Input.SetInputModeUI(false));
        }
    }

    private void OnDestroy()
    {
        if (_inputManager != null)
            _inputManager.OnTalkPressed -= HandleTalk;
    }
}
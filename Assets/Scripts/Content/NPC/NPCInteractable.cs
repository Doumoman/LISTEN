using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
    [Tooltip("wait_for_interaction 커맨드에 전달되는 키 값")]
    public string interactionName;

    [Tooltip("메인 스토리 외 독립 대화 노드 (비어 있으면 무시)")]
    public string dialogueNode;
}
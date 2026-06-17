using UnityEngine;

/// <summary>
/// 미각 게이트 — 특정 열매(_required)를 먹은 동안에만 열리는(통과 가능한) 장벽.
/// _blocker 의 콜라이더/비주얼을 on/off 해서 길을 막거나 연다.
/// 예: 위상(Phase) 열매를 먹으면 위상 벽이 사라져 통과 가능.
/// </summary>
public class FruitGate : MonoBehaviour
{
    [SerializeField] private TasteKind _required = TasteKind.Phase;
    [SerializeField] private GameObject _blocker;     // 막고 있는 벽(콜라이더 + 비주얼)
    [SerializeField] private bool _openWhenHas = true; // true: 열매 있으면 열림 / false: 열매 있으면 닫힘
    [SerializeField] private SpriteRenderer _statusLight;
    [SerializeField] private Color _openColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private Color _closedColor = new Color(1f, 0.35f, 0.3f);

    private PlayerTaste _taste;

    private void Start()
    {
        PlayerFSM player = FindFirstObjectByType<PlayerFSM>();
        if (player != null) _taste = player.GetComponent<PlayerTaste>();

        if (_blocker == null && transform.childCount > 0)
            _blocker = transform.GetChild(0).gameObject;
    }

    private void Update()
    {
        bool has = _taste != null && _taste.Has(_required);
        bool open = _openWhenHas ? has : !has;

        if (_blocker != null && _blocker.activeSelf == open)
            _blocker.SetActive(!open);

        if (_statusLight != null)
            _statusLight.color = open ? _openColor : _closedColor;
    }
}

using System.Collections;
using UnityEngine;

public class UI_Popup_FileAction : UI_Popup
{
    enum GameObjects
    {
        Action_StartGame,
        Action_Rename
    }

    private UI_FocusMenuItem[] _items;
    private int _selectedIndex = 0;
    private int _fileSlotIndex = -1;

    private UI_Popup_FileSelect _owner;
    private CanvasGroup _canvasGroup;
    private RectTransform _rect;
    private Vector2 _originPos;

    private bool _isAnimating = false;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Popup Animation")]
    [SerializeField] private float _appearDuration = 0.3f;
    [SerializeField] private float _disappearDuration = 0.25f;
    [SerializeField] private float _rightOffset = 300f;

    public void SetFileSlot(int index)
    {
        _fileSlotIndex = index;
    }
    public void SetOwner(UI_Popup_FileSelect owner)
    {
        _owner = owner;
    }
    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _rect = GetComponent<RectTransform>();
        _originPos = _rect.anchoredPosition;

        _items = new UI_FocusMenuItem[]
        {
            Get<GameObject>((int)GameObjects.Action_StartGame).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.Action_Rename).GetComponent<UI_FocusMenuItem>(),
        };

        _items[0].SetText("게임 시작");
        _items[1].SetText("이름 바꾸기");

        RefreshFocus();

        StartCoroutine(AppearFromRightSequence());
    }

    private IEnumerator AppearFromRightSequence()
    {
        _isAnimating = true;

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Vector2 startPos = _originPos + Vector2.right * Mathf.Abs(_rightOffset);
        Vector2 endPos = _originPos;

        _rect.anchoredPosition = startPos;

        float timer = 0f;

        while (timer < _appearDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _appearDuration);
            float eased = EaseOutCubic(t);

            _rect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);

            yield return null;
        }

        _rect.anchoredPosition = endPos;
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        _isAnimating = false;
    }

    private IEnumerator DisappearToRightSequence()
    {
        _isAnimating = true;

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Vector2 startPos = _rect.anchoredPosition;
        Vector2 endPos = _originPos + Vector2.right * Mathf.Abs(_rightOffset);

        float startAlpha = _canvasGroup.alpha;
        float timer = 0f;

        while (timer < _disappearDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _disappearDuration);
            float eased = EaseOutCubic(t);

            _rect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);

            yield return null;
        }

        _rect.anchoredPosition = endPos;
        _canvasGroup.alpha = 0f;

        _isAnimating = false;
    }

    public override void OnInput(Vector2 dir)
    {
        if (_isAnimating) return;

        if (Time.unscaledTime - _lastMoveTime < _moveCooldown)
            return;

        if (dir.y > 0.5f)
        {
            MoveFocus(-1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.y < -0.5f)
        {
            MoveFocus(1);
            _lastMoveTime = Time.unscaledTime;
        }
    }

    private void MoveFocus(int delta)
    {
        _selectedIndex += delta;

        if (_selectedIndex < 0)
            _selectedIndex = _items.Length - 1;
        else if (_selectedIndex >= _items.Length)
            _selectedIndex = 0;

        RefreshFocus();
    }

    private void RefreshFocus()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            _items[i].SetSelected(i == _selectedIndex);
        }
    }

    public override void OnSubmit()
    {
        if (_isAnimating) return;

        switch (_selectedIndex)
        {
            case 0:
                StartCoroutine(StartGameSequence());
                break;

            case 1:
                RenameFile();
                break;
        }
    }

    private IEnumerator StartGameSequence()
    {
        Debug.Log($"파일 {_fileSlotIndex + 1}번 선택");

        if (_owner == null)
        {
            Debug.LogError("[FileAction] Owner FileSelect가 없습니다.");
            yield break;
        }

        _owner.RequestStageSelectImmediate(this);

        yield return null;
    }

    private void RenameFile()
    {
        if (_isAnimating) return;

        Debug.Log($"파일 {_fileSlotIndex + 1}번 이름 바꾸기");

        // 나중에 이름 입력 팝업 연결
        // SingletonManagers.UI.ShowPopupUI<UI_Popup_RenameFile>();
    }

    public override void OnCancel()
    {
        if (_isAnimating) return;

        StartCoroutine(CloseSequence());
    }

    private IEnumerator CloseSequence()
    {
        yield return StartCoroutine(DisappearToRightSequence());
        ClosePopupUI();
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
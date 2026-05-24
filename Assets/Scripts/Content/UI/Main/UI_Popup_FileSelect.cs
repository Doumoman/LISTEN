using System.Collections;
using UnityEngine;

public class UI_Popup_FileSelect : UI_Popup
{
    enum GameObjects
    {
        FileSlot_0,
        FileSlot_1,
        FileSlot_2
    }

    private UI_Scene_Main _owner;
    private UI_SaveSlotItem[] _slots;
    private int _selectedIndex = 0;

    private CanvasGroup _canvasGroup;
    private RectTransform _rect;
    private Vector2 _originPos;

    private bool _isAnimating = false;
    private bool _isHiddenForStageSelect = false;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Popup Animation")]
    [SerializeField] private float _appearDuration = 0.45f;
    [SerializeField] private float _disappearDuration = 0.35f;
    [SerializeField] private float _startYOffset = -80f;

    public int SelectedIndex => _selectedIndex;

    public void SetOwner(UI_Scene_Main owner)
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

        _slots = new UI_SaveSlotItem[]
        {
            Get<GameObject>((int)GameObjects.FileSlot_0).GetComponent<UI_SaveSlotItem>(),
            Get<GameObject>((int)GameObjects.FileSlot_1).GetComponent<UI_SaveSlotItem>(),
            Get<GameObject>((int)GameObjects.FileSlot_2).GetComponent<UI_SaveSlotItem>(),
        };

        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].SetEmpty(i);
        }

        RefreshFocus();

        StartCoroutine(AppearSequence());
    }

    private IEnumerator AppearSequence()
    {
        _isAnimating = true;
        _isHiddenForStageSelect = false;

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Vector2 startPos = _originPos + Vector2.down * Mathf.Abs(_startYOffset);
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

    public IEnumerator HideDownForStageSelect()
    {
        yield return StartCoroutine(DisappearDownSequence(false));
        _isHiddenForStageSelect = true;
    }

    public void ShowAgainFromStageSelect()
    {
        if (!_isHiddenForStageSelect)
            return;

        StopAllCoroutines();
        StartCoroutine(AppearSequence());
    }

    public void RequestStageSelectImmediate(UI_Popup_FileAction actionPopup)
    {
        if (_isAnimating) return;

        StartCoroutine(StageSelectImmediateSequence(actionPopup));
    }

    private IEnumerator StageSelectImmediateSequence(UI_Popup_FileAction actionPopup)
    {
        Debug.Log("[FileSelect] StageSelect 즉시 전환 시작");

        // 1. FileAction 즉시 닫기
        if (actionPopup != null)
        {
            SingletonManagers.UI.ClosePopupUI(actionPopup);
        }

        // 2. FileSelect는 Destroy하지 않고 즉시 숨김
        _isHiddenForStageSelect = true;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_rect != null)
        {
            _rect.anchoredPosition = _originPos + Vector2.down * Mathf.Abs(_startYOffset);
        }

        // 3. StageSelect 즉시 생성
        UI_Popup_StageSelect stageSelect = SingletonManagers.UI.ShowPopupUI<UI_Popup_StageSelect>();

        if (stageSelect == null)
        {
            Debug.LogError("[FileSelect] StageSelect 생성 실패. Resources/UI/Popup/UI_Popup_StageSelect.prefab 확인 필요.");

            ShowAgainFromStageSelect();
            yield break;
        }

        stageSelect.SetReturnFileSelect(this);
        SingletonManagers.Input.SetInputModeUI(true);

        yield return null;
    }
    private IEnumerator CloseAndReturnMainSequence()
    {
        yield return StartCoroutine(DisappearDownSequence(true));

        ClosePopupUI();

        if (_owner != null)
            _owner.ShowMainMenu();
    }

    private IEnumerator DisappearDownSequence(bool closeAfter)
    {
        _isAnimating = true;

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Vector2 startPos = _rect.anchoredPosition;
        Vector2 endPos = _originPos + Vector2.down * Mathf.Abs(_startYOffset);

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
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _isAnimating = false;
    }

    public override void OnInput(Vector2 dir)
    {
        if (_isAnimating) return;
        if (_isHiddenForStageSelect) return;

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
            _selectedIndex = _slots.Length - 1;
        else if (_selectedIndex >= _slots.Length)
            _selectedIndex = 0;

        RefreshFocus();
    }

    private void RefreshFocus()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].SetSelected(i == _selectedIndex);
        }
    }

    public override void OnSubmit()
{
    if (_isAnimating) return;
    if (_isHiddenForStageSelect) return;

    UI_Popup_FileAction actionPopup = SingletonManagers.UI.ShowPopupUI<UI_Popup_FileAction>();

    if (actionPopup == null)
    {
        Debug.LogError("[FileSelect] FileAction 생성 실패");
        return;
    }

    actionPopup.SetFileSlot(_selectedIndex);
    actionPopup.SetOwner(this);
}

    public override void OnCancel()
    {
        if (_isAnimating) return;
        if (_isHiddenForStageSelect) return;

        StartCoroutine(CloseAndReturnMainSequence());
    }
    public void RequestStageSelectFromAction(UI_Popup_FileAction actionPopup)
    {
        if (_isAnimating) return;

        StartCoroutine(StageSelectSequence(actionPopup));
    }

    private IEnumerator StageSelectSequence(UI_Popup_FileAction actionPopup)
    {
        Debug.Log("[FileSelect] StageSelectSequence 시작");

        // 1. FileAction 팝업을 먼저 닫는다.
        if (actionPopup != null)
        {
            SingletonManagers.UI.ClosePopupUI(actionPopup);
        }

        // 2. FileSelect는 Destroy하지 않고 아래로 숨긴다.
        yield return StartCoroutine(HideDownForStageSelect());

        Debug.Log("[FileSelect] StageSelect 생성 시도");

        // 3. StageSelect 팝업을 생성한다.
        UI_Popup_StageSelect stageSelect = SingletonManagers.UI.ShowPopupUI<UI_Popup_StageSelect>();

        if (stageSelect == null)
        {
            Debug.LogError("[FileSelect] StageSelect 생성 실패. Resources/UI/Popup/UI_Popup_StageSelect.prefab 확인 필요.");

            // 실패했으면 다시 FileSelect를 보여준다.
            ShowAgainFromStageSelect();
            yield break;
        }

        Debug.Log("[FileSelect] StageSelect 생성 성공");

        stageSelect.SetReturnFileSelect(this);
        SingletonManagers.Input.SetInputModeUI(true);
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
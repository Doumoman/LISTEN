using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Popup_KeyConfig : UI_Popup
{
    enum GameObjects
    {
        Viewport,
        Content,

        Item_MoveLeft,
        Item_MoveRight,
        Item_MoveUp,
        Item_MoveDown,
        Item_Jump,
        Item_Interact,
        Item_Menu,
        Item_Talk,

        HelpText
    }

    private TextMeshProUGUI[] _texts;
    private RectTransform[] _itemRects;

    private RectTransform _viewportRect;
    private RectTransform _contentRect;
    private TextMeshProUGUI _helpText;

    private int _selectedIndex = 0;
    private bool _waitingKey = false;
    private Coroutine _duplicateRoutine;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Manual Layout")]
    [SerializeField] private float _itemHeight = 80f;
    [SerializeField] private float _itemSpacing = 18f;
    [SerializeField] private float _contentWidth = 1000f;

    [SerializeField] private float _focusCenterOffsetY = 0f;
    [SerializeField] private float _verticalPadding = 120f;

    [Header("Scroll")]
    [SerializeField] private float _scrollSmoothSpeed = 18f;

    [Header("Color")]
    [SerializeField] private Color _normalColor = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.25f);
    [SerializeField] private Color _waitingColor = new Color(1f, 0.5f, 0.4f);
    [SerializeField] private Color _duplicateColor = Color.red;

    [Header("Font Size")]
    [SerializeField] private float _normalFontSize = 70f;
    [SerializeField] private float _selectedFontSize = 80f;

    private float _targetContentY;

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        _viewportRect = Get<GameObject>((int)GameObjects.Viewport).GetComponent<RectTransform>();
        _contentRect = Get<GameObject>((int)GameObjects.Content).GetComponent<RectTransform>();

        _texts = new TextMeshProUGUI[]
        {
            GetTMP(GameObjects.Item_MoveLeft),
            GetTMP(GameObjects.Item_MoveRight),
            GetTMP(GameObjects.Item_MoveUp),
            GetTMP(GameObjects.Item_MoveDown),
            GetTMP(GameObjects.Item_Jump),
            GetTMP(GameObjects.Item_Interact),
            GetTMP(GameObjects.Item_Menu),
            GetTMP(GameObjects.Item_Talk),
        };

        _itemRects = new RectTransform[]
        {
            GetRect(GameObjects.Item_MoveLeft),
            GetRect(GameObjects.Item_MoveRight),
            GetRect(GameObjects.Item_MoveUp),
            GetRect(GameObjects.Item_MoveDown),
            GetRect(GameObjects.Item_Jump),
            GetRect(GameObjects.Item_Interact),
            GetRect(GameObjects.Item_Menu),
            GetRect(GameObjects.Item_Talk),
        };

        _helpText = GetTMP(GameObjects.HelpText);

        SetupViewport();
        SetupManualItemLayout();

        RefreshUI();
        CenterFocusItem(true);
    }

    private void Update()
    {
        if (_contentRect == null) return;

        Vector2 pos = _contentRect.anchoredPosition;
        pos.y = Mathf.Lerp(pos.y, _targetContentY, Time.unscaledDeltaTime * _scrollSmoothSpeed);
        _contentRect.anchoredPosition = pos;
    }

    private void SetupViewport()
    {
        if (_viewportRect == null) return;

        if (_viewportRect.GetComponent<RectMask2D>() == null)
            _viewportRect.gameObject.AddComponent<RectMask2D>();
    }

    private void SetupManualItemLayout()
    {
        if (_contentRect == null) return;

        // LayoutGroup / ContentSizeFitter가 있으면 위치를 덮어쓸 수 있으므로 비활성화
        VerticalLayoutGroup layout = _contentRect.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        ContentSizeFitter fitter = _contentRect.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.enabled = false;

        _contentRect.anchorMin = new Vector2(0.5f, 1f);
        _contentRect.anchorMax = new Vector2(0.5f, 1f);
        _contentRect.pivot = new Vector2(0.5f, 1f);

        float pitch = _itemHeight + _itemSpacing;

        float contentHeight =
            _verticalPadding * 2f +
            _itemRects.Length * _itemHeight +
            (_itemRects.Length - 1) * _itemSpacing;

        _contentRect.sizeDelta = new Vector2(_contentWidth, contentHeight);

        for (int i = 0; i < _itemRects.Length; i++)
        {
            RectTransform item = _itemRects[i];
            if (item == null) continue;

            item.anchorMin = new Vector2(0.5f, 1f);
            item.anchorMax = new Vector2(0.5f, 1f);
            item.pivot = new Vector2(0.5f, 1f);

            item.sizeDelta = new Vector2(_contentWidth, _itemHeight);

            // 위쪽 여백만큼 내려서 배치
            item.anchoredPosition = new Vector2(0f, -_verticalPadding - i * pitch);
        }

        _contentRect.anchoredPosition = Vector2.zero;
        _targetContentY = 0f;
    }

    public override void OnInput(Vector2 dir)
    {
        if (_waitingKey) return;

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
            _selectedIndex = _texts.Length - 1;
        else if (_selectedIndex >= _texts.Length)
            _selectedIndex = 0;

        RefreshUI();
        CenterFocusItem(false);
    }

    public override void OnSubmit()
    {
        if (_waitingKey) return;

        InputRebindAction action = GetActionByIndex(_selectedIndex);
        string currentKey = SingletonManagers.Input.GetBindingDisplayName(action);

        _waitingKey = true;

        if (_helpText != null)
            _helpText.text = $"변경할 키를 누르세요. 기존 키 : {currentKey}";

        SetTextSafe(_selectedIndex, $"{GetLabel(_selectedIndex)}      < 입력 대기... >");

        if (_texts[_selectedIndex] != null)
            _texts[_selectedIndex].color = _waitingColor;

        SingletonManagers.Input.StartRebind(
            action,
            onComplete: _ =>
            {
                _waitingKey = false;
                RefreshUI();
                CenterFocusItem(false);
            },
            onDuplicate: _ =>
            {
                _waitingKey = false;
                StartDuplicateWarning();
            },
            onCanceled: _ =>
            {
                _waitingKey = false;
                RefreshUI();
                CenterFocusItem(false);
            }
        );
    }

    private InputRebindAction GetActionByIndex(int index)
    {
        switch (index)
        {
            case 0: return InputRebindAction.MoveLeft;
            case 1: return InputRebindAction.MoveRight;
            case 2: return InputRebindAction.MoveUp;
            case 3: return InputRebindAction.MoveDown;
            case 4: return InputRebindAction.Jump;
            case 5: return InputRebindAction.Interact;
            case 6: return InputRebindAction.Menu;
            case 7: return InputRebindAction.Talk;
        }

        return InputRebindAction.Jump;
    }

    private void RefreshUI()
    {
        SetTextSafe(0, $"좌 이동      < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.MoveLeft)} >");
        SetTextSafe(1, $"우 이동      < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.MoveRight)} >");
        SetTextSafe(2, $"위 이동      < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.MoveUp)} >");
        SetTextSafe(3, $"아래 이동    < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.MoveDown)} >");
        SetTextSafe(4, $"점프         < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Jump)} >");
        SetTextSafe(5, $"상호작용     < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Interact)} >");
        SetTextSafe(6, $"일시정지     < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Menu)} >");
        SetTextSafe(7, $"대화하기     < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Talk)} >");

        for (int i = 0; i < _texts.Length; i++)
        {
            if (_texts[i] == null)
                continue;

            bool selected = i == _selectedIndex;

            _texts[i].color = selected ? _selectedColor : _normalColor;
            _texts[i].fontSize = selected ? _selectedFontSize : _normalFontSize;
        }

        if (_helpText != null && !_waitingKey)
            _helpText.text = "□□ 이동 / Enter 키 변경 / ESC 뒤로";
    }

    private void CenterFocusItem(bool immediate)
    {
        if (_viewportRect == null || _contentRect == null)
            return;

        float viewportHeight = _viewportRect.rect.height;
        float contentHeight = _contentRect.rect.height;

        float pitch = _itemHeight + _itemSpacing;

        // 선택된 아이템의 Content 내부 기준 중심 위치
        float selectedCenterY =
            _verticalPadding +
            _selectedIndex * pitch +
            _itemHeight * 0.5f;

        // 선택된 아이템 중심이 Viewport 중앙에 오도록 Content Y 이동
        // _focusCenterOffsetY가 +면 아이템이 화면상 더 위로 올라감
        float targetY =
            selectedCenterY -
            viewportHeight * 0.5f +
            _focusCenterOffsetY;

        float maxY = Mathf.Max(0f, contentHeight - viewportHeight);
        targetY = Mathf.Clamp(targetY, 0f, maxY);

        _targetContentY = targetY;

        if (immediate)
        {
            Vector2 pos = _contentRect.anchoredPosition;
            pos.y = _targetContentY;
            _contentRect.anchoredPosition = pos;
        }
    }

    private void StartDuplicateWarning()
    {
        if (_duplicateRoutine != null)
            StopCoroutine(_duplicateRoutine);

        _duplicateRoutine = StartCoroutine(DuplicateWarningSequence());
    }

    private IEnumerator DuplicateWarningSequence()
    {
        TextMeshProUGUI target = _texts[_selectedIndex];

        if (_helpText != null)
            _helpText.text = "이미 사용 중인 키입니다.";

        if (target == null)
        {
            RefreshUI();
            yield break;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        Vector2 originPos = rect != null ? rect.anchoredPosition : Vector2.zero;

        string originalText = target.text;

        target.color = _duplicateColor;
        target.text = $"{GetLabel(_selectedIndex)}      < 중복 키! >";

        float timer = 0f;
        float duration = 2f;
        float shakePower = 5f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            if (rect != null)
            {
                float x = Mathf.Sin(Time.unscaledTime * 70f) * shakePower;
                rect.anchoredPosition = originPos + new Vector2(x, 0f);
            }

            yield return null;
        }

        if (rect != null)
            rect.anchoredPosition = originPos;

        target.text = originalText;

        RefreshUI();
        CenterFocusItem(false);
    }

    private string GetLabel(int index)
    {
        switch (index)
        {
            case 0: return "좌 이동";
            case 1: return "우 이동";
            case 2: return "위 이동";
            case 3: return "아래 이동";
            case 4: return "점프";
            case 5: return "상호작용";
            case 6: return "일시정지";
            case 7: return "대화하기";
        }

        return "";
    }

    private TextMeshProUGUI GetTMP(GameObjects obj)
    {
        GameObject go = Get<GameObject>((int)obj);

        if (go == null)
        {
            Debug.LogError($"[KeyConfig] {obj} 오브젝트를 찾지 못했습니다.");
            return null;
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();

        if (tmp == null)
            tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);

        if (tmp == null)
            Debug.LogError($"[KeyConfig] {obj} 안에서 TMP를 찾지 못했습니다.");

        return tmp;
    }

    private RectTransform GetRect(GameObjects obj)
    {
        GameObject go = Get<GameObject>((int)obj);

        if (go == null)
            return null;

        return go.GetComponent<RectTransform>();
    }

    private void SetTextSafe(int index, string text)
    {
        if (_texts == null) return;
        if (index < 0 || index >= _texts.Length) return;
        if (_texts[index] == null) return;

        _texts[index].text = text;
    }

    public override void OnCancel()
    {
        if (_waitingKey)
            return;

        ClosePopupUI();
    }
}
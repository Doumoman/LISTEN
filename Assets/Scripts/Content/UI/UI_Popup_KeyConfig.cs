using System.Collections;
using TMPro;
using UnityEngine;

public class UI_Popup_KeyConfig : UI_Popup
{
    enum GameObjects
    {
        Item_Direction,
        Item_Jump,
        Item_Interact,
        Item_Menu,
        Item_Talk,
        HelpText
    }

    private TextMeshProUGUI[] _texts;
    private TextMeshProUGUI _helpText;

    private int _selectedIndex = 0;
    private DirectionKeyScheme _directionScheme;

    private bool _waitingKey = false;
    private Coroutine _flashRoutine;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Color")]
    [SerializeField] private Color _normalColor = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.25f);
    [SerializeField] private Color _waitingColor = new Color(1f, 0.5f, 0.4f);
    [SerializeField] private Color _duplicateColor = Color.red;

    [Header("Font Size")]
    [SerializeField] private float _normalFontSize = 100f;
    [SerializeField] private float _selectedFontSize = 110f;

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        _texts = new TextMeshProUGUI[]
        {
            GetTMP(GameObjects.Item_Direction),
            GetTMP(GameObjects.Item_Jump),
            GetTMP(GameObjects.Item_Interact),
            GetTMP(GameObjects.Item_Menu),
            GetTMP(GameObjects.Item_Talk),
        };

        _helpText = GetTMP(GameObjects.HelpText);

        _directionScheme = SingletonManagers.Input.LoadDirectionKeyScheme();

        RefreshUI();
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
        else if (dir.x > 0.5f || dir.x < -0.5f)
        {
            ChangeDirectionScheme();
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
    }

    private void ChangeDirectionScheme()
    {
        if (_selectedIndex != 0)
            return;

        _directionScheme = _directionScheme == DirectionKeyScheme.Arrow
            ? DirectionKeyScheme.WASD
            : DirectionKeyScheme.Arrow;

        SingletonManagers.Input.SetDirectionKeyScheme(_directionScheme);

        RefreshUI();
    }

    public override void OnSubmit()
    {
        if (_waitingKey) return;

        if (_selectedIndex == 0)
        {
            ChangeDirectionScheme();
            return;
        }

        InputRebindAction action = GetRebindActionByIndex(_selectedIndex);
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
            },
            onDuplicate: _ =>
            {
                _waitingKey = false;
                StartDuplicateFlash();
            },
            onCanceled: _ =>
            {
                _waitingKey = false;
                RefreshUI();
            }
        );
    }

    private InputRebindAction GetRebindActionByIndex(int index)
    {
        switch (index)
        {
            case 1:
                return InputRebindAction.Jump;

            case 2:
                return InputRebindAction.Interact;

            case 3:
                return InputRebindAction.Menu;

            case 4:
                return InputRebindAction.Talk;
        }

        return InputRebindAction.Jump;
    }

    private void RefreshUI()
    {
        SetTextSafe(0, $"이동 방식      < {GetDirectionText()} >");
        SetTextSafe(1, $"점프          < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Jump)} >");
        SetTextSafe(2, $"상호작용      < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Interact)} >");
        SetTextSafe(3, $"일시정지      < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Menu)} >");
        SetTextSafe(4, $"대화하기      < {SingletonManagers.Input.GetBindingDisplayName(InputRebindAction.Talk)} >");

        for (int i = 0; i < _texts.Length; i++)
        {
            if (_texts[i] == null)
                continue;

            _texts[i].color = i == _selectedIndex ? _selectedColor : _normalColor;
            _texts[i].fontSize = i == _selectedIndex ? _selectedFontSize : _normalFontSize;
        }

        if (_helpText != null && !_waitingKey)
            _helpText.text = "↑↓ 이동 / ←→ 이동 방식 변경 / Enter 키 변경 / ESC 뒤로";
    }

    private void StartDuplicateFlash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(DuplicateFlashSequence());
    }

    private IEnumerator DuplicateFlashSequence()
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

        float duration = 1f;
        float timer = 0f;
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
    }

    private string GetDirectionText()
    {
        return _directionScheme == DirectionKeyScheme.Arrow
            ? "화살표"
            : "WASD";
    }

    private string GetLabel(int index)
    {
        switch (index)
        {
            case 0: return "이동 방식";
            case 1: return "점프";
            case 2: return "상호작용";
            case 3: return "일시정지";
            case 4: return "대화하기";
        }

        return "";
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
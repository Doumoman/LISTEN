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

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Popup In Animation")]
    [SerializeField] private float _appearDuration = 0.45f;
    [SerializeField] private float _startYOffset = -80f;

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

            t = 1f - Mathf.Pow(1f - t, 3f);

            _rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        _rect.anchoredPosition = endPos;
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

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

        UI_Popup_FileAction actionPopup = SingletonManagers.UI.ShowPopupUI<UI_Popup_FileAction>();
        actionPopup.SetFileSlot(_selectedIndex);
    }

    public override void OnCancel()
    {
        if (_isAnimating) return;

        ClosePopupUI();

        if (_owner != null)
            _owner.ShowMainMenu();
    }
}
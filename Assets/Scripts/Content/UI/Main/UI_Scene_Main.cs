using System.Collections;
using UnityEngine;

public class UI_Scene_Main : UI_Scene
{
    enum GameObjects
    {
        MainMenuGroup,
        MenuItem_Start,
        MenuItem_Option,
        MenuItem_Credit,
        MenuItem_Quit
    }
    public override bool KeepUIInputMode => true;
    private UI_FocusMenuItem[] _items;
    private int _selectedIndex = 0;

    private CanvasGroup _menuCanvasGroup;
    private RectTransform _menuRect;
    private Vector2 _menuOriginPos;

    private bool _isTransitioning = false;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Menu Out Animation")]
    [SerializeField] private float _shakeDuration = 0.25f;
    [SerializeField] private float _fadeDuration = 0.45f;
    [SerializeField] private float _downMoveDistance = 35f;

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        GameObject menuGroup = Get<GameObject>((int)GameObjects.MainMenuGroup);

        _menuCanvasGroup = menuGroup.GetComponent<CanvasGroup>();
        if (_menuCanvasGroup == null)
            _menuCanvasGroup = menuGroup.AddComponent<CanvasGroup>();

        _menuRect = menuGroup.GetComponent<RectTransform>();
        _menuOriginPos = _menuRect.anchoredPosition;

        _items = new UI_FocusMenuItem[]
        {
            Get<GameObject>((int)GameObjects.MenuItem_Start).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_Option).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_Credit).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_Quit).GetComponent<UI_FocusMenuItem>(),
        };

        SingletonManagers.Input.SetInputModeUI(true);

        SingletonManagers.Input.OnInput -= HandleInput;
        SingletonManagers.Input.OnInput += HandleInput;

        SingletonManagers.Input.OnUISubmitPressed -= HandleSubmit;
        SingletonManagers.Input.OnUISubmitPressed += HandleSubmit;

        SingletonManagers.Input.OnUICancelPressed -= HandleCancel;
        SingletonManagers.Input.OnUICancelPressed += HandleCancel;

        RefreshFocus();
    }

    private void HandleInput(Vector2 dir)
    {
        if (_isTransitioning) return;
        if (SingletonManagers.UI.PopupCount > 0) return;

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
            if (_items[i] != null)
                _items[i].SetSelected(i == _selectedIndex);
        }
    }

    private void HandleSubmit()
    {
        if (_isTransitioning) return;
        if (SingletonManagers.UI.PopupCount > 0) return;

        switch (_selectedIndex)
        {
            case 0:
                StartCoroutine(OpenFileSelectSequence());
                break;

            case 1:
                SingletonManagers.UI.ShowPopupUI<UI_Popup_Option>();
                break;

            case 2:
                SingletonManagers.UI.ShowPopupUI<UI_Popup_Credit>();
                break;

            case 3:
                Application.Quit();
                break;
        }
    }

    private IEnumerator OpenFileSelectSequence()
    {
        _isTransitioning = true;

        yield return StartCoroutine(_items[_selectedIndex].PlayShake(_shakeDuration));

        yield return StartCoroutine(FadeOutMainMenu());

        UI_Popup_FileSelect popup = SingletonManagers.UI.ShowPopupUI<UI_Popup_FileSelect>();
        popup.SetOwner(this);

        _isTransitioning = false;
    }

    private IEnumerator FadeOutMainMenu()
    {
        float timer = 0f;

        Vector2 startPos = _menuOriginPos;
        Vector2 endPos = _menuOriginPos + Vector2.down * _downMoveDistance;

        _menuCanvasGroup.alpha = 1f;
        _menuCanvasGroup.interactable = false;
        _menuCanvasGroup.blocksRaycasts = false;

        while (timer < _fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _fadeDuration);

            _menuRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            _menuCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        _menuRect.anchoredPosition = endPos;
        _menuCanvasGroup.alpha = 0f;

        Get<GameObject>((int)GameObjects.MainMenuGroup).SetActive(false);
    }

    public void ShowMainMenu()
    {
        GameObject menuGroup = Get<GameObject>((int)GameObjects.MainMenuGroup);
        menuGroup.SetActive(true);

        _menuRect.anchoredPosition = _menuOriginPos;
        _menuCanvasGroup.alpha = 1f;
        _menuCanvasGroup.interactable = true;
        _menuCanvasGroup.blocksRaycasts = true;

        RefreshFocus();
    }

    private void HandleCancel()
    {
    }

    private void OnDestroy()
    {
        if (SingletonManagers.Input == null)
            return;

        SingletonManagers.Input.OnInput -= HandleInput;
        SingletonManagers.Input.OnUISubmitPressed -= HandleSubmit;
        SingletonManagers.Input.OnUICancelPressed -= HandleCancel;
    }
}
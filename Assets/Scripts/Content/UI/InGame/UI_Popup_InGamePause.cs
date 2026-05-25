using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_Popup_InGamePause : UI_Popup
{
    enum GameObjects
    {
        MenuItem_Resume,
        MenuItem_Retry,
        MenuItem_Option,
        MenuItem_SaveQuit,
        MenuItem_ReturnMap
    }

    private UI_FocusMenuItem[] _items;
    private int _selectedIndex = 0;

    private CanvasGroup _canvasGroup;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Scene Names")]
    [SerializeField] private string _mainSceneName = "Main";

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _items = new UI_FocusMenuItem[]
        {
            Get<GameObject>((int)GameObjects.MenuItem_Resume).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_Retry).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_Option).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_SaveQuit).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_ReturnMap).GetComponent<UI_FocusMenuItem>(),
        };

        _items[0].SetText("계속하기");
        _items[1].SetText("다시하기");
        _items[2].SetText("인게임 옵션");
        _items[3].SetText("저장하고 나가기");
        _items[4].SetText("등대로 돌아가기");

        Time.timeScale = 0f;
        SingletonManagers.Input.SetInputModeUI(true);

        ShowPausePanel();
        RefreshFocus();
    }

    public void HidePausePanel()
    {
        if (_canvasGroup == null) return;

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    public void ShowPausePanel()
    {
        if (_canvasGroup == null) return;

        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        RefreshFocus();
    }

    public override void OnInput(Vector2 dir)
    {
        if (_canvasGroup != null && _canvasGroup.alpha <= 0.01f)
            return;

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

    public override void OnSubmit()
    {
        if (_canvasGroup != null && _canvasGroup.alpha <= 0.01f)
            return;

        switch (_selectedIndex)
        {
            case 0:
                ResumeGame();
                break;

            case 1:
                RetryStage();
                break;

            case 2:
                OpenInGameOption();
                break;

            case 3:
                OpenSaveQuitConfirm();
                break;

            case 4:
                OpenReturnMapConfirm();
                break;
        }
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;

        ClosePopupUI();

        if (SingletonManagers.UI.PopupCount == 0)
            SingletonManagers.Input.SetInputModeUI(false);
    }

    private void RetryStage()
    {
        Time.timeScale = 1f;

        SingletonManagers.UI.CloseAllPopupUI();
        SingletonManagers.Input.SetInputModeUI(false);

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    private void OpenInGameOption()
    {
        HidePausePanel();

        UI_Popup_InGameOption option = SingletonManagers.UI.ShowPopupUI<UI_Popup_InGameOption>();

        if (option != null)
            option.SetOwner(this);
    }

    private void OpenSaveQuitConfirm()
    {
        HidePausePanel();

        UI_Popup_Confirm confirm = SingletonManagers.UI.ShowPopupUI<UI_Popup_Confirm>();

        if (confirm == null)
        {
            ShowPausePanel();
            return;
        }

        confirm.SetOwner(this);

        confirm.SetMessage(
            "해당 맵 위치를 기억하고 메인메뉴로 이동합니다.",
            onConfirm: SaveAndQuitToMain,
            onCancel: ShowPausePanel
        );
    }

    private void OpenReturnMapConfirm()
    {
        HidePausePanel();

        UI_Popup_Confirm confirm = SingletonManagers.UI.ShowPopupUI<UI_Popup_Confirm>();

        if (confirm == null)
        {
            ShowPausePanel();
            return;
        }

        confirm.SetOwner(this);

        confirm.SetMessage(
            "해당 챕터에서의 여정이 초기화됩니다.\n정말로 등대로 돌아가실건가요?",
            onConfirm: ReturnToStageSelectOnMain,
            onCancel: ShowPausePanel
        );
    }

    private void SaveAndQuitToMain()
    {
        Debug.Log("[Pause] 저장하고 메인메뉴로 이동");

        Time.timeScale = 1f;

        SingletonManagers.UI.CloseAllPopupUI();
        SingletonManagers.Input.SetInputModeUI(true);

        PlayerPrefs.SetInt("OpenStageSelectOnMain", 0);
        SceneManager.LoadScene(_mainSceneName);
    }

    private void ReturnToStageSelectOnMain()
    {
        Debug.Log("[Pause] 등대로 돌아가기: 메인화면 + 스테이지 선택창");

        Time.timeScale = 1f;

        SingletonManagers.UI.CloseAllPopupUI();
        SingletonManagers.Input.SetInputModeUI(true);

        PlayerPrefs.SetInt("OpenStageSelectOnMain", 1);
        SceneManager.LoadScene(_mainSceneName);
    }

    public override void OnCancel()
    {
        ResumeGame();
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    public void SetFileSlot(int index)
    {
        _fileSlotIndex = index;
    }

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        _items = new UI_FocusMenuItem[]
        {
            Get<GameObject>((int)GameObjects.Action_StartGame).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.Action_Rename).GetComponent<UI_FocusMenuItem>(),
        };

        _items[0].SetText("게임 시작");
        _items[1].SetText("이름 바꾸기");

        RefreshFocus();
    }

    public override void OnInput(Vector2 dir)
    {
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
        switch (_selectedIndex)
        {
            case 0:
                StartGame();
                break;

            case 1:
                RenameFile();
                break;
        }
    }

    private void StartGame()
    {
        Debug.Log($"파일 {_fileSlotIndex + 1}번으로 게임 시작");

        SingletonManagers.Input.SetInputModeUI(false);
        SingletonManagers.UI.CloseAllPopupUI();

        SceneManager.LoadScene("Stage1");
    }

    private void RenameFile()
    {
        Debug.Log($"파일 {_fileSlotIndex + 1}번 이름 바꾸기");

        // 나중에 이름 입력 팝업을 따로 만들면 됨
        // SingletonManagers.UI.ShowPopupUI<UI_Popup_RenameFile>();
    }

    public override void OnCancel()
    {
        ClosePopupUI();
    }
}
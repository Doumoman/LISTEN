using System;
using TMPro;
using UnityEngine;

public class UI_Popup_Confirm : UI_Popup
{
    enum GameObjects
    {
        MenuItem_Confirm,
        MenuItem_Cancel
    }

    enum Texts
    {
        MessageText
    }

    private UI_FocusMenuItem[] _items;
    private TextMeshProUGUI _messageText;

    private int _selectedIndex = 1;

    private string _message;
    private Action _onConfirm;
    private Action _onCancel;

    private UI_Popup_InGamePause _ownerPause;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    public void SetOwner(UI_Popup_InGamePause owner)
    {
        _ownerPause = owner;
    }

    public void SetMessage(string message, Action onConfirm, Action onCancel = null)
    {
        _message = message;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        ApplyMessage();
    }

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));
        Bind<TextMeshProUGUI>(typeof(Texts));

        _messageText = GetText((int)Texts.MessageText);

        _items = new UI_FocusMenuItem[]
        {
            Get<GameObject>((int)GameObjects.MenuItem_Confirm).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.MenuItem_Cancel).GetComponent<UI_FocusMenuItem>()
        };

        _items[0].SetText("확인");
        _items[1].SetText("취소");

        ApplyMessage();
        RefreshFocus();
    }

    private void ApplyMessage()
    {
        if (_messageText == null)
            return;

        _messageText.text = string.IsNullOrEmpty(_message)
            ? "정말로 진행할까요?"
            : _message;
    }

    public override void OnInput(Vector2 dir)
    {
        if (Time.unscaledTime - _lastMoveTime < _moveCooldown)
            return;

        if (dir.x > 0.5f || dir.y < -0.5f)
        {
            MoveFocus(1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.x < -0.5f || dir.y > 0.5f)
        {
            MoveFocus(-1);
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
        if (_items == null) return;

        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] != null)
                _items[i].SetSelected(i == _selectedIndex);
        }
    }

    public override void OnSubmit()
    {
        if (_selectedIndex == 0)
        {
            Action confirmAction = _onConfirm;

            ClosePopupUI();

            confirmAction?.Invoke();
        }
        else
        {
            Cancel();
        }
    }

    public override void OnCancel()
    {
        Cancel();
    }

    private void Cancel()
    {
        Action cancelAction = _onCancel;

        ClosePopupUI();

        cancelAction?.Invoke();

        if (_ownerPause != null)
            _ownerPause.ShowPausePanel();
    }
}
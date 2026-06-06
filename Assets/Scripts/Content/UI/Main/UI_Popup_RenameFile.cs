using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class UI_Popup_RenameFile : UI_Popup
{
    enum GameObjects
    {
        KeyboardRoot,
        Button_Cancel,
        Button_Delete,
        Button_Confirm
    }

    enum Texts
    {
        QuestionText,
        NameText,
        HelpText
    }

    private enum RenameAction
    {
        Letter,
        Cancel,
        Delete,
        Confirm
    }

    private class RenameEntry
    {
        public UI_FocusMenuItem Item;
        public RenameAction Action;
        public char Letter;
        public int Row;
        public int Col;
    }

    private readonly List<RenameEntry> _entries = new List<RenameEntry>();
    private readonly StringBuilder _nameBuilder = new StringBuilder();

    private UI_Popup_FileSelect _ownerFileSelect;
    private UI_Popup_FileAction _ownerFileAction;
    private int _fileSlotIndex = -1;
    private string _originalName = "";

    private int _selectedIndex = 0;
    private bool _isConfirmOpen = false;

    private float _lastMoveTime;

    [Header("Name")]
    [SerializeField] private int _maxNameLength = 6;
    [SerializeField] private int _letterColumns = 7;

    [Header("Input")]
    [SerializeField] private float _moveCooldown = 0.12f;

    public void Setup(UI_Popup_FileSelect fileSelect, UI_Popup_FileAction fileAction, int fileSlotIndex, string currentName)
    {
        _ownerFileSelect = fileSelect;
        _ownerFileAction = fileAction;
        _fileSlotIndex = fileSlotIndex;

        _nameBuilder.Length = 0;
        _originalName = "";

        if (!string.IsNullOrWhiteSpace(currentName))
        {
            string filtered = FilterEnglishLetters(currentName);
            int count = Mathf.Min(filtered.Length, _maxNameLength);

            for (int i = 0; i < count; i++)
                _nameBuilder.Append(filtered[i]);

            _originalName = _nameBuilder.ToString();
        }

        RefreshNameText();
    }

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));
        Bind<TextMeshProUGUI>(typeof(Texts));

        GetText((int)Texts.QuestionText).text = "파일 이름";
        GetText((int)Texts.HelpText).text = "방향키 이동 / Enter 선택 / ESC 취소";

        BuildEntries();
        RefreshNameText();
        RefreshFocus();
    }

    private void BuildEntries()
    {
        _entries.Clear();

        Transform keyboardRoot = Get<GameObject>((int)GameObjects.KeyboardRoot).transform;

        for (int i = 0; i < keyboardRoot.childCount; i++)
        {
            Transform child = keyboardRoot.GetChild(i);
            UI_FocusMenuItem item = child.GetComponent<UI_FocusMenuItem>();

            if (item == null || string.IsNullOrEmpty(child.name))
                continue;

            char letter = child.name[child.name.Length - 1];

            _entries.Add(new RenameEntry
            {
                Item = item,
                Action = RenameAction.Letter,
                Letter = letter,
                Row = i / _letterColumns,
                Col = i % _letterColumns
            });
        }

        int controlRow = Mathf.CeilToInt(_entries.Count / (float)_letterColumns);

        AddControlEntry(GameObjects.Button_Cancel, RenameAction.Cancel, controlRow, 0, "취소");
        AddControlEntry(GameObjects.Button_Delete, RenameAction.Delete, controlRow, 3, "지우기");
        AddControlEntry(GameObjects.Button_Confirm, RenameAction.Confirm, controlRow, 6, "확인");
    }

    private void AddControlEntry(GameObjects obj, RenameAction action, int row, int col, string label)
    {
        GameObject go = Get<GameObject>((int)obj);
        UI_FocusMenuItem item = go != null ? go.GetComponent<UI_FocusMenuItem>() : null;

        if (item == null)
            return;

        item.SetText(label);

        _entries.Add(new RenameEntry
        {
            Item = item,
            Action = action,
            Row = row,
            Col = col
        });
    }

    public override void OnInput(Vector2 dir)
    {
        if (_isConfirmOpen) return;
        if (_entries.Count == 0) return;
        if (Time.unscaledTime - _lastMoveTime < _moveCooldown) return;

        if (dir.x > 0.5f)
        {
            MoveHorizontal(1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.x < -0.5f)
        {
            MoveHorizontal(-1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.y > 0.5f)
        {
            MoveVertical(-1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.y < -0.5f)
        {
            MoveVertical(1);
            _lastMoveTime = Time.unscaledTime;
        }
    }

    private void MoveHorizontal(int delta)
    {
        RenameEntry current = _entries[_selectedIndex];
        List<int> sameRow = GetRowIndices(current.Row);

        if (sameRow.Count == 0)
            return;

        int rowPosition = sameRow.IndexOf(_selectedIndex);
        rowPosition += delta;

        if (rowPosition < 0)
            rowPosition = sameRow.Count - 1;
        else if (rowPosition >= sameRow.Count)
            rowPosition = 0;

        _selectedIndex = sameRow[rowPosition];
        RefreshFocus();
    }

    private void MoveVertical(int delta)
    {
        RenameEntry current = _entries[_selectedIndex];
        int targetRow = current.Row + delta;

        if (targetRow < 0)
            targetRow = GetMaxRow();
        else if (targetRow > GetMaxRow())
            targetRow = 0;

        _selectedIndex = FindNearestIndexInRow(targetRow, current.Col);
        RefreshFocus();
    }

    private List<int> GetRowIndices(int row)
    {
        List<int> indices = new List<int>();

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Row == row)
                indices.Add(i);
        }

        indices.Sort((a, b) => _entries[a].Col.CompareTo(_entries[b].Col));
        return indices;
    }

    private int FindNearestIndexInRow(int row, int col)
    {
        int bestIndex = _selectedIndex;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Row != row)
                continue;

            int distance = Mathf.Abs(_entries[i].Col - col);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int GetMaxRow()
    {
        int maxRow = 0;

        for (int i = 0; i < _entries.Count; i++)
            maxRow = Mathf.Max(maxRow, _entries[i].Row);

        return maxRow;
    }

    public override void OnSubmit()
    {
        if (_isConfirmOpen) return;
        if (_entries.Count == 0) return;

        RenameEntry entry = _entries[_selectedIndex];

        switch (entry.Action)
        {
            case RenameAction.Letter:
                AppendLetter(entry.Letter);
                break;

            case RenameAction.Delete:
                DeleteLastLetter();
                break;

            case RenameAction.Cancel:
                ClosePopupUI();
                break;

            case RenameAction.Confirm:
                RequestConfirmRename();
                break;
        }
    }

    public override void OnCancel()
    {
        if (_isConfirmOpen) return;

        ClosePopupUI();
    }

    private void AppendLetter(char letter)
    {
        if (_nameBuilder.Length >= _maxNameLength)
            return;

        _nameBuilder.Append(letter);
        RefreshNameText();
    }

    private void DeleteLastLetter()
    {
        if (_nameBuilder.Length == 0)
            return;

        _nameBuilder.Length -= 1;
        RefreshNameText();
    }

    private void RequestConfirmRename()
    {
        string finalName = _nameBuilder.ToString();

        if (string.IsNullOrWhiteSpace(finalName))
        {
            GetText((int)Texts.HelpText).text = "이름은 한 글자 이상이어야 합니다.";
            return;
        }

        if (IsReservedSpecialName(finalName))
        {
            ShowSpecialNameMessage();
            return;
        }

        if (IsSameAsOriginalName(finalName))
        {
            ClosePopupUI();

            if (_ownerFileAction != null)
                _ownerFileAction.CloseAfterRename();

            return;
        }

        _isConfirmOpen = true;

        UI_Popup_Confirm confirm = SingletonManagers.UI.ShowPopupUI<UI_Popup_Confirm>();

        if (confirm == null)
        {
            _isConfirmOpen = false;
            return;
        }

        confirm.SetMessage(
            "이름을 바꾸시겠습니까?",
            onConfirm: () =>
            {
                _isConfirmOpen = false;
                ApplyRename(finalName);
            },
            onCancel: () =>
            {
                _isConfirmOpen = false;
                RefreshFocus();
            }
        );
    }

    private void ApplyRename(string finalName)
    {
        if (_ownerFileSelect != null)
            _ownerFileSelect.RenameSlot(_fileSlotIndex, finalName);

        ClosePopupUI();

        if (_ownerFileAction != null)
            _ownerFileAction.CloseAfterRename();
    }

    private bool IsReservedSpecialName(string fileName)
    {
        return string.Equals(fileName, "listen", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSameAsOriginalName(string fileName)
    {
        return string.Equals(fileName, _originalName, StringComparison.Ordinal);
    }

    private void ShowSpecialNameMessage()
    {
        UI_Popup_SpecialNameMessage popup = SingletonManagers.UI.ShowPopupUI<UI_Popup_SpecialNameMessage>();

        if (popup == null)
        {
            Debug.Log("[RenameFile] 듣고 있단다, 아가야");
            return;
        }

        popup.Setup("듣고 있단다, 아가야");
    }

    private void RefreshNameText()
    {
        TextMeshProUGUI nameText = GetText((int)Texts.NameText);

        if (nameText == null)
            return;

        string display = "";

        for (int i = 0; i < _maxNameLength; i++)
        {
            display += i < _nameBuilder.Length ? _nameBuilder[i].ToString() : "_";

            if (i < _maxNameLength - 1)
                display += " ";
        }

        nameText.text = display;
    }

    private void RefreshFocus()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Item != null)
                _entries[i].Item.SetSelected(i == _selectedIndex);
        }
    }

    private string FilterEnglishLetters(string value)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                builder.Append(c);
        }

        return builder.ToString();
    }
}

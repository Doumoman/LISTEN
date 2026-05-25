using TMPro;
using UnityEngine;

public class UI_Popup_InGameOption : UI_Popup
{
    enum GameObjects
    {
        Panel,
        OptionItem_Bgm,
        OptionItem_Sfx,
        OptionItem_Resolution,
        HelpText
    }

    private TextMeshProUGUI[] _optionTexts;
    private TextMeshProUGUI _helpText;

    private int _selectedIndex = 0;

    private int _bgmVolume = 6;
    private int _sfxVolume = 4;

    private int _resolutionIndex = 1;
    private readonly string[] _resolutions =
    {
        "1280 x 720",
        "1920 x 1080",
        "2560 x 1440"
    };

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Color")]
    [SerializeField] private Color _normalColor = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.9f, 0.25f);

    [Header("Font Size")]
    [SerializeField] private float _normalFontSize = 34f;
    [SerializeField] private float _selectedFontSize = 38f;

    private UI_Popup_InGamePause _owner;

    public void SetOwner(UI_Popup_InGamePause owner)
    {
        _owner = owner;
    }
    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));

        _optionTexts = new TextMeshProUGUI[]
        {
            GetTMP(GameObjects.OptionItem_Bgm),
            GetTMP(GameObjects.OptionItem_Sfx),
            GetTMP(GameObjects.OptionItem_Resolution)
        };

        _helpText = GetTMP(GameObjects.HelpText);

        if (_helpText != null)
            _helpText.text = "↑↓ 이동 / ←→ 변경 / ESC 뒤로";

        RefreshUI();
    }

    private TextMeshProUGUI GetTMP(GameObjects obj)
    {
        GameObject go = Get<GameObject>((int)obj);

        if (go == null)
        {
            Debug.LogError($"[InGameOption] {obj} 오브젝트를 찾지 못했습니다.");
            return null;
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();

        if (tmp == null)
            tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);

        if (tmp == null)
            Debug.LogError($"[InGameOption] {obj} 안에서 TextMeshProUGUI를 찾지 못했습니다.");

        return tmp;
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
        else if (dir.x > 0.5f)
        {
            ChangeValue(1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.x < -0.5f)
        {
            ChangeValue(-1);
            _lastMoveTime = Time.unscaledTime;
        }
    }

    private void MoveFocus(int delta)
    {
        _selectedIndex += delta;

        if (_selectedIndex < 0)
            _selectedIndex = _optionTexts.Length - 1;
        else if (_selectedIndex >= _optionTexts.Length)
            _selectedIndex = 0;

        RefreshUI();
    }

    private void ChangeValue(int delta)
    {
        switch (_selectedIndex)
        {
            case 0:
                _bgmVolume = Mathf.Clamp(_bgmVolume + delta, 0, 10);
                break;

            case 1:
                _sfxVolume = Mathf.Clamp(_sfxVolume + delta, 0, 10);
                break;

            case 2:
                _resolutionIndex += delta;

                if (_resolutionIndex < 0)
                    _resolutionIndex = _resolutions.Length - 1;
                else if (_resolutionIndex >= _resolutions.Length)
                    _resolutionIndex = 0;
                break;
        }

        ApplyTemporarySettings();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_optionTexts == null || _optionTexts.Length < 3)
            return;

        SetTextSafe(0, $"배경음      < {_bgmVolume} >");
        SetTextSafe(1, $"효과음      < {_sfxVolume} >");
        SetTextSafe(2, $"해상도      < {_resolutions[_resolutionIndex]} >");

        for (int i = 0; i < _optionTexts.Length; i++)
        {
            if (_optionTexts[i] == null)
                continue;

            bool selected = i == _selectedIndex;

            _optionTexts[i].color = selected ? _selectedColor : _normalColor;
            _optionTexts[i].fontSize = selected ? _selectedFontSize : _normalFontSize;
        }
    }

    private void SetTextSafe(int index, string text)
    {
        if (_optionTexts[index] != null)
            _optionTexts[index].text = text;
    }

    private void ApplyTemporarySettings()
    {
        // 아직 SoundManager가 없으므로 임시 값만 유지.
        // 나중에 SoundManager 연결:
        // SingletonManagers.Sound.SetBgmVolume(_bgmVolume / 10f);
        // SingletonManagers.Sound.SetSfxVolume(_sfxVolume / 10f);

        Debug.Log($"[InGameOption] BGM:{_bgmVolume}, SFX:{_sfxVolume}, Resolution:{_resolutions[_resolutionIndex]}");
    }

    public override void OnSubmit()
    {
        // 인게임 옵션에서는 Enter로 닫지 않음.
    }

    public override void OnCancel()
    {
        ClosePopupUI();

        if (_owner != null)
            _owner.ShowPausePanel();
    }
}
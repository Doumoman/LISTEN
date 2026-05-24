using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UI_Popup_StageSelect : UI_Popup
{
    enum GameObjects
    {
        FlipRoot,
        DetailRoot,

        StageItem_0,
        StageItem_1,
        StageItem_2,
        StageItem_3,
        StageItem_4,
        StageItem_5,
        StageItem_6
    }

    enum Texts
    {
        DetailTitleText,
        DetailDescText,
        DetailRewardText,
        DetailHelpText
    }

    private UI_Popup_FileSelect _returnFileSelect;
    private UI_FocusMenuItem[] _stageItems;
    private int _selectedIndex = 0;

    private CanvasGroup _canvasGroup;
    private RectTransform _flipRoot;
    private Vector2 _flipOriginPos;

    private RectTransform _detailRoot;
    private CanvasGroup _detailCanvasGroup;
    private Vector2 _detailOriginPos;
    private Vector2 _detailHiddenPos;

    private bool _isAnimating = false;
    private bool _isDetailOpen = false;

    private float _lastMoveTime;
    [SerializeField] private float _moveCooldown = 0.15f;

    [Header("Stage Select Appear")]
    [SerializeField] private float _appearDuration = 0.6f;
    [SerializeField] private float _startYOffset = -500f;
    [SerializeField] private float _startRotationZ = 180f;

    [Header("Detail Slide")]
    [SerializeField] private float _detailSlideDuration = 0.35f;
    [SerializeField] private float _detailHiddenXOffset = 900f;

    [Header("Stage Data")]
    [SerializeField]
    private string[] _stageSceneNames =
    {
        "Stage1",
        "Stage2",
        "Stage3",
        "Stage4",
        "Stage5",
        "Stage6",
        "Stage7"
    };

    [SerializeField]
    private string[] _stageTitles =
    {
        "Chapter 1\nFirst Snow",
        "Chapter 2\nOld Site",
        "Chapter 3\nCrystal Road",
        "Chapter 4\nSilent Lake",
        "Chapter 5\nPulse Rail",
        "Chapter 6\nTwilight Sea",
        "Chapter 7\nLast Lighthouse"
    };

    [SerializeField]
    private string[] _stageDescriptions =
    {
        "첫 번째 여정입니다.\n기본 조작과 세계의 규칙을 익힙니다.",
        "오래된 유적지입니다.\n움직이는 발판과 낙하 구조가 등장합니다.",
        "결정으로 뒤덮인 길입니다.\n미끄러운 지형과 반사 기믹을 활용합니다.",
        "소리가 얼어붙은 호수입니다.\n침묵과 타이밍을 중심으로 진행됩니다.",
        "펄스 레일이 흐르는 구간입니다.\n빠른 이동과 방향 전환이 핵심입니다.",
        "황혼의 바다입니다.\n기억과 목소리를 따라 깊은 곳으로 향합니다.",
        "마지막 등대입니다.\n지금까지의 선택이 하나로 모입니다."
    };

    [SerializeField]
    private string[] _stageRewards =
    {
        "보상: 기본 기록 해금",
        "보상: 오래된 지도 조각",
        "보상: 결정 조각",
        "보상: 침묵의 기록",
        "보상: 펄스 장치",
        "보상: 바다의 편지",
        "보상: 마지막 빛"
    };

    public override void Init()
    {
        base.Init();

        Bind<GameObject>(typeof(GameObjects));
        Bind<TextMeshProUGUI>(typeof(Texts));

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _flipRoot = Get<GameObject>((int)GameObjects.FlipRoot).GetComponent<RectTransform>();
        _flipOriginPos = _flipRoot.anchoredPosition;

        _detailRoot = Get<GameObject>((int)GameObjects.DetailRoot).GetComponent<RectTransform>();
        _detailCanvasGroup = _detailRoot.GetComponent<CanvasGroup>();
        if (_detailCanvasGroup == null)
            _detailCanvasGroup = _detailRoot.gameObject.AddComponent<CanvasGroup>();

        _detailOriginPos = _detailRoot.anchoredPosition;
        _detailHiddenPos = _detailOriginPos + Vector2.right * _detailHiddenXOffset;

        _stageItems = new UI_FocusMenuItem[]
        {
            Get<GameObject>((int)GameObjects.StageItem_0).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.StageItem_1).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.StageItem_2).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.StageItem_3).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.StageItem_4).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.StageItem_5).GetComponent<UI_FocusMenuItem>(),
            Get<GameObject>((int)GameObjects.StageItem_6).GetComponent<UI_FocusMenuItem>(),
        };

        for (int i = 0; i < _stageItems.Length; i++)
        {
            _stageItems[i].SetText($"Stage {i + 1}");
        }

        HideDetailImmediate();
        RefreshFocus();

        StartCoroutine(AppearSequence());
    }

    private IEnumerator AppearSequence()
    {
        _isAnimating = true;

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Vector2 startPos = _flipOriginPos + Vector2.down * Mathf.Abs(_startYOffset);
        Vector2 endPos = _flipOriginPos;

        _flipRoot.anchoredPosition = startPos;
        _flipRoot.localRotation = Quaternion.Euler(0f, 0f, _startRotationZ);

        float timer = 0f;

        while (timer < _appearDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _appearDuration);
            float eased = EaseOutCubic(t);

            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);
            _flipRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);

            float z = Mathf.Lerp(_startRotationZ, 0f, eased);
            _flipRoot.localRotation = Quaternion.Euler(0f, 0f, z);

            yield return null;
        }

        _canvasGroup.alpha = 1f;
        _flipRoot.anchoredPosition = endPos;
        _flipRoot.localRotation = Quaternion.identity;

        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        _isAnimating = false;
    }

    public override void OnInput(Vector2 dir)
    {
        if (_isAnimating) return;

        // 설명창이 열려 있을 때는 스테이지 이동 금지
        if (_isDetailOpen) return;

        if (Time.unscaledTime - _lastMoveTime < _moveCooldown)
            return;

        if (dir.x > 0.5f)
        {
            MoveFocus(1);
            _lastMoveTime = Time.unscaledTime;
        }
        else if (dir.x < -0.5f)
        {
            MoveFocus(-1);
            _lastMoveTime = Time.unscaledTime;
        }
    }

    private void MoveFocus(int delta)
    {
        _selectedIndex += delta;

        if (_selectedIndex < 0)
            _selectedIndex = _stageItems.Length - 1;
        else if (_selectedIndex >= _stageItems.Length)
            _selectedIndex = 0;

        RefreshFocus();

        if (_isDetailOpen)
            RefreshDetailText();
    }

    private void RefreshFocus()
    {
        for (int i = 0; i < _stageItems.Length; i++)
        {
            if (_stageItems[i] != null)
                _stageItems[i].SetSelected(i == _selectedIndex);
        }
    }

    public override void OnSubmit()
    {
        if (_isAnimating) return;

        if (_isDetailOpen)
        {
            StartSelectedStage();
        }
        else
        {
            StartCoroutine(OpenDetailSequence());
        }
    }

    private IEnumerator OpenDetailSequence()
    {
        _isAnimating = true;
        _isDetailOpen = true;

        RefreshDetailText();

        _detailRoot.gameObject.SetActive(true);
        _detailRoot.anchoredPosition = _detailHiddenPos;
        _detailCanvasGroup.alpha = 0f;

        float timer = 0f;

        while (timer < _detailSlideDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _detailSlideDuration);
            float eased = EaseOutCubic(t);

            _detailRoot.anchoredPosition = Vector2.Lerp(_detailHiddenPos, _detailOriginPos, eased);
            _detailCanvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);

            yield return null;
        }

        _detailRoot.anchoredPosition = _detailOriginPos;
        _detailCanvasGroup.alpha = 1f;

        _isAnimating = false;
    }

    private IEnumerator CloseDetailSequence()
    {
        _isAnimating = true;

        Vector2 startPos = _detailRoot.anchoredPosition;
        Vector2 endPos = _detailHiddenPos;

        float timer = 0f;

        while (timer < _detailSlideDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _detailSlideDuration);
            float eased = EaseOutCubic(t);

            _detailRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
            _detailCanvasGroup.alpha = Mathf.Lerp(1f, 0f, eased);

            yield return null;
        }

        HideDetailImmediate();

        _isDetailOpen = false;
        _isAnimating = false;
    }

    private void HideDetailImmediate()
    {
        if (_detailRoot == null) return;

        _detailRoot.anchoredPosition = _detailHiddenPos;
        _detailCanvasGroup.alpha = 0f;
        _detailRoot.gameObject.SetActive(false);
    }

    private void RefreshDetailText()
    {
        GetText((int)Texts.DetailTitleText).text = SafeGet(_stageTitles, _selectedIndex, $"Stage {_selectedIndex + 1}");
        GetText((int)Texts.DetailDescText).text = SafeGet(_stageDescriptions, _selectedIndex, "스테이지 설명이 없습니다.");
        GetText((int)Texts.DetailRewardText).text = SafeGet(_stageRewards, _selectedIndex, "");
        GetText((int)Texts.DetailHelpText).text = "Enter: 선택   /   Esc: 뒤로   /   ← →: 스테이지 변경";
    }

    private void StartSelectedStage()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _stageSceneNames.Length)
        {
            Debug.LogError($"[StageSelect] Invalid stage index: {_selectedIndex}");
            return;
        }

        string sceneName = _stageSceneNames[_selectedIndex];

        SingletonManagers.Input.SetInputModeUI(false);
        SingletonManagers.UI.CloseAllPopupUI();

        SceneManager.LoadScene(sceneName);
    }
    public void SetReturnFileSelect(UI_Popup_FileSelect fileSelect)
    {
        _returnFileSelect = fileSelect;
    }

    public override void OnCancel()
    {
        if (_isAnimating) return;

        if (_isDetailOpen)
        {
            StartCoroutine(CloseDetailSequence());
        }
        else
        {
            StartCoroutine(CloseStageSelectSequence());
        }
    }
    private IEnumerator CloseStageSelectSequence()
    {
        _isAnimating = true;

        // 여기서 StageSelect 자체 닫힘 애니메이션이 있으면 먼저 실행하면 됨.
        // 아직 없다면 바로 닫아도 됨.
        ClosePopupUI();

        if (_returnFileSelect != null)
        {
            _returnFileSelect.ShowAgainFromStageSelect();
        }

        _isAnimating = false;

        yield return null;
    }

    private string SafeGet(string[] array, int index, string fallback)
    {
        if (array == null) return fallback;
        if (index < 0 || index >= array.Length) return fallback;
        if (string.IsNullOrEmpty(array[index])) return fallback;
        return array[index];
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
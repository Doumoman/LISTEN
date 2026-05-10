using UnityEngine;

public class SingletonManagers : Singleton<SingletonManagers>
{
    #region Core
    // 기반 관련
    private ResourceManager _resource = new ResourceManager();
    private UIManager _ui = new UIManager();
    private InputManager _input = new InputManager();
    private DataManager _data = new DataManager();
    #endregion

    #region Contents
    // 게임 로직 관련
    private StoryManager _story = new StoryManager();
    #endregion

    public static ResourceManager Resource { get { return Instance?._resource; } }
    public static UIManager UI { get { return Instance?._ui; } }
    public static InputManager Input { get { return Instance?._input; } }
    public static DataManager Data { get { return Instance?._data; } }
    public static StoryManager Story { get { return Instance?._story; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeSceneLoad()
    {
        var instance = SingletonManagers.Instance;
        Debug.Log("[Managers] System Initialized Before Scene Load.");
    }

    protected override void Awake()
    {
        base.Awake();
        Init(); // 가장 먼저 다른 Manager들 초기화
    }
    private void Update()
    {

    }

    private bool _init = false;
    public void Init()
    {
        if (_init) return;
        _init = true;

        // 매니저 인스턴스 생성 및 초기화 순서 제어
        _data.Init();
        _resource.Init();
        _input.Init();
        _ui.Init();
        _story.Init();

        // UI 이벤트 리스너가 사라지면 
        if (gameObject.GetComponent<UI_EventListener>() == null)
        {
            GameObject evt = new GameObject("@UI_EventListener");
            evt.AddComponent<UI_EventListener>();
            evt.transform.SetParent(gameObject.transform, false);
        }
    }

    public static void Clear()
    {
        var inst = Instance;
        if (inst == null) return;

        inst._data?.Clear();
        inst._resource?.Clear();
        inst._input?.Clear();
        inst._ui?.Clear();
        inst._story?.Clear();
    }

    protected override void OnDestroy() 
    {
        if (_story != null) _story.OnDestroy();
        if (_ui != null) _ui.OnDestroy();
        if (_input != null) _input.OnDestroy();
        if (_resource != null) _resource.OnDestroy();
        if(_data != null) _data.OnDestroy();

        base.OnDestroy();
    }
}
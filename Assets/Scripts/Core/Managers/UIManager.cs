using System.Collections.Generic;
using UnityEngine;

public class UIManager : IManager
{
    private bool _init = false;
    private int _order = 10;
    private Stack<UI_Popup> _popupStack = new Stack<UI_Popup>();
    private UI_Scene _sceneUI = null;
    public UI_Scene CurrentSceneUI => _sceneUI;
    private UI_Popup_Fade _fade = null;
    public void RegisterSceneUI(UI_Scene sceneUI)
    {
        _sceneUI = sceneUI;
    }
    private const int FadeSortOrder = 9999;

    public int PopupCount => _popupStack.Count;
    private GameObject _root;
    public GameObject Root
    {
        get
        {
            if (_root == null)
            {
                _root = GameObject.Find("@UI_Root");
                if (_root == null)
                {
                    _root = new GameObject { name = "@UI_Root" };
                }
            }
            return _root;
        }
    }

    public void Init()
    {
        if (_init) return;
        _init = true;

        // 초기화 로직
        _order = 10;
    }

    public void SetCanvas(GameObject go, bool sort = true, int sortOrder = 0)
    {
        Canvas canvas = Util.GetOrAddComponent<Canvas>(go);
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            canvas.overrideSorting = true;
            if (sort)
            {
                canvas.sortingOrder = _order;
                _order++;
            }
            else
            {
                canvas.sortingOrder = sortOrder;
            }
        }
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            canvas.worldCamera = Camera.main;
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = GameObject.FindFirstObjectByType<Camera>();
            }

            canvas.overrideSorting = true;
            if (sort)
            {
                canvas.sortingOrder = _order;
                _order++;
            }
            else
            {
                canvas.sortingOrder = sortOrder;
            }
        }
    }

    public T ShowSceneUI<T>(string name = null) where T : UI_Scene
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        if (_sceneUI != null)
        {
            SingletonManagers.Resource.Destroy(_sceneUI.gameObject);
            _sceneUI = null;
        }

        GameObject go = SingletonManagers.Resource.Instantiate($"UI/Scene/{name}", Root.transform);

        if (go == null)
        {
            Debug.LogError($"[UIManager] Failed to load Scene UI: {name}");
            return null;
        }

        // go.transform.SetParent(Root.transform);

        T sceneUI = Util.GetOrAddComponent<T>(go);
        _sceneUI = sceneUI;

        return sceneUI;
    }

    public T ShowPopupUI<T>(string name = null) where T : UI_Popup
    {
        if (string.IsNullOrEmpty(name))
            name = typeof(T).Name;

        GameObject go = SingletonManagers.Resource.Instantiate($"UI/Popup/{name}", Root.transform);

        if (go == null)
        {
            Debug.LogError($"[UIManager] Failed to load Popup UI: {name}");
            return null;
        }

        // go.transform.SetParent(Root.transform);

        T popup = Util.GetOrAddComponent<T>(go);
        _popupStack.Push(popup);

        return popup;
    }

    public UI_Popup GetTopPopup()
    {
        if (_popupStack.Count == 0) return null;
        return _popupStack.Peek();
    }

    public void ClosePopupUI(UI_Popup popup)
    {
        if (_popupStack.Count == 0) return;

        // 스택의 맨 위에 있는 것과 닫으려는 것이 다르면 에러 (순서 꼬임 방지)
        if (_popupStack.Peek() != popup)
        {
            Debug.LogError("Close Popup Failed! Top popup is not the requested popup.");
            return;
        }

        ClosePopupUI();
    }

    public void ClosePopupUI()
    {
        if (_popupStack.Count == 0) return;

        UI_Popup popup = _popupStack.Pop();

        SingletonManagers.Resource.Destroy(popup.gameObject);

        popup = null;
        _order--;
    }

    public void CloseAllPopupUI()
    {
        while (_popupStack.Count > 0)
        {
            ClosePopupUI();
        }
    }

    /// <summary>
    /// 씬 전체에서 단 하나만 존재하는 Fade UI를 반환한다. 없으면 생성한다.
    /// 팝업 스택에는 포함되지 않으며, 항상 최상단(SortOrder 9999)에 고정된다.
    /// </summary>
    public UI_Popup_Fade GetFade()
    {
        if (_fade != null) return _fade;

        GameObject go = SingletonManagers.Resource.Instantiate("UI/Popup/UI_Popup_Fade", Root.transform);
        if (go == null)
        {
            Debug.LogError("[UIManager] UI_Popup_Fade 로드에 실패했습니다.");
            return null;
        }

        _fade = Util.GetOrAddComponent<UI_Popup_Fade>(go);
        SetCanvas(go, false, FadeSortOrder); // 항상 최상단 고정
        _order--;
        return _fade;
    }

    public void Clear()
    {
        CloseAllPopupUI();

        if (_fade != null)
        {
            SingletonManagers.Resource.Destroy(_fade.gameObject);
            _fade = null;
        }

        _sceneUI = null;
        _root = null;
        _order = 10;
    }

    public void OnDestroy() => Clear();
}
using System.Collections;
using UnityEngine;

public class UI_EventListener : MonoBehaviour
{
    private bool _ignoreInput = false;

    private void Start()
    {
        // UI 관련 입력 이벤트들 구독
        SingletonManagers.Input.OnMenuPressed -= HandleGamePlayMenu;
        SingletonManagers.Input.OnMenuPressed += HandleGamePlayMenu;

        SingletonManagers.Input.OnInput -= HandleUIInput;
        SingletonManagers.Input.OnInput += HandleUIInput;

        SingletonManagers.Input.OnUISubmitPressed -= HandleUISubmit;
        SingletonManagers.Input.OnUISubmitPressed += HandleUISubmit;

        SingletonManagers.Input.OnUICancelPressed -= HandleUICancel;
        SingletonManagers.Input.OnUICancelPressed += HandleUICancel;
    }

    private IEnumerator IgnoreInputForMoment()
    {
        _ignoreInput = true;
        yield return null;
        _ignoreInput = false;
    }

    private void HandleGamePlayMenu()
    {
        UI_Scene sceneUI = SingletonManagers.UI.CurrentSceneUI;

        // 메인 메뉴 같은 UI 전용 화면에서는 GamePlay Menu 토글을 사용하지 않음
        if (sceneUI != null && sceneUI.KeepUIInputMode)
        {
            SingletonManagers.Input.SetInputModeUI(true);
            return;
        }

        int currentPopupCount = SingletonManagers.UI.PopupCount;

        if (currentPopupCount > 0)
        {
            SingletonManagers.UI.ClosePopupUI();

            if (SingletonManagers.UI.PopupCount == 0)
            {
                Time.timeScale = 1f;
                SingletonManagers.Input.SetInputModeUI(false);
            }
        }
        else
        {
            Time.timeScale = 0f;
            SingletonManagers.Input.SetInputModeUI(true);
            SingletonManagers.UI.ShowPopupUI<UI_Popup_InGamePause>();

            StartCoroutine(IgnoreInputForMoment());
        }
    }

    private void HandleUIInput(Vector2 dir)
    {
        if (_ignoreInput) return;

        var popup = SingletonManagers.UI.GetTopPopup();
        if (popup != null)
        {
            popup.OnInput(dir);
        }
    }

    private void HandleUISubmit()
    {
        if (_ignoreInput) return;

        var popup = SingletonManagers.UI.GetTopPopup();
        if (popup != null)
        {
            popup.OnSubmit();
        }
    }
    private void HandleUICancel()
    {
        if (_ignoreInput) return;

        var popup = SingletonManagers.UI.GetTopPopup();

        if (popup != null)
        {
            popup.OnCancel();
        }
        else
        {
            UI_Scene sceneUI = SingletonManagers.UI.CurrentSceneUI;

            // 메인 메뉴 같은 UI 전용 화면에서는 ESC로 UI 모드를 끄지 않는다.
            if (sceneUI != null && sceneUI.KeepUIInputMode)
            {
                SingletonManagers.Input.SetInputModeUI(true);
                return;
            }
        }

        if (SingletonManagers.UI.PopupCount == 0)
        {
            UI_Scene sceneUI = SingletonManagers.UI.CurrentSceneUI;

            if (sceneUI != null && sceneUI.KeepUIInputMode)
            {
                SingletonManagers.Input.SetInputModeUI(true);
            }
            else
            {
                SingletonManagers.Input.SetInputModeUI(false);
                Time.timeScale = 1f;
            }
        }
    }
}

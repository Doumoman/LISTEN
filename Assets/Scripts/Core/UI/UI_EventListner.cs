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
        int currentPopupCount = SingletonManagers.UI.PopupCount;
        if (currentPopupCount > 0)
        {
            SingletonManagers.UI.ClosePopupUI();

            if (SingletonManagers.UI.PopupCount == 0)
            {
                SingletonManagers.Input.SetInputModeUI(false);
            }
        }
        else
        {
            SingletonManagers.Input.SetInputModeUI(true);
            StartCoroutine(IgnoreInputForMoment()); // 한 프레임동안 남아있는 입력 방지
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

        if (SingletonManagers.UI.PopupCount == 0)
        {
            SingletonManagers.Input.SetInputModeUI(false);
            Time.timeScale = 1f;
        }
    }
}

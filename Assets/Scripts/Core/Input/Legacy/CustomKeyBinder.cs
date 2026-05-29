using UnityEngine;
using UnityEngine.InputSystem;

public class CustomKeyBinder : MonoBehaviour
{
    [Header("Input Action")]
    public InputActionReference gemAction; // 보석 키 (기본 Z)
    public InputActionReference interactAction; // 상호작용 키 (기본 X)
    public InputActionReference jumpAction; // 점프 키 (기본 C)

    [Header("UI")]
    // TODO: 키 설정창 UI들

    private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;  

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LoadBindings();   
    }

    public void RebindAction(InputActionReference inputAction)
    {
        // 리바인딩 할 액션 비활성화
        inputAction.action.Disable();

        _rebindingOperation = inputAction.action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse") // 마우스 클릭으로 이동 키가 바뀌는 것을 방지
            .OnMatchWaitForAnother(0.1f) // 여러 키가 동시에 눌리는 것 방지
            .OnComplete(operation => FinishRebind(inputAction)) // 입력 성공
            .OnCancel(operation => CancleRebind(inputAction)) // 취소 시
            .Start();
    }

    private void FinishRebind(InputActionReference inputAction)
    {
        _rebindingOperation.Dispose();
        inputAction.action.Enable();

        SaveBindings(inputAction);

        Debug.Log("키 변경 완료");
    }

    private void CancleRebind(InputActionReference inputAction)
    {
        _rebindingOperation.Dispose();
        inputAction.action.Enable();
    }


    private void SaveBindings(InputActionReference inputAction)
    {
        string rebinds = inputAction.action.actionMap.SaveBindingOverridesAsJson(); // 해당 액션이 속한 액션맵 전체를 저장
        PlayerPrefs.SetString("PlayerCustomKeyBindings", rebinds);
        PlayerPrefs.Save();
    }

    private void LoadBindings()
    {
        if (PlayerPrefs.HasKey("PlayerCustomKeyBindings"))
        {
            string rebinds = PlayerPrefs.GetString("PlayerCustomKeyBindings");

            if (interactAction != null)
            {
                interactAction.action.actionMap.LoadBindingOverridesFromJson(rebinds);
                // Player의 액션맵에 대해서만 저장하고 있음
                // TODO: Player 조작 외에도 키를 커스텀 바인딩이 필요한지?
            }
        }
    }
}

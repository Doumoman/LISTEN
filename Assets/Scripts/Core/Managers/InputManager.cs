using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : IManager
{
    private bool _init = false;
    private GameControls _controls;

    // ㅡㅡㅡㅡㅡ GamePlay Action Map ㅡㅡㅡㅡㅡ
    public event Action OnMenuPressed; // 메뉴 열기
    public event Action OnInteractPressed; // 상호작용
    public event Action OnJumpPressed;  // 점프
    public event Action OnJumpReleased; // 점프 키 해제
    public event Action OnSneakPressed; // 엎드리기
    public event Action OnSneakReleased; // 엎드리기 키 해제
    public event Action<Vector2> OnMove; // 플레이어 이동
    public event Action<Vector2> OnLadderMove; // 플레이어 사다리 이동

    // ㅡㅡㅡㅡㅡ UI Action Map ㅡㅡㅡㅡㅡ
    public event Action<Vector2> OnInput; // UI 이동
    public event Action OnUISubmitPressed; // 확인 (Enter)
    public event Action OnUICancelPressed; // 취소 (Esc)

    public Vector2 MoveDirection => _controls?.GamePlay.Move.ReadValue<Vector2>() ?? Vector2.zero;
    public Vector2 LadderMoveDirection => _controls?.GamePlay.LadderMove.ReadValue<Vector2>() ?? Vector2.zero;

    public void Init()
    {
        if (_init) return;
        _init = true;

        _controls = new GameControls();

        // ㅡㅡㅡㅡㅡ GamePlay Action Map ㅡㅡㅡㅡㅡ
        _controls.GamePlay.Menu.performed += HandleMenuPerformed;
        _controls.GamePlay.Interact.performed += HandleInteractPerformed;

        _controls.GamePlay.Move.performed += HandleMovePerformed;
        _controls.GamePlay.Move.canceled += HandleMoveCanceled;

        _controls.GamePlay.LadderMove.performed += HandleLadderMovePerformed;
        _controls.GamePlay.LadderMove.canceled += HandleLadderMoveCanceled;

        _controls.GamePlay.Jump.performed += HandleJumpPerformed;
        _controls.GamePlay.Jump.canceled += HandleJumpCanceled;
        
        _controls.GamePlay.Sneak.performed += HandleSneakPerformed;
        _controls.GamePlay.Sneak.canceled += HandleSneakCanceled;


        // ㅡㅡㅡㅡㅡ UI Action Map ㅡㅡㅡㅡㅡ
        _controls.UI.Input.performed += HandleUIInputPerformed;
        _controls.UI.Submit.performed += HandleSubmitPerformed;
        _controls.UI.Cancel.performed += HandleCancelPerformed;

        // 초기에는 플레이어 활성화, UI 비활성화
        _controls?.Enable();
        SetInputModeUI(false);
    }

    // 외부에서 전체 입력을 껐다 켰다 할 수 있게함
    public void SetInput(bool active)
    {
        if (_controls == null) return;

        if (active)
            _controls.Enable();
        else
            _controls.Disable();
    }

    // 모드 전환 기능 (캐릭터 조작 <-> UI 조작)
    public void SetInputModeUI(bool isUI)
    {
        if (_controls == null) return;

        if (isUI)
        {
            _controls.GamePlay.Disable();
            _controls.UI.Enable();
        }
        else
        {
            _controls.UI.Disable();
            _controls.GamePlay.Enable();
        }
    }

    public void Clear()
    {
        OnMenuPressed = null;
        OnInteractPressed = null;
        OnJumpPressed = null;
        OnJumpReleased = null;
        OnSneakPressed = null;
        OnSneakReleased = null;
        OnMove = null;
        OnLadderMove = null;
        OnInput = null;
        OnUISubmitPressed = null;
        OnUICancelPressed = null;
    }

    public void OnDestroy()
    {
        if (_controls != null)
        {
            // ㅡㅡㅡㅡㅡ GamePlay Action Map ㅡㅡㅡㅡㅡ
            _controls.GamePlay.Menu.performed -= HandleMenuPerformed;
            _controls.GamePlay.Interact.performed -= HandleInteractPerformed;

            _controls.GamePlay.Move.performed -= HandleMovePerformed;
            _controls.GamePlay.Move.canceled -= HandleMoveCanceled;

            _controls.GamePlay.LadderMove.performed -= HandleLadderMovePerformed;
            _controls.GamePlay.LadderMove.canceled -= HandleLadderMoveCanceled;

            _controls.GamePlay.Jump.performed -= HandleJumpPerformed;
            _controls.GamePlay.Jump.canceled -= HandleJumpCanceled;

            _controls.GamePlay.Sneak.performed -= HandleSneakPerformed;
            _controls.GamePlay.Sneak.canceled -= HandleSneakCanceled;

            // ㅡㅡㅡㅡㅡ UI Action Map ㅡㅡㅡㅡㅡ
            _controls.UI.Input.performed -= HandleUIInputPerformed;
            _controls.UI.Submit.performed -= HandleSubmitPerformed;
            _controls.UI.Cancel.performed -= HandleCancelPerformed;

            // 비활성화 및 메모리 해제
            _controls.Disable();
            _controls.Dispose();
            _controls = null;
        }

        Clear();
        _init = false;
    }

    #region Input Action Handlers (명시적 콜백 메서드들)

    // ㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡ GamePlay Action Map ㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡ
    private void HandleMenuPerformed(InputAction.CallbackContext context) => OnMenuPressed?.Invoke();
    private void HandleInteractPerformed(InputAction.CallbackContext context) => OnInteractPressed?.Invoke();

    private void HandleJumpPerformed(InputAction.CallbackContext context) => OnJumpPressed?.Invoke();
    private void HandleJumpCanceled(InputAction.CallbackContext context) => OnJumpReleased?.Invoke();

    private void HandleSneakPerformed(InputAction.CallbackContext context) => OnSneakPressed?.Invoke();
    private void HandleSneakCanceled(InputAction.CallbackContext context) => OnSneakReleased?.Invoke();

    private void HandleMovePerformed(InputAction.CallbackContext context) => OnMove?.Invoke(context.ReadValue<Vector2>());
    private void HandleMoveCanceled(InputAction.CallbackContext context) => OnMove?.Invoke(Vector2.zero);

    private void HandleLadderMovePerformed(InputAction.CallbackContext context) => OnLadderMove?.Invoke(context.ReadValue<Vector2>());
    private void HandleLadderMoveCanceled(InputAction.CallbackContext context) => OnLadderMove?.Invoke(Vector2.zero);

    // ㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡ UI Action Map ㅡㅡㅡㅡㅡㅡㅡㅡㅡㅡ
    private void HandleUIInputPerformed(InputAction.CallbackContext context) => OnInput?.Invoke(context.ReadValue<Vector2>());
    private void HandleSubmitPerformed(InputAction.CallbackContext context) => OnUISubmitPressed?.Invoke();
    private void HandleCancelPerformed(InputAction.CallbackContext context) => OnUICancelPressed?.Invoke();

    #endregion
}
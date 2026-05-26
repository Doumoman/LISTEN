using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class InputManager
{
    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

    public bool IsRebinding { get; private set; }

    public string GetBindingDisplayName(InputRebindAction action)
    {
        InputAction inputAction = GetInputAction(action);
        if (inputAction == null) return "-";

        int bindingIndex = GetFirstNormalBindingIndex(inputAction);
        if (bindingIndex < 0) return "-";

        return inputAction.GetBindingDisplayString(bindingIndex);
    }

    public string GetBindingDisplayNameFromPath(InputRebindAction action)
    {
        InputAction inputAction = GetInputAction(action);
        if (inputAction == null) return "-";

        int bindingIndex = GetFirstNormalBindingIndex(inputAction);
        if (bindingIndex < 0) return "-";

        return InputControlPath.ToHumanReadableString(
            GetEffectivePath(inputAction.bindings[bindingIndex]),
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );
    }

    public void StartRebind(
        InputRebindAction action,
        Action<string> onComplete,
        Action<string> onDuplicate = null,
        Action<string> onCanceled = null)
    {
        InputAction inputAction = GetInputAction(action);

        if (inputAction == null)
        {
            Debug.LogError($"[InputManager] Rebind 대상 액션을 찾지 못했습니다: {action}");
            return;
        }

        if (IsRebinding)
            return;

        int bindingIndex = GetFirstNormalBindingIndex(inputAction);

        if (bindingIndex < 0)
        {
            Debug.LogError($"[InputManager] 일반 키 바인딩을 찾지 못했습니다: {inputAction.name}");
            return;
        }

        string oldPath = GetEffectivePath(inputAction.bindings[bindingIndex]);

        IsRebinding = true;

        inputAction.Disable();

        _rebindOperation = inputAction.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Gamepad>")
            .OnComplete(operation =>
            {
                operation.Dispose();
                _rebindOperation = null;

                string newPath = GetEffectivePath(inputAction.bindings[bindingIndex]);

                // 기존 키를 다시 누르면 취소
                if (IsSamePath(oldPath, newPath))
                {
                    RollbackBinding(inputAction, bindingIndex, oldPath);

                    inputAction.Enable();
                    IsRebinding = false;

                    onCanceled?.Invoke(GetDisplayNameFromPath(oldPath));
                    return;
                }

                // 중복 키면 롤백
                if (IsDuplicateBinding(inputAction, newPath))
                {
                    RollbackBinding(inputAction, bindingIndex, oldPath);

                    inputAction.Enable();
                    IsRebinding = false;

                    SaveBindingOverrides();

                    onDuplicate?.Invoke(GetDisplayNameFromPath(oldPath));
                    return;
                }

                inputAction.Enable();
                IsRebinding = false;

                SaveBindingOverrides();

                onComplete?.Invoke(inputAction.GetBindingDisplayString(bindingIndex));
            })
            .OnCancel(operation =>
            {
                operation.Dispose();
                _rebindOperation = null;

                RollbackBinding(inputAction, bindingIndex, oldPath);

                inputAction.Enable();
                IsRebinding = false;

                onCanceled?.Invoke(GetDisplayNameFromPath(oldPath));
            });

        _rebindOperation.Start();
    }

    public void SetDirectionKeyScheme(DirectionKeyScheme scheme)
    {
        if (_controls == null) return;

        if (scheme == DirectionKeyScheme.Arrow)
        {
            ApplyMoveBinding(
                left: "<Keyboard>/leftArrow",
                right: "<Keyboard>/rightArrow",
                up: "<Keyboard>/upArrow",
                down: "<Keyboard>/downArrow"
            );

            ApplySingleBindingOverride(_controls.GamePlay.Sneak, "<Keyboard>/downArrow");
        }
        else
        {
            ApplyMoveBinding(
                left: "<Keyboard>/a",
                right: "<Keyboard>/d",
                up: "<Keyboard>/w",
                down: "<Keyboard>/s"
            );

            ApplySingleBindingOverride(_controls.GamePlay.Sneak, "<Keyboard>/s");
        }

        PlayerPrefs.SetInt("DirectionKeyScheme", (int)scheme);
        SaveBindingOverrides();
    }

    public DirectionKeyScheme LoadDirectionKeyScheme()
    {
        return (DirectionKeyScheme)PlayerPrefs.GetInt("DirectionKeyScheme", 0);
    }

    public void LoadBindingOverrides()
    {
        if (_controls == null) return;

        string json = PlayerPrefs.GetString("InputBindingOverrides", "");

        if (!string.IsNullOrEmpty(json))
            _controls.asset.LoadBindingOverridesFromJson(json);

        SetDirectionKeyScheme(LoadDirectionKeyScheme());
    }

    public void SaveBindingOverrides()
    {
        if (_controls == null) return;

        string json = _controls.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("InputBindingOverrides", json);
        PlayerPrefs.Save();
    }

    public void ResetBindingOverrides()
    {
        if (_controls == null) return;

        _controls.asset.RemoveAllBindingOverrides();

        PlayerPrefs.DeleteKey("InputBindingOverrides");
        PlayerPrefs.DeleteKey("DirectionKeyScheme");
        PlayerPrefs.Save();

        SetDirectionKeyScheme(DirectionKeyScheme.Arrow);
    }

    private InputAction GetInputAction(InputRebindAction action)
    {
        if (_controls == null) return null;

        switch (action)
        {
            case InputRebindAction.Jump:
                return _controls.GamePlay.Jump;

            case InputRebindAction.Interact:
                return _controls.GamePlay.Interact;

            case InputRebindAction.Menu:
                return _controls.GamePlay.Menu;

            case InputRebindAction.Talk:
                return _controls.GamePlay.Talk;
        }

        return null;
    }

    private void ApplyMoveBinding(string left, string right, string up, string down)
    {
        ApplyCompositePartOverride(_controls.GamePlay.Move, "left", left);
        ApplyCompositePartOverride(_controls.GamePlay.Move, "right", right);

        ApplyCompositePartOverride(_controls.GamePlay.LadderMove, "up", up);
        ApplyCompositePartOverride(_controls.GamePlay.LadderMove, "down", down);
    }

    private void ApplyCompositePartOverride(InputAction action, string partName, string path)
    {
        if (action == null) return;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isPartOfComposite &&
                string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase))
            {
                action.ApplyBindingOverride(i, path);
                return;
            }
        }

        Debug.LogWarning($"[InputManager] {action.name}에서 Composite Part를 찾지 못했습니다: {partName}");
    }

    private void ApplySingleBindingOverride(InputAction action, string path)
    {
        if (action == null) return;

        int bindingIndex = GetFirstNormalBindingIndex(action);

        if (bindingIndex < 0)
        {
            Debug.LogWarning($"[InputManager] {action.name}에서 일반 바인딩을 찾지 못했습니다.");
            return;
        }

        action.ApplyBindingOverride(bindingIndex, path);
    }

    private bool IsDuplicateBinding(InputAction targetAction, string selectedPath)
    {
        if (_controls == null) return false;
        if (targetAction == null) return false;
        if (string.IsNullOrEmpty(selectedPath)) return false;

        InputAction[] singleActions =
        {
            _controls.GamePlay.Jump,
            _controls.GamePlay.Interact,
            _controls.GamePlay.Menu,
            _controls.GamePlay.Talk
        };

        foreach (InputAction action in singleActions)
        {
            if (action == null)
                continue;

            if (action == targetAction)
                continue;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                string path = GetEffectivePath(binding);

                if (IsSamePath(path, selectedPath))
                    return true;
            }
        }

        if (IsPathUsedByComposite(_controls.GamePlay.Move, selectedPath))
            return true;

        if (IsPathUsedByComposite(_controls.GamePlay.LadderMove, selectedPath))
            return true;

        if (IsPathUsedBySingleAction(_controls.GamePlay.Sneak, selectedPath))
            return true;

        return false;
    }

    private bool IsPathUsedBySingleAction(InputAction action, string selectedPath)
    {
        if (action == null) return false;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            string path = GetEffectivePath(binding);

            if (IsSamePath(path, selectedPath))
                return true;
        }

        return false;
    }

    private bool IsPathUsedByComposite(InputAction action, string selectedPath)
    {
        if (action == null) return false;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (!binding.isPartOfComposite)
                continue;

            string path = GetEffectivePath(binding);

            if (IsSamePath(path, selectedPath))
                return true;
        }

        return false;
    }

    private int GetFirstNormalBindingIndex(InputAction action)
    {
        if (action == null) return -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            if (!string.IsNullOrEmpty(binding.path) || !string.IsNullOrEmpty(binding.overridePath))
                return i;
        }

        return -1;
    }

    private string GetEffectivePath(InputBinding binding)
    {
        return string.IsNullOrEmpty(binding.overridePath)
            ? binding.path
            : binding.overridePath;
    }

    private void RollbackBinding(InputAction action, int bindingIndex, string oldPath)
    {
        action.RemoveBindingOverride(bindingIndex);

        if (!string.IsNullOrEmpty(oldPath))
            action.ApplyBindingOverride(bindingIndex, oldPath);
    }

    private bool IsSamePath(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private string GetDisplayNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "-";

        return InputControlPath.ToHumanReadableString(
            path,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );
    }
}
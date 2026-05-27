using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class InputManager
{
    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

    public bool IsRebinding { get; private set; }

    private struct RebindTarget
    {
        public InputAction action;
        public int bindingIndex;
        public bool syncSneak;

        public bool IsValid => action != null && bindingIndex >= 0;
    }

    public string GetBindingDisplayName(InputRebindAction action)
    {
        RebindTarget target = GetRebindTarget(action);

        if (!target.IsValid)
            return "-";

        return target.action.GetBindingDisplayString(target.bindingIndex);
    }

    public void StartRebind(
        InputRebindAction action,
        Action<string> onComplete,
        Action<string> onDuplicate = null,
        Action<string> onCanceled = null)
    {
        RebindTarget target = GetRebindTarget(action);

        if (!target.IsValid)
        {
            Debug.LogError($"[InputManager] Rebind 대상 바인딩을 찾지 못했습니다: {action}");
            return;
        }

        if (IsRebinding)
            return;

        InputAction inputAction = target.action;
        int bindingIndex = target.bindingIndex;
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

                if (IsSamePath(oldPath, newPath))
                {
                    RollbackBinding(inputAction, bindingIndex, oldPath);

                    inputAction.Enable();
                    IsRebinding = false;

                    onCanceled?.Invoke(GetDisplayNameFromPath(oldPath));
                    return;
                }

                if (IsDuplicateBinding(inputAction, bindingIndex, newPath))
                {
                    RollbackBinding(inputAction, bindingIndex, oldPath);

                    inputAction.Enable();
                    IsRebinding = false;

                    SaveBindingOverrides();

                    onDuplicate?.Invoke(GetDisplayNameFromPath(oldPath));
                    return;
                }

                if (target.syncSneak)
                {
                    ApplySingleBindingOverride(_controls.GamePlay.Sneak, newPath);
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

    public void LoadBindingOverrides()
    {
        if (_controls == null) return;

        string json = PlayerPrefs.GetString("InputBindingOverrides", "");

        if (!string.IsNullOrEmpty(json))
            _controls.asset.LoadBindingOverridesFromJson(json);
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
        PlayerPrefs.Save();
    }

    private RebindTarget GetRebindTarget(InputRebindAction action)
    {
        if (_controls == null)
            return default;

        switch (action)
        {
            case InputRebindAction.MoveLeft:
                return new RebindTarget
                {
                    action = _controls.GamePlay.Move,
                    bindingIndex = FindCompositePartIndex(_controls.GamePlay.Move, "left"),
                    syncSneak = false
                };

            case InputRebindAction.MoveRight:
                return new RebindTarget
                {
                    action = _controls.GamePlay.Move,
                    bindingIndex = FindCompositePartIndex(_controls.GamePlay.Move, "right"),
                    syncSneak = false
                };

            case InputRebindAction.MoveUp:
                return new RebindTarget
                {
                    action = _controls.GamePlay.LadderMove,
                    bindingIndex = FindCompositePartIndex(_controls.GamePlay.LadderMove, "up"),
                    syncSneak = false
                };

            case InputRebindAction.MoveDown:
                return new RebindTarget
                {
                    action = _controls.GamePlay.LadderMove,
                    bindingIndex = FindCompositePartIndex(_controls.GamePlay.LadderMove, "down"),
                    syncSneak = true
                };

            case InputRebindAction.Jump:
                return new RebindTarget
                {
                    action = _controls.GamePlay.Jump,
                    bindingIndex = GetFirstNormalBindingIndex(_controls.GamePlay.Jump),
                    syncSneak = false
                };

            case InputRebindAction.Interact:
                return new RebindTarget
                {
                    action = _controls.GamePlay.Interact,
                    bindingIndex = GetFirstNormalBindingIndex(_controls.GamePlay.Interact),
                    syncSneak = false
                };

            case InputRebindAction.Menu:
                return new RebindTarget
                {
                    action = _controls.GamePlay.Menu,
                    bindingIndex = GetFirstNormalBindingIndex(_controls.GamePlay.Menu),
                    syncSneak = false
                };

            case InputRebindAction.Talk:
                return new RebindTarget
                {
                    action = _controls.GamePlay.Talk,
                    bindingIndex = GetFirstNormalBindingIndex(_controls.GamePlay.Talk),
                    syncSneak = false
                };
        }

        return default;
    }

    private int FindCompositePartIndex(InputAction action, string partName)
    {
        if (action == null) return -1;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isPartOfComposite &&
                string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
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

    private bool IsDuplicateBinding(InputAction targetAction, int targetBindingIndex, string selectedPath)
    {
        if (_controls == null) return false;
        if (targetAction == null) return false;
        if (string.IsNullOrEmpty(selectedPath)) return false;

        InputAction[] allActions =
        {
            _controls.GamePlay.Move,
            _controls.GamePlay.LadderMove,
            _controls.GamePlay.Jump,
            _controls.GamePlay.Interact,
            _controls.GamePlay.Menu,
            _controls.GamePlay.Talk,
            _controls.GamePlay.Sneak
        };

        foreach (InputAction action in allActions)
        {
            if (action == null)
                continue;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];

                if (binding.isComposite)
                    continue;

                if (action == targetAction && i == targetBindingIndex)
                    continue;

                // MoveDown은 Sneak과 같은 키를 공유해야 하므로 중복으로 보지 않음
                if (targetAction == _controls.GamePlay.LadderMove &&
                    IsCompositePart(action: targetAction, index: targetBindingIndex, partName: "down") &&
                    action == _controls.GamePlay.Sneak)
                {
                    continue;
                }

                string path = GetEffectivePath(binding);

                if (IsSamePath(path, selectedPath))
                    return true;
            }
        }

        return false;
    }

    private bool IsCompositePart(InputAction action, int index, string partName)
    {
        if (action == null) return false;
        if (index < 0 || index >= action.bindings.Count) return false;

        InputBinding binding = action.bindings[index];

        return binding.isPartOfComposite &&
               string.Equals(binding.name, partName, StringComparison.OrdinalIgnoreCase);
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
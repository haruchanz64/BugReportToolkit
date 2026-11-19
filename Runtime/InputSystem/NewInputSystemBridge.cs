using BugReporter.Config;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace BugReporter.InputSystemBridge
{
    /// <summary>
    /// Automatically compiled only when com.unity.inputsystem is installed.
    /// Captures New Input System button/key presses as breadcrumbs and handles the toggle hotkey.
    /// </summary>
    internal sealed class NewInputSystemBridge : MonoBehaviour
    {
        private static NewInputSystemBridge _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;

            var go = new GameObject("[BugReporter.InputSystemBridge]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NewInputSystemBridge>();
        }

        private void OnEnable()
        {
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
                return;

            BugReporterConfig config = Core.BugReporter.ActiveConfig;
            Key toggleKey = MapHotkey(config != null ? config.toggleHotkey : KeyCode.F9);
            if (toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame)
            {
                Core.BugReporter.Toggle();
            }
        }

        private static void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (Core.BugReporter.IsOpen || device == null)
                return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            foreach (InputControl control in eventPtr.EnumerateChangedControls(device))
            {
                if (control is ButtonControl button && button.ReadValueFromEvent(eventPtr) > 0.5f)
                {
                    Core.BugReporter.RecordInput(device.displayName, control.name, "Pressed", "1");
                }
            }
        }

        private static Key MapHotkey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;
                case KeyCode.BackQuote: return Key.Backquote;
                default: return Key.F9;
            }
        }
    }
}

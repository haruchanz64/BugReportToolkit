using System;
using System.Collections.Generic;
using BugReporter.Core;
using UnityEngine;

namespace BugReporter.Capture
{
    public sealed class InputBreadcrumbRecorder
    {
        private readonly RingBuffer<InputBreadcrumb> _buffer;
        private readonly DateTime _startTimeUtc;

        public InputBreadcrumbRecorder(int capacity = 250)
        {
            _buffer = new RingBuffer<InputBreadcrumb>(Math.Max(16, capacity));
            _startTimeUtc = DateTime.UtcNow;
        }

        public void Record(string device, string control, string action, string value = "")
        {
            DateTime now = DateTime.UtcNow;
            _buffer.Add(new InputBreadcrumb
            {
                timestampUtc = now.ToString("O"),
                elapsedSeconds = (float)(now - _startTimeUtc).TotalSeconds,
                device = device ?? string.Empty,
                control = control ?? string.Empty,
                action = action ?? string.Empty,
                value = value ?? string.Empty
            });
        }

        public List<InputBreadcrumb> GetSnapshot() => _buffer.Snapshot();

        public void Clear() => _buffer.Clear();

#if !BUG_REPORTER_INPUT_SYSTEM
        private static readonly KeyCode[] TrackedLegacyKeys =
        {
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.Space, KeyCode.Escape, KeyCode.Return, KeyCode.Tab,
            KeyCode.E, KeyCode.F, KeyCode.Q, KeyCode.R, KeyCode.LeftShift, KeyCode.LeftControl
        };

        public void PollLegacyInput()
        {
            for (int i = 0; i < TrackedLegacyKeys.Length; i++)
            {
                KeyCode key = TrackedLegacyKeys[i];
                if (Input.GetKeyDown(key))
                    Record("Keyboard", key.ToString(), "Pressed", "1");
            }

            if (Input.GetMouseButtonDown(0))
                Record("Mouse", "LeftButton", "Pressed", Input.mousePosition.ToString("F0"));
            if (Input.GetMouseButtonDown(1))
                Record("Mouse", "RightButton", "Pressed", Input.mousePosition.ToString("F0"));
        }
#endif
    }
}

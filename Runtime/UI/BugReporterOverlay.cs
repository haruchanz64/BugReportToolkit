using System;
using System.Collections.Generic;
using System.Text;
using BugReporter.Core;
using BugReporter.Export;
using UnityEngine;

namespace BugReporter.UI
{
    /// <summary>
    /// Self-contained zero-prefab runtime IMGUI overlay for submitting bug reports
    /// with screenshot preview, metadata fields, and capture toggles.
    /// </summary>
    internal sealed class BugReporterOverlay : IDisposable
    {
        private readonly Core.BugReporter _owner;

        private Texture2D _previewTexture;
        private byte[] _screenshotPng;

        private string _title = string.Empty;
        private string _description = string.Empty;
        private BugSeverity _severity = BugSeverity.Medium;
        private BugCategory _category = BugCategory.Gameplay;
        private bool _includeLogs = true;
        private bool _includeInputs = true;
        private bool _includeScene = true;
        private bool _isSubmitting;
        private string _statusMessage = string.Empty;
        private Rect _windowRect;

        public bool IsVisible { get; private set; }

        public BugReporterOverlay(Core.BugReporter owner)
        {
            _owner = owner;
        }

        public void Show(Texture2D previewTexture, byte[] screenshotPng)
        {
            DisposePreview();
            _previewTexture = previewTexture;
            _screenshotPng = screenshotPng;
            _statusMessage = string.Empty;
            _isSubmitting = false;

            float width = Mathf.Min(560f, Screen.width - 40f);
            float height = Mathf.Min(600f, Screen.height - 40f);
            _windowRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
            DisposePreview();
        }

        public void OnGUI()
        {
            if (!IsVisible)
                return;

            // Dimmed backdrop
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            _windowRect = GUILayout.Window(948271, _windowRect, DrawWindow, "In-Game Bug Reporter");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(4f);

            GUILayout.Label("Title");
            _title = GUILayout.TextField(_title, 120);

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Severity:", GUILayout.Width(65f));
            _severity = (BugSeverity)GUILayout.SelectionGrid((int)_severity, Enum.GetNames(typeof(BugSeverity)), 4);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Category:", GUILayout.Width(65f));
            _category = (BugCategory)GUILayout.SelectionGrid((int)_category, Enum.GetNames(typeof(BugCategory)), 4);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("Steps to Reproduce / Description");
            _description = GUILayout.TextArea(_description, GUILayout.Height(110f));

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            _includeLogs = GUILayout.Toggle(_includeLogs, "Attach Logs");
            _includeInputs = GUILayout.Toggle(_includeInputs, "Attach Input Trail");
            _includeScene = GUILayout.Toggle(_includeScene, "Attach Scene Snapshot");
            GUILayout.EndHorizontal();

            if (_previewTexture != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Captured Frame Preview:");
                float previewH = 140f;
                float aspect = (float)_previewTexture.width / Mathf.Max(1, _previewTexture.height);
                float previewW = Mathf.Min(_windowRect.width - 30f, previewH * aspect);
                Rect previewRect = GUILayoutUtility.GetRect(previewW, previewH, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_statusMessage);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = !_isSubmitting;
            if (GUILayout.Button(_isSubmitting ? "Submitting..." : "Submit Bug Report", GUILayout.Height(32f)))
            {
                Submit();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(100f), GUILayout.Height(32f)))
            {
                _owner.CloseInternal();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        private void Submit()
        {
            _isSubmitting = true;
            _statusMessage = "Packaging bug report bundle...";

            BugReportData report = _owner.BuildReport(
                _title,
                _description,
                _severity,
                _category,
                _screenshotPng,
                _includeLogs,
                _includeInputs,
                _includeScene);

            _owner.SubmitReport(report, results =>
            {
                _isSubmitting = false;
                var sb = new StringBuilder();
                foreach (TransportResult r in results)
                    sb.AppendLine($"{(r.Success ? "[OK]" : "[FAIL]")} {r.TransportName}: {r.Message}");
                _statusMessage = sb.ToString().Trim();
                _title = string.Empty;
                _description = string.Empty;
            });
        }

        private void DisposePreview()
        {
            if (_previewTexture != null)
            {
                UnityEngine.Object.Destroy(_previewTexture);
                _previewTexture = null;
            }
            _screenshotPng = null;
        }

        public void Dispose() => DisposePreview();
    }
}

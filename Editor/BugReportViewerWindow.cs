using System;
using System.IO;
using BugReporter.Core;
using BugReporter.Export;
using UnityEditor;
using UnityEngine;

namespace BugReporter.Editor
{
    /// <summary>
    /// Unity Editor window to inspect exported .zip bug report bundles:
    /// screenshot preview, device & performance metrics, custom context,
    /// filterable console logs, scene snapshot, and recorded input breadcrumbs.
    /// </summary>
    public sealed class BugReportViewerWindow : EditorWindow
    {
        private enum ViewerTab
        {
            Overview = 0,
            Logs = 1,
            Inputs = 2,
            Scene = 3
        }

        private string _loadedZipPath = string.Empty;
        private BugReportData _report;
        private Texture2D _screenshotTex;
        private string _loadError;

        private ViewerTab _tab = ViewerTab.Overview;
        private Vector2 _scroll;
        private string _logSearch = string.Empty;
        private bool _showInfoLogs = true;
        private bool _showWarnLogs = true;
        private bool _showErrorLogs = true;

        [MenuItem("Tools/Bug Reporter/Report Viewer", priority = 100)]
        public static void OpenWindow()
        {
            var win = GetWindow<BugReportViewerWindow>("Bug Report Viewer");
            win.minSize = new Vector2(700f, 500f);
            win.Show();
        }

        [MenuItem("Tools/Bug Reporter/Open Local Bug Reports Folder", priority = 101)]
        public static void OpenLocalReportsFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, "BugReports");
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private void OnDisable()
        {
            DisposeScreenshot();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!string.IsNullOrEmpty(_loadError))
                EditorGUILayout.HelpBox(_loadError, MessageType.Error);

            if (_report == null)
            {
                EditorGUILayout.HelpBox(
                    "Open a .zip bug report bundle (or click 'Load Latest' to inspect the most recent local capture).",
                    MessageType.Info);
                return;
            }

            _tab = (ViewerTab)GUILayout.Toolbar(
                (int)_tab,
                new[]
                {
                    "Overview",
                    $"Logs ({_report.logs?.Count ?? 0})",
                    $"Inputs ({_report.inputs?.Count ?? 0})",
                    $"Scene ({_report.scene?.rootObjects?.Count ?? 0})"
                },
                GUILayout.Height(26f));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case ViewerTab.Overview: DrawOverviewTab(); break;
                case ViewerTab.Logs: DrawLogsTab(); break;
                case ViewerTab.Inputs: DrawInputsTab(); break;
                case ViewerTab.Scene: DrawSceneTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Open .zip...", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                {
                    string defaultDir = Path.Combine(Application.persistentDataPath, "BugReports");
                    string path = EditorUtility.OpenFilePanel("Open Bug Report Bundle", Directory.Exists(defaultDir) ? defaultDir : "", "zip");
                    if (!string.IsNullOrEmpty(path))
                        LoadBundle(path);
                }

                if (GUILayout.Button("Load Latest", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                {
                    LoadLatestLocalBundle();
                }

                using (new EditorGUI.DisabledScope(_report == null))
                {
                    if (GUILayout.Button("Copy Markdown", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BugReportBundle.BuildMarkdownSummary(_report);
                        ShowNotification(new GUIContent("Markdown summary copied!"));
                    }
                }

                GUILayout.FlexibleSpace();
                if (!string.IsNullOrEmpty(_loadedZipPath))
                    GUILayout.Label(Path.GetFileName(_loadedZipPath), EditorStyles.miniLabel);
            }
        }

        private void DrawOverviewTab()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"[{_report.severity}] [{_report.category}] {_report.title}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Report ID: {_report.reportId}   |   UTC: {_report.createdAtUtc}", EditorStyles.miniLabel);

                if (!string.IsNullOrWhiteSpace(_report.description))
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(_report.description, EditorStyles.wordWrappedLabel);
                }
            }

            if (_screenshotTex != null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Screenshot ({_screenshotTex.width}x{_screenshotTex.height})", EditorStyles.boldLabel);
                    float maxH = 260f;
                    float aspect = (float)_screenshotTex.width / Mathf.Max(1, _screenshotTex.height);
                    float w = Mathf.Min(position.width - 40f, maxH * aspect);
                    Rect rect = GUILayoutUtility.GetRect(w, maxH, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(rect, _screenshotTex, ScaleMode.ScaleToFit);
                }
            }

            if (_report.performance != null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"FPS: {_report.performance.currentFps} (Avg: {_report.performance.averageFps}, Min: {_report.performance.minFps}) | Frame: {_report.performance.frameTimeMs} ms");
                    EditorGUILayout.LabelField($"Memory: {_report.performance.totalAllocatedMemoryMb} MB Total | {_report.performance.monoUsedMemoryMb} MB Mono | GC Last Frame: {_report.performance.gcAllocatedBytesLastFrame} B");
                }
            }

            if (_report.device != null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Device & Build", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"App: {_report.device.productName} v{_report.device.applicationVersion} (Unity {_report.device.unityVersion})");
                    EditorGUILayout.LabelField($"OS: {_report.device.operatingSystem} | Platform: {_report.device.platform}");
                    EditorGUILayout.LabelField($"Device: {_report.device.deviceModel} | CPU: {_report.device.processorType} ({_report.device.systemMemoryMb} MB)");
                    EditorGUILayout.LabelField($"GPU: {_report.device.graphicsDeviceName} ({_report.device.graphicsMemoryMb} MB) | Res: {_report.device.screenResolution} | Quality: {_report.device.qualityLevel}");
                }
            }

            if (_report.customContext != null && _report.customContext.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Custom Game Context", EditorStyles.boldLabel);
                    foreach (ContextSection section in _report.customContext)
                    {
                        EditorGUILayout.LabelField(section.sectionName, EditorStyles.miniBoldLabel);
                        EditorGUI.indentLevel++;
                        foreach (KeyValueEntry kv in section.entries)
                            EditorGUILayout.LabelField(kv.key, kv.value);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        private void DrawLogsTab()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _logSearch = EditorGUILayout.TextField(_logSearch, EditorStyles.toolbarSearchField);
                _showInfoLogs = GUILayout.Toggle(_showInfoLogs, "Info", EditorStyles.toolbarButton, GUILayout.Width(45f));
                _showWarnLogs = GUILayout.Toggle(_showWarnLogs, "Warn", EditorStyles.toolbarButton, GUILayout.Width(48f));
                _showErrorLogs = GUILayout.Toggle(_showErrorLogs, "Error", EditorStyles.toolbarButton, GUILayout.Width(48f));
            }

            if (_report.logs == null || _report.logs.Count == 0)
            {
                EditorGUILayout.HelpBox("No logs captured in this bundle.", MessageType.Info);
                return;
            }

            foreach (LogEntry entry in _report.logs)
            {
                bool isErr = entry.logType == "Error" || entry.logType == "Exception" || entry.logType == "Assert";
                bool isWarn = entry.logType == "Warning";
                if (isErr && !_showErrorLogs) continue;
                if (isWarn && !_showWarnLogs) continue;
                if (!isErr && !isWarn && !_showInfoLogs) continue;

                if (!string.IsNullOrEmpty(_logSearch) &&
                    entry.message.IndexOf(_logSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"[{entry.elapsedSeconds:F2}s] [{entry.logType}] {entry.message}", EditorStyles.wordWrappedLabel);
                    if (!string.IsNullOrWhiteSpace(entry.stackTrace))
                        EditorGUILayout.LabelField(entry.stackTrace.TrimEnd(), EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawInputsTab()
        {
            if (_report.inputs == null || _report.inputs.Count == 0)
            {
                EditorGUILayout.HelpBox("No input breadcrumbs captured in this bundle.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Time", EditorStyles.boldLabel, GUILayout.Width(70f));
                GUILayout.Label("Device", EditorStyles.boldLabel, GUILayout.Width(120f));
                GUILayout.Label("Control", EditorStyles.boldLabel, GUILayout.Width(140f));
                GUILayout.Label("Action", EditorStyles.boldLabel, GUILayout.Width(90f));
                GUILayout.Label("Value", EditorStyles.boldLabel);
            }

            foreach (InputBreadcrumb b in _report.inputs)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{b.elapsedSeconds:F2}s", GUILayout.Width(70f));
                    GUILayout.Label(b.device, GUILayout.Width(120f));
                    GUILayout.Label(b.control, GUILayout.Width(140f));
                    GUILayout.Label(b.action, GUILayout.Width(90f));
                    GUILayout.Label(b.value);
                }
            }
        }

        private void DrawSceneTab()
        {
            if (_report.scene == null)
            {
                EditorGUILayout.HelpBox("No scene snapshot captured in this bundle.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Active Scene: {_report.scene.activeSceneName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Loaded Scenes: {string.Join(", ", _report.scene.loadedScenes)}");
                EditorGUILayout.LabelField($"Main Camera: Pos {_report.scene.mainCameraPosition} | Rot {_report.scene.mainCameraRotationEuler}");
            }

            foreach (SceneObjectSummary obj in _report.scene.rootObjects)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string activeTag = obj.activeInHierarchy ? "Active" : "Inactive";
                    EditorGUILayout.LabelField($"{obj.name}  ({activeTag}, {obj.childCount} children)  @ {obj.position}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(obj.components, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void LoadLatestLocalBundle()
        {
            string dir = Path.Combine(Application.persistentDataPath, "BugReports");
            if (!Directory.Exists(dir))
            {
                _loadError = $"No local bug reports folder found at: {dir}";
                return;
            }

            string[] files = Directory.GetFiles(dir, "*.zip");
            if (files.Length == 0)
            {
                _loadError = $"No .zip bug report bundles found in: {dir}";
                return;
            }

            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            LoadBundle(files[0]);
        }

        private void LoadBundle(string zipPath)
        {
            _loadError = null;
            DisposeScreenshot();

            try
            {
                _report = BugReportBundle.LoadFromZipFile(zipPath);
                _loadedZipPath = zipPath;

                if (_report.screenshotPng != null && _report.screenshotPng.Length > 0)
                {
                    _screenshotTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    _screenshotTex.LoadImage(_report.screenshotPng);
                }
            }
            catch (Exception ex)
            {
                _report = null;
                _loadError = $"Failed to load bug report bundle: {ex.Message}";
            }
        }

        private void DisposeScreenshot()
        {
            if (_screenshotTex != null)
            {
                DestroyImmediate(_screenshotTex);
                _screenshotTex = null;
            }
        }
    }
}

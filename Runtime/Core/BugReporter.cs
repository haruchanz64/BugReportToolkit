using System;
using System.Collections;
using System.Collections.Generic;
using BugReporter.Capture;
using BugReporter.Config;
using BugReporter.Export;
using BugReporter.UI;
using UnityEngine;

namespace BugReporter.Core
{
    /// <summary>
    /// Central runtime facade for the In-Game Bug Reporter & Repro Capture toolkit.
    /// Call BugReporter.Open() from any button or debug console, or press the configured hotkey (default F9).
    /// </summary>
    public sealed class BugReporter : MonoBehaviour
    {
        private static BugReporter _instance;
        private static readonly List<IBugContextProvider> ContextProviders = new List<IBugContextProvider>();
        private static readonly List<IBugReportTransport> CustomTransports = new List<IBugReportTransport>();

        private BugReporterConfig _config;
        private LogCaptureService _logCapture;
        private InputBreadcrumbRecorder _inputRecorder;
        private PerformanceSampler _perfSampler;
        private BugReporterOverlay _overlay;

        private bool _isCapturingScreenshot;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public static BugReporter Instance => _instance;
        public static bool IsOpen => _instance != null && _instance._overlay != null && _instance._overlay.IsVisible;
        public static BugReporterConfig ActiveConfig => _instance != null ? _instance._config : null;

        public static event Action<BugReportData> OnReportCaptured;
        public static event Action<List<TransportResult>> OnReportSubmitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (_instance != null)
                return;

            BugReporterConfig config = Resources.Load<BugReporterConfig>("BugReporterConfig");
            if (config != null && !config.autoInitializeOnPlay)
                return;

            Initialize(config);
        }

        public static BugReporter Initialize(BugReporterConfig config = null)
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("[BugReporter]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BugReporter>();
            _instance.Setup(config);
            return _instance;
        }

        private void Setup(BugReporterConfig config)
        {
            _config = config != null ? config : ScriptableObject.CreateInstance<BugReporterConfig>();
            _logCapture = new LogCaptureService(_config.logBufferCapacity);
            _logCapture.Start();

            _inputRecorder = new InputBreadcrumbRecorder(_config.inputBufferCapacity);
            _perfSampler = new PerformanceSampler(_config.performanceSampleWindow);
            _overlay = new BugReporterOverlay(this);
        }

        public static void RegisterContextProvider(IBugContextProvider provider)
        {
            if (provider != null && !ContextProviders.Contains(provider))
                ContextProviders.Add(provider);
        }

        public static void UnregisterContextProvider(IBugContextProvider provider)
        {
            ContextProviders.Remove(provider);
        }

        public static void RegisterTransport(IBugReportTransport transport)
        {
            if (transport != null && !CustomTransports.Contains(transport))
                CustomTransports.Add(transport);
        }

        public static void UnregisterTransport(IBugReportTransport transport)
        {
            CustomTransports.Remove(transport);
        }

        public static void RecordInput(string device, string control, string action, string value = "")
        {
            _instance?._inputRecorder?.Record(device, control, action, value);
        }

        public static void Open()
        {
            if (_instance == null)
                Initialize();

            _instance.OpenInternal();
        }

        public static void Close()
        {
            _instance?.CloseInternal();
        }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        internal void OpenInternal()
        {
            if (_overlay == null || _overlay.IsVisible || _isCapturingScreenshot)
                return;

            StartCoroutine(CaptureScreenshotThenOpenCoroutine());
        }

        internal void CloseInternal()
        {
            if (_overlay == null || !_overlay.IsVisible)
                return;

            _overlay.Hide();

            if (_config.pauseGameWhileOpen)
                Time.timeScale = _previousTimeScale;

            if (_config.unlockCursorWhileOpen)
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
            }
        }

        private IEnumerator CaptureScreenshotThenOpenCoroutine()
        {
            _isCapturingScreenshot = true;
            yield return new WaitForEndOfFrame();

            byte[] pngBytes = null;
            Texture2D previewTex = null;
            try
            {
                previewTex = ScreenCapture.CaptureScreenshotAsTexture();
                if (previewTex != null)
                    pngBytes = previewTex.EncodeToPNG();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BugReporter] Screenshot capture failed: {ex.Message}");
            }

            if (_config.pauseGameWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (_config.unlockCursorWhileOpen)
            {
                _previousCursorLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            _overlay.Show(previewTex, pngBytes);
            _isCapturingScreenshot = false;
        }

        public BugReportData BuildReport(
            string title,
            string description,
            BugSeverity severity,
            BugCategory category,
            byte[] screenshotPng,
            bool includeLogs = true,
            bool includeInputs = true,
            bool includeScene = true)
        {
            var report = new BugReportData
            {
                reportId = Guid.NewGuid().ToString("N").Substring(0, 8),
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                title = string.IsNullOrWhiteSpace(title) ? "Untitled Bug Report" : title.Trim(),
                description = description ?? string.Empty,
                severity = severity,
                category = category,
                device = DeviceSnapshot.CaptureCurrent(),
                performance = _perfSampler?.CaptureSnapshot(),
                scene = includeScene ? SceneSnapshotCollector.Capture(_config.maxSceneRootObjects) : null,
                logs = includeLogs && _logCapture != null ? _logCapture.GetSnapshot() : new List<LogEntry>(),
                inputs = includeInputs && _inputRecorder != null ? _inputRecorder.GetSnapshot() : new List<InputBreadcrumb>(),
                customContext = CollectCustomContext(),
                screenshotPng = screenshotPng
            };

            OnReportCaptured?.Invoke(report);
            return report;
        }

        public void SubmitReport(BugReportData report, Action<List<TransportResult>> onComplete = null)
        {
            StartCoroutine(SubmitReportCoroutine(report, onComplete));
        }

        private IEnumerator SubmitReportCoroutine(BugReportData report, Action<List<TransportResult>> onComplete)
        {
            byte[] zipBytes = BugReportBundle.CreateZipBytes(report);
            List<IBugReportTransport> transports = CollectActiveTransports();
            var results = new List<TransportResult>(transports.Count);

            foreach (IBugReportTransport transport in transports)
            {
                if (transport == null || !transport.IsConfigured)
                    continue;

                TransportResult result = null;
                yield return transport.SendAsync(report, zipBytes, r => result = r);
                if (result != null)
                    results.Add(result);
            }

            OnReportSubmitted?.Invoke(results);
            onComplete?.Invoke(results);
        }

        private List<IBugReportTransport> CollectActiveTransports()
        {
            var list = new List<IBugReportTransport>();

            if (_config.enableLocalZipExport)
                list.Add(new LocalZipTransport(_config.GetEffectiveOutputDirectory()));

            if (_config.enableWebhook)
            {
                list.Add(new WebhookTransport(
                    _config.webhookUrl,
                    _config.webhookAttachZip,
                    BugReporterConfig.ReadEnvSecret(_config.webhookBearerTokenEnvVar)));
            }

            if (_config.enableGitHubIssues)
            {
                list.Add(new GitHubIssuesTransport(
                    _config.githubOwner,
                    _config.githubRepo,
                    BugReporterConfig.ReadEnvSecret(_config.githubTokenEnvVar)));
            }

            if (_config.enableTrello)
            {
                list.Add(new TrelloCardTransport(
                    BugReporterConfig.ReadEnvSecret(_config.trelloApiKeyEnvVar),
                    BugReporterConfig.ReadEnvSecret(_config.trelloTokenEnvVar),
                    _config.trelloListId));
            }

            foreach (IBugReportTransport custom in CustomTransports)
                list.Add(custom);

            return list;
        }

        private static List<ContextSection> CollectCustomContext()
        {
            var sections = new List<ContextSection>();
            foreach (IBugContextProvider provider in ContextProviders)
            {
                if (provider == null)
                    continue;

                try
                {
                    var dict = new Dictionary<string, string>();
                    provider.PopulateContext(dict);
                    var section = new ContextSection
                    {
                        sectionName = string.IsNullOrWhiteSpace(provider.SectionName) ? provider.GetType().Name : provider.SectionName
                    };
                    foreach (KeyValuePair<string, string> kv in dict)
                        section.entries.Add(new KeyValueEntry(kv.Key, kv.Value));
                    sections.Add(section);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BugReporter] Context provider '{provider.GetType().Name}' threw: {ex.Message}");
                }
            }
            return sections;
        }

        private void Update()
        {
            _perfSampler?.SampleFrame(Time.unscaledDeltaTime);

#if !BUG_REPORTER_INPUT_SYSTEM
            if (_config != null && _config.toggleHotkey != KeyCode.None && Input.GetKeyDown(_config.toggleHotkey))
            {
                Toggle();
            }

            if (!IsOpen)
            {
                _inputRecorder?.PollLegacyInput();
            }
#endif
        }

        private void OnGUI()
        {
            _overlay?.OnGUI();
        }

        private void OnDestroy()
        {
            _logCapture?.Dispose();
            _perfSampler?.Dispose();
            _overlay?.Dispose();
            if (_instance == this)
                _instance = null;
        }
    }
}

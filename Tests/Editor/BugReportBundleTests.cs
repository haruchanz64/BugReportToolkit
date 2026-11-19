using System.Collections.Generic;
using System.IO;
using BugReporter.Core;
using BugReporter.Export;
using NUnit.Framework;

namespace BugReporter.Tests
{
    public sealed class BugReportBundleTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"bug_reporter_tests_{System.Guid.NewGuid():N}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void ZipBundle_RoundTripsMetadataLogsInputsContextAndScreenshot()
        {
            var original = new BugReportData
            {
                reportId = "abc12345",
                createdAtUtc = "2026-11-19T02:00:00Z",
                title = "Player clips through floor",
                description = "Jump near the corner of Room 2.",
                severity = BugSeverity.High,
                category = BugCategory.Gameplay,
                device = DeviceSnapshot.CaptureCurrent(),
                performance = new PerformanceSnapshot { currentFps = 60f, averageFps = 58.5f, minFps = 42f, frameTimeMs = 16.6f },
                scene = new SceneSnapshot { activeSceneName = "Level_02", mainCameraPosition = "(1, 2, 3)" },
                logs = new List<LogEntry>
                {
                    new LogEntry { timestampUtc = "2026-11-19T02:00:00Z", elapsedSeconds = 12.4f, logType = "Error", message = "NullReferenceException", stackTrace = "Player.Move()" }
                },
                inputs = new List<InputBreadcrumb>
                {
                    new InputBreadcrumb { timestampUtc = "2026-11-19T02:00:00Z", elapsedSeconds = 12.1f, device = "Keyboard", control = "Space", action = "Pressed", value = "1" }
                },
                customContext = new List<ContextSection>
                {
                    new ContextSection
                    {
                        sectionName = "PlayerState",
                        entries = new List<KeyValueEntry> { new KeyValueEntry("HP", "75/100") }
                    }
                },
                screenshotPng = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 } // PNG magic header
            };

            string zipPath = BugReportBundle.SaveZipToFile(original, _tempDir);
            Assert.IsTrue(File.Exists(zipPath));

            BugReportData loaded = BugReportBundle.LoadFromZipFile(zipPath);
            Assert.AreEqual(original.reportId, loaded.reportId);
            Assert.AreEqual(original.title, loaded.title);
            Assert.AreEqual(BugSeverity.High, loaded.severity);
            Assert.AreEqual(1, loaded.logs.Count);
            Assert.AreEqual("NullReferenceException", loaded.logs[0].message);
            Assert.AreEqual(1, loaded.inputs.Count);
            Assert.AreEqual("Space", loaded.inputs[0].control);
            Assert.AreEqual(1, loaded.customContext.Count);
            Assert.AreEqual("75/100", loaded.customContext[0].entries[0].value);
            CollectionAssert.AreEqual(original.screenshotPng, loaded.screenshotPng);
        }

        [Test]
        public void MarkdownSummary_ContainsKeySections()
        {
            var report = new BugReportData
            {
                reportId = "deadbeef",
                title = "Audio crackle",
                severity = BugSeverity.Medium,
                category = BugCategory.Audio,
                description = "Occurs when opening inventory."
            };

            string md = BugReportBundle.BuildMarkdownSummary(report);
            StringAssert.Contains("[Medium] Audio crackle", md);
            StringAssert.Contains("deadbeef", md);
            StringAssert.Contains("Occurs when opening inventory.", md);
        }
    }
}

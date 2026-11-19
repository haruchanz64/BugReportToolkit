using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using BugReporter.Core;
using UnityEngine;

namespace BugReporter.Export
{
    /// <summary>
    /// Packs and unpacks BugReportData into a portable, zero-dependency .zip bundle
    /// containing report.json, console.log, inputs.json, scene.json, context.json, and screenshot.png.
    /// </summary>
    public static class BugReportBundle
    {
        public static byte[] CreateZipBytes(BugReportData report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddTextEntry(zip, "report.json", JsonUtility.ToJson(report, prettyPrint: true));
                AddTextEntry(zip, "console.log", FormatConsoleLog(report.logs));
                AddTextEntry(zip, "inputs.json", JsonUtility.ToJson(new InputListWrapper { inputs = report.inputs ?? new List<InputBreadcrumb>() }, prettyPrint: true));

                if (report.scene != null)
                    AddTextEntry(zip, "scene.json", JsonUtility.ToJson(report.scene, prettyPrint: true));

                if (report.customContext != null && report.customContext.Count > 0)
                    AddTextEntry(zip, "context.json", JsonUtility.ToJson(new ContextListWrapper { sections = report.customContext }, prettyPrint: true));

                if (report.screenshotPng != null && report.screenshotPng.Length > 0)
                    AddBinaryEntry(zip, "screenshot.png", report.screenshotPng);
            }

            return ms.ToArray();
        }

        public static string SaveZipToFile(BugReportData report, string directoryPath, byte[] prebuiltZipBytes = null)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                directoryPath = Path.Combine(Application.persistentDataPath, "BugReports");

            Directory.CreateDirectory(directoryPath);
            string safeId = SanitizeFileSegment(string.IsNullOrWhiteSpace(report.reportId) ? Guid.NewGuid().ToString("N").Substring(0, 8) : report.reportId);
            string safeTitle = SanitizeFileSegment(string.IsNullOrWhiteSpace(report.title) ? "BugReport" : report.title);
            if (safeTitle.Length > 32)
                safeTitle = safeTitle.Substring(0, 32);

            string fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeTitle}_{safeId}.zip";
            string fullPath = Path.Combine(directoryPath, fileName);

            byte[] bytes = prebuiltZipBytes ?? CreateZipBytes(report);
            File.WriteAllBytes(fullPath, bytes);
            return fullPath;
        }

        public static BugReportData LoadFromZipFile(string zipFilePath)
        {
            if (string.IsNullOrEmpty(zipFilePath) || !File.Exists(zipFilePath))
                throw new FileNotFoundException($"Bug report bundle not found: {zipFilePath}", zipFilePath);

            return LoadFromZipBytes(File.ReadAllBytes(zipFilePath));
        }

        public static BugReportData LoadFromZipBytes(byte[] zipBytes)
        {
            if (zipBytes == null || zipBytes.Length == 0)
                throw new ArgumentException("Zip bytes cannot be null or empty.", nameof(zipBytes));

            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            string reportJson = ReadTextEntry(zip, "report.json");
            if (string.IsNullOrEmpty(reportJson))
                throw new InvalidDataException("Invalid bug report bundle: missing report.json.");

            BugReportData report = JsonUtility.FromJson<BugReportData>(reportJson);
            report.screenshotPng = ReadBinaryEntry(zip, "screenshot.png");

            return report;
        }

        public static string BuildMarkdownSummary(BugReportData report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## [{report.severity}] {report.title}");
            sb.AppendLine($"- **Report ID:** `{report.reportId}`");
            sb.AppendLine($"- **Category:** {report.category}");
            sb.AppendLine($"- **Created (UTC):** {report.createdAtUtc}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(report.description))
            {
                sb.AppendLine("### Description");
                sb.AppendLine(report.description.Trim());
                sb.AppendLine();
            }

            if (report.device != null)
            {
                sb.AppendLine("### Environment");
                sb.AppendLine($"- **App:** {report.device.productName} v{report.device.applicationVersion} (Unity {report.device.unityVersion})");
                sb.AppendLine($"- **Platform:** {report.device.platform} | {report.device.operatingSystem}");
                sb.AppendLine($"- **Device:** {report.device.deviceModel} | {report.device.processorType} ({report.device.systemMemoryMb} MB RAM)");
                sb.AppendLine($"- **GPU:** {report.device.graphicsDeviceName} ({report.device.graphicsMemoryMb} MB VRAM) | {report.device.screenResolution}");
                sb.AppendLine();
            }

            if (report.performance != null)
            {
                sb.AppendLine("### Performance");
                sb.AppendLine($"- **FPS:** {report.performance.currentFps} (avg {report.performance.averageFps}, min {report.performance.minFps}) | Frame: {report.performance.frameTimeMs} ms");
                sb.AppendLine($"- **Memory:** {report.performance.totalAllocatedMemoryMb} MB total / {report.performance.monoUsedMemoryMb} MB Mono");
                sb.AppendLine();
            }

            if (report.scene != null)
            {
                sb.AppendLine("### Scene");
                sb.AppendLine($"- **Active Scene:** `{report.scene.activeSceneName}` | Camera: `{report.scene.mainCameraPosition}`");
                sb.AppendLine();
            }

            if (report.customContext != null && report.customContext.Count > 0)
            {
                sb.AppendLine("### Custom Context");
                foreach (ContextSection section in report.customContext)
                {
                    sb.AppendLine($"**{section.sectionName}**");
                    foreach (KeyValueEntry kv in section.entries)
                        sb.AppendLine($"- `{kv.key}`: {kv.value}");
                }
                sb.AppendLine();
            }

            if (report.logs != null && report.logs.Count > 0)
            {
                int errorCount = 0;
                foreach (LogEntry l in report.logs)
                    if (l.logType == "Error" || l.logType == "Exception" || l.logType == "Assert")
                        errorCount++;

                sb.AppendLine($"### Diagnostics");
                sb.AppendLine($"- **Captured Logs:** {report.logs.Count} ({errorCount} errors/exceptions)");
                sb.AppendLine($"- **Recorded Inputs:** {report.inputs?.Count ?? 0}");
            }

            return sb.ToString();
        }

        private static string FormatConsoleLog(List<LogEntry> logs)
        {
            var sb = new StringBuilder();
            if (logs == null)
                return string.Empty;

            foreach (LogEntry entry in logs)
            {
                sb.AppendLine($"[{entry.timestampUtc}] [{entry.elapsedSeconds:F2}s] [{entry.logType}] {entry.message}");
                if (!string.IsNullOrWhiteSpace(entry.stackTrace))
                    sb.AppendLine(entry.stackTrace.TrimEnd());
            }
            return sb.ToString();
        }

        private static void AddTextEntry(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content ?? string.Empty);
        }

        private static void AddBinaryEntry(ZipArchive zip, string name, byte[] data)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression);
            using Stream s = entry.Open();
            s.Write(data, 0, data.Length);
        }

        private static string ReadTextEntry(ZipArchive zip, string name)
        {
            ZipArchiveEntry entry = zip.GetEntry(name);
            if (entry == null)
                return null;
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static byte[] ReadBinaryEntry(ZipArchive zip, string name)
        {
            ZipArchiveEntry entry = zip.GetEntry(name);
            if (entry == null)
                return null;
            using var ms = new MemoryStream();
            using Stream s = entry.Open();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        private static string SanitizeFileSegment(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (char ch in value.Trim())
            {
                bool bad = Array.IndexOf(invalid, ch) >= 0 || char.IsWhiteSpace(ch);
                sb.Append(bad ? '_' : ch);
            }
            string res = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(res) ? "Report" : res;
        }
    }
}

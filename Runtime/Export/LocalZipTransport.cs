using System;
using System.Collections;
using System.IO;
using BugReporter.Core;
using UnityEngine;

namespace BugReporter.Export
{
    public sealed class LocalZipTransport : IBugReportTransport
    {
        private readonly string _outputDirectory;

        public string Name => "Local Zip";
        public bool IsConfigured => true;

        public LocalZipTransport(string outputDirectory = null)
        {
            _outputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Application.persistentDataPath, "BugReports")
                : outputDirectory;
        }

        public IEnumerator SendAsync(BugReportData report, byte[] zipBytes, Action<TransportResult> onComplete)
        {
            try
            {
                string savedPath = BugReportBundle.SaveZipToFile(report, _outputDirectory, zipBytes);
                onComplete?.Invoke(new TransportResult(Name, true, $"Saved to {savedPath}"));
            }
            catch (Exception ex)
            {
                onComplete?.Invoke(new TransportResult(Name, false, $"Failed to write zip: {ex.Message}"));
            }

            yield break;
        }
    }
}

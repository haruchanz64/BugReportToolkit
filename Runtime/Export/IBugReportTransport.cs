using System;
using System.Collections;
using BugReporter.Core;

namespace BugReporter.Export
{
    public sealed class TransportResult
    {
        public string TransportName { get; }
        public bool Success { get; }
        public string Message { get; }

        public TransportResult(string transportName, bool success, string message)
        {
            TransportName = transportName;
            Success = success;
            Message = message;
        }
    }

    /// <summary>
    /// Implement to add custom destinations for bug reports (e.g. Supabase Edge Function,
    /// Jira, Linear, S3, email relay, etc.).
    /// </summary>
    public interface IBugReportTransport
    {
        string Name { get; }
        bool IsConfigured { get; }
        IEnumerator SendAsync(BugReportData report, byte[] zipBytes, Action<TransportResult> onComplete);
    }
}

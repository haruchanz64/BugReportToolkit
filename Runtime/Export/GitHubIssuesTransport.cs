using System;
using System.Collections;
using System.Text;
using BugReporter.Core;
using UnityEngine.Networking;

namespace BugReporter.Export
{
    public sealed class GitHubIssuesTransport : IBugReportTransport
    {
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _token;

        public string Name => "GitHub Issues";
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_owner) &&
            !string.IsNullOrWhiteSpace(_repo) &&
            !string.IsNullOrWhiteSpace(_token);

        public GitHubIssuesTransport(string owner, string repo, string token)
        {
            _owner = owner;
            _repo = repo;
            _token = token;
        }

        public IEnumerator SendAsync(BugReportData report, byte[] zipBytes, Action<TransportResult> onComplete)
        {
            if (!IsConfigured)
            {
                onComplete?.Invoke(new TransportResult(Name, false, "GitHub owner, repo, or token is missing."));
                yield break;
            }

            string url = $"https://api.github.com/repos/{_owner}/{_repo}/issues";
            string title = $"[{report.severity}][{report.category}] {report.title}";
            string body = BugReportBundle.BuildMarkdownSummary(report);
            string json = $"{{\"title\":\"{WebhookTransport.EscapeJson(title)}\",\"body\":\"{WebhookTransport.EscapeJson(body)}\",\"labels\":[\"bug\",\"{report.severity.ToString().ToLowerInvariant()}\"]}}";

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("Authorization", $"Bearer {_token}");
            request.SetRequestHeader("User-Agent", "Unity-BugReporter-Plugin");

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            string msg = ok
                ? $"Issue created (HTTP {request.responseCode})"
                : $"HTTP {request.responseCode}: {request.error}";
            onComplete?.Invoke(new TransportResult(Name, ok, msg));
        }
    }
}

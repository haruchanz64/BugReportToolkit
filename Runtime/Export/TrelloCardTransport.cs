using System;
using System.Collections;
using BugReporter.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace BugReporter.Export
{
    public sealed class TrelloCardTransport : IBugReportTransport
    {
        private readonly string _apiKey;
        private readonly string _token;
        private readonly string _listId;

        public string Name => "Trello";
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_apiKey) &&
            !string.IsNullOrWhiteSpace(_token) &&
            !string.IsNullOrWhiteSpace(_listId);

        public TrelloCardTransport(string apiKey, string token, string listId)
        {
            _apiKey = apiKey;
            _token = token;
            _listId = listId;
        }

        public IEnumerator SendAsync(BugReportData report, byte[] zipBytes, Action<TransportResult> onComplete)
        {
            if (!IsConfigured)
            {
                onComplete?.Invoke(new TransportResult(Name, false, "Trello API key, token, or listId is missing."));
                yield break;
            }

            string url = $"https://api.trello.com/1/cards?key={UnityWebRequest.EscapeURL(_apiKey)}&token={UnityWebRequest.EscapeURL(_token)}";
            var form = new WWWForm();
            form.AddField("idList", _listId);
            form.AddField("name", $"[{report.severity}][{report.category}] {report.title}");
            form.AddField("desc", BugReportBundle.BuildMarkdownSummary(report));

            using UnityWebRequest request = UnityWebRequest.Post(url, form);
            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            string msg = ok
                ? $"Trello card created (HTTP {request.responseCode})"
                : $"HTTP {request.responseCode}: {request.error}";
            onComplete?.Invoke(new TransportResult(Name, ok, msg));
        }
    }
}

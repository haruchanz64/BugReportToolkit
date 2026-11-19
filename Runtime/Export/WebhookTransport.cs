using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BugReporter.Core;
using UnityEngine.Networking;

namespace BugReporter.Export
{
    /// <summary>
    /// Posts bug reports to a Discord webhook or generic multipart/JSON HTTP endpoint
    /// (such as a Supabase Edge Function or Cloudflare Worker).
    /// </summary>
    public sealed class WebhookTransport : IBugReportTransport
    {
        private readonly string _webhookUrl;
        private readonly bool _attachZipBundle;
        private readonly string _bearerToken;

        public string Name => "Webhook";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_webhookUrl);

        public WebhookTransport(string webhookUrl, bool attachZipBundle = true, string bearerToken = null)
        {
            _webhookUrl = webhookUrl;
            _attachZipBundle = attachZipBundle;
            _bearerToken = bearerToken;
        }

        public IEnumerator SendAsync(BugReportData report, byte[] zipBytes, Action<TransportResult> onComplete)
        {
            if (!IsConfigured)
            {
                onComplete?.Invoke(new TransportResult(Name, false, "Webhook URL is not configured."));
                yield break;
            }

            string summary = BugReportBundle.BuildMarkdownSummary(report);
            if (summary.Length > 1900)
                summary = summary.Substring(0, 1900) + "\n...(truncated; see attached .zip)";

            string payloadJson = $"{{\"content\":\"{EscapeJson(summary)}\"}}";

            UnityWebRequest request;
            if (_attachZipBundle && zipBytes != null && zipBytes.Length > 0)
            {
                var form = new List<IMultipartFormSection>
                {
                    new MultipartFormDataSection("payload_json", payloadJson, "application/json"),
                    new MultipartFormFileSection("file", zipBytes, $"bug_{report.reportId}.zip", "application/zip")
                };
                request = UnityWebRequest.Post(_webhookUrl, form);
            }
            else
            {
                request = new UnityWebRequest(_webhookUrl, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payloadJson));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (!string.IsNullOrWhiteSpace(_bearerToken))
                request.SetRequestHeader("Authorization", $"Bearer {_bearerToken}");

            using (request)
            {
                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                string msg = ok
                    ? $"Uploaded (HTTP {request.responseCode})"
                    : $"HTTP {request.responseCode}: {request.error}";
                onComplete?.Invoke(new TransportResult(Name, ok, msg));
            }
        }

        internal static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}

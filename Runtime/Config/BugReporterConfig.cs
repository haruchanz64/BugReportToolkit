using System;
using System.IO;
using UnityEngine;

namespace BugReporter.Config
{
    [CreateAssetMenu(fileName = "BugReporterConfig", menuName = "Bug Reporter/Config")]
    public sealed class BugReporterConfig : ScriptableObject
    {
        [Header("Activation")]
        public bool autoInitializeOnPlay = true;
        public KeyCode toggleHotkey = KeyCode.F9;
        public bool pauseGameWhileOpen = true;
        public bool unlockCursorWhileOpen = true;

        [Header("Repro Capture Buffers")]
        [Min(16)] public int logBufferCapacity = 500;
        [Min(16)] public int inputBufferCapacity = 250;
        [Min(10)] public int performanceSampleWindow = 120;
        [Min(8)] public int maxSceneRootObjects = 64;

        [Header("Local Zip Export")]
        public bool enableLocalZipExport = true;
        [Tooltip("Leave blank to use Application.persistentDataPath/BugReports")]
        public string customOutputDirectory = string.Empty;

        [Header("Webhook Transport (Discord / Generic / Supabase)")]
        public bool enableWebhook = false;
        public string webhookUrl = string.Empty;
        public bool webhookAttachZip = true;
        [Tooltip("Optional environment variable name holding a Bearer token (avoids committing secrets).")]
        public string webhookBearerTokenEnvVar = string.Empty;

        [Header("GitHub Issues Transport")]
        public bool enableGitHubIssues = false;
        public string githubOwner = string.Empty;
        public string githubRepo = string.Empty;
        [Tooltip("Environment variable name holding a GitHub Personal Access Token.")]
        public string githubTokenEnvVar = "GITHUB_BUG_REPORTER_TOKEN";

        [Header("Trello Transport")]
        public bool enableTrello = false;
        public string trelloListId = string.Empty;
        [Tooltip("Environment variable names for Trello API key & token.")]
        public string trelloApiKeyEnvVar = "TRELLO_API_KEY";
        public string trelloTokenEnvVar = "TRELLO_TOKEN";

        public string GetEffectiveOutputDirectory()
        {
            return string.IsNullOrWhiteSpace(customOutputDirectory)
                ? Path.Combine(Application.persistentDataPath, "BugReports")
                : customOutputDirectory;
        }

        public static string ReadEnvSecret(string envVarName)
        {
            if (string.IsNullOrWhiteSpace(envVarName))
                return string.Empty;
            return Environment.GetEnvironmentVariable(envVarName.Trim()) ?? string.Empty;
        }
    }
}

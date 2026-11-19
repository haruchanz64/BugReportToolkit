using System.IO;
using BugReporter.Config;
using UnityEditor;
using UnityEngine;

namespace BugReporter.Editor
{
    [CustomEditor(typeof(BugReporterConfig))]
    public sealed class BugReporterConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10f);
            var cfg = (BugReporterConfig)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Reports Folder", GUILayout.Height(26f)))
                {
                    string dir = cfg.GetEffectiveOutputDirectory();
                    Directory.CreateDirectory(dir);
                    EditorUtility.RevealInFinder(dir);
                }

                if (GUILayout.Button("Open Report Viewer", GUILayout.Height(26f)))
                {
                    BugReportViewerWindow.OpenWindow();
                }
            }
        }
    }
}

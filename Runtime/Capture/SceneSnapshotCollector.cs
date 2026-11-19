using System.Text;
using BugReporter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugReporter.Capture
{
    public static class SceneSnapshotCollector
    {
        public static SceneSnapshot Capture(int maxRootObjects = 64)
        {
            var snapshot = new SceneSnapshot();
            Scene active = SceneManager.GetActiveScene();
            snapshot.activeSceneName = active.IsValid() ? active.name : "(None)";

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.IsValid())
                    snapshot.loadedScenes.Add(s.name);
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                snapshot.mainCameraPosition = mainCam.transform.position.ToString("F2");
                snapshot.mainCameraRotationEuler = mainCam.transform.eulerAngles.ToString("F1");
            }

            if (active.IsValid())
            {
                GameObject[] roots = active.GetRootGameObjects();
                int limit = Mathf.Min(roots.Length, maxRootObjects);
                var sb = new StringBuilder();

                for (int i = 0; i < limit; i++)
                {
                    GameObject go = roots[i];
                    if (go == null)
                        continue;

                    sb.Clear();
                    Component[] comps = go.GetComponents<Component>();
                    for (int c = 0; c < comps.Length; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        sb.Append(comps[c] != null ? comps[c].GetType().Name : "MissingScript");
                    }

                    snapshot.rootObjects.Add(new SceneObjectSummary
                    {
                        name = go.name,
                        activeInHierarchy = go.activeInHierarchy,
                        childCount = go.transform.childCount,
                        position = go.transform.position.ToString("F2"),
                        components = sb.ToString()
                    });
                }
            }

            return snapshot;
        }
    }
}

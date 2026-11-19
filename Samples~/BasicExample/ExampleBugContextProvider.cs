using System.Collections.Generic;
using BugReporter.Core;
using UnityEngine;

namespace BugReporter.Samples
{
    /// <summary>
    /// Drop onto a Player or GameManager to automatically attach custom gameplay state
    /// to every captured bug report.
    /// </summary>
    public sealed class ExampleBugContextProvider : MonoBehaviour, IBugContextProvider
    {
        [SerializeField] private int playerHealth = 100;
        [SerializeField] private string currentQuest = "Tutorial_01";

        public string SectionName => "Gameplay State";

        private void OnEnable() => Core.BugReporter.RegisterContextProvider(this);
        private void OnDisable() => Core.BugReporter.UnregisterContextProvider(this);

        public void PopulateContext(IDictionary<string, string> target)
        {
            target["Player Health"] = playerHealth.ToString();
            target["Current Quest"] = currentQuest;
            target["Player Position"] = transform.position.ToString("F2");
        }
    }
}

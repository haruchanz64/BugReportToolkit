using System.Collections.Generic;

namespace BugReporter.Core
{
    /// <summary>
    /// Implement and register via BugReporter.RegisterContextProvider(...) to attach
    /// game-specific state (player health, inventory, level seed, quest state, etc.)
    /// to every captured bug report.
    /// </summary>
    public interface IBugContextProvider
    {
        string SectionName { get; }
        void PopulateContext(IDictionary<string, string> target);
    }
}

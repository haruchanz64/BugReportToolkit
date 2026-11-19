# In-Game Bug Reporter & Repro Capture (Unity Plugin)

A zero-dependency Unity runtime bug reporting and repro capture package for QA, playtesters, and developers. Press a single hotkey (default `F9`) or call `BugReporter.Open()` to capture a clean screenshot, console logs, input breadcrumbs, performance metrics, device specs, scene hierarchy, and custom game state into a portable `.zip` bundle—or send it straight to Discord, GitHub Issues, Trello, or a custom webhook.

## Features

- **Zero External Dependencies:** Pure C# using built-in `System.IO.Compression`, `UnityWebRequest`, and `Unity.Profiling`. Automatically integrates with `com.unity.inputsystem` when installed (via `versionDefines`) and falls back to Legacy Input otherwise.
- **Always-On Ring-Buffer Capture:**
  - **Console Logs:** Thread-safe `LogCaptureService` storing the last N logs/warnings/errors + stack traces.
  - **Input Breadcrumbs:** Records recent player inputs with timestamps so QA can reproduce edge-case bugs.
  - **Performance & Device Snapshot:** Current/Avg/Min FPS, frame time, GC allocation, Mono/total memory, OS/CPU/GPU/resolution/quality level.
  - **Scene Snapshot:** Active & loaded scenes, main camera transform, and root GameObject summary.
  - **Custom Game Context:** Implement `IBugContextProvider` to attach player HP, inventory, level seed, or save hashes.
- **Zero-Setup Runtime Overlay:** Captures a PNG screenshot *before* opening the overlay so the UI never covers the bug, with optional pause & cursor unlock.
- **Portable `.zip` Bundles & Transports (`IBugReportTransport`):**
  - `LocalZipTransport` (saves `report.json`, `console.log`, `inputs.json`, `scene.json`, `context.json`, and `screenshot.png`)
  - `WebhookTransport` (Discord / generic JSON / Supabase Edge Function)
  - `GitHubIssuesTransport` (REST API issue creation)
  - `TrelloCardTransport` (REST API card creation)
- **Editor Report Viewer (`Tools > Bug Reporter > Report Viewer`):** Inspect any `.zip` bug bundle inside Unity with screenshot preview, filterable logs, scene snapshot, input timeline, and one-click Markdown copy.

## Installation (Unity Package Manager)

1. Open **Window > Package Manager** in Unity.
2. Click **+** (top-left) > **Add package from git URL...**
3. Enter:
   ```
   https://github.com/haruchanz64/bug-reporter.git
   ```

## Quick Start

1. Press **F9** during Play Mode (or call `BugReporter.Core.BugReporter.Open()` from any UI button or debug console).
2. *(Optional)* Create a `BugReporterConfig` asset via **Create > Bug Reporter > Config** and place it in a `Resources/` folder named `BugReporterConfig.asset` to customize hotkeys, buffer sizes, and webhook/GitHub/Trello transports.
3. Open **Tools > Bug Reporter > Report Viewer** and click **Load Latest** to inspect captured `.zip` bundles inside Unity.

## Attaching Custom Game State

```csharp
using System.Collections.Generic;
using BugReporter.Core;
using UnityEngine;

public class PlayerContextProvider : MonoBehaviour, IBugContextProvider
{
    public string SectionName => "Player";

    private void OnEnable() => BugReporter.Core.BugReporter.RegisterContextProvider(this);
    private void OnDisable() => BugReporter.Core.BugReporter.UnregisterContextProvider(this);

    public void PopulateContext(IDictionary<string, string> target)
    {
        target["Position"] = transform.position.ToString("F2");
        target["Scene"] = gameObject.scene.name;
    }
}
```

## License

MIT — see [LICENSE.md](LICENSE.md).

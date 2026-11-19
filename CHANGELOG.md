# Changelog

All notable changes to this package will be documented in this file.

## [1.0.0] - 2026-11-19
### Added
- Thread-safe generic `RingBuffer<T>` for zero-allocation-after-init log and input breadcrumb recording.
- Always-on `LogCaptureService`, `InputBreadcrumbRecorder` (with optional New Input System bridge via `versionDefines`), `PerformanceSampler`, and `SceneSnapshotCollector`.
- Extensible `IBugContextProvider` interface for attaching custom game state to bug reports.
- Pure C# `.zip` bug bundle writer & reader (`BugReportBundle`) containing `report.json`, `console.log`, `inputs.json`, `scene.json`, `context.json`, and `screenshot.png`.
- Extensible `IBugReportTransport` system with built-in `LocalZipTransport`, `WebhookTransport` (Discord / generic JSON), `GitHubIssuesTransport`, and `TrelloCardTransport`.
- Zero-setup in-game IMGUI overlay (`BugReporterOverlay`) and `BugReporter` static facade.
- Unity Editor **Bug Report Viewer** window (`Tools/Bug Reporter/Report Viewer`) to inspect `.zip` bundles, screenshots, logs, scene snapshots, and input trails.
- EditMode unit tests and sample `IBugContextProvider`.

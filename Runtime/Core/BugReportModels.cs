using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugReporter.Core
{
    public enum BugSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum BugCategory
    {
        Gameplay = 0,
        UI = 1,
        Audio = 2,
        Visual = 3,
        Performance = 4,
        Crash = 5,
        Other = 6
    }

    [Serializable]
    public sealed class LogEntry
    {
        public string timestampUtc;
        public float elapsedSeconds;
        public string logType;
        public string message;
        public string stackTrace;
    }

    [Serializable]
    public sealed class InputBreadcrumb
    {
        public string timestampUtc;
        public float elapsedSeconds;
        public string device;
        public string control;
        public string action;
        public string value;
    }

    [Serializable]
    public sealed class PerformanceSnapshot
    {
        public float currentFps;
        public float averageFps;
        public float minFps;
        public float frameTimeMs;
        public long gcAllocatedBytesLastFrame;
        public float totalAllocatedMemoryMb;
        public float monoUsedMemoryMb;
    }

    [Serializable]
    public sealed class DeviceSnapshot
    {
        public string operatingSystem;
        public string deviceModel;
        public string processorType;
        public int systemMemoryMb;
        public string graphicsDeviceName;
        public int graphicsMemoryMb;
        public string screenResolution;
        public string unityVersion;
        public string applicationVersion;
        public string productName;
        public string platform;
        public string qualityLevel;

        public static DeviceSnapshot CaptureCurrent()
        {
            string quality = QualitySettings.names != null && QualitySettings.GetQualityLevel() < QualitySettings.names.Length
                ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                : QualitySettings.GetQualityLevel().ToString();

            return new DeviceSnapshot
            {
                operatingSystem = SystemInfo.operatingSystem,
                deviceModel = SystemInfo.deviceModel,
                processorType = SystemInfo.processorType,
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsMemoryMb = SystemInfo.graphicsMemorySize,
                screenResolution = $"{Screen.width}x{Screen.height}",
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version,
                productName = Application.productName,
                platform = Application.platform.ToString(),
                qualityLevel = quality
            };
        }
    }

    [Serializable]
    public sealed class SceneObjectSummary
    {
        public string name;
        public bool activeInHierarchy;
        public int childCount;
        public string position;
        public string components;
    }

    [Serializable]
    public sealed class SceneSnapshot
    {
        public string activeSceneName;
        public List<string> loadedScenes = new List<string>();
        public string mainCameraPosition;
        public string mainCameraRotationEuler;
        public List<SceneObjectSummary> rootObjects = new List<SceneObjectSummary>();
    }

    [Serializable]
    public sealed class KeyValueEntry
    {
        public string key;
        public string value;

        public KeyValueEntry() { }
        public KeyValueEntry(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }

    [Serializable]
    public sealed class ContextSection
    {
        public string sectionName;
        public List<KeyValueEntry> entries = new List<KeyValueEntry>();
    }

    [Serializable]
    public sealed class LogListWrapper
    {
        public List<LogEntry> logs = new List<LogEntry>();
    }

    [Serializable]
    public sealed class InputListWrapper
    {
        public List<InputBreadcrumb> inputs = new List<InputBreadcrumb>();
    }

    [Serializable]
    public sealed class ContextListWrapper
    {
        public List<ContextSection> sections = new List<ContextSection>();
    }

    [Serializable]
    public sealed class BugReportData
    {
        public string reportId;
        public string createdAtUtc;
        public string title;
        public string description;
        public BugSeverity severity;
        public BugCategory category;
        public DeviceSnapshot device;
        public PerformanceSnapshot performance;
        public SceneSnapshot scene;
        public List<LogEntry> logs = new List<LogEntry>();
        public List<InputBreadcrumb> inputs = new List<InputBreadcrumb>();
        public List<ContextSection> customContext = new List<ContextSection>();

        [NonSerialized]
        public byte[] screenshotPng;
    }
}

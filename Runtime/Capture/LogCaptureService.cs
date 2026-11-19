using System;
using System.Collections.Generic;
using BugReporter.Core;
using UnityEngine;

namespace BugReporter.Capture
{
    public sealed class LogCaptureService : IDisposable
    {
        private readonly RingBuffer<LogEntry> _buffer;
        private readonly DateTime _startTimeUtc;
        private bool _isListening;

        public LogCaptureService(int capacity = 500)
        {
            _buffer = new RingBuffer<LogEntry>(Math.Max(16, capacity));
            _startTimeUtc = DateTime.UtcNow;
        }

        public void Start()
        {
            if (_isListening)
                return;

            Application.logMessageReceivedThreaded += HandleLog;
            _isListening = true;
        }

        public void Stop()
        {
            if (!_isListening)
                return;

            Application.logMessageReceivedThreaded -= HandleLog;
            _isListening = false;
        }

        public void RecordManual(string message, string stackTrace = "", LogType type = LogType.Log)
        {
            HandleLog(message, stackTrace, type);
        }

        public List<LogEntry> GetSnapshot() => _buffer.Snapshot();

        public void Clear() => _buffer.Clear();

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            DateTime now = DateTime.UtcNow;
            _buffer.Add(new LogEntry
            {
                timestampUtc = now.ToString("O"),
                elapsedSeconds = (float)(now - _startTimeUtc).TotalSeconds,
                logType = type.ToString(),
                message = condition ?? string.Empty,
                stackTrace = stackTrace ?? string.Empty
            });
        }

        public void Dispose() => Stop();
    }
}

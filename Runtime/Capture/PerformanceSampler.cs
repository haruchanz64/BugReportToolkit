using System;
using BugReporter.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace BugReporter.Capture
{
    public sealed class PerformanceSampler : IDisposable
    {
        private readonly float[] _frameTimes;
        private int _index;
        private int _sampleCount;
        private float _lastDeltaTime;
        private ProfilerRecorder _gcAllocRecorder;

        public PerformanceSampler(int sampleWindow = 120)
        {
            _frameTimes = new float[Math.Max(10, sampleWindow)];
            try
            {
                _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            }
            catch
            {
                // Safe fallback if ProfilerRecorder metric is unavailable on target platform
            }
        }

        public void SampleFrame(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0.00001f)
                return;

            _lastDeltaTime = unscaledDeltaTime;
            _frameTimes[_index] = unscaledDeltaTime;
            _index = (_index + 1) % _frameTimes.Length;
            if (_sampleCount < _frameTimes.Length)
                _sampleCount++;
        }

        public PerformanceSnapshot CaptureSnapshot()
        {
            float sum = 0f;
            float maxDelta = 0f;

            for (int i = 0; i < _sampleCount; i++)
            {
                float dt = _frameTimes[i];
                sum += dt;
                if (dt > maxDelta)
                    maxDelta = dt;
            }

            float avgDelta = _sampleCount > 0 ? sum / _sampleCount : _lastDeltaTime;
            float currentFps = _lastDeltaTime > 0.00001f ? 1f / _lastDeltaTime : 0f;
            float avgFps = avgDelta > 0.00001f ? 1f / avgDelta : 0f;
            float minFps = maxDelta > 0.00001f ? 1f / maxDelta : 0f;

            long gcBytes = _gcAllocRecorder.Valid ? _gcAllocRecorder.LastValue : 0L;

            return new PerformanceSnapshot
            {
                currentFps = (float)Math.Round(currentFps, 1),
                averageFps = (float)Math.Round(avgFps, 1),
                minFps = (float)Math.Round(minFps, 1),
                frameTimeMs = (float)Math.Round(_lastDeltaTime * 1000f, 2),
                gcAllocatedBytesLastFrame = gcBytes,
                totalAllocatedMemoryMb = (float)Math.Round(Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f), 2),
                monoUsedMemoryMb = (float)Math.Round(Profiler.GetMonoUsedSizeLong() / (1024f * 1024f), 2)
            };
        }

        public void Dispose()
        {
            if (_gcAllocRecorder.Valid)
                _gcAllocRecorder.Dispose();
        }
    }
}

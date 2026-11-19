using System.Collections.Generic;
using BugReporter.Capture;
using BugReporter.Core;
using NUnit.Framework;

namespace BugReporter.Tests
{
    public sealed class RingBufferTests
    {
        [Test]
        public void RingBuffer_OverwritesOldestInOrderWhenFull()
        {
            var rb = new RingBuffer<int>(3);
            rb.Add(10);
            rb.Add(20);
            rb.Add(30);
            rb.Add(40);
            rb.Add(50);

            Assert.AreEqual(3, rb.Count);
            CollectionAssert.AreEqual(new[] { 30, 40, 50 }, rb.Snapshot());
        }

        [Test]
        public void InputBreadcrumbRecorder_PreservesChronologicalOrder()
        {
            var recorder = new InputBreadcrumbRecorder(16);
            recorder.Record("Keyboard", "W", "Pressed", "1");
            recorder.Record("Keyboard", "Space", "Pressed", "1");

            List<InputBreadcrumb> snap = recorder.GetSnapshot();
            Assert.AreEqual(2, snap.Count);
            Assert.AreEqual("W", snap[0].control);
            Assert.AreEqual("Space", snap[1].control);
        }

        [Test]
        public void PerformanceSampler_ComputesFpsMetrics()
        {
            using var sampler = new PerformanceSampler(20);
            sampler.SampleFrame(1f / 60f);
            sampler.SampleFrame(1f / 30f);

            PerformanceSnapshot snap = sampler.CaptureSnapshot();
            Assert.Greater(snap.averageFps, 35f);
            Assert.Less(snap.averageFps, 45f);
            Assert.AreEqual(30f, snap.minFps, 0.5f);
        }
    }
}

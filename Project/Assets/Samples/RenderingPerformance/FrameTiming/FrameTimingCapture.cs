using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCoreLab.Samples.RenderingPerformance.FrameTiming
{
    /// <summary>
    /// Drives Unity's FrameTimingManager through a fixed warm-up and sample
    /// window. Tick must be called once per frame after the workload update.
    /// </summary>
    public sealed class FrameTimingCapture
    {
        private const int MaximumEmptyReads = 120;

        private readonly UnityEngine.FrameTiming[] _latestTiming =
            new UnityEngine.FrameTiming[1];
        private readonly List<double> _cpuFrameSamples = new List<double>();
        private readonly List<double> _cpuMainThreadSamples =
            new List<double>();
        private readonly List<double> _cpuRenderThreadSamples =
            new List<double>();
        private readonly List<double> _gpuFrameSamples = new List<double>();
        private int _warmupFramesRemaining;
        private int _targetSampleCount;
        private int _emptyReadCount;
        private bool _captureRequested;
        private ulong _lastFrameStartTimestamp;

        public FrameTimingCaptureState State { get; private set; }

        public string Label { get; private set; } = string.Empty;

        public int WarmupFramesRemaining => _warmupFramesRemaining;

        public int CapturedFrameCount => _cpuFrameSamples.Count;

        public int TargetSampleCount => _targetSampleCount;

        public FrameTimingSummary Summary { get; private set; }

        public bool IsRunning =>
            State == FrameTimingCaptureState.WarmingUp ||
            State == FrameTimingCaptureState.Sampling;

        public void Start(string label, int warmupFrames, int sampleFrames)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "A capture label is required.",
                    nameof(label));
            }

            if (warmupFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupFrames));
            }

            if (sampleFrames <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleFrames));
            }

            ResetSamples(sampleFrames);
            Label = label;
            _warmupFramesRemaining = warmupFrames;
            State = FrameTimingManager.IsFeatureEnabled()
                ? warmupFrames > 0
                    ? FrameTimingCaptureState.WarmingUp
                    : FrameTimingCaptureState.Sampling
                : FrameTimingCaptureState.Unavailable;
        }

        public void Cancel()
        {
            if (IsRunning)
            {
                State = FrameTimingCaptureState.Idle;
            }

            _captureRequested = false;
        }

        public void Tick()
        {
            if (State == FrameTimingCaptureState.WarmingUp)
            {
                _warmupFramesRemaining--;
                if (_warmupFramesRemaining <= 0)
                {
                    State = FrameTimingCaptureState.Sampling;
                }

                return;
            }

            if (State != FrameTimingCaptureState.Sampling)
            {
                return;
            }

            if (_captureRequested)
            {
                ReadLatestTiming();
                if (CapturedFrameCount >= _targetSampleCount)
                {
                    Complete();
                    return;
                }

                if (_emptyReadCount >= MaximumEmptyReads)
                {
                    State = FrameTimingCaptureState.Unavailable;
                    _captureRequested = false;
                    return;
                }
            }

            FrameTimingManager.CaptureFrameTimings();
            _captureRequested = true;
        }

        private void ReadLatestTiming()
        {
            uint count = FrameTimingManager.GetLatestTimings(1, _latestTiming);
            if (count == 0)
            {
                _emptyReadCount++;
                return;
            }

            UnityEngine.FrameTiming timing = _latestTiming[0];
            if (timing.frameStartTimestamp != 0 &&
                timing.frameStartTimestamp == _lastFrameStartTimestamp)
            {
                _emptyReadCount++;
                return;
            }

            _lastFrameStartTimestamp = timing.frameStartTimestamp;
            _emptyReadCount = 0;
            _cpuFrameSamples.Add(timing.cpuFrameTime);
            _cpuMainThreadSamples.Add(timing.cpuMainThreadFrameTime);
            _cpuRenderThreadSamples.Add(timing.cpuRenderThreadFrameTime);
            if (timing.gpuFrameTime > 0d &&
                !double.IsNaN(timing.gpuFrameTime) &&
                !double.IsInfinity(timing.gpuFrameTime))
            {
                _gpuFrameSamples.Add(timing.gpuFrameTime);
            }
        }

        private void Complete()
        {
            Summary = new FrameTimingSummary(
                Label,
                CapturedFrameCount,
                FrameTimingStatistics.Create(_cpuFrameSamples),
                FrameTimingStatistics.Create(_cpuMainThreadSamples),
                FrameTimingStatistics.Create(_cpuRenderThreadSamples),
                FrameTimingStatistics.Create(_gpuFrameSamples));
            State = FrameTimingCaptureState.Completed;
            _captureRequested = false;
        }

        private void ResetSamples(int sampleFrames)
        {
            _cpuFrameSamples.Clear();
            _cpuMainThreadSamples.Clear();
            _cpuRenderThreadSamples.Clear();
            _gpuFrameSamples.Clear();
            _cpuFrameSamples.Capacity =
                Math.Max(_cpuFrameSamples.Capacity, sampleFrames);
            _cpuMainThreadSamples.Capacity =
                Math.Max(_cpuMainThreadSamples.Capacity, sampleFrames);
            _cpuRenderThreadSamples.Capacity =
                Math.Max(_cpuRenderThreadSamples.Capacity, sampleFrames);
            _gpuFrameSamples.Capacity =
                Math.Max(_gpuFrameSamples.Capacity, sampleFrames);
            _targetSampleCount = sampleFrames;
            _emptyReadCount = 0;
            _captureRequested = false;
            _lastFrameStartTimestamp = 0;
            Summary = default(FrameTimingSummary);
        }
    }
}

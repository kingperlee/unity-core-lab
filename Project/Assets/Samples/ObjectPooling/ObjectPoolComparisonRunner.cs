using System;
using System.Collections;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.ObjectPooling
{
    /// <summary>
    /// Runs matched Instantiate/Destroy and Rent/Return workloads. The result is
    /// descriptive evidence for one environment, not a universal performance
    /// assertion.
    /// </summary>
    public sealed class ObjectPoolComparisonRunner : MonoBehaviour
    {
        public const int DefaultOperationsPerSample = 64;
        public const int DefaultSampleCount = 7;

        private static readonly ProfilerMarker InstantiateDestroyMarker =
            new ProfilerMarker("UnityCoreLab.ObjectPooling.InstantiateDestroy");
        private static readonly ProfilerMarker RentReturnMarker =
            new ProfilerMarker("UnityCoreLab.ObjectPooling.RentReturn");

        private GameObject _template;
        private Transform _instantiateRoot;
        private Transform _activePoolRoot;
        private Transform _inactivePoolRoot;
        private GameObjectPool _pool;

        public bool IsRunning { get; private set; }

        public bool HasResult { get; private set; }

        public string Status { get; private set; } = "Ready";

        public ObjectPoolComparisonResult LastResult { get; private set; }

        private void Awake()
        {
            _instantiateRoot = CreateChild("Comparison - Instantiate Destroy");
            _activePoolRoot = CreateChild("Comparison - Pool Active");
            _inactivePoolRoot = CreateChild("Comparison - Pool Inactive");

            _template = new GameObject("Comparison Template");
            _template.transform.SetParent(transform, false);
            _template.AddComponent<PooledDemoActor>();
            _template.SetActive(false);
        }

        public IEnumerator RunComparison(
            int operationsPerSample = DefaultOperationsPerSample,
            int sampleCount = DefaultSampleCount)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    "A pooling comparison is already running.");
            }

            if (operationsPerSample <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationsPerSample));
            }

            if (sampleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }

            IsRunning = true;
            HasResult = false;
            Status = "Preparing matched workloads";

            try
            {
                var instances = new GameObject[operationsPerSample];
                var instantiateTicks = new long[sampleCount];
                var instantiateBytes = new long[sampleCount];
                var poolTicks = new long[sampleCount];
                var poolBytes = new long[sampleCount];

                ResetPool(operationsPerSample);

                Status = "Warm-up (excluded from results)";
                RunInstantiateDestroy(instances);
                yield return null;
                RunRentReturn(instances);
                yield return null;

                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    Status = $"Instantiate/Destroy sample {sampleIndex + 1}/{sampleCount}";
                    Measurement instantiate = MeasureInstantiateDestroy(instances);
                    instantiateTicks[sampleIndex] = instantiate.ElapsedTicks;
                    instantiateBytes[sampleIndex] = instantiate.ManagedAllocatedBytes;

                    // Destroy is deferred until the end of the frame. Waiting here
                    // prevents destroyed objects from accumulating across samples.
                    yield return null;

                    Status = $"Rent/Return sample {sampleIndex + 1}/{sampleCount}";
                    Measurement pooled = MeasureRentReturn(instances);
                    poolTicks[sampleIndex] = pooled.ElapsedTicks;
                    poolBytes[sampleIndex] = pooled.ManagedAllocatedBytes;
                    yield return null;
                }

                var instantiateSample = new ObjectPoolPerformanceSample(
                    "Instantiate / Destroy",
                    operationsPerSample,
                    sampleCount,
                    Median(instantiateTicks),
                    Median(instantiateBytes));
                var poolSample = new ObjectPoolPerformanceSample(
                    "Rent / Return",
                    operationsPerSample,
                    sampleCount,
                    Median(poolTicks),
                    Median(poolBytes));

                LastResult = new ObjectPoolComparisonResult(
                    instantiateSample,
                    poolSample,
                    _pool.Snapshot,
                    Application.isEditor);
                HasResult = true;
                Status = "Complete";
            }
            finally
            {
                IsRunning = false;
            }
        }

        private Measurement MeasureInstantiateDestroy(GameObject[] instances)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long startedAt = Stopwatch.GetTimestamp();
            RunInstantiateDestroy(instances);
            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            long allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            return new Measurement(elapsedTicks, allocatedBytes);
        }

        private Measurement MeasureRentReturn(GameObject[] instances)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long startedAt = Stopwatch.GetTimestamp();
            RunRentReturn(instances);
            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            long allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            return new Measurement(elapsedTicks, allocatedBytes);
        }

        private void RunInstantiateDestroy(GameObject[] instances)
        {
            using (InstantiateDestroyMarker.Auto())
            {
                for (int i = 0; i < instances.Length; i++)
                {
                    instances[i] = Object.Instantiate(
                        _template,
                        _instantiateRoot,
                        false);
                    instances[i].SetActive(true);
                }

                for (int i = instances.Length - 1; i >= 0; i--)
                {
                    Object.Destroy(instances[i]);
                    instances[i] = null;
                }
            }
        }

        private void RunRentReturn(GameObject[] instances)
        {
            using (RentReturnMarker.Auto())
            {
                for (int i = 0; i < instances.Length; i++)
                {
                    instances[i] = _pool.Rent();
                }

                for (int i = instances.Length - 1; i >= 0; i--)
                {
                    _pool.Return(instances[i]);
                    instances[i] = null;
                }
            }
        }

        private void ResetPool(int capacity)
        {
            _pool?.Dispose();
            _pool = new GameObjectPool(
                _template,
                _activePoolRoot,
                _inactivePoolRoot,
                capacity);
            _pool.Prewarm(capacity);
        }

        private Transform CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.transform;
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
            _pool = null;
        }

        private static long Median(long[] values)
        {
            Array.Sort(values);
            int middle = values.Length / 2;
            if ((values.Length & 1) == 1)
            {
                return values[middle];
            }

            long lower = values[middle - 1];
            long upper = values[middle];
            return lower + ((upper - lower) / 2L);
        }

        private readonly struct Measurement
        {
            public Measurement(long elapsedTicks, long managedAllocatedBytes)
            {
                ElapsedTicks = elapsedTicks;
                ManagedAllocatedBytes = managedAllocatedBytes;
            }

            public long ElapsedTicks { get; }

            public long ManagedAllocatedBytes { get; }
        }
    }
}

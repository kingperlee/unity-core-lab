using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.RenderingPerformance.LodCulling
{
    /// <summary>
    /// Applies an allocation-free, deterministic distance-LOD and explicit
    /// culling policy to a fixed set of Renderer objects.
    /// </summary>
    public sealed class DetailCullingWorkload : IDisposable
    {
        private static readonly ProfilerMarker EvaluateMarker =
            new ProfilerMarker("UnityCoreLab.DetailCulling.Evaluate");
        private static readonly ProfilerMarker SimulatedUpdateMarker =
            new ProfilerMarker("UnityCoreLab.DetailCulling.SimulatedUpdate");

        private readonly Mesh _highDetailMesh;
        private readonly Mesh _lowDetailMesh;
        private readonly Material _sharedMaterial;
        private readonly Transform _root;
        private readonly int _highDetailTriangleCount;
        private readonly int _lowDetailTriangleCount;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private DetailCullingSnapshot _snapshot;
        private bool _disposed;

        public DetailCullingWorkload(
            Mesh highDetailMesh,
            Mesh lowDetailMesh,
            Material sharedMaterial,
            Transform root)
        {
            _highDetailMesh = highDetailMesh != null
                ? highDetailMesh
                : throw new ArgumentNullException(nameof(highDetailMesh));
            _lowDetailMesh = lowDetailMesh != null
                ? lowDetailMesh
                : throw new ArgumentNullException(nameof(lowDetailMesh));
            _sharedMaterial = sharedMaterial != null
                ? sharedMaterial
                : throw new ArgumentNullException(nameof(sharedMaterial));
            _root = root != null
                ? root
                : throw new ArgumentNullException(nameof(root));
            _highDetailTriangleCount = TriangleCount(_highDetailMesh);
            _lowDetailTriangleCount = TriangleCount(_lowDetailMesh);
        }

        public DetailCullingSnapshot Snapshot => _snapshot;

        public int LastSimulatedUpdateCount { get; private set; }

        public int LastSimulatedUpdateIterations { get; private set; }

        public bool LastUpdateUsedPolicyActiveOnly { get; private set; }

        public float SimulationChecksum { get; private set; }

        public void Rebuild(IReadOnlyList<Vector3> localPositions)
        {
            ThrowIfDisposed();
            if (localPositions == null)
            {
                throw new ArgumentNullException(nameof(localPositions));
            }

            if (localPositions.Count == 0)
            {
                throw new ArgumentException(
                    "At least one object position is required.",
                    nameof(localPositions));
            }

            ClearObjects();
            for (int index = 0; index < localPositions.Count; index++)
            {
                var instance = new GameObject($"LOD Instance {index:000}");
                instance.transform.SetParent(_root, false);
                instance.transform.localPosition = localPositions[index];

                var meshFilter = instance.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _highDetailMesh;

                var renderer = instance.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var worldBounds = new Bounds(
                    instance.transform.position,
                    Vector3.one * 1.2f);
                _entries.Add(new Entry(meshFilter, renderer, worldBounds));
            }

            _snapshot = new DetailCullingSnapshot(
                DetailCullingMode.AllHighDetail,
                _entries.Count,
                _entries.Count,
                0,
                0,
                _entries.Count * _highDetailTriangleCount,
                0f,
                0f);
        }

        public void Evaluate(
            Camera camera,
            DetailCullingMode mode,
            float highDetailDistance,
            float cullDistance)
        {
            ThrowIfDisposed();
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!Enum.IsDefined(typeof(DetailCullingMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            if (highDetailDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(highDetailDistance));
            }

            if (cullDistance <= highDetailDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(cullDistance));
            }

            using (EvaluateMarker.Auto())
            {
                bool useLod = mode != DetailCullingMode.AllHighDetail;
                bool useExplicitCulling =
                    mode == DetailCullingMode.DistanceLodAndExplicitCulling;
                float highDistanceSquared =
                    highDetailDistance * highDetailDistance;
                float cullDistanceSquared = cullDistance * cullDistance;

                if (useExplicitCulling)
                {
                    Matrix4x4 viewProjection =
                        camera.projectionMatrix * camera.worldToCameraMatrix;
                    GeometryUtility.CalculateFrustumPlanes(
                        viewProjection,
                        _frustumPlanes);
                }

                int highCount = 0;
                int lowCount = 0;
                int culledCount = 0;

                foreach (Entry entry in _entries)
                {
                    float distanceSquared =
                        (entry.Bounds.center - camera.transform.position)
                        .sqrMagnitude;
                    bool policyCulled = useExplicitCulling &&
                        (distanceSquared > cullDistanceSquared ||
                         !GeometryUtility.TestPlanesAABB(
                             _frustumPlanes,
                             entry.Bounds));

                    if (policyCulled)
                    {
                        SetRendererEnabled(entry.Renderer, false);
                        culledCount++;
                        continue;
                    }

                    SetRendererEnabled(entry.Renderer, true);
                    bool useHighDetail =
                        !useLod || distanceSquared <= highDistanceSquared;
                    Mesh desiredMesh = useHighDetail
                        ? _highDetailMesh
                        : _lowDetailMesh;
                    if (entry.MeshFilter.sharedMesh != desiredMesh)
                    {
                        entry.MeshFilter.sharedMesh = desiredMesh;
                    }

                    if (useHighDetail)
                    {
                        highCount++;
                    }
                    else
                    {
                        lowCount++;
                    }
                }

                int triangleCount =
                    highCount * _highDetailTriangleCount +
                    lowCount * _lowDetailTriangleCount;
                _snapshot = new DetailCullingSnapshot(
                    mode,
                    _entries.Count,
                    highCount,
                    lowCount,
                    culledCount,
                    triangleCount,
                    highDetailDistance,
                    cullDistance);
            }
        }

        /// <summary>
        /// Runs deterministic synthetic CPU work to demonstrate that culling
        /// only saves CPU time when it gates more expensive downstream logic.
        /// This is a controlled lab workload, not a production benchmark.
        /// </summary>
        public void RunSimulatedUpdate(
            bool policyActiveOnly,
            int iterations,
            float timeStep)
        {
            ThrowIfDisposed();
            if (iterations <= 0 || iterations > 128)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }

            using (SimulatedUpdateMarker.Auto())
            {
                int updatedCount = 0;
                float checksum = 0f;

                foreach (Entry entry in _entries)
                {
                    if (policyActiveOnly && !entry.Renderer.enabled)
                    {
                        continue;
                    }

                    float value = entry.SimulationState + timeStep + 0.001f;
                    for (int iteration = 0;
                         iteration < iterations;
                         iteration++)
                    {
                        value = Mathf.Sin(
                            value * 1.173f + iteration * 0.013f);
                    }

                    entry.SimulationState = value;
                    checksum += value;
                    updatedCount++;
                }

                LastSimulatedUpdateCount = updatedCount;
                LastSimulatedUpdateIterations = iterations;
                LastUpdateUsedPolicyActiveOnly = policyActiveOnly;
                SimulationChecksum = checksum;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ClearObjects();
            _disposed = true;
        }

        private void ClearObjects()
        {
            foreach (Entry entry in _entries)
            {
                if (entry.MeshFilter != null)
                {
                    DestroyOwned(entry.MeshFilter.gameObject);
                }
            }

            _entries.Clear();
            _snapshot = default(DetailCullingSnapshot);
            LastSimulatedUpdateCount = 0;
            LastSimulatedUpdateIterations = 0;
            LastUpdateUsedPolicyActiveOnly = false;
            SimulationChecksum = 0f;
        }

        private static int TriangleCount(Mesh mesh)
        {
            int triangleCount = 0;
            for (int subMeshIndex = 0;
                 subMeshIndex < mesh.subMeshCount;
                 subMeshIndex++)
            {
                triangleCount += (int)mesh.GetIndexCount(subMeshIndex) / 3;
            }

            return triangleCount;
        }

        private static void SetRendererEnabled(Renderer renderer, bool enabled)
        {
            if (renderer.enabled != enabled)
            {
                renderer.enabled = enabled;
            }
        }

        private static void DestroyOwned(Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(ownedObject);
            }
            else
            {
                Object.DestroyImmediate(ownedObject);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DetailCullingWorkload));
            }
        }

        private sealed class Entry
        {
            public Entry(
                MeshFilter meshFilter,
                MeshRenderer renderer,
                Bounds bounds)
            {
                MeshFilter = meshFilter;
                Renderer = renderer;
                Bounds = bounds;
            }

            public MeshFilter MeshFilter { get; }

            public MeshRenderer Renderer { get; }

            public Bounds Bounds { get; }

            public float SimulationState { get; set; }
        }
    }
}

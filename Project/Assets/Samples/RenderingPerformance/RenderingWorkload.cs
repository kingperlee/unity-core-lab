using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.RenderingPerformance
{
    /// <summary>
    /// Owns three equivalent visual workloads while making their managed Unity
    /// object and material costs explicit.
    /// </summary>
    public sealed class RenderingWorkload : IDisposable
    {
        public const int MaxInstancesPerDrawMeshInstancedCall = 1023;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Mesh _mesh;
        private readonly Material _sharedMaterial;
        private readonly Transform _root;
        private readonly List<GameObject> _rendererObjects =
            new List<GameObject>();
        private readonly List<Material> _ownedMaterials =
            new List<Material>();
        private Matrix4x4[][] _instancedBatches = Array.Empty<Matrix4x4[]>();
        private RenderingWorkloadSnapshot _snapshot;
        private bool _disposed;

        public RenderingWorkload(
            Mesh mesh,
            Material sharedMaterial,
            Transform root)
        {
            _mesh = mesh != null
                ? mesh
                : throw new ArgumentNullException(nameof(mesh));
            _sharedMaterial = sharedMaterial != null
                ? sharedMaterial
                : throw new ArgumentNullException(nameof(sharedMaterial));
            _root = root != null
                ? root
                : throw new ArgumentNullException(nameof(root));
        }

        public RenderingWorkloadSnapshot Snapshot => _snapshot;

        public void Rebuild(
            RenderingWorkloadMode mode,
            int instanceCount,
            int columns = 20,
            float spacing = 0.85f)
        {
            ThrowIfDisposed();
            if (instanceCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceCount));
            }

            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }

            ClearCurrentWorkload();
            Matrix4x4[] matrices = BuildMatrices(instanceCount, columns, spacing);

            switch (mode)
            {
                case RenderingWorkloadMode.UniqueMaterialRenderers:
                    _sharedMaterial.enableInstancing = false;
                    BuildRendererObjects(matrices, uniqueMaterials: true);
                    break;

                case RenderingWorkloadMode.SharedMaterialPropertyBlocks:
                    _sharedMaterial.enableInstancing = true;
                    BuildRendererObjects(matrices, uniqueMaterials: false);
                    break;

                case RenderingWorkloadMode.ExplicitGpuInstancing:
                    _sharedMaterial.enableInstancing = true;
                    _instancedBatches = SplitIntoInstancedBatches(matrices);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            int rendererCount = _rendererObjects.Count;
            int materialCount = mode == RenderingWorkloadMode.UniqueMaterialRenderers
                ? _ownedMaterials.Count
                : 1;
            _snapshot = new RenderingWorkloadSnapshot(
                mode,
                instanceCount,
                rendererCount,
                materialCount,
                _instancedBatches.Length);
        }

        public void RenderInstanced(Camera targetCamera = null)
        {
            ThrowIfDisposed();
            if (_snapshot.Mode != RenderingWorkloadMode.ExplicitGpuInstancing)
            {
                return;
            }

            foreach (Matrix4x4[] batch in _instancedBatches)
            {
                Graphics.DrawMeshInstanced(
                    _mesh,
                    0,
                    _sharedMaterial,
                    batch,
                    batch.Length,
                    null,
                    ShadowCastingMode.Off,
                    receiveShadows: false,
                    layer: _root.gameObject.layer,
                    camera: targetCamera,
                    lightProbeUsage: LightProbeUsage.Off,
                    lightProbeProxyVolume: null);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ClearCurrentWorkload();
            _disposed = true;
        }

        private void BuildRendererObjects(
            Matrix4x4[] matrices,
            bool uniqueMaterials)
        {
            var propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < matrices.Length; i++)
            {
                var instance = new GameObject($"Render Instance {i:0000}");
                instance.transform.SetParent(_root, false);
                instance.transform.localPosition = matrices[i].GetColumn(3);
                instance.transform.localScale = Vector3.one * 0.65f;

                var meshFilter = instance.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _mesh;
                var meshRenderer = instance.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                Color color = Color.HSVToRGB((i % 24) / 24f, 0.72f, 0.95f);
                if (uniqueMaterials)
                {
                    var material = new Material(_sharedMaterial)
                    {
                        enableInstancing = false
                    };
                    material.SetColor(BaseColorId, color);
                    meshRenderer.sharedMaterial = material;
                    _ownedMaterials.Add(material);
                }
                else
                {
                    meshRenderer.sharedMaterial = _sharedMaterial;
                    propertyBlock.SetColor(BaseColorId, color);
                    meshRenderer.SetPropertyBlock(propertyBlock);
                }

                _rendererObjects.Add(instance);
            }
        }

        private void ClearCurrentWorkload()
        {
            foreach (GameObject rendererObject in _rendererObjects)
            {
                DestroyOwned(rendererObject);
            }

            foreach (Material material in _ownedMaterials)
            {
                DestroyOwned(material);
            }

            _rendererObjects.Clear();
            _ownedMaterials.Clear();
            _instancedBatches = Array.Empty<Matrix4x4[]>();
            _snapshot = default(RenderingWorkloadSnapshot);
        }

        private static Matrix4x4[] BuildMatrices(
            int instanceCount,
            int columns,
            float spacing)
        {
            var matrices = new Matrix4x4[instanceCount];
            int rows = Mathf.CeilToInt((float)instanceCount / columns);
            float xOffset = (columns - 1) * spacing * 0.5f;
            float yOffset = (rows - 1) * spacing * 0.5f;

            for (int i = 0; i < instanceCount; i++)
            {
                int column = i % columns;
                int row = i / columns;
                var position = new Vector3(
                    column * spacing - xOffset,
                    row * spacing - yOffset,
                    0f);
                matrices[i] = Matrix4x4.TRS(
                    position,
                    Quaternion.identity,
                    Vector3.one * 0.65f);
            }

            return matrices;
        }

        private static Matrix4x4[][] SplitIntoInstancedBatches(
            Matrix4x4[] matrices)
        {
            int batchCount = Mathf.CeilToInt(
                (float)matrices.Length / MaxInstancesPerDrawMeshInstancedCall);
            var batches = new Matrix4x4[batchCount][];

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                int sourceIndex =
                    batchIndex * MaxInstancesPerDrawMeshInstancedCall;
                int count = Math.Min(
                    MaxInstancesPerDrawMeshInstancedCall,
                    matrices.Length - sourceIndex);
                var batch = new Matrix4x4[count];
                Array.Copy(matrices, sourceIndex, batch, 0, count);
                batches[batchIndex] = batch;
            }

            return batches;
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
                throw new ObjectDisposedException(nameof(RenderingWorkload));
            }
        }
    }
}

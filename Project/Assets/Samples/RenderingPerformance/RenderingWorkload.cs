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
        private readonly Material _rendererMaterial;
        private readonly Material _instancedMaterial;
        private readonly Transform _root;
        private readonly List<GameObject> _rendererObjects =
            new List<GameObject>();
        private readonly List<Material> _ownedMaterials =
            new List<Material>();
        private Matrix4x4[][] _instancedBatches = Array.Empty<Matrix4x4[]>();
        private MaterialPropertyBlock[] _instancedPropertyBlocks =
            Array.Empty<MaterialPropertyBlock>();
        private int _explicitColorCount;
        private RenderingWorkloadSnapshot _snapshot;
        private bool _disposed;

        public RenderingWorkload(
            Mesh mesh,
            Material rendererMaterial,
            Material instancedMaterial,
            Transform root)
        {
            _mesh = mesh != null
                ? mesh
                : throw new ArgumentNullException(nameof(mesh));
            _rendererMaterial = rendererMaterial != null
                ? rendererMaterial
                : throw new ArgumentNullException(nameof(rendererMaterial));
            _instancedMaterial = instancedMaterial != null
                ? instancedMaterial
                : throw new ArgumentNullException(nameof(instancedMaterial));
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
                    _rendererMaterial.enableInstancing = false;
                    BuildRendererObjects(
                        matrices,
                        uniqueMaterials: true,
                        usePropertyBlocks: false);
                    break;

                case RenderingWorkloadMode.SharedMaterialRenderers:
                    _rendererMaterial.enableInstancing = false;
                    BuildRendererObjects(
                        matrices,
                        uniqueMaterials: false,
                        usePropertyBlocks: false);
                    break;

                case RenderingWorkloadMode.SharedMaterialPropertyBlocks:
                    _rendererMaterial.enableInstancing = false;
                    BuildRendererObjects(
                        matrices,
                        uniqueMaterials: false,
                        usePropertyBlocks: true);
                    break;

                case RenderingWorkloadMode.ExplicitGpuInstancing:
                    _instancedMaterial.enableInstancing = true;
                    BuildExplicitInstancedBatches(matrices);
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
                _instancedBatches.Length,
                _explicitColorCount);
        }

        public void RenderInstanced(Camera targetCamera = null)
        {
            ThrowIfDisposed();
            if (_snapshot.Mode != RenderingWorkloadMode.ExplicitGpuInstancing)
            {
                return;
            }

            for (int batchIndex = 0;
                 batchIndex < _instancedBatches.Length;
                 batchIndex++)
            {
                Matrix4x4[] batch = _instancedBatches[batchIndex];
                Graphics.DrawMeshInstanced(
                    _mesh,
                    0,
                    _instancedMaterial,
                    batch,
                    batch.Length,
                    _instancedPropertyBlocks[batchIndex],
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
            bool uniqueMaterials,
            bool usePropertyBlocks)
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

                Color color = InstanceColor(i);
                if (uniqueMaterials)
                {
                    var material = new Material(_rendererMaterial)
                    {
                        enableInstancing = false
                    };
                    material.SetColor(BaseColorId, color);
                    meshRenderer.sharedMaterial = material;
                    _ownedMaterials.Add(material);
                }
                else
                {
                    meshRenderer.sharedMaterial = _rendererMaterial;
                    if (usePropertyBlocks)
                    {
                        propertyBlock.SetColor(BaseColorId, color);
                        meshRenderer.SetPropertyBlock(propertyBlock);
                    }
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
            _instancedPropertyBlocks = Array.Empty<MaterialPropertyBlock>();
            _explicitColorCount = 0;
            _snapshot = default(RenderingWorkloadSnapshot);
        }

        private void BuildExplicitInstancedBatches(Matrix4x4[] matrices)
        {
            _instancedBatches = SplitIntoInstancedBatches(matrices);
            _instancedPropertyBlocks =
                new MaterialPropertyBlock[_instancedBatches.Length];

            int sourceIndex = 0;
            for (int batchIndex = 0;
                 batchIndex < _instancedBatches.Length;
                 batchIndex++)
            {
                int batchLength = _instancedBatches[batchIndex].Length;
                var colors = new Vector4[batchLength];
                for (int colorIndex = 0;
                     colorIndex < batchLength;
                     colorIndex++)
                {
                    int instanceIndex = sourceIndex + colorIndex;
                    Color color = InstanceColor(instanceIndex);
                    colors[colorIndex] = color;
                }

                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetVectorArray(BaseColorId, colors);
                _instancedPropertyBlocks[batchIndex] = propertyBlock;
                sourceIndex += batchLength;
            }

            _explicitColorCount = sourceIndex;
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

        private static Color InstanceColor(int instanceIndex)
        {
            return Color.HSVToRGB(
                (instanceIndex % 24) / 24f,
                0.72f,
                0.95f);
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

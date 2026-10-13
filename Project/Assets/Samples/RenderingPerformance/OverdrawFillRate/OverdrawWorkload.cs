using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.RenderingPerformance.OverdrawFillRate
{
    /// <summary>
    /// Builds matched layers whose draw structure stays constant while their
    /// depth-write and blending behavior changes.
    /// </summary>
    public sealed class OverdrawWorkload : IDisposable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FragmentIterationsId =
            Shader.PropertyToID("_FragmentIterations");
        private const float LayerDepthSpacing = 0.015f;

        private readonly Mesh _quadMesh;
        private readonly Material _opaqueMaterialTemplate;
        private readonly Material _transparentMaterialTemplate;
        private readonly Transform _root;
        private readonly List<GameObject> _layerObjects = new List<GameObject>();
        private readonly List<Material> _ownedMaterials = new List<Material>();
        private OverdrawWorkloadSnapshot _snapshot;
        private bool _disposed;

        public OverdrawWorkload(
            Mesh quadMesh,
            Material opaqueMaterialTemplate,
            Material transparentMaterialTemplate,
            Transform root)
        {
            _quadMesh = quadMesh != null
                ? quadMesh
                : throw new ArgumentNullException(nameof(quadMesh));
            _opaqueMaterialTemplate = opaqueMaterialTemplate != null
                ? opaqueMaterialTemplate
                : throw new ArgumentNullException(nameof(opaqueMaterialTemplate));
            _transparentMaterialTemplate = transparentMaterialTemplate != null
                ? transparentMaterialTemplate
                : throw new ArgumentNullException(nameof(transparentMaterialTemplate));
            _root = root != null
                ? root
                : throw new ArgumentNullException(nameof(root));
        }

        public OverdrawWorkloadSnapshot Snapshot => _snapshot;

        public void Rebuild(
            OverdrawMode mode,
            int layerCount,
            float coverageRatio,
            int fragmentIterations,
            Vector2 fullscreenWorldSize)
        {
            ThrowIfDisposed();
            if (mode != OverdrawMode.OpaqueDepthWrite &&
                mode != OverdrawMode.TransparentAlphaBlend)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            if (layerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(layerCount));
            }

            if (coverageRatio <= 0f || coverageRatio > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(coverageRatio));
            }

            if (fragmentIterations <= 0 || fragmentIterations > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(fragmentIterations));
            }

            if (fullscreenWorldSize.x <= 0f || fullscreenWorldSize.y <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fullscreenWorldSize));
            }

            ClearCurrentWorkload();

            float linearCoverage = Mathf.Sqrt(coverageRatio);
            var quadWorldSize = fullscreenWorldSize * linearCoverage;
            Material template = mode == OverdrawMode.OpaqueDepthWrite
                ? _opaqueMaterialTemplate
                : _transparentMaterialTemplate;

            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                var layer = new GameObject($"Overdraw Layer {layerIndex:000}");
                layer.transform.SetParent(_root, false);
                layer.transform.localPosition = new Vector3(
                    0f,
                    0f,
                    layerIndex * LayerDepthSpacing);
                layer.transform.localScale = new Vector3(
                    quadWorldSize.x,
                    quadWorldSize.y,
                    1f);

                var meshFilter = layer.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _quadMesh;

                var renderer = layer.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                var material = new Material(template)
                {
                    name = $"{mode} Layer {layerIndex:000}"
                };
                material.SetColor(BaseColorId, LayerColor(mode, layerIndex));
                material.SetInt(FragmentIterationsId, fragmentIterations);
                renderer.sharedMaterial = material;

                _ownedMaterials.Add(material);
                _layerObjects.Add(layer);
            }

            _snapshot = new OverdrawWorkloadSnapshot(
                mode,
                layerCount,
                _layerObjects.Count,
                _ownedMaterials.Count,
                coverageRatio,
                fragmentIterations,
                quadWorldSize);
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

        private void ClearCurrentWorkload()
        {
            foreach (GameObject layerObject in _layerObjects)
            {
                DestroyOwned(layerObject);
            }

            foreach (Material material in _ownedMaterials)
            {
                DestroyOwned(material);
            }

            _layerObjects.Clear();
            _ownedMaterials.Clear();
            _snapshot = default(OverdrawWorkloadSnapshot);
        }

        private static Color LayerColor(OverdrawMode mode, int layerIndex)
        {
            Color color = Color.HSVToRGB(
                (0.54f + layerIndex * 0.035f) % 1f,
                0.72f,
                0.95f);
            color.a = mode == OverdrawMode.TransparentAlphaBlend ? 0.14f : 1f;
            return color;
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
                throw new ObjectDisposedException(nameof(OverdrawWorkload));
            }
        }
    }
}

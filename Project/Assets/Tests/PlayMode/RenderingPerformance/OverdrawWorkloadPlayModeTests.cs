using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.RenderingPerformance.OverdrawFillRate;

namespace UnityCoreLab.Tests.PlayMode.RenderingPerformance
{
    public sealed class OverdrawWorkloadPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_InitializesAndSwitchesMatchedWorkload()
        {
            var root = new GameObject("Overdraw Fill Rate Demo Test");
            var demo = root.AddComponent<OverdrawFillRateDemo>();

            Assert.That(demo.IsInitialized, Is.True);
            AssertSnapshot(
                demo.Snapshot,
                OverdrawMode.OpaqueDepthWrite,
                layers: OverdrawFillRateDemo.DefaultLayerCount,
                coverageRatio: OverdrawFillRateDemo.DefaultCoverageRatio,
                fragmentIterations:
                    OverdrawFillRateDemo.DefaultFragmentIterations);

            demo.Rebuild(
                OverdrawMode.TransparentAlphaBlend,
                layerCount: 8,
                coverageRatio: 0.25f,
                fragmentIterations: 24);
            yield return null;

            AssertSnapshot(
                demo.Snapshot,
                OverdrawMode.TransparentAlphaBlend,
                layers: 8,
                coverageRatio: 0.25f,
                fragmentIterations: 24);
            Assert.That(demo.Snapshot.EquivalentFullscreenLayers, Is.EqualTo(2f));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebuild_KeepsGeometryCountsMatchedAcrossModes()
        {
            using (var fixture = new WorkloadFixture())
            {
                var fullscreenSize = new Vector2(16f, 9f);
                fixture.Workload.Rebuild(
                    OverdrawMode.OpaqueDepthWrite,
                    layerCount: 8,
                    coverageRatio: 0.25f,
                    fragmentIterations: 8,
                    fullscreenWorldSize: fullscreenSize);
                yield return null;

                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    OverdrawMode.OpaqueDepthWrite,
                    layers: 8,
                    coverageRatio: 0.25f,
                    fragmentIterations: 8);
                Assert.That(
                    fixture.Workload.Snapshot.QuadWorldSize.x,
                    Is.EqualTo(8f).Within(0.001f));
                Assert.That(
                    fixture.Workload.Snapshot.QuadWorldSize.y,
                    Is.EqualTo(4.5f).Within(0.001f));
                Assert.That(fixture.WorkloadRoot.childCount, Is.EqualTo(8));
                AssertLayerState(
                    fixture.WorkloadRoot.GetChild(0),
                    expectedZWrite: 1,
                    expectedQueue: (int)RenderQueue.Geometry,
                    expectedFragmentIterations: 8);

                fixture.Workload.Rebuild(
                    OverdrawMode.TransparentAlphaBlend,
                    layerCount: 8,
                    coverageRatio: 0.25f,
                    fragmentIterations: 24,
                    fullscreenWorldSize: fullscreenSize);
                yield return null;

                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    OverdrawMode.TransparentAlphaBlend,
                    layers: 8,
                    coverageRatio: 0.25f,
                    fragmentIterations: 24);
                Assert.That(fixture.WorkloadRoot.childCount, Is.EqualTo(8));
                AssertLayerState(
                    fixture.WorkloadRoot.GetChild(0),
                    expectedZWrite: 0,
                    expectedQueue: (int)RenderQueue.Transparent,
                    expectedFragmentIterations: 24);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebuild_RejectsInvalidCoverageAndLayerCounts()
        {
            using (var fixture = new WorkloadFixture())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.Rebuild(
                        OverdrawMode.OpaqueDepthWrite,
                        0,
                        0.5f,
                        8,
                        new Vector2(16f, 9f)));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.Rebuild(
                        OverdrawMode.OpaqueDepthWrite,
                        8,
                        1.1f,
                        8,
                        new Vector2(16f, 9f)));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.Rebuild(
                        OverdrawMode.OpaqueDepthWrite,
                        8,
                        0.5f,
                        65,
                        new Vector2(16f, 9f)));
            }

            yield return null;
        }

        private static void AssertSnapshot(
            OverdrawWorkloadSnapshot snapshot,
            OverdrawMode mode,
            int layers,
            float coverageRatio,
            int fragmentIterations)
        {
            Assert.That(snapshot.Mode, Is.EqualTo(mode));
            Assert.That(snapshot.LayerCount, Is.EqualTo(layers));
            Assert.That(snapshot.RendererCount, Is.EqualTo(layers));
            Assert.That(snapshot.MaterialCount, Is.EqualTo(layers));
            Assert.That(snapshot.CoverageRatio, Is.EqualTo(coverageRatio));
            Assert.That(snapshot.FragmentIterations,
                Is.EqualTo(fragmentIterations));
        }

        private static void AssertLayerState(
            Transform layer,
            int expectedZWrite,
            int expectedQueue,
            int expectedFragmentIterations)
        {
            var renderer = layer.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.GetInt("_ZWrite"),
                Is.EqualTo(expectedZWrite));
            Assert.That(renderer.sharedMaterial.renderQueue,
                Is.EqualTo(expectedQueue));
            Assert.That(renderer.sharedMaterial.GetInt("_FragmentIterations"),
                Is.EqualTo(expectedFragmentIterations));
        }

        private sealed class WorkloadFixture : IDisposable
        {
            private readonly GameObject _meshSource;
            private readonly GameObject _root;
            private readonly Material _opaqueMaterial;
            private readonly Material _transparentMaterial;

            public WorkloadFixture()
            {
                _meshSource = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _meshSource.SetActive(false);
                Mesh mesh = _meshSource.GetComponent<MeshFilter>().sharedMesh;
                Shader shader = Shader.Find(
                    "UnityCoreLab/Rendering/OverdrawFillRate");
                Assert.That(shader, Is.Not.Null);

                _opaqueMaterial = CreateMaterial(
                    shader,
                    zWrite: 1,
                    sourceBlend: BlendMode.One,
                    destinationBlend: BlendMode.Zero,
                    renderQueue: RenderQueue.Geometry);
                _transparentMaterial = CreateMaterial(
                    shader,
                    zWrite: 0,
                    sourceBlend: BlendMode.SrcAlpha,
                    destinationBlend: BlendMode.OneMinusSrcAlpha,
                    renderQueue: RenderQueue.Transparent);

                _root = new GameObject("Overdraw Workload Test Root");
                WorkloadRoot = _root.transform;
                Workload = new OverdrawWorkload(
                    mesh,
                    _opaqueMaterial,
                    _transparentMaterial,
                    WorkloadRoot);
            }

            public OverdrawWorkload Workload { get; }

            public Transform WorkloadRoot { get; }

            public void Dispose()
            {
                Workload.Dispose();
                UnityEngine.Object.Destroy(_opaqueMaterial);
                UnityEngine.Object.Destroy(_transparentMaterial);
                UnityEngine.Object.Destroy(_meshSource);
                UnityEngine.Object.Destroy(_root);
            }

            private static Material CreateMaterial(
                Shader shader,
                int zWrite,
                BlendMode sourceBlend,
                BlendMode destinationBlend,
                RenderQueue renderQueue)
            {
                var material = new Material(shader)
                {
                    renderQueue = (int)renderQueue
                };
                material.SetInt("_ZWrite", zWrite);
                material.SetInt("_SrcBlend", (int)sourceBlend);
                material.SetInt("_DstBlend", (int)destinationBlend);
                return material;
            }
        }
    }
}

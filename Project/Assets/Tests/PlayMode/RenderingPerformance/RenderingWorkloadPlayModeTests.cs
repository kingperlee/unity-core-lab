using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.RenderingPerformance;

namespace UnityCoreLab.Tests.PlayMode.RenderingPerformance
{
    public sealed class RenderingWorkloadPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_InitializesAndSwitchesRuntimeSubmissionMode()
        {
            var root = new GameObject("Rendering Performance Demo Test");
            var demo = root.AddComponent<RenderingPerformanceDemo>();

            Assert.That(demo.IsInitialized, Is.True);
            AssertSnapshot(
                demo.Snapshot,
                RenderingWorkloadMode.UniqueMaterialRenderers,
                instances: RenderingPerformanceDemo.DefaultInstanceCount,
                renderers: RenderingPerformanceDemo.DefaultInstanceCount,
                materials: RenderingPerformanceDemo.DefaultInstanceCount,
                explicitBatches: 0,
                explicitColors: 0);

            demo.Rebuild(RenderingWorkloadMode.ExplicitGpuInstancing, 32);
            yield return null;

            AssertSnapshot(
                demo.Snapshot,
                RenderingWorkloadMode.ExplicitGpuInstancing,
                instances: 32,
                renderers: 0,
                materials: 1,
                explicitBatches: 1,
                explicitColors: 32);

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebuild_ExposesOwnershipAndInstancedBatchBoundaries()
        {
            using (var fixture = new WorkloadFixture())
            {
                fixture.Workload.Rebuild(
                    RenderingWorkloadMode.UniqueMaterialRenderers,
                    12,
                    columns: 4);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    RenderingWorkloadMode.UniqueMaterialRenderers,
                    instances: 12,
                    renderers: 12,
                    materials: 12,
                    explicitBatches: 0,
                    explicitColors: 0);

                fixture.Workload.Rebuild(
                    RenderingWorkloadMode.SharedMaterialRenderers,
                    12,
                    columns: 4);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    RenderingWorkloadMode.SharedMaterialRenderers,
                    instances: 12,
                    renderers: 12,
                    materials: 1,
                    explicitBatches: 0,
                    explicitColors: 0);

                yield return null;
                Assert.That(fixture.WorkloadRoot.childCount, Is.EqualTo(12));
                Assert.That(
                    fixture.WorkloadRoot.GetChild(0)
                        .GetComponent<MeshRenderer>()
                        .HasPropertyBlock(),
                    Is.False);

                fixture.Workload.Rebuild(
                    RenderingWorkloadMode.SharedMaterialPropertyBlocks,
                    12,
                    columns: 4);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    RenderingWorkloadMode.SharedMaterialPropertyBlocks,
                    instances: 12,
                    renderers: 12,
                    materials: 1,
                    explicitBatches: 0,
                    explicitColors: 0);

                yield return null;
                Assert.That(fixture.WorkloadRoot.childCount, Is.EqualTo(12));
                Assert.That(
                    fixture.WorkloadRoot.GetChild(0)
                        .GetComponent<MeshRenderer>()
                        .HasPropertyBlock(),
                    Is.True);

                fixture.Workload.Rebuild(
                    RenderingWorkloadMode.ExplicitGpuInstancing,
                    2050,
                    columns: 50);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    RenderingWorkloadMode.ExplicitGpuInstancing,
                    instances: 2050,
                    renderers: 0,
                    materials: 1,
                    explicitBatches: 3,
                    explicitColors: 2050);

                yield return null;
                Assert.That(fixture.WorkloadRoot.childCount, Is.Zero);
            }

            yield return null;
        }

        private static void AssertSnapshot(
            RenderingWorkloadSnapshot snapshot,
            RenderingWorkloadMode mode,
            int instances,
            int renderers,
            int materials,
            int explicitBatches,
            int explicitColors)
        {
            Assert.That(snapshot.Mode, Is.EqualTo(mode));
            Assert.That(snapshot.InstanceCount, Is.EqualTo(instances));
            Assert.That(snapshot.RendererCount, Is.EqualTo(renderers));
            Assert.That(snapshot.MaterialCount, Is.EqualTo(materials));
            Assert.That(snapshot.ExplicitInstancedBatchCount,
                Is.EqualTo(explicitBatches));
            Assert.That(snapshot.ExplicitColorCount, Is.EqualTo(explicitColors));
        }

        private sealed class WorkloadFixture : IDisposable
        {
            private readonly GameObject _meshSource;
            private readonly GameObject _root;
            private readonly Material _rendererMaterial;
            private readonly Material _instancedMaterial;

            public WorkloadFixture()
            {
                _meshSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _meshSource.SetActive(false);
                Mesh mesh = _meshSource.GetComponent<MeshFilter>().sharedMesh;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                _rendererMaterial = new Material(shader);
                Shader instancedShader = Shader.Find(
                    "UnityCoreLab/Rendering/InstancedColor");
                Assert.That(instancedShader, Is.Not.Null);
                _instancedMaterial = new Material(instancedShader)
                {
                    enableInstancing = true
                };
                _root = new GameObject("Rendering Workload Test Root");
                WorkloadRoot = _root.transform;
                Workload = new RenderingWorkload(
                    mesh,
                    _rendererMaterial,
                    _instancedMaterial,
                    WorkloadRoot);
            }

            public RenderingWorkload Workload { get; }

            public Transform WorkloadRoot { get; }

            public void Dispose()
            {
                Workload.Dispose();
                UnityEngine.Object.Destroy(_rendererMaterial);
                UnityEngine.Object.Destroy(_instancedMaterial);
                UnityEngine.Object.Destroy(_meshSource);
                UnityEngine.Object.Destroy(_root);
            }
        }
    }
}

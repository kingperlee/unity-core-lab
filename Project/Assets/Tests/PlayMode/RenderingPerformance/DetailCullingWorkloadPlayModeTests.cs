using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.RenderingPerformance.LodCulling;

namespace UnityCoreLab.Tests.PlayMode.RenderingPerformance
{
    public sealed class DetailCullingWorkloadPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_InitializesAndAppliesVisibilityPolicy()
        {
            var root = new GameObject("Detail Culling Demo Test");
            var demo = root.AddComponent<DetailCullingDemo>();

            Assert.That(demo.IsInitialized, Is.True);
            Assert.That(demo.Snapshot.ObjectCount,
                Is.EqualTo(DetailCullingDemo.DefaultObjectCount));
            Assert.That(demo.Snapshot.HighDetailCount,
                Is.EqualTo(DetailCullingDemo.DefaultObjectCount));

            demo.SetMode(
                DetailCullingMode.DistanceLodAndExplicitCulling);
            yield return null;

            DetailCullingSnapshot snapshot = demo.Snapshot;
            Assert.That(
                snapshot.HighDetailCount +
                snapshot.LowDetailCount +
                snapshot.PolicyCulledCount,
                Is.EqualTo(snapshot.ObjectCount));
            Assert.That(snapshot.PolicyCulledCount, Is.GreaterThan(0));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Evaluate_SeparatesDetailAndCullingPolicies()
        {
            using (var fixture = new WorkloadFixture())
            {
                var positions = new[]
                {
                    new Vector3(0f, 0f, 5f),
                    new Vector3(0f, 0f, 15f),
                    new Vector3(0f, 0f, 35f),
                    new Vector3(25f, 0f, 10f)
                };
                fixture.Workload.Rebuild(positions);

                fixture.Workload.Evaluate(
                    fixture.Camera,
                    DetailCullingMode.AllHighDetail,
                    highDetailDistance: 10f,
                    cullDistance: 30f);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    DetailCullingMode.AllHighDetail,
                    high: 4,
                    low: 0,
                    culled: 0,
                    expectedTriangles: 4 * fixture.HighTriangles);

                fixture.Workload.Evaluate(
                    fixture.Camera,
                    DetailCullingMode.DistanceLod,
                    highDetailDistance: 10f,
                    cullDistance: 30f);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    DetailCullingMode.DistanceLod,
                    high: 1,
                    low: 3,
                    culled: 0,
                    expectedTriangles:
                        fixture.HighTriangles + 3 * fixture.LowTriangles);

                fixture.Workload.Evaluate(
                    fixture.Camera,
                    DetailCullingMode.DistanceLodAndExplicitCulling,
                    highDetailDistance: 10f,
                    cullDistance: 30f);
                AssertSnapshot(
                    fixture.Workload.Snapshot,
                    DetailCullingMode.DistanceLodAndExplicitCulling,
                    high: 1,
                    low: 1,
                    culled: 2,
                    expectedTriangles:
                        fixture.HighTriangles + fixture.LowTriangles);

                yield return null;
                Assert.That(fixture.WorkloadRoot.childCount, Is.EqualTo(4));
                Assert.That(
                    fixture.WorkloadRoot.GetChild(0)
                        .GetComponent<MeshRenderer>().enabled,
                    Is.True);
                Assert.That(
                    fixture.WorkloadRoot.GetChild(2)
                        .GetComponent<MeshRenderer>().enabled,
                    Is.False);
                Assert.That(
                    fixture.WorkloadRoot.GetChild(3)
                        .GetComponent<MeshRenderer>().enabled,
                    Is.False);

                fixture.Workload.RunSimulatedUpdate(
                    policyActiveOnly: false,
                    iterations: 8,
                    timeStep: 0.016f);
                Assert.That(
                    fixture.Workload.LastSimulatedUpdateCount,
                    Is.EqualTo(4));
                Assert.That(
                    fixture.Workload.LastUpdateUsedPolicyActiveOnly,
                    Is.False);

                fixture.Workload.RunSimulatedUpdate(
                    policyActiveOnly: true,
                    iterations: 8,
                    timeStep: 0.016f);
                Assert.That(
                    fixture.Workload.LastSimulatedUpdateCount,
                    Is.EqualTo(2));
                Assert.That(
                    fixture.Workload.LastUpdateUsedPolicyActiveOnly,
                    Is.True);
                Assert.That(
                    fixture.Workload.SimulationChecksum,
                    Is.Not.NaN);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Evaluate_RejectsInvalidThresholds()
        {
            using (var fixture = new WorkloadFixture())
            {
                fixture.Workload.Rebuild(new[] {new Vector3(0f, 0f, 5f)});

                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.Evaluate(
                        fixture.Camera,
                        DetailCullingMode.DistanceLod,
                        highDetailDistance: 0f,
                        cullDistance: 30f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.Evaluate(
                        fixture.Camera,
                        DetailCullingMode.DistanceLod,
                        highDetailDistance: 10f,
                        cullDistance: 10f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.RunSimulatedUpdate(
                        policyActiveOnly: false,
                        iterations: 0,
                        timeStep: 0.016f));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    fixture.Workload.RunSimulatedUpdate(
                        policyActiveOnly: false,
                        iterations: 129,
                        timeStep: 0.016f));
            }

            yield return null;
        }

        private static void AssertSnapshot(
            DetailCullingSnapshot snapshot,
            DetailCullingMode mode,
            int high,
            int low,
            int culled,
            int expectedTriangles)
        {
            Assert.That(snapshot.Mode, Is.EqualTo(mode));
            Assert.That(snapshot.ObjectCount, Is.EqualTo(4));
            Assert.That(snapshot.HighDetailCount, Is.EqualTo(high));
            Assert.That(snapshot.LowDetailCount, Is.EqualTo(low));
            Assert.That(snapshot.PolicyCulledCount, Is.EqualTo(culled));
            Assert.That(snapshot.EstimatedVisibleTriangleCount,
                Is.EqualTo(expectedTriangles));
        }

        private sealed class WorkloadFixture : IDisposable
        {
            private readonly GameObject _highMeshSource;
            private readonly GameObject _lowMeshSource;
            private readonly GameObject _root;
            private readonly GameObject _cameraObject;
            private readonly Material _material;

            public WorkloadFixture()
            {
                _highMeshSource =
                    GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _highMeshSource.SetActive(false);
                Mesh highMesh =
                    _highMeshSource.GetComponent<MeshFilter>().sharedMesh;

                _lowMeshSource =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);
                _lowMeshSource.SetActive(false);
                Mesh lowMesh =
                    _lowMeshSource.GetComponent<MeshFilter>().sharedMesh;
                HighTriangles = TriangleCount(highMesh);
                LowTriangles = TriangleCount(lowMesh);

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                _material = new Material(shader);
                _root = new GameObject("Detail Culling Workload Test Root");
                WorkloadRoot = _root.transform;
                Workload = new DetailCullingWorkload(
                    highMesh,
                    lowMesh,
                    _material,
                    WorkloadRoot);

                _cameraObject = new GameObject("Detail Culling Test Camera");
                Camera = _cameraObject.AddComponent<Camera>();
                Camera.transform.position = Vector3.zero;
                Camera.transform.rotation = Quaternion.identity;
                Camera.fieldOfView = 60f;
                Camera.aspect = 16f / 9f;
                Camera.nearClipPlane = 0.3f;
                Camera.farClipPlane = 100f;
            }

            public DetailCullingWorkload Workload { get; }

            public Transform WorkloadRoot { get; }

            public Camera Camera { get; }

            public int HighTriangles { get; }

            public int LowTriangles { get; }

            public void Dispose()
            {
                Workload.Dispose();
                UnityEngine.Object.Destroy(_material);
                UnityEngine.Object.Destroy(_cameraObject);
                UnityEngine.Object.Destroy(_highMeshSource);
                UnityEngine.Object.Destroy(_lowMeshSource);
                UnityEngine.Object.Destroy(_root);
            }

            private static int TriangleCount(Mesh mesh)
            {
                int result = 0;
                for (int index = 0; index < mesh.subMeshCount; index++)
                {
                    result += (int)mesh.GetIndexCount(index) / 3;
                }

                return result;
            }
        }
    }
}

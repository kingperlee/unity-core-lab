using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCoreLab.Samples.AssetLifecycle;

namespace UnityCoreLab.EditorTools
{
    public static class AssetLifecycleDemoSceneBuilder
    {
        private const string ScenePath =
            "Assets/Samples/AssetLifecycle/AssetLifecycleDemo.unity";

        [MenuItem("Unity Core Lab/Samples/Create Asset Lifecycle Demo Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var demoObject = new GameObject("Asset Lifecycle Demo");
            demoObject.AddComponent<AssetLifecycleDemo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = demoObject;
            Debug.Log($"Created asset lifecycle demo scene at {ScenePath}");
        }
    }
}

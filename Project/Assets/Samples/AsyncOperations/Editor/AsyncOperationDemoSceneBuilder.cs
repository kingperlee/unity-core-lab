using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCoreLab.Samples.AsyncOperations;

namespace UnityCoreLab.EditorTools
{
    public static class AsyncOperationDemoSceneBuilder
    {
        private const string ScenePath =
            "Assets/Samples/AsyncOperations/AsyncOperationDemo.unity";

        [MenuItem("Unity Core Lab/Samples/Create Async Operation Demo Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            var demoObject = new GameObject("Async Operation Demo");
            demoObject.AddComponent<AsyncOperationDemo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = demoObject;
            Debug.Log($"Created async operation demo scene at {ScenePath}");
        }
    }
}

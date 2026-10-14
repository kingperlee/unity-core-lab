using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCoreLab.Samples.UiPresentation;

namespace UnityCoreLab.EditorTools
{
    public static class UiPresenterDemoAssetBuilder
    {
        private const string ScenePath =
            "Assets/Samples/UiPresentation/UiPresenterDemo.unity";

        [MenuItem("Unity Core Lab/Samples/Rebuild UI Presenter Demo")]
        public static void RebuildAssets()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("UI Presenter Demo");
            root.AddComponent<UiPresenterDemo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt UI presenter demo scene: {ScenePath}");
        }
    }
}

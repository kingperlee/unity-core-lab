using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCoreLab.Samples.UiVirtualization;

namespace UnityCoreLab.EditorTools
{
    public static class UiVirtualizedListDemoAssetBuilder
    {
        private const string ScenePath =
            "Assets/Samples/UiVirtualization/UiVirtualizedListDemo.unity";

        [MenuItem("Unity Core Lab/Samples/Rebuild UI Virtualized List Demo")]
        public static void RebuildAssets()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("UI Virtualized List Demo");
            root.AddComponent<UiVirtualizedListDemo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt UI virtualized list demo scene: {ScenePath}");
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCoreLab.Samples.UiDialogs;

namespace UnityCoreLab.EditorTools
{
    public static class UiDialogDemoAssetBuilder
    {
        private const string ScenePath =
            "Assets/Samples/UiDialogs/UiDialogDemo.unity";

        [MenuItem("Unity Core Lab/Samples/Rebuild UI Dialog Demo")]
        public static void RebuildAssets()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var root = new GameObject("UI Dialog Demo");
            root.AddComponent<UiDialogDemo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt UI dialog demo scene: {ScenePath}");
        }
    }
}

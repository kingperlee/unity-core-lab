using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityCoreLab.Samples.UiNavigation;

namespace UnityCoreLab.EditorTools
{
    public static class UiPrefabDemoAssetBuilder
    {
        private const string ResourceRoot =
            "Assets/Resources/UiNavigation/Prefabs";
        private const string ScenePath =
            "Assets/Samples/UiNavigation/UiPrefabFactoryDemo.unity";

        [MenuItem("Unity Core Lab/Samples/Rebuild UI Prefab Factory Demo")]
        public static void RebuildAssets()
        {
            EnsureFolders();
            CreateViewPrefab(
                "HomeScreen",
                "HOME SCREEN PREFAB",
                false,
                new Color(0.12f, 0.33f, 0.52f, 1f));
            CreateViewPrefab(
                "InventoryScreen",
                "INVENTORY SCREEN PREFAB",
                false,
                new Color(0.18f, 0.48f, 0.38f, 1f));
            CreateViewPrefab(
                "ProfileScreen",
                "PROFILE SCREEN PREFAB",
                false,
                new Color(0.39f, 0.28f, 0.57f, 1f));
            CreateViewPrefab(
                "ShopScreen",
                "SHOP SCREEN PREFAB",
                false,
                new Color(0.55f, 0.35f, 0.16f, 1f));
            CreateViewPrefab(
                "SettingsModal",
                "SETTINGS MODAL PREFAB",
                true,
                new Color(0.16f, 0.19f, 0.24f, 0.98f));
            CreateViewPrefab(
                "ConfirmModal",
                "CONFIRM MODAL PREFAB",
                true,
                new Color(0.45f, 0.18f, 0.22f, 0.98f));

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var demoObject = new GameObject("UI Prefab Factory Demo");
            demoObject.AddComponent<UiPrefabFactoryDemo>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = demoObject;
            Debug.Log($"Rebuilt UI prefab demo assets and {ScenePath}");
        }

        private static void CreateViewPrefab(
            string assetName,
            string templateLabel,
            bool isModal,
            Color color)
        {
            var root = new GameObject(
                assetName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(PrefabUiViewBehaviour));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = isModal
                    ? new Vector2(0.14f, 0.28f)
                    : Vector2.zero;
                rect.anchorMax = isModal
                    ? new Vector2(0.86f, 0.72f)
                    : Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                Image image = root.GetComponent<Image>();
                image.color = color;
                CanvasGroup group = root.GetComponent<CanvasGroup>();

                var textObject = new GameObject(
                    "Title",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                var textRect = (RectTransform)textObject.transform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = new Vector2(0.08f, 0.14f);
                textRect.anchorMax = new Vector2(0.92f, 0.86f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                Text text = textObject.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
                text.fontSize = isModal ? 38 : 50;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                text.text = templateLabel;

                root.GetComponent<PrefabUiViewBehaviour>().ConfigureTemplate(
                    group,
                    text,
                    templateLabel);
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    $"{ResourceRoot}/{assetName}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Resources", "UiNavigation");
            EnsureFolder("Assets/Resources/UiNavigation", "Prefabs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}

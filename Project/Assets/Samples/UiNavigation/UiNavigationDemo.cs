using System;
using UnityEngine;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Samples.UiNavigation
{
    /// <summary>
    /// Lightweight visual adapter for the engine-independent navigation
    /// model. Real view creation and asynchronous lifecycle arrive in 06.2.
    /// </summary>
    public sealed class UiNavigationDemo : MonoBehaviour
    {
        public const int ScreenCapacity = 6;
        public const int ModalCapacity = 3;
        private const int HistoryCapacity = 10;

        private UiNavigationStack _navigation;
        private readonly string[] _history = new string[HistoryCapacity];
        private int _historyStart;
        private int _historyCount;
        private string _status;
        private Vector2 _scrollPosition;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public bool IsInitialized => _navigation != null;

        public UiNavigationStack Navigation => _navigation;

        private void Awake()
        {
            _navigation = new UiNavigationStack(
                ScreenCapacity,
                ModalCapacity);
            _navigation.Changed += HandleNavigationChanged;
            Apply(
                "Create root Home screen",
                _navigation.PushScreen("Home"));
        }

        private void OnDestroy()
        {
            if (_navigation != null)
            {
                _navigation.Changed -= HandleNavigationChanged;
            }
        }

        public UiNavigationResult OpenInventory()
        {
            return Apply(
                "Push Inventory screen",
                _navigation.PushScreen("Inventory"));
        }

        public UiNavigationResult OpenProfile()
        {
            return Apply(
                "Push Profile screen",
                _navigation.PushScreen("Profile"));
        }

        public UiNavigationResult ReplaceWithShop()
        {
            return Apply(
                "Replace top screen with Shop",
                _navigation.ReplaceTopScreen("Shop"));
        }

        public UiNavigationResult OpenSettingsModal()
        {
            return Apply(
                "Push Settings modal",
                _navigation.PushModal("Settings"));
        }

        public UiNavigationResult OpenConfirmModal()
        {
            return Apply(
                "Push Confirm modal",
                _navigation.PushModal("Confirm"));
        }

        public UiNavigationResult NavigateBack()
        {
            UiNavigationResult result = _navigation.Back(out UiRoute removed);
            string action = result == UiNavigationResult.Succeeded
                ? $"Back removed {removed}"
                : "Back";
            return Apply(action, result);
        }

        public void ResetToRoot()
        {
            while (_navigation.Back(out _) == UiNavigationResult.Succeeded)
            {
            }

            _status = "Reset to the protected root screen.";
        }

        private UiNavigationResult Apply(
            string action,
            UiNavigationResult result)
        {
            _status = $"{action}: {result}";
            return result;
        }

        private void HandleNavigationChanged(UiNavigationChange change)
        {
            string previous = string.IsNullOrEmpty(change.PreviousRoute.Id)
                ? string.Empty
                : $" (replaced {change.PreviousRoute.Id})";
            string entry =
                $"r{change.Revision}: {change.Kind} {change.Route}" + previous;
            int writeIndex = (_historyStart + _historyCount) % HistoryCapacity;
            if (_historyCount < HistoryCapacity)
            {
                _historyCount++;
            }
            else
            {
                writeIndex = _historyStart;
                _historyStart = (_historyStart + 1) % HistoryCapacity;
            }

            _history[writeIndex] = entry;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            float panelWidth = Mathf.Min(600f, logicalWidth - 32f);
            GUILayout.BeginArea(
                new Rect(16f, 16f, panelWidth, logicalHeight - 32f),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label(
                "06.1 UI navigation stack baseline",
                _headingStyle);
            GUILayout.Label(
                "State only: view loading and async lifecycle are added in 06.2.",
                _labelStyle);
            GUILayout.Space(10f);

            DrawCurrentState();
            GUILayout.Space(12f);
            GUILayout.Label("Navigation commands", _headingStyle);
            if (GUILayout.Button("Push Inventory screen", _buttonStyle))
            {
                OpenInventory();
            }

            if (GUILayout.Button("Push Profile screen", _buttonStyle))
            {
                OpenProfile();
            }

            if (GUILayout.Button("Replace top with Shop", _buttonStyle))
            {
                ReplaceWithShop();
            }

            if (GUILayout.Button("Open Settings modal", _buttonStyle))
            {
                OpenSettingsModal();
            }

            if (GUILayout.Button("Open Confirm modal", _buttonStyle))
            {
                OpenConfirmModal();
            }

            if (GUILayout.Button("Back (modal first)", _buttonStyle))
            {
                NavigateBack();
            }

            if (GUILayout.Button("Reset to root", _buttonStyle))
            {
                ResetToRoot();
            }

            GUILayout.Space(10f);
            GUILayout.Label(_status ?? "Ready.", _labelStyle);
            GUILayout.Space(12f);
            DrawHistory();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (logicalWidth >= 1000f)
            {
                DrawPreview(new Rect(
                    640f,
                    16f,
                    logicalWidth - 656f,
                    logicalHeight - 32f));
            }

            GUI.matrix = previousMatrix;
        }

        private void DrawCurrentState()
        {
            GUILayout.Label(
                $"Revision: {_navigation.Revision}",
                _labelStyle);
            GUILayout.Label(
                $"Screens: {_navigation.ScreenCount}/" +
                $"{_navigation.ScreenCapacity}",
                _labelStyle);
            for (int index = 0; index < _navigation.ScreenCount; index++)
            {
                string marker = index == _navigation.ScreenCount - 1
                    ? "  <- visible"
                    : string.Empty;
                GUILayout.Label(
                    $"  {index}: {_navigation.GetScreen(index).Id}{marker}",
                    _labelStyle);
            }

            GUILayout.Label(
                $"Modals: {_navigation.ModalCount}/" +
                $"{_navigation.ModalCapacity}",
                _labelStyle);
            for (int index = 0; index < _navigation.ModalCount; index++)
            {
                string marker = index == _navigation.ModalCount - 1
                    ? "  <- input owner"
                    : string.Empty;
                GUILayout.Label(
                    $"  {index}: {_navigation.GetModal(index).Id}{marker}",
                    _labelStyle);
            }
        }

        private void DrawHistory()
        {
            GUILayout.Label("Recent successful changes", _headingStyle);
            for (int index = _historyCount - 1; index >= 0; index--)
            {
                int physicalIndex =
                    (_historyStart + index) % HistoryCapacity;
                GUILayout.Label(_history[physicalIndex], _labelStyle);
            }
        }

        private void DrawPreview(Rect rect)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.10f, 0.15f, 0.23f, 1f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previousColor;

            _navigation.TryGetTopScreen(out UiRoute screen);
            GUI.Label(
                new Rect(rect.x + 32f, rect.y + 28f, rect.width - 64f, 50f),
                $"SCREEN · {screen.Id}",
                _titleStyle);
            GUI.Label(
                new Rect(rect.x + 32f, rect.y + 92f, rect.width - 64f, 100f),
                "Only the top screen is visible. The root Home screen is " +
                "protected from Back.",
                _headingStyle);

            if (_navigation.TryGetTopModal(out UiRoute modal))
            {
                var modalRect = new Rect(
                    rect.x + rect.width * 0.15f,
                    rect.y + rect.height * 0.36f,
                    rect.width * 0.70f,
                    rect.height * 0.30f);
                GUI.color = new Color(0.18f, 0.20f, 0.24f, 1f);
                GUI.Box(modalRect, GUIContent.none);
                GUI.color = previousColor;
                GUI.Label(
                    new Rect(
                        modalRect.x + 24f,
                        modalRect.y + 24f,
                        modalRect.width - 48f,
                        50f),
                    $"MODAL · {modal.Id}",
                    _titleStyle);
                GUI.Label(
                    new Rect(
                        modalRect.x + 24f,
                        modalRect.y + 82f,
                        modalRect.width - 48f,
                        80f),
                    "Back closes the top modal before changing screens.",
                    _headingStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fixedHeight = 42f,
                wordWrap = true
            };
        }
    }
}

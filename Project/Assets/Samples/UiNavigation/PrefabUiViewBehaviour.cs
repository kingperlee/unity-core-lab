using UnityEngine;
using UnityEngine.UI;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Samples.UiNavigation
{
    /// <summary>
    /// Component stored in each UI prefab. It owns presentation only; route
    /// lookup, asynchronous loading and resource ownership live outside it.
    /// </summary>
    public sealed class PrefabUiViewBehaviour : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text title;
        [SerializeField] private string templateLabel;

        public UiRoute Route { get; private set; }

        public string TemplateLabel => templateLabel;

        public bool IsVisible =>
            canvasGroup != null && canvasGroup.alpha > 0.99f;

        public void ConfigureTemplate(
            CanvasGroup group,
            Text titleText,
            string label)
        {
            canvasGroup = group;
            title = titleText;
            templateLabel = label;
        }

        public void Initialize(UiRoute route)
        {
            Route = route;
            if (title != null)
            {
                title.text =
                    $"{templateLabel}\n\n{route.Layer} · {route.Id}\n\n" +
                    "Resources prefab · shared asset lease";
            }

            SetPresentation(false);
        }

        public void Enter()
        {
            SetPresentation(true);
        }

        public void Cover()
        {
            SetPresentation(false);
        }

        public void Resume()
        {
            SetPresentation(true);
        }

        public void Exit()
        {
            SetPresentation(false);
        }

        private void SetPresentation(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}

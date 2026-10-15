using UnityEngine;
using UnityEngine.UI;

namespace UnityCoreLab.Samples.UiVirtualization
{
    public sealed class VirtualizedListRow : MonoBehaviour
    {
        private Image _background;
        private Text _label;

        public int BoundIndex { get; private set; } = -1;

        public int BindCount { get; private set; }

        public void Configure(Image background, Text label)
        {
            _background = background;
            _label = label;
        }

        public void Bind(int index)
        {
            BoundIndex = index;
            BindCount++;
            name = $"Pooled Row {index}";
            _background.color = index % 2 == 0
                ? new Color(0.105f, 0.15f, 0.22f, 1f)
                : new Color(0.13f, 0.185f, 0.265f, 1f);
            _label.text =
                $"#{index + 1:00000}    Virtualized record {index + 1}\n" +
                "The row GameObject is reused when this item leaves the window.";
        }

        public void Hide()
        {
            BoundIndex = -1;
            gameObject.SetActive(false);
        }
    }
}

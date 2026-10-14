using System;

namespace UnityCoreLab.UiNavigation
{
    public readonly struct UiRoute
    {
        public UiRoute(string id, UiLayer layer)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "A UI route identifier is required.",
                    nameof(id));
            }

            if (!Enum.IsDefined(typeof(UiLayer), layer))
            {
                throw new ArgumentOutOfRangeException(nameof(layer));
            }

            Id = id;
            Layer = layer;
        }

        public string Id { get; }

        public UiLayer Layer { get; }

        public override string ToString()
        {
            return $"{Layer}:{Id}";
        }
    }
}

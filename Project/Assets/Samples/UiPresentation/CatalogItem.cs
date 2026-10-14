using System;

namespace UnityCoreLab.Samples.UiPresentation
{
    public sealed class CatalogItem
    {
        public CatalogItem(string id, string title, string detail)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An item ID is required.", nameof(id));
            }

            Id = id;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public string Id { get; }

        public string Title { get; }

        public string Detail { get; }
    }
}

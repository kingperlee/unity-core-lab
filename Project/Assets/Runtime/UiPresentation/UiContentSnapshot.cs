using System;
using System.Collections.Generic;

namespace UnityCoreLab.UiPresentation
{
    /// <summary>
    /// Immutable state rendered by a content view. Items are copied so a source
    /// cannot mutate a snapshot after the presenter publishes it.
    /// </summary>
    public sealed class UiContentSnapshot<TItem>
    {
        private static readonly IReadOnlyList<TItem> NoItems =
            Array.Empty<TItem>();

        private UiContentSnapshot(
            long revision,
            UiContentState state,
            IReadOnlyList<TItem> items,
            string errorMessage)
        {
            Revision = revision;
            State = state;
            Items = items;
            ErrorMessage = errorMessage;
        }

        public long Revision { get; }

        public UiContentState State { get; }

        public IReadOnlyList<TItem> Items { get; }

        public string ErrorMessage { get; }

        public static UiContentSnapshot<TItem> Loading(long revision)
        {
            return new UiContentSnapshot<TItem>(
                revision,
                UiContentState.Loading,
                NoItems,
                null);
        }

        public static UiContentSnapshot<TItem> Content(
            long revision,
            IReadOnlyList<TItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException(
                    "Content state requires at least one item.",
                    nameof(items));
            }

            var copy = new TItem[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                copy[index] = items[index];
            }

            return new UiContentSnapshot<TItem>(
                revision,
                UiContentState.Content,
                copy,
                null);
        }

        public static UiContentSnapshot<TItem> Empty(long revision)
        {
            return new UiContentSnapshot<TItem>(
                revision,
                UiContentState.Empty,
                NoItems,
                null);
        }

        public static UiContentSnapshot<TItem> Error(
            long revision,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException(
                    "An error state requires a message.",
                    nameof(errorMessage));
            }

            return new UiContentSnapshot<TItem>(
                revision,
                UiContentState.Error,
                NoItems,
                errorMessage);
        }
    }
}

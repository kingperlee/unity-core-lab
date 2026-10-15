using System;

namespace UnityCoreLab.UiDialogs
{
    public sealed class UiDialogRequest
    {
        public UiDialogRequest(
            string id,
            UiDialogKind kind,
            string title,
            string message,
            UiDialogPriority priority = UiDialogPriority.Normal)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "A dialog request requires a non-empty ID.",
                    nameof(id));
            }

            Id = id;
            Kind = kind;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Priority = priority;
        }

        public string Id { get; }

        public UiDialogKind Kind { get; }

        public string Title { get; }

        public string Message { get; }

        public UiDialogPriority Priority { get; }
    }
}

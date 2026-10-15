using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityCoreLab.UiDialogs
{
    /// <summary>
    /// Engine-independent dialog owner with one active request, a stable
    /// priority queue, duplicate coalescing, and awaitable completion.
    /// </summary>
    public sealed class UiDialogQueue : IDisposable
    {
        private readonly IUiDialogView _view;
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<Entry> _pending = new List<Entry>();
        private Entry _active;
        private bool _disposed;

        public UiDialogQueue(IUiDialogView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.DecisionRequested += HandleDecisionRequested;
        }

        public UiDialogRequest ActiveRequest => _active?.Request;

        public int PendingCount => _pending.Count;

        public bool IsInputBlocked => _active != null;

        public IReadOnlyList<UiDialogRequest> PendingRequests
        {
            get
            {
                var requests = new UiDialogRequest[_pending.Count];
                for (int index = 0; index < _pending.Count; index++)
                {
                    requests[index] = _pending[index].Request;
                }

                return requests;
            }
        }

        public Task<UiDialogResult> ShowAsync(UiDialogRequest request)
        {
            ThrowIfDisposed();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_entries.TryGetValue(request.Id, out Entry duplicate))
            {
                if (duplicate.Request.Kind != request.Kind)
                {
                    throw new InvalidOperationException(
                        $"Dialog ID '{request.Id}' is already owned by " +
                        $"{duplicate.Request.Kind}; it cannot also identify " +
                        $"{request.Kind}.");
                }

                return duplicate.Completion.Task;
            }

            var entry = new Entry(request);
            _entries.Add(request.Id, entry);
            if (_active == null)
            {
                Activate(entry);
            }
            else
            {
                Enqueue(entry);
            }

            return entry.Completion.Task;
        }

        public Task<UiDialogResult> ShowNoticeAsync(
            string id,
            string title,
            string message,
            UiDialogPriority priority = UiDialogPriority.Normal)
        {
            return ShowAsync(
                new UiDialogRequest(
                    id,
                    UiDialogKind.Notice,
                    title,
                    message,
                    priority));
        }

        public async Task<bool> ShowConfirmAsync(
            string id,
            string title,
            string message,
            UiDialogPriority priority = UiDialogPriority.Normal)
        {
            UiDialogResult result = await ShowAsync(
                new UiDialogRequest(
                    id,
                    UiDialogKind.Confirmation,
                    title,
                    message,
                    priority));
            return result == UiDialogResult.Confirmed;
        }

        public Task<UiDialogResult> ShowBlockingAsync(
            string id,
            string title,
            string message,
            UiDialogPriority priority = UiDialogPriority.High)
        {
            return ShowAsync(
                new UiDialogRequest(
                    id,
                    UiDialogKind.Blocking,
                    title,
                    message,
                    priority));
        }

        public bool Complete(string requestId, UiDialogResult result)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(requestId) ||
                !_entries.TryGetValue(requestId, out Entry entry))
            {
                return false;
            }

            if (ReferenceEquals(entry, _active))
            {
                CompleteActive(result);
                return true;
            }

            _pending.Remove(entry);
            _entries.Remove(entry.Request.Id);
            entry.Completion.TrySetResult(result);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _view.DecisionRequested -= HandleDecisionRequested;

            Entry active = _active;
            _active = null;
            if (active != null)
            {
                _view.Hide();
                active.Completion.TrySetResult(UiDialogResult.Dismissed);
            }

            for (int index = 0; index < _pending.Count; index++)
            {
                _pending[index].Completion.TrySetResult(
                    UiDialogResult.Dismissed);
            }

            _pending.Clear();
            _entries.Clear();
        }

        private void HandleDecisionRequested(UiDialogResult result)
        {
            if (_disposed || _active == null)
            {
                return;
            }

            switch (_active.Request.Kind)
            {
                case UiDialogKind.Notice:
                    if (result == UiDialogResult.Acknowledged)
                    {
                        CompleteActive(result);
                    }

                    break;

                case UiDialogKind.Confirmation:
                    if (result == UiDialogResult.Confirmed ||
                        result == UiDialogResult.Cancelled)
                    {
                        CompleteActive(result);
                    }

                    break;

                case UiDialogKind.Blocking:
                    // A blocking dialog is released only by its operation owner.
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void CompleteActive(UiDialogResult result)
        {
            Entry completed = _active;
            if (completed == null)
            {
                return;
            }

            _active = null;
            _entries.Remove(completed.Request.Id);
            _view.Hide();
            completed.Completion.TrySetResult(result);

            if (_pending.Count > 0)
            {
                Entry next = _pending[0];
                _pending.RemoveAt(0);
                Activate(next);
            }
        }

        private void Activate(Entry entry)
        {
            _active = entry;
            _view.Show(entry.Request);
        }

        private void Enqueue(Entry entry)
        {
            int insertIndex = 0;
            while (insertIndex < _pending.Count &&
                   _pending[insertIndex].Request.Priority >=
                   entry.Request.Priority)
            {
                insertIndex++;
            }

            _pending.Insert(insertIndex, entry);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UiDialogQueue));
            }
        }

        private sealed class Entry
        {
            public Entry(UiDialogRequest request)
            {
                Request = request;
                Completion = new TaskCompletionSource<UiDialogResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public UiDialogRequest Request { get; }

            public TaskCompletionSource<UiDialogResult> Completion { get; }
        }
    }
}

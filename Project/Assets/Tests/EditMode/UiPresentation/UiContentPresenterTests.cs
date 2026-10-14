using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityCoreLab.UiPresentation;

namespace UnityCoreLab.Tests.EditMode.UiPresentation
{
    public sealed class UiContentPresenterTests
    {
        [UnityTest]
        public IEnumerator Refresh_RendersLoadingThenContent()
        {
            var view = new RecordingView<string>();
            var source = new ControlledSource<string>();
            using (var presenter = new UiContentPresenter<string>(view, source))
            {
                Task refresh = presenter.RefreshAsync();

                Assert.That(view.Last.State, Is.EqualTo(UiContentState.Loading));
                Assert.That(view.Last.Revision, Is.EqualTo(1));

                source.Requests[0].Complete("Alpha", "Beta");
                yield return Await(refresh);

                Assert.That(view.Last.State, Is.EqualTo(UiContentState.Content));
                Assert.That(view.Last.Items, Is.EqualTo(new[] { "Alpha", "Beta" }));
                Assert.That(view.Snapshots.Count, Is.EqualTo(2));
                Assert.That(presenter.IsLoading, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Refresh_MapsEmptyAndFailureToExplicitStates()
        {
            var view = new RecordingView<string>();
            var source = new ControlledSource<string>();
            using (var presenter = new UiContentPresenter<string>(view, source))
            {
                Task empty = presenter.RefreshAsync();
                source.Requests[0].Complete();
                yield return Await(empty);

                Assert.That(view.Last.State, Is.EqualTo(UiContentState.Empty));
                Assert.That(view.Last.Revision, Is.EqualTo(1));

                Task failed = presenter.RefreshAsync();
                source.Requests[1].Fail("catalog unavailable");
                yield return Await(failed);

                Assert.That(view.Last.State, Is.EqualTo(UiContentState.Error));
                Assert.That(view.Last.ErrorMessage, Is.EqualTo("catalog unavailable"));
                Assert.That(view.Last.Revision, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator NewRefresh_CancelsAndSuppressesOlderLateResult()
        {
            var view = new RecordingView<string>();
            var source = new ControlledSource<string>();
            using (var presenter = new UiContentPresenter<string>(view, source))
            {
                Task older = presenter.RefreshAsync();
                ControlledSource<string>.Request olderRequest = source.Requests[0];

                Task newer = presenter.RefreshAsync();
                ControlledSource<string>.Request newerRequest = source.Requests[1];

                Assert.That(
                    olderRequest.CancellationToken.IsCancellationRequested,
                    Is.True);

                newerRequest.Complete("Newest");
                yield return Await(newer);
                olderRequest.Complete("Stale");
                yield return Await(older);

                Assert.That(view.Last.Revision, Is.EqualTo(2));
                Assert.That(view.Last.Items, Is.EqualTo(new[] { "Newest" }));
                Assert.That(view.Snapshots.Count, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator Dispose_CancelsRequestSuppressesResultAndDetachesView()
        {
            var view = new RecordingView<string>();
            var source = new ControlledSource<string>();
            var presenter = new UiContentPresenter<string>(view, source);
            Task refresh = presenter.RefreshAsync();
            ControlledSource<string>.Request request = source.Requests[0];

            presenter.Dispose();

            Assert.That(request.CancellationToken.IsCancellationRequested, Is.True);
            request.Complete("Too late");
            yield return Await(refresh);

            Assert.That(view.Last.State, Is.EqualTo(UiContentState.Loading));
            view.RequestRefresh();
            Assert.That(source.Requests.Count, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => presenter.RefreshAsync());
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.GetBaseException() ??
                      new InvalidOperationException("Task failed without an exception.");
            }

            Assert.That(task.IsCanceled, Is.False);
        }

        private sealed class RecordingView<TItem> : IUiContentView<TItem>
        {
            public event Action RefreshRequested;

            public List<UiContentSnapshot<TItem>> Snapshots { get; } =
                new List<UiContentSnapshot<TItem>>();

            public UiContentSnapshot<TItem> Last =>
                Snapshots[Snapshots.Count - 1];

            public void Render(UiContentSnapshot<TItem> snapshot)
            {
                Snapshots.Add(snapshot);
            }

            public void RequestRefresh()
            {
                RefreshRequested?.Invoke();
            }
        }

        private sealed class ControlledSource<TItem> : IUiContentSource<TItem>
        {
            public List<Request> Requests { get; } = new List<Request>();

            public Task<IReadOnlyList<TItem>> LoadAsync(
                CancellationToken cancellationToken)
            {
                var request = new Request(cancellationToken);
                Requests.Add(request);
                return request.Task;
            }

            public sealed class Request
            {
                private readonly TaskCompletionSource<IReadOnlyList<TItem>>
                    _completion =
                        new TaskCompletionSource<IReadOnlyList<TItem>>(
                            TaskCreationOptions.RunContinuationsAsynchronously);

                public Request(CancellationToken cancellationToken)
                {
                    CancellationToken = cancellationToken;
                }

                public CancellationToken CancellationToken { get; }

                public Task<IReadOnlyList<TItem>> Task => _completion.Task;

                public void Complete(params TItem[] items)
                {
                    _completion.TrySetResult(items);
                }

                public void Fail(string message)
                {
                    _completion.TrySetException(
                        new InvalidOperationException(message));
                }
            }
        }
    }
}

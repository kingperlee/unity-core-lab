using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Tests.EditMode.UiNavigation
{
    public sealed class UiViewLifecycleCoordinatorTests
    {
        [UnityTest]
        public IEnumerator PushAndBack_CoverExitDisposeAndResumeInOrder()
        {
            var navigation = new UiNavigationStack(4, 2);
            var factory = new ControlledFactory();
            using (var views = new UiViewLifecycleCoordinator(navigation, factory))
            {
                navigation.PushScreen("Home");
                FakeView home = factory.Complete("Home");
                yield return WaitUntil(() => views.ActiveScreenId == "Home");

                navigation.PushScreen("Inventory");
                FakeView inventory = factory.Complete("Inventory");
                yield return WaitUntil(() => views.ActiveScreenId == "Inventory");

                Assert.That(home.CoverCount, Is.EqualTo(1));
                Assert.That(inventory.EnterCount, Is.EqualTo(1));

                navigation.Back(out _);

                Assert.That(views.ActiveScreenId, Is.EqualTo("Home"));
                Assert.That(inventory.ExitCount, Is.EqualTo(1));
                Assert.That(inventory.DisposeCount, Is.EqualTo(1));
                Assert.That(home.ResumeCount, Is.EqualTo(1));
                Assert.That(home.DisposeCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator BackDuringLoad_CancelsAndDisposesLateFactoryResult()
        {
            var navigation = new UiNavigationStack(4, 2);
            var factory = new ControlledFactory();
            using (var views = new UiViewLifecycleCoordinator(navigation, factory))
            {
                navigation.PushScreen("Home");
                FakeView home = factory.Complete("Home");
                yield return WaitUntil(() => views.ActiveScreenId == "Home");

                navigation.PushScreen("SlowDetails");
                ControlledFactory.Request request = factory.Get("SlowDetails");
                navigation.Back(out UiRoute removed);

                Assert.That(removed.Id, Is.EqualTo("SlowDetails"));
                Assert.That(request.CancellationToken.IsCancellationRequested, Is.True);
                Assert.That(views.ActiveScreenId, Is.EqualTo("Home"));

                FakeView lateView = request.Complete();
                yield return WaitUntil(() => lateView.DisposeCount == 1);

                Assert.That(lateView.EnterCount, Is.Zero);
                Assert.That(home.CoverCount, Is.Zero);
                Assert.That(views.TrackedViewCount, Is.EqualTo(1));
                Assert.That(views.TryGetState(
                    "SlowDetails",
                    UiLayer.Screen,
                    out _), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Modal_CoversAndResumesOnlyThePreviousModal()
        {
            var navigation = new UiNavigationStack(4, 3);
            var factory = new ControlledFactory();
            using (var views = new UiViewLifecycleCoordinator(navigation, factory))
            {
                navigation.PushScreen("Home");
                FakeView home = factory.Complete("Home");
                yield return WaitUntil(() => views.ActiveScreenId == "Home");

                navigation.PushModal("Settings");
                FakeView settings = factory.Complete("Settings");
                yield return WaitUntil(() => views.ActiveModalId == "Settings");

                navigation.PushModal("Confirm");
                FakeView confirm = factory.Complete("Confirm");
                yield return WaitUntil(() => views.ActiveModalId == "Confirm");

                Assert.That(settings.CoverCount, Is.EqualTo(1));
                Assert.That(home.CoverCount, Is.Zero);

                navigation.Back(out _);

                Assert.That(views.ActiveModalId, Is.EqualTo("Settings"));
                Assert.That(settings.ResumeCount, Is.EqualTo(1));
                Assert.That(confirm.DisposeCount, Is.EqualTo(1));
                Assert.That(views.ActiveScreenId, Is.EqualTo("Home"));
            }
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            const int maximumFrames = 120;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("The asynchronous condition did not complete in time.");
        }

        private sealed class ControlledFactory : IAsyncUiViewFactory
        {
            private readonly Dictionary<string, Request> _requests =
                new Dictionary<string, Request>(StringComparer.Ordinal);

            public Task<IUiView> CreateAsync(
                UiRoute route,
                CancellationToken cancellationToken)
            {
                var request = new Request(route, cancellationToken);
                _requests.Add(route.Id, request);
                return request.Task;
            }

            public Request Get(string routeId)
            {
                return _requests[routeId];
            }

            public FakeView Complete(string routeId)
            {
                return Get(routeId).Complete();
            }

            public sealed class Request
            {
                private readonly TaskCompletionSource<IUiView> _completion =
                    new TaskCompletionSource<IUiView>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                public Request(
                    UiRoute route,
                    CancellationToken cancellationToken)
                {
                    Route = route;
                    CancellationToken = cancellationToken;
                }

                public UiRoute Route { get; }

                public CancellationToken CancellationToken { get; }

                public Task<IUiView> Task => _completion.Task;

                public FakeView Complete()
                {
                    var view = new FakeView(Route);
                    _completion.TrySetResult(view);
                    return view;
                }
            }
        }

        private sealed class FakeView : IUiView
        {
            public FakeView(UiRoute route)
            {
                Route = route;
            }

            public UiRoute Route { get; }

            public int EnterCount { get; private set; }

            public int CoverCount { get; private set; }

            public int ResumeCount { get; private set; }

            public int ExitCount { get; private set; }

            public int DisposeCount { get; private set; }

            public void Enter()
            {
                EnterCount++;
            }

            public void Cover()
            {
                CoverCount++;
            }

            public void Resume()
            {
                ResumeCount++;
            }

            public void Exit()
            {
                ExitCount++;
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}

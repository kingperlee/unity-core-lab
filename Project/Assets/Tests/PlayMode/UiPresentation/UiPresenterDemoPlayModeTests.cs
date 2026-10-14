using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.UiPresentation;
using UnityCoreLab.UiPresentation;

namespace UnityCoreLab.Tests.PlayMode.UiPresentation
{
    public sealed class UiPresenterDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_RendersStatesCancelsSupersededWorkAndRecreates()
        {
            var root = new GameObject("UI Presenter Demo Test");
            var demo = root.AddComponent<UiPresenterDemo>();

            yield return WaitUntil(
                () => demo.View.CurrentState == UiContentState.Content);
            Assert.That(demo.View.ContentCount, Is.EqualTo(3));
            Assert.That(demo.View.RenderedText, Does.Contain("Passive Unity view"));
            Assert.That(demo.View.IsInteractive, Is.True);

            demo.StartSlowRequest();
            yield return null;
            Assert.That(demo.View.CurrentState, Is.EqualTo(UiContentState.Loading));

            demo.LoadEmpty();
            yield return WaitUntil(
                () => demo.View.CurrentState == UiContentState.Empty);
            Assert.That(demo.CancellationCount, Is.EqualTo(1));
            Assert.That(demo.View.ContentCount, Is.Zero);

            demo.LoadError();
            yield return WaitUntil(
                () => demo.View.CurrentState == UiContentState.Error);
            Assert.That(
                demo.View.RenderedText,
                Does.Contain("Simulated catalog service failure"));

            demo.StartSlowRequest();
            yield return null;
            long loadingRevision = demo.View.CurrentRevision;
            int renderCount = demo.View.RenderCount;
            demo.DisposePresenter();
            yield return WaitUntil(() => demo.CancellationCount == 2);

            Assert.That(demo.HasPresenter, Is.False);
            Assert.That(demo.View.IsInteractive, Is.False);
            Assert.That(demo.View.CurrentRevision, Is.EqualTo(loadingRevision));
            Assert.That(demo.View.RenderCount, Is.EqualTo(renderCount));

            demo.RecreatePresenter();
            yield return WaitUntil(
                () => demo.View.CurrentState == UiContentState.Content &&
                      demo.View.CurrentRevision == 1);

            Assert.That(demo.HasPresenter, Is.True);
            Assert.That(demo.View.IsInteractive, Is.True);

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True,
                "The presenter demo did not reach the expected state in time.");
        }
    }
}

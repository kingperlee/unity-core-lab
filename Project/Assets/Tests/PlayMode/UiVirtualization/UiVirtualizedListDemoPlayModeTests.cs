using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.UiVirtualization;
using UnityCoreLab.UiVirtualization;

namespace UnityCoreLab.Tests.PlayMode.UiVirtualization
{
    public sealed class UiVirtualizedListDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_ReusesBoundedRowsAcrossLargeDatasetAndShrink()
        {
            var root = new GameObject("UI Virtualized List Demo Test");
            var demo = root.AddComponent<UiVirtualizedListDemo>();
            yield return null;

            Assert.That(demo.ItemCount, Is.EqualTo(1000));
            Assert.That(demo.CreatedRowCount, Is.GreaterThan(0));
            Assert.That(demo.CreatedRowCount, Is.LessThan(40));
            Assert.That(demo.ActiveRowsMatchCurrentRange(), Is.True);

            int originalRowCount = demo.CreatedRowCount;
            int originalBindings = demo.TotalBindOperations;
            demo.SetItemCount(10000);
            demo.JumpToIndex(5000, VirtualizedListAlignment.Center);
            yield return null;

            Assert.That(demo.CreatedRowCount, Is.EqualTo(originalRowCount));
            Assert.That(demo.CurrentRange.Contains(5000), Is.True);
            Assert.That(
                demo.TotalBindOperations,
                Is.GreaterThan(originalBindings));
            Assert.That(demo.ActiveRowsMatchCurrentRange(), Is.True);

            demo.SetItemCount(8);
            yield return null;

            Assert.That(demo.ScrollOffset, Is.Zero);
            Assert.That(demo.ContentExtent, Is.EqualTo(8 * 72f));
            Assert.That(demo.CreatedRowCount, Is.EqualTo(originalRowCount));
            Assert.That(demo.ActiveRowCount, Is.EqualTo(8));
            Assert.That(demo.ActiveRowsMatchCurrentRange(), Is.True);

            Object.Destroy(root);
            yield return null;
        }
    }
}

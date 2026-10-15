using System;
using NUnit.Framework;
using UnityCoreLab.UiVirtualization;

namespace UnityCoreLab.Tests.EditMode.UiVirtualization
{
    public sealed class FixedHeightVirtualListTests
    {
        [Test]
        public void Constructor_RejectsInvalidGeometryAndBuffer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FixedHeightVirtualList(0f, 200f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FixedHeightVirtualList(50f, 0f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FixedHeightVirtualList(50f, 200f, -1));
        }

        [Test]
        public void EmptyList_HasNoRangeViewsOrScrollExtent()
        {
            var list = new FixedHeightVirtualList(50f, 200f, 1);

            Assert.That(list.ContentExtent, Is.Zero);
            Assert.That(list.MaxScrollOffset, Is.Zero);
            Assert.That(list.RequiredViewCount, Is.Zero);
            Assert.That(list.VisibleRange, Is.EqualTo(
                new VirtualizedListRange(0, 0)));
        }

        [Test]
        public void TopWindow_AddsBufferWithoutCrossingStart()
        {
            var list = new FixedHeightVirtualList(50f, 200f, 1);
            list.SetItemCount(100);

            Assert.That(list.RequiredViewCount, Is.EqualTo(7));
            Assert.That(list.VisibleRange, Is.EqualTo(
                new VirtualizedListRange(0, 5)));
        }

        [Test]
        public void MiddleAndBottomWindows_ClampToDatasetBounds()
        {
            var list = new FixedHeightVirtualList(50f, 200f, 1);
            list.SetItemCount(100);

            list.SetScrollOffset(125f);
            Assert.That(list.VisibleRange, Is.EqualTo(
                new VirtualizedListRange(1, 8)));

            list.SetScrollOffset(float.MaxValue);
            Assert.That(list.ScrollOffset, Is.EqualTo(4800f));
            Assert.That(list.VisibleRange, Is.EqualTo(
                new VirtualizedListRange(95, 100)));
        }

        [Test]
        public void ScrollToIndex_HonorsAlignmentAndKeepsItemInWindow()
        {
            var list = new FixedHeightVirtualList(50f, 200f, 1);
            list.SetItemCount(100);

            list.ScrollToIndex(50, VirtualizedListAlignment.Center);
            Assert.That(list.ScrollOffset, Is.EqualTo(2425f));
            Assert.That(list.VisibleRange.Contains(50), Is.True);

            list.ScrollToIndex(99, VirtualizedListAlignment.End);
            Assert.That(list.ScrollOffset, Is.EqualTo(list.MaxScrollOffset));
            Assert.That(list.VisibleRange.Contains(99), Is.True);
        }

        [Test]
        public void DatasetAndViewportChanges_ClampOffsetAndRecalculateCapacity()
        {
            var list = new FixedHeightVirtualList(50f, 200f, 1);
            list.SetItemCount(100);
            list.SetScrollOffset(2000f);

            list.SetItemCount(3);
            Assert.That(list.ScrollOffset, Is.Zero);
            Assert.That(list.RequiredViewCount, Is.EqualTo(3));
            Assert.That(list.VisibleRange, Is.EqualTo(
                new VirtualizedListRange(0, 3)));

            list.SetItemCount(100);
            list.SetViewportExtent(300f);
            Assert.That(list.RequiredViewCount, Is.EqualTo(9));
            Assert.That(list.MaxScrollOffset, Is.EqualTo(4700f));
        }
    }
}

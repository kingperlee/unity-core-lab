using System;

namespace UnityCoreLab.UiVirtualization
{
    /// <summary>
    /// Engine-independent visible-range model for a fixed-height vertical list.
    /// </summary>
    public sealed class FixedHeightVirtualList
    {
        private readonly float _itemExtent;
        private readonly int _bufferItems;
        private float _viewportExtent;
        private int _itemCount;
        private float _scrollOffset;

        public FixedHeightVirtualList(
            float itemExtent,
            float viewportExtent,
            int bufferItems)
        {
            if (itemExtent <= 0f || float.IsNaN(itemExtent))
            {
                throw new ArgumentOutOfRangeException(nameof(itemExtent));
            }

            if (viewportExtent <= 0f || float.IsNaN(viewportExtent))
            {
                throw new ArgumentOutOfRangeException(nameof(viewportExtent));
            }

            if (bufferItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferItems));
            }

            _itemExtent = itemExtent;
            _viewportExtent = viewportExtent;
            _bufferItems = bufferItems;
        }

        public float ItemExtent => _itemExtent;

        public float ViewportExtent => _viewportExtent;

        public int BufferItems => _bufferItems;

        public int ItemCount => _itemCount;

        public float ScrollOffset => _scrollOffset;

        public float ContentExtent => _itemCount * _itemExtent;

        public float MaxScrollOffset =>
            Math.Max(0f, ContentExtent - _viewportExtent);

        public int RequiredViewCount
        {
            get
            {
                if (_itemCount == 0)
                {
                    return 0;
                }

                int visibleCapacity =
                    (int)Math.Ceiling(_viewportExtent / _itemExtent) + 1;
                return Math.Min(
                    _itemCount,
                    visibleCapacity + (_bufferItems * 2));
            }
        }

        public VirtualizedListRange VisibleRange
        {
            get
            {
                if (_itemCount == 0)
                {
                    return new VirtualizedListRange(0, 0);
                }

                int firstVisible = Math.Min(
                    _itemCount - 1,
                    (int)Math.Floor(_scrollOffset / _itemExtent));
                int lastVisibleExclusive = Math.Min(
                    _itemCount,
                    (int)Math.Ceiling(
                        (_scrollOffset + _viewportExtent) / _itemExtent));
                int start = Math.Max(0, firstVisible - _bufferItems);
                int end = Math.Min(
                    _itemCount,
                    lastVisibleExclusive + _bufferItems);
                return new VirtualizedListRange(start, end);
            }
        }

        public void SetItemCount(int itemCount)
        {
            if (itemCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }

            _itemCount = itemCount;
            _scrollOffset = ClampOffset(_scrollOffset);
        }

        public void SetViewportExtent(float viewportExtent)
        {
            if (viewportExtent <= 0f || float.IsNaN(viewportExtent))
            {
                throw new ArgumentOutOfRangeException(nameof(viewportExtent));
            }

            _viewportExtent = viewportExtent;
            _scrollOffset = ClampOffset(_scrollOffset);
        }

        public void SetScrollOffset(float scrollOffset)
        {
            if (float.IsNaN(scrollOffset))
            {
                throw new ArgumentOutOfRangeException(nameof(scrollOffset));
            }

            _scrollOffset = ClampOffset(scrollOffset);
        }

        public void ScrollToIndex(
            int index,
            VirtualizedListAlignment alignment)
        {
            if (index < 0 || index >= _itemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            float itemTop = index * _itemExtent;
            float target;
            switch (alignment)
            {
                case VirtualizedListAlignment.Start:
                    target = itemTop;
                    break;
                case VirtualizedListAlignment.Center:
                    target = itemTop + (_itemExtent * 0.5f) -
                             (_viewportExtent * 0.5f);
                    break;
                case VirtualizedListAlignment.End:
                    target = itemTop + _itemExtent - _viewportExtent;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(alignment));
            }

            _scrollOffset = ClampOffset(target);
        }

        public float GetItemOffset(int index)
        {
            if (index < 0 || index >= _itemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index * _itemExtent;
        }

        private float ClampOffset(float offset)
        {
            return Math.Max(0f, Math.Min(offset, MaxScrollOffset));
        }
    }
}

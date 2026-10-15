using System;

namespace UnityCoreLab.UiVirtualization
{
    public readonly struct VirtualizedListRange : IEquatable<VirtualizedListRange>
    {
        public VirtualizedListRange(int startIndex, int endExclusive)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (endExclusive < startIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endExclusive));
            }

            StartIndex = startIndex;
            EndExclusive = endExclusive;
        }

        public int StartIndex { get; }

        public int EndExclusive { get; }

        public int Count => EndExclusive - StartIndex;

        public bool Contains(int index)
        {
            return index >= StartIndex && index < EndExclusive;
        }

        public bool Equals(VirtualizedListRange other)
        {
            return StartIndex == other.StartIndex &&
                   EndExclusive == other.EndExclusive;
        }

        public override bool Equals(object obj)
        {
            return obj is VirtualizedListRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartIndex * 397) ^ EndExclusive;
            }
        }

        public override string ToString()
        {
            return $"[{StartIndex}, {EndExclusive})";
        }
    }
}

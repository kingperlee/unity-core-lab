using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Fixed-capacity in-memory source used by the safe interactive sample.
    /// Production builds replace this with a native or crash-SDK adapter.
    /// </summary>
    public sealed class BufferedPlatformIncidentSource : IPlatformIncidentSource
    {
        private readonly PlatformIncidentRecord[] _records;
        private int _startIndex;
        private int _count;

        public BufferedPlatformIncidentSource(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _records = new PlatformIncidentRecord[capacity];
        }

        public int Capacity => _records.Length;

        public int PendingCount => _count;

        public bool TryEnqueue(PlatformIncidentRecord incident)
        {
            if (_count == _records.Length)
            {
                return false;
            }

            int writeIndex = (_startIndex + _count) % _records.Length;
            _records[writeIndex] = incident;
            _count++;
            return true;
        }

        public bool TryPeek(out PlatformIncidentRecord incident)
        {
            if (_count == 0)
            {
                incident = default;
                return false;
            }

            incident = _records[_startIndex];
            return true;
        }

        public void Acknowledge(string incidentId)
        {
            if (_count == 0)
            {
                throw new InvalidOperationException(
                    "There is no pending platform incident.");
            }

            if (!string.Equals(
                    _records[_startIndex].IncidentId,
                    incidentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only the current head incident can be acknowledged.");
            }

            _records[_startIndex] = default;
            _startIndex = (_startIndex + 1) % _records.Length;
            _count--;
        }

        public void Clear()
        {
            Array.Clear(_records, 0, _records.Length);
            _startIndex = 0;
            _count = 0;
        }
    }
}

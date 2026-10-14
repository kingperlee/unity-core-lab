using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Fixed-capacity, thread-safe inbox for callbacks that can arrive in
    /// parallel. Oldest pending entries are dropped under sustained pressure.
    /// </summary>
    public sealed class RuntimeLogInbox
    {
        private readonly object _gate = new object();
        private readonly RuntimeLogEntry[] _entries;
        private readonly int _maximumMessageCharacters;
        private readonly int _maximumStackTraceCharacters;
        private int _startIndex;
        private int _count;
        private long _receivedCount;
        private long _drainedCount;
        private long _droppedCount;
        private long _truncatedCount;

        public RuntimeLogInbox(
            int capacity,
            int maximumMessageCharacters,
            int maximumStackTraceCharacters)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (maximumMessageCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumMessageCharacters));
            }

            if (maximumStackTraceCharacters < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumStackTraceCharacters));
            }

            _entries = new RuntimeLogEntry[capacity];
            _maximumMessageCharacters = maximumMessageCharacters;
            _maximumStackTraceCharacters = maximumStackTraceCharacters;
        }

        public int Capacity => _entries.Length;

        public void Enqueue(
            DiagnosticSeverity severity,
            string message,
            string stackTrace)
        {
            bool messageTruncated;
            bool stackTraceTruncated;
            string normalizedMessage = string.IsNullOrWhiteSpace(message)
                ? "(empty log message)"
                : message;
            string boundedMessage = Bound(
                normalizedMessage,
                _maximumMessageCharacters,
                out messageTruncated);
            string boundedStackTrace = Bound(
                stackTrace,
                _maximumStackTraceCharacters,
                out stackTraceTruncated);
            var entry = new RuntimeLogEntry(
                severity,
                boundedMessage,
                boundedStackTrace);

            lock (_gate)
            {
                _receivedCount++;
                if (messageTruncated || stackTraceTruncated)
                {
                    _truncatedCount++;
                }

                int writeIndex;
                if (_count < _entries.Length)
                {
                    writeIndex = (_startIndex + _count) % _entries.Length;
                    _count++;
                }
                else
                {
                    writeIndex = _startIndex;
                    _startIndex = (_startIndex + 1) % _entries.Length;
                    _droppedCount++;
                }

                _entries[writeIndex] = entry;
            }
        }

        public bool TryDequeue(out RuntimeLogEntry entry)
        {
            lock (_gate)
            {
                if (_count == 0)
                {
                    entry = default;
                    return false;
                }

                entry = _entries[_startIndex];
                _entries[_startIndex] = default;
                _startIndex = (_startIndex + 1) % _entries.Length;
                _count--;
                _drainedCount++;
                return true;
            }
        }

        public RuntimeLogCaptureStatistics GetStatistics()
        {
            lock (_gate)
            {
                return new RuntimeLogCaptureStatistics(
                    _entries.Length,
                    _count,
                    _receivedCount,
                    _drainedCount,
                    _droppedCount,
                    _truncatedCount);
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_entries, 0, _entries.Length);
                _startIndex = 0;
                _count = 0;
                _receivedCount = 0;
                _drainedCount = 0;
                _droppedCount = 0;
                _truncatedCount = 0;
            }
        }

        private static string Bound(
            string value,
            int maximumCharacters,
            out bool truncated)
        {
            string normalized = value ?? string.Empty;
            if (normalized.Length <= maximumCharacters)
            {
                truncated = false;
                return normalized;
            }

            truncated = true;
            return normalized.Substring(0, maximumCharacters);
        }
    }
}

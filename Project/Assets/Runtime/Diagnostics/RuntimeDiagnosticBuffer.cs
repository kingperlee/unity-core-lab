using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Fixed-capacity chronological event buffer. Recording never grows the
    /// backing storage; snapshot allocation is left to the caller.
    /// </summary>
    public sealed class RuntimeDiagnosticBuffer
    {
        private readonly RuntimeDiagnosticEvent[] _events;
        private int _startIndex;
        private int _count;
        private long _nextSequence = 1;

        public RuntimeDiagnosticBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _events = new RuntimeDiagnosticEvent[capacity];
        }

        public int Capacity => _events.Length;

        public int Count => _count;

        public long OverwrittenCount { get; private set; }

        public RuntimeDiagnosticEvent this[int chronologicalIndex]
        {
            get
            {
                if (chronologicalIndex < 0 || chronologicalIndex >= _count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(chronologicalIndex));
                }

                int physicalIndex =
                    (_startIndex + chronologicalIndex) % _events.Length;
                return _events[physicalIndex];
            }
        }

        public RuntimeDiagnosticEvent Add(
            double timestampSeconds,
            string category,
            DiagnosticSeverity severity,
            string message,
            double durationMilliseconds = 0d,
            int occurrenceCount = 1)
        {
            ValidateFiniteNonNegative(
                timestampSeconds,
                nameof(timestampSeconds));
            ValidateFiniteNonNegative(
                durationMilliseconds,
                nameof(durationMilliseconds));
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException(
                    "A diagnostic category is required.",
                    nameof(category));
            }

            if (!Enum.IsDefined(typeof(DiagnosticSeverity), severity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(severity),
                    severity,
                    null);
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "A diagnostic message is required.",
                    nameof(message));
            }

            if (occurrenceCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
            }

            var diagnosticEvent = new RuntimeDiagnosticEvent(
                _nextSequence++,
                timestampSeconds,
                category,
                severity,
                message,
                durationMilliseconds,
                occurrenceCount);

            int writeIndex;
            if (_count < _events.Length)
            {
                writeIndex = (_startIndex + _count) % _events.Length;
                _count++;
            }
            else
            {
                writeIndex = _startIndex;
                _startIndex = (_startIndex + 1) % _events.Length;
                OverwrittenCount++;
            }

            _events[writeIndex] = diagnosticEvent;
            return diagnosticEvent;
        }

        public int CopyTo(RuntimeDiagnosticEvent[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length < _count)
            {
                throw new ArgumentException(
                    "The destination is smaller than the current event count.",
                    nameof(destination));
            }

            for (int index = 0; index < _count; index++)
            {
                destination[index] = this[index];
            }

            return _count;
        }

        public void Clear()
        {
            Array.Clear(_events, 0, _events.Length);
            _startIndex = 0;
            _count = 0;
            OverwrittenCount = 0;
        }

        private static void ValidateFiniteNonNegative(
            double value,
            string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}

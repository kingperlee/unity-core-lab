using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Deterministic exponential-backoff policy. Jitter belongs in a transport
    /// adapter so pure state-machine tests remain repeatable.
    /// </summary>
    public sealed class DiagnosticUploadPolicy
    {
        public DiagnosticUploadPolicy(
            int maximumAttempts,
            double initialRetryDelaySeconds,
            double maximumRetryDelaySeconds)
        {
            if (maximumAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            }

            ValidatePositiveFinite(
                initialRetryDelaySeconds,
                nameof(initialRetryDelaySeconds));
            ValidatePositiveFinite(
                maximumRetryDelaySeconds,
                nameof(maximumRetryDelaySeconds));
            if (maximumRetryDelaySeconds < initialRetryDelaySeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumRetryDelaySeconds));
            }

            MaximumAttempts = maximumAttempts;
            InitialRetryDelaySeconds = initialRetryDelaySeconds;
            MaximumRetryDelaySeconds = maximumRetryDelaySeconds;
        }

        public int MaximumAttempts { get; }

        public double InitialRetryDelaySeconds { get; }

        public double MaximumRetryDelaySeconds { get; }

        public double GetRetryDelaySeconds(int completedAttemptCount)
        {
            if (completedAttemptCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedAttemptCount));
            }

            double delay = InitialRetryDelaySeconds;
            for (int index = 1; index < completedAttemptCount; index++)
            {
                if (delay >= MaximumRetryDelaySeconds / 2d)
                {
                    return MaximumRetryDelaySeconds;
                }

                delay *= 2d;
            }

            return Math.Min(delay, MaximumRetryDelaySeconds);
        }

        private static void ValidatePositiveFinite(
            double value,
            string parameterName)
        {
            if (value <= 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}

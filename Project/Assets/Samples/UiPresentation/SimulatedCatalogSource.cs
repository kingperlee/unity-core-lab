using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityCoreLab.UiPresentation;

namespace UnityCoreLab.Samples.UiPresentation
{
    /// <summary>
    /// Deterministic sample source used to exercise every presenter state.
    /// A real project can replace it with HTTP, local cache or domain services.
    /// </summary>
    public sealed class SimulatedCatalogSource :
        IUiContentSource<CatalogItem>
    {
        private int _requestCount;
        private int _cancellationCount;

        public CatalogLoadScenario Scenario { get; set; } =
            CatalogLoadScenario.Content;

        public int RequestCount => Volatile.Read(ref _requestCount);

        public int CancellationCount => Volatile.Read(ref _cancellationCount);

        public async Task<IReadOnlyList<CatalogItem>> LoadAsync(
            CancellationToken cancellationToken)
        {
            CatalogLoadScenario scenario = Scenario;
            Interlocked.Increment(ref _requestCount);

            int delayMilliseconds = scenario == CatalogLoadScenario.SlowContent
                ? 2200
                : 420;
            try
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref _cancellationCount);
                throw;
            }

            switch (scenario)
            {
                case CatalogLoadScenario.Content:
                case CatalogLoadScenario.SlowContent:
                    return new[]
                    {
                        new CatalogItem(
                            "runtime",
                            "Runtime architecture",
                            "Engine-independent state and cancellation policy"),
                        new CatalogItem(
                            "unity-view",
                            "Passive Unity view",
                            "uGUI only renders immutable presenter snapshots"),
                        new CatalogItem(
                            "tests",
                            "Deterministic tests",
                            "Race, empty, error and disposal paths are covered")
                    };

                case CatalogLoadScenario.Empty:
                    return Array.Empty<CatalogItem>();

                case CatalogLoadScenario.Error:
                    throw new InvalidOperationException(
                        "Simulated catalog service failure.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario));
            }
        }
    }
}

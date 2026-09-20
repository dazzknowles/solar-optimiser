using System.Net;

namespace SolarOptimiser.Providers.FoxESS
{
    /// <summary>
    /// The outcome of one FoxESS HTTP call, carrying everything <see cref="FoxESSTelemetryProvider"/> needs to
    /// build a <c>ProviderOutcome</c> and the sanitized evidence fields (SOL-T-503) without leaking the raw
    /// text through more than one place.
    /// </summary>
    public sealed class FoxESSApiCallResult<TResponse> where TResponse : class
    {
        public HttpStatusCode? HttpStatusCode { get; init; }

        public TResponse? Parsed { get; init; }

        public bool TransportFailed { get; init; }

        public bool ParseFailed { get; init; }

        public string RequestSanitized { get; init; } = string.Empty;

        public string RawResponseSanitized { get; init; } = string.Empty;
    }
}

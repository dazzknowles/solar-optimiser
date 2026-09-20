using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net;

namespace SolarOptimiser.Host.Security
{
    /// <summary>
    /// SOL-T-1101: deny-by-default LAN allowlist. Checks the actual socket peer address only — no
    /// <c>X-Forwarded-For</c> or similar header is trusted, since there is no reverse proxy in front of this
    /// service. The application-level check is the sole control.
    /// </summary>
    public sealed class CidrAllowlistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IReadOnlyList<CidrRange> _allowedRanges;

        public CidrAllowlistMiddleware(RequestDelegate next, IOptions<SolarHostOptions> hostOptions)
        {
            _next = next;
            _allowedRanges = ParseAllowedRanges(hostOptions.Value.AllowedCIDRRanges);
        }

        /// <summary>
        /// Parses the configured CIDR ranges at construction time (which happens during host startup), throwing so
        /// the application fails to start on missing or invalid configuration rather than silently allowing
        /// everyone through (SOL-T-1101: "deny-by-default, not allow-by-default").
        /// </summary>
        private static IReadOnlyList<CidrRange> ParseAllowedRanges(IReadOnlyList<string> configuredRanges)
        {
            if (configuredRanges == null || configuredRanges.Count == 0)
            {
                throw new InvalidOperationException("SolarHostOptions.AllowedCIDRRanges must contain at least one CIDR range. Refusing to start with an empty (allow-nothing-but-effectively-unsafe-by-omission) or missing allow-list.");
            }

            List<CidrRange> ranges = new List<CidrRange>();
            foreach (string configuredRange in configuredRanges)
            {
                if (!CidrRange.TryParse(configuredRange, out CidrRange? range) || range == null)
                {
                    throw new InvalidOperationException($"SolarHostOptions.AllowedCIDRRanges contains an invalid CIDR range: '{configuredRange}'.");
                }

                ranges.Add(range);
            }

            return ranges;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            IPAddress? remoteAddress = context.Connection.RemoteIpAddress;

            if (remoteAddress == null || !IsAllowed(remoteAddress))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden.");
                return;
            }

            await _next(context);
        }

        private bool IsAllowed(IPAddress remoteAddress)
        {
            foreach (CidrRange range in _allowedRanges)
            {
                if (range.Contains(remoteAddress))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

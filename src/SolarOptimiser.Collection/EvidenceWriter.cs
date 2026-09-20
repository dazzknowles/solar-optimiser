using Microsoft.Extensions.Configuration;
using System.Text;

namespace SolarOptimiser.Collection
{
    public sealed class EvidenceWriter : IEvidenceWriter
    {
        private readonly string _rawResponseRoot;
        private readonly int _maxCapturedEvidenceBytes;

        // Read directly from configuration rather than a strongly-typed HostOptions reference -
        // SolarOptimiser.Collection must not depend on SolarOptimiser.Host (SOL-T-101's dependencies flow the
        // other way: Host depends on Collection). RawResponseRoot is configured under HostOptions per SOL-T-1201
        // even though this writer - Collection's responsibility - is what actually uses it.
        public EvidenceWriter(IConfiguration configuration)
        {
            _rawResponseRoot = configuration["HostOptions:RawResponseRoot"] ?? string.Empty;
            _maxCapturedEvidenceBytes = configuration.GetValue<int?>("CollectionOptions:MaxCapturedEvidenceBytes") ?? 1_048_576;
        }

        public async Task<string?> WriteAsync(string evidenceId, string suffix, string? content, DateTime atUtc, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_rawResponseRoot) || string.IsNullOrEmpty(content))
            {
                return null;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            if (bytes.Length > _maxCapturedEvidenceBytes)
            {
                return null;
            }

            try
            {
                string directory = Path.Combine(
                    _rawResponseRoot,
                    atUtc.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    atUtc.ToString("MM", System.Globalization.CultureInfo.InvariantCulture),
                    atUtc.ToString("dd", System.Globalization.CultureInfo.InvariantCulture));

                Directory.CreateDirectory(directory);

                string finalPath = Path.Combine(directory, $"{evidenceId}-{suffix}.json");
                string tempPath = finalPath + ".tmp";

                await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
                File.Move(tempPath, finalPath, overwrite: true);

                return finalPath;
            }
            catch (Exception)
            {
                // Best-effort and secondary (SOL-T-502) - never block or fail the primary DB write.
                return null;
            }
        }
    }
}

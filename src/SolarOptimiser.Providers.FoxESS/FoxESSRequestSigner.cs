using System.Security.Cryptography;
using System.Text;

namespace SolarOptimiser.Providers.FoxESS
{
    /// <summary>
    /// The private API token signing scheme (FoxESS r04 §4.1): signature is
    /// <c>MD5(url + "\r\n" + token + "\r\n" + timestamp)</c>, hex-encoded lowercase.
    /// </summary>
    public sealed class FoxESSRequestSigner
    {
        /// <summary>
        /// Signs a request path. FoxESS's documentation does not disambiguate whether "url" in the signature
        /// formula means the path alone or a full URL, and does not say whether a query string is included —
        /// this signs the request-target exactly as it will appear on the wire (path plus any query string, no
        /// scheme/host), which is the common convention for this style of signed API and is straightforward to
        /// verify/adjust once tenant-zero evidence confirms it.
        /// </summary>
        public string Sign(string requestPathAndQuery, string apiKey, string timestampMilliseconds)
        {
            if (requestPathAndQuery == null)
            {
                throw new ArgumentNullException(nameof(requestPathAndQuery));
            }

            if (apiKey == null)
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            if (timestampMilliseconds == null)
            {
                throw new ArgumentNullException(nameof(timestampMilliseconds));
            }

            string signaturePayload = requestPathAndQuery + "\r\n" + apiKey + "\r\n" + timestampMilliseconds;
            byte[] payloadBytes = Encoding.UTF8.GetBytes(signaturePayload);
            byte[] hashBytes = MD5.HashData(payloadBytes);

            StringBuilder hex = new StringBuilder(hashBytes.Length * 2);
            foreach (byte b in hashBytes)
            {
                hex.Append(b.ToString("x2"));
            }

            return hex.ToString();
        }

        public static string CurrentTimestampMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        }
    }
}

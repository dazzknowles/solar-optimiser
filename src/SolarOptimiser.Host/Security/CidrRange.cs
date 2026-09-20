using System.Net;
using System.Net.Sockets;

namespace SolarOptimiser.Host.Security
{
    /// <summary>
    /// A parsed CIDR range (e.g. <c>192.168.1.0/24</c> or <c>::1/128</c>) that can test whether an address falls
    /// inside it. IPv4-mapped IPv6 addresses are normalised before comparison, per SOL-T-1101.
    /// </summary>
    public sealed class CidrRange
    {
        private readonly byte[] _networkBytes;
        private readonly int _prefixLength;
        private readonly AddressFamily _family;

        private CidrRange(byte[] networkBytes, int prefixLength, AddressFamily family)
        {
            _networkBytes = networkBytes;
            _prefixLength = prefixLength;
            _family = family;
        }

        public static bool TryParse(string cidr, out CidrRange? range)
        {
            range = null;

            if (string.IsNullOrWhiteSpace(cidr))
            {
                return false;
            }

            string[] parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[0].Trim(), out IPAddress? address))
            {
                return false;
            }

            if (!int.TryParse(parts[1].Trim(), out int prefixLength))
            {
                return false;
            }

            IPAddress normalized = NormalizeAddress(address);
            byte[] addressBytes = normalized.GetAddressBytes();
            int maxPrefixLength = addressBytes.Length * 8;

            if (prefixLength < 0 || prefixLength > maxPrefixLength)
            {
                return false;
            }

            range = new CidrRange(addressBytes, prefixLength, normalized.AddressFamily);
            return true;
        }

        public bool Contains(IPAddress address)
        {
            IPAddress normalized = NormalizeAddress(address);
            if (normalized.AddressFamily != _family)
            {
                return false;
            }

            byte[] candidateBytes = normalized.GetAddressBytes();
            int fullBytes = _prefixLength / 8;
            int remainingBits = _prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (candidateBytes[i] != _networkBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits == 0)
            {
                return true;
            }

            int mask = 0xFF << (8 - remainingBits);
            return (candidateBytes[fullBytes] & mask) == (_networkBytes[fullBytes] & mask);
        }

        private static IPAddress NormalizeAddress(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return address.MapToIPv4();
            }

            return address;
        }
    }
}

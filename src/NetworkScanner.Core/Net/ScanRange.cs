using System.Net;

namespace NetworkScanner.Core;

public sealed record ScanRange(IReadOnlyList<IPAddress> Hosts, bool Capped)
{
    const int LargestPrefixScanned = 22;

    public static ScanRange For(IPAddress address, int prefixLength)
    {
        var capped = prefixLength < LargestPrefixScanned;
        if (capped) prefixLength = 24;

        var ip = IpUtil.ToUInt32(address);
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var first = ip & mask;
        var last = first | ~mask;
        if (prefixLength <= 30)
        {
            first++;
            last--;
        }

        var hosts = new List<IPAddress>();
        for (var v = first; v <= last && v >= first; v++) hosts.Add(IpUtil.FromUInt32(v));
        return new ScanRange(hosts, capped);
    }
}

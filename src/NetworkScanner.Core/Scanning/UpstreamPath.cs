using System.Net;

namespace NetworkScanner.Core;

public static class UpstreamPath
{
    public static IReadOnlyList<IPAddress> Select(IPAddress gateway, IEnumerable<IPAddress?> hops)
    {
        var result = new List<IPAddress>();
        var first = true;
        foreach (var hop in hops)
        {
            if (first)
            {
                if (hop is null || !hop.Equals(gateway)) return Array.Empty<IPAddress>();
                first = false;
                continue;
            }
            if (hop is null || hop.Equals(gateway)) continue;
            if (!IpUtil.IsPrivate(hop)) break;
            result.Add(hop);
        }
        return result;
    }
}

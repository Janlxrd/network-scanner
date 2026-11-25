using System.Net;
using System.Net.Sockets;

namespace NetworkScanner.Core;

public static class HostName
{
    static readonly string[] LocalSuffixes = { ".home.arpa", ".localdomain", ".local", ".lan", ".home" };

    public static string? Normalize(string? name)
    {
        if (name is null) return null;
        var n = name.Trim().TrimEnd('.');
        foreach (var suffix in LocalSuffixes)
        {
            if (n.Length > suffix.Length && n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                n = n[..^suffix.Length];
                break;
            }
        }
        if (n.Length == 0) return null;
        if (IPAddress.TryParse(n, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork) return null;
        return n;
    }
}

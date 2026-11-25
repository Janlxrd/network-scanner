using System.Text.RegularExpressions;

namespace NetworkScanner.Core;

public static class DeviceText
{
    public const string MacUnknown = "MAC unknown";
    public const string PrivateMac = "Private MAC";
    public const string UnknownVendor = "Unknown vendor";

    public static string Vendor(MacAddress? mac, string? ouiVendor)
    {
        if (mac is not { } m) return MacUnknown;
        if (m.IsLocallyAdministered) return PrivateMac;
        return ouiVendor ?? UnknownVendor;
    }

    static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
    {
        "Co", "Ltd", "Inc", "Corp", "Corporation", "LLC", "GmbH", "AG", "SA", "Limited", "Company",
        "Technologies", "Technology", "Electronics", "Communication", "Communications", "International",
        "INTL", "Trading", "Ind", "Industrial",
    };

    public static string ShortVendor(string vendor)
    {
        if (vendor is MacUnknown or PrivateMac or UnknownVendor) return vendor;
        var original = vendor.Trim();
        var text = original.Split(',')[0];
        text = Regex.Replace(text, @"\([^)]*\)", " ");
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (words.Count > 0 && Filler.Contains(words[^1].TrimEnd('.'))) words.RemoveAt(words.Count - 1);
        if (words.Count == 0) return original;
        return string.Join(" ", words.Select(FixCaps));
    }

    static string FixCaps(string word) =>
        word.Length > 3 && word.Any(char.IsLetter) && word.Where(char.IsLetter).All(char.IsUpper)
            ? word[0] + word[1..].ToLowerInvariant()
            : word;

    public static string LastSeenText(DateTimeOffset lastSeen, DateTimeOffset now, bool online)
    {
        if (online) return "now";
        var ago = now - lastSeen;
        if (ago < TimeSpan.FromMinutes(1)) return "just now";
        if (ago < TimeSpan.FromHours(1)) return $"{(int)ago.TotalMinutes} min ago";
        if (ago < TimeSpan.FromDays(1)) return $"{(int)ago.TotalHours} h ago";
        if (ago < TimeSpan.FromDays(7)) return (int)ago.TotalDays == 1 ? "1 day ago" : $"{(int)ago.TotalDays} days ago";
        return lastSeen.ToLocalTime().ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string DisplayName(KnownDevice d) =>
        d.CustomName ?? DisplayName(d.Kind, d.Hostname, ShortVendor(d.Vendor));

    public static string DisplayName(DeviceKind kind, string? hostname, string vendor)
    {
        if (kind == DeviceKind.ThisPc) return "This PC";
        if (hostname is not null) return hostname;
        if (kind == DeviceKind.Router) return "Router";
        return vendor is MacUnknown or UnknownVendor ? "Unknown" : vendor;
    }
}

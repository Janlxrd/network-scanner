using System.Globalization;
using System.Net;
using System.Windows.Media;
using NetworkScanner.Core;

namespace NetworkScanner;

public sealed class DeviceItem
{
    public DeviceItem(DeviceStatus status)
    {
        var d = status.Device;
        Device = d;
        Key = d.Key;
        Kind = d.EffectiveKind;
        Online = status.Online;
        Name = DeviceText.DisplayName(d);
        Ip = d.Ip;
        IpSort = IPAddress.TryParse(d.Ip, out var ip) ? IpUtil.ToUInt32(ip) : uint.MaxValue;
        Mac = d.Mac ?? "-";
        Vendor = DeviceText.ShortVendor(d.Vendor);
        FullVendor = d.Vendor;
        Subtitle = string.Equals(Vendor, Name, StringComparison.OrdinalIgnoreCase) ? null : Vendor;
        Latency = status.Online && status.LatencyMs is { } ms ? $"{ms} ms" : "-";
        LatencySort = status.Online && status.LatencyMs is { } sortMs ? sortMs : int.MaxValue;
        LastSeen = DeviceText.LastSeenText(d.LastSeen, DateTimeOffset.Now, status.Online);
        LastSeenSort = status.Online ? DateTimeOffset.MaxValue : d.LastSeen;
        FirstSeen = Format(d.FirstSeen);
        KindText = DeviceIcons.Label(Kind);
        Glyph = DeviceIcons.Glyph(Kind);
        StatusTag = !status.Online ? "Offline" : status.LatencyMs is { } tagMs ? $"Online · {tagMs} ms" : "Online";
        RowOpacity = status.Online ? 1 : 0.5;
        var (fill, fore) = DeviceIcons.Tint(Kind, ThemeSwitcher.Current == AppSettings.Dark);
        TileFill = Frozen(fill);
        TileFore = Frozen(fore);
    }

    public string? Subtitle { get; }
    public string StatusTag { get; }
    public double RowOpacity { get; }
    public SolidColorBrush TileFill { get; }
    public SolidColorBrush TileFore { get; }

    static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public KnownDevice Device { get; }
    public string Key { get; }
    public DeviceKind Kind { get; }
    public string KindText { get; }
    public string Glyph { get; }
    public bool Online { get; }
    public string StatusText => Online ? "Online" : "Offline";
    public string Name { get; }
    public string Ip { get; }
    public uint IpSort { get; }
    public string Mac { get; }
    public string Vendor { get; }
    public string FullVendor { get; }
    public string Latency { get; }
    public int LatencySort { get; }
    public string LastSeen { get; }
    public DateTimeOffset LastSeenSort { get; }
    public string FirstSeen { get; }

    static string Format(DateTimeOffset t) => t.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
}

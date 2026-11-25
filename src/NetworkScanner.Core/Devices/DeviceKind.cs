using System.Net;
using System.Text.RegularExpressions;

namespace NetworkScanner.Core;

public enum DeviceKind
{
    Router,
    ThisPc,
    RaspberryPi,
    Computer,
    Phone,
    Tv,
    Speaker,
    Printer,
    Camera,
    GameConsole,
    SmartHome,
    Nas,
    Device,
}

public static class DeviceClassifier
{
    public static readonly IReadOnlySet<uint> RaspberryPiOuis = new HashSet<uint>
    {
        0xB827EB, 0xDCA632, 0xE45F01, 0x28CDC1, 0xD83ADD, 0x2CCF67, 0x88A29E, 0x98FE54,
    };

    static readonly (DeviceKind Kind, string[] Words)[] HostnameWords =
    {
        (DeviceKind.Phone, new[] { "iphone", "ipad", "android", "galaxy", "pixel", "redmi", "oneplus" }),
        (DeviceKind.Tv, new[] { "tv", "bravia", "roku", "chromecast", "firetv", "webos" }),
        (DeviceKind.Computer, new[] { "desktop-", "laptop-", "macbook", "imac" }),
        (DeviceKind.Speaker, new[] { "sonos", "echo", "homepod" }),
        (DeviceKind.Printer, new[] { "printer", "epson", "brother", "canon" }),
        (DeviceKind.Camera, new[] { "cam", "camera" }),
        (DeviceKind.GameConsole, new[] { "xbox", "playstation", "ps4", "ps5", "nintendo" }),
        (DeviceKind.Nas, new[] { "nas", "synology", "diskstation", "qnap" }),
        (DeviceKind.SmartHome, new[] { "esp", "shelly", "tasmota", "tuya", "hue", "nest" }),
    };

    static readonly (DeviceKind Kind, string[] Words)[] VendorWords =
    {
        (DeviceKind.Speaker, new[] { "Sonos", "Bose" }),
        (DeviceKind.Printer, new[] { "Seiko Epson", "Brother", "Canon", "Lexmark", "Xerox" }),
        (DeviceKind.Camera, new[] { "Hikvision", "Dahua", "Reolink", "Wyze", "Ring", "Arlo" }),
        (DeviceKind.GameConsole, new[] { "Nintendo", "Sony Interactive" }),
        (DeviceKind.Nas, new[] { "Synology", "QNAP" }),
        (DeviceKind.SmartHome, new[] { "Espressif", "Tuya", "Shelly", "Signify", "Philips Lighting", "Nest", "ecobee" }),
        (DeviceKind.Tv, new[] { "Roku", "LG Electronics", "Vizio", "TCL", "Hisense" }),
        (DeviceKind.Computer, new[] { "Intel", "Micro-Star", "ASUSTek", "Dell", "Lenovo", "Gigabyte", "Hon Hai", "AzureWave", "Liteon" }),
        (DeviceKind.Phone, new[] { "Apple", "Xiaomi", "Motorola", "OnePlus", "Huawei", "Oppo", "Vivo", "Samsung" }),
    };

    public static DeviceKind Classify(IPAddress ip, MacAddress? mac, string? hostname, string? vendor, IPAddress gateway, IPAddress localIp)
    {
        if (ip.Equals(gateway)) return DeviceKind.Router;
        if (ip.Equals(localIp)) return DeviceKind.ThisPc;
        if (mac is { } m && RaspberryPiOuis.Contains(m.Oui)) return DeviceKind.RaspberryPi;
        if (hostname?.Contains("raspberrypi", StringComparison.OrdinalIgnoreCase) == true) return DeviceKind.RaspberryPi;
        if (hostname is not null && FromHostname(hostname) is { } byHost) return byHost;
        if (vendor is not null && FromVendor(vendor) is { } byVendor) return byVendor;
        if (mac is { IsLocallyAdministered: true }) return DeviceKind.Phone;
        return DeviceKind.Device;
    }

    static DeviceKind? FromHostname(string hostname)
    {
        var host = hostname.ToLowerInvariant();
        var tokens = Regex.Split(host, "[^a-z0-9]+").Where(t => t.Length > 0).ToArray();
        foreach (var (kind, words) in HostnameWords)
        {
            foreach (var w in words)
            {
                var hit = w.Length >= 4
                    ? host.Contains(w)
                    : tokens.Any(t => t.StartsWith(w) || t.EndsWith(w));
                if (hit) return kind;
            }
        }
        return null;
    }

    static DeviceKind? FromVendor(string vendor)
    {
        foreach (var (kind, words) in VendorWords)
        {
            if (words.Any(w => Regex.IsMatch(vendor, $@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase))) return kind;
        }
        return null;
    }
}

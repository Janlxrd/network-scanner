using System.Net;

namespace NetworkScanner.Core;

public sealed record DeviceStatus(KnownDevice Device, bool Online, int? LatencyMs);

public static class DeviceMerger
{
    public static IReadOnlyList<DeviceStatus> Merge(DeviceStore store, ScanResult scan, OuiDatabase oui, DateTimeOffset now)
    {
        var net = scan.Network;
        var gatewayMac = scan.Devices.FirstOrDefault(d => d.Ip.Equals(net.Gateway))?.Mac?.ToString();
        var gatewayIp = net.Gateway.ToString();
        var known = store.Find(gatewayMac, gatewayIp, net.PrefixLength);
        if (known is null)
        {
            known = new KnownNetwork { GatewayMac = gatewayMac ?? "", GatewayIp = gatewayIp, PrefixLength = net.PrefixLength };
            store.Networks.Add(known);
        }
        else
        {
            if (gatewayMac is not null) known.GatewayMac = gatewayMac;
            known.GatewayIp = gatewayIp;
            known.PrefixLength = net.PrefixLength;
        }

        var seen = new Dictionary<KnownDevice, int?>();
        foreach (var s in scan.Devices)
        {
            var ip = s.Ip.ToString();
            var host = HostName.Normalize(s.Hostname);
            var entry = FindEntry(known, s, ip, host, seen);
            if (entry is null)
            {
                entry = new KnownDevice { FirstSeen = now, Key = "ip:" + ip };
                known.Devices.Add(entry);
            }

            MacAddress? mac = s.Mac;
            if (mac is null && entry.Mac is not null && MacAddress.TryParse(entry.Mac, out var old)) mac = old;
            if (mac is { } m)
            {
                entry.Mac = m.ToString();
                entry.Key = entry.Mac;
            }
            entry.Ip = ip;
            if (host is not null) entry.Hostname = host;
            entry.Vendor = DeviceText.Vendor(mac, mac is { } v ? oui.Lookup(v) : null);
            entry.Kind = DeviceClassifier.Classify(s.Ip, mac, entry.Hostname, entry.Vendor, net.Gateway, net.Address);
            entry.LastSeen = now;
            seen[entry] = s.LatencyMs;
        }

        foreach (var d in known.Devices.Where(d => !seen.ContainsKey(d))) Reguess(d, net);

        return known.Devices
            .Select(d => new DeviceStatus(d, seen.ContainsKey(d), seen.GetValueOrDefault(d)))
            .OrderBy(d => IpSort(d.Device.Ip))
            .ToList();
    }

    public static IReadOnlyList<DeviceStatus> Preview(DeviceStore store, ScanResult scan, OuiDatabase oui, DateTimeOffset now) =>
        Merge(store.Copy(), scan, oui, now);

    public static void SetCustom(KnownDevice device, string? name, DeviceKind? kind)
    {
        device.CustomName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        device.CustomKind = device.Kind is DeviceKind.Router or DeviceKind.ThisPc ? null : kind;
    }

    public static IReadOnlyList<DeviceStatus> Saved(DeviceStore store, LocalNetwork net)
    {
        var known = store.Find(null, net.Gateway.ToString(), net.PrefixLength);
        if (known is null) return Array.Empty<DeviceStatus>();
        foreach (var d in known.Devices) Reguess(d, net);
        return known.Devices.Select(d => new DeviceStatus(d, false, null)).OrderBy(d => IpSort(d.Device.Ip)).ToList();
    }

    static KnownDevice? FindEntry(KnownNetwork known, ScannedDevice s, string ip, string? host, Dictionary<KnownDevice, int?> seen)
    {
        var free = known.Devices.Where(d => !seen.ContainsKey(d));
        if (s.Mac is not { } mac) return free.FirstOrDefault(d => d.Ip == ip);

        var key = mac.ToString();
        return free.FirstOrDefault(d => d.Key == key)
            ?? free.FirstOrDefault(d => d.Key == "ip:" + ip)
            ?? (mac.IsLocallyAdministered && host is not null
                ? free.FirstOrDefault(d => d.Mac is not null && IsPrivate(d.Mac) && string.Equals(d.Hostname, host, StringComparison.OrdinalIgnoreCase))
                : null);
    }

    static void Reguess(KnownDevice d, LocalNetwork net)
    {
        if (!IPAddress.TryParse(d.Ip, out var ip)) return;
        MacAddress? mac = d.Mac is not null && MacAddress.TryParse(d.Mac, out var m) ? m : null;
        d.Kind = DeviceClassifier.Classify(ip, mac, d.Hostname, d.Vendor, net.Gateway, net.Address);
    }

    static bool IsPrivate(string mac) => MacAddress.TryParse(mac, out var m) && m.IsLocallyAdministered;

    static uint IpSort(string ip) => IPAddress.TryParse(ip, out var a) ? IpUtil.ToUInt32(a) : uint.MaxValue;
}

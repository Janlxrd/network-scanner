using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class MergerTests
{
    static readonly IPAddress Gw = IPAddress.Parse("192.168.1.1");
    static readonly LocalNetwork Net = new("Wi-Fi", IPAddress.Parse("192.168.1.23"), 24, Gw, M("00:11:22:33:44:55"));
    static readonly OuiDatabase Oui = OuiDatabase.Parse(new StringReader("DCA632\tRaspberry Pi Trading Ltd"));
    static readonly DateTimeOffset T0 = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset T1 = T0.AddDays(1);

    readonly DeviceStore store = new();

    static MacAddress M(string s)
    {
        MacAddress.TryParse(s, out var m);
        return m;
    }

    static ScannedDevice D(string lastOctet, string? mac, string? host = null, int? ms = 5) =>
        new(IPAddress.Parse("192.168.1." + lastOctet), mac is null ? null : M(mac), host, ms);

    static ScanResult Scan(params ScannedDevice[] devices) => ScanVia(Net, "AA:BB:CC:00:00:01", devices);

    static ScanResult ScanVia(LocalNetwork net, string gatewayMac, params ScannedDevice[] devices) =>
        new(net, devices.Prepend(new ScannedDevice(net.Gateway, M(gatewayMac), null, 1)).ToList(), Array.Empty<IPAddress>(), false, false);

    KnownDevice Single(string ip) => store.Networks.Single().Devices.Single(d => d.Ip == ip);

    [Fact]
    public void Merge_NewDevice_AddedWithVendorAndFirstSeen()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T0);
        var d = Single("192.168.1.40");
        Assert.Equal("Raspberry Pi Trading Ltd", d.Vendor);
        Assert.Equal(DeviceKind.RaspberryPi, d.Kind);
        Assert.Equal(T0, d.FirstSeen);
        Assert.Equal(T0, d.LastSeen);
        Assert.Equal("DC:A6:32:00:00:05", d.Key);
    }

    [Fact]
    public void Merge_GatewayIsRouter()
    {
        DeviceMerger.Merge(store, Scan(), Oui, T0);
        Assert.Equal(DeviceKind.Router, Single("192.168.1.1").Kind);
        Assert.Equal("AA:BB:CC:00:00:01", store.Networks.Single().GatewayMac);
    }

    [Fact]
    public void Merge_KnownDevice_UpdatesIpKeepsFirstSeen()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T0);
        DeviceMerger.Merge(store, Scan(D("41", "DC:A6:32:00:00:05")), Oui, T1);
        var d = store.Networks.Single().Devices.Single(x => x.Key == "DC:A6:32:00:00:05");
        Assert.Equal("192.168.1.41", d.Ip);
        Assert.Equal(T0, d.FirstSeen);
        Assert.Equal(T1, d.LastSeen);
    }

    [Fact]
    public void Merge_KeepsOldHostnameWhenNewIsNull()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05", "pi.lan")), Oui, T0);
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T1);
        Assert.Equal("pi", Single("192.168.1.40").Hostname);
    }

    [Fact]
    public void Merge_MissingDevice_IsOfflineAndLastSeenUnchanged()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T0);
        var result = DeviceMerger.Merge(store, Scan(), Oui, T1);
        var s = result.Single(x => x.Device.Ip == "192.168.1.40");
        Assert.False(s.Online);
        Assert.Null(s.LatencyMs);
        Assert.Equal(T0, s.Device.LastSeen);
        Assert.True(result.Single(x => x.Device.Ip == "192.168.1.1").Online);
    }

    [Fact]
    public void Merge_ResultOrderedByIpNumerically()
    {
        var result = DeviceMerger.Merge(store, Scan(D("100", "00:00:01:00:00:64"), D("9", "00:00:01:00:00:09")), Oui, T0);
        Assert.Equal(new[] { "192.168.1.1", "192.168.1.9", "192.168.1.100" }, result.Select(x => x.Device.Ip));
    }

    [Fact]
    public void Merge_PrivateMacMatchesByNormalizedHostname()
    {
        DeviceMerger.Merge(store, Scan(D("52", "DA:00:00:00:00:01", "Pixel-7.lan")), Oui, T0);
        DeviceMerger.Merge(store, Scan(D("53", "DA:00:00:00:00:99", "pixel-7")), Oui, T1);
        var phones = store.Networks.Single().Devices.Where(d => d.Kind == DeviceKind.Phone).ToList();
        Assert.Single(phones);
        Assert.Equal("DA:00:00:00:00:99", phones[0].Key);
        Assert.Equal("DA:00:00:00:00:99", phones[0].Mac);
        Assert.Equal(T0, phones[0].FirstSeen);
    }

    [Fact]
    public void Merge_TwoPrivateMacsSameHostnameInOneScan_StayTwoEntries()
    {
        DeviceMerger.Merge(store, Scan(D("52", "DA:00:00:00:00:01", "iPhone"), D("53", "DA:00:00:00:00:02", "iPhone")), Oui, T0);
        Assert.Equal(3, store.Networks.Single().Devices.Count);
    }

    [Fact]
    public void Merge_NoMacReplyForKnownMacDevice_DoesNotDuplicate()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T0);
        var result = DeviceMerger.Merge(store, Scan(D("40", null)), Oui, T1);
        var d = Single("192.168.1.40");
        Assert.Equal("DC:A6:32:00:00:05", d.Key);
        Assert.Equal("Raspberry Pi Trading Ltd", d.Vendor);
        Assert.True(result.Single(x => x.Device == d).Online);
    }

    [Fact]
    public void Merge_PublicMacDoesNotMatchByHostname()
    {
        DeviceMerger.Merge(store, Scan(D("60", "00:00:01:00:00:60", "tv")), Oui, T0);
        DeviceMerger.Merge(store, Scan(D("61", "00:00:01:00:00:61", "tv")), Oui, T1);
        Assert.Equal(3, store.Networks.Single().Devices.Count);
    }

    [Fact]
    public void Merge_IpOnlyEntryUpgradesToMac()
    {
        DeviceMerger.Merge(store, Scan(D("60", null)), Oui, T0);
        Assert.Equal("ip:192.168.1.60", Single("192.168.1.60").Key);
        DeviceMerger.Merge(store, Scan(D("60", "00:1A:11:00:00:60")), Oui, T1);
        var d = Single("192.168.1.60");
        Assert.Equal("00:1A:11:00:00:60", d.Key);
        Assert.Equal(T0, d.FirstSeen);
    }

    [Fact]
    public void Merge_RouterReplaced_ReusesNetworkByIp()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T0);
        DeviceMerger.Merge(store, ScanVia(Net, "AA:BB:CC:00:00:02"), Oui, T1);
        var n = Assert.Single(store.Networks);
        Assert.Equal("AA:BB:CC:00:00:02", n.GatewayMac);
        Assert.Contains(n.Devices, d => d.Key == "DC:A6:32:00:00:05");
    }

    [Fact]
    public void Merge_DifferentNetwork_AddsSecondNetwork()
    {
        DeviceMerger.Merge(store, Scan(), Oui, T0);
        var other = new LocalNetwork("Wi-Fi", IPAddress.Parse("10.0.0.5"), 24, IPAddress.Parse("10.0.0.1"), Net.Mac);
        DeviceMerger.Merge(store, ScanVia(other, "AA:BB:CC:00:00:77"), Oui, T1);
        Assert.Equal(2, store.Networks.Count);
    }

    [Fact]
    public void Saved_ReturnsDevicesOffline()
    {
        DeviceMerger.Merge(store, Scan(D("40", "DC:A6:32:00:00:05")), Oui, T0);
        var saved = DeviceMerger.Saved(store, Net);
        Assert.Equal(2, saved.Count);
        Assert.All(saved, s => Assert.False(s.Online));
    }

    [Fact]
    public void Saved_UnknownNetwork_ReturnsEmpty() => Assert.Empty(DeviceMerger.Saved(store, Net));

    [Fact]
    public void Merge_KeepsCustomNameAndKindAcrossRescanAndIpChange()
    {
        DeviceMerger.Merge(store, Scan(D("40", "00:00:01:00:00:40")), Oui, T0);
        DeviceMerger.SetCustom(Single("192.168.1.40"), "Living room TV", DeviceKind.Tv);
        DeviceMerger.Merge(store, Scan(D("41", "00:00:01:00:00:40")), Oui, T1);
        var d = Single("192.168.1.41");
        Assert.Equal("Living room TV", d.CustomName);
        Assert.Equal(DeviceKind.Tv, d.EffectiveKind);
        Assert.Equal(DeviceKind.Device, d.Kind);
    }

    [Fact]
    public void Merge_PrivateMacHostnameMatch_CarriesCustomName()
    {
        DeviceMerger.Merge(store, Scan(D("52", "DA:00:00:00:00:01", "pixel-7")), Oui, T0);
        DeviceMerger.SetCustom(Single("192.168.1.52"), "Sam's phone", null);
        DeviceMerger.Merge(store, Scan(D("53", "DA:00:00:00:00:99", "pixel-7")), Oui, T1);
        Assert.Equal("Sam's phone", Single("192.168.1.53").CustomName);
    }

    [Fact]
    public void SetCustom_BlankNameClears()
    {
        var d = new KnownDevice { Kind = DeviceKind.Device };
        DeviceMerger.SetCustom(d, "  Printer  ", DeviceKind.Printer);
        Assert.Equal("Printer", d.CustomName);
        DeviceMerger.SetCustom(d, "   ", null);
        Assert.Null(d.CustomName);
        Assert.Null(d.CustomKind);
        Assert.Equal(DeviceKind.Device, d.EffectiveKind);
    }

    [Theory]
    [InlineData(DeviceKind.Router)]
    [InlineData(DeviceKind.ThisPc)]
    public void SetCustom_RouterAndThisPcKindIgnored(DeviceKind kind)
    {
        var d = new KnownDevice { Kind = kind };
        DeviceMerger.SetCustom(d, "Main", DeviceKind.Tv);
        Assert.Equal("Main", d.CustomName);
        Assert.Equal(kind, d.EffectiveKind);
    }

    [Fact]
    public void DisplayName_PrefersCustomNameThenShortVendor()
    {
        var d = new KnownDevice { Kind = DeviceKind.Phone, Vendor = "XIAOMI Electronics,CO.,LTD" };
        Assert.Equal("Xiaomi", DeviceText.DisplayName(d));
        DeviceMerger.SetCustom(d, "Kitchen", null);
        Assert.Equal("Kitchen", DeviceText.DisplayName(d));
        Assert.Equal("This PC", DeviceText.DisplayName(new KnownDevice { Kind = DeviceKind.ThisPc, Vendor = "Intel" }));
    }

    [Fact]
    public void Merge_ReguessesUnseenDevices()
    {
        DeviceMerger.Merge(store, Scan(D("40", "00:00:01:00:00:40")), Oui, T0);
        var d = Single("192.168.1.40");
        d.Kind = DeviceKind.Device;
        d.Vendor = "Xiaomi Communications Co Ltd";
        DeviceMerger.Merge(store, Scan(), Oui, T1);
        Assert.Equal(DeviceKind.Phone, d.Kind);
        Assert.Equal(T0, d.LastSeen);
    }

    [Fact]
    public void Saved_ReguessesKinds()
    {
        DeviceMerger.Merge(store, Scan(D("40", "00:00:01:00:00:40")), Oui, T0);
        var d = Single("192.168.1.40");
        d.Kind = DeviceKind.Device;
        d.Hostname = "LGwebOSTV";
        DeviceMerger.Saved(store, Net);
        Assert.Equal(DeviceKind.Tv, d.Kind);
    }
}

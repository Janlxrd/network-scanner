using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class StoreTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory().FullName;
    string P => Path.Combine(dir, "sub", "devices.json");

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void Load_Missing_ReturnsEmpty() => Assert.Empty(DeviceStore.Load(P).Networks);

    [Fact]
    public void Load_CorruptFile_ReturnsEmptyAndKeepsBackup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P)!);
        File.WriteAllText(P, "{ not json");
        Assert.Empty(DeviceStore.Load(P).Networks);
        Assert.True(File.Exists(P + ".bad"));
        Assert.Equal("{ not json", File.ReadAllText(P + ".bad"));
    }

    [Fact]
    public void Load_CorruptTwice_OverwritesBackup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P)!);
        File.WriteAllText(P, "bad one");
        DeviceStore.Load(P);
        File.WriteAllText(P, "bad two");
        DeviceStore.Load(P);
        Assert.Equal("bad two", File.ReadAllText(P + ".bad"));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var s = new DeviceStore();
        s.Networks.Add(new KnownNetwork
        {
            GatewayMac = "AA:BB:CC:00:00:01",
            GatewayIp = "192.168.1.1",
            PrefixLength = 24,
            Devices = { new KnownDevice { Key = "ip:192.168.1.9", Ip = "192.168.1.9", Vendor = "MAC unknown", Kind = DeviceKind.RaspberryPi } },
        });
        s.Save(P);
        var back = DeviceStore.Load(P);
        Assert.Equal(DeviceKind.RaspberryPi, back.Networks[0].Devices[0].Kind);
        Assert.Contains("RaspberryPi", File.ReadAllText(P));
        Assert.NotNull(back.Find(null, "192.168.1.1", 24));
        Assert.NotNull(back.Find("AA:BB:CC:00:00:01", "10.0.0.1", 8));
        Assert.Null(back.Find("AA:BB:CC:00:00:09", "192.168.1.1", 16));
    }

    [Fact]
    public void Load_NullNetworks_ReturnsEmptyAndFindWorks()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P)!);
        File.WriteAllText(P, "{\"Networks\": null}");
        var s = DeviceStore.Load(P);
        Assert.Empty(s.Networks);
        Assert.Null(s.Find(null, "192.168.1.1", 24));
    }

    [Fact]
    public void Load_V1File_Works()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P)!);
        File.WriteAllText(P, """
            {"Networks":[{"GatewayMac":"AA:BB:CC:00:00:01","GatewayIp":"192.168.1.1","PrefixLength":24,"Devices":[
              {"Key":"DC:A6:32:00:00:05","Ip":"192.168.1.40","Mac":"DC:A6:32:00:00:05","Vendor":"Raspberry Pi Trading Ltd","Hostname":null,"Kind":"RaspberryPi","FirstSeen":"2026-11-25T08:14:00+01:00","LastSeen":"2026-11-25T08:14:00+01:00"},
              {"Key":"ip:192.168.1.9","Ip":"192.168.1.9","Mac":null,"Vendor":"MAC unknown","Hostname":null,"Kind":"Device","FirstSeen":"2026-11-25T08:14:00+01:00","LastSeen":"2026-11-25T08:14:00+01:00"}]}]}
            """);
        var devices = DeviceStore.Load(P).Networks.Single().Devices;
        Assert.Equal(2, devices.Count);
        Assert.Equal(DeviceKind.RaspberryPi, devices[0].EffectiveKind);
        Assert.Null(devices[1].CustomName);
        Assert.Null(devices[1].CustomKind);
    }

    [Fact]
    public void SaveThenLoad_KeepsCustomFields()
    {
        var s = new DeviceStore();
        s.Networks.Add(new KnownNetwork { GatewayIp = "192.168.1.1", PrefixLength = 24,
            Devices = { new KnownDevice { Key = "k", Ip = "192.168.1.9", Vendor = "x", Kind = DeviceKind.Device, CustomName = "NAS", CustomKind = DeviceKind.Nas } } });
        s.Save(P);
        var d = DeviceStore.Load(P).Networks[0].Devices[0];
        Assert.Equal("NAS", d.CustomName);
        Assert.Equal(DeviceKind.Nas, d.CustomKind);
        Assert.Contains("\"Nas\"", File.ReadAllText(P));
    }
}

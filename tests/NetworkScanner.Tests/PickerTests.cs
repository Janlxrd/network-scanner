using System.Net.NetworkInformation;
using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class PickerTests
{
    static InterfaceInfo I(string name, string? ip, string? gw, bool tunnel = false, bool best = false, bool up = true) =>
        new(name, ip is null ? null : IPAddress.Parse(ip), 24, gw is null ? null : IPAddress.Parse(gw), default, up, tunnel, best);

    [Fact]
    public void Choose_SkipsTunnelAndPrefersBestRoute()
    {
        var n = NetworkPicker.Choose(new[]
        {
            I("Tailscale", "100.101.1.2", "100.100.100.100", tunnel: true, best: true),
            I("vEthernet (WSL)", "172.20.0.1", null),
            I("Ethernet", "192.168.1.30", "192.168.1.1"),
            I("Wi-Fi", "192.168.1.23", "192.168.1.1", best: true),
        });
        Assert.Equal("Wi-Fi", n!.AdapterName);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), n.Gateway);
        Assert.Equal(24, n.PrefixLength);
    }

    [Fact]
    public void Choose_NoBestRoute_TakesFirstUsable()
    {
        var n = NetworkPicker.Choose(new[] { I("Bluetooth", null, null), I("Ethernet", "10.0.0.4", "10.0.0.1") });
        Assert.Equal("Ethernet", n!.AdapterName);
    }

    [Fact]
    public void Choose_NoUsableInterface_ReturnsNull() =>
        Assert.Null(NetworkPicker.Choose(new[] { I("Wi-Fi", "192.168.1.23", "192.168.1.1", up: false) }));

    [Theory]
    [InlineData(NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter", false)]
    [InlineData(NetworkInterfaceType.Wireless80211, "Intel(R) Wi-Fi 6 AX201", false)]
    [InlineData(NetworkInterfaceType.Ethernet, "WireGuard Tunnel", true)]
    [InlineData(NetworkInterfaceType.Ethernet, "Tailscale Tunnel", true)]
    [InlineData(NetworkInterfaceType.Tunnel, "Teredo", true)]
    [InlineData(NetworkInterfaceType.Loopback, "Loopback", true)]
    public void IsTunnel_ByTypeAndDescription(NetworkInterfaceType type, string description, bool expected) =>
        Assert.Equal(expected, NetworkPicker.IsTunnel(type, description));

    [Fact]
    public void Choose_HyperVExternalSwitchWithGateway_IsPicked()
    {
        var n = NetworkPicker.Choose(new[]
        {
            I("Ethernet", null, null),
            I("vEthernet (External)", "192.168.1.30", "192.168.1.1", tunnel: NetworkPicker.IsTunnel(NetworkInterfaceType.Ethernet, "Hyper-V Virtual Ethernet Adapter"), best: true),
        });
        Assert.Equal("vEthernet (External)", n!.AdapterName);
    }
}

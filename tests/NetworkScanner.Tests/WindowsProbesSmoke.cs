using System.Diagnostics;
using NetworkScanner.Core;
using Xunit.Abstractions;

namespace NetworkScanner.Tests;

public class WindowsProbesSmoke(ITestOutputHelper output)
{
    [Fact(Skip = "manual")]
    public async Task ScanRealNetwork()
    {
        var net = NetworkPicker.Choose(NetworkPicker.ReadSystem());
        Assert.NotNull(net);
        output.WriteLine($"{net.AdapterName} {net.Address}/{net.PrefixLength} gw {net.Gateway}");
        var sw = Stopwatch.StartNew();
        var retry = DeviceStore.Load(DeviceStore.DefaultPath).Find(null, net.Gateway.ToString(), net.PrefixLength)?.Devices
            .Select(d => IPAddress.Parse(d.Ip)).ToList();
        var r = await new Scanner(new WindowsProbes()).ScanAsync(net, null, CancellationToken.None, retry);
        output.WriteLine($"{r.Devices.Count} devices in {sw.Elapsed.TotalSeconds:F1}s, upstream: {string.Join(", ", r.UpstreamRouters)}");
        var oui = OuiDatabase.LoadEmbedded();
        foreach (var d in r.Devices)
            output.WriteLine($"{d.Ip,-16} {d.Mac?.ToString() ?? "-",-18} {d.LatencyMs,4} {d.Hostname,-24} {DeviceText.Vendor(d.Mac, d.Mac is { } m ? oui.Lookup(m) : null)}");
        Assert.Contains(r.Devices, d => d.Ip.Equals(net.Gateway));
    }
}

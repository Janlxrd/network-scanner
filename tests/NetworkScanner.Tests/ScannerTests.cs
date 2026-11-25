using System.Collections.Concurrent;
using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class ScannerTests
{
    static readonly LocalNetwork Net = new("Wi-Fi", IPAddress.Parse("192.168.1.23"), 24, IPAddress.Parse("192.168.1.1"), M("00:11:22:33:44:55"));

    static MacAddress M(string s)
    {
        MacAddress.TryParse(s, out var m);
        return m;
    }

    static IPAddress A(string lastOctet) => IPAddress.Parse("192.168.1." + lastOctet);

    sealed class FakeProbes : IProbes
    {
        public readonly Dictionary<IPAddress, MacAddress> Arp = new();
        public readonly Dictionary<IPAddress, int> Ping = new();
        public readonly Dictionary<IPAddress, string> Names = new();
        public readonly ConcurrentBag<IPAddress> Resolved = new();
        public IPAddress? Hang;
        public IPAddress? IgnoresCancel;
        public IPAddress? WakesOnSecondArp;
        public readonly ConcurrentDictionary<IPAddress, int> ArpCalls = new();
        public IReadOnlyList<IPAddress> Upstream = Array.Empty<IPAddress>();

        public async Task<MacAddress?> ArpAsync(IPAddress ip, CancellationToken ct)
        {
            if (ip.Equals(Hang)) await Task.Delay(Timeout.Infinite, ct);
            if (ip.Equals(IgnoresCancel)) await Task.Delay(400);
            var calls = ArpCalls.AddOrUpdate(ip, 1, (_, n) => n + 1);
            if (ip.Equals(WakesOnSecondArp) && calls >= 2) return M("DA:00:00:00:00:50");
            return Arp.TryGetValue(ip, out var m) ? m : null;
        }

        public Task<int?> PingAsync(IPAddress ip, CancellationToken ct) =>
            Task.FromResult(Ping.TryGetValue(ip, out var ms) ? ms : (int?)null);

        public Task<string?> ResolveNameAsync(IPAddress ip, CancellationToken ct)
        {
            Resolved.Add(ip);
            return Task.FromResult(Names.GetValueOrDefault(ip));
        }

        public Task<IReadOnlyList<IPAddress>> TraceUpstreamAsync(IPAddress gateway, CancellationToken ct) => Task.FromResult(Upstream);
    }

    sealed class SyncProgress : IProgress<ScanProgress>
    {
        public readonly ConcurrentQueue<ScanProgress> Reports = new();
        public void Report(ScanProgress value) => Reports.Enqueue(value);
    }

    [Fact]
    public async Task ScanAsync_OnlineIfArpOrPing()
    {
        var p = new FakeProbes();
        p.Arp[A("40")] = M("DC:A6:32:00:00:40");
        p.Ping[A("41")] = 7;
        var r = await new Scanner(p).ScanAsync(Net, null, CancellationToken.None);
        Assert.Contains(r.Devices, d => d.Ip.Equals(A("40")) && d.Mac == M("DC:A6:32:00:00:40") && d.LatencyMs == null);
        Assert.Contains(r.Devices, d => d.Ip.Equals(A("41")) && d.Mac == null && d.LatencyMs == 7);
        Assert.DoesNotContain(r.Devices, d => d.Ip.Equals(A("42")));
        Assert.Contains(r.Devices, d => d.Ip.Equals(Net.Address) && d.Mac == Net.Mac && d.LatencyMs == 0);
        Assert.Equal(3, r.Devices.Count);
        Assert.False(r.Cancelled);
    }

    [Fact]
    public async Task ScanAsync_ResolvesNamesOnlyForOnline()
    {
        var p = new FakeProbes();
        p.Arp[A("40")] = M("DC:A6:32:00:00:40");
        p.Names[A("40")] = "raspberrypi.local";
        var r = await new Scanner(p).ScanAsync(Net, null, CancellationToken.None);
        Assert.Equal(new[] { A("23"), A("40") }.OrderBy(IpUtil.ToUInt32), p.Resolved.OrderBy(IpUtil.ToUInt32));
        Assert.Equal("raspberrypi", r.Devices.Single(d => d.Ip.Equals(A("40"))).Hostname);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgressTotal()
    {
        var progress = new SyncProgress();
        await new Scanner(new FakeProbes()).ScanAsync(Net, progress, CancellationToken.None);
        var finding = progress.Reports.Where(r => r.Stage == "Finding devices").ToList();
        Assert.All(finding, r => Assert.Equal(253, r.Total));
        Assert.Equal(253, finding.Max(r => r.Done));
        Assert.Equal("Done", progress.Reports.Last().Stage);
    }

    [Fact]
    public async Task ScanAsync_Cancelled_ReturnsPartialResult()
    {
        var p = new FakeProbes { Hang = A("99") };
        p.Arp[A("40")] = M("DC:A6:32:00:00:40");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var r = await new Scanner(p).ScanAsync(Net, null, cts.Token);
        Assert.True(r.Cancelled);
        Assert.Contains(r.Devices, d => d.Ip.Equals(A("40")));
    }

    [Fact]
    public async Task ScanAsync_IncludesUpstreamRouters()
    {
        var p = new FakeProbes { Upstream = new[] { IPAddress.Parse("10.0.0.1") } };
        var r = await new Scanner(p).ScanAsync(Net, null, CancellationToken.None);
        Assert.Equal(p.Upstream, r.UpstreamRouters);
    }

    [Fact]
    public async Task ScanAsync_LargeSubnet_IsCapped()
    {
        var r = await new Scanner(new FakeProbes()).ScanAsync(Net with { PrefixLength = 16 }, null, CancellationToken.None);
        Assert.True(r.Capped);
    }

    [Fact]
    public async Task ScanAsync_Cancelled_NoProgressAfterDone()
    {
        var p = new FakeProbes { IgnoresCancel = A("99") };
        var progress = new SyncProgress();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var r = await new Scanner(p).ScanAsync(Net, progress, cts.Token);
        await Task.Delay(600);
        Assert.True(r.Cancelled);
        Assert.Equal("Done", progress.Reports.Last().Stage);
    }

    [Fact]
    public async Task ScanAsync_RetriesKnownDevices_FindsSleepingPhone()
    {
        var p = new FakeProbes { WakesOnSecondArp = A("50") };
        p.Names[A("50")] = "pixel-7";
        var r = await new Scanner(p).ScanAsync(Net, null, CancellationToken.None, new[] { A("50") });
        var d = Assert.Single(r.Devices, x => x.Ip.Equals(A("50")));
        Assert.Equal("pixel-7", d.Hostname);
        Assert.Equal(2, p.ArpCalls[A("50")]);
    }

    [Fact]
    public async Task ScanAsync_NoRetryList_ProbesOnce()
    {
        var p = new FakeProbes { WakesOnSecondArp = A("50") };
        var r = await new Scanner(p).ScanAsync(Net, null, CancellationToken.None);
        Assert.DoesNotContain(r.Devices, x => x.Ip.Equals(A("50")));
        Assert.Equal(1, p.ArpCalls[A("50")]);
    }

    [Fact]
    public async Task ScanAsync_Retry_SkipsFoundOutOfRangeAndSelf()
    {
        var p = new FakeProbes();
        p.Arp[A("40")] = M("DC:A6:32:00:00:40");
        await new Scanner(p).ScanAsync(Net, null, CancellationToken.None,
            new[] { A("40"), IPAddress.Parse("10.9.9.9"), Net.Address, A("60"), A("60") });
        Assert.Equal(1, p.ArpCalls[A("40")]);
        Assert.False(p.ArpCalls.ContainsKey(IPAddress.Parse("10.9.9.9")));
        Assert.False(p.ArpCalls.ContainsKey(Net.Address));
        Assert.Equal(2, p.ArpCalls[A("60")]);
    }
}

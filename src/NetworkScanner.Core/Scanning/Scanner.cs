using System.Collections.Concurrent;
using System.Net;

namespace NetworkScanner.Core;

public sealed class Scanner(IProbes probes, int parallelism = 256)
{
    public const string FindingStage = "Finding devices";
    public const string NamesStage = "Looking up names";
    public const string DoneStage = "Done";

    public async Task<ScanResult> ScanAsync(LocalNetwork net, IProgress<ScanProgress>? progress, CancellationToken ct,
        IReadOnlyCollection<IPAddress>? retry = null)
    {
        var range = ScanRange.For(net.Address, net.PrefixLength);
        var hosts = range.Hosts.Where(h => !h.Equals(net.Address)).ToList();
        var found = new ConcurrentDictionary<IPAddress, (MacAddress? Mac, int? Ms)>();
        found[net.Address] = (net.Mac, 0);
        var trace = probes.TraceUpstreamAsync(net.Gateway, ct);

        async Task Probe(IPAddress ip)
        {
            var arp = probes.ArpAsync(ip, ct);
            var ping = probes.PingAsync(ip, ct);
            await Task.WhenAll(arp, ping);
            if (arp.Result is not null || ping.Result is not null) found[ip] = (arp.Result, ping.Result);
        }

        var done = 0;
        progress?.Report(new ScanProgress(0, hosts.Count, FindingStage));
        var sweep = Bounded(hosts, async ip =>
        {
            await Probe(ip);
            if (!ct.IsCancellationRequested) progress?.Report(new ScanProgress(Interlocked.Increment(ref done), hosts.Count, FindingStage));
        }, ct);
        var completed = await Finish(sweep, ct);

        var names = new ConcurrentDictionary<IPAddress, string>();
        var named = 0;
        Task Lookup(IReadOnlyCollection<IPAddress> ips, int total) => Bounded(ips, async ip =>
        {
            if (HostName.Normalize(await probes.ResolveNameAsync(ip, ct)) is { } name) names[ip] = name;
            if (!ct.IsCancellationRequested) progress?.Report(new ScanProgress(Interlocked.Increment(ref named), total, NamesStage));
        }, ct);

        if (completed)
        {
            var inRange = hosts.ToHashSet();
            var again = (retry ?? Array.Empty<IPAddress>()).Distinct().Where(ip => inRange.Contains(ip) && !found.ContainsKey(ip)).ToList();
            var online = found.Keys.ToList();
            progress?.Report(new ScanProgress(0, online.Count + again.Count, NamesStage));
            completed = await Finish(Task.WhenAll(Bounded(again, Probe, ct), Lookup(online, online.Count + again.Count)), ct);
            if (completed)
            {
                var woke = again.Where(found.ContainsKey).ToList();
                completed = await Finish(Lookup(woke, online.Count + again.Count), ct);
            }
        }

        var upstream = await Finish(trace, ct) ? trace.Result : Array.Empty<IPAddress>();
        var devices = found
            .Select(kv => new ScannedDevice(kv.Key, kv.Value.Mac, names.GetValueOrDefault(kv.Key), kv.Value.Ms))
            .OrderBy(d => IpUtil.ToUInt32(d.Ip))
            .ToList();
        progress?.Report(new ScanProgress(1, 1, DoneStage));
        return new ScanResult(net, devices, upstream, range.Capped, !completed);
    }

    Task Bounded<T>(IEnumerable<T> items, Func<T, Task> work, CancellationToken ct)
    {
        var gate = new SemaphoreSlim(parallelism);
        return Task.WhenAll(items.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try
            {
                await work(item);
            }
            finally
            {
                gate.Release();
            }
        }));
    }

    static async Task<bool> Finish(Task task, CancellationToken ct)
    {
        try
        {
            await task.WaitAsync(ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

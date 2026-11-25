using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NetworkScanner.Core;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsProbes : IProbes
{
    static readonly TimeSpan NameTimeout = TimeSpan.FromSeconds(1);
    static readonly TimeSpan PingTimeout = TimeSpan.FromMilliseconds(1000);
    static readonly IPAddress TraceTarget = IPAddress.Parse("1.1.1.1");
    const int MaxTraceHops = 4;

    public Task<MacAddress?> ArpAsync(IPAddress ip, CancellationToken ct) =>
        Task.Factory.StartNew<MacAddress?>(() =>
        {
            var mac = new byte[6];
            var len = mac.Length;
            var dest = BitConverter.ToUInt32(ip.GetAddressBytes());
            if (SendARP(dest, 0, mac, ref len) != 0 || len != 6) return null;
            var result = MacAddress.FromBytes(mac);
            return result.Value == 0 ? null : result;
        }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public async Task<int?> PingAsync(IPAddress ip, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, PingTimeout, null, null, ct);
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : null;
        }
        catch (PingException)
        {
            return null;
        }
    }

    public async Task<string?> ResolveNameAsync(IPAddress ip, CancellationToken ct)
    {
        var dns = ReverseDns(ip, ct);
        var mdns = Udp(ip, 5353, MdnsPacket.BuildPtrQuery(ip), b => MdnsPacket.ParsePtrAnswer(b), ct);
        var netbios = Udp(ip, 137, NetBiosPacket.BuildNodeStatusQuery((ushort)Random.Shared.Next(1, ushort.MaxValue)), b => NetBiosPacket.ParseNodeStatus(b), ct);
        await Task.WhenAll(dns, mdns, netbios);
        return dns.Result ?? mdns.Result ?? netbios.Result;
    }

    public async Task<IReadOnlyList<IPAddress>> TraceUpstreamAsync(IPAddress gateway, CancellationToken ct)
    {
        var hops = new List<IPAddress?>();
        var buffer = new byte[32];
        using var ping = new Ping();
        for (var ttl = 1; ttl <= MaxTraceHops; ttl++)
        {
            PingReply reply;
            try
            {
                reply = await ping.SendPingAsync(TraceTarget, PingTimeout, buffer, new PingOptions(ttl, true), ct);
            }
            catch (PingException)
            {
                break;
            }
            if (reply.Status == IPStatus.Success) break;
            hops.Add(reply.Status == IPStatus.TtlExpired ? reply.Address : null);
        }
        return UpstreamPath.Select(gateway, hops);
    }

    static async Task<string?> ReverseDns(IPAddress ip, CancellationToken ct)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(ip.ToString(), ct).WaitAsync(NameTimeout, ct);
            return entry.HostName;
        }
        catch (Exception e) when (e is SocketException or TimeoutException or ArgumentException)
        {
            return null;
        }
    }

    static async Task<string?> Udp(IPAddress ip, int port, byte[] query, Func<byte[], string?> parse, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            await udp.SendAsync(query, new IPEndPoint(ip, port), ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(NameTimeout);
            while (true)
            {
                var result = await udp.ReceiveAsync(timeout.Token);
                if (result.RemoteEndPoint.Address.Equals(ip)) return parse(result.Buffer);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial int SendARP(uint destIp, uint srcIp, [Out] byte[] macAddr, ref int physicalAddrLen);
}

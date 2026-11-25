using System.Net;

namespace NetworkScanner.Core;

public sealed record ScannedDevice(IPAddress Ip, MacAddress? Mac, string? Hostname, int? LatencyMs);

public sealed record ScanResult(
    LocalNetwork Network,
    IReadOnlyList<ScannedDevice> Devices,
    IReadOnlyList<IPAddress> UpstreamRouters,
    bool Capped,
    bool Cancelled);

public sealed record ScanProgress(int Done, int Total, string Stage, IReadOnlyList<ScannedDevice>? Devices = null);

public interface IProbes
{
    Task<MacAddress?> ArpAsync(IPAddress ip, CancellationToken ct);
    Task<int?> PingAsync(IPAddress ip, CancellationToken ct);
    Task<string?> ResolveNameAsync(IPAddress ip, CancellationToken ct);
    Task<IReadOnlyList<IPAddress>> TraceUpstreamAsync(IPAddress gateway, CancellationToken ct);
}

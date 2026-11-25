using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NetworkScanner.Core;

public sealed record LocalNetwork(string AdapterName, IPAddress Address, int PrefixLength, IPAddress Gateway, MacAddress Mac);

public sealed record InterfaceInfo(
    string Name,
    IPAddress? Address,
    int PrefixLength,
    IPAddress? Gateway,
    MacAddress Mac,
    bool IsUp,
    bool IsLoopbackOrTunnel,
    bool IsBestRoute);

public static partial class NetworkPicker
{
    static readonly string[] TunnelWords = { "WireGuard", "Tailscale", "TAP-Windows" };

    public static LocalNetwork? Choose(IEnumerable<InterfaceInfo> interfaces)
    {
        var usable = interfaces.Where(i => i.IsUp && !i.IsLoopbackOrTunnel && i.Address is not null && i.Gateway is not null).ToList();
        var pick = usable.FirstOrDefault(i => i.IsBestRoute) ?? usable.FirstOrDefault();
        return pick is null ? null : new LocalNetwork(pick.Name, pick.Address!, pick.PrefixLength, pick.Gateway!, pick.Mac);
    }

    public static bool IsTunnel(NetworkInterfaceType type, string description) =>
        type is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel
        || TunnelWords.Any(w => description.Contains(w, StringComparison.OrdinalIgnoreCase));

    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<InterfaceInfo> ReadSystem()
    {
        var best = GetBestInterface(BitConverter.ToUInt32(IPAddress.Parse("1.1.1.1").GetAddressBytes()), out var bestIndex) == 0
            ? (int)bestIndex
            : -1;
        var list = new List<InterfaceInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            var props = nic.GetIPProperties();
            var unicast = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
            var gateway = props.GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
            var index = nic.Supports(NetworkInterfaceComponent.IPv4) ? props.GetIPv4Properties()?.Index ?? -2 : -2;
            var macBytes = nic.GetPhysicalAddress().GetAddressBytes();
            var tunnel = IsTunnel(nic.NetworkInterfaceType, nic.Description);
            list.Add(new InterfaceInfo(
                nic.Name,
                unicast?.Address,
                unicast?.PrefixLength ?? 0,
                gateway,
                macBytes.Length == 6 ? MacAddress.FromBytes(macBytes) : default,
                nic.OperationalStatus == OperationalStatus.Up,
                tunnel,
                index == best));
        }
        return list;
    }

    [LibraryImport("iphlpapi.dll")]
    private static partial int GetBestInterface(uint destAddr, out uint bestIfIndex);
}

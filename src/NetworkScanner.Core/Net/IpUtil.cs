using System.Net;

namespace NetworkScanner.Core;

public static class IpUtil
{
    public static uint ToUInt32(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }

    public static IPAddress FromUInt32(uint v) =>
        new(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

    public static bool IsPrivate(IPAddress ip)
    {
        var v = ToUInt32(ip);
        return (v & 0xFF000000) == 0x0A000000
            || (v & 0xFFF00000) == 0xAC100000
            || (v & 0xFFFF0000) == 0xC0A80000
            || (v & 0xFFC00000) == 0x64400000;
    }
}

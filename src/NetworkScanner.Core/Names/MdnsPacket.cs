using System.Net;
using System.Text;

namespace NetworkScanner.Core;

public static class MdnsPacket
{
    const ushort TypePtr = 12;
    const int MaxJumps = 16;

    public static byte[] BuildPtrQuery(IPAddress ip)
    {
        var b = new List<byte> { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
        var labels = ip.GetAddressBytes().Reverse().Select(x => x.ToString()).Concat(new[] { "in-addr", "arpa" });
        foreach (var label in labels)
        {
            b.Add((byte)label.Length);
            b.AddRange(Encoding.ASCII.GetBytes(label));
        }
        b.AddRange(new byte[] { 0, 0x00, 0x0C, 0x80, 0x01 });
        return b.ToArray();
    }

    public static string? ParsePtrAnswer(ReadOnlySpan<byte> p)
    {
        if (p.Length < 12) return null;
        int questions = U16(p, 4), answers = U16(p, 6);
        var i = 12;
        for (var q = 0; q < questions; q++)
        {
            if (!SkipName(p, ref i)) return null;
            i += 4;
            if (i > p.Length) return null;
        }
        for (var a = 0; a < answers; a++)
        {
            if (!SkipName(p, ref i) || i + 10 > p.Length) return null;
            var type = U16(p, i);
            var length = U16(p, i + 8);
            i += 10;
            if (i + length > p.Length) return null;
            if (type == TypePtr) return ReadName(p, i);
            i += length;
        }
        return null;
    }

    internal static bool SkipName(ReadOnlySpan<byte> p, ref int i)
    {
        while (i < p.Length)
        {
            var len = p[i];
            if (len == 0)
            {
                i++;
                return true;
            }
            if ((len & 0xC0) == 0xC0)
            {
                i += 2;
                return i <= p.Length;
            }
            i += len + 1;
        }
        return false;
    }

    static string? ReadName(ReadOnlySpan<byte> p, int i)
    {
        var parts = new List<string>();
        var jumps = 0;
        while (i < p.Length)
        {
            var len = p[i];
            if (len == 0) return parts.Count == 0 ? null : string.Join(".", parts);
            if ((len & 0xC0) == 0xC0)
            {
                if (i + 1 >= p.Length || ++jumps > MaxJumps) return null;
                i = (len & 0x3F) << 8 | p[i + 1];
                continue;
            }
            if (i + 1 + len > p.Length) return null;
            parts.Add(Encoding.UTF8.GetString(p.Slice(i + 1, len)));
            i += len + 1;
        }
        return null;
    }

    internal static int U16(ReadOnlySpan<byte> p, int i) => p[i] << 8 | p[i + 1];
}

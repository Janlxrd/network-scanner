using System.Text;

namespace NetworkScanner.Core;

public static class NetBiosPacket
{
    const int EntrySize = 18;

    public static byte[] BuildNodeStatusQuery(ushort transactionId)
    {
        var b = new List<byte> { (byte)(transactionId >> 8), (byte)transactionId, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0x20 };
        var name = new byte[16];
        name[0] = (byte)'*';
        foreach (var c in name)
        {
            b.Add((byte)('A' + (c >> 4)));
            b.Add((byte)('A' + (c & 0x0F)));
        }
        b.AddRange(new byte[] { 0, 0x00, 0x21, 0x00, 0x01 });
        return b.ToArray();
    }

    public static string? ParseNodeStatus(ReadOnlySpan<byte> p)
    {
        if (p.Length < 12 || MdnsPacket.U16(p, 6) < 1) return null;
        var i = 12;
        if (!MdnsPacket.SkipName(p, ref i) || i + 11 > p.Length) return null;
        i += 10;
        int count = p[i++];
        if (i + count * EntrySize > p.Length) return null;
        for (var n = 0; n < count; n++, i += EntrySize)
        {
            var suffix = p[i + 15];
            var group = (p[i + 16] & 0x80) != 0;
            if (suffix != 0x00 || group) continue;
            var name = Encoding.ASCII.GetString(p.Slice(i, 15)).TrimEnd(' ', '\0');
            if (name.Length > 0) return name;
        }
        return null;
    }
}

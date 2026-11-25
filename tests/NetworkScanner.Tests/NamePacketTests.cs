using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class NamePacketTests
{
    const string Header = "000084000000000100000000";
    const string ReverseName = "023430013101310331363807696e2d61646472046172706100";

    [Fact]
    public void Mdns_Query_HasReversedNameAndUnicastBit()
    {
        var q = MdnsPacket.BuildPtrQuery(IPAddress.Parse("192.168.1.40"));
        var expected = Convert.FromHexString(
            "000000000001000000000000" +
            "023430" + "0131" + "03313638" + "03313932" + "07696e2d61646472" + "0461727061" + "00" +
            "000C8001");
        Assert.Equal(expected, q);
    }

    [Fact]
    public void Mdns_ParsesAnswer()
    {
        var pkt = Convert.FromHexString(Header + ReverseName + "000C000100000078" + "0013" + "0B7261737062657272797069056C6F63616C00");
        Assert.Equal("raspberrypi.local", MdnsPacket.ParsePtrAnswer(pkt));
    }

    [Fact]
    public void Mdns_ParsesCompressedPointer()
    {
        var pkt = Convert.FromHexString(Header + ReverseName + "000C000100000078" + "000E" + "0B7261737062657272797069C00C");
        Assert.Equal("raspberrypi.40.1.1.168.in-addr.arpa", MdnsPacket.ParsePtrAnswer(pkt));
    }

    [Fact]
    public void Mdns_SkipsQuestionSection()
    {
        var pkt = Convert.FromHexString("000084000001000100000000" + ReverseName + "000C0001" + "C00C" + "000C000100000078" + "0013" + "0B7261737062657272797069056C6F63616C00");
        Assert.Equal("raspberrypi.local", MdnsPacket.ParsePtrAnswer(pkt));
    }

    [Fact]
    public void Mdns_PointerLoop_ReturnsNull()
    {
        var pkt = Convert.FromHexString(Header + "C00C" + "000C000100000078" + "0002" + "C00C");
        Assert.Null(MdnsPacket.ParsePtrAnswer(pkt));
    }

    [Fact]
    public void Mdns_Truncated_ReturnsNull()
    {
        Assert.Null(MdnsPacket.ParsePtrAnswer(new byte[] { 0, 0, 0x84 }));
        var full = Convert.FromHexString(Header + ReverseName + "000C000100000078" + "0013" + "0B7261737062657272797069056C6F63616C00");
        for (var len = 0; len < full.Length; len++) MdnsPacket.ParsePtrAnswer(full.AsSpan(0, len));
    }

    [Fact]
    public void NetBios_Query_IsWildcardNbstat()
    {
        var q = NetBiosPacket.BuildNodeStatusQuery(0x1234);
        Assert.Equal(50, q.Length);
        Assert.Equal(new byte[] { 0x12, 0x34 }, q[..2]);
        Assert.Equal(0x20, q[12]);
        Assert.Equal("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", Encoding.ASCII.GetString(q, 13, 32));
        Assert.Equal(new byte[] { 0x00, 0x21, 0x00, 0x01 }, q[^4..]);
    }

    [Fact]
    public void NetBios_ParsesFirstUniqueWorkstationName()
    {
        var pkt = NetBiosTestData.NodeStatus(("WORKGROUP", 0x00, 0x8400), ("DESKTOP-ABC", 0x00, 0x0400), ("DESKTOP-ABC", 0x20, 0x0400));
        Assert.Equal("DESKTOP-ABC", NetBiosPacket.ParseNodeStatus(pkt));
    }

    [Fact]
    public void NetBios_Garbage_ReturnsNull()
    {
        Assert.Null(NetBiosPacket.ParseNodeStatus(new byte[] { 1, 2, 3 }));
        var full = NetBiosTestData.NodeStatus(("DESKTOP-ABC", 0x00, 0x0400));
        for (var len = 0; len < full.Length; len++) Assert.Null(NetBiosPacket.ParseNodeStatus(full.AsSpan(0, len)));
    }

}

static class NetBiosTestData
{
    public static byte[] NodeStatus(params (string name, byte suffix, ushort flags)[] names)
    {
        var b = new List<byte>(Convert.FromHexString("123484000000000100000000"));
        b.Add(0x20);
        b.AddRange(Encoding.ASCII.GetBytes("CKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"));
        b.Add(0x00);
        b.AddRange(new byte[] { 0x00, 0x21, 0x00, 0x01, 0, 0, 0, 0 });
        var rdLength = 1 + names.Length * 18;
        b.Add((byte)(rdLength >> 8));
        b.Add((byte)rdLength);
        b.Add((byte)names.Length);
        foreach (var (name, suffix, flags) in names)
        {
            b.AddRange(Encoding.ASCII.GetBytes(name.PadRight(15)));
            b.Add(suffix);
            b.Add((byte)(flags >> 8));
            b.Add((byte)flags);
        }
        return b.ToArray();
    }
}

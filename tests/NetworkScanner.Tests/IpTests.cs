using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class IpTests
{
    static IPAddress A(string s) => IPAddress.Parse(s);

    [Fact]
    public void For_Slash24_Returns254Hosts()
    {
        var r = ScanRange.For(A("192.168.1.23"), 24);
        Assert.Equal(254, r.Hosts.Count);
        Assert.Equal(A("192.168.1.1"), r.Hosts[0]);
        Assert.Equal(A("192.168.1.254"), r.Hosts[^1]);
        Assert.False(r.Capped);
    }

    [Fact]
    public void For_Slash22_NotCapped() =>
        Assert.Equal(1022, ScanRange.For(A("10.0.5.9"), 22).Hosts.Count);

    [Fact]
    public void For_Slash16_CapsToOwnSlash24()
    {
        var r = ScanRange.For(A("172.16.40.7"), 16);
        Assert.True(r.Capped);
        Assert.Equal(254, r.Hosts.Count);
        Assert.Equal(A("172.16.40.1"), r.Hosts[0]);
    }

    [Fact]
    public void For_Slash30_ReturnsTwoHosts() =>
        Assert.Equal(new[] { A("10.0.0.1"), A("10.0.0.2") }, ScanRange.For(A("10.0.0.1"), 30).Hosts);

    [Fact]
    public void For_Slash32_ReturnsSelf() =>
        Assert.Equal(new[] { A("10.0.0.9") }, ScanRange.For(A("10.0.0.9"), 32).Hosts);

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.31.0.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.0.1", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.128.0.1", false)]
    [InlineData("1.1.1.1", false)]
    public void IsPrivate_Ranges(string ip, bool expected) => Assert.Equal(expected, IpUtil.IsPrivate(A(ip)));
}

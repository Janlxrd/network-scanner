using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class UpstreamPathTests
{
    static readonly IPAddress Gw = IPAddress.Parse("192.168.55.1");

    static IPAddress? A(string? s) => s is null ? null : IPAddress.Parse(s);

    static IPAddress B(string s) => IPAddress.Parse(s);

    static IReadOnlyList<IPAddress> Run(params string?[] hops) => UpstreamPath.Select(Gw, hops.Select(A));

    [Fact]
    public void Normal_ReturnsPrivateHopsAfterGateway() =>
        Assert.Equal(new[] { B("10.22.1.13"), B("10.133.62.1") }, Run("192.168.55.1", "10.22.1.13", "10.133.62.1", "1.2.3.4"));

    [Fact]
    public void StopsAtFirstPublicHop() =>
        Assert.Equal(new[] { B("10.0.0.1") }, Run("192.168.55.1", "10.0.0.1", "8.8.4.4", "10.9.9.9"));

    [Fact]
    public void SkipsSilentHops() =>
        Assert.Equal(new[] { B("10.0.0.1") }, Run("192.168.55.1", null, "10.0.0.1"));

    [Fact]
    public void FirstHopNotGateway_ReturnsEmpty() =>
        Assert.Empty(Run("100.64.0.1", "10.0.0.1"));

    [Fact]
    public void FirstHopSilent_ReturnsEmpty() =>
        Assert.Empty(Run(null, "10.0.0.1"));
}

using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class MacAndOuiTests
{
    [Fact]
    public void TryParse_AcceptsColonsAndDashes()
    {
        Assert.True(MacAddress.TryParse("dc-a6-32-01-02-03", out var a));
        Assert.True(MacAddress.TryParse("DC:A6:32:01:02:03", out var b));
        Assert.Equal(a, b);
        Assert.Equal("DC:A6:32:01:02:03", a.ToString());
        Assert.Equal(0xDCA632u, a.Oui);
    }

    [Fact]
    public void TryParse_RejectsGarbage() => Assert.False(MacAddress.TryParse("not a mac", out _));

    [Theory]
    [InlineData("DA:A1:19:00:00:01", true)]
    [InlineData("DC:A6:32:00:00:01", false)]
    [InlineData("02:00:00:00:00:01", true)]
    public void IsLocallyAdministered(string s, bool expected)
    {
        MacAddress.TryParse(s, out var m);
        Assert.Equal(expected, m.IsLocallyAdministered);
    }

    [Fact]
    public void FromBytes_MatchesParse()
    {
        MacAddress.TryParse("DC:A6:32:01:02:03", out var parsed);
        Assert.Equal(parsed, MacAddress.FromBytes(new byte[] { 0xDC, 0xA6, 0x32, 1, 2, 3 }));
    }

    [Fact]
    public void Parse_AndLookup()
    {
        var db = OuiDatabase.Parse(new StringReader("DCA632\tRaspberry Pi Trading Ltd\n\nbad line\n001A11\tGoogle, Inc.\n"));
        MacAddress.TryParse("DC:A6:32:11:22:33", out var pi);
        MacAddress.TryParse("00:00:00:11:22:33", out var none);
        Assert.Equal("Raspberry Pi Trading Ltd", db.Lookup(pi));
        Assert.Null(db.Lookup(none));
    }

    [Fact]
    public void LoadEmbedded_HasRaspberryPi()
    {
        MacAddress.TryParse("DC:A6:32:11:22:33", out var pi);
        Assert.Contains("Raspberry Pi", OuiDatabase.LoadEmbedded().Lookup(pi));
    }
}

using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class LastSeenTextTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Online_IsNow() => Assert.Equal("now", DeviceText.LastSeenText(Now.AddDays(-3), Now, true));

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(59, "just now")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(60 * 60, "1 h ago")]
    [InlineData(23 * 3600, "23 h ago")]
    [InlineData(24 * 3600, "1 day ago")]
    [InlineData(3 * 86400, "3 days ago")]
    [InlineData(6 * 86400 + 3600, "6 days ago")]
    public void Offline_Relative(int secondsAgo, string expected) =>
        Assert.Equal(expected, DeviceText.LastSeenText(Now.AddSeconds(-secondsAgo), Now, false));

    [Fact]
    public void Offline_OverAWeek_ShowsDate() =>
        Assert.Equal("Nov 17", DeviceText.LastSeenText(Now.AddDays(-8), Now, false));
}

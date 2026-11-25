using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class MapLayoutTests
{
    static double R(Point2 p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);

    [Fact]
    public void Place_FirstIsHalfStepClockwiseFromTop()
    {
        var p = MapLayout.Place(4, 100);
        Assert.Equal(100 * Math.Sqrt(0.5), p[0].X, 6);
        Assert.Equal(-100 * Math.Sqrt(0.5), p[0].Y, 6);
    }

    [Fact]
    public void Place_GoesClockwise()
    {
        var p = MapLayout.Place(4, 100);
        Assert.True(p[1].Y > 0 && p[1].X > 0);
        Assert.True(p[2].Y > 0 && p[2].X < 0);
    }

    [Fact]
    public void Place_NothingStraightAboveRouter()
    {
        foreach (var n in new[] { 1, 2, 5, 16, 17, 30 })
            Assert.DoesNotContain(MapLayout.Place(n, 100), p => Math.Abs(p.X) < 1 && p.Y < 0);
    }

    [Fact]
    public void Place_SeventeenthStartsSecondRing()
    {
        var p = MapLayout.Place(17, 100);
        Assert.Equal(100, R(p[15]), 6);
        Assert.Equal(200, R(p[16]), 6);
    }

    [Fact]
    public void Place_Zero_ReturnsEmpty() => Assert.Empty(MapLayout.Place(0, 100));
}

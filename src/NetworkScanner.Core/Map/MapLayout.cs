namespace NetworkScanner.Core;

public readonly record struct Point2(double X, double Y);

public static class MapLayout
{
    public const int RingCapacity = 16;

    public static IReadOnlyList<Point2> Place(int count, double ringSpacing)
    {
        var points = new List<Point2>(count);
        for (var i = 0; i < count; i++)
        {
            var ring = i / RingCapacity;
            var inRing = Math.Min(RingCapacity, count - ring * RingCapacity);
            var angle = -Math.PI / 2 + 2 * Math.PI * (i % RingCapacity + 0.5) / inRing;
            var radius = ringSpacing * (ring + 1);
            points.Add(new Point2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
        }
        return points;
    }
}

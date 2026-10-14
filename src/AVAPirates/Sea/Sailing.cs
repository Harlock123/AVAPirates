namespace AVAPirates.Sea;

/// <summary>
/// How a square-rigged ship goes with the wind. Angles are degrees clockwise from north, and the
/// wind's angle is the way it blows toward. A ship is fastest across the wind or with it on her
/// quarter, slower running straight before it, and barely moves heading into it.
/// </summary>
public static class Sailing
{
    /// <summary>The smallest angle between two headings, 0 to 180.</summary>
    public static double Between(double a, double b)
    {
        double d = Math.Abs(Norm(a) - Norm(b)) % 360;
        return d > 180 ? 360 - d : d;
    }

    public static double Norm(double a) => ((a % 360) + 360) % 360;

    /// <summary>Signed turn from one heading to another, -180 to 180.</summary>
    public static double Turn(double from, double to)
    {
        double d = Norm(to - from);
        return d > 180 ? d - 360 : d;
    }

    private static readonly (double Angle, double Factor)[] Polar =
        [(0, 0.72), (30, 0.88), (60, 1.0), (100, 1.0), (125, 0.82), (140, 0.55), (155, 0.3), (180, 0.15)];

    /// <summary>How well a ship goes on a heading, 0.15 to 1, for a wind blowing toward an angle.</summary>
    public static double PointOfSail(double heading, double windTo)
    {
        double a = Between(heading, windTo);
        for (int i = 1; i < Polar.Length; i++)
            if (a <= Polar[i].Angle)
            {
                var (a0, f0) = Polar[i - 1];
                var (a1, f1) = Polar[i];
                return f0 + (f1 - f0) * (a - a0) / (a1 - a0);
            }
        return Polar[^1].Factor;
    }

    public static string PointName(double heading, double windTo) => Between(heading, windTo) switch
    {
        < 25 => "running before the wind",
        < 75 => "on a broad reach",
        < 115 => "on a beam reach",
        < 145 => "close-hauled",
        _ => "in irons",
    };

    /// <summary>The step a heading makes, as (x, y) per unit of distance (north is up, so y falls).</summary>
    public static (double X, double Y) Vector(double heading)
    {
        double r = heading * Math.PI / 180;
        return (Math.Sin(r), -Math.Cos(r));
    }

    /// <summary>The heading that points from one place to another.</summary>
    public static double Bearing(double x0, double y0, double x1, double y1) =>
        Norm(Math.Atan2(x1 - x0, -(y1 - y0)) * 180 / Math.PI);

    public static string Compass(double angle) =>
        new[] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" }[(int)Math.Round(Norm(angle) / 45) % 8];
}

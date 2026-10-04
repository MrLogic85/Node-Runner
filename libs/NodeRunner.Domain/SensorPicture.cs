namespace NodeRunner.Domain;

/// <summary>
/// Where a sensor's picture sits on its beam (#576): a square of its kind's <see cref="SizeOf"/>,
/// centred on the beam's midpoint and turned with the beam. A beam holds one sensor (#593), so this
/// is also the sensor's tap area. Stateless and shared by Build's canvas and gestures and the
/// creature in Training. See docs/CREATURE_MODEL.md.
/// </summary>
public static class SensorPicture
{
    /// <summary>The Accelerometer picture's side, in canvas units.</summary>
    public const double AccelerometerSize = 24;

    /// <summary>The Camera picture's side, in canvas units: twice the first size so it reads on a phone (#622).</summary>
    public const double CameraSize = 44;

    /// <summary>The largest picture's side; every beam leaves room for it (#593), since any sensor can go on any beam.</summary>
    public const double LargestSize = CameraSize;

    /// <summary>The picture's side for <paramref name="kind"/>, in canvas units.</summary>
    public static double SizeOf(SensorKind kind) => kind switch
    {
        SensorKind.Accelerometer => AccelerometerSize,
        SensorKind.Camera => CameraSize,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sensor kind."),
    };

    /// <summary>Whether <paramref name="point"/> is on the picture of the <paramref name="kind"/> sensor on the beam from <paramref name="nodeA"/> to <paramref name="nodeB"/>.</summary>
    public static bool Contains(SensorKind kind, Vector2D point, Vector2D nodeA, Vector2D nodeB)
    {
        var half = SizeOf(kind) / 2;
        var dx = nodeB.X - nodeA.X;
        var dy = nodeB.Y - nodeA.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var (alongX, alongY) = length > 0 ? (dx / length, dy / length) : (1.0, 0.0);
        var relativeX = point.X - ((nodeA.X + nodeB.X) / 2);
        var relativeY = point.Y - ((nodeA.Y + nodeB.Y) / 2);
        var along = (relativeX * alongX) + (relativeY * alongY);
        var across = (-relativeX * alongY) + (relativeY * alongX);
        return Math.Abs(along) <= half && Math.Abs(across) <= half;
    }
}

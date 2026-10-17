namespace NodeRunner.Domain;

/// <summary>
/// Where a sensor's picture sits on its beam: a square of its kind's <see cref="SizeOf"/>,
/// centred midway between what is drawn over the beam's two ends (<see cref="Shift"/>) and
/// turned with the beam. A beam holds one sensor, so this is also the sensor's
/// tap area, and the sensor senses from its centre. Stateless. See docs/CREATURE_MODEL.md.
/// </summary>
public static class SensorPicture
{
    /// <summary>The Accelerometer picture's side, in canvas units.</summary>
    public const double AccelerometerSize = 24;

    /// <summary>The Camera picture's side, in canvas units: twice the first size so it reads on a phone.</summary>
    public const double CameraSize = 44;

    /// <summary>The picture's side for <paramref name="kind"/>, in canvas units.</summary>
    public static double SizeOf(SensorKind kind) => kind switch
    {
        SensorKind.Accelerometer => AccelerometerSize,
        SensorKind.Camera => CameraSize,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sensor kind."),
    };

    /// <summary>
    /// How far the picture's centre sits from its beam's midpoint, towards node B: midway
    /// between what is drawn over the beam's two ends, its hubs or a motor
    /// (<see cref="CreatureDef.CoveredRadius(int, IEnumerable{IJointMotorDef})"/>), so at the midpoint
    /// unless a motor sits on one end. A Wheel's size does not move it: the Wheel is see-through
    /// under its links.
    /// </summary>
    public static double Shift(double coveredA, double coveredB) => (coveredA - coveredB) / 2;

    /// <summary>The picture's centre on the beam from <paramref name="nodeA"/> to <paramref name="nodeB"/>: see <see cref="Shift"/>.</summary>
    public static Vector2D Centre(Vector2D nodeA, Vector2D nodeB, double coveredA, double coveredB)
    {
        var (alongX, alongY) = Along(nodeA, nodeB);
        var shift = Shift(coveredA, coveredB);
        return new Vector2D(((nodeA.X + nodeB.X) / 2) + (alongX * shift), ((nodeA.Y + nodeB.Y) / 2) + (alongY * shift));
    }

    public static bool Contains(SensorKind kind, Vector2D point, Vector2D nodeA, Vector2D nodeB, double coveredA, double coveredB)
    {
        var half = SizeOf(kind) / 2;
        var (alongX, alongY) = Along(nodeA, nodeB);
        var centre = Centre(nodeA, nodeB, coveredA, coveredB);
        var relativeX = point.X - centre.X;
        var relativeY = point.Y - centre.Y;
        var along = (relativeX * alongX) + (relativeY * alongY);
        var across = (-relativeX * alongY) + (relativeY * alongX);
        return Math.Abs(along) <= half && Math.Abs(across) <= half;
    }

    private static (double X, double Y) Along(Vector2D nodeA, Vector2D nodeB)
    {
        var dx = nodeB.X - nodeA.X;
        var dy = nodeB.Y - nodeA.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        return length > 0 ? (dx / length, dy / length) : (1.0, 0.0);
    }
}

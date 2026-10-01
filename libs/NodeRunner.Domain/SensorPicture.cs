namespace NodeRunner.Domain;

/// <summary>
/// Where a sensor's picture sits on its beam (#576): a <see cref="Size"/> square centred on the
/// beam's midpoint and turned with the beam. A beam holds one sensor (#593), so this is also the
/// sensor's tap area. Stateless and shared by Build's canvas and gestures and the creature in
/// Training, like <see cref="LineOfSight"/>. See docs/CREATURE_MODEL.md.
/// </summary>
public static class SensorPicture
{
    /// <summary>The picture's side, in canvas units; it fits in the free length a beam must leave (#593).</summary>
    public const double Size = 24;

    /// <summary>Whether <paramref name="point"/> is on the picture of the sensor on the beam from <paramref name="nodeA"/> to <paramref name="nodeB"/>.</summary>
    public static bool Contains(Vector2D point, Vector2D nodeA, Vector2D nodeB)
    {
        var dx = nodeB.X - nodeA.X;
        var dy = nodeB.Y - nodeA.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var (alongX, alongY) = length > 0 ? (dx / length, dy / length) : (1.0, 0.0);
        var relativeX = point.X - ((nodeA.X + nodeB.X) / 2);
        var relativeY = point.Y - ((nodeA.Y + nodeB.Y) / 2);
        var along = (relativeX * alongX) + (relativeY * alongY);
        var across = (-relativeX * alongY) + (relativeY * alongX);
        return Math.Abs(along) <= Size / 2 && Math.Abs(across) <= Size / 2;
    }
}

namespace NodeRunner.Domain;

public sealed record JointDef
{
    public JointDef(Vector2D position, double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Joint radius must be finite and positive.");
        }

        Position = position;
        Radius = radius;
    }

    public Vector2D Position { get; }

    public double Radius { get; }
}

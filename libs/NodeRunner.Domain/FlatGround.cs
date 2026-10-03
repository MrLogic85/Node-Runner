namespace NodeRunner.Domain;

/// <summary>Level ground at the ground line, everywhere.</summary>
public sealed record FlatGround : MapGround
{
    public override double HeightAt(double x) => 0;
}

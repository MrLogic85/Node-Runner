namespace NodeRunner.Domain;

/// <summary>
/// The shape of a map's ground: its height above the arena's ground line at a world x, in world
/// units, up positive. The same x always gives the same height, so every trial meets the same
/// ground. Endless: a creature never walks off its end.
/// </summary>
public abstract record MapGround
{
    public abstract double HeightAt(double x);
}

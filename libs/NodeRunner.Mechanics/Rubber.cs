namespace NodeRunner.Mechanics;

/// <summary>
/// A Rubber's grip (#1142): its joint's friction against the ground, twice a bare joint's 1. See
/// docs/CREATURE_MODEL.md → "Rubber".
/// </summary>
public static class Rubber
{
    /// <summary>The friction of a joint, or its Wheel's tyre, with a Rubber: twice a bare joint's.</summary>
    public const double Friction = 2;
}

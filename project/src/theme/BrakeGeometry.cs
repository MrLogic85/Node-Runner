using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// A Brake at rotation 0: a plain joint's size, as its joint collides as a plain joint
/// does. Its caliper's four pads lie either side of the Fixed side and turn with it; its disc and
/// hand turn with the Target side. A gap between pads and disc is where a slip shows.
/// </summary>
public static class BrakeGeometry
{
    /// <summary>The Brake's edge: a plain joint's radius, which it covers.</summary>
    public const float Radius = (float)NodeDef.PlainJointRadius;

    /// <summary>How far each pad lies from the Fixed side's line.</summary>
    public const float PadOffset = 10;

    public const float PadInner = 2.5f;
    public const float PadOuter = 6.5f;

    /// <summary>The centre line of the disc's ring.</summary>
    public const float DiscRadius = 6;

    /// <summary>The hand runs out past the disc ring to just inside the pads, so it sweeps past them on a slip.</summary>
    public const float HandLength = 8.5f;

    /// <summary>A Locked Brake's solid disc, out to the disc ring's outer edge.</summary>
    public const float LockedDiscRadius = 7;

    /// <summary>The four pads as strokes, two either side of the Fixed side.</summary>
    public static Vector2[][] PadStrokes() =>
    [
        [new(PadInner, PadOffset), new(PadOuter, PadOffset)],
        [new(-PadInner, PadOffset), new(-PadOuter, PadOffset)],
        [new(PadInner, -PadOffset), new(PadOuter, -PadOffset)],
        [new(-PadInner, -PadOffset), new(-PadOuter, -PadOffset)],
    ];
}

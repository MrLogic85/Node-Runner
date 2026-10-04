using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Piston's <see cref="PistonPart"/> in Training (#451), its ends following its two node bodies
/// every frame, so the rod slides in and out of its cylinder live.
/// </summary>
public partial class PistonVisual : PistonPart, IShadowVisual
{
    /// <summary>Links draw on a shadow as on the followed creature (docs/CREATURE_MODEL.md).</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Same;

    public required PistonLink Link { get; init; }

    /// <summary>Nothing to switch: <see cref="Creature"/> never selects a shadow's Piston.</summary>
    public bool IsShadow { get; set; }

    public override void _Process(double delta)
    {
        A = ToLocal(Link.NodeA.GlobalPosition);
        B = ToLocal(Link.NodeB.GlobalPosition);
        QueueRedraw();
    }
}

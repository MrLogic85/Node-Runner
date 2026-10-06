using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Spring's <see cref="SpringPart"/> in Training (#453), its ends following its two node bodies
/// every frame, so the coil stretches and squeezes live.
/// </summary>
public partial class SpringVisual : SpringPart, IShadowVisual
{
    /// <summary>
    /// Links draw on a shadow as on the followed creature (docs/WORLD_VISUALS.md → "Drawing as a
    /// shadow").
    /// </summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Same;

    public required RigidBody2D NodeA { get; init; }

    public required RigidBody2D NodeB { get; init; }

    /// <summary>Nothing to switch: <see cref="Creature"/> never selects a shadow's Spring.</summary>
    public bool IsShadow { get; set; }

    public override void _Process(double delta)
    {
        A = ToLocal(NodeA.GlobalPosition);
        B = ToLocal(NodeB.GlobalPosition);
    }
}

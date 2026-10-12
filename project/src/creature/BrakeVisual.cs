using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Brake's caliper and disc in Training (#1041): its caliper's layer turns with the Fixed side and
/// its disc's layer with the Target side, each by its rotation alone, so it is never redrawn as the
/// creature moves and a slip shows as the hand sweeping past the pads.
/// </summary>
public partial class BrakeVisual : BrakePart, IShadowVisual
{
    /// <summary>A shadow's Brake is its outer ring and its disc's ring, neither turning.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public required MotorSides Sides { get; init; }

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }

    // Unturned, so the layers' angles are the sides' world angles.
    public override void _Process(double delta)
    {
        GlobalTransform = new Transform2D(0, Sides.JointPosition);
        FixedAngle = (float)Sides.FixedRotation;
        TargetAngle = (float)Sides.TargetRotation;
    }
}

using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Velocity motor's hub motor in Training: its coils' layer turns with the Fixed side and its
/// magnets' layer with the Target side, each by its rotation alone, so it is never redrawn as the
/// creature moves.
/// </summary>
public partial class VelocityMotorVisual : VelocityMotorPart, IShadowVisual
{
    /// <summary>A shadow's motor is its outer ring and a circle round the coils, neither turning.</summary>
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

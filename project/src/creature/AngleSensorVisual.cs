using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// An Angle sensor's ring and arc in Training (#1205): the arc follows its sides' live directions,
/// so it shows the angle the brain reads, and the followed creature's <see cref="Knockout"/> runs
/// along it, so it never merges into the ground line.
/// </summary>
public partial class AngleSensorVisual : AngleSensorPart, IShadowVisual
{
    /// <summary>A shadow's Angle sensor is a plain shadow joint, as shadows draw no sensors.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public required MotorSides Sides { get; init; }

    /// <summary>The knock-out along the arc, a sibling under the whole creature.</summary>
    public required KnockoutPart Knockout { get; init; }

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }

    // Unturned, so the angles are the sides' world angles. A shadow's plain joint never turns, so it is never redrawn.
    public override void _Process(double delta)
    {
        GlobalTransform = new Transform2D(0, Sides.JointPosition);
        if (IsShadow)
        {
            return;
        }

        FixedAngle = (float)Sides.FixedRotation;
        TargetAngle = (float)Sides.TargetRotation;
        Knockout.GlobalTransform = GlobalTransform;
        Knockout.Path = Arc;
    }
}

using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Stepper's detent wheel and pawl in Training (#1067): its rim's layer turns with the Fixed side and
/// its pawl's with the Target side, each by its rotation alone, so it is not redrawn while it holds. Each
/// frame it shows its <see cref="StepperJoint"/>'s state through <see cref="StepperLooks"/>.
/// </summary>
public partial class StepperVisual : StepperPart, IShadowVisual
{
    /// <summary>A shadow's Stepper is its rim's and pawl's outlines.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public required StepperJoint Joint { get; init; }

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }

    // Unturned, so the layers' angles are the sides' world angles.
    public override void _Process(double delta)
    {
        var sides = Joint.Sides;
        GlobalTransform = new Transform2D(0, sides.JointPosition);
        Turn((float)sides.FixedRotation, (float)sides.TargetRotation);
        var state = Joint.State;

        // A torn Stepper drives nothing, so its last state stands; it shows as holding.
        Look = StepperLooks.Next(state.Releasing && !Joint.IsPassive, state.Notch, state.Direction, sides.ContinuousAngle - sides.BuiltAngle, StepsPerTurn);
    }
}

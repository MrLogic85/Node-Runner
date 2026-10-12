using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>A Velocity motor's housing and horn in Training, following the Fixed side.</summary>
public partial class VelocityMotorVisual : VelocityMotorPart, IShadowVisual
{
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public required MotorSides Sides { get; init; }

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }

    public override void _Process(double delta)
    {
        GlobalPosition = Sides.JointPosition;
        GlobalRotation = (float)Sides.FixedRotation;
        BuiltAngle = (float)Sides.BuiltRelativeRotation;
        TargetAngle = (float)Sides.TargetRelativeRotation;
        QueueRedraw();
    }
}

using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>A Servo's housing, range band and horn in Training, following the fixed link.</summary>
public partial class ServoVisual : ServoPart, IShadowVisual
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

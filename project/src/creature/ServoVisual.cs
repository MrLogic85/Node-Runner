using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>A Servo's housing, range band and horn in Training, following the fixed link.</summary>
public partial class ServoVisual : ServoPart, IShadowVisual
{
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public required ServoJoint Link { get; init; }

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }

    public override void _Process(double delta)
    {
        GlobalPosition = Link.JointPosition;
        GlobalRotation = (float)Link.FixedRotation;
        BuiltAngle = (float)Link.BuiltRelativeRotation;
        TargetAngle = (float)Link.TargetRelativeRotation;
        QueueRedraw();
    }
}

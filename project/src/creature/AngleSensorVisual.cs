using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// An Angle sensor's ring and arc in Training: the arc follows its sides' live directions,
/// so it shows the angle the network reads, and fills and empties with each turn counted from the built
/// pose (<see cref="AngleSensorTurn"/>); each trial builds a new one, so the count starts again at rest.
/// The followed creature's <see cref="Knockout"/> runs along it, so it never merges into the ground line.
/// </summary>
public partial class AngleSensorVisual : AngleSensorPart, IShadowVisual
{
    /// <summary>A shadow's Angle sensor is a plain shadow joint, as shadows draw no sensors.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public required HingePartSides Sides { get; init; }

    /// <summary>The knock-out along the arc, a sibling under the whole creature.</summary>
    public required KnockoutPart Knockout { get; init; }

    private float _restAngle;
    private int _restTurn;

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }

    // The built pose, which Build draws at rest.
    public override void _Ready()
    {
        base._Ready();
        _restAngle = AngleSensorGeometry.Angle(FixedAngle, TargetAngle);
        _restTurn = AngleSensorGeometry.RestTurn(FixedAngle, TargetAngle);
        Turn = _restTurn;
    }

    // Unturned, so the angles are the sides' world angles. A shadow's plain joint never turns.
    public override void _Process(double delta)
    {
        GlobalTransform = new Transform2D(0, Sides.JointPosition);
        if (IsShadow)
        {
            return;
        }

        FixedAngle = (float)Sides.FixedRotation;
        TargetAngle = (float)Sides.TargetRotation;
        var angle = AngleSensorGeometry.Angle(FixedAngle, TargetAngle);
        Turn = AngleSensorTurn.At(_restAngle, _restTurn, Sides.ContinuousAngle - Sides.BuiltAngle, angle);
        Knockout.GlobalTransform = GlobalTransform;
        Knockout.Path = Arc;
    }
}

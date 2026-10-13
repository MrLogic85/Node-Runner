using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// An Angle sensor (#1205): a plain joint's ring and inner ring in <c>accent</c> on an opaque
/// <c>panel</c> disc, standing in for its joint's ring or a Wheel's hub, and round it an <c>accent</c>
/// arc between the Fixed side's direction and the Target side's that fills and empties with each
/// <see cref="Turn"/> (<see cref="AngleSensorGeometry"/>).
/// Both draw over the links, the arc with a knock-out edge (<c>docs/WORLD_VISUALS.md</c> → Angle sensor).
/// A sensor missing a side draws its ring in <c>danger</c> with the "!" badge and no arc.
/// Selected, it draws a halo outside the arc.
/// </summary>
public partial class AngleSensorPart : PartVisual
{
    private const int _haloSegments = 64;

    private float _fixedAngle;
    private float _targetAngle;
    private float _arcRadius = (float)AngleSensorDef.ArcRadius(rubber: false, touch: false);
    private int? _turn;
    private bool _hasFixed = true;
    private bool _hasTarget = true;
    private bool _simplified;
    private Vector2[]? _arc;

    public AngleSensorPart()
        : base(DrawSlot.Over)
    {
    }

    /// <summary>The Fixed side's direction, where the arc starts, in the part's frame.</summary>
    public float FixedAngle
    {
        get => _fixedAngle;
        set => Change(ref _fixedAngle, value);
    }

    /// <summary>The Target side's direction, where the arc ends, in the part's frame.</summary>
    public float TargetAngle
    {
        get => _targetAngle;
        set => Change(ref _targetAngle, value);
    }

    /// <summary>The arc's centre line, outside the joint's outermost part (<see cref="AngleSensorDef.ArcRadius"/>).</summary>
    public float ArcRadius
    {
        get => _arcRadius;
        set => Change(ref _arcRadius, value);
    }

    /// <summary>
    /// The turn the sides are on, which picks the arc's way round (<see cref="AngleSensorGeometry.Sweep"/>);
    /// null at rest (<see cref="AngleSensorGeometry.RestTurn"/>), as Build and thumbnails draw it.
    /// </summary>
    public int? Turn
    {
        get => _turn;
        set => Change(ref _turn, value);
    }

    public bool HasFixed
    {
        get => _hasFixed;
        set => Change(ref _hasFixed, value);
    }

    public bool HasTarget
    {
        get => _hasTarget;
        set => Change(ref _hasTarget, value);
    }

    /// <summary>A Training shadow's look: a plain shadow joint, as shadows draw no sensors.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>The arc's centre line in the part's frame (<see cref="AngleSensorGeometry.ArcPoints"/>).</summary>
    public Vector2[] Arc => _arc ??= AngleSensorGeometry.ArcPoints(ArcRadius, HasFixed ? FixedAngle : null, HasTarget ? TargetAngle : null, Turn);

    /// <summary>A sensor missing a side cannot train, so it draws in danger with the badge and no arc (#1111).</summary>
    private bool Missing => !HasFixed || !HasTarget;

    protected override void Changed()
    {
        _arc = null;
        base.Changed();
    }

    public override void _Draw()
    {
        if (Simplified)
        {
            JointDrawing.DrawPlain(this, Theme, Transform2D.Identity, Vector2.Zero, AngleSensorGeometry.Radius, JointLook.Plain, simplified: true);
            return;
        }

        using var pen = UiPixelPen.Begin(this);
        var line = Missing ? Theme.Danger : Theme.MotorAccent;
        pen.Disc(Vector2.Zero, AngleSensorGeometry.Radius, Theme.ArenaBackground);
        pen.Disc(Vector2.Zero, AngleSensorGeometry.Radius, Theme.ServoPanelFill);
        pen.Ring(Vector2.Zero, WheelPart.TyreRingRadius(AngleSensorGeometry.Radius, Theme.JointRingWidth), line, Theme.JointRingWidth);
        pen.Ring(Vector2.Zero, AngleSensorGeometry.Radius * JointDrawing.InnerRingPerRadius, line, Theme.JointInnerRingWidth);
        DrawArc(pen);

        if (Selected)
        {
            pen.Ring(Vector2.Zero, (float)AngleSensorDef.HaloRadius(ArcRadius), Theme.SelectionGlow, (float)AngleSensorDef.HaloWidth, _haloSegments);
        }

        if (Missing)
        {
            JointMotorDrawing.DrawDangerBadge(pen, Theme, AngleSensorGeometry.Radius);
        }
    }

    // A stroke of fewer than two points draws nothing, so the sides in line draw the round ends' dot.
    private void DrawArc(UiPixelPen pen)
    {
        const float knockedOut = AngleSensorGeometry.ArcWidth + (2 * AngleSensorGeometry.KnockoutWidth);
        if (Arc.Length == 1)
        {
            pen.Disc(Arc[0], knockedOut / 2, Theme.ArenaBackground);
            pen.Disc(Arc[0], AngleSensorGeometry.ArcWidth / 2, Theme.MotorAccent);
        }
        else if (Arc.Length > 1)
        {
            pen.Strokes([Arc], Theme.ArenaBackground, knockedOut);
            pen.Strokes([Arc], Theme.MotorAccent, AngleSensorGeometry.ArcWidth);
        }
    }
}

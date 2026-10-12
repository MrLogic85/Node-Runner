using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A Brake (#1041): a caliper and disc in <c>detail</c>, as it is passive, that stands in for its
/// joint's ring or a Wheel's hub (<see cref="BrakeGeometry"/>). Its caliper turns with the Fixed side
/// and its disc with the Target side, each drawn once on its own layer behind the part, so a moving
/// creature never redraws it and a slip shows as the hand sweeping past the pads. A Locked Brake's
/// disc is solid, with no hand. The part itself draws its marks over both.
/// </summary>
public partial class BrakePart : PartVisual
{
    private static readonly Vector2[][] _pads = BrakeGeometry.PadStrokes();

    private readonly Node2D _caliperLayer = new() { Name = "Caliper", ShowBehindParent = true };
    private readonly Node2D _discLayer = new() { Name = "Disc", ShowBehindParent = true };
    private bool _hasFixed = true;
    private bool _hasTarget = true;
    private bool _locked;
    private bool _simplified;

    public BrakePart()
        : base(DrawSlot.Over)
    {
        _caliperLayer.Draw += DrawCaliperLayer;
        _discLayer.Draw += DrawDiscLayer;
        AddChild(_caliperLayer);
        AddChild(_discLayer);
    }

    /// <summary>The Fixed side's direction, which the caliper turns with, in the part's frame.</summary>
    public float FixedAngle
    {
        get => _caliperLayer.Rotation;
        set => _caliperLayer.Rotation = value;
    }

    /// <summary>The Target side's direction, which the disc turns with, in the part's frame.</summary>
    public float TargetAngle
    {
        get => _discLayer.Rotation;
        set => _discLayer.Rotation = value;
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

    /// <summary>A Locked Brake (#1039), which holds its joint as one piece: a solid disc.</summary>
    public bool Locked
    {
        get => _locked;
        set => Change(ref _locked, value);
    }

    /// <summary>A Training shadow's look: the outer ring and the disc's ring, which do not turn.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>A Brake missing a side cannot train, so the whole part draws in danger (#1111).</summary>
    private bool Missing => !HasFixed || !HasTarget;

    private Color Line => Missing ? Theme.Danger : Theme.BrakeLine;

    private float RingRadius => WheelPart.TyreRingRadius(BrakeGeometry.Radius, Theme.JointRingWidth);

    protected override void Changed()
    {
        base.Changed();
        _caliperLayer.QueueRedraw();
        _discLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        if (Simplified)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(this);
        if (Selected)
        {
            JointMotorDrawing.DrawHalo(pen, Theme, BrakeGeometry.Radius);
        }

        if (Missing)
        {
            JointMotorDrawing.DrawDangerBadge(pen, Theme, BrakeGeometry.Radius);
        }
    }

    private void DrawCaliperLayer()
    {
        using var pen = UiPixelPen.Begin(_caliperLayer);
        if (Simplified)
        {
            pen.Ring(Vector2.Zero, RingRadius, Line, Theme.JointRingWidth);
            pen.Ring(Vector2.Zero, BrakeGeometry.DiscRadius, Line, Theme.MotorSignalWidth);
            return;
        }

        pen.Disc(Vector2.Zero, BrakeGeometry.Radius, Theme.ArenaBackground);
        pen.Disc(Vector2.Zero, BrakeGeometry.Radius, Theme.ServoPanelFill);
        pen.Ring(Vector2.Zero, RingRadius, Line, Theme.JointRingWidth);
        pen.Strokes(_pads, Line, Theme.MotorSignalWidth);
    }

    private void DrawDiscLayer()
    {
        if (Simplified)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(_discLayer);
        if (Locked)
        {
            pen.Disc(Vector2.Zero, BrakeGeometry.LockedDiscRadius, Line);
            return;
        }

        pen.Ring(Vector2.Zero, BrakeGeometry.DiscRadius, Line, Theme.MotorSignalWidth);
        pen.Line(Vector2.Zero, new Vector2(BrakeGeometry.HandLength, 0), Line, Theme.MotorSignalWidth);
        pen.Disc(Vector2.Zero, WheelPart.AxleRadius, Line);
    }
}

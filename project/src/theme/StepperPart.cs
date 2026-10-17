using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A Stepper: a detent wheel in <c>accent</c> that stands in for its joint's ring or a Wheel's
/// hub (<see cref="StepperGeometry"/>). Its notched rim turns with the Fixed side and its pawl with the
/// Target side, each drawn on its own layer behind the part, so a holding creature never redraws it.
/// While it steps (<see cref="Look"/>) the pawl lifts and a soft wedge runs from it to the notch it steps
/// to. The part itself draws the hub and the missing-link
/// badge over them all, and its selection mark turns with the pawl on a layer over the rim and pawl.
/// </summary>
public partial class StepperPart : PartVisual
{
    // Under this, the wedge is too thin to see and too thin for Godot to fill.
    private const float _leastWedgeSweep = 0.01f;

    private readonly Node2D _rimLayer = new() { Name = "Rim", ShowBehindParent = true };
    private readonly Node2D _pawlLayer = new() { Name = "Pawl", ShowBehindParent = true };
    private readonly Node2D _haloLayer = new() { Name = "Halo", ShowBehindParent = true };
    private int _stepsPerTurn = StepperDef.DefaultStepsPerTurn;
    private float _builtAngle;
    private bool _hasFixed = true;
    private bool _hasTarget = true;
    private bool _cushioned;
    private bool _simplified;
    private StepperLook _look;

    public StepperPart()
        : base(DrawSlot.Over)
    {
        _rimLayer.Draw += DrawRimLayer;
        _pawlLayer.Draw += DrawPawlLayer;
        _haloLayer.Draw += DrawHaloLayer;
        AddChild(_rimLayer);
        AddChild(_pawlLayer);
        AddChild(_haloLayer);
    }

    public int StepsPerTurn
    {
        get => _stepsPerTurn;
        set => Change(ref _stepsPerTurn, value);
    }

    /// <summary>The Fixed side's direction, which the rim turns with, in the part's frame.</summary>
    public float FixedAngle => _rimLayer.Rotation;

    /// <summary>The Target side's direction, which the pawl turns with, in the part's frame.</summary>
    public float TargetAngle => _pawlLayer.Rotation;

    /// <summary>Notch 0's direction in the Fixed side's frame: the Target side's as built.</summary>
    public float BuiltAngle
    {
        get => _builtAngle;
        set => Change(ref _builtAngle, value);
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

    /// <summary>Whether its joint has a Touch sensor's cushion, which its selection mark goes round.</summary>
    public bool Cushioned
    {
        get => _cushioned;
        set => Change(ref _cushioned, value);
    }

    /// <summary>A Training shadow's look: a plain rim circle and the pawl's outline only.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>The pawl and wedge of a stepping Stepper; Build shows <see cref="StepperLook.Hold"/>.</summary>
    public StepperLook Look
    {
        get => _look;
        set
        {
            var last = _look;
            _look = value;

            // Only the pawl's layer shows the look, so a step redraws it alone each frame.
            if (last.Lifted != value.Lifted || (!Simplified && last.Sweep != value.Sweep))
            {
                _pawlLayer.QueueRedraw();
            }
        }
    }

    /// <summary>
    /// Turns the rim to <paramref name="fixedAngle"/> and the pawl, with the selection mark's keyhole, to
    /// <paramref name="targetAngle"/>, by their layers' rotations alone, so nothing is drawn again.
    /// </summary>
    public void Turn(float fixedAngle, float targetAngle)
    {
        _rimLayer.Rotation = fixedAngle;
        _pawlLayer.Rotation = targetAngle;
        _haloLayer.Rotation = targetAngle;
    }

    // A Stepper missing a side cannot train, so it draws in danger.
    private bool Missing => !HasFixed || !HasTarget;

    private Color Line => Missing ? Theme.Danger : Theme.MotorAccent;

    private bool ShowsHalo => Selected && !Simplified;

    protected override void Changed()
    {
        base.Changed();
        _rimLayer.QueueRedraw();
        _pawlLayer.QueueRedraw();
        _haloLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        if (Simplified)
        {
            return;
        }

        using (var pen = UiPixelPen.Begin(this))
        {
            pen.Ring(Vector2.Zero, StepperGeometry.HubRingRadius, Line, UiSize.Stroke.Hair);
            pen.Disc(Vector2.Zero, StepperGeometry.HubDiscRadius, Line);
            if (Missing)
            {
                JointMotorDrawing.DrawDangerBadge(pen, Theme, StepperGeometry.Radius);
            }
        }
    }

    private void DrawRimLayer()
    {
        using var pen = UiPixelPen.Begin(_rimLayer);
        var plain = StepperGeometry.IsPlain(StepsPerTurn, pen.Scale, Simplified);
        if (!Simplified)
        {
            var fill = Selected ? Theme.SelectionFill : Theme.JointFill;
            pen.Disc(Vector2.Zero, StepperGeometry.Radius, Theme.ArenaBackground);
            pen.Disc(Vector2.Zero, StepperGeometry.Radius, fill);
        }

        if (plain)
        {
            pen.Ring(Vector2.Zero, StepperGeometry.RimRadius, Line, Theme.MotorSignalWidth);
        }
        else
        {
            pen.Strokes([StepperGeometry.Outline(StepsPerTurn, BuiltAngle)], Line, Theme.MotorSignalWidth);
        }
    }

    // On a Touch sensor's joint the mark is a ring outside the cushion and the pawl.
    private void DrawHaloLayer()
    {
        if (!ShowsHalo)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(_haloLayer);
        if (Cushioned)
        {
            pen.Ring(Vector2.Zero, StepperGeometry.CushionedHaloRadius, Theme.SelectionGlow, Theme.SelectionRingWidth);
            return;
        }

        pen.Strokes([StepperGeometry.Halo(keyhole: HasTarget)], Theme.SelectionGlow, Theme.SelectionRingWidth);
    }

    private void DrawPawlLayer()
    {
        if (!HasTarget || (Simplified && Missing))
        {
            return;
        }

        using var pen = UiPixelPen.Begin(_pawlLayer);
        if (!Simplified && Look.Lifted && MathF.Abs(Look.Sweep) > _leastWedgeSweep)
        {
            pen.Polygon(StepperGeometry.Wedge(Look.Sweep), Theme.StepperWedge);
        }

        var pawl = StepperGeometry.Pawl(StepsPerTurn, seated: !Look.Lifted);
        if (!Simplified)
        {
            // A dark band round the pawl keeps it apart from the notch walls it sits between.
            pen.Strokes([pawl], Theme.ArenaBackground, Theme.MotorSignalWidth + (2 * StepperGeometry.PawlGap));
            pen.Polygon(pawl[..^1], Look.Lifted || Missing ? Theme.ServoPanelFill : Theme.MotorAccent);
        }

        pen.Strokes([pawl], Theme.MotorAccent, Theme.MotorSignalWidth);
    }
}

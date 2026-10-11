using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A joint (#767): a Touch sensor's cushion (<see cref="Cushion"/>, #1045) under everything else it
/// draws, and its halo while that sensor is selected; its ring (<see cref="JointDrawing"/>) at its
/// origin and, while selected, its halo; a Wheel's hub (<see cref="ShowsHub"/>) where a Wheel stands in for its ring, so the hub sits over
/// the links that reach it (#1086); and over them its <see cref="Placing"/> mark, which it draws even
/// where a Servo or Wheel stands in for its ring (<see cref="ShowsRing"/>).
/// </summary>
public partial class JointPart : PartVisual
{
    private const int _refusedRingDashes = 8;
    private const int _refusedDashSegments = 8;
    private const int _cushionSegments = 64;

    private float _radius;
    private bool _simplified;
    private bool _loose;
    private bool _showsRing = true;
    private bool _showsHub;
    private PlacingMark _placing;
    private float _placingRadius;
    private bool _cushion;
    private float _touch;
    private bool _touchSelected;
    private bool _wheelSelected;

    public JointPart()
        : base(DrawSlot.Over)
    {
    }

    public float Radius
    {
        get => _radius;
        set => Change(ref _radius, value);
    }

    /// <summary>The outer ring only, without inner ring or tint.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>Joined to nothing, so it cannot train: tinted in <c>danger</c> (<see cref="JointLook.Loose"/>).</summary>
    public bool Loose
    {
        get => _loose;
        set => Change(ref _loose, value);
    }

    /// <summary>Whether it draws its ring; false where a Servo or Wheel, placed or previewed, stands in for it.</summary>
    public bool ShowsRing
    {
        get => _showsRing;
        set => Change(ref _showsRing, value);
    }

    /// <summary>
    /// Whether it draws a Wheel's hub (<see cref="WheelPart.DrawHub"/>): where a Wheel, placed or
    /// previewed, and no Servo sits on it. A shadow (<see cref="Simplified"/>) draws only its joint's outer ring at the hub.
    /// </summary>
    public bool ShowsHub
    {
        get => _showsHub;
        set => Change(ref _showsHub, value);
    }

    /// <summary>
    /// Whether it draws its Touch sensor's cushion, from <see cref="Radius"/>, the joint's drawn edge,
    /// out by <see cref="TouchSensorDef.Cushion"/>. A shadow (<see cref="Simplified"/>) draws none.
    /// </summary>
    public bool Cushion
    {
        get => _cushion;
        set => Change(ref _cushion, value);
    }

    /// <summary>
    /// Its Touch sensor's reading, 0–1 (<c>NodeRunner.Mechanics.Touch.Reading</c>): the cushion's colour,
    /// from <c>accent</c> at <c>alpha_soft</c> at 0 to solid <c>accent</c> at 1. Build shows 0.
    /// </summary>
    public float Touch
    {
        get => _touch;
        set => Change(ref _touch, value);
    }

    /// <summary>
    /// Whether its Touch sensor is selected: the joint ring's selection mark
    /// (<see cref="SelectionDrawing.DrawJoint"/>), <see cref="SelectionMarks.Gap"/> outside the
    /// cushion, as every part's mark sits outside its own edge.
    /// </summary>
    public bool TouchSelected
    {
        get => _touchSelected;
        set => Change(ref _touchSelected, value);
    }

    /// <summary>
    /// Whether its Wheel is selected (#1045): the joint ring's selection mark outside the tyre, at
    /// <see cref="Radius"/>'s halo. The joint draws it, as it draws the hub the Wheel is chosen
    /// at, so it lies over the links and the cushion, as a joint's own mark does.
    /// </summary>
    public bool WheelSelected
    {
        get => _wheelSelected;
        set => Change(ref _wheelSelected, value);
    }

    /// <summary>Whether it would take or refuse the joint part being placed: a ring at <see cref="PlacingRadius"/>'s halo.</summary>
    public PlacingMark Placing
    {
        get => _placing;
        set => Change(ref _placing, value);
    }

    /// <summary>
    /// The radius the <see cref="Placing"/> ring goes round: the placed part's there, which is where a
    /// finger aims (#1055, <c>PlacingTargets.JointRingOf</c>), or the joint's own while it refuses.
    /// </summary>
    public float PlacingRadius
    {
        get => _placingRadius;
        set => Change(ref _placingRadius, value);
    }

    public override void _Draw()
    {
        var cushion = Cushion && !Simplified;
        if (cushion)
        {
            using var cushionPen = UiPixelPen.Begin(this);
            var color = Theme.TouchCushion.Lerp(Theme.TouchCushionTouching, Touch);
            cushionPen.Ring(Vector2.Zero, Radius + ((float)TouchSensorDef.Cushion / 2), color, (float)TouchSensorDef.Cushion, _cushionSegments);
        }

        if (ShowsRing)
        {
            var look = Selected ? JointLook.Selected : Loose ? JointLook.Loose : JointLook.Plain;
            JointDrawing.DrawPlain(this, Theme, Transform2D.Identity, Vector2.Zero, Radius, look, Simplified);
            if (Selected)
            {
                SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, (float)SelectionMarks.JointHalo(Radius));
            }
        }

        if (WheelSelected && !Simplified)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, (float)SelectionMarks.JointHalo(Radius));
        }

        if (cushion && TouchSelected)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, (float)SelectionMarks.JointHalo(Radius + (float)TouchSensorDef.Cushion));
        }

        if (ShowsHub && !ShowsRing && Simplified)
        {
            // A shadow's Wheel joint shows its outer ring at the hub, as every shadow joint does.
            JointDrawing.DrawPlain(this, Theme, Transform2D.Identity, Vector2.Zero, JointDrawing.HubRadius, Loose ? JointLook.Loose : JointLook.Plain, simplified: true);
        }

        using var pen = UiPixelPen.Begin(this);
        if (ShowsHub && !ShowsRing && !Simplified)
        {
            WheelPart.DrawHub(pen, Theme, Loose);
        }

        if (Placing == PlacingMark.None)
        {
            return;
        }

        var ring = (float)SelectionMarks.JointHalo(PlacingRadius);
        if (Placing == PlacingMark.Takes)
        {
            pen.Ring(Vector2.Zero, ring, Theme.SelectionGlow, Theme.SelectionRingWidth);
        }
        else
        {
            pen.DashedRing(Vector2.Zero, ring, _refusedRingDashes, _refusedDashSegments, Theme.Danger, Theme.SelectionRingWidth);
        }
    }
}

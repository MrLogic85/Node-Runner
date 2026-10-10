using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A joint (#767): its ring (<see cref="JointDrawing"/>) at its origin and, while selected, its halo;
/// and over them its <see cref="Placing"/> mark, which it draws even where a Servo or Wheel stands
/// in for its ring (<see cref="ShowsRing"/>).
/// </summary>
public partial class JointPart : PartVisual
{
    private const int _refusedRingDashes = 8;
    private const int _refusedDashSegments = 8;

    private float _radius;
    private bool _simplified;
    private bool _loose;
    private bool _showsRing = true;
    private PlacingMark _placing;
    private float _placingRadius;

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

    /// <summary>Whether it would take or refuse the joint part being placed: a ring at <see cref="PlacingRadius"/>'s halo.</summary>
    public PlacingMark Placing
    {
        get => _placing;
        set => Change(ref _placing, value);
    }

    /// <summary>
    /// The radius the <see cref="Placing"/> ring goes round: the placed part's, which is where a
    /// finger aims (#1055), or the joint's own while it refuses.
    /// </summary>
    public float PlacingRadius
    {
        get => _placingRadius;
        set => Change(ref _placingRadius, value);
    }

    public override void _Draw()
    {
        if (ShowsRing)
        {
            var look = Selected ? JointLook.Selected : Loose ? JointLook.Loose : JointLook.Plain;
            JointDrawing.DrawPlain(this, Theme, Transform2D.Identity, Vector2.Zero, Radius, look, Simplified);
            if (Selected)
            {
                SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, (float)SelectionMarks.JointHalo(Radius));
            }
        }

        if (Placing == PlacingMark.None)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(this);
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

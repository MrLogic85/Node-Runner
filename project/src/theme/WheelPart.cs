using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A Wheel (#129) on its joint, unfilled in every theme: an opaque face, so links stop at the
/// tyre; a <c>detail</c> tyre whose outer edge is the collision radius, with a fine line
/// <see cref="TyreWidth"/> inside it; <see cref="CueCount"/> tubes in the tyre band that turn with
/// the part, so a rolling wheel reads as rolling; and the passive hub. Training hangs it on the
/// wheel's body, so its own rotation is the wheel's; Build shows it at rotation 0.
/// </summary>
public partial class WheelPart : PartVisual
{
    /// <summary>How many rotation-cue tubes the tyre carries, at every radius.</summary>
    public const int CueCount = 3;

    /// <summary>The angle each rotation-cue tube spans.</summary>
    public const float CueSpan = 26 * Mathf.Pi / 180;

    /// <summary>How far inside the edge the tyre's inner line runs.</summary>
    public const float TyreWidth = 6;

    /// <summary>How far inside the edge the cue tubes run: midway between the inner line and the tyre ring.</summary>
    public const float CueInset = 3.75f;

    /// <summary>The passive hub's ring: the plain joint's size, as a motor would sit there.</summary>
    public const float HubRadius = (float)NodeDef.PlainJointRadius;

    /// <summary>The axle dot in the hub.</summary>
    public const float AxleRadius = 2.6f;

    private const int _ringSegments = 64;
    private const int _cueSegments = 8;

    private float _radius = (float)WheelDef.DefaultRadius;
    private bool _simplified;
    private bool _loose;

    public WheelPart()
        : base(DrawSlot.Under)
    {
    }

    /// <summary>The Wheel's radius, the outer edge of its tyre.</summary>
    public float Radius
    {
        get => _radius;
        set => Change(ref _radius, value);
    }

    /// <summary>A Training shadow's look (#664): the tyre ring at the edge only, no cue or hub.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>Its joint is joined to nothing, so it cannot train: all of it in <c>danger</c>.</summary>
    public bool Loose
    {
        get => _loose;
        set => Change(ref _loose, value);
    }

    /// <summary>The centre angle of each cue tube at rotation 0, a third of a turn apart.</summary>
    public static float[] CueAngles() =>
        [.. Enumerable.Range(0, CueCount).Select(index => index * Mathf.Tau / CueCount)];

    /// <summary>Where the cue tubes run in a Wheel of <paramref name="radius"/>.</summary>
    public static float CueRadius(float radius) => radius - CueInset;

    /// <summary>The cue tubes of a Wheel of <paramref name="radius"/> at rotation 0, each a short arc of points.</summary>
    public static Vector2[][] CueTubes(float radius) =>
        [.. CueAngles().Select(centre => ArcPoints(CueRadius(radius), centre - (CueSpan / 2), centre + (CueSpan / 2)))];

    /// <summary>The centre line of the tyre ring, whose outer edge is <paramref name="radius"/>.</summary>
    public static float TyreRingRadius(float radius, float ringWidth) => radius - (ringWidth / 2);

    /// <summary>The selected Wheel's <c>halo</c> ring, <see cref="SelectionMarks.Gap"/> outside its edge, as every part's.</summary>
    public static float HaloRadius(float radius) => (float)SelectionMarks.JointHalo(radius);

    public override void _Draw()
    {
        using var pen = UiPixelPen.Begin(this);
        var line = Loose ? Theme.Danger : Theme.WheelLine;
        if (Simplified)
        {
            pen.Ring(Vector2.Zero, TyreRingRadius(Radius, Theme.JointRingWidth), line, Theme.JointRingWidth, _ringSegments);
            return;
        }

        pen.Disc(Vector2.Zero, Radius, Theme.ArenaBackground);
        pen.Ring(Vector2.Zero, TyreRingRadius(Radius, Theme.JointRingWidth), line, Theme.JointRingWidth, _ringSegments);
        pen.Ring(Vector2.Zero, Radius - TyreWidth, line, Theme.JointInnerRingWidth, _ringSegments);
        pen.Strokes(CueTubes(Radius), line, Theme.MotorSignalWidth);
        pen.Ring(Vector2.Zero, TyreRingRadius(HubRadius, Theme.JointRingWidth), line, Theme.JointRingWidth, _ringSegments);
        pen.Disc(Vector2.Zero, AxleRadius, line);
        if (Selected)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, HaloRadius(Radius));
        }
    }

    private static Vector2[] ArcPoints(float radius, float from, float to) =>
        [.. Enumerable.Range(0, _cueSegments + 1).Select(index => Vector2.FromAngle(Mathf.Lerp(from, to, index / (float)_cueSegments)) * radius)];
}

using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A Wheel on its joint, unfilled in every theme: an opaque <c>detail</c> tyre whose outer
/// edge is the collision radius, with a fine line <see cref="TyreWidth"/> inside it, and see-through
/// within that, so it never hides what lies under it; and <see cref="CueCount"/> tubes in
/// the tyre band that turn with the part, so a rolling wheel reads as rolling. Its links are drawn over it to the hub,
/// which its joint draws over them (<see cref="DrawHub"/>). Its joint draws its selection
/// mark too, as one of its bands over the links and under the hub (<see cref="JointPart.WheelSelected"/>). Training hangs it on the
/// wheel's body, so its own rotation is the wheel's; Build shows it at rotation 0.
/// </summary>
public partial class WheelPart : PartVisual
{
    /// <summary>How many rotation-cue tubes the tyre carries, at every radius.</summary>
    public const int CueCount = 3;

    /// <summary>The angle each rotation-cue tube spans.</summary>
    public const float CueSpan = 26 * Mathf.Pi / 180;

    /// <summary>How far inside the edge the tyre's inner line runs, the solid band; inside it the Wheel is see-through.</summary>
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

    /// <summary>A Training shadow's look: the tyre ring at the edge only, no cue or hub.</summary>
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

    /// <summary>
    /// The passive hub, drawn by the Wheel's joint over the links that reach it: an opaque
    /// disc, its ring and the axle dot, all in <c>danger</c> while <paramref name="loose"/>.
    /// </summary>
    public static void DrawHub(UiPixelPen pen, VisualTheme theme, bool loose)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var line = loose ? theme.Danger : theme.WheelLine;
        pen.Disc(Vector2.Zero, HubRadius, theme.ArenaBackground);
        pen.Ring(Vector2.Zero, TyreRingRadius(HubRadius, theme.JointRingWidth), line, theme.JointRingWidth, _ringSegments);
        pen.Disc(Vector2.Zero, AxleRadius, line);
    }

    public override void _Draw()
    {
        using var pen = UiPixelPen.Begin(this);
        var line = Loose ? Theme.Danger : Theme.WheelLine;
        if (Simplified)
        {
            pen.Ring(Vector2.Zero, TyreRingRadius(Radius, Theme.JointRingWidth), line, Theme.JointRingWidth, _ringSegments);
            return;
        }

        pen.Ring(Vector2.Zero, Radius - (TyreWidth / 2), Theme.ArenaBackground, TyreWidth, _ringSegments);
        pen.Ring(Vector2.Zero, TyreRingRadius(Radius, Theme.JointRingWidth), line, Theme.JointRingWidth, _ringSegments);
        pen.Ring(Vector2.Zero, Radius - TyreWidth, line, Theme.JointInnerRingWidth, _ringSegments);
        pen.Strokes(CueTubes(Radius), line, Theme.MotorSignalWidth);
    }

    private static Vector2[] ArcPoints(float radius, float from, float to) =>
        [.. Enumerable.Range(0, _cueSegments + 1).Select(index => Vector2.FromAngle(Mathf.Lerp(from, to, index / (float)_cueSegments)) * radius)];
}

using System.Runtime.CompilerServices;
using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// The world's look, read live from the Theme: a handle on a Control, normally the
/// <see cref="UiWorldView"/> that shows the world, so every colour, opacity, flag and font is the
/// one that Control inherits, and an overlay such as Paper falls back to Neon's fonts as a
/// Control's would. Hold the handle, never a value read from it. The world gets no theme change of
/// its own, so on one the handle redraws every CanvasItem under its anchor, renders a still world
/// again and calls what <see cref="Bind"/> keeps outside <c>_Draw</c>. Dimensions
/// are constants every palette shares.
/// </summary>
public sealed class VisualTheme
{
    private static readonly ConditionalWeakTable<Control, VisualTheme> _handles = [];

    // The reference's rigid hatch: muted hairlines at half opacity, 7 apart.
    private const float _rigidHatchOpacity = 0.5f;
    private const float _rigidHatchSpacing = 7f;
    private const float _servoFixedBandOpacity = 0.30f;
    private const float _servoFixedHatchOpacity = 0.85f;
    private const float _servoFixedEdgeOpacity = 0.90f;
    private const float _servoTargetBandOpacity = 0.24f;
    private const float _servoTargetEdgeOpacity = 0.90f;

    // Null for the project theme.
    private readonly Control? _anchor;

    // What has been read since the last theme change, as drawing reads the same tokens over and over.
    private readonly Color?[] _colors = new Color?[Enum.GetValues<UiTokens.Color>().Length];
    private readonly float?[] _alphas = new float?[Enum.GetValues<UiTokens.Alpha>().Length];
    private readonly bool?[] _flags = new bool?[Enum.GetValues<UiTokens.Flag>().Length];
    private readonly (Font Font, int Size)?[] _fonts = new (Font, int)?[Enum.GetValues<UiTokens.Typography>().Length];
    private bool _refreshQueued;

    private VisualTheme(Control? anchor)
    {
        _anchor = anchor;
        if (anchor is not null)
        {
            anchor.ThemeChanged += OnThemeChanged;
        }
    }

    /// <summary>
    /// The project theme, Neon, which never changes: for a world outside any view, before it has
    /// one, and in the editor.
    /// </summary>
    public static VisualTheme Project { get; } = new(null);

    // Raised once the handle has redrawn its world and reads the new values.
    private event Action? Changed;

    /// <summary>
    /// The handle for <paramref name="node"/>'s world: anchored on the Control whose SubViewport
    /// holds it, normally a <see cref="UiWorldView"/>, or on <paramref name="node"/> itself when it is
    /// such a Control. One handle per anchor. <see cref="Project"/> in the editor and outside a view.
    /// </summary>
    public static VisualTheme For(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Engine.IsEditorHint())
        {
            return Project;
        }

        if (!node.IsInsideTree())
        {
            throw new InvalidOperationException($"{node.Name} must be in the tree to find its world's theme.");
        }

        var anchor = node.GetViewport() is SubViewport viewport && viewport.GetParent() is Control holder
            ? holder
            : node as Control;
        return anchor is null ? Project : _handles.GetValue(anchor, static anchor => new VisualTheme(anchor));
    }

    /// <summary>
    /// Calls <paramref name="apply"/> now and after every theme change while <paramref name="owner"/>
    /// is in the tree: for a value kept outside <c>_Draw</c>, such as a <c>Polygon2D</c>'s colour.
    /// </summary>
    public void Bind(Node owner, Action apply)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(apply);
        apply();
        if (owner.IsInsideTree())
        {
            Changed += apply;
        }

        owner.TreeEntered += () => Changed += apply;
        owner.TreeExiting += () => Changed -= apply;
    }

    /// <summary>
    /// The theme a Control in the world takes, since the world's viewport cuts Godot's
    /// inheritance: the anchor's (<see cref="UiThemes.NearestOf"/>). Null means the project theme.
    /// </summary>
    public Godot.Theme? ControlTheme => _anchor is { } anchor ? UiThemes.NearestOf(anchor) : null;

    public Color ArenaBackground => ColorOf(UiTokens.Color.Background);

    public Color ArenaGrid => ColorOf(UiTokens.Color.Line);

    public Color GroundFill => ColorOf(UiTokens.Color.Panel);

    public Color GroundEdge => ColorOf(UiTokens.Color.Accent);

    public bool EffectsEnabled => FlagOf(UiTokens.Flag.EffectsEnabled);

    /// <summary>How opaque the shadows other than the followed one are drawn in Training, as one picture.</summary>
    public float ShadowAlpha => AlphaOf(UiTokens.Alpha.Shadow);

    /// <summary>How far the followed creature's knock-out outline reaches past its beams and joints.</summary>
    public float KnockoutWidth => UiSize.Stroke.Signal;

    public Color SelectionGlow => ColorOf(UiTokens.Color.Halo);

    /// <summary>The inside of the Select tool's box while it is dragged: <c>halo</c> at <c>alpha_soft</c>.</summary>
    public Color SelectionFill => ColorOf(UiTokens.Color.Halo) with { A = AlphaOf(UiTokens.Alpha.Soft) };

    /// <summary>The inside of the selection frame's corner squares: <c>panel</c>.</summary>
    public Color SelectionCornerFill => ColorOf(UiTokens.Color.Panel);

    /// <summary>The stroke of a selected joint's ring.</summary>
    // The reference's 2-wide ring, not scaled up with our wider beams (owner decision).
    public float SelectionRingWidth => UiSize.Stroke.Signal;

    /// <summary>How far each of a selected beam's two lines runs from its centre line: half its width plus <see cref="SelectionMarks.Gap"/>.</summary>
    public float SelectedBeamOffset => (UiSize.Widget.CreatureBeamWidth / 2f) + (float)SelectionMarks.Gap;

    /// <summary>The width of each of a selected beam's two lines.</summary>
    public float SelectedBeamLineWidth => UiSize.Stroke.Signal;

    public Color Beam => ColorOf(UiTokens.Color.LineStrong);

    /// <summary>The soft tint inside a plain joint; <see cref="SelectionFill"/> when selected, <see cref="DangerFill"/> when loose.</summary>
    public Color JointFill => ColorOf(UiTokens.Color.LineStrong) with { A = AlphaOf(UiTokens.Alpha.Soft) };

    /// <summary>The soft tint inside a loose joint.</summary>
    public Color DangerFill => ColorOf(UiTokens.Color.Danger) with { A = AlphaOf(UiTokens.Alpha.Soft) };

    /// <summary>The width of a plain joint's outer ring, whose outer edge is the joint's radius.</summary>
    public float JointRingWidth => UiSize.Stroke.Signal;

    /// <summary>The width of a plain joint's fine inner ring.</summary>
    public float JointInnerRingWidth => UiSize.Stroke.Hair;

    /// <summary>The front of a Spring's coil, over its damper's <see cref="SensorFill"/> body (see <c>SpringDrawing</c>).</summary>
    public Color SpringCoil => ColorOf(UiTokens.Color.Detail);

    public Color MotorAccent => ColorOf(UiTokens.Color.Accent);

    /// <summary>The panel fill inside a Servo's housing, range band and horn.</summary>
    public Color ServoPanelFill => ColorOf(UiTokens.Color.Panel);

    /// <summary>The detail colour for the passive Fixed side of a Servo.</summary>
    public Color ServoFixed => ColorOf(UiTokens.Color.Detail);

    /// <summary>The translucent band under a selected Servo's Fixed link.</summary>
    public Color ServoFixedBand => ColorOf(UiTokens.Color.Detail) with { A = _servoFixedBandOpacity };

    /// <summary>The hatch inside a selected Servo's Fixed-link band.</summary>
    public Color ServoFixedHatch => ColorOf(UiTokens.Color.Detail) with { A = _servoFixedHatchOpacity };

    /// <summary>The hairline edges of a selected Servo's Fixed-link band.</summary>
    public Color ServoFixedBandEdge => ColorOf(UiTokens.Color.Detail) with { A = _servoFixedEdgeOpacity };

    /// <summary>A Wheel's tyre, rotation cue and passive hub: <c>detail</c>, as it is passive.</summary>
    public Color WheelLine => ColorOf(UiTokens.Color.Detail);

    /// <summary>The translucent band under a selected Servo's Target link.</summary>
    public Color ServoTargetBand => ColorOf(UiTokens.Color.Accent) with { A = _servoTargetBandOpacity };

    /// <summary>The hairline edges of a selected Servo's Target-link band.</summary>
    public Color ServoTargetBandEdge => ColorOf(UiTokens.Color.Accent) with { A = _servoTargetEdgeOpacity };

    public Color Danger => ColorOf(UiTokens.Color.Danger);

    /// <summary>The inside of a sensor's picture on its beam, so the beam does not show through.</summary>
    public Color SensorFill => ColorOf(UiTokens.Color.Panel);

    /// <summary>The lines of a sensor's picture on its beam.</summary>
    public Color SensorLine => ColorOf(UiTokens.Color.Accent);

    /// <summary>A Touch sensor's cushion at rest: <c>accent</c> at <c>alpha_soft</c>, a soft glow round its joint.</summary>
    public Color TouchCushion => ColorOf(UiTokens.Color.Accent) with { A = AlphaOf(UiTokens.Alpha.Soft) };

    /// <summary>A Touch sensor's cushion at a reading of 1, its joint on the ground: solid <c>accent</c>.</summary>
    public Color TouchCushionTouching => ColorOf(UiTokens.Color.Accent);

    /// <summary>A Rubber's skin round its joint: <c>detail</c>, as it is passive.</summary>
    public Color RubberSkin => ColorOf(UiTokens.Color.Detail);

    /// <summary>A Brake's caliper, disc and ring: <c>detail</c>, as it is passive.</summary>
    public Color BrakeLine => ColorOf(UiTokens.Color.Detail);

    /// <summary>A stepping Stepper's wedge from its pawl to the notch it steps to: <c>accent</c> at <c>alpha_soft</c>.</summary>
    public Color StepperWedge => ColorOf(UiTokens.Color.Accent) with { A = AlphaOf(UiTokens.Alpha.Soft) };

    /// <summary>The corner marks that show where the Build area ends.</summary>
    public Color AreaCorner => ColorOf(UiTokens.Color.Accent);

    public float AreaCornerWidth => UiSize.Stroke.Signal;

    public float BeamWidth => UiSize.Widget.CreatureBeamWidth;

    public float MotorSignalWidth => UiSize.Stroke.Signal;

    public float GroundEdgeWidth => UiSize.Stroke.Signal;

    /// <summary>The ticks of the Training arena's distance and height scales along the view's edges.</summary>
    public Color RulerTick => ColorOf(UiTokens.Color.LineStrong);

    public float RulerTickWidth => UiSize.Stroke.Hair;

    /// <summary>The Training arena's distance and height labels, in the readout style.</summary>
    public Color RulerLabel => ColorOf(UiTokens.Color.Muted);

    public Font RulerFont => FontOf(UiTokens.Typography.ReadoutMedium).Font;

    public int RulerFontSize => FontOf(UiTokens.Typography.ReadoutMedium).Size;

    /// <summary>The Training best marker's dashed line, flag outline and text.</summary>
    public Color MarkerInk => ColorOf(UiTokens.Color.Ink);

    /// <summary>The Training best marker's flag.</summary>
    public Color MarkerFill => ColorOf(UiTokens.Color.Panel);

    public float MarkerLineWidth => UiSize.Stroke.Marker;

    public Font MarkerFont => FontOf(UiTokens.Typography.NoteStrong).Font;

    public int MarkerFontSize => FontOf(UiTokens.Typography.NoteStrong).Size;

    /// <summary>The hatch lines inside a rigid triangle in Build: one pixel wide at any zoom.</summary>
    public Color RigidHatch => ColorOf(UiTokens.Color.Muted) with { A = _rigidHatchOpacity };

    /// <summary>How far apart a rigid triangle's hatch lines are, in creature units.</summary>
    public float RigidHatchSpacing => _rigidHatchSpacing;

    /// <summary>
    /// A rigid triangle's fill where its hatch is too dense to read (<see cref="TriangleHatch.IsTooDense"/>):
    /// the hatch's own coverage at that point, one-pixel lines <see cref="TriangleHatch.MinPixelSpacing"/>
    /// apart, so zooming across it does not jump.
    /// </summary>
    public Color RigidFill => ColorOf(UiTokens.Color.Muted) with { A = _rigidHatchOpacity / TriangleHatch.MinPixelSpacing };

    private Color ColorOf(UiTokens.Color token) =>
        _colors[(int)token] ??= _anchor is { } anchor ? UiThemeLookup.Color(anchor, token) : UiThemes.Color(UiThemes.Neon, token);

    private float AlphaOf(UiTokens.Alpha token) =>
        _alphas[(int)token] ??= _anchor is { } anchor ? UiThemeLookup.Alpha(anchor, token) : UiThemes.Alpha(UiThemes.Neon, token);

    private bool FlagOf(UiTokens.Flag token) =>
        _flags[(int)token] ??= _anchor is { } anchor ? UiThemeLookup.Flag(anchor, token) : UiThemes.Flag(UiThemes.Neon, token);

    private (Font Font, int Size) FontOf(UiTokens.Typography token) =>
        _fonts[(int)token] ??= _anchor is { } anchor
            ? (UiThemeLookup.Font(anchor, token), UiThemeLookup.FontSize(anchor, token))
            : (UiThemes.Neon.GetFont("font", UiTokens.Variation(token)), UiThemes.Neon.GetFontSize("font_size", UiTokens.Variation(token)));

    // Godot emits ThemeChanged before the anchor drops its own cached theme items, so a value read
    // then would still be the old palette's. Once per change, however often the signal comes.
    private void OnThemeChanged()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Callable.From(Refresh).CallDeferred();
    }

    private void Refresh()
    {
        _refreshQueued = false;
        if (!GodotObject.IsInstanceValid(_anchor))
        {
            return;
        }

        Array.Clear(_colors);
        Array.Clear(_alphas);
        Array.Clear(_flags);
        Array.Clear(_fonts);
        Redraw(_anchor!);
        (_anchor as UiWorldView)?.RequestRender();
        Changed?.Invoke();
    }

    // Internal children too: a part or the ruler may draw on one of its own.
    private static void Redraw(Node node)
    {
        foreach (var child in node.GetChildren(includeInternal: true))
        {
            (child as CanvasItem)?.QueueRedraw();
            Redraw(child);
        }
    }
}

using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

public sealed class VisualTheme
{
    private VisualTheme()
    {
    }

    public static VisualTheme Neon { get; } = FromTheme(UiThemes.Neon);

    public static VisualTheme Paper { get; } = FromTheme(UiThemes.Paper);

    // The reference's rigid hatch: muted hairlines at half opacity, 7 apart (#612).
    private const float _rigidHatchOpacity = 0.5f;
    private const float _rigidHatchSpacing = 7f;
    private const float _servoFixedBandOpacity = 0.30f;
    private const float _servoFixedHatchOpacity = 0.85f;
    private const float _servoFixedEdgeOpacity = 0.90f;
    private const float _servoTargetBandOpacity = 0.24f;
    private const float _servoTargetEdgeOpacity = 0.90f;

    public static VisualTheme FromTheme(Godot.Theme theme) => new()
    {
        RigidHatch = UiThemes.Color(theme, UiTokens.Color.Muted) with { A = _rigidHatchOpacity },
        RigidHatchSpacing = _rigidHatchSpacing,
        RigidFill = UiThemes.Color(theme, UiTokens.Color.Muted) with { A = _rigidHatchOpacity / TriangleHatch.MinPixelSpacing },
        ArenaBackground = UiThemes.Color(theme, UiTokens.Color.Background),
        ArenaGrid = UiThemes.Color(theme, UiTokens.Color.Line),
        GroundFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        GroundEdge = UiThemes.Color(theme, UiTokens.Color.Accent),
        EffectsEnabled = UiThemes.Flag(theme, UiTokens.Flag.EffectsEnabled),
        ShadowAlpha = UiThemes.Alpha(theme, UiTokens.Alpha.Shadow),
        KnockoutWidth = UiSize.Stroke.Signal,
        SelectionGlow = UiThemes.Color(theme, UiTokens.Color.Halo),
        SelectionFill = UiThemes.Color(theme, UiTokens.Color.Halo) with { A = UiThemes.Alpha(theme, UiTokens.Alpha.Soft) },
        SelectionCornerFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        Beam = UiThemes.Color(theme, UiTokens.Color.LineStrong),
        JointFill = UiThemes.Color(theme, UiTokens.Color.LineStrong) with { A = UiThemes.Alpha(theme, UiTokens.Alpha.Soft) },
        DangerFill = UiThemes.Color(theme, UiTokens.Color.Danger) with { A = UiThemes.Alpha(theme, UiTokens.Alpha.Soft) },
        JointRingWidth = UiSize.Stroke.Signal,
        JointInnerRingWidth = UiSize.Stroke.Hair,
        SpringCoil = UiThemes.Color(theme, UiTokens.Color.Detail),
        MotorAccent = UiThemes.Color(theme, UiTokens.Color.Accent),
        ServoPanelFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        ServoFixed = UiThemes.Color(theme, UiTokens.Color.Detail),
        ServoFixedBand = UiThemes.Color(theme, UiTokens.Color.Detail) with { A = _servoFixedBandOpacity },
        ServoFixedHatch = UiThemes.Color(theme, UiTokens.Color.Detail) with { A = _servoFixedHatchOpacity },
        ServoFixedBandEdge = UiThemes.Color(theme, UiTokens.Color.Detail) with { A = _servoFixedEdgeOpacity },
        WheelLine = UiThemes.Color(theme, UiTokens.Color.Detail),
        ServoTargetBand = UiThemes.Color(theme, UiTokens.Color.Accent) with { A = _servoTargetBandOpacity },
        ServoTargetBandEdge = UiThemes.Color(theme, UiTokens.Color.Accent) with { A = _servoTargetEdgeOpacity },
        Danger = UiThemes.Color(theme, UiTokens.Color.Danger),
        SensorFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        SensorLine = UiThemes.Color(theme, UiTokens.Color.Accent),
        AreaCorner = UiThemes.Color(theme, UiTokens.Color.Accent),
        BeamWidth = UiSize.Widget.CreatureBeamWidth,
        // The reference's 2-wide ring, not scaled up with our wider beams (owner, 2026-10-03).
        SelectionRingWidth = UiSize.Stroke.Signal,
        SelectedBeamOffset = (UiSize.Widget.CreatureBeamWidth / 2f) + (float)SelectionMarks.Gap,
        SelectedBeamLineWidth = UiSize.Stroke.Signal,
        MotorSignalWidth = UiSize.Stroke.Signal,
        GroundEdgeWidth = UiSize.Stroke.Signal,
        RulerTick = UiThemes.Color(theme, UiTokens.Color.LineStrong),
        RulerTickWidth = UiSize.Stroke.Hair,
        RulerLabel = UiThemes.Color(theme, UiTokens.Color.Muted),
        RulerFont = theme.GetFont("font", UiTokens.Variation(UiTokens.Typography.ReadoutMedium)),
        RulerFontSize = theme.GetFontSize("font_size", UiTokens.Variation(UiTokens.Typography.ReadoutMedium)),
        AreaCornerWidth = UiSize.Stroke.Signal,
        MarkerInk = UiThemes.Color(theme, UiTokens.Color.Ink),
        MarkerFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        MarkerLineWidth = UiSize.Stroke.Marker,
        MarkerFont = theme.GetFont("font", UiTokens.Variation(UiTokens.Typography.NoteStrong)),
        MarkerFontSize = theme.GetFontSize("font_size", UiTokens.Variation(UiTokens.Typography.NoteStrong)),
    };

    public Color ArenaBackground { get; private init; }

    public Color ArenaGrid { get; private init; }

    public Color GroundFill { get; private init; }

    public Color GroundEdge { get; private init; }

    public bool EffectsEnabled { get; private init; }

    /// <summary>How opaque the shadows other than the followed one are drawn in Training, as one picture (#385, #818).</summary>
    public float ShadowAlpha { get; private init; }

    /// <summary>How far the followed creature's knock-out outline reaches past its beams and joints (#818).</summary>
    public float KnockoutWidth { get; private init; }

    public Color SelectionGlow { get; private init; }

    /// <summary>The inside of the Select tool's box while it is dragged: <c>halo</c> at <c>alpha_soft</c>.</summary>
    public Color SelectionFill { get; private init; }

    /// <summary>The inside of the selection frame's corner squares: <c>panel</c>.</summary>
    public Color SelectionCornerFill { get; private init; }

    /// <summary>The stroke of a selected joint's ring.</summary>
    public float SelectionRingWidth { get; private init; }

    /// <summary>How far each of a selected beam's two lines runs from its centre line: half its width plus <see cref="SelectionMarks.Gap"/>.</summary>
    public float SelectedBeamOffset { get; private init; }

    /// <summary>The width of each of a selected beam's two lines.</summary>
    public float SelectedBeamLineWidth { get; private init; }

    public Color Beam { get; private init; }

    /// <summary>The soft tint inside a plain joint (#626); <see cref="SelectionFill"/> when selected, <see cref="DangerFill"/> when loose.</summary>
    public Color JointFill { get; private init; }

    /// <summary>The soft tint inside a loose joint.</summary>
    public Color DangerFill { get; private init; }

    /// <summary>The width of a plain joint's outer ring, whose outer edge is the joint's radius.</summary>
    public float JointRingWidth { get; private init; }

    /// <summary>The width of a plain joint's fine inner ring.</summary>
    public float JointInnerRingWidth { get; private init; }

    /// <summary>The front of a Spring's coil (#807), over its damper's <see cref="SensorFill"/> body (see <c>SpringDrawing</c>).</summary>
    public Color SpringCoil { get; private init; }

    public Color MotorAccent { get; private init; }

    /// <summary>The panel fill inside a Servo's housing, range band and horn.</summary>
    public Color ServoPanelFill { get; private init; }

    /// <summary>The detail colour for the passive Fixed side of a Servo (#832).</summary>
    public Color ServoFixed { get; private init; }

    /// <summary>The translucent band under a selected Servo's Fixed link.</summary>
    public Color ServoFixedBand { get; private init; }

    /// <summary>The hatch inside a selected Servo's Fixed-link band.</summary>
    public Color ServoFixedHatch { get; private init; }

    /// <summary>The hairline edges of a selected Servo's Fixed-link band.</summary>
    public Color ServoFixedBandEdge { get; private init; }

    /// <summary>A Wheel's tyre, rotation cue and passive hub (#129): <c>detail</c>, as it is passive.</summary>
    public Color WheelLine { get; private init; }

    /// <summary>The translucent band under a selected Servo's Target link.</summary>
    public Color ServoTargetBand { get; private init; }

    /// <summary>The hairline edges of a selected Servo's Target-link band.</summary>
    public Color ServoTargetBandEdge { get; private init; }

    public Color Danger { get; private init; }

    /// <summary>The inside of a sensor's picture on its beam (#576), so the beam does not show through.</summary>
    public Color SensorFill { get; private init; }

    /// <summary>The lines of a sensor's picture on its beam.</summary>
    public Color SensorLine { get; private init; }

    /// <summary>The corner marks that show where the Build area ends.</summary>
    public Color AreaCorner { get; private init; }

    public float AreaCornerWidth { get; private init; }

    public float BeamWidth { get; private init; }

    public float MotorSignalWidth { get; private init; }

    public float GroundEdgeWidth { get; private init; }

    /// <summary>The Training ruler's ticks, hanging from the ground line (#668).</summary>
    public Color RulerTick { get; private init; }

    public float RulerTickWidth { get; private init; }

    /// <summary>The Training ruler's distance labels, in the readout style.</summary>
    public Color RulerLabel { get; private init; }

    public Font RulerFont { get; private init; } = null!;

    public int RulerFontSize { get; private init; }

    /// <summary>The Training best marker's dashed line, flag outline and text (#388).</summary>
    public Color MarkerInk { get; private init; }

    /// <summary>The Training best marker's flag.</summary>
    public Color MarkerFill { get; private init; }

    public float MarkerLineWidth { get; private init; }

    public Font MarkerFont { get; private init; } = null!;

    public int MarkerFontSize { get; private init; }

    /// <summary>The hatch lines inside a rigid triangle in Build: one pixel wide at any zoom.</summary>
    public Color RigidHatch { get; private init; }

    /// <summary>How far apart a rigid triangle's hatch lines are, in creature units.</summary>
    public float RigidHatchSpacing { get; private init; }

    /// <summary>
    /// A rigid triangle's fill where its hatch is too dense to read (<see cref="TriangleHatch.IsTooDense"/>):
    /// the hatch's own coverage at that point, one-pixel lines <see cref="TriangleHatch.MinPixelSpacing"/>
    /// apart, so zooming across it does not jump.
    /// </summary>
    public Color RigidFill { get; private init; }
}

using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

public sealed class VisualTheme
{
    private VisualTheme()
    {
    }

    public static VisualTheme Neon { get; } = FromTheme(UiThemes.Neon);

    public static VisualTheme Paper { get; } = FromTheme(UiThemes.Paper);

    // The reference's selected joint ring is 2 wide beside 3-wide beams (#624).
    private const float _selectionRingPerBeam = 2f / 3;

    // A selected beam's lines run half its width outside its edges (#624).
    private const float _selectedBeamOffsetPerBeam = 0.5f + 0.5f;

    // The reference's rigid hatch: muted hairlines at half opacity, 7 apart (#612).
    private const float _rigidHatchOpacity = 0.5f;
    private const float _rigidHatchSpacing = 7f;

    public static VisualTheme FromTheme(Godot.Theme theme) => new()
    {
        RigidHatch = UiThemes.Color(theme, UiTokens.Color.Muted) with { A = _rigidHatchOpacity },
        RigidHatchSpacing = _rigidHatchSpacing,
        ArenaBackground = UiThemes.Color(theme, UiTokens.Color.Background),
        ArenaGrid = UiThemes.Color(theme, UiTokens.Color.Line),
        GroundFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        GroundEdge = UiThemes.Color(theme, UiTokens.Color.Accent),
        NodeFill = UiThemes.Color(theme, UiTokens.Color.PanelRaised),
        EffectsEnabled = UiThemes.Flag(theme, UiTokens.Flag.EffectsEnabled),
        SelectionGlow = UiThemes.Color(theme, UiTokens.Color.Halo),
        Beam = UiThemes.Color(theme, UiTokens.Color.LineStrong),
        MotorAccent = UiThemes.Color(theme, UiTokens.Color.Accent),
        Danger = UiThemes.Color(theme, UiTokens.Color.Danger),
        SensorFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        SensorLine = UiThemes.Color(theme, UiTokens.Color.Accent),
        AreaCorner = UiThemes.Color(theme, UiTokens.Color.Accent),
        BeamWidth = UiSize.Widget.CreatureBeamWidth,
        SelectionRingWidth = UiSize.Widget.CreatureBeamWidth * _selectionRingPerBeam,
        SelectedBeamOffset = UiSize.Widget.CreatureBeamWidth * _selectedBeamOffsetPerBeam,
        SelectedBeamLineWidth = UiSize.Stroke.Signal,
        MotorSignalWidth = UiSize.Stroke.Signal,
        GroundEdgeWidth = UiSize.Stroke.Signal,
        GridSpacing = UiSize.Control.Touch,
        AreaCornerWidth = UiSize.Stroke.Signal,
    };

    public Color ArenaBackground { get; private init; }

    public Color ArenaGrid { get; private init; }

    public Color GroundFill { get; private init; }

    public Color GroundEdge { get; private init; }

    public Color NodeFill { get; private init; }

    public bool EffectsEnabled { get; private init; }

    public Color SelectionGlow { get; private init; }

    /// <summary>The stroke of a selected joint's ring.</summary>
    public float SelectionRingWidth { get; private init; }

    /// <summary>How far each of a selected beam's two lines runs from its centre line: half its width plus half again.</summary>
    public float SelectedBeamOffset { get; private init; }

    /// <summary>The width of each of a selected beam's two lines.</summary>
    public float SelectedBeamLineWidth { get; private init; }

    public Color Beam { get; private init; }

    public Color MotorAccent { get; private init; }

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

    public float GridSpacing { get; private init; }

    /// <summary>The hatch lines inside a rigid triangle in Build: one pixel wide at any zoom.</summary>
    public Color RigidHatch { get; private init; }

    /// <summary>How far apart a rigid triangle's hatch lines are, in creature units.</summary>
    public float RigidHatchSpacing { get; private init; }
}

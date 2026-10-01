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

    public static VisualTheme FromTheme(Godot.Theme theme) => new()
    {
        ArenaBackground = UiThemes.Color(theme, UiTokens.Color.Background),
        ArenaGrid = UiThemes.Color(theme, UiTokens.Color.Line),
        GroundFill = UiThemes.Color(theme, UiTokens.Color.Panel),
        GroundEdge = UiThemes.Color(theme, UiTokens.Color.Accent),
        NodeFill = UiThemes.Color(theme, UiTokens.Color.PanelRaised),
        EffectsEnabled = UiThemes.Flag(theme, UiTokens.Flag.EffectsEnabled),
        SelectionGlow = UiThemes.Color(theme, UiTokens.Color.Halo),
        CoreMarker = UiThemes.Color(theme, UiTokens.Color.Accent),
        Beam = UiThemes.Color(theme, UiTokens.Color.LineStrong),
        MotorAccent = UiThemes.Color(theme, UiTokens.Color.Accent),
        Danger = UiThemes.Color(theme, UiTokens.Color.Danger),
        AreaCorner = UiThemes.Color(theme, UiTokens.Color.Accent),
        BeamWidth = UiSize.Stroke.Beam,
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

    public Color CoreMarker { get; private init; }

    public Color Beam { get; private init; }

    public Color MotorAccent { get; private init; }

    public Color Danger { get; private init; }

    /// <summary>The corner marks that show where the Build area ends.</summary>
    public Color AreaCorner { get; private init; }

    public float AreaCornerWidth { get; private init; }

    public float BeamWidth { get; private init; }

    public float MotorSignalWidth { get; private init; }

    public float GroundEdgeWidth { get; private init; }

    public float GridSpacing { get; private init; }
}

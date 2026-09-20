using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

public sealed class VisualTheme
{
    private VisualTheme()
    {
    }

    public static VisualTheme Neon { get; } = FromTokens(UiTokens.Neon);

    public static VisualTheme Paper { get; } = FromTokens(UiTokens.Paper);

    public static VisualTheme FromTokens(UiTokens tokens) => new()
    {
        ArenaBackground = tokens.Background,
        ArenaGrid = tokens.Line,
        GroundFill = tokens.Panel,
        GroundEdge = tokens.Accent,
        NodeFill = tokens.PanelRaised,
        NodeGlow = tokens.AccentGlow,
        SelectionGlow = tokens.Halo,
        CoreMarker = tokens.Accent,
        Beam = tokens.LineStrong,
        MotorAccent = tokens.Accent,
        Danger = tokens.Danger,
        BeamWidth = tokens.StrokeBeam,
        MotorSignalWidth = tokens.StrokeSignal,
        GroundEdgeWidth = tokens.StrokeSignal,
        GridSpacing = tokens.TouchTarget,
    };

    public Color ArenaBackground { get; private init; }

    public Color ArenaGrid { get; private init; }

    public Color GroundFill { get; private init; }

    public Color GroundEdge { get; private init; }

    public Color NodeFill { get; private init; }

    public Color NodeGlow { get; private init; }

    public Color SelectionGlow { get; private init; }

    public Color CoreMarker { get; private init; }

    public Color Beam { get; private init; }

    public Color MotorAccent { get; private init; }

    public Color Danger { get; private init; }

    public float BeamWidth { get; private init; }

    public float MotorSignalWidth { get; private init; }

    public float GroundEdgeWidth { get; private init; }

    public float GridSpacing { get; private init; }
}

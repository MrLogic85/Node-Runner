using Godot;

namespace NodeRunner.Theme;

public sealed class VisualTheme
{
    private VisualTheme()
    {
    }

    public static VisualTheme Neon { get; } = new()
    {
        ArenaBackground = new Color(0.02f, 0.03f, 0.09f),
        ArenaGrid = new Color(0.05f, 0.22f, 0.32f, 0.55f),
        GroundFill = new Color(0.03f, 0.10f, 0.13f),
        GroundEdge = new Color(0.00f, 0.95f, 0.82f),
        NodeFill = new Color(0.02f, 0.16f, 0.22f),
        NodeGlow = new Color(0.00f, 0.92f, 1.00f),
        SelectionGlow = new Color(1.00f, 0.90f, 0.15f, 0.72f),
        CoreMarker = new Color(1.00f, 0.15f, 0.78f),
        Beam = new Color(0.00f, 0.82f, 1.00f),
        MotorAccent = new Color(1.00f, 0.18f, 0.72f),
        BeamWidth = 6,
        GroundEdgeWidth = 4,
        GridSpacing = 48,
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

    public float BeamWidth { get; private init; }

    public float GroundEdgeWidth { get; private init; }

    public float GridSpacing { get; private init; }
}

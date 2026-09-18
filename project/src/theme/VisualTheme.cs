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
        JointFill = new Color(0.02f, 0.16f, 0.22f),
        JointGlow = new Color(0.00f, 0.92f, 1.00f),
        SelectionGlow = new Color(1.00f, 0.90f, 0.15f, 0.72f),
        HeadMarker = new Color(1.00f, 0.15f, 0.78f),
        Bone = new Color(0.00f, 0.82f, 1.00f),
        Muscle = new Color(1.00f, 0.18f, 0.72f),
        BoneWidth = 6,
        MuscleWidth = 4,
        GroundEdgeWidth = 4,
        GridSpacing = 48,
    };

    public Color ArenaBackground { get; private init; }

    public Color ArenaGrid { get; private init; }

    public Color GroundFill { get; private init; }

    public Color GroundEdge { get; private init; }

    public Color JointFill { get; private init; }

    public Color JointGlow { get; private init; }

    public Color SelectionGlow { get; private init; }

    public Color HeadMarker { get; private init; }

    public Color Bone { get; private init; }

    public Color Muscle { get; private init; }

    public float BoneWidth { get; private init; }

    public float MuscleWidth { get; private init; }

    public float GroundEdgeWidth { get; private init; }

    public float GridSpacing { get; private init; }
}

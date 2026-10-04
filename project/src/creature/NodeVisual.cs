using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>A node's <see cref="JointPart"/> in Training: a child of the node's own physics body.</summary>
public partial class NodeVisual : JointPart, IShadowVisual
{
    /// <summary>A shadow's node is its outer ring at its collision size, without inner ring, tint or selection.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public bool IsShadow
    {
        get => Simplified;
        set => Simplified = value;
    }
}

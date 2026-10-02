using Godot;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Creature;

/// <summary>
/// Draws a node: rendering only, a child of the node's own physics body.
/// </summary>
public partial class NodeVisual : Node2D, IShadowVisual
{
    private bool _isSelected;
    private bool _isShadow;

    /// <summary>A shadow's node is its ring at its drawn size, without a glyph or selection.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Simplified;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public float Radius { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            QueueRedraw();
        }
    }

    public bool IsShadow
    {
        get => _isShadow;
        set
        {
            if (_isShadow == value)
            {
                return;
            }

            _isShadow = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawCircle(
            Vector2.Zero,
            Radius * 1.18f,
            UiGlow.FromBase(Theme.GroundEdge, Theme.EffectsEnabled));
        DrawCircle(Vector2.Zero, Radius, Theme.NodeFill);
        if (IsSelected && !IsShadow)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, Radius * 1.65f);
        }
    }
}

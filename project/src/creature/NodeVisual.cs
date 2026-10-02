using Godot;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Creature;

/// <summary>
/// Draws a node: rendering only, a child of the node's own physics body.
/// </summary>
public partial class NodeVisual : Node2D
{
    private bool _isSelected;

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

    public override void _Draw()
    {
        DrawCircle(
            Vector2.Zero,
            Radius * 1.18f,
            UiGlow.FromBase(Theme.GroundEdge, Theme.EffectsEnabled));
        DrawCircle(Vector2.Zero, Radius, Theme.NodeFill);
        if (IsSelected)
        {
            SelectionDrawing.DrawJoint(this, Theme, Transform2D.Identity, Vector2.Zero, Radius * 1.65f);
        }
    }
}

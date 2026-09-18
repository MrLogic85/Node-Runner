using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// Draws the shared attachment point between beams. Lives as a child of
/// whichever beam "anchors" the node, so it moves with physics for free.
/// </summary>
public partial class NodeVisual : Node2D
{
    private bool _isSelected;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public float Radius { get; set; }

    public bool HasCore { get; set; }

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
        if (IsSelected)
        {
            DrawCircle(Vector2.Zero, Radius * 1.65f, Theme.SelectionGlow);
        }

        DrawCircle(Vector2.Zero, Radius * 1.18f, Theme.NodeGlow);
        DrawCircle(Vector2.Zero, Radius, Theme.NodeFill);

        if (!HasCore)
        {
            return;
        }

        DrawCircle(Vector2.Zero, Radius * 0.42f, Theme.CoreMarker);
    }
}

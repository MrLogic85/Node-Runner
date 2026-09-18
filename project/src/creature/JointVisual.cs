using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class JointVisual : Node2D
{
    private bool _isSelected;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public float Radius { get; set; }

    public bool IsHead { get; set; }

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

        DrawCircle(Vector2.Zero, Radius * 1.18f, Theme.JointGlow);
        DrawCircle(Vector2.Zero, Radius, Theme.JointFill);

        if (!IsHead)
        {
            return;
        }

        DrawCircle(Vector2.Zero, Radius * 0.42f, Theme.HeadMarker);
    }
}

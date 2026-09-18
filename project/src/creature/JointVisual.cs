using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class JointVisual : Node2D
{
    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public float Radius { get; set; }

    public bool IsHead { get; set; }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius * 1.18f, Theme.JointGlow);
        DrawCircle(Vector2.Zero, Radius, Theme.JointFill);

        if (!IsHead)
        {
            return;
        }

        DrawCircle(Vector2.Zero, Radius * 0.42f, Theme.HeadMarker);
    }
}

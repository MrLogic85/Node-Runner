using Godot;

namespace NodeRunner.Theme;

public partial class ArenaBackdrop : Node2D
{
    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public Vector2 Size { get; set; } = new(900, 540);

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Theme.ArenaBackground);

        for (var x = 0f; x <= Size.X; x += Theme.GridSpacing)
        {
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), Theme.ArenaGrid, 1);
        }

        for (var y = 0f; y <= Size.Y; y += Theme.GridSpacing)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), Theme.ArenaGrid, 1);
        }
    }
}

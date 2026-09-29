using Godot;

namespace NodeRunner.Theme;

/// <summary>The arena's background: a grid in the theme's colours, <see cref="Size"/> big from its position.</summary>
public partial class ArenaBackdrop : Node2D
{
    private VisualTheme _theme = VisualTheme.Neon;
    private Vector2 _size = new(900, 540);

    public VisualTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            QueueRedraw();
        }
    }

    [Export]
    public Vector2 Size
    {
        get => _size;
        set
        {
            _size = value;
            QueueRedraw();
        }
    }

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

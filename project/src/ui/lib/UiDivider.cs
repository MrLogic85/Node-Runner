using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A straight divider line in the theme's <c>edge</c> colour. It is as thick as its stroke
/// token and stretches along its orientation as far as its container lets it.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiDivider : Control
{
    private Orientation _orientation = Orientation.Horizontal;
    private UiTokens.Size.Stroke _thickness = UiTokens.Size.Stroke.Hair;
    private UiTokens.Color _color = UiTokens.Color.Edge;

    [Export]
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            Refresh();
        }
    }

    [Export]
    public UiTokens.Size.Stroke Thickness
    {
        get => _thickness;
        set
        {
            _thickness = value;
            Refresh();
        }
    }

    [Export]
    public UiTokens.Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Refresh();
        }
    }


    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var pixels = UiThemeLookup.Size(Thickness);
        return Orientation == Orientation.Horizontal ? new Vector2(0, pixels) : new Vector2(pixels, 0);
    }

    public override void _Draw()
    {
        var pixels = UiThemeLookup.Size(Thickness);
        var line = Orientation == Orientation.Horizontal
            ? new Rect2(0, (Size.Y - pixels) * 0.5f, Size.X, pixels)
            : new Rect2((Size.X - pixels) * 0.5f, 0, pixels, Size.Y);
        var color = UiThemeLookup.Color(this, _color);
        DrawRect(line, color);
    }

    private void Refresh()
    {
        UpdateMinimumSize();
        QueueRedraw();
    }
}

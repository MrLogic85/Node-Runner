using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A straight divider line in a theme colour, <c>edge</c> by default. It is as thick as its stroke
/// token and stretches along its orientation as far as its container lets it. With
/// <see cref="Fill"/> below 1 it draws only that share of its length, as a progress line.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiDivider : Control
{
    private Orientation _orientation = Orientation.Horizontal;
    private UiTokens.Size.Stroke _thickness = UiTokens.Size.Stroke.Hair;
    private UiTokens.Color _color = UiTokens.Color.Edge;
    private float _fill = 1;

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
    /// <summary>The share of the length drawn from the start, from 0 to 1.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Fill
    {
        get => _fill;
        set
        {
            _fill = Mathf.Clamp(value, 0, 1);
            QueueRedraw();
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
            ? new Rect2(0, (Size.Y - pixels) * 0.5f, Size.X * _fill, pixels)
            : new Rect2((Size.X - pixels) * 0.5f, 0, pixels, Size.Y * _fill);
        var color = UiThemeLookup.Color(this, _color);
        DrawRect(line, color);
    }

    private void Refresh()
    {
        UpdateMinimumSize();
        QueueRedraw();
    }
}

using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// One canonical icon on its own, placed in a scene: the glyph at a canonical size, tinted with a
/// theme colour. It draws the icon itself, so it follows a theme swap and writes nothing on its node.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiIcon : Control
{
    private UiIconId _iconId = UiIconId.None;
    private UiIconSize _iconSize = UiIconSize.Standard;
    private UiTokens.Color _color = UiTokens.Color.Ink;

    public UiIcon()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = UiIcons.IconFilter;
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property) => UiIcons.HideIconFilter(property);

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid icon: {value}. Keeping {_iconId}.");
                return;
            }

            _iconId = value;
            ReportDisallowedSize();
            QueueRedraw();
        }
    }

    [Export]
    public UiIconSize IconSize
    {
        get => _iconSize;
        set
        {
            _iconSize = value;
            ReportDisallowedSize();
            UpdateMinimumSize();
            QueueRedraw();
        }
    }

    [Export]
    public UiTokens.Color Color
    {
        get => _color;
        set
        {
            _color = value;
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize() => Vector2.One * UiIcons.Pixels(IconSize);

    // A part glyph at Small is rejected by UiIcons.Load; say so where it is picked, and draw nothing.
    private void ReportDisallowedSize()
    {
        if (!UiIcons.IsAllowed(IconId, IconSize))
        {
            GD.PushError($"{IconId} is a part glyph and is never drawn at {IconSize}.");
        }
    }

    public override void _Draw()
    {
        if (IconId == UiIconId.None || !UiIcons.IsAllowed(IconId, IconSize))
        {
            return;
        }

        var pixels = Vector2.One * UiIcons.Pixels(IconSize);
        DrawTextureRect(UiIcons.Load(IconId, IconSize), new Rect2((Size - pixels) / 2, pixels), tile: false, UiThemeLookup.Color(this, Color));
    }
}

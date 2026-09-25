using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>A canonical notification glyph.</summary>
public sealed record UiNotificationIcon
{
    private readonly UiIconId _icon;

    public UiNotificationIcon(UiIconId icon)
    {
        if (!Enum.IsDefined(icon) || icon == UiIconId.None)
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Choose a visible UI icon.");
        }
        _icon = icon;
    }

    public string ResourcePath => UiIcons.PathFor(_icon);

    internal Texture2D Load(UiIconSize size) => UiIcons.Load(_icon, size);
}

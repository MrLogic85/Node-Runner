using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>A notification glyph selected from either canonical icon library.</summary>
public sealed record UiNotificationIcon
{
    private readonly UiIconId _uiIcon;
    private readonly UiPartIconId? _partIcon;

    public UiNotificationIcon(UiIconId icon)
    {
        if (!Enum.IsDefined(icon) || icon == UiIconId.None)
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Choose a visible UI icon.");
        }
        _uiIcon = icon;
    }

    public UiNotificationIcon(UiPartIconId icon)
    {
        if (!Enum.IsDefined(icon))
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "Choose a canonical part icon.");
        }
        _partIcon = icon;
    }

    public string ResourcePath => _partIcon is { } part
        ? UiIcons.PathFor(part)
        : UiIcons.PathFor(_uiIcon);

    internal Texture2D Load(UiIconSize size) => _partIcon is { } part
        ? UiIcons.Load(part, size)
        : UiIcons.Load(_uiIcon, size);
}

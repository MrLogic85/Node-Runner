using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The optional-icon-plus-caption row shared by <see cref="UiChip"/> and <see cref="UiCallout"/>.
/// It is an unowned child, so it is never saved into a scene; <see cref="Ensure"/> finds it again after a reload.
/// </summary>
internal sealed class UiIconCaption
{
    private const string _nodeName = "_UiContent";

    private UiIconCaption(HBoxContainer row, TextureRect icon, UiLabel label)
    {
        Row = row;
        Icon = icon;
        Label = label;
    }

    public HBoxContainer Row { get; }

    public TextureRect Icon { get; }

    public UiLabel Label { get; }

    public static UiIconCaption Ensure(Control owner)
    {
        if (owner.GetNodeOrNull<HBoxContainer>(_nodeName) is { } existing)
        {
            return new UiIconCaption(existing, existing.GetNode<TextureRect>("Icon"), existing.GetNode<UiLabel>("Label"));
        }

        var row = new HBoxContainer
        {
            Name = _nodeName,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", UiSize.Space.S2);
        var icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var label = new UiLabel
        {
            Name = "Label",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddChild(icon);
        row.AddChild(label);
        owner.AddChild(row);
        return new UiIconCaption(row, icon, label);
    }

    /// <summary>Applies content; the caption is styled by name, the icon is tinted with <paramref name="iconTint"/>.</summary>
    public void Apply(string text, UiIconId iconId, UiIconSize iconSize, UiTokens.Color textColor, Color iconTint)
    {
        Label.Text = text;
        Label.TextStyle = UiTokens.Typography.NoteStrong;
        Label.TextColor = textColor;

        bool hasIcon = iconId != UiIconId.None;
        Icon.Visible = hasIcon;
        Icon.CustomMinimumSize = Vector2.One * UiIcons.Pixels(iconSize);
        Icon.Texture = hasIcon ? UiIcons.Load(iconId, iconSize) : null;
        Icon.SelfModulate = iconTint;
        UiIcons.UseIconFilter(Icon);
    }
}

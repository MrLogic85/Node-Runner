using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Small token-backed status, lock, unlock, or brain-shape label.</summary>
public partial class UiChip : PanelContainer
{
    public enum ChipKind
    {
        Neutral,
        Accent,
        Locked,
        Danger,
        Warning,
        Bad,
        Ok,
    }

    private UiTokens _tokens = UiTokens.Neon;
    private ChipKind _kind;
    private string _text = string.Empty;
    private string _iconText = string.Empty;
    private UiIconId? _iconId;

    [Export]
    public ChipKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Refresh();
        }
    }

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Refresh();
        }
    }

    public UiIconId? IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            Refresh();
        }
    }

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            _iconId = UiIconGlyphs.TryParse(value, out var icon) ? icon : null;
            Refresh();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Refresh();
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        CustomMinimumSize = new Vector2(0, _tokens.ControlExtraSmall);
        var color = Kind switch
        {
            ChipKind.Accent => _tokens.Accent,
            ChipKind.Locked => _tokens.Muted,
            ChipKind.Danger => _tokens.Danger,
            ChipKind.Warning => _tokens.Halo,
            ChipKind.Bad => _tokens.Danger,
            ChipKind.Ok => _tokens.Accent,
            _ => _tokens.Edge,
        };
        var textColor = Kind switch
        {
            ChipKind.Accent => _tokens.Accent,
            ChipKind.Locked => _tokens.Muted,
            ChipKind.Danger => _tokens.Danger,
            ChipKind.Warning => _tokens.Halo,
            ChipKind.Bad => _tokens.Danger,
            ChipKind.Ok => _tokens.Accent,
            _ => _tokens.Ink,
        };

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(row);
        if (IconId is { } icon)
        {
            row.AddChild(UiFieldAndRows.Icon(icon, UiIconSize.Small, textColor));
        }

        var label = UiFieldAndRows.Label(Text, _tokens, _tokens.CaptionText, textColor, HorizontalAlignment.Center);
        row.AddChild(label);
        AddThemeColorOverride("font_color", textColor);
        AddThemeStyleboxOverride("panel", _tokens.ControlStyle(
            _tokens.PanelRaised,
            color,
            radius: _tokens.RadiusPill,
            horizontalPadding: _tokens.Space2,
            verticalPadding: _tokens.Space1));
    }

}

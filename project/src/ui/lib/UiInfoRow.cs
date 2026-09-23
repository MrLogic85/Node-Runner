using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Ringed icon, title, and one help line.</summary>
public partial class UiInfoRow : HBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;

    private string _iconText = "?";
    private UiIconId _iconId = UiIconId.Warn;

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            if (UiIconGlyphs.TryParse(value, out var icon))
            {
                _iconId = icon;
            }

            Rebuild();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            _iconText = value.ToString();
            Rebuild();
        }
    }

    [Export]
    public string Title { get; set; } = "Rotate handle";

    [Export]
    public string Help { get; set; } = "Drag the stem to rotate selected parts.";

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Rebuild();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Rebuild();
    }

    private void Rebuild()
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

        AddThemeConstantOverride("separation", (int)_tokens.Space2);
        var iconFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(_tokens.ControlExtraSmall, _tokens.ControlExtraSmall),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        iconFrame.AddThemeStyleboxOverride("panel", _tokens.ControlStyle(Colors.Transparent, _tokens.Halo, radius: _tokens.RadiusPill));
        iconFrame.AddChild(UiFieldAndRows.Icon(IconId, UiIconSize.Small, _tokens.Halo));
        AddChild(iconFrame);
        var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        labels.AddThemeConstantOverride("separation", 0);
        AddChild(labels);
        labels.AddChild(UiFieldAndRows.Label(Title, _tokens, _tokens.BodyStrongText, _tokens.Ink));
        labels.AddChild(UiFieldAndRows.Label(Help, _tokens, _tokens.NoteText, _tokens.Muted));
    }

}

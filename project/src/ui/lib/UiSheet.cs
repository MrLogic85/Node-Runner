using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Token-backed modal surface used for focused settings and confirmations.</summary>
public partial class UiSheet : PanelContainer
{
    private UiTokens _tokens = UiTokens.Neon;
    private Label? _titleLabel;

    [Export]
    public string Title { get; set; } = string.Empty;

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshStyle();
        }
    }

    public override void _Ready()
    {
        RefreshStyle();
    }

    public void SetBody(Control body)
    {
        _titleLabel = null;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        margin.AddChild(stack);
        if (!string.IsNullOrWhiteSpace(Title))
        {
            _titleLabel = new Label { Text = Title };
            stack.AddChild(_titleLabel);
        }

        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stack.AddChild(body);
        RefreshTitle();
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _tokens.PanelRaised,
            BorderColor = _tokens.LineStrong,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
        });
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (_titleLabel is null)
        {
            return;
        }

        _titleLabel.AddThemeFontSizeOverride("font_size", (int)_tokens.LabelFontSize + 4);
        _titleLabel.AddThemeColorOverride("font_color", _tokens.Ink);
    }
}

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
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space4);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space4);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space4);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space4);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space3);
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

        AddThemeStyleboxOverride("panel", _tokens.PanelStyle(raised: true, borderColor: _tokens.LineStrong));
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (_titleLabel is null)
        {
            return;
        }

        _tokens.ApplyTextStyle(_titleLabel, _tokens.HeadingText);
        _titleLabel.AddThemeColorOverride("font_color", _tokens.Ink);
    }
}

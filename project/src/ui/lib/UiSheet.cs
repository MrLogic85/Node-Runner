using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Token-backed modal surface used for focused settings and confirmations.</summary>
public partial class UiSheet : UiCard
{
    private Label? _titleLabel;
    private string _title = string.Empty;

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            RefreshTitle();
        }
    }

    public override UiTokens Tokens
    {
        get => base.Tokens;
        set
        {
            base.Tokens = value;
            RefreshTitle();
        }
    }

    public override void _Ready()
    {
        Kind = CardVariant.Frame;
        SizeVariant = CardSize.Flush;
        Glow = true;
        base._Ready();
    }

    protected override StyleBoxFlat CreateStyle()
    {
        var style = base.CreateStyle();
        style.BorderColor = Tokens.LineStrong;
        return style;
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
        margin.AddThemeConstantOverride("margin_left", (int)Tokens.Space4);
        margin.AddThemeConstantOverride("margin_top", (int)Tokens.Space4);
        margin.AddThemeConstantOverride("margin_right", (int)Tokens.Space4);
        margin.AddThemeConstantOverride("margin_bottom", (int)Tokens.Space4);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)Tokens.Space3);
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

    private void RefreshTitle()
    {
        if (_titleLabel is null)
        {
            return;
        }

        _titleLabel.Text = Title;
        Tokens.ApplyTextStyle(_titleLabel, Tokens.HeadingText);
        _titleLabel.AddThemeColorOverride("font_color", Tokens.Ink);
    }
}

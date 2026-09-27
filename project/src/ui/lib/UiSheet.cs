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
        style.BorderColor = UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
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
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S4);
        margin.AddThemeConstantOverride("margin_top", (int)UiSize.Space.S4);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S4);
        margin.AddThemeConstantOverride("margin_bottom", (int)UiSize.Space.S4);
        AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)UiSize.Space.S3);
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
        UiThemeLookup.ApplyTextStyle(_titleLabel, UiTokens.Typography.Heading, UiTokens.Color.Ink);
    }
}

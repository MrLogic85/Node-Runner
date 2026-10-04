using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compact numeric readout for changing learner-facing values.</summary>
public partial class UiReadout : VBoxContainer
{
    private Label? _valueLabel;
    private Label? _captionLabel;

    private string _caption = string.Empty;

    [Export]
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value;
            Refresh();
        }
    }

    private string _valueText = "—";

    [Export]
    public string ValueText
    {
        get => _valueText;
        set
        {
            _valueText = value;
            Refresh();
        }
    }

    private bool _emphasis;

    [Export]
    public bool Emphasis
    {
        get => _emphasis;
        set
        {
            _emphasis = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        _captionLabel = new Label { Text = Caption };
        _valueLabel = new Label { Text = ValueText };
        AddChild(_captionLabel);
        AddChild(_valueLabel);
        Refresh();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Refresh);
        }
    }

    public void SetValue(string value)
    {
        ValueText = value;
    }

    private void Refresh()
    {
        if (!IsInsideTree() || _captionLabel is null || _valueLabel is null)
        {
            return;
        }

        UiTranslation.ShareContext(this, _captionLabel);
        UiTranslation.ShareContext(this, _valueLabel);
        _captionLabel.Text = Caption;
        _valueLabel.Text = ValueText;
        AddThemeConstantOverride("separation", UiSize.Space.S1);

        UiThemeLookup.ApplyTextStyle(_captionLabel, UiTokens.Typography.Caption, UiTokens.Color.Muted);
        UiThemeLookup.ApplyTextStyle(
            _valueLabel,
            Emphasis ? UiTokens.Typography.ReadoutLarge : UiTokens.Typography.Readout,
            Emphasis ? UiTokens.Color.Accent : UiTokens.Color.Ink);
    }
}

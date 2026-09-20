using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compact numeric readout for changing learner-facing values.</summary>
public partial class UiReadout : VBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;
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
        AddThemeConstantOverride("separation", (int)_tokens.Space1);
        _captionLabel = new Label { Text = Caption };
        _valueLabel = new Label { Text = ValueText };
        AddChild(_captionLabel);
        AddChild(_valueLabel);
        Refresh();
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

        _captionLabel.Text = Caption;
        _valueLabel.Text = ValueText;
        _tokens.ApplyTextStyle(_captionLabel, _tokens.CaptionText);
        _captionLabel.AddThemeColorOverride("font_color", _tokens.Muted);
        _tokens.ApplyTextStyle(_valueLabel, Emphasis ? _tokens.ReadoutLargeText : _tokens.ReadoutText);
        _valueLabel.AddThemeColorOverride("font_color", Emphasis ? _tokens.Accent : _tokens.Ink);
    }
}

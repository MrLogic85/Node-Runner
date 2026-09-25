using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Static label/value row with an optional icon before the value.</summary>
[Tool]
[GlobalClass]
public partial class UiValueRow : HBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = "";
    private string _valueText = "";
    private UiIconId _iconId = UiIconId.None;

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            Rebuild();
        }
    }

    [Export]
    public string ValueText
    {
        get => _valueText;
        set
        {
            _valueText = value;
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
            Rebuild();
        }
    }

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
        MouseFilter = MouseFilterEnum.Ignore;
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
        var label = UiFieldAndRows.Label(LabelText, _tokens, _tokens.CaptionText, _tokens.Muted);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(label);
        var readout = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        readout.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(readout);
        if (IconId != UiIconId.None)
        {
            var glyph = UiFieldAndRows.Icon(IconId, UiIconSize.Small, _tokens.Ink);
            glyph.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            readout.AddChild(glyph);
        }

        var value = UiFieldAndRows.Label(ValueText, _tokens, _tokens.ReadoutMediumText, _tokens.Ink, HorizontalAlignment.Right);
        value.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        value.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        readout.AddChild(value);
    }
}

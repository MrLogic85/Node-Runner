using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>On/off setting row with optional help text and dense presentation.</summary>
public partial class UiToggleRow : PanelContainer
{
    [Signal]
    public delegate void ToggledEventHandler(bool on);

    private UiTokens _tokens = UiTokens.Neon;
    private bool _on = true;
    private bool _disabled;
    private bool _dense;

    [Export]
    public string LabelText { get; set; } = "Sounds";

    [Export]
    public string Subtext { get; set; } = "On";

    [Export]
    public bool On
    {
        get => _on;
        set
        {
            _on = value;
            Rebuild();
        }
    }

    [Export]
    public bool Dense
    {
        get => _dense;
        set
        {
            _dense = value;
            Rebuild();
        }
    }

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
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

    public override void _Ready() => Rebuild();

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

        CustomMinimumSize = new Vector2(0, Dense ? _tokens.ControlSmall : _tokens.TouchTarget);
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeStyleboxOverride("panel", _tokens.ControlStyle(_tokens.PanelRaised, Disabled ? _tokens.Line : _tokens.LineStrong));

        var toggle = new Button
        {
            Flat = true,
            Disabled = Disabled,
            CustomMinimumSize = new Vector2(0, Dense ? _tokens.ControlSmall : _tokens.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = LabelText,
        };
        toggle.Pressed += Toggle;
        AddChild(toggle);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        toggle.AddChild(row);
        var labelColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        labelColumn.AddThemeConstantOverride("separation", 0);
        row.AddChild(labelColumn);
        labelColumn.AddChild(UiFieldAndRows.Label(LabelText, _tokens, _tokens.BodyStrongText, Disabled ? _tokens.Muted : _tokens.Ink));
        if (!Dense && !string.IsNullOrWhiteSpace(Subtext))
        {
            labelColumn.AddChild(UiFieldAndRows.Label(Subtext, _tokens, _tokens.NoteText, _tokens.Muted));
        }

        var pill = UiFieldAndRows.Label(On ? "ON" : "OFF", _tokens, _tokens.CaptionText, On ? _tokens.OnAccent : _tokens.Muted, HorizontalAlignment.Center);
        pill.CustomMinimumSize = new Vector2(Dense ? 42 : 54, Dense ? 24 : 28);
        pill.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(On ? _tokens.Accent : _tokens.Panel, On ? _tokens.Accent : _tokens.LineStrong, radius: _tokens.RadiusPill));
        row.AddChild(pill);

        toggle.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private void Toggle()
    {
        if (Disabled)
        {
            return;
        }

        On = !On;
        EmitSignal(SignalName.Toggled, On);
    }
}

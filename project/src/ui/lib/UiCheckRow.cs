using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Checkbox row with shape and text state cues.</summary>
public partial class UiCheckRow : PanelContainer
{
    [Signal]
    public delegate void ToggledEventHandler(bool checkedState);

    private UiTokens _tokens = UiTokens.Neon;
    private bool _checked;
    private bool _disabled;

    [Export]
    public string LabelText { get; set; } = "Run until power is out";

    [Export]
    public string Subtext { get; set; } = "Ends when the battery does";

    [Export]
    public bool Checked
    {
        get => _checked;
        set
        {
            _checked = value;
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

        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget);
        MouseFilter = MouseFilterEnum.Pass;
        AddThemeStyleboxOverride("panel", _tokens.ControlStyle(_tokens.PanelRaised, Disabled ? _tokens.Line : _tokens.LineStrong));
        var toggle = new Button
        {
            Flat = true,
            Disabled = Disabled,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = LabelText,
        };
        toggle.Pressed += Toggle;
        toggle.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        AddChild(toggle);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        toggle.AddChild(row);
        var box = new PanelContainer
        {
            CustomMinimumSize = new Vector2(24, 24),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        box.CustomMinimumSize = new Vector2(24, 24);
        box.AddThemeStyleboxOverride("panel", _tokens.ControlStyle(Checked ? _tokens.Accent : _tokens.Panel, Checked ? _tokens.Accent : _tokens.LineStrong, radius: _tokens.RadiusSmall));
        if (Checked)
        {
            box.AddChild(UiFieldAndRows.Icon(UiIconId.Check, UiIconSize.Small, _tokens.OnAccent));
        }

        row.AddChild(box);
        var labels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        labels.AddThemeConstantOverride("separation", 0);
        row.AddChild(labels);
        labels.AddChild(UiFieldAndRows.Label(LabelText, _tokens, _tokens.BodyStrongText, Disabled ? _tokens.Muted : _tokens.Ink));
        if (!string.IsNullOrWhiteSpace(Subtext))
        {
            labels.AddChild(UiFieldAndRows.Label(Subtext, _tokens, _tokens.NoteText, _tokens.Muted));
        }
    }

    private void Toggle()
    {
        if (Disabled)
        {
            return;
        }

        Checked = !Checked;
        EmitSignal(SignalName.Toggled, Checked);
    }
}

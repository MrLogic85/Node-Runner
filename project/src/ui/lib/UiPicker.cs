using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Picker that exposes valid choices inline and identifies the current choice by shape and colour.</summary>
public partial class UiPicker : PanelContainer
{
    [Signal]
    public delegate void SelectionChangedEventHandler(string value);

    [Signal]
    public delegate void OpenChangedEventHandler(bool open);

    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = "Fixed part";
    private string _valueText = "Beam A";
    private bool _open;
    private bool _locked;
    private string[] _options = ["Beam A", "Beam B · swaps", "Beam C"];

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
    public bool Open
    {
        get => _open;
        set => SetOpen(value, emit: false);
    }

    [Export]
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            if (_locked)
            {
                _open = false;
            }

            Rebuild();
        }
    }

    [Export]
    public string[] Options
    {
        get => _options;
        set
        {
            _options = value ?? [];
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

        MouseFilter = MouseFilterEnum.Pass;
        AddThemeStyleboxOverride("panel", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.LineStrong));
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        AddChild(stack);

        var closedRow = new Button
        {
            Flat = true,
            Disabled = Locked,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = LabelText,
        };
        closedRow.Pressed += () => SetOpen(!Open, emit: true);
        closedRow.AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
        stack.AddChild(closedRow);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        closedRow.AddChild(row);
        if (Locked)
        {
            row.AddChild(UiFieldAndRows.Icon(UiIconId.Lock, UiIconSize.Small, _tokens.Muted));
        }

        row.AddChild(UiFieldAndRows.Label(LabelText, _tokens, _tokens.CaptionText, _tokens.Muted));
        var value = UiFieldAndRows.Label(ValueText, _tokens, _tokens.BodyStrongText, Locked ? _tokens.Muted : _tokens.Ink, HorizontalAlignment.Right);
        value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(value);
        row.AddChild(UiFieldAndRows.Icon(Open ? UiIconId.ChevronDown : UiIconId.ChevronRight, UiIconSize.Standard, Locked ? _tokens.Muted : _tokens.Accent));

        if (!Open)
        {
            return;
        }

        foreach (var option in Options)
        {
            var optionButton = new Button
            {
                Text = DisplayValue(option),
                Flat = true,
                CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left,
            };
            var capturedOption = option;
            optionButton.Pressed += () => Select(capturedOption);
            _tokens.ApplyTextStyle(optionButton, _tokens.SmallText);
            optionButton.AddThemeColorOverride("font_color", IsCurrent(option) ? _tokens.Accent : _tokens.Ink);
            optionButton.AddThemeColorOverride("font_hover_color", _tokens.Accent);
            if (IsCurrent(option))
            {
                UiIcons.Apply(optionButton, UiIconId.Check, UiIconSize.Small, _tokens.Accent);
            }

            optionButton.AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
            stack.AddChild(optionButton);
        }
    }

    private void SetOpen(bool open, bool emit)
    {
        var nextOpen = open && !Locked;
        if (_open == nextOpen)
        {
            return;
        }

        _open = nextOpen;
        Rebuild();
        if (emit)
        {
            EmitSignal(SignalName.OpenChanged, _open);
        }
    }

    private void Select(string option)
    {
        ValueText = DisplayValue(option);
        SetOpen(false, emit: true);
        EmitSignal(SignalName.SelectionChanged, ValueText);
    }

    private bool IsCurrent(string option) =>
        string.Equals(DisplayValue(option), ValueText, StringComparison.Ordinal);

    private static string DisplayValue(string option) =>
        option.EndsWith(" · swaps", StringComparison.Ordinal)
            ? option[..^" · swaps".Length]
            : option;
}

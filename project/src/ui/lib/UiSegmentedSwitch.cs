using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe mutually exclusive choice control for compact mode/profile switches.</summary>
public partial class UiSegmentedSwitch : HBoxContainer
{
    [Signal]
    public delegate void SelectionChangedEventHandler(int index);

    private UiTokens _tokens = UiTokens.Neon;
    private string[] _options = { "One", "Two" };
    private int _selectedIndex;

    [Export]
    public string[] Options
    {
        get => _options;
        set
        {
            _options = value ?? System.Array.Empty<string>();
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _options.Length - 1));
            Rebuild();
        }
    }

    [Export]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = Mathf.Clamp(value, 0, Mathf.Max(0, _options.Length - 1));
            RefreshSelection();
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

        AddThemeConstantOverride("separation", 4);
        for (var index = 0; index < _options.Length; index++)
        {
            var button = new Button
            {
                Text = _options[index],
                ToggleMode = true,
                ButtonPressed = index == _selectedIndex,
                CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var capturedIndex = index;
            button.Pressed += () => Select(capturedIndex);
            AddChild(button);
            StyleButton(button, index == _selectedIndex);
        }
    }

    private void Select(int index)
    {
        if (index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        RefreshSelection();
        EmitSignal(SignalName.SelectionChanged, _selectedIndex);
    }

    private void RefreshSelection()
    {
        for (var index = 0; index < GetChildCount(); index++)
        {
            if (GetChild(index) is Button button)
            {
                button.ButtonPressed = index == _selectedIndex;
                StyleButton(button, index == _selectedIndex);
            }
        }
    }

    private void StyleButton(Button button, bool selected)
    {
        button.AddThemeFontSizeOverride("font_size", (int)_tokens.LabelFontSize);
        button.AddThemeColorOverride("font_color", selected ? _tokens.OnAccent : _tokens.Ink);
        button.AddThemeColorOverride("font_hover_color", _tokens.OnAccent);
        button.AddThemeStyleboxOverride("normal", CreateStyle(selected));
        button.AddThemeStyleboxOverride("hover", CreateStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateStyle(true));
        button.AddThemeStyleboxOverride("focus", CreateStyle(true));
    }

    private StyleBoxFlat CreateStyle(bool selected)
    {
        return new StyleBoxFlat
        {
            BgColor = selected ? _tokens.Accent : _tokens.Panel,
            BorderColor = _tokens.Edge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
        };
    }
}

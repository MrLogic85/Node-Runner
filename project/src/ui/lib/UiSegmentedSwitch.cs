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
    private string[] _icons = [];
    private UiIconId[] _iconIds = [];
    private bool _fullWidth = true;

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

    [Export]
    public string[] Icons
    {
        get => _icons;
        set
        {
            _icons = value ?? [];
            Rebuild();
        }
    }

    public UiIconId[] IconIds
    {
        get => _iconIds;
        set
        {
            _iconIds = value ?? [];
            Rebuild();
        }
    }

    [Export]
    public bool FullWidth
    {
        get => _fullWidth;
        set
        {
            _fullWidth = value;
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

        AddThemeConstantOverride("separation", (int)_tokens.Space1);
        for (var index = 0; index < _options.Length; index++)
        {
            var button = new Button
            {
                Text = _options[index],
                ToggleMode = true,
                ButtonPressed = index == _selectedIndex,
                CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
                SizeFlagsHorizontal = FullWidth ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
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
            RefreshSelection();
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
                button.Text = _options[index];
                StyleButton(button, index == _selectedIndex);
            }
        }
    }

    private void StyleButton(Button button, bool selected)
    {
        _tokens.ApplyTextStyle(button, _tokens.LabelText);
        button.AddThemeColorOverride("font_color", _tokens.Ink);
        button.AddThemeColorOverride("font_hover_color", selected ? _tokens.Ink : _tokens.Accent);
        var icon = selected ? UiIconId.Check : IconFor(button.GetIndex());
        if (icon is { } iconId)
        {
            UiIcons.Apply(button, iconId, UiIconSize.Small, selected ? _tokens.Accent : _tokens.Ink);
        }
        else
        {
            button.Icon = null;
        }

        button.AddThemeStyleboxOverride("normal", CreateStyle(selected));
        button.AddThemeStyleboxOverride("hover", CreateStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateStyle(true));
        button.AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
    }

    private UiIconId? IconFor(int index) =>
        index < _iconIds.Length
            ? _iconIds[index]
            : index < Icons.Length && UiIconGlyphs.TryParse(Icons[index], out var icon) ? icon : null;

    private StyleBoxFlat CreateStyle(bool selected)
    {
        return new StyleBoxFlat
        {
            BgColor = selected ? _tokens.AccentSoft : _tokens.PanelRaised,
            BorderColor = selected ? _tokens.Accent : _tokens.LineStrong,
            BorderWidthLeft = (int)(selected ? _tokens.StrokeSignal : _tokens.StrokeHair),
            BorderWidthTop = (int)(selected ? _tokens.StrokeSignal : _tokens.StrokeHair),
            BorderWidthRight = (int)(selected ? _tokens.StrokeSignal : _tokens.StrokeHair),
            BorderWidthBottom = (int)(selected ? _tokens.StrokeSignal : _tokens.StrokeHair),
            CornerRadiusTopLeft = (int)_tokens.RadiusMedium,
            CornerRadiusTopRight = (int)_tokens.RadiusMedium,
            CornerRadiusBottomLeft = (int)_tokens.RadiusMedium,
            CornerRadiusBottomRight = (int)_tokens.RadiusMedium,
            ContentMarginLeft = UiSpacing.SegmentedControlHorizontalPadding(_tokens),
            ContentMarginTop = UiSpacing.ControlVerticalPadding(_tokens),
            ContentMarginRight = UiSpacing.SegmentedControlHorizontalPadding(_tokens),
            ContentMarginBottom = UiSpacing.ControlVerticalPadding(_tokens),
        };
    }
}

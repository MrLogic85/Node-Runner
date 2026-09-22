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
    private ButtonGroup? _group;

    [Export]
    public string[] Options
    {
        get => _options;
        set
        {
            _options = value ?? System.Array.Empty<string>();
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _options.Length - 1));
            RefreshOptions();
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
            RefreshAppearance();
        }
    }

    public UiIconId[] IconIds
    {
        get => _iconIds;
        set
        {
            _iconIds = value ?? [];
            RefreshAppearance();
        }
    }

    [Export]
    public bool FullWidth
    {
        get => _fullWidth;
        set
        {
            _fullWidth = value;
            RefreshLayout();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshAppearance();
        }
    }

    public override void _Ready()
    {
        _group ??= new ButtonGroup { AllowUnpress = false };
        RefreshOptions();
    }

    private void RefreshOptions()
    {
        if (_group is null)
        {
            return;
        }

        var focusedIndex = -1;
        for (var index = 0; index < GetChildCount(); index++)
        {
            if (GetChild<Button>(index).HasFocus())
            {
                focusedIndex = index;
            }
        }

        while (GetChildCount() > _options.Length)
        {
            var button = GetChild<Button>(GetChildCount() - 1);
            button.ButtonGroup = null;
            RemoveChild(button);
            button.QueueFree();
        }

        while (GetChildCount() < _options.Length)
        {
            var index = GetChildCount();
            var button = new Button
            {
                ToggleMode = true,
                ButtonGroup = _group,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            button.Pressed += () => Select(index);
            AddChild(button);
        }

        RefreshAppearance();
        RefreshSelection();
        if (focusedIndex >= 0 && GetChildCount() > 0 && IsInsideTree())
        {
            GetChild<Button>(Math.Min(focusedIndex, GetChildCount() - 1)).GrabFocus();
        }
    }

    private void Select(int index)
    {
        if (index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        EmitSignal(SignalName.SelectionChanged, _selectedIndex);
    }

    private void RefreshSelection()
    {
        if (_selectedIndex < GetChildCount())
        {
            // SetPressedNoSignal bypasses ButtonGroup exclusivity. ButtonPressed
            // updates the group without emitting the Button.Pressed we forward.
            GetChild<Button>(_selectedIndex).ButtonPressed = true;
        }
    }

    private void RefreshAppearance()
    {
        for (var index = 0; index < GetChildCount(); index++)
        {
            var button = GetChild<Button>(index);
            button.Text = _options[index];
            button.AccessibilityName = _options[index];
            _tokens.ApplyTextStyle(button, _tokens.LabelText);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            {
                button.AddThemeColorOverride(state, _tokens.Ink);
            }

            if (IconFor(index) is { } iconId)
            {
                UiIcons.Apply(button, iconId, UiIconSize.Standard, _tokens.Ink);
                button.AddThemeColorOverride("icon_hover_pressed_color", _tokens.Ink);
                button.AddThemeColorOverride("icon_focus_color", _tokens.Ink);
            }
            else
            {
                button.Icon = null;
            }

            button.AddThemeConstantOverride("h_separation", (int)_tokens.Space2);
            var normal = CreateStyle(index, false);
            var selected = CreateStyle(index, true);
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", normal);
            button.AddThemeStyleboxOverride("pressed", selected);
            button.AddThemeStyleboxOverride("hover_pressed", selected);
            var focus = CreateStyle(index, true);
            focus.DrawCenter = false;
            focus.BorderColor = _tokens.Halo;
            button.AddThemeStyleboxOverride("focus", focus);
        }

        RefreshLayout();
    }

    private void RefreshLayout()
    {
        AddThemeConstantOverride("separation", 0);
        var width = _tokens.TouchTarget;
        foreach (var child in GetChildren())
        {
            var button = (Button)child;
            width = Mathf.Max(width, button.GetMinimumSize().X);
        }

        foreach (var child in GetChildren())
        {
            var button = (Button)child;
            button.CustomMinimumSize = new Vector2(FullWidth ? width : _tokens.TouchTarget, _tokens.TouchTarget);
            button.SizeFlagsHorizontal = FullWidth ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        }
    }

    private UiIconId? IconFor(int index) =>
        index < _iconIds.Length
            ? _iconIds[index]
            : index < Icons.Length && UiIconGlyphs.TryParse(Icons[index], out var icon) ? icon : null;

    private StyleBoxFlat CreateStyle(int index, bool selected)
    {
        var first = index == 0;
        var last = index == _options.Length - 1;
        var stroke = (int)(selected ? _tokens.StrokeSignal : _tokens.StrokeHair);
        var inset = (_tokens.TouchTarget - _tokens.ControlHeight) * 0.5f;
        var verticalPadding = (_tokens.TouchTarget - _tokens.LabelText.LineHeight) * 0.5f;
        return new StyleBoxFlat
        {
            BgColor = selected ? _tokens.PanelRaised.Blend(_tokens.AccentSoft) : _tokens.PanelRaised,
            BorderColor = selected ? _tokens.Accent : _tokens.LineStrong,
            BorderWidthLeft = first || selected ? stroke : 0,
            BorderWidthTop = stroke,
            BorderWidthRight = stroke,
            BorderWidthBottom = stroke,
            CornerRadiusTopLeft = first ? (int)_tokens.RadiusMedium : 0,
            CornerRadiusTopRight = last ? (int)_tokens.RadiusMedium : 0,
            CornerRadiusBottomLeft = first ? (int)_tokens.RadiusMedium : 0,
            CornerRadiusBottomRight = last ? (int)_tokens.RadiusMedium : 0,
            ExpandMarginTop = -inset,
            ExpandMarginBottom = -inset,
            ContentMarginLeft = UiSpacing.SegmentedControlHorizontalPadding(_tokens),
            ContentMarginTop = verticalPadding,
            ContentMarginRight = UiSpacing.SegmentedControlHorizontalPadding(_tokens),
            ContentMarginBottom = verticalPadding,
        };
    }
}

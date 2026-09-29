using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Icon tab strip with single active selection. The tabs share the strip's width equally with a
/// <c>space-1</c> gap (reference <c>flex: 1</c>), so the owner decides the width.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiIconTabs : HBoxContainer
{
    [Signal]
    public delegate void TabSelectedEventHandler(int index);

    private ButtonGroup? _group;
    private Godot.Collections.Array<UiIconId> _icons = [];
    private readonly List<Button> _buttons = [];

    [Export]
    public Godot.Collections.Array<UiIconId> Icons
    {
        get => _icons;
        set
        {
            var icons = value?.Duplicate() ?? [];
            ValidateIcons(icons);
            _icons = icons;
            _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _icons.Count);
            RebuildButtons();
        }
    }

    [Export]
    public int SelectedIndex
    {
        get => _activeIndex;
        set
        {
            var normalized = UiComponentContracts.NormalizeTabIndex(value, _icons.Count);
            if (normalized == _activeIndex)
            {
                return;
            }

            _activeIndex = normalized;
            ApplySelection();
        }
    }

    private int _activeIndex;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _group ??= new ButtonGroup { AllowUnpress = false };
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _icons.Count);
        RebuildButtons();
    }

    private readonly UiUnsavedState _unsaved = new("theme_override_constants/separation");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, ApplyTheme))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, ApplyTheme);
        }
    }

    private void RebuildButtons()
    {
        if (_group is null)
        {
            return;
        }

        var focusedIndex = -1;
        for (var index = 0; index < _buttons.Count; index++)
        {
            if (_buttons[index].HasFocus())
            {
                focusedIndex = index;
                break;
            }
        }

        while (_buttons.Count > _icons.Count)
        {
            var button = _buttons[^1];
            button.ButtonGroup = null;
            RemoveChild(button);
            button.QueueFree();
            _buttons.RemoveAt(_buttons.Count - 1);
        }

        while (_buttons.Count < _icons.Count)
        {
            var button = new Button
            {
                Text = string.Empty,
                IconAlignment = HorizontalAlignment.Center,
                ToggleMode = true,
                ButtonGroup = _group,
                MouseFilter = MouseFilterEnum.Pass,
            };
            var buttonIndex = _buttons.Count;
            button.Pressed += () => SelectTab(buttonIndex);
            _buttons.Add(button);
            AddChild(button);
        }

        ApplyTheme();
        ApplySelection();
        if (focusedIndex >= 0 && _buttons.Count > 0 && IsInsideTree())
        {
            _buttons[Math.Min(focusedIndex, _buttons.Count - 1)].GrabFocus();
        }
    }

    private void SelectTab(int index)
    {
        var normalized = UiComponentContracts.NormalizeTabIndex(index, _icons.Count);
        if (normalized == _activeIndex)
        {
            return;
        }

        _activeIndex = normalized;
        ApplySelection();
        EmitSignal(SignalName.TabSelected, normalized);
    }

    private void ApplySelection()
    {
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _icons.Count);
        if (_activeIndex < _buttons.Count)
        {
            _buttons[_activeIndex].ButtonPressed = true;
        }
    }

    private void ApplyTheme()
    {
        AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        for (var index = 0; index < _buttons.Count; index++)
        {
            var button = _buttons[index];
            button.Text = string.Empty;
            button.CustomMinimumSize = new Vector2(0, UiSize.Control.Small);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            ApplyIcon(button, _icons[index]);
            ApplyStyle(button);
        }
    }

    private static void ValidateIcons(Godot.Collections.Array<UiIconId> icons)
    {
        for (var index = 0; index < icons.Count; index++)
        {
            var icon = icons[index];
            if (!Enum.IsDefined(icon) || icon == UiIconId.None)
            {
                throw new ArgumentException(
                    $"Icon tab at index {index} must use a visible UiIconId.",
                    nameof(icons));
            }
        }
    }

    private void ApplyIcon(Button button, UiIconId icon)
    {
        button.ThemeTypeVariation = UiThemeExpander.IconTabVariationName;
        UiIcons.Apply(button, icon, UiIconSize.Large);
        button.AddThemeConstantOverride("h_separation", 0);
    }

    private void ApplyStyle(Button button)
    {
        var inactive = CreateStyle(selected: false);
        var active = CreateStyle(selected: true);
        button.AddThemeStyleboxOverride("normal", inactive);
        button.AddThemeStyleboxOverride("hover", inactive);
        button.AddThemeStyleboxOverride("pressed", active);
        button.AddThemeStyleboxOverride("hover_pressed", active);
        button.AddThemeStyleboxOverride("focus", CreateFocusStyle());
    }

    private StyleBoxFlat CreateStyle(bool selected)
    {
        var background = selected
            ? UiThemeLookup.Color(this, UiTokens.Color.PanelRaised).Blend(UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)))
            : UiThemeLookup.Color(this, UiTokens.Color.PanelRaised);
        var border = selected ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
        var style = UiThemeLookup.CreateStyleBox(background,
            border,
            selected ? UiSize.Stroke.Signal : UiSize.Stroke.Hair,
            UiSize.Radius.Medium);
        style.SetContentMarginAll(0);
        return style;
    }

    private StyleBoxFlat CreateFocusStyle()
    {
        var style = CreateStyle(selected: true);
        style.DrawCenter = false;
        style.BorderColor = UiThemeLookup.Color(this, UiTokens.Color.Halo);
        return style;
    }
}

using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Icon tab strip with single active selection.</summary>
public partial class UiIconTabs : HBoxContainer
{
    [Signal]
    public delegate void TabSelectedEventHandler(string tabId, int index);

    private UiTokens _tokens = UiTokens.Neon;
    private ButtonGroup? _group;
    private bool _tabsConfigured;
    private TabItem[] _tabs = [];

    private readonly List<TabSlot> _slots = [];

    public readonly record struct TabItem(string Id, UiIconId IconId, string AccessibleLabel, bool Enabled = true)
    {
        public TabItem(string id, UiPartIconId partIconId, string accessibleLabel, bool enabled = true)
            : this(id, default(UiIconId), accessibleLabel, enabled)
        {
            PartIconId = partIconId;
        }

        public UiPartIconId? PartIconId { get; init; }
    }

    [Export]
    public string[] Glyphs
    {
        get => _glyphs;
        set
        {
            _glyphs = value ?? [];
            _tabsConfigured = true;
            _tabs = CreateTabsFromGlyphs();
            _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
            RefreshTabs();
        }
    }

    private string[] _glyphs = ["beam", "core", "joint", "select"];

    [Export]
    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            var normalized = UiComponentContracts.NormalizeTabIndex(value, _tabs.Length);
            if (normalized == _activeIndex)
            {
                return;
            }

            _activeIndex = normalized;
            RefreshSelection();
        }
    }

    private int _activeIndex;

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
        if (!_tabsConfigured)
        {
            _tabs = CreateTabsFromGlyphs();
            _tabsConfigured = true;
        }
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
        RefreshTabs();
    }

    public void SetTabs(params TabItem[] tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        _tabsConfigured = true;
        _tabs = tabs.ToArray();
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
        RefreshTabs();
    }

    private void RefreshTabs()
    {
        if (_group is null)
        {
            return;
        }

        AddThemeConstantOverride("separation", (int)_tokens.Space1);

        var focusedIndex = -1;
        for (var index = 0; index < _slots.Count; index++)
        {
            if (_slots[index].Button.HasFocus())
            {
                focusedIndex = index;
                break;
            }
        }

        while (_slots.Count > _tabs.Length)
        {
            var slot = _slots[^1];
            slot.Button.ButtonGroup = null;
            RemoveChild(slot.Button);
            slot.Button.QueueFree();
            _slots.RemoveAt(_slots.Count - 1);
        }

        while (_slots.Count < _tabs.Length)
        {
            var button = new Button
            {
                Text = string.Empty,
                IconAlignment = HorizontalAlignment.Center,
                ToggleMode = true,
                ButtonGroup = _group,
                CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            var slotIndex = _slots.Count;
            button.Pressed += () => SelectTab(slotIndex);
            _slots.Add(new TabSlot(button));
            AddChild(button);
        }

        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
        RefreshAppearance();
        RefreshSelection();
        if (focusedIndex >= 0 && _slots.Count > 0 && IsInsideTree())
        {
            _slots[Math.Min(focusedIndex, _slots.Count - 1)].Button.GrabFocus();
        }
    }

    private void SelectTab(int index)
    {
        var normalized = UiComponentContracts.NormalizeTabIndex(index, _tabs.Length);
        if (normalized == _activeIndex)
        {
            return;
        }

        _activeIndex = normalized;
        RefreshSelection();
        EmitSignal(SignalName.TabSelected, _tabs[normalized].Id, normalized);
    }

    private void RefreshSelection()
    {
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
        if (_activeIndex < _slots.Count)
        {
            _slots[_activeIndex].Button.ButtonPressed = true;
        }

        RefreshDisabledStyle();
    }

    private void RefreshAppearance()
    {
        AddThemeConstantOverride("separation", (int)_tokens.Space1);
        for (var index = 0; index < _slots.Count; index++)
        {
            var item = _tabs[index];
            var slot = _slots[index];
            var button = slot.Button;
            var label = string.IsNullOrWhiteSpace(item.AccessibleLabel) ? item.Id : item.AccessibleLabel;
            button.Text = string.Empty;
            button.TooltipText = label;
            button.AccessibilityName = label;
            button.Disabled = !item.Enabled;
            button.CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
            button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            ApplyIcon(button, item);
            ApplyStyle(slot);
        }

        RefreshDisabledStyle();
    }

    private TabItem[] CreateTabsFromGlyphs() =>
        Glyphs.Select((glyph, index) => new TabItem(
            index.ToString(),
            UiIconGlyphs.ParseOr(glyph, UiIconId.Select),
            glyph)).ToArray();

    private void ApplyIcon(Button button, TabItem item)
    {
        if (item.PartIconId is { } partIcon)
        {
            UiIcons.Apply(button, partIcon, UiIconSize.Large, _tokens.Muted);
        }
        else
        {
            UiIcons.Apply(button, item.IconId, UiIconSize.Large, _tokens.Muted);
        }

        button.AddThemeConstantOverride("h_separation", 0);
        button.AddThemeColorOverride("icon_pressed_color", _tokens.Accent);
        button.AddThemeColorOverride("icon_hover_pressed_color", _tokens.Accent);
        button.AddThemeColorOverride("icon_focus_color", _tokens.Muted);
    }

    private void ApplyStyle(TabSlot slot)
    {
        slot.Disabled = CreateStyle(selected: false, disabled: true);
        slot.DisabledSelected = CreateStyle(selected: true, disabled: true);
        var inactive = CreateStyle(selected: false);
        var active = CreateStyle(selected: true);
        slot.Button.AddThemeStyleboxOverride("normal", inactive);
        slot.Button.AddThemeStyleboxOverride("hover", inactive);
        slot.Button.AddThemeStyleboxOverride("pressed", active);
        slot.Button.AddThemeStyleboxOverride("hover_pressed", active);
        slot.Button.AddThemeStyleboxOverride("focus", CreateFocusStyle());
        slot.Button.AddThemeStyleboxOverride("disabled", slot.Disabled);
    }

    private void RefreshDisabledStyle()
    {
        for (var index = 0; index < _slots.Count; index++)
        {
            var slot = _slots[index];
            slot.Button.AddThemeStyleboxOverride("disabled", index == _activeIndex ? slot.DisabledSelected : slot.Disabled);
            slot.Button.AddThemeColorOverride("icon_disabled_color",
                DisabledColor(index == _activeIndex ? _tokens.Accent : _tokens.Muted));
        }
    }

    private StyleBoxFlat CreateStyle(bool selected, bool disabled = false)
    {
        var background = selected ? _tokens.AccentSoft : Colors.Transparent;
        var border = selected ? _tokens.Accent : _tokens.Line;
        var style = _tokens.ControlStyle(
            disabled ? DisabledColor(background) : background,
            disabled ? DisabledColor(border) : border,
            selected ? _tokens.StrokeSignal : _tokens.StrokeHair,
            _tokens.RadiusMedium);
        var inset = (_tokens.TouchTarget - _tokens.ControlSmall) * 0.5f;
        style.ExpandMarginTop = -inset;
        style.ExpandMarginBottom = -inset;
        style.SetContentMarginAll(0);
        return style;
    }

    private StyleBoxFlat CreateFocusStyle()
    {
        var style = CreateStyle(selected: true);
        style.DrawCenter = false;
        style.BorderColor = _tokens.Halo;
        return style;
    }

    private static Color DisabledColor(Color color) => UiTokens.MultiplyAlpha(color, 0.5f);

    private sealed class TabSlot(Button button)
    {
        public Button Button { get; } = button;
        public StyleBoxFlat Disabled { get; set; } = new();
        public StyleBoxFlat DisabledSelected { get; set; } = new();
    }
}

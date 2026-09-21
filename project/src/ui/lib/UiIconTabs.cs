using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Icon tab strip with single active selection.</summary>
public partial class UiIconTabs : HBoxContainer
{
    [Signal]
    public delegate void TabSelectedEventHandler(string tabId, int index);

    private UiTokens _tokens = UiTokens.Neon;
    private TabItem[]? _pendingTabs;
    private TabItem[] _tabs = [];

    public readonly record struct TabItem(string Id, UiIconId IconId, string AccessibleLabel, bool Enabled = true);

    [Export]
    public string[] Glyphs
    {
        get => _glyphs;
        set
        {
            _glyphs = value ?? [];
            _pendingTabs = null;
            _tabs = CreateTabsFromGlyphs();
            _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
            Rebuild();
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
            Rebuild();
        }
    }

    private int _activeIndex;

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
        _tabs = _pendingTabs ?? CreateTabsFromGlyphs();
        _pendingTabs = null;
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
        Rebuild();
    }

    public void SetTabs(params TabItem[] tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        if (!IsInsideTree())
        {
            _pendingTabs = tabs;
            return;
        }

        _tabs = tabs;
        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
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

        AddThemeConstantOverride("separation", (int)_tokens.Space1);
        if (_tabs.Length == 0)
        {
            return;
        }

        _activeIndex = UiComponentContracts.NormalizeTabIndex(_activeIndex, _tabs.Length);
        var group = new ButtonGroup();
        for (var index = 0; index < _tabs.Length; index++)
        {
            var item = _tabs[index];
            var tabIndex = index;
            var selected = tabIndex == _activeIndex;
            var button = new Button
            {
                Text = string.Empty,
                TooltipText = string.IsNullOrWhiteSpace(item.AccessibleLabel) ? item.Id : item.AccessibleLabel,
                ToggleMode = true,
                ButtonPressed = selected,
                ButtonGroup = group,
                Disabled = !item.Enabled,
                CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget),
            };
            _tokens.ApplyTextStyle(button, _tokens.HeadingText);
            var iconColor = selected ? _tokens.OnAccent : _tokens.Ink;
            button.AddThemeColorOverride("font_color", iconColor);
            button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
            UiIcons.Apply(button, item.IconId, UiIconSize.Large, item.Enabled ? iconColor : _tokens.Muted);
            button.AddThemeStyleboxOverride("normal", CreateStyle(selected));
            button.AddThemeStyleboxOverride("hover", CreateStyle(selected, hovered: true));
            button.AddThemeStyleboxOverride("pressed", CreateStyle(true));
            button.AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
            button.AddThemeStyleboxOverride("disabled", CreateStyle(false, disabled: true));
            button.Pressed += () => SelectTab(tabIndex, item.Id);
            AddChild(button);
        }
    }

    private void SelectTab(int index, string id)
    {
        var normalized = UiComponentContracts.NormalizeTabIndex(index, _tabs.Length);
        if (normalized == _activeIndex)
        {
            return;
        }

        _activeIndex = normalized;
        Rebuild();
        EmitSignal(SignalName.TabSelected, id, normalized);
    }

    private TabItem[] CreateTabsFromGlyphs() =>
        Glyphs.Select((glyph, index) => new TabItem(
            index.ToString(),
            UiIconGlyphs.ParseOr(glyph, UiIconId.Select),
            glyph)).ToArray();

    private StyleBoxFlat CreateStyle(bool selected, bool hovered = false, bool disabled = false)
    {
        var background = selected
            ? _tokens.Accent
            : hovered ? _tokens.AccentSoft : _tokens.PanelRaised;
        var border = selected ? _tokens.Accent : _tokens.LineStrong;
        return _tokens.ControlStyle(
            disabled ? UiTokens.MultiplyAlpha(background, 0.5f) : background,
            disabled ? UiTokens.MultiplyAlpha(border, 0.5f) : border,
            selected ? _tokens.StrokeSignal : _tokens.StrokeHair);
    }
}

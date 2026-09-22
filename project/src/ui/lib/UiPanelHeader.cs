using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Side-panel title row with optional glyph and compact actions.</summary>
public partial class UiPanelHeader : HBoxContainer
{
    [Signal]
    public delegate void ActionSelectedEventHandler(string actionId);

    private UiTokens _tokens = UiTokens.Neon;
    private PanelAction[]? _pendingActions;
    private PanelAction[] _actionItems = [];
    private string[] _actions = ["close"];
    private string _glyph = "gear";
    private UiIconId _glyphIconId = UiIconId.Gear;

    public readonly record struct PanelAction(string Id, UiIconId IconId, string AccessibleLabel, UiComponentContracts.SemanticState State = UiComponentContracts.SemanticState.Neutral);

    [Export]
    public string Title { get; set; } = "Part settings";

    [Export]
    public string Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            if (UiIconGlyphs.TryParse(value, out var icon))
            {
                _glyphIconId = icon;
            }

            Rebuild();
        }
    }

    [Export]
    public UiIconId GlyphIconId
    {
        get => _glyphIconId;
        set
        {
            _glyphIconId = value;
            _glyph = value.ToString();
            Rebuild();
        }
    }

    [Export]
    public string[] Actions
    {
        get => _actions;
        set
        {
            _actions = value ?? [];
            _actionItems = CreateActionsFromGlyphs();
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
        _actionItems = _pendingActions ?? CreateActionsFromGlyphs();
        _pendingActions = null;
        Rebuild();
    }

    public void SetActions(params PanelAction[] actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (!IsInsideTree())
        {
            _pendingActions = actions;
            return;
        }

        _actionItems = actions;
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

        CustomMinimumSize = new Vector2(0, _tokens.ControlSmall);
        AddThemeConstantOverride("separation", (int)_tokens.Space2);
        if (!string.IsNullOrWhiteSpace(Glyph))
        {
            AddChild(UiFieldAndRows.Icon(GlyphIconId, UiIconSize.Standard, _tokens.Accent));
        }

        var title = UiFieldAndRows.Label(Title, _tokens, _tokens.SubheadingText, _tokens.Ink);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(title);
        foreach (var action in _actionItems.Take(2))
        {
            var button = new Button
            {
                Text = string.Empty,
                TooltipText = string.IsNullOrWhiteSpace(action.AccessibleLabel) ? action.Id : action.AccessibleLabel,
                CustomMinimumSize = new Vector2(_tokens.ControlSmall, _tokens.ControlSmall),
                Disabled = action.State is UiComponentContracts.SemanticState.Disabled or UiComponentContracts.SemanticState.Locked,
            };
            _tokens.ApplyTextStyle(button, _tokens.LabelText);
            var actionColor = ActionColor(action.State);
            button.AddThemeColorOverride("font_color", actionColor);
            button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
            UiIcons.Apply(button, action.IconId, UiIconSize.Standard, button.Disabled ? _tokens.Muted : actionColor);
            button.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.LineStrong));
            button.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent));
            button.AddThemeStyleboxOverride("pressed", _tokens.ControlStyle(_tokens.Accent, _tokens.Accent, _tokens.StrokeSignal));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeStyleboxOverride("disabled", _tokens.ControlStyle(UiTokens.MultiplyAlpha(_tokens.PanelRaised, 0.5f), _tokens.Line));
            var id = action.Id;
            button.Pressed += () => EmitSignal(SignalName.ActionSelected, id);
            AddChild(button);
        }
    }

    private PanelAction[] CreateActionsFromGlyphs() =>
        Actions.Select((icon, index) => new PanelAction(
            index.ToString(),
            UiIconGlyphs.ParseOr(icon, UiIconId.More),
            icon)).ToArray();

    private Color ActionColor(UiComponentContracts.SemanticState state) =>
        state is UiComponentContracts.SemanticState.Danger or UiComponentContracts.SemanticState.Bad
            ? _tokens.Danger
            : state == UiComponentContracts.SemanticState.Warning
                ? _tokens.Halo
                : _tokens.Muted;
}

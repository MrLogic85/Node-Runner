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
        MouseFilter = MouseFilterEnum.Pass;
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
        AddThemeConstantOverride("separation", (int)_tokens.Space1);
        if (!string.IsNullOrWhiteSpace(Glyph))
        {
            AddChild(UiFieldAndRows.Icon(GlyphIconId, UiIconSize.Standard, _tokens.Ink));
        }

        var title = UiFieldAndRows.Label(Title, _tokens, _tokens.SubheadingText, _tokens.Ink);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(title);
        foreach (var action in _actionItems.Take(2))
        {
            var button = new UiButton
            {
                Tokens = _tokens,
                Style = UiButtonStyle.Flat,
                ContentLayout = UiButtonContentLayout.Icon,
                Compact = true,
                IconId = action.IconId,
                Enabled = action.State is not UiComponentContracts.SemanticState.Disabled and not UiComponentContracts.SemanticState.Locked,
                TooltipText = string.IsNullOrWhiteSpace(action.AccessibleLabel) ? action.Id : action.AccessibleLabel,
                MouseFilter = MouseFilterEnum.Pass,
            };
            var id = action.Id;
            button.Activated += () => EmitSignal(SignalName.ActionSelected, id);
            AddChild(button);
        }
    }

    private PanelAction[] CreateActionsFromGlyphs() =>
        Actions.Select((icon, index) => new PanelAction(
            index.ToString(),
            UiIconGlyphs.ParseOr(icon, UiIconId.More),
            icon)).ToArray();

}

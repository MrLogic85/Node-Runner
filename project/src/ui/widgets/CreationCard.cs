using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

public partial class CreationCard : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private string _creationKey = string.Empty;
    private string _creationName = string.Empty;
    private string _summary = string.Empty;
    private string _note = string.Empty;
    private string _thumbnail = string.Empty;
    private string _savedState = string.Empty;
    private string _unlockCredit = string.Empty;
    private bool _canOpen;
    private bool _canEdit;
    private bool _canDuplicate;
    private bool _canDelete;

    [Signal]
    public delegate void OpenRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void EditRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationKey, string creationName);

    public void Setup(
        UiTokens tokens,
        string creationKey,
        string creationName,
        string summary,
        string note,
        string thumbnail,
        string savedState,
        string unlockCredit,
        bool canOpen,
        bool canEdit,
        bool canDuplicate,
        bool canDelete)
    {
        _tokens = tokens;
        _creationKey = creationKey;
        _creationName = creationName;
        _summary = summary;
        _note = note;
        _thumbnail = thumbnail;
        _savedState = savedState;
        _unlockCredit = unlockCredit;
        _canOpen = canOpen;
        _canEdit = canEdit;
        _canDuplicate = canDuplicate;
        _canDelete = canDelete;
        if (IsInsideTree())
        {
            Rebuild();
        }
    }

    public override void _Ready()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        CustomMinimumSize = new Vector2(280, 0);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var panel = new UiPanel
        {
            Tokens = _tokens,
            Raised = true,
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel(_creationName, 19, _tokens.Ink));
        stack.AddChild(CreateThumbnail(_thumbnail));
        stack.AddChild(CreateLabel(_summary, 14, _tokens.Muted));
        stack.AddChild(CreateLabel(_savedState, 13, _tokens.Accent));
        if (!string.IsNullOrWhiteSpace(_unlockCredit))
        {
            stack.AddChild(CreateLabel(_unlockCredit, 13, _tokens.Accent));
        }

        stack.AddChild(CreateLabel(_note, 14, _tokens.Accent));
        stack.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        var openButton = CreateButton("Open", UiActionButton.ActionKind.Primary);
        openButton.Locked = !_canOpen;
        openButton.Pressed += () => EmitSignal(SignalName.OpenRequested, _creationKey, _creationName);
        actions.AddChild(openButton);
        var editButton = CreateButton("Edit", UiActionButton.ActionKind.Secondary);
        editButton.Locked = !_canEdit;
        editButton.Pressed += () => EmitSignal(SignalName.EditRequested, _creationKey, _creationName);
        actions.AddChild(editButton);
        stack.AddChild(actions);

        var safeActions = new HBoxContainer();
        safeActions.AddThemeConstantOverride("separation", 8);
        var duplicateButton = CreateButton("Duplicate", UiActionButton.ActionKind.Secondary);
        duplicateButton.Locked = !_canDuplicate;
        duplicateButton.Pressed += () => EmitSignal(SignalName.DuplicateRequested, _creationKey, _creationName);
        safeActions.AddChild(duplicateButton);
        var deleteButton = CreateButton("Delete", UiActionButton.ActionKind.Danger);
        deleteButton.Locked = !_canDelete;
        deleteButton.Pressed += () => EmitSignal(SignalName.DeleteRequested, _creationKey, _creationName);
        safeActions.AddChild(deleteButton);
        stack.AddChild(safeActions);
    }

    private Control CreateThumbnail(string text)
    {
        var panel = new Panel
        {
            CustomMinimumSize = new Vector2(0, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _tokens.AccentSoft,
            BorderColor = _tokens.LineStrong,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
        });

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var label = CreateLabel(text, 13, _tokens.Ink);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        margin.AddChild(label);
        return panel;
    }

    private Label CreateLabel(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.Fill,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private UiActionButton CreateButton(string text, UiActionButton.ActionKind kind) =>
        new()
        {
            Tokens = _tokens,
            LabelText = text,
            Kind = kind,
            CustomMinimumSize = new Vector2(96, _tokens.TouchTarget),
        };
}

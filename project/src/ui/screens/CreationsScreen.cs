using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>Sample-data Creations workspace; no persistence or game-state access.</summary>
public partial class CreationsScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;

    [Signal]
    public delegate void OpenRequestedEventHandler(string creationName);

    [Signal]
    public delegate void EditRequestedEventHandler(string creationName);

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationName);

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            if (IsInsideTree())
            {
                Rebuild();
            }
        }
    }

    public override void _Ready()
    {
        Name = nameof(CreationsScreen);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Size = GetViewportRect().Size;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        AddChild(new ColorRect
        {
            Color = _tokens.Background,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 16);
        margin.AddChild(layout);

        var header = new HBoxContainer();
        header.AddChild(CreateLabel("Creations", 24, _tokens.Ink, true));
        var back = CreateButton("Back", UiActionButton.ActionKind.Secondary);
        back.Pressed += () => EmitSignal(SignalName.BackRequested);
        header.AddChild(back);
        layout.AddChild(header);
        layout.AddChild(CreateLabel("Saved creatures you can resume, edit, or duplicate.", 14, _tokens.Muted));

        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 14);
        cards.SizeFlagsVertical = SizeFlags.ExpandFill;
        cards.AddChild(CreateCreationCard(
            "First Walker",
            "Generation 18 · best 42.6 m",
            "2 cores unlocked",
            () => EmitSignal(SignalName.OpenRequested, "First Walker"),
            () => EmitSignal(SignalName.EditRequested, "First Walker"),
            () => EmitSignal(SignalName.DuplicateRequested, "First Walker"),
            () => EmitSignal(SignalName.DeleteRequested, "First Walker")));
        cards.AddChild(CreateCreationCard(
            "Triangle Study",
            "Generation 7 · best 12.8 m",
            "Training copied",
            () => EmitSignal(SignalName.OpenRequested, "Triangle Study"),
            () => EmitSignal(SignalName.EditRequested, "Triangle Study"),
            () => EmitSignal(SignalName.DuplicateRequested, "Triangle Study"),
            () => EmitSignal(SignalName.DeleteRequested, "Triangle Study")));
        layout.AddChild(cards);
    }

    private Control CreateCreationCard(string title, string summary, string note, Action open, Action edit, Action duplicate, Action delete)
    {
        var card = new UiPanel
        {
            Tokens = _tokens,
            Raised = true,
            CustomMinimumSize = new Vector2(280, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        card.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel(title, 19, _tokens.Ink));
        stack.AddChild(CreateLabel(summary, 14, _tokens.Muted));
        stack.AddChild(CreateLabel(note, 14, _tokens.Accent));
        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        stack.AddChild(spacer);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        var openButton = CreateButton("Open", UiActionButton.ActionKind.Primary);
        openButton.Pressed += open;
        actions.AddChild(openButton);
        var editButton = CreateButton("Edit", UiActionButton.ActionKind.Secondary);
        editButton.Pressed += edit;
        actions.AddChild(editButton);
        stack.AddChild(actions);
        var safeActions = new HBoxContainer();
        safeActions.AddThemeConstantOverride("separation", 8);
        var duplicateButton = CreateButton("Duplicate", UiActionButton.ActionKind.Secondary);
        duplicateButton.Pressed += duplicate;
        safeActions.AddChild(duplicateButton);
        var deleteButton = CreateButton("Delete", UiActionButton.ActionKind.Danger);
        deleteButton.Pressed += delete;
        safeActions.AddChild(deleteButton);
        stack.AddChild(safeActions);
        return card;
    }

    private Label CreateLabel(string text, int size, Color color, bool expand = false)
    {
        var label = new Label { Text = text };
        label.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill;
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private UiActionButton CreateButton(string text, UiActionButton.ActionKind kind)
    {
        return new UiActionButton
        {
            Tokens = _tokens,
            LabelText = text,
            Kind = kind,
            CustomMinimumSize = new Vector2(96, _tokens.TouchTarget),
        };
    }
}

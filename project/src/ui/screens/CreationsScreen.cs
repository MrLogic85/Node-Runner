using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>Creations workspace; receives presentation state from the host.</summary>
public partial class CreationsScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private CreationsPresentationViewModel? _presentation;
    private bool _subscribedToPresentation;

    [Signal]
    public delegate void OpenRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void EditRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationKey, string creationName);

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

    public void Setup(CreationsPresentationViewModel presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (_presentation is not null)
        {
            UnsubscribeFromPresentation();
        }

        _presentation = presentation;
        if (IsInsideTree())
        {
            SubscribeToPresentation();
            Rebuild();
        }
    }

    public override void _EnterTree()
    {
        SubscribeToPresentation();
    }

    public override void _Ready()
    {
        Name = nameof(CreationsScreen);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Size = GetViewportRect().Size;
        Rebuild();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromPresentation();
    }

    private void SubscribeToPresentation()
    {
        if (_presentation is null || _subscribedToPresentation)
        {
            return;
        }

        _presentation.PropertyChanged += OnPresentationChanged;
        _subscribedToPresentation = true;
    }

    private void UnsubscribeFromPresentation()
    {
        if (_presentation is null || !_subscribedToPresentation)
        {
            return;
        }

        _presentation.PropertyChanged -= OnPresentationChanged;
        _subscribedToPresentation = false;
    }

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (IsInsideTree())
        {
            Rebuild();
        }
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

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var cards = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        cards.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(cards);
        if (_presentation is null)
        {
            AddSampleCards(cards);
        }
        else if (_presentation.HasError)
        {
            cards.AddChild(CreateLabel(_presentation.ErrorText ?? "Could not load Creations.", 16, _tokens.Danger));
        }
        else if (!_presentation.HasCards)
        {
            cards.AddChild(CreateLabel(_presentation.EmptyText, 16, _tokens.Muted));
        }
        else
        {
            foreach (var creation in _presentation.Cards)
            {
                cards.AddChild(CreateCreationCard(creation));
            }
        }

        layout.AddChild(scroll);
    }

    private void AddSampleCards(HBoxContainer cards)
    {
        cards.AddChild(CreateCreationCard(
            "First Walker",
            "Generation 18 · best 42.6 m",
            "2 cores unlocked",
            () => EmitSignal(SignalName.OpenRequested, "sample:first-walker", "First Walker"),
            () => EmitSignal(SignalName.EditRequested, "sample:first-walker", "First Walker"),
            () => EmitSignal(SignalName.DuplicateRequested, "sample:first-walker", "First Walker"),
            () => EmitSignal(SignalName.DeleteRequested, "sample:first-walker", "First Walker"),
            canOpen: true,
            canEdit: true,
            canDuplicate: true,
            canDelete: true));
        cards.AddChild(CreateCreationCard(
            "Triangle Study",
            "Generation 7 · best 12.8 m",
            "Training copied",
            () => EmitSignal(SignalName.OpenRequested, "sample:triangle-study", "Triangle Study"),
            () => EmitSignal(SignalName.EditRequested, "sample:triangle-study", "Triangle Study"),
            () => EmitSignal(SignalName.DuplicateRequested, "sample:triangle-study", "Triangle Study"),
            () => EmitSignal(SignalName.DeleteRequested, "sample:triangle-study", "Triangle Study"),
            canOpen: true,
            canEdit: true,
            canDuplicate: true,
            canDelete: true));
    }

    private Control CreateCreationCard(CreationCardPresentation creation) =>
        CreateCreationCard(
            creation.Name,
            creation.SummaryText,
            creation.NoteText,
            () => EmitPresentationIntent(CreationCommandKind.Open, creation.Id, creation.Name),
            () => EmitPresentationIntent(CreationCommandKind.Edit, creation.Id, creation.Name),
            () => EmitPresentationIntent(CreationCommandKind.Duplicate, creation.Id, creation.Name),
            () => EmitPresentationIntent(CreationCommandKind.Delete, creation.Id, creation.Name),
            canOpen: creation.CanOpen,
            canEdit: creation.CanEdit,
            canDuplicate: creation.CanDuplicate,
            canDelete: creation.CanDelete);

    private void EmitPresentationIntent(CreationCommandKind kind, Guid id, string name)
    {
        if (_presentation is null)
        {
            return;
        }

        var intent = kind switch
        {
            CreationCommandKind.Open => _presentation.RequestOpen(id),
            CreationCommandKind.Edit => _presentation.RequestEdit(id),
            CreationCommandKind.Duplicate => _presentation.RequestDuplicate(id),
            CreationCommandKind.Delete => _presentation.RequestDelete(id),
            _ => null,
        };
        if (intent is null)
        {
            return;
        }

        var creationId = intent.CreationId.ToString("D");
        switch (intent.Kind)
        {
            case CreationCommandKind.Open:
                EmitSignal(SignalName.OpenRequested, creationId, name);
                break;
            case CreationCommandKind.Edit:
                EmitSignal(SignalName.EditRequested, creationId, name);
                break;
            case CreationCommandKind.Duplicate:
                EmitSignal(SignalName.DuplicateRequested, creationId, name);
                break;
            case CreationCommandKind.Delete:
                EmitSignal(SignalName.DeleteRequested, creationId, name);
                break;
        }
    }

    private Control CreateCreationCard(string title, string summary, string note, Action open, Action edit, Action duplicate, Action delete, bool canOpen, bool canEdit, bool canDuplicate, bool canDelete)
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
        openButton.Locked = !canOpen;
        openButton.Pressed += open;
        actions.AddChild(openButton);
        var editButton = CreateButton("Edit", UiActionButton.ActionKind.Secondary);
        editButton.Locked = !canEdit;
        editButton.Pressed += edit;
        actions.AddChild(editButton);
        stack.AddChild(actions);
        var safeActions = new HBoxContainer();
        safeActions.AddThemeConstantOverride("separation", 8);
        var duplicateButton = CreateButton("Duplicate", UiActionButton.ActionKind.Secondary);
        duplicateButton.Locked = !canDuplicate;
        duplicateButton.Pressed += duplicate;
        safeActions.AddChild(duplicateButton);
        var deleteButton = CreateButton("Delete", UiActionButton.ActionKind.Danger);
        deleteButton.Locked = !canDelete;
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

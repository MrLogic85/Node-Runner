using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Reference Creations home hub. The layout is authored in <c>scenes/screens/CreationsScreen.tscn</c>;
/// this script binds the host's presentation state and forwards intents.
/// </summary>
public partial class CreationsScreen : Control
{
    private CreationsPresentationViewModel? _presentation;
    private bool _subscribedToPresentation;

    [Signal]
    public delegate void OpenRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void NewRequestedEventHandler();

    [Signal]
    public delegate void AchievementsRequestedEventHandler();

    [Signal]
    public delegate void ExamplesRequestedEventHandler();

    [Signal]
    public delegate void ComponentLibraryRequestedEventHandler();

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationKey, string creationName);

    private bool _showComponentLibraryLink;

    /// <summary>The card the screen instances once per saved creation.</summary>
    [Export]
    public PackedScene? CardScene { get; set; }

    [Export]
    public bool ShowComponentLibraryLink
    {
        get => _showComponentLibraryLink;
        set
        {
            _showComponentLibraryLink = value;
            if (IsNodeReady())
            {
                ApplyDebugLinks();
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
        }

        if (IsNodeReady())
        {
            Apply();
        }
    }

    public override void _EnterTree()
    {
        SubscribeToPresentation();
    }

    public override void _Ready()
    {
        UiLayout.ApplyScreen(this);
        var toolbar = GetNode<UiToolbar>("%Toolbar");
        GetNode<UiButton>("%Achievements").Activated += () => EmitSignal(SignalName.AchievementsRequested);
        GetNode<UiButton>("%New").Activated += () => EmitSignal(SignalName.NewRequested);
        GetNode<UiButton>("%EmptyNew").Activated += () => EmitSignal(SignalName.NewRequested);
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuExamples"), SignalName.ExamplesRequested);
        BindMenuItem(toolbar, GetNode<UiMenuActionItem>("%MenuComponentLibrary"), SignalName.ComponentLibraryRequested);
        ApplyDebugLinks();
        Apply();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromPresentation();
    }

    private void BindMenuItem(UiToolbar toolbar, UiMenuActionItem item, StringName signal) =>
        item.Activated += () =>
        {
            toolbar.CloseMenu();
            EmitSignal(signal);
        };

    private void ApplyDebugLinks()
    {
        GetNode<UiMenuItemDivider>("%MenuDebugDivider").Visible = _showComponentLibraryLink;
        GetNode<UiMenuActionItem>("%MenuComponentLibrary").Visible = _showComponentLibraryLink;
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
        if (IsNodeReady())
        {
            Apply();
        }
    }

    private void Apply()
    {
        var presentation = _presentation;
        if (presentation is not null)
        {
            GetNode<UiLabel>("%EmptyText").Text = presentation.EmptyText;
        }

        GetNode<UiButton>("%Achievements").BadgeText = presentation?.AchievementBadgeText ?? string.Empty;

        var row = GetNode<Control>("%CardRow");
        foreach (var card in row.GetChildren().OfType<CreationCard>())
        {
            row.RemoveChild(card);
            card.QueueFree();
        }

        var error = GetNode<UiLabel>("%ErrorState");
        error.Visible = presentation?.HasError == true;
        error.Text = presentation?.ErrorText ?? error.Text;
        GetNode<UiCard>("%EmptyCard").Visible = presentation is null || (!presentation.HasError && !presentation.HasCards);
        if (presentation is null || presentation.HasError || CardScene is null)
        {
            return;
        }

        foreach (var creation in presentation.Cards)
        {
            var card = CardScene.Instantiate<CreationCard>();
            card.Bind(creation);
            card.OpenRequested += (key, name) => EmitCardIntent(CreationCommandKind.Open, key, name);
            card.DuplicateRequested += (key, name) => EmitCardIntent(CreationCommandKind.Duplicate, key, name);
            card.DeleteRequested += (key, name) => EmitCardIntent(CreationCommandKind.Delete, key, name);
            row.AddChild(card);
            UiNativeScroll.AllowGesturesToBubble(card);
        }
    }

    private void EmitCardIntent(CreationCommandKind kind, string creationKey, string creationName)
    {
        if (Guid.TryParse(creationKey, out var id))
        {
            EmitPresentationIntent(kind, id, creationName);
        }
    }

    private void EmitPresentationIntent(CreationCommandKind kind, Guid id, string name)
    {
        if (_presentation is null)
        {
            return;
        }

        var intent = kind switch
        {
            CreationCommandKind.Open => _presentation.RequestOpen(id),
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
            case CreationCommandKind.Duplicate:
                EmitSignal(SignalName.DuplicateRequested, creationId, name);
                break;
            case CreationCommandKind.Delete:
                EmitSignal(SignalName.DeleteRequested, creationId, name);
                break;
        }
    }
}

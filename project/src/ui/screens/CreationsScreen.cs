using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Screens;

/// <summary>Reference Creations home hub; receives presentation state from the host.</summary>
public partial class CreationsScreen : Control
{
    private CreationsPresentationViewModel? _presentation;
    private bool _subscribedToPresentation;

    [Signal]
    public delegate void OpenRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void EditRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void NewRequestedEventHandler();

    [Signal]
    public delegate void AchievementsRequestedEventHandler();

    [Signal]
    public delegate void RestoreExampleRequestedEventHandler();

    [Signal]
    public delegate void ComponentLibraryRequestedEventHandler();

    [Signal]
    public delegate void ColorsAndStylesRequestedEventHandler();

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationKey, string creationName);

    private bool _showComponentLibraryLink;

    [Export]
    public bool ShowComponentLibraryLink
    {
        get => _showComponentLibraryLink;
        set
        {
            if (_showComponentLibraryLink == value)
            {
                return;
            }

            _showComponentLibraryLink = value;
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
        UiLayout.ApplyScreen(this);
        Rebuild();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Rebuild);
        }
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
            Color = UiThemeLookup.Color(this, UiTokens.Color.Background),
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        UiSpacing.ApplyUniformMargin(margin, UiSpacing.ScreenEdgeInset);
        AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(layout);
        layout.AddChild(CreateTopBar());

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        var cards = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        cards.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        scroll.AddChild(cards);

        if (_presentation is null)
        {
            AddSampleCards(cards);
        }
        else if (_presentation.HasError)
        {
            cards.AddChild(CreateStatusLabel(_presentation.ErrorText ?? "Could not load Creations.", UiTokens.Color.Danger));
        }
        else if (!_presentation.HasCards)
        {
            cards.AddChild(CreateStatusLabel(_presentation.EmptyText, UiTokens.Color.Muted));
        }
        else
        {
            foreach (var creation in _presentation.Cards)
            {
                cards.AddChild(CreateCreationCard(creation));
            }
        }

        layout.AddChild(scroll);
        AddChild(CreateOverflowMenu());
    }

    private Control CreateTopBar()
    {
        var topBar = new UiCard
        {
            Kind = UiCard.CardVariant.Frame,
            CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S3);
        margin.AddThemeConstantOverride("margin_top", (int)UiSize.Space.S1);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S1);
        margin.AddThemeConstantOverride("margin_bottom", (int)UiSize.Space.S1);
        topBar.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(row);

        var titleStack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        titleStack.AddThemeConstantOverride("separation", 0);
        titleStack.AddChild(CreateStyledLabel("Creations", UiTokens.Typography.Heading, UiTokens.Color.Ink));
        titleStack.AddChild(CreateStyledLabel(_presentation?.SavedCueText ?? "Saved", UiTokens.Typography.Caption, UiTokens.Color.Accent));
        row.AddChild(titleStack);

        row.AddChild(CreateAchievementButton());

        var newButton = CreateButton("+ New", UiButtonKind.Primary);
        newButton.CustomMinimumSize = new Vector2(104, UiSize.Control.Touch);
        newButton.Pressed += () => EmitSignal(SignalName.NewRequested);
        row.AddChild(newButton);

        var overflowButton = new UiButton
        {
            ContentLayout = UiButtonContentLayout.Stacked,
            IconId = UiIconId.More,
            TooltipText = "More",
        };
        overflowButton.Pressed += ToggleOverflowMenu;
        row.AddChild(overflowButton);
        return topBar;
    }

    private Control CreateAchievementButton()
    {
        var holder = new Control
        {
            CustomMinimumSize = new Vector2(UiSize.Control.Touch, UiSize.Control.Touch),
        };
        var trophy = new UiButton
        {
            ContentLayout = UiButtonContentLayout.Stacked,
            IconId = UiIconId.Trophy,
            TooltipText = "Achievements",
        };
        trophy.Pressed += () => EmitSignal(SignalName.AchievementsRequested);
        holder.AddChild(trophy);

        if (_presentation?.HasAchievementCue == true)
        {
            var badge = new PanelContainer
            {
                CustomMinimumSize = new Vector2(14, 14),
                Position = new Vector2(31, 4),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            badge.AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Danger), UiThemeLookup.Color(this, UiTokens.Color.Danger), radius: (int)UiSize.Radius.Pill));
            holder.AddChild(badge);
        }

        return holder;
    }

    private UiMenu CreateOverflowMenu()
    {
        var menu = new UiMenu
        {
            Name = "CreationsOverflow",
            Position = new Vector2(UiLayout.CanvasWidth - 196, UiLayout.TopBarHeight + (UiSpacing.ScreenEdgeInset * 2)),
        };
        var actions = new List<(string Id, UiMenuItemSpec Item)>
        {
            ("close", new("Close Creations")),
            ("restore-example", new("Restore example")),
        };
        if (ShowComponentLibraryLink)
        {
            actions.Add(("colors-and-styles", new("Colors and styles")));
            actions.Add(("component-library", new("Component library")));
        }

        UiMenuItems.Populate(menu, actions.Select(action => action.Item));
        menu.IndexClicked += index =>
        {
            var id = index >= 0 && index < actions.Count ? actions[index].Id : null;
            menu.Hide();
            switch (id)
            {
                case "close":
                    EmitSignal(SignalName.BackRequested);
                    break;
                case "restore-example":
                    EmitSignal(SignalName.RestoreExampleRequested);
                    break;
                case "component-library":
                    EmitSignal(SignalName.ComponentLibraryRequested);
                    break;
                case "colors-and-styles":
                    EmitSignal(SignalName.ColorsAndStylesRequested);
                    break;
            }
        };
        menu.Visible = false;
        return menu;
    }

    private void ToggleOverflowMenu()
    {
        if (GetNodeOrNull<UiMenu>("CreationsOverflow") is { } menu)
        {
            menu.Visible = !menu.Visible;
        }
    }

    private void AddSampleCards(HBoxContainer cards)
    {
        cards.AddChild(CreateCreationCard(
            "sample:first-walker",
            "First Walker",
            null,
            "Generation 18 · best 42.6 m",
            "2 cores unlocked",
            "3 nodes · 2 beams · 1 core",
            "Saved training · generation 18",
            "Earned extra core unlock · generation 18",
            1f,
            "Achievement complete",
            isExample: false,
            canOpen: true,
            canEdit: true,
            canDuplicate: true,
            canDelete: true));
        cards.AddChild(CreateCreationCard(
            "sample:starter-worm",
            "Example: Worm",
            null,
            "Ready to train",
            "Example",
            "5 nodes · 4 beams · 1 core",
            "Untrained Creation",
            string.Empty,
            0f,
            string.Empty,
            isExample: true,
            canOpen: true,
            canEdit: true,
            canDuplicate: true,
            canDelete: false));
    }

    private Control CreateCreationCard(CreationCardPresentation creation) =>
        CreateCreationCard(
            creation.Id.ToString("D"),
            creation.Name,
            creation.Creature,
            creation.SummaryText,
            creation.NoteText,
            creation.ThumbnailText,
            creation.SavedStateText,
            creation.UnlockCreditText,
            creation.AchievementProgress,
            creation.AchievementProgressText,
            creation.IsExample,
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

    private Control CreateCreationCard(
        string creationKey,
        string title,
        CreatureDef? creature,
        string summary,
        string note,
        string thumbnail,
        string savedState,
        string unlockCredit,
        float achievementProgress,
        string achievementProgressText,
        bool isExample,
        bool canOpen,
        bool canEdit,
        bool canDuplicate,
        bool canDelete)
    {
        var card = new CreationCard();
        card.Setup(
            creationKey,
            title,
            creature,
            summary,
            note,
            thumbnail,
            savedState,
            unlockCredit,
            achievementProgress,
            achievementProgressText,
            isExample,
            canOpen,
            canEdit,
            canDuplicate,
            canDelete);
        card.OpenRequested += HandleCardOpenRequested;
        card.EditRequested += HandleCardEditRequested;
        card.DuplicateRequested += HandleCardDuplicateRequested;
        card.DeleteRequested += HandleCardDeleteRequested;
        return card;
    }

    private void HandleCardOpenRequested(string creationKey, string creationName)
    {
        if (_presentation is null)
        {
            EmitSignal(SignalName.OpenRequested, creationKey, creationName);
            return;
        }

        if (Guid.TryParse(creationKey, out var id))
        {
            EmitPresentationIntent(CreationCommandKind.Open, id, creationName);
        }
    }

    private void HandleCardEditRequested(string creationKey, string creationName)
    {
        if (_presentation is null)
        {
            EmitSignal(SignalName.EditRequested, creationKey, creationName);
            return;
        }

        if (Guid.TryParse(creationKey, out var id))
        {
            EmitPresentationIntent(CreationCommandKind.Edit, id, creationName);
        }
    }

    private void HandleCardDuplicateRequested(string creationKey, string creationName)
    {
        if (_presentation is null)
        {
            EmitSignal(SignalName.DuplicateRequested, creationKey, creationName);
            return;
        }

        if (Guid.TryParse(creationKey, out var id))
        {
            EmitPresentationIntent(CreationCommandKind.Duplicate, id, creationName);
        }
    }

    private void HandleCardDeleteRequested(string creationKey, string creationName)
    {
        if (_presentation is null)
        {
            EmitSignal(SignalName.DeleteRequested, creationKey, creationName);
            return;
        }

        if (Guid.TryParse(creationKey, out var id))
        {
            EmitPresentationIntent(CreationCommandKind.Delete, id, creationName);
        }
    }

    private Label CreateStatusLabel(string text, UiTokens.Color color)
    {
        var label = CreateStyledLabel(text, UiTokens.Typography.Body, color, expand: true);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private Label CreateStyledLabel(string text, UiTokens.Typography style, UiTokens.Color color, bool expand = false)
    {
        var label = new Label { Text = text };
        label.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill;
        UiThemeLookup.ApplyTextStyle(label, style, color);
        return label;
    }

    private UiButton CreateButton(string text, UiButtonKind kind) =>
        new()
        {
            Text = text,
            Kind = kind,
            CustomMinimumSize = new Vector2(96, UiSize.Control.Touch),
        };
}

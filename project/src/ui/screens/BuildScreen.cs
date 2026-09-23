using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Build shell using sample presentation data by default, or injected App
/// presentation state when hosted by the live game. This scene does not bind
/// to simulation, managers, or persistence.
/// </summary>
public partial class BuildScreen : Control
{
    private const string _hostedInputPassthroughMeta = "HostedInputPassthrough";
    private const int _modeSwitchHeight = 52;
    private const int _toolButtonHeight = 56;
    private UiTokens _tokens = UiTokens.Neon;
    private ConstructionPresentationViewModel? _presentation;
    private bool _isSubscribedToPresentation;
    private bool _partsTrayCollapsed;
    private bool _brainSetupOpen;
    private bool _nameEntryOpen;
    private bool _creationOverflowOpen;

    [Export]
    public bool ShowTopBar { get; set; } = true;

    [Export]
    public bool Hosted { get; set; }

    [Export]
    public bool ShowCanvasPreview { get; set; } = true;

    [Signal]
    public delegate void TrainingRequestedEventHandler();

    [Signal]
    public delegate void SaveRequestedEventHandler();

    [Signal]
    public delegate void RebuildRequestedEventHandler();

    [Signal]
    public delegate void SimulateRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void CreationNameChangedEventHandler(string name);

    [Signal]
    public delegate void ResetTrainingRequestedEventHandler();

    [Signal]
    public delegate void DeleteCreationRequestedEventHandler();

    [Signal]
    public delegate void ClearSelectionRequestedEventHandler();

    [Signal]
    public delegate void DeleteSelectionRequestedEventHandler();

    [Signal]
    public delegate void ResumeTrainingRequestedEventHandler();

    [Signal]
    public delegate void StatsRequestedEventHandler();

    [Signal]
    public delegate void BrainRequestedEventHandler();

    [Signal]
    public delegate void ToolRequestedEventHandler(ConstructionTool tool);

    [Signal]
    public delegate void BrainShapeChangedEventHandler(int hiddenLayers, int neuronsPerLayer);

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public ConstructionPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            UnsubscribeFromPresentation();
            _presentation = value;

            if (IsInsideTree())
            {
                SubscribeToPresentation();
                RebuildLayout();
            }
        }
    }

    public override void _EnterTree()
    {
        SubscribeToPresentation();
    }

    public override void _Ready()
    {
        Name = nameof(BuildScreen);
        MouseFilter = MouseFilterEnum.Ignore;
        UiLayout.ApplyScreen(this);
        if (!Hosted)
        {
            Size = GetViewportRect().Size;
        }
        RebuildLayout();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromPresentation();
    }

    private void RebuildLayout()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
        if (_brainSetupOpen)
        {
            AddChild(CreateBrainSetupOverlay());
        }

        if (_nameEntryOpen)
        {
            AddChild(CreateNameEntryOverlay());
        }

        if (_creationOverflowOpen)
        {
            AddChild(CreateCreationOverflowOverlay());
        }

        if (Hosted)
        {
            ApplyHostedInputPassthrough(this);
        }
    }

    private void OnPresentationChanged(object? sender, EventArgs eventArgs)
    {
        if (IsInsideTree())
        {
            RebuildLayout();
        }
    }

    private void SubscribeToPresentation()
    {
        if (_presentation is null || _isSubscribedToPresentation)
        {
            return;
        }

        _presentation.PresentationChanged += OnPresentationChanged;
        _isSubscribedToPresentation = true;
    }

    private void UnsubscribeFromPresentation()
    {
        if (_presentation is null || !_isSubscribedToPresentation)
        {
            return;
        }

        _presentation.PresentationChanged -= OnPresentationChanged;
        _isSubscribedToPresentation = false;
    }

    private void BuildLayout()
    {
        if (!Hosted)
        {
            AddChild(new ColorRect
            {
                Color = _tokens.Background,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorRight = 1,
                AnchorBottom = 1,
            });
        }

        var safeFrame = MarkHostedInputPassthrough(CreateMargin(UiSpacing.ScreenEdgeInset(_tokens)));
        AddChild(safeFrame);

        var screenParent = safeFrame;
        if (!Hosted)
        {
            var frame = CreatePanel(raised: false);
            frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            safeFrame.AddChild(frame);

            var screenMargin = MarkHostedInputPassthrough(CreateMargin(0));
            frame.AddChild(screenMargin);
            screenParent = screenMargin;
        }

        var screen = MarkHostedInputPassthrough(new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        screen.AddThemeConstantOverride("separation", 0);
        screenParent.AddChild(screen);

        if (ShowTopBar)
        {
            screen.AddChild(CreateTopBar());
        }

        var contentRow = MarkHostedInputPassthrough(new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        contentRow.AddThemeConstantOverride("separation", 0);
        screen.AddChild(contentRow);

        contentRow.AddChild(CreateToolRail());
        contentRow.AddChild(CreateBuildCanvasPanel());
        contentRow.AddChild(CreateBrainPanel());
    }

    private Control CreateTopBar()
    {
        var topBarPanel = CreatePanel(raised: true);
        topBarPanel.CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight);
        topBarPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_top", 0);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_bottom", 0);
        topBarPanel.AddChild(margin);

        var topBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight),
        };
        topBar.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(topBar);

        var back = new UiSecondaryIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.Back,
            AccessibleLabel = "Back",
        };
        back.Pressed += () => EmitSignal(SignalName.BackRequested);
        topBar.AddChild(back);

        var titleStack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        titleStack.AddThemeConstantOverride("separation", 0);
        topBar.AddChild(titleStack);
        var title = new Button
        {
            Text = Presentation?.CreationName ?? "Untitled Creation",
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28),
        };
        _tokens.ApplyTextStyle(title, _tokens.HeadingText);
        UiIcons.Apply(title, UiIconId.Edit, UiIconSize.Small, _tokens.Ink);
        title.AddThemeColorOverride("font_color", _tokens.Ink);
        title.Pressed += () =>
        {
            _nameEntryOpen = true;
            RebuildLayout();
        };
        titleStack.AddChild(title);
        titleStack.AddChild(CreateLabel(Presentation?.CreationSubtitle ?? "Unsaved anatomy draft", 11, _tokens.Muted));

        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        topBar.AddChild(CreateBrainChip(buildPanel));
        if (Presentation?.ShowCompleteAction != false)
        {
            var save = CreateButton("Save", buildPanel.CanCompleteCreation ? UiActionButton.ActionKind.Primary : UiActionButton.ActionKind.Secondary, buildPanel.DisabledReason ?? "Save this Creation");
            save.CustomMinimumSize = new Vector2(88, _tokens.TouchTarget);
            save.Locked = !buildPanel.CanCompleteCreation;
            save.ShowLockReasonInText = false;
            if (buildPanel.DisabledReason is not null)
            {
                save.LockReason = buildPanel.DisabledReason;
            }
            if (buildPanel.CanCompleteCreation)
            {
                save.Pressed += () => EmitSignal(SignalName.SaveRequested);
            }
            topBar.AddChild(save);
        }
        var overflow = new UiSecondaryIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.More,
            AccessibleLabel = Presentation?.ShowCompleteAction == false ? "Reset training or delete creation" : "More build actions",
        };
        overflow.Pressed += () =>
        {
            _creationOverflowOpen = true;
            RebuildLayout();
        };
        topBar.AddChild(overflow);

        return topBarPanel;
    }

    private Control CreateBrainChip(ConstructionBuildPanelPresentation buildPanel)
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var locked = Presentation?.IsBrainShapeLocked ?? false;
        var chip = new Button
        {
            Text = locked ? $"Brain {shape.HiddenLayers} × {shape.NeuronsPerLayer} locked" : $"Brain {shape.HiddenLayers} × {shape.NeuronsPerLayer}",
            CustomMinimumSize = new Vector2(112, 32),
            TooltipText = locked ? "Brain shape is locked after Save" : $"{buildPanel.InputCount} senses · {buildPanel.OutputCount} motors",
            Disabled = locked,
        };
        _tokens.ApplyTextStyle(chip, _tokens.LabelText);
        chip.AddThemeColorOverride("font_color", _tokens.Accent);
        chip.AddThemeColorOverride("font_hover_color", _tokens.Ink);
        chip.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.Accent, radius: _tokens.RadiusPill));
        chip.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent, radius: _tokens.RadiusPill));
        if (!locked)
        {
            chip.Pressed += () =>
            {
                _brainSetupOpen = true;
                RebuildLayout();
            };
        }
        return chip;
    }

    private Control CreateToolRail()
    {
        var presentation = Presentation;
        var panel = CreatePanel(raised: true);
        panel.CustomMinimumSize = new Vector2(UiLayout.LeftRailWidth, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(0);
        panel.AddChild(margin);

        var rail = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rail.AddThemeConstantOverride("separation", 2);
        margin.AddChild(rail);

        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Place,
            "Move",
            ToolButtonKind(ConstructionTool.Place),
            presentation is null ? "Move sample nodes" : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Place),
            locked: false));
        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Beam,
            presentation?.BeamToolText ?? "Beam",
            ToolButtonKind(ConstructionTool.Beam),
            presentation is null
                ? "Connect two sample nodes"
                : presentation.LockTopologyTools
                    ? presentation.MoveOnlyLockReason
                    : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Beam),
            presentation?.LockTopologyTools ?? false));
        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Select,
            presentation?.SelectToolText ?? "Select",
            ToolButtonKind(ConstructionTool.Select),
            presentation is null ? "Select parts" : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Select),
            locked: false));
        rail.AddChild(CreateSpacer());

        return panel;
    }

    private static string? CompactCoreToolText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var hintStart = text.IndexOf(" (", StringComparison.Ordinal);
        return hintStart < 0 ? text : text[..hintStart];
    }

    private UiActionButton.ActionKind ToolButtonKind(ConstructionTool tool) =>
        Presentation?.ActiveTool == tool ? UiActionButton.ActionKind.Primary : UiActionButton.ActionKind.Secondary;

    private Button CreateBuildToolButton(ConstructionTool tool, string label, UiActionButton.ActionKind kind, string tooltip, bool locked)
    {
        var active = kind == UiActionButton.ActionKind.Primary;
        var button = new Button
        {
            Text = label.ToUpperInvariant(),
            TooltipText = tooltip,
            Disabled = locked,
            CustomMinimumSize = new Vector2(UiLayout.LeftRailWidth, _toolButtonHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.AddThemeColorOverride("font_color", locked ? _tokens.Muted : _tokens.Ink);
        button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
        button.AddThemeColorOverride("font_pressed_color", _tokens.Ink);
        button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
        button.AddThemeStyleboxOverride("normal", CreateToolStyle(active, locked));
        button.AddThemeStyleboxOverride("hover", CreateToolStyle(true, locked));
        button.AddThemeStyleboxOverride("pressed", CreateToolStyle(true, locked));
        button.AddThemeStyleboxOverride("focus", CreateToolStyle(true, locked, 3));
        button.AddThemeStyleboxOverride("disabled", CreateToolStyle(false, locked, opacity: 0.5f));
        if (locked)
        {
            button.Draw += () => DrawLockedToolBorder(button);
        }

        if (locked)
        {
            return button;
        }

        button.Pressed += () => EmitSignal(SignalName.ToolRequested, (int)tool);
        return button;
    }

    private void DrawLockedToolBorder(Button button)
    {
        var rect = new Rect2(Vector2.Zero, button.Size).Grow(-4);
        var color = _tokens.Muted;
        color.A = 0.72f;
        button.DrawDashedLine(rect.Position, rect.Position + new Vector2(rect.Size.X, 0), color, 2, 6, antialiased: true);
        button.DrawDashedLine(rect.Position + new Vector2(rect.Size.X, 0), rect.End, color, 2, 6, antialiased: true);
        button.DrawDashedLine(rect.End, rect.Position + new Vector2(0, rect.Size.Y), color, 2, 6, antialiased: true);
        button.DrawDashedLine(rect.Position + new Vector2(0, rect.Size.Y), rect.Position, color, 2, 6, antialiased: true);
    }

    private Control CreateBuildCanvasPanel()
    {
        var panel = MarkHostedInputPassthrough(ShowCanvasPreview ? CreatePanel(raised: true) : new Control());
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = MarkHostedInputPassthrough(CreateMargin(0));
        margin.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(margin);

        var layout = MarkHostedInputPassthrough(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        margin.AddChild(layout);

        var placeholder = MarkHostedInputPassthrough(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        placeholder.Draw += () =>
        {
            DrawBuildGrid(placeholder);
            if (ShowCanvasPreview)
            {
                DrawBuildPreview(placeholder);
            }
        };
        layout.AddChild(placeholder);

        if (Presentation is { ShowCompleteAction: false } presentation)
        {
            var chip = new UiChip
            {
                Tokens = _tokens,
                Text = presentation.PartsLockedChipText,
                Kind = UiChip.ChipKind.Locked,
                Position = new Vector2(18, 18),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            layout.AddChild(chip);
        }

        return panel;
    }

    private Control CreateBrainPanel()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var panel = CreatePanel(raised: true);
        panel.CustomMinimumSize = new Vector2(_partsTrayCollapsed ? 28 : UiLayout.RightPanelWidth, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        if (_partsTrayCollapsed)
        {
            var handle = new Button
            {
                Text = "‹",
                CustomMinimumSize = new Vector2(28, 0),
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            _tokens.ApplyTextStyle(handle, _tokens.HeadingText);
            handle.AddThemeColorOverride("font_color", _tokens.Accent);
            handle.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.Edge));
            handle.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent));
            handle.Pressed += TogglePartsTrayCollapsed;
            panel.AddChild(handle);
            return panel;
        }

        var margin = CreateMargin(UiSpacing.ControlGap(_tokens));
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));
        margin.AddChild(stack);

        var presentation = Presentation;
        if (presentation?.SelectedPartCount > 0)
        {
            stack.AddChild(presentation.SelectedPartCount == 1
                ? CreatePartSettingsPanel(presentation, allowDelete: presentation.ShowCompleteAction)
                : CreateSelectionPanel(presentation, allowDelete: presentation.ShowCompleteAction));
        }
        else if (presentation?.ShowCompleteAction == false)
        {
            stack.AddChild(CreateSavedCreationPanel(presentation));
        }
        else
        {
            stack.AddChild(CreatePartsTrayHeader());
            stack.AddChild(CreatePartButton("Node", "1 left", locked: false, ConstructionTool.Place));
            stack.AddChild(CreatePartButton("Core", $"{Mathf.Max(0, (presentation?.MaxCores ?? 1) - (presentation?.CoreCount ?? 0))} left", locked: presentation?.LockTopologyTools ?? false, ConstructionTool.Core));
            stack.AddChild(CreatePartButton("Motor", "1 left", locked: false, ConstructionTool.Beam));
            stack.AddChild(CreatePartButton("Spring", "locked", locked: true, null));
            stack.AddChild(CreateCompactValidationLine(buildPanel));
        }

        return panel;
    }

    private Control CreateSavedCreationPanel(ConstructionPresentationViewModel presentation)
    {
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));
        stack.AddChild(CreateLabel(presentation.TrainingSummaryTitle, 14, _tokens.Ink, expand: true));
        stack.AddChild(CreateLabel($"Best distance {presentation.BestDistanceText}", 12, _tokens.Accent, expand: true));
        stack.AddChild(CreateLabel(presentation.TrainingSummaryBody, 11, _tokens.Muted, expand: true));
        var resume = CreateButton("Resume training", UiActionButton.ActionKind.Primary, "Open Train setup");
        resume.Pressed += () => EmitSignal(SignalName.ResumeTrainingRequested);
        stack.AddChild(resume);
        var stats = CreateButton("Stats", UiActionButton.ActionKind.Secondary, "Open stats");
        stats.Pressed += () => EmitSignal(SignalName.StatsRequested);
        stack.AddChild(stats);
        var brain = CreateButton("Brain", UiActionButton.ActionKind.Secondary, "Open brain view");
        brain.Pressed += () => EmitSignal(SignalName.BrainRequested);
        stack.AddChild(brain);
        stack.AddChild(CreateSpacer());
        return stack;
    }

    private Control CreatePartSettingsPanel(ConstructionPresentationViewModel presentation, bool allowDelete)
    {
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", UiSpacing.DenseStackGap(_tokens));
        var header = new HBoxContainer();
        header.AddChild(CreateLabel(presentation.SinglePartTitle, 14, _tokens.Ink, expand: true));
        if (allowDelete)
        {
            var delete = CreateButton("Delete", UiActionButton.ActionKind.Danger, "Delete selected part", UiIconId.Trash);
            delete.CustomMinimumSize = new Vector2(38, 34);
            delete.Pressed += () => EmitSignal(SignalName.DeleteSelectionRequested);
            header.AddChild(delete);
        }

        var close = new UiSecondaryIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.Close,
            AccessibleLabel = "Close settings",
        };
        close.Pressed += () => EmitSignal(SignalName.ClearSelectionRequested);
        header.AddChild(close);
        stack.AddChild(header);
        stack.AddChild(CreateLabel(presentation.SinglePartPrimaryLabel, 10, _tokens.Muted));
        stack.AddChild(CreateLabel(presentation.SinglePartPrimaryValue, 12, _tokens.Ink, expand: true));
        stack.AddChild(CreateLabel(presentation.SinglePartConnectionsLabel, 10, _tokens.Muted));
        stack.AddChild(CreateLabel(presentation.SinglePartConnectionsValue, 11, _tokens.Ink, expand: true));
        stack.AddChild(CreateLabel("Facts", 10, _tokens.Muted));
        stack.AddChild(CreateLabel(presentation.SinglePartFacts, 10, _tokens.Muted, expand: true));
        stack.AddChild(CreateLabel(allowDelete ? "Structure" : "Locked topology", 10, _tokens.Muted));
        stack.AddChild(CreateLabel(presentation.SinglePartBody, 11, _tokens.Muted, expand: true));
        if (!allowDelete)
        {
            stack.AddChild(CreateLabel("No Delete in saved Creation", 11, _tokens.Muted, expand: true));
        }

        stack.AddChild(CreateSpacer());
        return stack;
    }

    private Control CreateSelectionPanel(ConstructionPresentationViewModel presentation, bool allowDelete)
    {
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));
        var header = new HBoxContainer();
        header.AddChild(CreateLabel(presentation.MultiSelectionTitle, 14, _tokens.Ink, expand: true));
        if (allowDelete)
        {
            var delete = CreateButton("Delete", UiActionButton.ActionKind.Danger, "Delete selected parts");
            delete.CustomMinimumSize = new Vector2(82, 34);
            delete.Pressed += () => EmitSignal(SignalName.DeleteSelectionRequested);
            header.AddChild(delete);
        }

        var close = new UiSecondaryIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.Close,
            AccessibleLabel = "Close selection",
        };
        close.Pressed += () => EmitSignal(SignalName.ClearSelectionRequested);
        header.AddChild(close);
        stack.AddChild(header);
        stack.AddChild(CreateLabel(presentation.MultiSelectionCounts, 12, _tokens.Ink, expand: true));
        stack.AddChild(CreateLabel(allowDelete ? "Drag any selected part to move them together, or delete the selection." : presentation.MultiSelectionBody, 11, _tokens.Muted, expand: true));
        stack.AddChild(new UiChip
        {
            Tokens = _tokens,
            Text = allowDelete ? "Move · Delete" : "Move only",
            Kind = allowDelete ? UiChip.ChipKind.Neutral : UiChip.ChipKind.Locked,
        });
        stack.AddChild(CreateSpacer());
        return stack;
    }

    private Control CreateNameEntryOverlay()
    {
        var overlay = CreateDismissOverlay(() =>
        {
            _nameEntryOpen = false;
            RebuildLayout();
        });
        var panel = CreatePanel(raised: true);
        panel.Position = new Vector2(96, 58);
        panel.CustomMinimumSize = new Vector2(280, 104);
        overlay.AddChild(panel);

        var margin = CreateMargin((int)_tokens.Space3);
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel("Creation name", 14, _tokens.Ink));
        var entry = new LineEdit
        {
            Text = Presentation?.CreationName ?? "Untitled Creation",
            CustomMinimumSize = new Vector2(0, 36),
        };
        entry.TextSubmitted += text =>
        {
            SubmitCreationName(text);
        };
        stack.AddChild(entry);
        var apply = CreateButton("Apply", UiActionButton.ActionKind.Primary, "Rename Creation");
        apply.Pressed += () => SubmitCreationName(entry.Text);
        stack.AddChild(apply);
        entry.CallDeferred(LineEdit.MethodName.GrabFocus);
        return overlay;
    }

    private void SubmitCreationName(string text)
    {
        var name = text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _nameEntryOpen = false;
        EmitSignal(SignalName.CreationNameChanged, name);
    }

    private Control CreateCreationOverflowOverlay()
    {
        var overlay = CreateDismissOverlay(() =>
        {
            _creationOverflowOpen = false;
            RebuildLayout();
        });
        var panel = CreatePanel(raised: true);
        panel.Position = new Vector2(458, 58);
        panel.CustomMinimumSize = new Vector2(170, 130);
        overlay.AddChild(panel);
        var margin = CreateMargin((int)_tokens.Space2);
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(stack);

        if (Presentation?.ShowCompleteAction == false)
        {
            var reset = CreateButton("Reset training", UiActionButton.ActionKind.Secondary, "Reset saved training");
            reset.Pressed += () =>
            {
                _creationOverflowOpen = false;
                RebuildLayout();
                EmitSignal(SignalName.ResetTrainingRequested);
            };
            stack.AddChild(reset);
            var delete = CreateButton("Delete creation", UiActionButton.ActionKind.Danger, "Delete this Creation");
            delete.Pressed += () =>
            {
                _creationOverflowOpen = false;
                RebuildLayout();
                EmitSignal(SignalName.DeleteCreationRequested);
            };
            stack.AddChild(delete);
        }
        else
        {
            stack.AddChild(CreateLabel("More actions arrive in later milestones.", 11, _tokens.Muted, expand: true));
        }

        return overlay;
    }

    private Control CreateDismissOverlay(Action dismissed)
    {
        var overlay = new Control
        {
            MouseFilter = MouseFilterEnum.Stop,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.25f),
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Stop,
        };
        dim.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
            {
                dismissed();
            }
        };
        overlay.AddChild(dim);
        return overlay;
    }

    private Control CreatePartsTrayHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        row.AddChild(CreateLabel("Parts tray", 14, _tokens.Ink, expand: true));
        var collapse = new Button
        {
            Text = "›",
            CustomMinimumSize = new Vector2(28, 28),
        };
        _tokens.ApplyTextStyle(collapse, _tokens.LabelText);
        collapse.AddThemeColorOverride("font_color", _tokens.Accent);
        collapse.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.Edge, radius: (int)_tokens.RadiusSmall));
        collapse.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent, radius: (int)_tokens.RadiusSmall));
        collapse.Pressed += TogglePartsTrayCollapsed;
        row.AddChild(collapse);
        return row;
    }

    private void TogglePartsTrayCollapsed()
    {
        _partsTrayCollapsed = !_partsTrayCollapsed;
        RebuildLayout();
    }

    private Control CreateCompactValidationLine(ConstructionBuildPanelPresentation buildPanel)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 34),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        row.AddChild(UiFieldAndRows.Icon(buildPanel.CanStartTraining ? UiIconId.Check : UiIconId.Warn, UiIconSize.Small, buildPanel.CanStartTraining ? _tokens.Accent : _tokens.Danger));
        var reason = buildPanel.CanStartTraining
            ? "Ready to save"
            : ShortValidationText(buildPanel.DisabledReason ?? buildPanel.ValidationLine);
        row.AddChild(CreateLabel(reason, 12, buildPanel.CanStartTraining ? _tokens.Accent : _tokens.Danger, expand: true));
        return row;
    }

    private Control CreateBrainSetupOverlay()
    {
        var overlay = new Control
        {
            Name = "BrainSetupOverlay",
            MouseFilter = MouseFilterEnum.Stop,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        overlay.AddChild(new ColorRect
        {
            Color = new Color(0, 0, 0, 0.45f),
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Stop,
        });

        var sheet = CreatePanel(raised: true);
        sheet.Position = new Vector2(76, 52);
        sheet.CustomMinimumSize = new Vector2(488, 260);
        overlay.AddChild(sheet);

        var margin = CreateMargin((int)_tokens.Space3);
        sheet.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(layout);
        layout.AddChild(CreateBrainSetupTopBar());

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", (int)_tokens.Space3);
        layout.AddChild(body);

        var controls = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(270, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        controls.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        body.AddChild(controls);
        controls.AddChild(CreateLayerChooser());
        controls.AddChild(CreateNeuronControl());

        body.AddChild(CreateBrainPreviewPanel());
        return overlay;
    }

    private Control CreateBrainSetupTopBar()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        row.AddChild(CreateLabel("Brain setup", 16, _tokens.Ink, expand: true));
        var recommended = new Button
        {
            Text = "Use recommended",
            CustomMinimumSize = new Vector2(124, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Reset layers and neurons",
        };
        _tokens.ApplyTextStyle(recommended, _tokens.CaptionText);
        recommended.AddThemeColorOverride("font_color", _tokens.Ink);
        recommended.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.Edge, radius: (int)_tokens.RadiusSmall));
        recommended.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent, radius: (int)_tokens.RadiusSmall));
        recommended.Pressed += () => EmitBrainShape(BrainShapeDef.DefaultHiddenLayers, RecommendedNeurons(buildPanel));
        row.AddChild(recommended);
        var close = new Button
        {
            Text = string.Empty,
            CustomMinimumSize = new Vector2(36, 36),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Close brain setup",
        };
        _tokens.ApplyTextStyle(close, _tokens.HeadingText);
        close.AddThemeColorOverride("font_color", _tokens.Accent);
        UiIcons.Apply(close, UiIconId.Close, UiIconSize.Standard, _tokens.Accent);
        close.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(_tokens.PanelRaised, _tokens.Edge, radius: (int)_tokens.RadiusSmall));
        close.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent, radius: (int)_tokens.RadiusSmall));
        close.Pressed += () =>
        {
            _brainSetupOpen = false;
            RebuildLayout();
        };
        row.AddChild(close);
        return row;
    }

    private Control CreateLayerChooser()
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        stack.AddChild(row);
        row.AddChild(CreateLayerButton(1, "simple", shape.HiddenLayers == 1));
        row.AddChild(CreateLayerButton(2, "navigation", shape.HiddenLayers == 2));
        row.AddChild(CreateLayerButton(3, "experiment", shape.HiddenLayers == 3));
        var help = shape.HiddenLayers switch
        {
            1 => "Recommended for simple tasks",
            2 => "Recommended for harder navigation",
            _ => "! Not recommended: slow to learn. For experiments.",
        };
        stack.AddChild(CreateLabel(help, 12, shape.HiddenLayers == 3 ? _tokens.Halo : _tokens.Muted, expand: true));
        return stack;
    }

    private Control CreateLayerButton(int layers, string caption, bool active)
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var button = new Button
        {
            Text = $"{layers}\n{caption}".ToUpperInvariant(),
            CustomMinimumSize = new Vector2(82, 42),
        };
        _tokens.ApplyTextStyle(button, _tokens.CaptionText);
        button.AddThemeColorOverride("font_color", active ? _tokens.OnAccent : _tokens.Ink);
        button.AddThemeStyleboxOverride("normal", _tokens.ControlStyle(active ? _tokens.Accent : _tokens.PanelRaised, active ? _tokens.Accent : _tokens.Edge));
        button.AddThemeStyleboxOverride("hover", _tokens.ControlStyle(_tokens.AccentSoft, _tokens.Accent));
        button.Pressed += () => EmitBrainShape(layers, shape.NeuronsPerLayer);
        return button;
    }

    private Control CreateNeuronControl()
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        stack.AddChild(CreateLabel($"Neurons per layer · tick = default {RecommendedNeurons(buildPanel)}", 12, _tokens.Muted));
        stack.AddChild(CreateLabel(shape.HiddenLayers == 1 ? "Layer 1 shares this value" : $"Layers 1-{shape.HiddenLayers} share this value", 10, _tokens.Muted));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        stack.AddChild(row);
        row.AddChild(CreateStepper("−", -1, "Decrease neurons"));
        var minimumNeurons = BrainShapeDef.MinimumNeuronsPerLayer;
        var maximumNeurons = BrainShapeDef.MaximumNeuronsPerLayer;
        var slider = new UiSlider
        {
            Tokens = _tokens,
            LabelText = "Neurons",
            ReadoutText = shape.NeuronsPerLayer.ToString(),
            Thumbs =
            [
                (shape.NeuronsPerLayer - minimumNeurons) /
                (double)(maximumNeurons - minimumNeurons),
            ],
            StepLabels = [minimumNeurons.ToString(), maximumNeurons.ToString()],
            MarkerPosition =
                (RecommendedNeurons(buildPanel) - minimumNeurons) /
                (double)(maximumNeurons - minimumNeurons),
            MarkerText = $"default {RecommendedNeurons(buildPanel)}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        slider.ThumbChangeCommitted += (_, position) =>
        {
            var neurons = (int)Math.Round(minimumNeurons + (position * (maximumNeurons - minimumNeurons)));
            EmitBrainShape(shape.HiddenLayers, neurons);
        };
        row.AddChild(slider);
        row.AddChild(CreateStepper("+", 1, "Increase neurons"));
        return stack;
    }

    private UiButton CreateStepper(string label, int delta, string accessibleLabel)
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var button = new UiSecondaryIconButton
        {
            Tokens = _tokens,
            IconId = null,
            SymbolText = label,
            AccessibleLabel = accessibleLabel,
            ButtonSize = UiIconButtonSize.Small,
        };
        button.Activated += () => EmitBrainShape(
            shape.HiddenLayers,
            Mathf.Clamp(shape.NeuronsPerLayer + delta, BrainShapeDef.MinimumNeuronsPerLayer, BrainShapeDef.MaximumNeuronsPerLayer));
        return button;
    }

    private Control CreateBrainPreviewPanel()
    {
        var panel = CreatePanel(raised: false);
        panel.CustomMinimumSize = new Vector2(170, 176);
        var margin = CreateMargin((int)_tokens.Space2);
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel("Live preview", 14, _tokens.Ink));
        stack.AddChild(CreateBrainSetupPreview());
        var connections = ConnectionCount();
        stack.AddChild(CreateLabel($"{connections:0} connections", 14, _tokens.Accent));
        return panel;
    }

    private Control CreateBrainSetupPreview()
    {
        var preview = new Control
        {
            CustomMinimumSize = new Vector2(0, 96),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        preview.Draw += () => DrawBrainSetupPreview(preview);
        return preview;
    }

    private void DrawBrainSetupPreview(Control control)
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        if (buildPanel.InputCount <= 0 || buildPanel.OutputCount <= 0)
        {
            control.DrawString(
                ThemeDB.FallbackFont,
                new Vector2(12, control.Size.Y / 2),
                "Add anatomy to preview brain",
                HorizontalAlignment.Left,
                control.Size.X - 24,
                10,
                _tokens.Muted);
            return;
        }

        var layers = new[] { buildPanel.InputCount }.Concat(Enumerable.Repeat(shape.NeuronsPerLayer, shape.HiddenLayers)).Concat([buildPanel.OutputCount]).ToArray();
        var spacingX = control.Size.X / (layers.Length + 1);
        var font = ThemeDB.FallbackFont;
        var nodeColumns = new List<List<Vector2>>();
        for (var layer = 0; layer < layers.Length; layer++)
        {
            var count = layers[layer];
            var shown = Math.Min(6, Math.Max(1, count));
            var x = spacingX * (layer + 1);
            var column = new List<Vector2>();
            var header = layer == 0
                ? "Senses"
                : layer == layers.Length - 1
                    ? "Motors"
                    : $"H{layer}";
            control.DrawString(font, new Vector2(x - 24, 10), header, HorizontalAlignment.Center, 48, 8, _tokens.Muted);
            for (var i = 0; i < shown; i++)
            {
                var y = 14 + ((control.Size.Y - 34) / (shown + 1) * (i + 1));
                var point = new Vector2(x, y);
                column.Add(point);
                var color = layer == 0 || layer == layers.Length - 1 ? _tokens.Accent : _tokens.LineStrong;
                control.DrawArc(point, 4, 0, Mathf.Tau, 18, color, 1.5f, antialiased: true);
            }

            var label = count > 6 ? $"+{count - 6} more" : $"{count}";
            control.DrawString(font, new Vector2(x - 22, control.Size.Y - 3), label, HorizontalAlignment.Center, 44, 10, _tokens.Muted);
            nodeColumns.Add(column);
        }

        for (var layer = 0; layer < nodeColumns.Count - 1; layer++)
        {
            foreach (var from in nodeColumns[layer])
            {
                foreach (var to in nodeColumns[layer + 1])
                {
                    control.DrawLine(from, to, new Color(_tokens.Edge, 0.35f), 0.5f, antialiased: true);
                }
            }
        }
    }

    private void EmitBrainShape(int hiddenLayers, int neuronsPerLayer)
    {
        EmitSignal(SignalName.BrainShapeChanged, hiddenLayers, neuronsPerLayer);
    }

    private int RecommendedNeurons(ConstructionBuildPanelPresentation buildPanel) =>
        Mathf.Clamp((int)Math.Ceiling((buildPanel.InputCount + buildPanel.OutputCount) / 2.0), BrainShapeDef.MinimumNeuronsPerLayer, BrainShapeDef.MaximumNeuronsPerLayer);

    private int ConnectionCount()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var layers = new[] { buildPanel.InputCount }.Concat(Enumerable.Repeat(shape.NeuronsPerLayer, shape.HiddenLayers)).Concat([buildPanel.OutputCount]).ToArray();
        var total = 0;
        for (var i = 0; i < layers.Length - 1; i++)
        {
            total += layers[i] * layers[i + 1];
        }

        return total;
    }

    private Control CreatePartButton(string label, string countText, bool locked, ConstructionTool? tool)
    {
        var button = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = $"{label} · {countText}",
            Kind = UiActionButton.ActionKind.Secondary,
            Locked = locked,
            ShowLockReasonInText = false,
            LockReason = countText,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        if (!locked && tool is { } activeTool)
        {
            button.Pressed += () => EmitSignal(SignalName.ToolRequested, (int)activeTool);
        }

        return button;
    }

    private Control CreateBrainPreview(ConstructionBuildPanelPresentation buildPanel)
    {
        var preview = new Control
        {
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        preview.Draw += () => DrawBrainPreview(preview, buildPanel);
        return preview;
    }

    private static string BuildBrainCountLine(ConstructionBuildPanelPresentation buildPanel)
    {
        var motorWord = buildPanel.OutputCount == 1 ? "motor" : "motors";
        return $"{buildPanel.InputCount} senses · {buildPanel.OutputCount} {motorWord}";
    }

    private Control CreateValidationLine(ConstructionBuildPanelPresentation buildPanel)
    {
        var text = buildPanel.CanStartTraining
            ? "Ready to train"
            : ShortValidationText(buildPanel.DisabledReason ?? buildPanel.ValidationLine);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var icon = UiFieldAndRows.Icon(buildPanel.CanStartTraining ? UiIconId.Check : UiIconId.Warn, UiIconSize.Large, buildPanel.CanStartTraining ? _tokens.Accent : _tokens.Danger);
        icon.CustomMinimumSize = new Vector2(28, 0);
        row.AddChild(icon);
        row.AddChild(CreateLabel(text, 18, buildPanel.CanStartTraining ? _tokens.Accent : _tokens.Danger, expand: true));
        return row;
    }

    private static string ShortValidationText(string text) =>
        text.StartsWith("Add nodes and beams", StringComparison.Ordinal)
            ? "Add nodes + beams"
            : text.Contains("has no beams attached", StringComparison.Ordinal)
                ? "1 node not connected"
                : text;

    private void DrawBuildGrid(Control control)
    {
        var size = control.Size;
        for (var x = 48f; x < size.X; x += 48f)
        {
            control.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), _tokens.Line, 1);
        }

        for (var y = 48f; y < size.Y; y += 48f)
        {
            control.DrawLine(new Vector2(0, y), new Vector2(size.X, y), _tokens.Line, 1);
        }

        DrawCornerMarks(control, size);
    }

    private void DrawBuildPreview(Control control)
    {
        var size = control.Size;
        var leftHip = new Vector2(size.X * 0.28f, size.Y * 0.42f);
        var top = new Vector2(size.X * 0.36f, size.Y * 0.26f);
        var rightHip = new Vector2(size.X * 0.44f, size.Y * 0.42f);
        var leftKnee = new Vector2(size.X * 0.24f, size.Y * 0.58f);
        var leftFoot = new Vector2(size.X * 0.20f, size.Y * 0.76f);
        var rightKnee = new Vector2(size.X * 0.50f, size.Y * 0.60f);
        var rightFoot = new Vector2(size.X * 0.58f, size.Y * 0.76f);
        var brokenA = new Vector2(size.X * 0.72f, size.Y * 0.66f);
        var brokenB = new Vector2(size.X * 0.84f, size.Y * 0.86f);

        DrawTriangleFill(control, leftHip, top, rightHip);
        DrawBeam(control, leftHip, top, _tokens.Edge);
        DrawBeam(control, top, rightHip, _tokens.Edge);
        DrawBeam(control, leftHip, rightHip, _tokens.Edge);
        DrawBeam(control, leftHip, leftKnee, _tokens.Edge);
        DrawBeam(control, leftKnee, leftFoot, _tokens.Edge);
        DrawBeam(control, rightHip, rightKnee, _tokens.Edge);
        DrawBeam(control, rightKnee, rightFoot, _tokens.Edge);
        control.DrawDashedLine(brokenA, brokenB, _tokens.Danger, 4, 7, antialiased: true);

        DrawMotorArc(control, leftHip, clockwise: false);
        DrawMotorArc(control, rightHip, clockwise: true);
        DrawMotorArc(control, leftKnee, clockwise: false);
        DrawMotorArc(control, rightKnee, clockwise: true);

        DrawNode(control, leftHip, hasCore: false);
        DrawNode(control, top, hasCore: true);
        DrawNode(control, rightHip, hasCore: false);
        DrawNode(control, leftKnee, hasCore: false);
        DrawNode(control, leftFoot, hasCore: true, selected: true);
        DrawNode(control, rightKnee, hasCore: false);
        DrawNode(control, rightFoot, hasCore: true);
        DrawInvalidNode(control, brokenA);
        DrawInvalidNode(control, brokenB);

        DrawTag(control, "Rigid: no joints", top + new Vector2(-70, -42), _tokens.Halo);
        DrawTag(control, "Not connected", brokenA + new Vector2(-48, -46), _tokens.Danger);
    }

    private void DrawBrainPreview(Control control, ConstructionBuildPanelPresentation buildPanel)
    {
        var size = control.Size;
        if (buildPanel.InputCount == 0 || buildPanel.OutputCount == 0)
        {
            control.DrawString(
                ThemeDB.FallbackFont,
                new Vector2(8, size.Y * 0.58f),
                "Build anatomy to preview brain",
                HorizontalAlignment.Left,
                -1,
                17,
                _tokens.Muted);
            return;
        }

        var inputX = size.X * 0.08f;
        var hiddenX = size.X * 0.52f;
        var outputX = size.X * 0.92f;
        var inputPreviewCount = Mathf.Clamp(buildPanel.InputCount, 1, 6);
        var outputPreviewCount = Mathf.Clamp(buildPanel.OutputCount, 1, 6);
        var hiddenPreviewCount = Mathf.Clamp((inputPreviewCount + outputPreviewCount) / 2 + 1, 3, 6);
        var inputs = Enumerable.Range(0, inputPreviewCount).Select(index => PreviewPoint(inputX, size.Y, inputPreviewCount, index)).ToArray();
        var hidden = Enumerable.Range(0, hiddenPreviewCount).Select(index => PreviewPoint(hiddenX, size.Y, hiddenPreviewCount, index)).ToArray();
        var outputs = Enumerable.Range(0, outputPreviewCount).Select(index => PreviewPoint(outputX, size.Y, outputPreviewCount, index)).ToArray();
        foreach (var from in inputs)
        {
            foreach (var to in hidden)
            {
                control.DrawLine(from, to, _tokens.Accent with { A = 0.45f }, 1.2f, antialiased: true);
            }
        }

        foreach (var from in hidden)
        {
            foreach (var to in outputs)
            {
                control.DrawLine(from, to, _tokens.Accent with { A = 0.55f }, 1.2f, antialiased: true);
            }
        }

        foreach (var point in inputs.Concat(hidden).Concat(outputs))
        {
            control.DrawCircle(point, 5, _tokens.PanelRaised);
            control.DrawArc(point, 5, 0, Mathf.Tau, 18, _tokens.LineStrong, 1.5f, antialiased: true);
        }
    }

    private static Vector2 PreviewPoint(float x, float height, int count, int index)
    {
        var spacing = height / (count + 1);
        return new Vector2(x, spacing * (index + 1));
    }

    private void DrawCornerMarks(Control control, Vector2 size)
    {
        control.DrawLine(new Vector2(14, 14), new Vector2(42, 14), _tokens.Accent, 3);
        control.DrawLine(new Vector2(14, 14), new Vector2(14, 42), _tokens.Accent, 3);
        control.DrawLine(new Vector2(size.X - 42, 14), new Vector2(size.X - 14, 14), _tokens.Accent, 3);
        control.DrawLine(new Vector2(size.X - 14, 14), new Vector2(size.X - 14, 42), _tokens.Accent, 3);
    }

    private void DrawTriangleFill(Control control, Vector2 a, Vector2 b, Vector2 c)
    {
        control.DrawColoredPolygon([a, b, c], _tokens.Muted with { A = 0.10f });
    }

    private void DrawBeam(Control control, Vector2 start, Vector2 end, Color color)
    {
        control.DrawLine(start, end, color, 5, antialiased: true);
    }

    private void DrawNode(Control control, Vector2 position, bool hasCore, bool selected = false)
    {
        if (selected)
        {
            control.DrawCircle(position, 28, _tokens.Halo);
            control.DrawCircle(position, 23, _tokens.Background);
        }

        if (_tokens.EffectsEnabled)
        {
            control.DrawCircle(position, 22, _tokens.AccentGlow);
        }

        control.DrawCircle(position, 12, _tokens.Panel);
        control.DrawArc(position, 12, 0, Mathf.Tau, 24, _tokens.LineStrong, 3, antialiased: true);
        if (hasCore)
        {
            var half = new Vector2(12, 12);
            var points = new[]
            {
                position + new Vector2(0, -half.Y),
                position + new Vector2(half.X, 0),
                position + new Vector2(0, half.Y),
                position + new Vector2(-half.X, 0),
            };
            control.DrawPolyline(points.Append(points[0]).ToArray(), _tokens.Accent, 3, antialiased: true);
        }
    }

    private void DrawInvalidNode(Control control, Vector2 position)
    {
        control.DrawCircle(position, 12, _tokens.Panel);
        control.DrawArc(position, 12, 0, Mathf.Tau, 24, _tokens.Danger, 3, antialiased: true);
    }

    private void DrawMotorArc(Control control, Vector2 center, bool clockwise)
    {
        var start = clockwise ? -0.35f : 0.8f;
        var end = clockwise ? 1.0f : 2.1f;
        control.DrawArc(center, 28, start, end, 20, _tokens.Accent, 3, antialiased: true);
    }

    private void DrawTag(Control control, string text, Vector2 position, Color borderColor)
    {
        var width = Mathf.Max(126, text.Length * 10);
        control.DrawRect(new Rect2(position, new Vector2(width, 30)), _tokens.PanelRaised);
        control.DrawRect(new Rect2(position, new Vector2(width, 30)), borderColor, filled: false, width: 2);
        control.DrawString(ThemeDB.FallbackFont, position + new Vector2(12, 21), text, HorizontalAlignment.Left, -1, 16, _tokens.Ink);
    }

    private UiPanel CreatePanel(bool raised)
    {
        return new UiPanel
        {
            Tokens = _tokens,
            Variant = raised
                ? UiSurfaceContracts.FrameVariant.Raised
                : UiSurfaceContracts.FrameVariant.Frame,
        };
    }

    private Control CreateModeSwitch()
    {
        var frame = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, _modeSwitchHeight),
        };
        frame.AddThemeConstantOverride("separation", 0);
        frame.AddChild(CreateModeSegment("Simulate", active: false, first: true, last: false));
        frame.AddChild(CreateModeSegment("Build", active: true, first: false, last: true));
        return frame;
    }

    private Button CreateModeSegment(string label, bool active, bool first, bool last)
    {
        var button = new Button
        {
            Text = label.ToUpperInvariant(),
            CustomMinimumSize = new Vector2(148, _modeSwitchHeight),
            Disabled = active,
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", _tokens.Ink);
        button.AddThemeColorOverride("font_disabled_color", _tokens.Ink);
        button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
        button.AddThemeStyleboxOverride("normal", CreateSegmentStyle(active, first, last));
        button.AddThemeStyleboxOverride("hover", CreateSegmentStyle(true, first, last));
        button.AddThemeStyleboxOverride("pressed", CreateSegmentStyle(true, first, last));
        button.AddThemeStyleboxOverride("disabled", CreateSegmentStyle(active, first, last));
        if (!active)
        {
            button.Pressed += () => EmitSignal(SignalName.SimulateRequested);
        }
        return button;
    }

    private UiActionButton CreateButton(string label, UiActionButton.ActionKind kind, string tooltip, UiIconId? iconId = null)
    {
        return new UiActionButton
        {
            Tokens = _tokens,
            Kind = kind,
            LabelText = label,
            IconId = iconId,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
    }

    private StyleBoxFlat CreateSegmentStyle(bool active, bool first, bool last)
    {
        var radius = (int)_tokens.RadiusMedium;
        return new StyleBoxFlat
        {
            BgColor = active ? _tokens.AccentSoft : _tokens.PanelRaised,
            BorderColor = active ? _tokens.Accent : _tokens.LineStrong,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = last ? 1 : 0,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = first ? radius : 0,
            CornerRadiusBottomLeft = first ? radius : 0,
            CornerRadiusTopRight = last ? radius : 0,
            CornerRadiusBottomRight = last ? radius : 0,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
        };
    }

    private StyleBoxFlat CreateToolStyle(bool active, bool locked, int borderWidth = 1, float opacity = 1)
    {
        var radius = (int)_tokens.RadiusMedium;
        var border = active ? _tokens.Accent : _tokens.LineStrong;
        var alpha = opacity * (locked ? 0.5f : 1f);
        return new StyleBoxFlat
        {
            BgColor = active
                ? new Color(_tokens.AccentSoft.R, _tokens.AccentSoft.G, _tokens.AccentSoft.B, _tokens.AccentSoft.A * alpha)
                : new Color(_tokens.PanelRaised.R, _tokens.PanelRaised.G, _tokens.PanelRaised.B, _tokens.PanelRaised.A * alpha),
            BorderColor = new Color(border.R, border.G, border.B, border.A * alpha),
            BorderWidthLeft = locked ? 2 : active ? 2 : borderWidth,
            BorderWidthTop = locked ? 1 : active ? 2 : borderWidth,
            BorderWidthRight = locked ? 2 : active ? 2 : borderWidth,
            BorderWidthBottom = locked ? 1 : active ? 2 : borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
        };
    }

    private Control CreateSpacer()
    {
        return new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
    }

    private MarginContainer CreateMargin(int margin)
    {
        var container = new MarginContainer();
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
        return container;
    }

    private static T MarkHostedInputPassthrough<T>(T control) where T : Control
    {
        control.SetMeta(_hostedInputPassthroughMeta, true);
        return control;
    }

    private Label CreateLabel(string text, int fontSize, Color color, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void ApplyHostedInputPassthrough(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = control == this || control.HasMeta(_hostedInputPassthroughMeta)
                ? MouseFilterEnum.Ignore
                : MouseFilterEnum.Stop;
        }

        foreach (var child in node.GetChildren())
        {
            ApplyHostedInputPassthrough(child);
        }
    }
}

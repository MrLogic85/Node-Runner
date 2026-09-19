using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Sample-data host for the persistent Watch/Build shell. It intentionally
/// keeps both child screens disconnected from game state.
/// </summary>
public partial class SampleFlowScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private Control? _content;
    private WatchScreen? _watch;
    private BuildScreen? _build;
    private UiSegmentedSwitch? _modeSwitch;
    private Control? _overlay;
    private Button? _overlayDismiss;
    private UiOverflowMenu? _overflowMenu;
    private UiToast? _toast;
    private UiSheet? _sheet;
    private int _selectedMode;
    private Control? _sampleView;
    private string? _lastDeletedCreation;

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            if (_watch is not null)
            {
                _watch.Tokens = value;
            }

            if (_build is not null)
            {
                _build.Tokens = value;
            }

            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public override void _Ready()
    {
        Name = nameof(SampleFlowScreen);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Size = GetViewportRect().Size;
        RebuildLayout();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            RefreshOverlayLayout();
        }
    }

    private void RebuildLayout()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _content = null;
        _watch = null;
        _build = null;
        _modeSwitch = null;
        _overlay = null;
        _overlayDismiss = null;
        _overflowMenu = null;
        _toast = null;
        _sheet = null;
        _sampleView = null;
        BuildLayout();
        if (_selectedMode == 0)
        {
            ShowWatch();
        }
        else
        {
            ShowBuild();
        }
    }

    private void BuildLayout()
    {
        AddChild(new ColorRect
        {
            Color = _tokens.Background,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeConstantOverride("margin_left", 24);
        frame.AddThemeConstantOverride("margin_top", 18);
        frame.AddThemeConstantOverride("margin_right", 24);
        frame.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(frame);

        var shell = new VBoxContainer();
        shell.AddThemeConstantOverride("separation", 10);
        frame.AddChild(shell);

        var header = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
        header.AddThemeConstantOverride("separation", 12);
        shell.AddChild(header);

        var title = new Label
        {
            Text = "NODE RUNNER",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", _tokens.Ink);
        header.AddChild(title);

        _modeSwitch = new UiSegmentedSwitch
        {
            Tokens = _tokens,
            Options = new[] { "Watch", "Build" },
            SelectedIndex = _selectedMode,
            CustomMinimumSize = new Vector2(208, _tokens.TouchTarget),
        };
        _modeSwitch.SelectionChanged += index => SetMode(index);
        header.AddChild(_modeSwitch);

        var menu = new UiIconButton
        {
            Tokens = _tokens,
            IconText = "⋯",
            AccessibleLabel = "Open sample menu",
        };
        menu.Pressed += ToggleOverflowMenu;
        header.AddChild(menu);

        _content = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        shell.AddChild(_content);

        _overlay = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 9,
        };
        _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        _overlayDismiss = new Button
        {
            Flat = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _overlayDismiss.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _overlayDismiss.Pressed += CloseOverlays;
        _overlayDismiss.Visible = false;
        _overlay.AddChild(_overlayDismiss);

        _overflowMenu = new UiOverflowMenu
        {
            Tokens = _tokens,
            ZIndex = 10,
        };
        _overflowMenu.SetActions(
            ("training-settings", "Sample settings", false),
            ("start-over", "Sample start over", true),
            ("creations", "Creations", false));
        _overflowMenu.ActionSelected += OnOverflowAction;
        _overlay.AddChild(_overflowMenu);

        _toast = new UiToast
        {
            Tokens = _tokens,
            ZIndex = 11,
            Position = new Vector2(24, 0),
            SizeFlagsHorizontal = SizeFlags.Expand,
        };
        _toast.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _toast.CustomMinimumSize = new Vector2(420, _tokens.TouchTarget);
        _toast.UndoPressed += RestoreDeletedCreation;
        _overlay.AddChild(_toast);

        _sheet = new UiSheet
        {
            Tokens = _tokens,
            ZIndex = 12,
            CustomMinimumSize = new Vector2(460, 0),
        };
        _overlay.AddChild(_sheet);
        _sheet.Hide();
    }

    private void ShowWatch()
    {
        if (_content is null)
        {
            return;
        }

        ClearContent();
        _watch = new WatchScreen
        {
            Tokens = _tokens,
            ShowTopBar = false,
            Hosted = true,
        };
        _sampleView = _watch;
        _content.AddChild(_watch);
    }

    private void ShowBuild()
    {
        if (_content is null)
        {
            return;
        }

        ClearContent();
        _build = new BuildScreen
        {
            Tokens = _tokens,
            ShowTopBar = false,
            Hosted = true,
        };
        _build.TrainingRequested += () => SetMode(0);
        _sampleView = _build;
        _content.AddChild(_build);
    }

    private void SetMode(int mode)
    {
        CloseOverlays();
        _selectedMode = Mathf.Clamp(mode, 0, 1);
        if (_modeSwitch is not null)
        {
            _modeSwitch.SelectedIndex = _selectedMode;
        }

        if (_selectedMode == 0)
        {
            ShowWatch();
        }
        else
        {
            ShowBuild();
        }
    }

    private void ShowCreations()
    {
        if (_content is null)
        {
            return;
        }

        ClearContent();
        var creations = new CreationsScreen { Tokens = _tokens };
        creations.OpenRequested += _ => SetMode(0);
        creations.EditRequested += ShowEdit;
        creations.DuplicateRequested += name => ShowSheet("Duplicate " + name + "?", CreateDuplicateBody(name));
        creations.DeleteRequested += name => ShowSheet("Delete " + name + "?", CreateDeleteBody(name));
        creations.BackRequested += () => SetMode(0);
        _sampleView = creations;
        _content.AddChild(creations);
    }

    private void ShowEdit(string creationName)
    {
        if (_content is null)
        {
            return;
        }

        ClearContent();
        var edit = new EditScreen { Tokens = _tokens, CreationName = creationName };
        edit.DoneRequested += () => SetMode(0);
        edit.RebuildRequested += () => ShowSheet("Rebuild body?", CreateRebuildBody(creationName));
        _sampleView = edit;
        _content.AddChild(edit);
    }

    private void ToggleOverflowMenu()
    {
        if (_overflowMenu is null)
        {
            return;
        }

        RefreshOverlayLayout();
        _overflowMenu.Visible = !_overflowMenu.Visible;
        if (_overlayDismiss is not null)
        {
            _overlayDismiss.Visible = _overflowMenu.Visible;
        }
    }

    private void CloseOverlays()
    {
        if (_overflowMenu is not null)
        {
            _overflowMenu.Hide();
        }

        if (_overlayDismiss is not null)
        {
            _overlayDismiss.Hide();
        }

        _toast?.Hide();
        _sheet?.Hide();
    }

    private void RefreshOverlayLayout()
    {
        if (_overflowMenu is not null)
        {
            _overflowMenu.Position = new Vector2(Mathf.Max(24, Size.X - 240), 76);
        }

        if (_sheet is not null && _sheet.Visible)
        {
            _sheet.Position = new Vector2(
                Mathf.Max(24, (Size.X - _sheet.CustomMinimumSize.X) / 2),
                Mathf.Max(72, (Size.Y - 240) / 2));
        }
    }

    private void OnOverflowAction(string actionId)
    {
        CloseOverlays();
        if (_sheet is null)
        {
            return;
        }

        if (actionId == "creations")
        {
            ShowCreations();
        }
        else if (actionId == "start-over")
        {
            ShowSheet("Start over?", CreateConfirmationBody());
        }
        else
        {
            ShowSheet("Training settings", CreateTrainingSettingsBody());
        }
    }

    private void ShowSheet(string title, Control body)
    {
        _sheet!.Title = title;
        _sheet.SetBody(body);
        _sheet.Show();
        RefreshOverlayLayout();
        _overlayDismiss?.Show();
    }

    private Control CreateConfirmationBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        stack.AddChild(new Label
        {
            Text = "This sample action would clear the current run. The real flow keeps an Undo window.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        var cancel = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Cancel",
            Kind = UiActionButton.ActionKind.Secondary,
        };
        cancel.Pressed += CloseOverlays;
        actions.AddChild(cancel);
        var confirm = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Confirm sample reset",
            Kind = UiActionButton.ActionKind.Danger,
        };
        confirm.Pressed += () =>
        {
            CloseOverlays();
            _lastDeletedCreation = "current sample run";
            _toast?.ShowMessage("Sample only: run reset confirmed.", "Undo");
        };
        actions.AddChild(confirm);
        stack.AddChild(actions);
        return stack;
    }

    private Control CreateTrainingSettingsBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        stack.AddChild(new Label { Text = "Choose how much time the sample gives each learner." });
        stack.AddChild(new UiSegmentedSwitch
        {
            Tokens = _tokens,
            Options = new[] { "Quick", "Standard", "Deep" },
            SelectedIndex = 0,
        });
        var done = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Done",
            Kind = UiActionButton.ActionKind.Primary,
        };
        done.Pressed += CloseOverlays;
        stack.AddChild(done);
        return stack;
    }

    private Control CreateRebuildBody(string creationName)
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        stack.AddChild(new Label
        {
            Text = $"Rebuild creates a new body and brain. {creationName} stays saved as the original version.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var actions = new HBoxContainer();
        var cancel = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Cancel",
            Kind = UiActionButton.ActionKind.Secondary,
        };
        cancel.Pressed += CloseOverlays;
        actions.AddChild(cancel);
        var confirm = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Create new body",
            Kind = UiActionButton.ActionKind.Danger,
        };
        confirm.Pressed += () =>
        {
            CloseOverlays();
            SetMode(1);
        };
        actions.AddChild(confirm);
        stack.AddChild(actions);
        return stack;
    }

    private Control CreateDuplicateBody(string name)
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        stack.AddChild(new Label
        {
            Text = "Copy brain is selected. Start fresh creates a new random brain while keeping the body.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var choice = new UiSegmentedSwitch
        {
            Tokens = _tokens,
            Options = new[] { "Copy brain", "Start fresh" },
            SelectedIndex = 0,
        };
        stack.AddChild(choice);
        var done = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Copy brain",
            Kind = UiActionButton.ActionKind.Primary,
        };
        choice.SelectionChanged += index => done.LabelText = index == 0 ? "Copy brain" : "Start fresh";
        done.Pressed += () =>
        {
            CloseOverlays();
            _toast?.ShowMessage($"Sample only: {name} duplicate created using {done.LabelText.ToLowerInvariant()}.");
        };
        stack.AddChild(done);
        return stack;
    }

    private Control CreateDeleteBody(string name)
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        stack.AddChild(new Label
        {
            Text = $"Hold-to-confirm is represented here by an explicit confirmation. {name} and its training data would be removed.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var actions = new HBoxContainer();
        var cancel = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Cancel",
            Kind = UiActionButton.ActionKind.Secondary,
        };
        cancel.Pressed += CloseOverlays;
        actions.AddChild(cancel);
        var confirm = new UiActionButton
        {
            Tokens = _tokens,
            LabelText = "Hold to delete",
            Kind = UiActionButton.ActionKind.Danger,
        };
        var holdTimer = new Godot.Timer { OneShot = true, WaitTime = 1.2f };
        holdTimer.Timeout += () =>
        {
            CloseOverlays();
            _lastDeletedCreation = name;
            _toast?.ShowMessage($"Sample only: {name} deleted.", "Undo", 4);
        };
        confirm.ButtonDown += () =>
        {
            confirm.LabelText = "Keep holding…";
            holdTimer.Start();
        };
        confirm.ButtonUp += () =>
        {
            if (holdTimer.TimeLeft > 0)
            {
                holdTimer.Stop();
                confirm.LabelText = "Hold to delete";
            }
        };
        stack.AddChild(holdTimer);
        actions.AddChild(confirm);
        stack.AddChild(actions);
        return stack;
    }

    private void RestoreDeletedCreation()
    {
        if (_lastDeletedCreation is null || _toast is null)
        {
            return;
        }

        var restoredName = _lastDeletedCreation;
        _lastDeletedCreation = null;
        _toast.ShowMessage($"Sample only: {restoredName} restored.");
    }

    private void ClearContent()
    {
        _watch = null;
        _build = null;
        _sampleView = null;
        foreach (var child in _content!.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }
}

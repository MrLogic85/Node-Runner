using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Sample-data host for the persistent Simulate/Build shell. It intentionally
/// keeps both child screens disconnected from game state.
/// </summary>
public partial class SampleFlowScreen : Control
{
    private Control? _content;
    private SimulateScreen? _simulate;
    private BuildScreen? _build;
    private UiSegmentedSwitch? _modeSwitch;
    private Control? _overlay;
    private Button? _overlayDismiss;
    private UiMenu? _overflowMenu;
    private UiNotification? _toast;
    private UiSheet? _sheet;
    private int _selectedMode;
    private Control? _sampleView;
    private TrainingPresentationViewModel? _presentation;
    private Godot.Timer? _activeHoldTimer;
    private UiButton? _activeHoldButton;
    private string _activeHoldLabel = string.Empty;

    public TrainingPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            _presentation = value;
            if (_simulate is not null)
            {
                _simulate.Presentation = value;
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
        _simulate = null;
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
            ShowSimulate();
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
            Color = UiThemeLookup.Color(this, UiTokens.Color.Background),
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
            CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
        };
        header.AddThemeConstantOverride("separation", 12);
        shell.AddChild(header);

        var title = new Label
        {
            Text = "NODE RUNNER",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", UiThemeLookup.Color(this, UiTokens.Color.Ink));
        header.AddChild(title);

        _modeSwitch = new UiSegmentedSwitch
        {
            Segments = [new() { Text = "Simulate" }, new() { Text = "Build" }],
            SelectedIndex = _selectedMode,
            CustomMinimumSize = new Vector2(208, UiSize.Control.Touch),
        };
        _modeSwitch.SelectionChanged += index => SetMode(index);
        header.AddChild(_modeSwitch);

        var menu = new UiButton
        {
            ContentLayout = UiButtonContentLayout.Stacked,
            IconId = UiIconId.More,
            TooltipText = "Open sample menu",
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

        _overflowMenu = new UiMenu
        {
            ZIndex = 10,
        };
        UiMenuItems.Populate(
            _overflowMenu,
            [
                new UiMenuItemSpec("Sample settings"),
                new UiMenuItemSpec(
                    "Sample start over",
                    UiIconId.Trash,
                    UiMenuActionItem.MenuItemKind.Danger),
                new UiMenuItemSpec("Creations"),
            ]);
        _overflowMenu.IndexClicked += index =>
        {
            string? action = index switch
            {
                0 => "training-settings",
                1 => "start-over",
                2 => "creations",
                _ => null,
            };
            if (action is null)
                return;

            _overflowMenu.Hide();
            OnOverflowAction(action);
        };
        _overlay.AddChild(_overflowMenu);

        _toast = new UiNotification { ZIndex = 11 };
        _overlay.AddChild(_toast);

        _sheet = new UiSheet
        {
            ZIndex = 12,
            CustomMinimumSize = new Vector2(460, 0),
        };
        _overlay.AddChild(_sheet);
        _sheet.Hide();
    }

    private void ShowSimulate()
    {
        if (_content is null)
        {
            return;
        }

        ClearContent();
        _simulate = new SimulateScreen
        {
            ShowTopBar = false,
            Hosted = true,
            Presentation = _presentation,
        };
        _simulate.BrainFocusRequested += () => ShowSheet("BrainFocus · Decides", CreateBrainFocusBody());
        _sampleView = _simulate;
        _content.AddChild(_simulate);
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
            ShowTopBar = false,
            Hosted = true,
            Presentation = CreateSampleConstructionPresentation(),
        };
        _build.TrainingRequested += () => SetMode(0);
        _sampleView = _build;
        _content.AddChild(_build);
    }

    private static ConstructionPresentationViewModel CreateSampleConstructionPresentation()
    {
        var construction = new ConstructionViewModel();
        construction.SetMaxCores(2);
        var rear = construction.PlaceNode(new Vector2D(-90, 20), 18);
        var mid = construction.PlaceNode(new Vector2D(0, -18), 18);
        var front = construction.PlaceNode(new Vector2D(90, 18), 18);
        construction.SelectNodeForBeam(rear);
        construction.SelectNodeForBeam(mid);
        construction.SelectNodeForBeam(mid);
        construction.SelectNodeForBeam(front);
        construction.ToggleCoreOnNode(rear);
        construction.ToggleCoreOnNode(front);
        construction.ActiveTool = ConstructionTool.Core;
        return new ConstructionPresentationViewModel(construction);
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
            ShowSimulate();
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
        var creations = GD.Load<PackedScene>("res://scenes/screens/CreationsScreen.tscn").Instantiate<CreationsScreen>();
        creations.OpenRequested += (_, _) => SetMode(0);
        creations.EditRequested += (_, name) => ShowEdit(name);
        creations.DuplicateRequested += (_, name) => ShowSheet("Duplicate " + name + "?", CreateDuplicateBody(name));
        creations.DeleteRequested += (_, name) => ShowSheet("Delete " + name + "?", CreateDeleteBody(name));
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
        var edit = new EditScreen { CreationName = creationName };
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
        CancelActiveHold();
        if (_overflowMenu is not null)
        {
            _overflowMenu.Hide();
        }

        if (_overlayDismiss is not null)
        {
            _overlayDismiss.Hide();
        }

        _toast?.Clear();
        _sheet?.Hide();
    }

    private void CancelActiveHold()
    {
        if (_activeHoldTimer is not null)
        {
            _activeHoldTimer.Stop();
        }

        if (_activeHoldButton is not null && !string.IsNullOrEmpty(_activeHoldLabel))
        {
            _activeHoldButton.Text = _activeHoldLabel;
        }

        _activeHoldTimer = null;
        _activeHoldButton = null;
        _activeHoldLabel = string.Empty;
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
            Text = "Hold the reset action to clear the current run. An Undo window keeps the action recoverable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        var cancel = new UiButton
        {
            Text = "Cancel",
            Kind = UiButtonKind.Secondary,
        };
        cancel.Pressed += CloseOverlays;
        actions.AddChild(cancel);
        var confirm = new UiButton
        {
            Text = "Hold to reset",
            Kind = UiButtonKind.Tertiary,
        };
        var holdTimer = new Godot.Timer { OneShot = true, WaitTime = 1.2f };
        _activeHoldTimer = holdTimer;
        _activeHoldButton = confirm;
        _activeHoldLabel = "Hold to reset";
        holdTimer.Timeout += () =>
        {
            _activeHoldTimer = null;
            _activeHoldButton = null;
            _activeHoldLabel = string.Empty;
            CloseOverlays();
            Notify("Sample only: run reset confirmed.");
        };
        confirm.ButtonDown += () =>
        {
            confirm.Text = "Keep holding…";
            holdTimer.Start();
        };
        confirm.ButtonUp += () =>
        {
            if (holdTimer.TimeLeft > 0)
            {
                holdTimer.Stop();
                confirm.Text = "Hold to reset";
            }
        };
        stack.AddChild(holdTimer);
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
            Segments = [new() { Text = "Quick" }, new() { Text = "Standard" }, new() { Text = "Deep" }],
            SelectedIndex = 0,
        });
        var done = new UiButton
        {
            Text = "Done",
            Kind = UiButtonKind.Primary,
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
        var cancel = new UiButton
        {
            Text = "Cancel",
            Kind = UiButtonKind.Secondary,
        };
        cancel.Pressed += CloseOverlays;
        actions.AddChild(cancel);
        var confirm = new UiButton
        {
            Text = "Create new body",
            Kind = UiButtonKind.Tertiary,
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

    private Control CreateBrainFocusBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        stack.AddChild(new Label { Text = "Tap a neuron to see which signal it is shaping." });
        var explanation = new Label
        {
            Text = "Hidden neuron 2 combines contact and body angle before the motor targets.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        var network = new Control { CustomMinimumSize = new Vector2(420, 150) };
        var selectedNeuron = 1;
        network.Draw += () =>
        {
            var left = new[] { new Vector2(50, 35), new Vector2(50, 105) };
            var middle = new[] { new Vector2(210, 20), new Vector2(210, 75), new Vector2(210, 130) };
            var right = new[] { new Vector2(370, 50), new Vector2(370, 105) };
            foreach (var input in left)
            {
                foreach (var neuron in middle)
                {
                    network.DrawLine(input, neuron, UiThemeLookup.Color(this, UiTokens.Color.Edge), 2);
                }
            }

            foreach (var neuron in middle)
            {
                foreach (var output in right)
                {
                    network.DrawLine(neuron, output, UiThemeLookup.Color(this, UiTokens.Color.Accent), 3);
                }
            }

            foreach (var node in left)
            {
                // Illustrative network diagram -- themes without effects drop the
                // glow treatment for flat schematic dots instead of hiding
                // them (#134).
                network.DrawCircle(
                    node,
                    UiThemeLookup.EffectsEnabled(this) ? 12 : 8,
                    UiThemeLookup.EffectsEnabled(this) ? UiGlow.FromBase(UiThemeLookup.Color(this, UiTokens.Color.Accent), true) : UiThemeLookup.Color(this, UiTokens.Color.Line));
            }

            for (var index = 0; index < middle.Length; index++)
            {
                network.DrawCircle(middle[index], UiThemeLookup.EffectsEnabled(this) ? 15 : 10, UiThemeLookup.EffectsEnabled(this) ? UiThemeLookup.Color(this, UiTokens.Color.Halo) : UiThemeLookup.Color(this, UiTokens.Color.LineStrong));
                if (index == selectedNeuron)
                {
                    network.DrawArc(middle[index], 22, 0, Mathf.Tau, 32, UiThemeLookup.Color(this, UiTokens.Color.Accent), 3);
                }
            }

            foreach (var node in right)
            {
                network.DrawCircle(node, 12, UiThemeLookup.Color(this, UiTokens.Color.Accent));
            }
        };
        stack.AddChild(network);
        var neurons = new HBoxContainer();
        neurons.AddThemeConstantOverride("separation", 8);
        var neuronButtons = new List<UiButton>();
        for (var index = 0; index < 3; index++)
        {
            var neuronIndex = index;
            var button = new UiButton
            {
                Text = $"Hidden {index + 1}",
                Kind = index == selectedNeuron
                    ? UiButtonKind.Primary
                    : UiButtonKind.Secondary,
            };
            button.Pressed += () =>
            {
                selectedNeuron = neuronIndex;
                for (var buttonIndex = 0; buttonIndex < neuronButtons.Count; buttonIndex++)
                {
                    neuronButtons[buttonIndex].Kind = buttonIndex == selectedNeuron
                        ? UiButtonKind.Primary
                        : UiButtonKind.Secondary;
                }
                explanation.Text = $"Hidden neuron {neuronIndex + 1} is highlighted; its weighted links shape the next motor targets.";
                network.QueueRedraw();
            };
            neuronButtons.Add(button);
            neurons.AddChild(button);
        }
        stack.AddChild(neurons);
        stack.AddChild(explanation);
        stack.AddChild(new Label { Text = "Inputs: core contact · body angle    Outputs: left joint · right joint" });
        var close = new UiButton
        {
            Text = "Back to SignalFlow",
            Kind = UiButtonKind.Primary,
        };
        close.Pressed += CloseOverlays;
        stack.AddChild(close);
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
            Segments = [new() { Text = "Copy brain" }, new() { Text = "Start fresh" }],
            SelectedIndex = 0,
        };
        stack.AddChild(choice);
        var done = new UiButton
        {
            Text = "Copy brain",
            Kind = UiButtonKind.Primary,
        };
        choice.SelectionChanged += index => done.Text = index == 0 ? "Copy brain" : "Start fresh";
        done.Pressed += () =>
        {
            CloseOverlays();
            Notify($"Sample only: {name} duplicate created using {done.Text.ToLowerInvariant()}.");
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
            Text = $"Hold to delete {name} and its training data. Undo remains available for 10 seconds.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var actions = new HBoxContainer();
        var cancel = new UiButton
        {
            Text = "Cancel",
            Kind = UiButtonKind.Secondary,
        };
        cancel.Pressed += CloseOverlays;
        actions.AddChild(cancel);
        var confirm = new UiButton
        {
            Text = "Hold to delete",
            Kind = UiButtonKind.Tertiary,
        };
        var holdTimer = new Godot.Timer { OneShot = true, WaitTime = 1.2f };
        _activeHoldTimer = holdTimer;
        _activeHoldButton = confirm;
        _activeHoldLabel = "Hold to delete";
        holdTimer.Timeout += () =>
        {
            _activeHoldTimer = null;
            _activeHoldButton = null;
            _activeHoldLabel = string.Empty;
            CloseOverlays();
            Notify($"Sample only: {name} deleted.");
        };
        confirm.ButtonDown += () =>
        {
            confirm.Text = "Keep holding…";
            holdTimer.Start();
        };
        confirm.ButtonUp += () =>
        {
            if (holdTimer.TimeLeft > 0)
            {
                holdTimer.Stop();
                confirm.Text = "Hold to delete";
            }
        };
        stack.AddChild(holdTimer);
        actions.AddChild(confirm);
        stack.AddChild(actions);
        return stack;
    }

    private void Notify(string message) =>
        _toast?.Enqueue(new UiNotificationSpec(UiPopupType.Default, "Sample flow", message));

    private void ClearContent()
    {
        _simulate = null;
        _build = null;
        _sampleView = null;
        foreach (var child in _content!.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }
}

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
    private int _selectedMode;

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
            ("start-over", "Sample start over", true));
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
        _overlay.AddChild(_toast);
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
    }

    private void RefreshOverlayLayout()
    {
        if (_overflowMenu is not null)
        {
            _overflowMenu.Position = new Vector2(Mathf.Max(24, Size.X - 240), 76);
        }
    }

    private void OnOverflowAction(string actionId)
    {
        CloseOverlays();
        if (_toast is null)
        {
            return;
        }

        var message = actionId == "start-over"
            ? "Sample only: start over would clear this run after confirmation."
            : "Sample only: settings would open as a sheet here.";
        _toast.ShowMessage(message);
    }

    private void ClearContent()
    {
        _watch = null;
        _build = null;
        foreach (var child in _content!.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
    }
}

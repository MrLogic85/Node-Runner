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
        header.AddChild(menu);

        _content = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        shell.AddChild(_content);
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

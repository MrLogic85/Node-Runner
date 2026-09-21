using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Reference top bar: Back, title, spacer, up to two actions and overflow.</summary>
public partial class UiTopBar : UiPanel
{
    [Signal]
    public delegate void BackPressedEventHandler();

    [Signal]
    public delegate void OverflowPressedEventHandler();

    private UiTokens _tokens = UiTokens.Neon;
    private UiIconButton? _backButton;
    private Label? _titleLabel;
    private HBoxContainer? _actionHost;
    private UiIconButton? _overflowButton;
    private UiIconButton[] _pendingActions = [];
    private string _titleText = "Screen";
    private bool _showBack = true;
    private bool _showOverflow;

    [Export]
    public string TitleText
    {
        get => _titleText;
        set
        {
            _titleText = value;
            Refresh();
        }
    }

    [Export]
    public bool ShowBack
    {
        get => _showBack;
        set
        {
            _showBack = value;
            Refresh();
        }
    }

    [Export]
    public bool ShowOverflow
    {
        get => _showOverflow;
        set
        {
            _showOverflow = value;
            Refresh();
        }
    }

    public new UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            base.Tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        Variant = UiSurfaceContracts.FrameVariant.Frame;
        CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        AddChild(row);

        _backButton = new UiIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.Back,
            AccessibleLabel = "Back",
        };
        _backButton.Pressed += () => EmitSignal(SignalName.BackPressed);
        row.AddChild(_backButton);

        _titleLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(_titleLabel);

        _actionHost = new HBoxContainer();
        _actionHost.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        row.AddChild(_actionHost);

        _overflowButton = new UiIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.More,
            AccessibleLabel = "More",
        };
        _overflowButton.Pressed += () => EmitSignal(SignalName.OverflowPressed);
        row.AddChild(_overflowButton);

        ApplyActions(_pendingActions);
        Refresh();
    }

    public void SetActions(params UiIconButton[] actions)
    {
        _pendingActions = actions.Take(2).ToArray();
        if (_actionHost is null)
        {
            return;
        }

        ApplyActions(_pendingActions);
    }

    private void ApplyActions(IReadOnlyList<UiIconButton> actions)
    {
        if (_actionHost is null)
        {
            return;
        }

        foreach (var child in _actionHost.GetChildren())
        {
            _actionHost.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var action in actions)
        {
            action.Tokens = _tokens;
            _actionHost.AddChild(action);
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight);
        if (_backButton is not null)
        {
            _backButton.Tokens = _tokens;
            _backButton.Visible = ShowBack;
        }

        if (_titleLabel is not null)
        {
            _titleLabel.Text = TitleText;
            _tokens.ApplyTextStyle(_titleLabel, _tokens.HeadingText);
            _titleLabel.AddThemeColorOverride("font_color", _tokens.Ink);
        }

        if (_actionHost is not null)
        {
            _actionHost.AddThemeConstantOverride("separation", (int)_tokens.Space1);
            foreach (var child in _actionHost.GetChildren().OfType<UiIconButton>())
            {
                child.Tokens = _tokens;
            }
        }

        if (_overflowButton is not null)
        {
            _overflowButton.Tokens = _tokens;
            _overflowButton.Visible = ShowOverflow;
        }
    }
}

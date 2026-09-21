using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Shared themed foundation for labelled buttons.</summary>
public partial class UiButton : Button
{
    [Signal]
    public delegate void ActivatedEventHandler();

    private const float _disabledOpacity = 0.5f;
    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = string.Empty;
    private UiIconId? _iconId;
    private bool _enabled = true;
    private UiColor _borderColor = UiColor.LineStrong;
    private UiColor _contentColor = UiColor.Ink;
    private UiSpace _horizontalPadding = UiSpace.Space4;
    private UiSpace _verticalPadding = UiSpace.None;
    private bool _filled;
    private float _progress = -1f;
    private float _holdDurationSeconds;
    private double _holdElapsedSeconds;
    private bool _isHolding;
    private Panel? _progressBackground;
    private Panel? _progressFill;

    public UiButton()
    {
        Alignment = HorizontalAlignment.Center;
    }

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            RefreshStyle();
        }
    }

    public UiIconId? IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiColor BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiColor ContentColor
    {
        get => _contentColor;
        set
        {
            _contentColor = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiSpace HorizontalPadding
    {
        get => _horizontalPadding;
        set
        {
            _horizontalPadding = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiSpace VerticalPadding
    {
        get => _verticalPadding;
        set
        {
            _verticalPadding = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool Filled
    {
        get => _filled;
        set
        {
            _filled = value;
            RefreshStyle();
        }
    }

    /// <summary>A value below zero hides progress; otherwise values are clamped to 0..1.</summary>
    [Export(PropertyHint.Range, "-1,1,0.01")]
    public float Progress
    {
        get => _progress;
        set
        {
            _progress = value < 0 ? -1 : Mathf.Clamp(value, 0, 1);
            RefreshStyle();
        }
    }

    /// <summary>Zero uses normal click activation; a positive value requires holding for this many seconds.</summary>
    [Export(PropertyHint.Range, "0,5,0.05")]
    public float HoldDurationSeconds
    {
        get => _holdDurationSeconds;
        set
        {
            _holdDurationSeconds = Mathf.Max(value, 0);
            _holdElapsedSeconds = 0;
            _isHolding = false;
            Progress = _holdDurationSeconds > 0 ? 0 : -1;
            SetProcess(false);
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshStyle();
        }
    }

    public override void _Ready()
    {
        Resized += LayoutProgress;
        Pressed += HandlePressed;
        ButtonDown += BeginHold;
        ButtonUp += EndHold;
        SetProcess(false);
        RefreshStyle();
    }

    public override void _ExitTree()
    {
        EndHold();
        Resized -= LayoutProgress;
        Pressed -= HandlePressed;
        ButtonDown -= BeginHold;
        ButtonUp -= EndHold;
    }

    public override void _Process(double delta)
    {
        if (!_isHolding)
        {
            return;
        }

        _holdElapsedSeconds += delta;
        Progress = UiComponentContracts.HoldProgress(
            _holdElapsedSeconds,
            HoldDurationSeconds);
        if (Progress < 1)
        {
            return;
        }

        _isHolding = false;
        SetProcess(false);
        EmitSignal(SignalName.Activated);
    }

    protected virtual string DisplayText => LabelText.ToUpperInvariant();

    protected virtual string AccessibleDescription => string.Empty;

    protected virtual UiIconSize DisplayIconSize => UiIconSize.Standard;

    protected virtual Vector2 MinimumSize => new(0, Tokens.TouchTarget);

    protected virtual float HorizontalVisibleInset => 0;

    protected void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Disabled = !Enabled;
        Text = DisplayText;
        TooltipText = AccessibleDescription;
        CustomMinimumSize = MinimumSize;
        Tokens.ApplyTextStyle(this, Tokens.LabelText);
        AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(Tokens));

        var content = Resolve(ContentColor);
        AddThemeColorOverride("font_color", content);
        AddThemeColorOverride("font_hover_color", content);
        AddThemeColorOverride("font_pressed_color", content);
        AddThemeColorOverride("font_disabled_color", UiTokens.MultiplyAlpha(content, _disabledOpacity));
        if (IconId is { } icon)
        {
            UiIcons.Apply(this, icon, DisplayIconSize, content);
            AddThemeColorOverride("icon_disabled_color", UiTokens.MultiplyAlpha(content, _disabledOpacity));
        }
        else
        {
            Icon = null;
        }

        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("focus", Tokens.FocusRingStyle());
        AddThemeStyleboxOverride("disabled", CreateStyle(false, _disabledOpacity));
        RefreshProgress();
    }

    private void HandlePressed()
    {
        if (Enabled && HoldDurationSeconds <= 0)
        {
            EmitSignal(SignalName.Activated);
        }
    }

    private void BeginHold()
    {
        if (!Enabled || HoldDurationSeconds <= 0)
        {
            return;
        }

        _holdElapsedSeconds = 0;
        _isHolding = true;
        Progress = 0;
        SetProcess(true);
    }

    private void EndHold()
    {
        if (!_isHolding)
        {
            return;
        }

        _holdElapsedSeconds = 0;
        _isHolding = false;
        Progress = 0;
        SetProcess(false);
    }

    private StyleBoxFlat CreateStyle(bool active, float opacity = 1)
    {
        var border = Resolve(BorderColor);
        var background = Filled
            ? border
            : active ? Tokens.AccentSoft : Tokens.PanelRaised;
        if (Progress >= 0)
        {
            background = Colors.Transparent;
        }

        return InsetToVisibleControl(Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            UiTokens.MultiplyAlpha(border, opacity),
            horizontalPadding: Resolve(HorizontalPadding),
            verticalPadding: Resolve(VerticalPadding)));
    }

    private StyleBoxFlat CreateBackgroundStyle(Color background, float opacity) =>
        Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            Colors.Transparent,
            borderWidth: 0,
            horizontalPadding: Resolve(HorizontalPadding),
            verticalPadding: Resolve(VerticalPadding));

    private StyleBoxFlat CreateProgressStyle(float opacity)
    {
        var style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(
                Resolve(BorderColor),
                UiComponentContracts.ButtonProgressOpacity * opacity),
            Colors.Transparent,
            borderWidth: 0);
        if (Progress < 1)
        {
            style.CornerRadiusTopRight = 0;
            style.CornerRadiusBottomRight = 0;
        }

        return style;
    }

    private void RefreshProgress()
    {
        EnsureProgressLayers();
        var visible = Progress >= 0;
        _progressBackground!.Visible = visible;
        _progressFill!.Visible = visible && Progress > 0;
        if (!visible)
        {
            return;
        }

        var opacity = Enabled ? 1f : _disabledOpacity;
        var background = Filled ? Resolve(BorderColor) : Tokens.PanelRaised;
        _progressBackground.AddThemeStyleboxOverride(
            "panel",
            CreateBackgroundStyle(background, opacity));
        _progressFill.AddThemeStyleboxOverride("panel", CreateProgressStyle(opacity));
        LayoutProgress();
    }

    private void EnsureProgressLayers()
    {
        if (_progressBackground is not null)
        {
            return;
        }

        _progressBackground = CreateProgressLayer();
        _progressFill = CreateProgressLayer();
        AddChild(_progressBackground);
        AddChild(_progressFill);
    }

    private static Panel CreateProgressLayer() =>
        new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ShowBehindParent = true,
        };

    private void LayoutProgress()
    {
        if (_progressBackground is null || _progressFill is null)
        {
            return;
        }

        var verticalInset = (Size.Y - Tokens.ControlHeight) * 0.5f;
        var visibleWidth = Size.X - (HorizontalVisibleInset * 2);
        _progressBackground.Position = new Vector2(HorizontalVisibleInset, verticalInset);
        _progressBackground.Size = new Vector2(visibleWidth, Tokens.ControlHeight);
        _progressFill.Position = new Vector2(HorizontalVisibleInset, verticalInset);
        _progressFill.Size = new Vector2(visibleWidth * Mathf.Max(Progress, 0), Tokens.ControlHeight);
    }

    private StyleBoxFlat InsetToVisibleControl(StyleBoxFlat style)
    {
        var verticalInset = (Tokens.TouchTarget - Tokens.ControlHeight) * 0.5f;
        style.ExpandMarginLeft = -HorizontalVisibleInset;
        style.ExpandMarginTop = -verticalInset;
        style.ExpandMarginRight = -HorizontalVisibleInset;
        style.ExpandMarginBottom = -verticalInset;
        return style;
    }

    private Color Resolve(UiColor color) => color switch
    {
        UiColor.LineStrong => Tokens.LineStrong,
        UiColor.Ink => Tokens.Ink,
        UiColor.Muted => Tokens.Muted,
        UiColor.Accent => Tokens.Accent,
        UiColor.OnAccent => Tokens.OnAccent,
        UiColor.Danger => Tokens.Danger,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
    };

    private int Resolve(UiSpace space) => space switch
    {
        UiSpace.None => 0,
        UiSpace.Space1 => (int)Tokens.Space1,
        UiSpace.Space2 => (int)Tokens.Space2,
        UiSpace.Space3 => (int)Tokens.Space3,
        UiSpace.Space4 => (int)Tokens.Space4,
        UiSpace.Space5 => (int)Tokens.Space5,
        _ => throw new ArgumentOutOfRangeException(nameof(space), space, null),
    };
}

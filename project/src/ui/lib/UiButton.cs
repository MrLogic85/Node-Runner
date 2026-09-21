using Godot;

namespace NodeRunner.Ui.Lib;

public enum UiButtonContentLayout
{
    Row,
    Stack,
}

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
    private bool _on;
    private UiButtonContentLayout _contentLayout;
    private float _progress = -1f;
    private float _holdDurationSeconds;
    private double _holdElapsedSeconds;
    private bool _isHolding;
    private Panel? _progressBackground;
    private Panel? _progressFill;
    private VBoxContainer? _stackContent;
    private TextureRect? _stackIcon;
    private Label? _stackLabel;

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
            if (!value)
            {
                EndHold();
            }

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

    /// <summary>Selected state using the canonical accent-soft fill, accent border, and glow.</summary>
    [Export]
    public bool On
    {
        get => _on;
        set
        {
            _on = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiButtonContentLayout ContentLayout
    {
        get => _contentLayout;
        set
        {
            _contentLayout = value;
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
        if (!_isHolding || !Enabled)
        {
            EndHold();
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

    protected virtual Vector2 MinimumSize => ContentLayout switch
    {
        UiButtonContentLayout.Row => new(0, Tokens.TouchTarget),
        UiButtonContentLayout.Stack => new(Tokens.TouchTarget, Tokens.TouchTarget),
        _ => throw new ArgumentOutOfRangeException(nameof(ContentLayout), ContentLayout, null),
    };

    protected virtual float VisibleControlSize => ContentLayout switch
    {
        UiButtonContentLayout.Row => Tokens.ControlHeight,
        UiButtonContentLayout.Stack => Tokens.TouchTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(ContentLayout), ContentLayout, null),
    };

    protected virtual float HorizontalVisibleInset => 0;

    protected virtual float VerticalVisibleInset =>
        (MinimumSize.Y - VisibleControlSize) * 0.5f;

    protected virtual HorizontalAlignment DisplayIconAlignment =>
        ContentLayout == UiButtonContentLayout.Stack
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;

    protected void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Disabled = !Enabled;
        TooltipText = AccessibleDescription;
        CustomMinimumSize = MinimumSize;
        ClipText = ContentLayout == UiButtonContentLayout.Stack;
        IconAlignment = DisplayIconAlignment;
        VerticalIconAlignment = ContentLayout == UiButtonContentLayout.Stack
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
        Tokens.ApplyTextStyle(
            this,
            ContentLayout == UiButtonContentLayout.Stack
                ? Tokens.OverlineText
                : Tokens.LabelText);
        AddThemeConstantOverride(
            "h_separation",
            ContentLayout == UiButtonContentLayout.Stack
                ? (int)Tokens.Space1
                : UiSpacing.ControlGap(Tokens));

        var content = Resolve(ContentColor);
        AddThemeColorOverride("font_color", content);
        AddThemeColorOverride("font_hover_color", content);
        AddThemeColorOverride("font_pressed_color", content);
        AddThemeColorOverride("font_disabled_color", UiTokens.MultiplyAlpha(content, _disabledOpacity));
        EnsureStackContent();
        _stackContent!.Visible = ContentLayout == UiButtonContentLayout.Stack;
        if (ContentLayout == UiButtonContentLayout.Stack)
        {
            Text = string.Empty;
            Icon = null;
            RefreshStackContent(content);
        }
        else
        {
            Text = DisplayText;
            if (IconId is { } icon)
            {
                UiIcons.Apply(this, icon, DisplayIconSize, content);
                AddThemeColorOverride("icon_disabled_color", UiTokens.MultiplyAlpha(content, _disabledOpacity));
            }
            else
            {
                Icon = null;
            }
        }

        AddThemeStyleboxOverride("normal", CreateStyle());
        AddThemeStyleboxOverride("hover", CreateStyle());
        AddThemeStyleboxOverride("pressed", CreateStyle());
        AddThemeStyleboxOverride("focus", Tokens.FocusRingStyle());
        AddThemeStyleboxOverride("disabled", CreateStyle(_disabledOpacity, transparentBorder: true));
        RefreshProgress();
        if (!Enabled)
        {
            QueueRedraw();
        }
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

    public override void _Draw()
    {
        base._Draw();
        if (Enabled)
        {
            return;
        }

        var color = UiTokens.MultiplyAlpha(Resolve(BorderColor), _disabledOpacity);
        var inset = new Vector2(HorizontalVisibleInset, VerticalVisibleInset);
        var halfStroke = Tokens.StrokeHair * 0.5f;
        var left = inset.X + halfStroke;
        var top = inset.Y + halfStroke;
        var right = Size.X - HorizontalVisibleInset - halfStroke;
        var bottom = VerticalVisibleInset + VisibleControlSize - halfStroke;
        var radius = Tokens.RadiusMedium;
        DrawDashedRoundedRect(new Rect2(left, top, right - left, bottom - top), radius, color);
    }

    private void DrawDashedRoundedRect(Rect2 rect, float radius, Color color)
    {
        const float targetPatternLength = 7;
        const float dashRatio = 4f / 7f;
        var straightWidth = rect.Size.X - (radius * 2);
        var straightHeight = rect.Size.Y - (radius * 2);
        var perimeter = (straightWidth * 2) + (straightHeight * 2) + (Mathf.Tau * radius);
        var patternCount = Mathf.Max(1, Mathf.RoundToInt(perimeter / targetPatternLength));
        var patternLength = perimeter / patternCount;
        var dashLength = patternLength * dashRatio;

        for (var start = 0f; start < perimeter; start += patternLength)
        {
            var sampleCount = Mathf.Max(2, Mathf.CeilToInt(dashLength) + 1);
            var points = new Vector2[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var distance = start + (dashLength * index / (sampleCount - 1));
                points[index] = PointOnRoundedRect(rect, radius, distance);
            }

            DrawPolyline(points, color, Tokens.StrokeHair, antialiased: true);
        }
    }

    private static Vector2 PointOnRoundedRect(Rect2 rect, float radius, float distance)
    {
        var straightWidth = rect.Size.X - (radius * 2);
        var straightHeight = rect.Size.Y - (radius * 2);
        var arcLength = Mathf.Pi * radius * 0.5f;

        if (distance <= straightWidth)
        {
            return new Vector2(rect.Position.X + radius + distance, rect.Position.Y);
        }

        distance -= straightWidth;
        if (distance <= arcLength)
        {
            return ArcPoint(rect.Position + new Vector2(rect.Size.X - radius, radius), radius, -Mathf.Pi * 0.5f, distance);
        }

        distance -= arcLength;
        if (distance <= straightHeight)
        {
            return new Vector2(rect.End.X, rect.Position.Y + radius + distance);
        }

        distance -= straightHeight;
        if (distance <= arcLength)
        {
            return ArcPoint(rect.End - new Vector2(radius, radius), radius, 0, distance);
        }

        distance -= arcLength;
        if (distance <= straightWidth)
        {
            return new Vector2(rect.End.X - radius - distance, rect.End.Y);
        }

        distance -= straightWidth;
        if (distance <= arcLength)
        {
            return ArcPoint(new Vector2(rect.Position.X + radius, rect.End.Y - radius), radius, Mathf.Pi * 0.5f, distance);
        }

        distance -= arcLength;
        if (distance <= straightHeight)
        {
            return new Vector2(rect.Position.X, rect.End.Y - radius - distance);
        }

        distance -= straightHeight;
        return ArcPoint(rect.Position + new Vector2(radius, radius), radius, Mathf.Pi, distance);
    }

    private static Vector2 ArcPoint(Vector2 center, float radius, float startAngle, float distance)
    {
        var angle = startAngle + (distance / radius);
        return center + (Vector2.FromAngle(angle) * radius);
    }

    private StyleBoxFlat CreateStyle(float opacity = 1, bool transparentBorder = false)
    {
        var border = Resolve(BorderColor);
        var background = On
            ? CompositeOver(Tokens.AccentSoft, Tokens.Background)
            : Filled
            ? border
            : Tokens.PanelRaised;
        var styleBorder = On ? Tokens.Accent : border;
        if (Progress >= 0)
        {
            background = Colors.Transparent;
        }

        var style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            transparentBorder
                ? Colors.Transparent
                : UiTokens.MultiplyAlpha(styleBorder, opacity),
            borderWidth: On ? 2 : null,
            glow: Progress < 0 && (On || Filled),
            horizontalPadding: ContentLayout == UiButtonContentLayout.Stack
                ? 0
                : Resolve(HorizontalPadding),
            verticalPadding: ContentLayout == UiButtonContentLayout.Stack
                ? 0
                : Resolve(VerticalPadding));
        if (style.ShadowSize > 0)
        {
            ApplyButtonGlow(style);
        }

        return InsetToVisibleControl(style);
    }

    private StyleBoxFlat CreateBackgroundStyle(Color background, float opacity)
    {
        var style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            Colors.Transparent,
            borderWidth: 0,
            glow: On || Filled,
            horizontalPadding: Resolve(HorizontalPadding),
            verticalPadding: Resolve(VerticalPadding));
        if (style.ShadowSize > 0)
        {
            ApplyButtonGlow(style);
        }

        return style;
    }

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
        var background = On
            ? CompositeOver(Tokens.AccentSoft, Tokens.Background)
            : Filled
                ? Resolve(BorderColor)
                : Tokens.PanelRaised;
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

    private void EnsureStackContent()
    {
        if (_stackContent is not null)
        {
            return;
        }

        _stackContent = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _stackContent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stackIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(
                UiIcons.Pixels(UiIconSize.Large),
                UiIcons.Pixels(UiIconSize.Large)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        _stackLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        _stackContent.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        _stackContent.AddChild(_stackIcon);
        _stackContent.AddChild(_stackLabel);
        AddChild(_stackContent);
    }

    private void RefreshStackContent(Color content)
    {
        _stackContent!.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        _stackLabel!.Text = DisplayText;
        Tokens.ApplyTextStyle(_stackLabel, Tokens.OverlineText);
        var visibleContent = Enabled
            ? content
            : UiTokens.MultiplyAlpha(content, _disabledOpacity);
        _stackLabel.AddThemeColorOverride("font_color", visibleContent);
        _stackIcon!.Texture = IconId is { } icon ? UiIcons.Load(icon, UiIconSize.Large) : null;
        _stackIcon.SelfModulate = visibleContent;
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

        var verticalInset = VerticalVisibleInset;
        var visibleWidth = Size.X - (HorizontalVisibleInset * 2);
        _progressBackground.Position = new Vector2(HorizontalVisibleInset, verticalInset);
        _progressBackground.Size = new Vector2(visibleWidth, VisibleControlSize);
        _progressFill.Position = new Vector2(HorizontalVisibleInset, verticalInset);
        _progressFill.Size = new Vector2(visibleWidth * Mathf.Max(Progress, 0), VisibleControlSize);
    }

    private StyleBoxFlat InsetToVisibleControl(StyleBoxFlat style)
    {
        style.ExpandMarginLeft = -HorizontalVisibleInset;
        style.ExpandMarginTop = -VerticalVisibleInset;
        style.ExpandMarginRight = -HorizontalVisibleInset;
        style.ExpandMarginBottom = -VerticalVisibleInset;
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

    private static Color CompositeOver(Color foreground, Color background)
    {
        var inverseAlpha = 1 - foreground.A;
        return new Color(
            (foreground.R * foreground.A) + (background.R * inverseAlpha),
            (foreground.G * foreground.A) + (background.G * inverseAlpha),
            (foreground.B * foreground.A) + (background.B * inverseAlpha),
            1);
    }

    private static void ApplyButtonGlow(StyleBoxFlat style)
    {
        style.ShadowSize = (int)UiComponentContracts.ButtonGlowSize;
        style.ShadowColor = UiTokens.MultiplyAlpha(
            style.ShadowColor,
            UiComponentContracts.ButtonGlowOpacity);
    }
}

using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Shared themed foundation for labelled buttons.</summary>
public partial class UiButton : Button
{
    [Signal]
    public delegate void ActivatedEventHandler();

    private const float _disabledOpacity = 0.5f;
    private UiTokens _tokens = UiTokens.Neon;
    private UiButtonStyle _style = UiButtonStyle.Secondary;
    private string _labelText = string.Empty;
    private string _callerTooltip = string.Empty;
    private string _lastAppliedTooltip = string.Empty;
    private UiIconId? _iconId;
    private bool _enabled = true;
    private UiColor _borderColor = UiColor.LineStrong;
    private UiColor _contentColor = UiColor.Ink;
    private UiSpace _horizontalPadding = UiSpace.Space4;
    private UiSpace _verticalPadding = UiSpace.None;
    private bool _filled;
    private bool _on;
    private bool _compact;
    private string _badgeText = string.Empty;
    private UiButtonContentLayout _contentLayout;
    private SizeFlags _rowSizeFlagsHorizontal = SizeFlags.Fill;
    private SizeFlags _rowSizeFlagsVertical = SizeFlags.Fill;
    private float _progress = -1f;
    private float _holdDurationSeconds;
    private double _holdElapsedSeconds;
    private bool _isHolding;
    private bool _handlersConnected;
    private Control? _progressClip;
    private Panel? _progressBackground;
    private Control? _progressFillClip;
    private Panel? _progressFill;
    private VBoxContainer? _stackContent;
    private TextureRect? _stackIcon;
    private Label? _stackLabel;
    private Label? _badge;

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
    public UiButtonKind Kind
    {
        get => _style.Kind;
        set
        {
            _style = UiButtonStyle.For(value);
            RefreshStyle();
        }
    }

    /// <summary>Composes the semantic colours without coupling callers to rendering details.</summary>
    public UiButtonStyle Style
    {
        get => _style;
        set
        {
            _style = value;
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
            _style = _style with { BorderColor = value };
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
            _style = _style with { ContentColor = value };
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

    /// <summary>Selected state preserving the style background with its selected border and glow.</summary>
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
            if (_contentLayout != value)
            {
                if (value is UiButtonContentLayout.Stack or UiButtonContentLayout.Icon)
                {
                    _rowSizeFlagsHorizontal = SizeFlagsHorizontal;
                    _rowSizeFlagsVertical = SizeFlagsVertical;
                }
                else
                {
                    SizeFlagsHorizontal = _rowSizeFlagsHorizontal;
                    SizeFlagsVertical = _rowSizeFlagsVertical;
                }
            }

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
            var next = value < 0 ? -1 : Mathf.Clamp(value, 0, 1);
            if (next.Equals(_progress))
            {
                return;
            }

            var stayedVisible = _progress >= 0 && next >= 0;
            _progress = next;
            if (stayedVisible && IsInsideTree())
            {
                // Only the revealed fraction changed. Rebuilding the button
                // styleboxes here would invalidate its minimum size on every
                // hold frame and make containers re-sort mid-gesture.
                RefreshProgressLayout();
                return;
            }

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
            SetProcessInput(false);
            RefreshStyle();
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

    public override void _EnterTree()
    {
        base._EnterTree();
        if (_handlersConnected)
        {
            return;
        }

        Resized += LayoutProgress;
        Pressed += HandlePressed;
        ButtonDown += BeginHold;
        ButtonUp += EndHold;
        _handlersConnected = true;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        SetProcess(false);
        SetProcessInput(false);
        RefreshStyle();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!_isHolding)
        {
            return;
        }

        // Observe before a parent scroll container can consume the release or drag.
        switch (inputEvent)
        {
            case InputEventMouseMotion motion:
                CancelHoldOutside(motion.Position);
                break;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }:
                EndHold();
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationScrollBegin || what == NotificationDragBegin
            || what == NotificationFocusExit || what == NotificationApplicationFocusOut
            || (what == NotificationVisibilityChanged && !IsVisibleInTree()))
        {
            EndHold();
        }
    }

    private void CancelHoldOutside(Vector2 viewportPosition)
    {
        var localPosition = GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
        if (!new Rect2(Vector2.Zero, Size).HasPoint(localPosition))
        {
            EndHold();
        }
    }

    public override void _ExitTree()
    {
        EndHold();
        if (_handlersConnected)
        {
            Resized -= LayoutProgress;
            Pressed -= HandlePressed;
            ButtonDown -= BeginHold;
            ButtonUp -= EndHold;
            _handlersConnected = false;
        }

        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (!_isHolding || !Enabled || !IsPressed() || !IsVisibleInTree())
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
        SetProcessInput(false);
        OnHoldCompleted();
        Activate();
    }

    protected virtual string DisplayText => LabelText.ToUpperInvariant();

    protected virtual string AccessibleDescription => string.Empty;

    protected virtual UiIconSize DisplayIconSize => UiIconSize.Standard;

    protected virtual Vector2 MinimumSize => ContentLayout switch
    {
        UiButtonContentLayout.Row => new(0, Tokens.TouchTarget),
        UiButtonContentLayout.Icon => new(Tokens.TouchTarget, Tokens.TouchTarget),
        UiButtonContentLayout.Stack => new(Tokens.TouchTarget, Tokens.TouchTarget),
        _ => throw new ArgumentOutOfRangeException(nameof(ContentLayout), ContentLayout, null),
    };

    protected virtual float VisibleControlSize => ContentLayout switch
    {
        UiButtonContentLayout.Row => UiButtonMetrics.From(Tokens).VisibleControlSize(ContentLayout, Compact),
        UiButtonContentLayout.Icon => UiButtonMetrics.From(Tokens).VisibleControlSize(ContentLayout, Compact),
        UiButtonContentLayout.Stack => Tokens.TouchTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(ContentLayout), ContentLayout, null),
    };

    protected virtual float HorizontalVisibleInset =>
        ContentLayout == UiButtonContentLayout.Icon
            ? (MinimumSize.X - VisibleControlSize) * 0.5f
            : 0;

    protected virtual float VerticalVisibleInset =>
        (MinimumSize.Y - VisibleControlSize) * 0.5f;

    protected virtual HorizontalAlignment DisplayIconAlignment =>
        ContentLayout is UiButtonContentLayout.Stack or UiButtonContentLayout.Icon
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;

    protected virtual UiTokens.TextStyle DisplayTextStyle =>
        ContentLayout == UiButtonContentLayout.Stack
            ? Tokens.OverlineText
            : Tokens.LabelText;

    /// <summary>Uses the compact control size for row and icon layouts.</summary>
    [Export]
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            RefreshStyle();
        }
    }

    /// <summary>Optional halo count displayed just outside the upper-right corner.</summary>
    [Export]
    public string BadgeText
    {
        get => _badgeText;
        set
        {
            _badgeText = value;
            RefreshStyle();
        }
    }

    protected void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Disabled = !Enabled;
        if (TooltipText != _lastAppliedTooltip)
        {
            _callerTooltip = TooltipText;
        }

        TooltipText = string.IsNullOrEmpty(AccessibleDescription)
            ? _callerTooltip
            : AccessibleDescription;
        _lastAppliedTooltip = TooltipText;
        CustomMinimumSize = MinimumSize;
        if (ContentLayout is UiButtonContentLayout.Stack or UiButtonContentLayout.Icon)
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        ClipText = ContentLayout == UiButtonContentLayout.Stack;
        IconAlignment = DisplayIconAlignment;
        VerticalIconAlignment = ContentLayout == UiButtonContentLayout.Stack
            ? VerticalAlignment.Top
            : VerticalAlignment.Center;
        Tokens.ApplyTextStyle(this, DisplayTextStyle);
        AddThemeConstantOverride(
            "h_separation",
            ContentLayout == UiButtonContentLayout.Stack
                ? (int)Tokens.Space1
                : UiSpacing.ControlGap(Tokens));

        var content = _style.Resolve(Tokens).Content;
        AddThemeColorOverride("font_color", content);
        AddThemeColorOverride("font_hover_color", content);
        AddThemeColorOverride("font_pressed_color", content);
        AddThemeColorOverride("font_disabled_color", UiTokens.MultiplyAlpha(content, _disabledOpacity));
        EnsureProgressLayers();
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
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        AddThemeStyleboxOverride("disabled", CreateStyle(_disabledOpacity, transparentBorder: true));
        RefreshProgress();
        RefreshSelectedGlow();
        RefreshBadge();
        if (!Enabled)
        {
            QueueRedraw();
        }
    }

    protected virtual bool CanBeginHold => true;

    protected virtual void OnHoldStarted()
    {
    }

    protected virtual void OnHoldCancelled()
    {
    }

    protected virtual void OnHoldCompleted()
    {
    }

    protected virtual void OnActivated()
    {
    }

    private void Activate()
    {
        OnActivated();
        EmitSignal(SignalName.Activated);
    }

    private void HandlePressed()
    {
        if (Enabled && HoldDurationSeconds <= 0)
        {
            Activate();
        }
    }

    private void BeginHold()
    {
        if (!Enabled || HoldDurationSeconds <= 0 || !CanBeginHold)
        {
            return;
        }

        _holdElapsedSeconds = 0;
        _isHolding = true;
        Progress = 0;
        SetProcess(true);
        SetProcessInput(true);
        OnHoldStarted();
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
        SetProcessInput(false);
        OnHoldCancelled();
    }

    public override void _Draw()
    {
        base._Draw();
        if (Enabled && On && Tokens.EffectsEnabled)
        {
            DrawSelectedGlow();
        }

        if (Enabled)
        {
            return;
        }

        var style = _style.Resolve(Tokens);
        var color = UiTokens.MultiplyAlpha(On ? style.Selected : style.Border, _disabledOpacity);
        var inset = new Vector2(HorizontalVisibleInset, VerticalVisibleInset);
        var halfStroke = Tokens.StrokeHair * 0.5f;
        var left = inset.X + halfStroke;
        var top = inset.Y + halfStroke;
        var right = Size.X - HorizontalVisibleInset - halfStroke;
        var bottom = VerticalVisibleInset + VisibleControlSize - halfStroke;
        var radius = Tokens.RadiusMedium;
        UiDashedBorder.DrawRoundedRect(this, new Rect2(left, top, right - left, bottom - top), radius, color, Tokens.StrokeHair);
    }

    private StyleBoxFlat CreateStyle(float opacity = 1, bool transparentBorder = false)
    {
        var visual = _style.Resolve(Tokens);
        var background = visual.Background;
        var styleBorder = _style.BorderFor(Tokens, On);
        if (Progress >= 0)
        {
            background = Colors.Transparent;
        }

        var style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            transparentBorder
                ? Colors.Transparent
                : UiTokens.MultiplyAlpha(styleBorder, opacity),
            borderWidth: transparentBorder ? 0 : On ? Tokens.ButtonSelectedStroke : null,
            glow: false,
            horizontalPadding: ContentLayout == UiButtonContentLayout.Stack
                ? 0
            : ResolveSpace(HorizontalPadding),
            verticalPadding: ContentLayout == UiButtonContentLayout.Stack
                ? 0
                : ResolveSpace(VerticalPadding));
        var glowColor = _style.GlowBaseFor(Tokens, On, Enabled);
        if (glowColor is { } color)
        {
            UiGlow.ApplyButtonGlow(style, color, Tokens.EffectsEnabled);
        }

        return InsetToVisibleControl(style);
    }

    private StyleBoxFlat CreateBackgroundStyle(Color background, float opacity)
    {
        var style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            Colors.Transparent,
            borderWidth: 0,
            glow: false);
        return style;
    }

    private StyleBoxFlat CreateProgressStyle(float opacity)
    {
        var style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(
                _style.Resolve(Tokens).Selected,
                UiComponentContracts.ButtonProgressOpacity * opacity),
            Colors.Transparent,
            borderWidth: 0);

        // The fill always spans the whole visible frame and keeps the frame's
        // corner radii; the revealed fraction is produced by clipping. Sizing
        // the fill itself would make Godot shrink the corner radii to fit the
        // narrow rect, so early hold frames would bleed outside the rounded
        // contour of the button.
        return style;
    }

    private void RefreshProgressLayout()
    {
        EnsureProgressLayers();
        LayoutProgress();
        _progressFill!.Visible = Progress > 0;
    }

    private void RefreshProgress()
    {
        EnsureProgressLayers();
        var visible = Progress >= 0;
        LayoutProgress();
        _progressClip!.Visible = visible;
        _progressBackground!.Visible = visible;
        _progressFill!.Visible = visible && Progress > 0;
        if (!visible)
        {
            return;
        }

        var opacity = Enabled ? 1f : _disabledOpacity;
        var background = _style.Resolve(Tokens).Background;
        _progressBackground.AddThemeStyleboxOverride(
            "panel",
            CreateBackgroundStyle(background, opacity));
        _progressFill.AddThemeStyleboxOverride("panel", CreateProgressStyle(opacity));
    }

    private void EnsureProgressLayers()
    {
        if (_progressBackground is not null)
        {
            return;
        }

        _progressClip = new Control
        {
            // Only hold layers must stay within the visible button frame;
            // badges and the intentional outer glow may extend beyond it.
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
            ShowBehindParent = true,
        };
        _progressBackground = CreateProgressLayer();
        _progressFillClip = new Control
        {
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _progressFill = CreateProgressLayer();
        _progressFillClip.AddChild(_progressFill);
        _progressClip.AddChild(_progressBackground);
        _progressClip.AddChild(_progressFillClip);
        AddChild(_progressClip);
    }

    private void RefreshSelectedGlow()
    {
        QueueRedraw();
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

    private void RefreshBadge()
    {
        if (_badge is null)
        {
            _badge = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 1,
            };
            AddChild(_badge);
        }

        var metrics = UiButtonMetrics.From(Tokens);
        _badge.Visible = !string.IsNullOrWhiteSpace(BadgeText);
        if (!_badge.Visible)
        {
            return;
        }

        _badge.Text = BadgeText;
        _badge.CustomMinimumSize = new Vector2(metrics.BadgeMinimumSize, metrics.BadgeMinimumSize);
        _badge.Size = _badge.CustomMinimumSize;
        _badge.Position = metrics.BadgePosition(
            Size,
            ContentLayout,
            Compact);
        Tokens.ApplyTextStyle(_badge, Tokens.CaptionText);
        _badge.AddThemeColorOverride("font_color", Tokens.Background);
        _badge.AddThemeStyleboxOverride(
            "normal",
            Tokens.ControlStyle(Tokens.Halo, Colors.Transparent, borderWidth: 0, radius: Tokens.RadiusPill));
    }

    private void RefreshStackContent(Color content)
    {
        var hasLabel = !string.IsNullOrWhiteSpace(LabelText);
        _stackContent!.AddThemeConstantOverride(
            "separation",
            hasLabel ? (int)Tokens.Space1 : 0);
        _stackLabel!.Text = DisplayText;
        _stackLabel.Visible = hasLabel;
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
        };

    private void LayoutProgress()
    {
        if (_progressClip is null
            || _progressBackground is null
            || _progressFillClip is null
            || _progressFill is null)
        {
            return;
        }

        var frame = UiButtonMetrics.From(Tokens).VisibleFrame(
            Size,
            ContentLayout,
            Compact);
        var layout = UiButtonMetrics.ProgressLayout(frame, Progress);
        _progressClip.Position = frame.Position;
        _progressClip.Size = layout.Fill.Size;
        _progressBackground.Position = layout.Fill.Position;
        _progressBackground.Size = layout.Fill.Size;
        _progressFillClip.Position = layout.Reveal.Position;
        _progressFillClip.Size = layout.Reveal.Size;

        // The fill keeps the full rounded frame geometry and is offset back by
        // the reveal window's origin, so only the revealed fraction is visible.
        _progressFill.Position = layout.Fill.Position - layout.Reveal.Position;
        _progressFill.Size = layout.Fill.Size;
        RefreshBadge();
    }

    private void DrawSelectedGlow()
    {
        var baseColor = UiGlow.FromButtonBase(_style.Resolve(Tokens).Selected, enabled: true);
        var opacity = Enabled ? 1f : _disabledOpacity;
        var inset = new Vector2(HorizontalVisibleInset, VerticalVisibleInset);
        for (var depth = 0; depth < UiGlow.InsetExtent; depth++)
        {
            var strength = 1f - (depth / (float)UiGlow.InsetExtent);
            var color = UiTokens.MultiplyAlpha(baseColor, opacity * strength * strength);
            var rect = new Rect2(
                inset.X + depth,
                inset.Y + depth,
                Size.X - (HorizontalVisibleInset * 2) - (depth * 2),
                VisibleControlSize - (depth * 2));
            if (rect.Size.X <= 0 || rect.Size.Y <= 0)
            {
                break;
            }

            DrawRoundedRectOutline(rect, Mathf.Max(0, Tokens.RadiusMedium - depth), color);
        }
    }

    private void DrawRoundedRectOutline(Rect2 rect, float radius, Color color)
    {
        if (radius <= 0)
        {
            DrawRect(rect, color, filled: false, width: Tokens.StrokeHair, antialiased: true);
            return;
        }

        var perimeter = ((rect.Size.X - (radius * 2)) * 2)
            + ((rect.Size.Y - (radius * 2)) * 2)
            + (Mathf.Tau * radius);
        var sampleCount = Mathf.Max(12, Mathf.CeilToInt(perimeter / Tokens.Space1));
        var points = new Vector2[sampleCount + 1];
        for (var index = 0; index <= sampleCount; index++)
        {
            points[index] = UiDashedBorder.PointOnRoundedRect(rect, radius, perimeter * index / sampleCount);
        }

        DrawPolyline(points, color, Tokens.StrokeHair, antialiased: true);
    }

    private StyleBoxFlat InsetToVisibleControl(StyleBoxFlat style)
    {
        style.ExpandMarginLeft = -HorizontalVisibleInset;
        style.ExpandMarginTop = -VerticalVisibleInset;
        style.ExpandMarginRight = -HorizontalVisibleInset;
        style.ExpandMarginBottom = -VerticalVisibleInset;
        return style;
    }

    private int ResolveSpace(UiSpace space) => space switch
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

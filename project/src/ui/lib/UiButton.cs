using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical button with row, compact row or stacked content and optional hold activation.</summary>
[Tool]
[GlobalClass]
public sealed partial class UiButton : Button
{
    [Signal]
    public delegate void ActivatedEventHandler();

    private const float _disabledOpacity = 0.5f;
    private UiButtonStyle _style = UiButtonStyle.Secondary;
    private UiButtonKind _kind = UiButtonKind.Secondary;
    private string _labelText = string.Empty;
    private UiIconId _iconId = UiIconId.None;
    private bool _selected;
    private string _badgeText = string.Empty;
    private UiButtonContentLayout _contentLayout;
    private SizeFlags _rowSizeFlagsHorizontal = SizeFlags.Fill;
    private bool _squareContent;
    private float _progress = -1f;
    private float _holdDurationSeconds = 0.8f;
    private bool _holdToActivate;
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

    public UiButton() => Alignment = HorizontalAlignment.Center;

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

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid button icon: {value}. Keeping {_iconId}.");
                return;
            }
            _iconId = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiButtonKind Kind
    {
        get => _kind;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid button kind: {value}. Keeping {_style.Kind}.");
                return;
            }

            _kind = value;
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
            _kind = value.Kind;
            RefreshStyle();
        }
    }

    [Export]
    public UiButtonContentLayout ContentLayout
    {
        get => _contentLayout;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid button layout: {value}. Keeping {_contentLayout}.");
                return;
            }

            _contentLayout = value;
            RefreshStyle();
        }
    }

    /// <summary>Selected state preserving the style background with its selected border and glow.</summary>
    [Export]
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool HoldToActivate
    {
        get => _holdToActivate;
        set
        {
            EndHold();
            _holdToActivate = value;
            Progress = value ? 0 : -1;
        }
    }

    /// <summary>A value below zero hides progress; otherwise values are clamped to 0..1.</summary>
    private float Progress
    {
        get => _progress;
        set
        {
            float next = value < 0 ? -1 : Mathf.Clamp(value, 0, 1);
            if (next.Equals(_progress))
                return;

            bool stayedVisible = _progress >= 0 && next >= 0;
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

    /// <summary>Time required when HoldToActivate is enabled.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")]
    public float HoldDurationSeconds
    {
        get => _holdDurationSeconds;
        set
        {
            _holdDurationSeconds = Mathf.Max(value, 0);
            EndHold();
            Progress = HoldToActivate ? 0 : -1;
        }
    }

    public UiTokens Tokens
    {
        get;
        set
        {
            field = value;
            RefreshStyle();
        }
    } = UiTokens.Neon;

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() is "text" or "icon")
        {
            // Derived from LabelText/IconId: visible for inspection, but not authored or saved.
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)((usage | PropertyUsageFlags.ReadOnly) & ~PropertyUsageFlags.Storage);
        }
        else if (property["name"].AsString() == "flat")
        {
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)(usage & ~PropertyUsageFlags.Editor);
        }
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        if (_handlersConnected)
            return;

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
            return;

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
        Vector2 localPosition = GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
        if (!new Rect2(Vector2.Zero, Size).HasPoint(localPosition))
            EndHold();
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
        if (!_isHolding || !HoldToActivate || Disabled || !IsPressed() || !IsVisibleInTree())
        {
            EndHold();
            return;
        }

        _holdElapsedSeconds += delta;
        Progress = UiComponentContracts.HoldProgress(
            _holdElapsedSeconds,
            HoldDurationSeconds);
        if (Progress < 1)
            return;

        _isHolding = false;
        SetProcess(false);
        SetProcessInput(false);
        Activate();
    }

    private string DisplayText => LabelText.ToUpperInvariant();

    private bool HasRowLabel => !string.IsNullOrWhiteSpace(LabelText);

    private UiTokens.TextStyle DisplayTextStyle =>
        ContentLayout == UiButtonContentLayout.Stacked
            ? Tokens.OverlineText
            : Tokens.LabelText;

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

    private void RefreshStyle()
    {
        if (!IsInsideTree())
            return;

        CustomMinimumSize = UiButtonMetrics.From(Tokens).MinimumSize(ContentLayout);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bool squareContent = ContentLayout == UiButtonContentLayout.Stacked || !HasRowLabel;
        if (squareContent && !_squareContent)
        {
            _rowSizeFlagsHorizontal = SizeFlagsHorizontal;
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        }
        else if (!squareContent && _squareContent)
        {
            SizeFlagsHorizontal = _rowSizeFlagsHorizontal;
        }

        _squareContent = squareContent;

        ClipText = ContentLayout == UiButtonContentLayout.Stacked;
        IconAlignment = HasRowLabel ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        VerticalIconAlignment = VerticalAlignment.Center;
        Tokens.ApplyTextStyle(this, DisplayTextStyle);
        AddThemeConstantOverride(
            "h_separation",
            ContentLayout == UiButtonContentLayout.Stacked
                ? (int)Tokens.Space1
                : UiSpacing.ControlGap(Tokens));

        Color content = _style.Resolve(Tokens).Content;
        AddThemeColorOverride("font_color", content);
        AddThemeColorOverride("font_focus_color", content);
        AddThemeColorOverride("font_hover_color", content);
        AddThemeColorOverride("font_pressed_color", content);
        AddThemeColorOverride("font_disabled_color", UiTokens.MultiplyAlpha(content, _disabledOpacity));
        EnsureProgressLayers();
        EnsureStackContent();
        _stackContent!.Visible = ContentLayout == UiButtonContentLayout.Stacked;
        if (ContentLayout == UiButtonContentLayout.Stacked)
        {
            Text = string.Empty;
            Icon = null;
            RefreshStackContent(content);
        }
        else
        {
            Text = DisplayText;
            if (IconId != UiIconId.None)
            {
                UiIcons.Apply(this, IconId, UiButtonMetrics.IconSize(ContentLayout), content);
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
        QueueRedraw();
        RefreshBadge();
    }

    private void Activate()
    {
        if (!Engine.IsEditorHint())
            EmitSignal(SignalName.Activated);
    }

    private void HandlePressed()
    {
        if (!Disabled && !HoldToActivate)
            Activate();
    }

    private void BeginHold()
    {
        if (Engine.IsEditorHint() || Disabled || !HoldToActivate)
            return;

        _holdElapsedSeconds = 0;
        _isHolding = true;
        Progress = 0;
        SetProcess(true);
        SetProcessInput(true);
    }

    private void EndHold()
    {
        if (!_isHolding)
            return;

        _holdElapsedSeconds = 0;
        _isHolding = false;
        Progress = 0;
        SetProcess(false);
        SetProcessInput(false);
    }

    public override void _Draw()
    {
        base._Draw();
        // Native Disabled queues a redraw; update our child visuals without a second state property.
        var modulation = new Color(1, 1, 1, Disabled ? _disabledOpacity : 1);
        if (_stackContent is not null)
            _stackContent.Modulate = modulation;
        if (_progressClip is not null)
            _progressClip.Modulate = modulation;
        if (!Disabled && Selected && Tokens.EffectsEnabled)
            DrawSelectedGlow();

        if (!Disabled)
            return;
        EndHold();

        UiResolvedButtonStyle style = _style.Resolve(Tokens);
        Color color = UiTokens.MultiplyAlpha(Selected ? style.Selected : style.Border, _disabledOpacity);
        float halfStroke = Tokens.StrokeHair * 0.5f;
        float radius = Tokens.RadiusMedium;
        UiDashedBorder.DrawRoundedRect(this, new Rect2(Vector2.One * halfStroke, Size - Vector2.One * Tokens.StrokeHair), radius, color, Tokens.StrokeHair);
    }

    private StyleBoxFlat CreateStyle(float opacity = 1, bool transparentBorder = false)
    {
        UiResolvedButtonStyle visual = _style.Resolve(Tokens);
        Color background = visual.Background;
        Color styleBorder = _style.BorderFor(Tokens, Selected);
        if (Progress >= 0)
            background = Colors.Transparent;

        StyleBoxFlat style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            transparentBorder
                ? Colors.Transparent
                : UiTokens.MultiplyAlpha(styleBorder, opacity),
            borderWidth: transparentBorder ? 0 : Selected ? Tokens.ButtonSelectedStroke : null,
            glow: false,
            horizontalPadding: ContentLayout != UiButtonContentLayout.Stacked && HasRowLabel
                ? (int)Tokens.Space4
                : 0,
            verticalPadding: 0);
        Color? glowColor = _style.GlowBaseFor(Tokens, Selected, opacity == 1);
        if (glowColor is { } color)
            UiGlow.ApplyToControl(style, color, Tokens.EffectsEnabled);

        return style;
    }

    private StyleBoxFlat CreateBackgroundStyle(Color background, float opacity)
    {
        StyleBoxFlat style = Tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            Colors.Transparent,
            borderWidth: 0,
            glow: false);
        return style;
    }

    private StyleBoxFlat CreateProgressStyle(float opacity)
    {
        StyleBoxFlat style = Tokens.ControlStyle(
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
        bool visible = Progress >= 0;
        LayoutProgress();
        _progressClip!.Visible = visible;
        _progressBackground!.Visible = visible;
        _progressFill!.Visible = visible && Progress > 0;
        if (!visible)
            return;

        Color background = _style.Resolve(Tokens).Background;
        _progressBackground.AddThemeStyleboxOverride(
            "panel",
            CreateBackgroundStyle(background, 1));
        _progressFill.AddThemeStyleboxOverride("panel", CreateProgressStyle(1));
    }

    private void EnsureProgressLayers()
    {
        if (_progressClip is null && GetNodeOrNull<Control>("_UiProgress") is { } existing)
        {
            _progressClip = existing;
            _progressBackground = existing.GetNode<Panel>("Background");
            _progressFillClip = existing.GetNode<Control>("Reveal");
            _progressFill = _progressFillClip.GetNode<Panel>("Fill");
        }
        if (_progressBackground is not null)
            return;

        _progressClip = new Control
        {
            Name = "_UiProgress",
            // Only hold layers must stay within the visible button frame;
            // badges and the intentional outer glow may extend beyond it.
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
            ShowBehindParent = true,
        };
        _progressBackground = CreateProgressLayer();
        _progressBackground.Name = "Background";
        _progressFillClip = new Control
        {
            Name = "Reveal",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _progressFill = CreateProgressLayer();
        _progressFill.Name = "Fill";
        _progressFillClip.AddChild(_progressFill);
        _progressClip.AddChild(_progressBackground);
        _progressClip.AddChild(_progressFillClip);
        AddChild(_progressClip);
    }

    private void EnsureStackContent()
    {
        if (_stackContent is null && GetNodeOrNull<VBoxContainer>("_UiStack") is { } existing)
        {
            _stackContent = existing;
            _stackIcon = existing.GetNode<TextureRect>("Icon");
            _stackLabel = existing.GetNode<Label>("Label");
        }
        if (_stackContent is not null)
            return;

        _stackContent = new VBoxContainer
        {
            Name = "_UiStack",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _stackContent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stackIcon = new TextureRect
        {
            Name = "Icon",
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
            Name = "Label",
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.Fill,
        };
        _stackContent.AddThemeConstantOverride("separation", (int)Tokens.Space1);
        _stackContent.AddChild(_stackIcon);
        _stackContent.AddChild(_stackLabel);
        AddChild(_stackContent);
    }

    private void RefreshBadge()
    {
        _badge ??= GetNodeOrNull<Label>("_UiBadge");
        if (_badge is null)
        {
            _badge = new Label
            {
                Name = "_UiBadge",
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
            return;

        _badge.Text = BadgeText;
        _badge.CustomMinimumSize = new Vector2(metrics.BadgeMinimumSize, metrics.BadgeMinimumSize);
        _badge.Size = _badge.CustomMinimumSize;
        _badge.Position = metrics.BadgePosition(Size);
        Tokens.ApplyTextStyle(_badge, Tokens.CaptionText);
        _badge.AddThemeColorOverride("font_color", Tokens.Background);
        _badge.AddThemeStyleboxOverride(
            "normal",
            Tokens.ControlStyle(Tokens.Halo, Colors.Transparent, borderWidth: 0, radius: Tokens.RadiusPill));
    }

    private void RefreshStackContent(Color content)
    {
        bool hasLabel = !string.IsNullOrWhiteSpace(LabelText);
        bool hasIcon = IconId != UiIconId.None;
        _stackContent!.AddThemeConstantOverride(
            "separation",
            hasLabel && hasIcon ? (int)Tokens.Space1 : 0);
        _stackLabel!.Text = DisplayText;
        _stackLabel.Visible = hasLabel;
        Tokens.ApplyTextStyle(_stackLabel, Tokens.OverlineText);
        _stackLabel.AddThemeColorOverride("font_color", content);
        _stackIcon!.Texture = hasIcon ? UiIcons.Load(IconId, UiIconSize.Large) : null;
        _stackIcon.Visible = hasIcon;
        _stackIcon.SelfModulate = content;
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

        var frame = new Rect2(Vector2.Zero, Size);
        UiButtonProgressLayout layout = UiButtonMetrics.ProgressLayout(frame, Progress);
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
        Color baseColor = UiGlow.FromBase(_style.Resolve(Tokens).Selected, enabled: true);
        for (int depth = 0; depth < UiGlow.Extent; depth++)
        {
            float strength = 1f - (depth / (float)UiGlow.Extent);
            Color color = UiTokens.MultiplyAlpha(baseColor, strength * strength);
            var rect = new Rect2(
                depth,
                depth,
                Size.X - (depth * 2),
                Size.Y - (depth * 2));
            if (rect.Size.X <= 0 || rect.Size.Y <= 0)
                break;

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

        float perimeter = ((rect.Size.X - (radius * 2)) * 2)
            + ((rect.Size.Y - (radius * 2)) * 2)
            + (Mathf.Tau * radius);
        int sampleCount = Mathf.Max(12, Mathf.CeilToInt(perimeter / Tokens.Space1));
        var points = new Vector2[sampleCount + 1];
        for (int index = 0; index <= sampleCount; index++)
            points[index] = UiDashedBorder.PointOnRoundedRect(rect, radius, perimeter * index / sampleCount);

        DrawPolyline(points, color, Tokens.StrokeHair, antialiased: true);
    }

}

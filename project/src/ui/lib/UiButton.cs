using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Canonical button with row, compact row or stacked content. It activates on a plain tap (#866).
/// Native <c>Text</c> is the authored caption (and translation key); an internal UiLabel renders
/// it, so letter case follows the caption's typography exactly as for UiLabel.
/// </summary>
[Tool]
[GlobalClass]
public sealed partial class UiButton : Button, ISerializationListener
{
    [Signal]
    public delegate void ActivatedEventHandler();

    private const float _disabledOpacity = 0.5f;
    private const int _minimumOutlineSamples = 12;
    private UiButtonStyle _style = UiButtonStyle.Secondary;
    private IUiButtonDesigner? _designer;
    private UiButtonDesign? _drawnDesign;
    private UiButtonKind _kind = UiButtonKind.Secondary;
    private UiIconId _iconId = UiIconId.None;
    private bool _selected;
    private bool _unavailable;
    private string _badgeText = string.Empty;
    private UiButtonContentLayout _contentLayout;
    private SizeFlags _rowSizeFlagsHorizontal = SizeFlags.Fill;
    private bool _squareContent;
    private bool _refreshingStyle;
    private Func<string>? _textSource;
    private AutoTranslateModeEnum _authoredTranslateMode;
    private BoxContainer? _content;
    private TextureRect? _contentIcon;
    private UiLabel? _contentLabel;
    private Label? _badge;
    // Object/method callables survive assembly reloads without retaining managed delegates.
    private Callable ResizedCallback => new(this, MethodName.RefreshBadge);
    private Callable PressedCallback => new(this, MethodName.HandlePressed);
    private Callable ContentMinimumSizeCallback => new(this, MethodName.FitContent);

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

    /// <summary>
    /// Drawn like a disabled button (dashed, dimmed) but still pressable, so the screen can answer
    /// the tap with why: a feature that comes in a later version (#841), or play blocked by the
    /// drawing (#844).
    /// </summary>
    [Export]
    public bool Unavailable
    {
        get => _unavailable;
        set
        {
            _unavailable = value;
            RefreshStyle();
        }
    }

    private bool LooksDisabled => Disabled || Unavailable;

    // The internal content draws the icon and caption, so the native properties that would lay
    // them out are derived, hidden from the Inspector, and not saved.
    private static readonly HashSet<StringName> _derivedProperties =
    [
        Control.PropertyName.Theme,
        Control.PropertyName.ThemeTypeVariation,
        Button.PropertyName.Icon,
        Button.PropertyName.Flat,
        Button.PropertyName.Alignment,
        Button.PropertyName.TextOverrunBehavior,
        Button.PropertyName.AutowrapMode,
        Button.PropertyName.ClipText,
        Button.PropertyName.IconAlignment,
        Button.PropertyName.VerticalIconAlignment,
        Button.PropertyName.ExpandIcon,
    ];

    private readonly UiUnsavedState _unsaved = new([.. UiUnsavedState.ButtonStyles, Control.PropertyName.CustomMinimumSize]);

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (_derivedProperties.Contains(property["name"].AsStringName()))
        {
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
        }
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        ConnectHandlers();
        RequestReady();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        RefreshStyle();
    }

    /// <summary>
    /// Already translated text for <c>Text</c>, as on <see cref="UiLabel.TextSource"/>: the button
    /// asks again when the language changes, and its own auto-translation is off meanwhile.
    /// </summary>
    public Func<string>? TextSource
    {
        get => _textSource;
        set
        {
            if (_textSource is null && value is not null)
            {
                _authoredTranslateMode = AutoTranslateMode;
            }
            else if (_textSource is not null && value is null)
            {
                AutoTranslateMode = _authoredTranslateMode;
            }

            _textSource = value;
            if (value is not null)
            {
                AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                Text = value();
            }
        }
    }

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, RefreshStyle))
        {
            return;
        }

        // Like UiLabel: with auto-translation off the button gets no notice on entering the tree.
        if (_textSource is not null && (what == NotificationTranslationChanged || what == NotificationEnterTree))
        {
            Text = _textSource();
        }

        if (what == NotificationParented)
        {
            // Only the direct parent may restyle the button; it is re-read on every reparent.
            _designer = GetParent() as IUiButtonDesigner;
            RefreshStyle();
        }
        else if (what == NotificationUnparented)
        {
            _designer = null;
        }
        else if (what == NotificationThemeChanged && IsNodeReady())
        {
            RefreshStyle();
        }
    }

    public override void _ExitTree()
    {
        DisconnectHandlers();
        base._ExitTree();
    }

    public void OnBeforeSerialize() => DisconnectHandlers();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreAfterDeserialize);

    private void RestoreAfterDeserialize()
    {
        if (!IsInsideTree())
            return;

        ConnectHandlers();
        RefreshStyle();
    }

    private void ConnectHandlers()
    {
        ConnectIfMissing(Control.SignalName.Resized, ResizedCallback);
        ConnectIfMissing(BaseButton.SignalName.Pressed, PressedCallback);
    }

    private void DisconnectHandlers()
    {
        DisconnectIfConnected(Control.SignalName.Resized, ResizedCallback);
        DisconnectIfConnected(BaseButton.SignalName.Pressed, PressedCallback);
    }

    private void ConnectIfMissing(StringName signal, Callable callback)
    {
        if (!IsConnected(signal, callback))
            Connect(signal, callback);
    }

    private void DisconnectIfConnected(StringName signal, Callable callback)
    {
        if (IsConnected(signal, callback))
            Disconnect(signal, callback);
    }

    private bool HasLabel => !string.IsNullOrWhiteSpace(Text);

    private UiButtonDesign Design =>
        _designer?.DesignFor(this) ?? new UiButtonDesign(_style, _contentLayout, UiCorners.Uniform(UiSize.Radius.Medium));

    private UiButtonStyle DrawnStyle => Design.Style;

    private UiButtonContentLayout DrawnLayout => Design.ContentLayout;

    private UiTokens.Typography CaptionStyle =>
        DrawnLayout == UiButtonContentLayout.Stacked
            ? UiTokens.Typography.Overline
            : UiTokens.Typography.Label;

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

    /// <summary>Restyles the button if its designer now gives it a different design.</summary>
    internal void RefreshDesign()
    {
        if (_drawnDesign != Design)
            RefreshStyle();
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree() || _refreshingStyle)
            return;

        _refreshingStyle = true;
        try
        {
            RefreshStyleCore();
        }
        finally
        {
            _refreshingStyle = false;
        }
    }

    private void RefreshStyleCore()
    {
        _drawnDesign = Design;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bool squareContent = DrawnLayout == UiButtonContentLayout.Stacked || !HasLabel;
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

        // Native text stays the storage but is never measured or seen: the variation makes it
        // transparent and the internal content renders the caption.
        ClipText = true;
        Icon = null;
        if (ThemeTypeVariation != UiThemeExpander.ButtonVariationName)
        {
            ThemeTypeVariation = UiThemeExpander.ButtonVariationName;
        }

        EnsureContent();
        RefreshContent(DrawnStyle.Resolve(this).Content);

        AddThemeStyleboxOverride("normal", Unavailable ? CreateDisabledStyle() : CreateStyle());
        AddThemeStyleboxOverride("hover", Unavailable ? CreateDisabledStyle() : CreateStyle());
        AddThemeStyleboxOverride("pressed", Unavailable ? CreateDisabledStyle() : CreateStyle());
        AddThemeStyleboxOverride("hover_pressed", Unavailable ? CreateDisabledStyle() : CreateStyle());
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        AddThemeStyleboxOverride("disabled", CreateDisabledStyle());
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
        if (!Disabled)
            Activate();
    }

    public override void _Draw()
    {
        base._Draw();
        // Native Text has no change signal, but setting it always queues a redraw.
        SyncCaption();
        // Native Disabled queues a redraw; update our child visuals without a second state property.
        var modulation = Colors.White with { A = LooksDisabled ? _disabledOpacity : 1 };
        if (_content is not null)
            _content.Modulate = modulation;
        if (!LooksDisabled && Selected && UiThemeLookup.EffectsEnabled(this))
            DrawSelectedGlow();
        if (UiPressFeedback.Shows(this, Selected))
            UiPressFeedback.Draw(this, Design.Corners, Design.Style.BackgroundColor, Design.Style.Kind == UiButtonKind.Tertiary);

        if (!LooksDisabled)
            return;

        UiResolvedButtonStyle style = DrawnStyle.Resolve(this);
        Color color = Selected ? style.Selected : style.Border.ScaleAlpha(_disabledOpacity);
        float stroke = UiSize.Stroke.Hair;
        float halfStroke = stroke * 0.5f;
        float radius = Design.Corners.Smallest;
        UiDashedBorder.DrawRoundedRect(this, new Rect2(Vector2.One * halfStroke, Size - Vector2.One * stroke), radius, color, stroke);
    }

    private StyleBoxFlat CreateDisabledStyle() => CreateStyle(_disabledOpacity, transparentBorder: true);

    private StyleBoxFlat CreateStyle(float opacity = 1, bool transparentBorder = false)
    {
        UiButtonDesign design = Design;
        UiResolvedButtonStyle visual = design.Style.Resolve(this);
        Color styleBorder = design.Style.BorderFor(this, Selected);
        StyleBoxFlat style = UiThemeLookup.CreateStyleBox(visual.Background.ScaleAlpha(opacity),
            transparentBorder
                ? Colors.Transparent
                : styleBorder.ScaleAlpha(opacity),
            borderWidth: transparentBorder ? 0 : Selected ? UiSize.Stroke.ButtonSelected : null,
            horizontalPadding: ContentPadding,
            verticalPadding: 0);
        design.Corners.ApplyTo(style);
        Color? glowColor = design.Style.GlowBaseFor(this, Selected, opacity == 1);
        if (glowColor is { } color)
            UiGlow.ApplyToControl(style, color, UiThemeLookup.EffectsEnabled(this));

        return style;
    }

    private float ContentPadding =>
        DrawnLayout != UiButtonContentLayout.Stacked && HasLabel ? UiSize.Space.S4 : 0;

    private void EnsureContent()
    {
        if (_content is null && GetNodeOrNull<BoxContainer>("_UiContent") is { } existing)
        {
            _content = existing;
            _contentIcon = existing.GetNode<TextureRect>("Icon");
            _contentLabel = existing.GetNode<UiLabel>("Label");
        }
        if (_content is null)
        {
            _content = new BoxContainer
            {
                Name = "_UiContent",
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _contentIcon = new TextureRect
            {
                Name = "Icon",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            _contentLabel = new UiLabel
            {
                Name = "Label",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            _content.AddChild(_contentIcon);
            _content.AddChild(_contentLabel);
            AddChild(_content);
        }
        if (!_content.IsConnected(Control.SignalName.MinimumSizeChanged, ContentMinimumSizeCallback))
            _content.Connect(Control.SignalName.MinimumSizeChanged, ContentMinimumSizeCallback);
    }

    private void RefreshContent(Color content)
    {
        bool stacked = DrawnLayout == UiButtonContentLayout.Stacked;
        bool hasIcon = IconId != UiIconId.None;
        var iconSize = UiButtonMetrics.IconSize(DrawnLayout, HasLabel);
        _content!.Vertical = stacked;
        _content.AddThemeConstantOverride(
            "separation",
            HasLabel && hasIcon ? (stacked ? UiSize.Space.S1 : UiSpacing.ControlGap) : 0);
        _content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _content.OffsetLeft = ContentPadding;
        _content.OffsetRight = -ContentPadding;

        UiTranslation.ShareContext(this, _contentLabel!);
        _contentLabel!.Text = Text;
        _contentLabel.Visible = HasLabel;
        _contentLabel.TextStyle = CaptionStyle;
        _contentLabel.TextColor = DrawnStyle.ContentColor;
        _contentLabel.TextOverrunBehavior = stacked
            ? TextServer.OverrunBehavior.TrimEllipsis
            : TextServer.OverrunBehavior.NoTrimming;
        // Stacked captions span the column; row captions center in the space beside the icon.
        _contentLabel.SizeFlagsHorizontal = stacked ? SizeFlags.Fill : SizeFlags.ExpandFill;

        _contentIcon!.Visible = hasIcon;
        _contentIcon.Texture = hasIcon ? UiIcons.Load(IconId, iconSize) : null;
        _contentIcon.CustomMinimumSize = Vector2.One * UiIcons.Pixels(iconSize);
        _contentIcon.SelfModulate = content;
        UiIcons.UseIconFilter(_contentIcon);
        FitContent();
    }

    private void SyncCaption()
    {
        if (_contentLabel is null || _contentLabel.Text == Text)
            return;

        if (_contentLabel.Visible == HasLabel)
            _contentLabel.Text = Text;
        else
            CallDeferred(MethodName.RefreshStyle);
    }

    // Button measures neither its children nor clipped text, so the content sets the minimum.
    private void FitContent()
    {
        if (_content is null)
            return;

        var minimum = UiButtonMetrics.Default.MinimumSize(DrawnLayout)
            .Max(_content.GetCombinedMinimumSize() + new Vector2(ContentPadding * 2, 0));
        if (CustomMinimumSize != minimum)
            CustomMinimumSize = minimum;
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
            };
            // Tree order, not ZIndex, keeps the badge over the button: a ZIndex sorts across the
            // whole CanvasLayer and would draw it through any screen or overlay above (#463).
            AddChild(_badge, @internal: InternalMode.Back);
        }

        var metrics = UiButtonMetrics.Default;
        _badge.Visible = !string.IsNullOrWhiteSpace(BadgeText);
        if (!_badge.Visible)
            return;

        UiTranslation.ShareContext(this, _badge);
        _badge.Text = BadgeText;
        _badge.CustomMinimumSize = new Vector2(metrics.BadgeMinimumSize, metrics.BadgeMinimumSize);
        _badge.Size = _badge.CustomMinimumSize;
        _badge.Position = metrics.BadgePosition(Size);
        UiThemeLookup.ApplyTypography(_badge, UiTokens.Typography.Caption);
        _badge.ThemeTypeVariation = UiThemeExpander.BadgeVariationName;
        _badge.AddThemeStyleboxOverride(
            "normal",
            UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Halo),
                Colors.Transparent,
                borderWidth: 0,
                radius: UiSize.Radius.Pill));
    }

    private void DrawSelectedGlow()
    {
        UiButtonDesign design = Design;
        Color baseColor = UiGlow.FromBase(design.Style.Resolve(this).Selected, enabled: true);
        for (int depth = 0; depth < UiGlow.Extent; depth++)
        {
            float strength = 1f - (depth / (float)UiGlow.Extent);
            Color color = baseColor.ScaleAlpha(strength * strength);
            var rect = new Rect2(
                depth,
                depth,
                Size.X - (depth * 2),
                Size.Y - (depth * 2));
            if (rect.Size.X <= 0 || rect.Size.Y <= 0)
                break;

            DrawRoundedRectOutline(
                rect,
                Mathf.Max(0, design.Corners.Smallest - depth),
                color);
        }
    }

    private void DrawRoundedRectOutline(Rect2 rect, float radius, Color color)
    {
        using var pen = UiPixelPen.Begin(this);
        if (radius <= 0)
        {
            pen.Polyline([rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y), rect.Position], color, UiSize.Stroke.Hair);
            return;
        }

        float perimeter = ((rect.Size.X - (radius * 2)) * 2)
            + ((rect.Size.Y - (radius * 2)) * 2)
            + (Mathf.Tau * radius);
        int sampleCount = Mathf.Max(
            _minimumOutlineSamples,
            Mathf.CeilToInt(perimeter / UiSize.Space.S1));
        var points = new Vector2[sampleCount + 1];
        for (int index = 0; index <= sampleCount; index++)
            points[index] = UiDashedBorder.PointOnRoundedRect(rect, radius, perimeter * index / sampleCount);

        pen.Polyline(points, color, UiSize.Stroke.Hair);
    }

}

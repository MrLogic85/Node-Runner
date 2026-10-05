using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>The authored dialog view, shared by the editor and the modal Window.</summary>
[Tool]
public sealed partial class UiDialogContent : Control
{
    public enum PreviewTheme { Neon, Paper }

    private UiPopupType _type;
    private UiNotificationIcon? _iconOverride;
    private PreviewTheme _theme;
    private UiPopupCard? _card;
    private ScrollContainer _scroll = null!;
    private UiLabel _title = null!;
    private UiLabel _body = null!;
    private UiLabel _error = null!;
    private UiButton _cancel = null!;
    private UiButton _confirm = null!;
    private bool _layoutQueued;

    [Export]
    public UiPopupType Type
    {
        get => _type;
        set { _type = value; ApplyAppearance(); }
    }

    [Export]
    public PreviewTheme ThemePreview
    {
        get => _theme;
        set
        {
            _theme = value;
            Theme = UiThemes.For(value switch
            {
                PreviewTheme.Paper => UiTokenType.Paper,
                _ => UiTokenType.Neon,
            });
            ApplyAppearance();
        }
    }

    public UiButton AbortButton => _cancel;
    public UiButton ActionButton => _confirm;

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        _card = GetNode<UiPopupCard>("%Card");
        _scroll = GetNode<ScrollContainer>("%BodyScroll");
        _title = GetNode<UiLabel>("%Title");
        _body = GetNode<UiLabel>("%Content");
        _error = GetNode<UiLabel>("%Error");
        _cancel = GetNode<UiButton>("%Cancel");
        _confirm = GetNode<UiButton>("%Confirm");
        Resized += QueueLayout;
        _card.MinimumSizeChanged += QueueLayout;
        _cancel.MinimumSizeChanged += QueueLayout;
        _confirm.MinimumSizeChanged += QueueLayout;
        _body.MinimumSizeChanged += QueueLayout;
        _scroll.Resized += QueueLayout;
        ApplyAppearance();
    }

    private readonly UiUnsavedState _unsaved = new(
        [
            ("%Scrim", ColorRect.PropertyName.Color),
            ("%Card", Control.PropertyName.OffsetLeft),
            ("%Card", Control.PropertyName.OffsetTop),
            ("%Card", Control.PropertyName.OffsetRight),
            ("%Card", Control.PropertyName.OffsetBottom),
            ("%SemanticIcon", TextureRect.PropertyName.Texture),
            ("%SemanticIcon", CanvasItem.PropertyName.SelfModulate),
            ("%SemanticType", Label.PropertyName.Text),
            ("%SemanticType", CanvasItem.PropertyName.Visible),
            ("%SemanticType", Control.PropertyName.ThemeTypeVariation),
            ("%SemanticType", Label.PropertyName.Uppercase),
            ("%BodyScroll", Control.PropertyName.CustomMinimumSize),
            ("%Confirm", UiButton.PropertyName.Kind),
        ]);

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, ApplyAppearance))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            ApplyAppearance();
        }
    }

    public void Bind(UiDialogSpec spec)
    {
        _type = spec.Type;
        _iconOverride = spec.Icon;
        ShowText(_title, spec.Title, spec.TitleSource);
        ShowText(_body, spec.Content, spec.ContentSource);
        _cancel.Text = spec.AbortText;
        _confirm.Text = spec.ActionText ?? "";
        _confirm.Visible = spec.HasAction;
        SetBusy(false);
        ShowError(null);
        _scroll.ScrollVertical = 0;
        ApplyAppearance();
    }

    public void SetBusy(bool busy)
    {
        _cancel.Disabled = busy;
        _confirm.Disabled = busy;
        QueueLayout();
    }

    /// <summary>Shows <paramref name="message"/>, or <paramref name="source"/>'s text in its place; neither hides the error.</summary>
    public void ShowError(string? message, Func<string>? source = null)
    {
        ShowText(_error, message ?? "", source);
        _error.Visible = message is not null || source is not null;
        QueueLayout();
    }

    private static void ShowText(UiLabel label, string text, Func<string>? source)
    {
        label.TextSource = source;
        if (source is null)
        {
            label.Text = text;
        }
    }

    private void ApplyAppearance()
    {
        if (_card is null || !IsInsideTree())
        {
            return;
        }
        _card.PopupType = Type;
        GetNode<ColorRect>("%Scrim").Color = UiThemeLookup.Color(this, UiTokens.Color.Scrim);
        var color = UiPopupStyle.SemanticColor(Type, this);
        var icon = GetNode<TextureRect>("%SemanticIcon");
        icon.Texture = _iconOverride is { } glyph
            ? glyph.Load(UiIconSize.Large)
            : UiIcons.Load(Type == UiPopupType.Default ? UiIconId.Model : UiIconId.Warn, UiIconSize.Large);
        icon.SelfModulate = color;
        var typeLabel = GetNode<Label>("%SemanticType");
        typeLabel.Text = UiPopupStyle.Overline(Type);
        typeLabel.Visible = typeLabel.Text.Length > 0;
        StyleText(typeLabel, UiTokens.Typography.Overline, UiPopupStyle.SemanticToken(Type));
        _confirm.Kind = Type switch
        {
            UiPopupType.Warn => UiButtonKind.Flat,
            UiPopupType.Danger => UiButtonKind.Tertiary,
            _ => UiButtonKind.Primary,
        };
        QueueLayout();
    }

    private static void StyleText(Label label, UiTokens.Typography style, UiTokens.Color color) =>
        UiThemeLookup.ApplyTextStyle(label, style, color);

    private void QueueLayout()
    {
        if (_layoutQueued || !IsInsideTree())
        {
            return;
        }
        _layoutQueued = true;
        Callable.From(() =>
        {
            _layoutQueued = false;
            if (IsInsideTree())
            {
                LayoutCard();
            }
        }).CallDeferred();
    }

    public void LayoutCard()
    {
        if (_card is null)
        {
            return;
        }
        var available = Size - Vector2.One * UiSize.Space.S4 * 2;
        var width = Mathf.Min(_card.CustomMinimumSize.X, available.X);
        _card.Size = new Vector2(width, 0);
        using var measured = new TextParagraph();
        measured.AddString(_body.Text, _body.GetThemeFont("font"), _body.GetThemeFontSize("font_size"));
        measured.Width = Mathf.Max(1, _scroll.Size.X - UiSize.Space.S4);
        var fixedHeight = _card.GetCombinedMinimumSize().Y - _scroll.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(measured.GetSize().Y, Mathf.Max(UiSize.Control.Default, available.Y - fixedHeight)));
        _card.Size = new Vector2(width, 0);
        _card.Position = (Size - _card.Size) / 2;
    }

    public override void _ExitTree()
    {
        Resized -= QueueLayout;
        if (_card is not null)
        {
            _card.MinimumSizeChanged -= QueueLayout;
            _cancel.MinimumSizeChanged -= QueueLayout;
            _confirm.MinimumSizeChanged -= QueueLayout;
            _body.MinimumSizeChanged -= QueueLayout;
            _scroll.Resized -= QueueLayout;
        }
    }
}

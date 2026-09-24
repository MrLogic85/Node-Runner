using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>The authored dialog view, shared by the editor and the modal Window.</summary>
[Tool]
public sealed partial class UiDialogContent : Control
{
    public enum PreviewTheme { Neon, Paper, EffectsLite }

    private UiPopupType _type;
    private PreviewTheme _theme;
    private UiTokens _tokens = UiTokens.Neon;
    private UiCard? _card;
    private ScrollContainer _scroll = null!;
    private Label _body = null!;
    private Label _error = null!;
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
            _tokens = value switch
            {
                PreviewTheme.Paper => UiTokens.Paper,
                PreviewTheme.EffectsLite => UiTokens.Neon.WithEffects(false),
                _ => UiTokens.Neon,
            };
            ApplyAppearance();
        }
    }

    public UiButton AbortButton => _cancel;
    public UiButton ActionButton => _confirm;

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        _card = GetNode<UiCard>("%Card");
        _scroll = GetNode<ScrollContainer>("%BodyScroll");
        _body = GetNode<Label>("%Content");
        _error = GetNode<Label>("%Error");
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

    public void Bind(UiDialogSpec spec, UiTokens tokens)
    {
        _type = spec.Type;
        _tokens = tokens;
        GetNode<Label>("%Title").Text = spec.Title;
        _body.Text = spec.Content;
        _cancel.LabelText = spec.AbortText;
        _cancel.HoldDurationSeconds = 0;
        _cancel.HoldToActivate = false;
        _confirm.LabelText = spec.ActionText ?? "";
        _confirm.Visible = spec.HasAction;
        _confirm.HoldDurationSeconds = spec.HoldToAction ? UiComponentContracts.HoldCompletionSeconds : 0;
        _confirm.HoldToActivate = spec.HoldToAction;
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

    public void ShowError(string? message)
    {
        _error.Text = message ?? "";
        _error.Visible = message is not null;
        QueueLayout();
    }

    private void ApplyAppearance()
    {
        if (_card is null || !IsInsideTree())
        {
            return;
        }
        _card.Tokens = _tokens;
        _card.Kind = UiPopupStyle.CardKind(Type);
        GetNode<ColorRect>("%Scrim").Color = _tokens.Scrim;
        var color = UiPopupStyle.SemanticColor(Type, _tokens);
        var icon = GetNode<TextureRect>("%SemanticIcon");
        icon.Texture = UiIcons.Load(Type == UiPopupType.Default ? UiIconId.Model : UiIconId.Warn, UiIconSize.Large);
        icon.SelfModulate = color;
        var typeLabel = GetNode<Label>("%SemanticType");
        typeLabel.Text = Type.ToString();
        StyleText(typeLabel, _tokens.OverlineText, color);
        StyleText(GetNode<Label>("%Title"), _tokens.SubheadingText, _tokens.Ink);
        StyleText(_body, _tokens.BodyText, _tokens.Ink);
        StyleText(_error, _tokens.NoteText, _tokens.Danger);
        _cancel.Tokens = _tokens;
        _confirm.Tokens = _tokens;
        _confirm.Kind = Type switch
        {
            UiPopupType.Warn => UiButtonKind.Flat,
            UiPopupType.Danger => UiButtonKind.Tertiary,
            _ => UiButtonKind.Primary,
        };
        QueueLayout();
    }

    private void StyleText(Label label, UiTokens.TextStyle style, Color color)
    {
        _tokens.ApplyTextStyle(label, style);
        label.AddThemeColorOverride("font_color", color);
    }

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
        var available = Size - Vector2.One * _tokens.Space4 * 2;
        var actionWidth = Mathf.Max(_cancel.GetMinimumSize().X, _confirm.Visible ? _confirm.GetMinimumSize().X : 0);
        _cancel.CustomMinimumSize = new Vector2(actionWidth, _tokens.ControlHeight);
        _confirm.CustomMinimumSize = _cancel.CustomMinimumSize;
        var width = Mathf.Min(_card.CustomMinimumSize.X, available.X);
        _card.Size = new Vector2(width, 0);
        using var measured = new TextParagraph();
        measured.AddString(_body.Text, _body.GetThemeFont("font"), _body.GetThemeFontSize("font_size"));
        measured.Width = Mathf.Max(1, _scroll.Size.X - _tokens.Space4);
        var fixedHeight = _card.GetCombinedMinimumSize().Y - _scroll.GetCombinedMinimumSize().Y;
        _scroll.CustomMinimumSize = new Vector2(0, Mathf.Min(measured.GetSize().Y, Mathf.Max(_tokens.ControlHeight, available.Y - fixedHeight)));
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

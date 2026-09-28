using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>The authored notification card, shared by the editor and the queue host.</summary>
[Tool]
public sealed partial class UiNotificationContent : UiCard
{
    public enum PreviewTheme { Neon, Paper, EffectsLite }

    private UiPopupType _type;
    private PreviewTheme _theme;
    private UiLabel? _title;
    private UiLabel _message = null!;
    private UiLabel _semanticType = null!;
    private TextureRect _icon = null!;
    private UiNotificationIcon? _iconOverride;

    [Export]
    public UiPopupType Type
    {
        get => _type;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid notification type: {value}. Keeping {_type}.");
                return;
            }
            _type = value;
            ApplyAppearance();
        }
    }

    [Export]
    public PreviewTheme ThemePreview
    {
        get => _theme;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid notification preview theme: {value}. Keeping {_theme}.");
                return;
            }
            _theme = value;
            Theme = UiThemes.For(value switch
            {
                PreviewTheme.Paper => UiTokenType.Paper,
                PreviewTheme.EffectsLite => UiTokenType.Light,
                _ => UiTokenType.Neon,
            });
            ApplyAppearance();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _Ready()
    {
        base._Ready();
        _title = GetNode<UiLabel>("%Title");
        _message = GetNode<UiLabel>("%Message");
        _semanticType = GetNode<UiLabel>("%SemanticType");
        _icon = GetNode<TextureRect>("%SemanticIcon");
        ApplyAppearance();
    }

    private readonly UiUnsavedState _unsaved = new(
        [
            (".", UiCard.PropertyName.Kind),
            ("%SemanticIcon", TextureRect.PropertyName.Texture),
            ("%SemanticIcon", CanvasItem.PropertyName.SelfModulate),
            ("%SemanticType", Label.PropertyName.Text),
            ("%SemanticType", UiLabel.PropertyName.TextColor),
            ("%Title", UiLabel.PropertyName.TextColor),
            ("%Message", UiLabel.PropertyName.TextColor),
        ]);

    public override void _Notification(int what)
    {
        // Before the base, so the card's own state is cleared after what these setters restyle.
        var saving = _unsaved.Handle(this, what, ApplyAppearance);
        base._Notification(what);
        if (saving)
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, ApplyAppearance);
        }
    }

    public void Bind(UiNotificationSpec spec)
    {
        _type = spec.Type;
        _iconOverride = spec.Icon;
        _title!.Text = spec.Title;
        _message.Text = spec.Message;
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        if (_title is null || !IsInsideTree())
        {
            return;
        }
        Kind = UiPopupStyle.CardKind(Type);
        var color = UiPopupStyle.SemanticColor(Type, this);
        _icon.Texture = _iconOverride is { } icon
            ? icon.Load(UiIconSize.Large)
            : UiIcons.Load(Type == UiPopupType.Default ? UiIconId.Model : UiIconId.Warn, UiIconSize.Large);
        _icon.SelfModulate = color;
        _semanticType.Text = Type.ToString();
        _semanticType.TextColor = UiPopupStyle.SemanticToken(Type);
        _title.TextColor = UiTokens.Color.Ink;
        _message.TextColor = UiTokens.Color.Ink;
    }
}

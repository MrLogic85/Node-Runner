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
            Tokens = value switch
            {
                PreviewTheme.Paper => UiTokens.Paper,
                PreviewTheme.EffectsLite => UiTokens.Neon.WithEffects(false),
                _ => UiTokens.Neon,
            };
        }
    }

    public override UiTokens Tokens
    {
        get => base.Tokens;
        set
        {
            base.Tokens = value;
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

    public void Bind(UiNotificationSpec spec, UiTokens tokens)
    {
        _type = spec.Type;
        _iconOverride = spec.Icon;
        _title!.Text = spec.Title;
        _message.Text = spec.Message;
        Tokens = tokens;
    }

    private void ApplyAppearance()
    {
        if (_title is null || !IsInsideTree())
        {
            return;
        }
        Kind = UiPopupStyle.CardKind(Type);
        var color = UiPopupStyle.SemanticColor(Type, Tokens);
        _icon.Texture = _iconOverride is { } icon
            ? icon.Load(UiIconSize.Large)
            : UiIcons.Load(Type == UiPopupType.Default ? UiIconId.Model : UiIconId.Warn, UiIconSize.Large);
        _icon.SelfModulate = color;
        _semanticType.Text = Type.ToString();
        _semanticType.Tokens = Tokens;
        _semanticType.AddThemeColorOverride("font_color", color);
        _title.Tokens = Tokens;
        _title.AddThemeColorOverride("font_color", Tokens.Ink);
        _message.Tokens = Tokens;
        _message.AddThemeColorOverride("font_color", Tokens.Ink);
    }
}

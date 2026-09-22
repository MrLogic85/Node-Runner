using Godot;

namespace NodeRunner.Ui.Lib;

public enum UiIconButtonSize
{
    Small,
    Default,
    Large,
}

/// <summary>Textless icon-button foundation with canonical padding and a square touch target.</summary>
public abstract partial class UiIconButton : UiButton
{
    private string _accessibleLabel = string.Empty;
    private string _symbolText = string.Empty;
    private UiIconButtonSize _buttonSize = UiIconButtonSize.Default;
    private UiIconSize _iconSize = UiIconSize.Large;

    protected UiIconButton()
    {
        ContentLayout = UiButtonContentLayout.Icon;
        IconAlignment = HorizontalAlignment.Center;
        Alignment = HorizontalAlignment.Center;
        IconId = UiIconId.More;
        HorizontalPadding = UiSpace.None;
        VerticalPadding = UiSpace.None;
        Enabled = true;
        Progress = -1;
    }

    [Export]
    public string AccessibleLabel
    {
        get => _accessibleLabel;
        set
        {
            _accessibleLabel = value;
            RefreshStyle();
        }
    }

    [Export]
    public string SymbolText
    {
        get => _symbolText;
        set
        {
            _symbolText = value;
            RefreshStyle();
        }
    }

    [Export]
    public UiIconButtonSize ButtonSize
    {
        get => _buttonSize;
        set
        {
            _buttonSize = value;
            Compact = value == UiIconButtonSize.Small;
            RefreshStyle();
        }
    }

    [Export]
    public UiIconSize IconSize
    {
        get => _iconSize;
        set
        {
            _iconSize = value;
            RefreshStyle();
        }
    }

    protected override string DisplayText => SymbolText;

    protected override string AccessibleDescription => AccessibleLabel;

    protected override UiIconSize DisplayIconSize => IconSize;

    protected override UiTokens.TextStyle DisplayTextStyle =>
        string.IsNullOrEmpty(SymbolText)
            ? base.DisplayTextStyle
            : Tokens.ReadoutMediumText;

    protected override Vector2 MinimumSize => new(Tokens.TouchTarget, Tokens.TouchTarget);

    protected override float VisibleControlSize => ButtonSize switch
    {
        UiIconButtonSize.Small => Tokens.ControlSmall,
        UiIconButtonSize.Default => Tokens.ControlHeight,
        UiIconButtonSize.Large => Tokens.TouchTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(ButtonSize), ButtonSize, null),
    };

    protected override float HorizontalVisibleInset => (MinimumSize.X - VisibleControlSize) * 0.5f;
}

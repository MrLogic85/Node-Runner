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
    private UiIconButtonSize _buttonSize = UiIconButtonSize.Default;
    private UiIconSize _iconSize = UiIconSize.Large;

    protected UiIconButton()
    {
        IconAlignment = HorizontalAlignment.Center;
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
    public UiIconButtonSize ButtonSize
    {
        get => _buttonSize;
        set
        {
            _buttonSize = value;
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

    protected override string DisplayText => string.Empty;

    protected override string AccessibleDescription => AccessibleLabel;

    protected override UiIconSize DisplayIconSize => IconSize;

    protected override Vector2 MinimumSize => ButtonSize switch
    {
        UiIconButtonSize.Small => new(Tokens.TouchTarget, Tokens.TouchTarget),
        UiIconButtonSize.Default => new(Tokens.TouchTarget, Tokens.TouchTarget),
        UiIconButtonSize.Large => new(Tokens.TouchTarget, Tokens.TouchTarget),
        _ => throw new ArgumentOutOfRangeException(nameof(ButtonSize), ButtonSize, null),
    };

    protected override float VisibleControlSize => ButtonSize switch
    {
        UiIconButtonSize.Small => Tokens.ControlSmall,
        UiIconButtonSize.Default => Tokens.ControlHeight,
        UiIconButtonSize.Large => Tokens.TouchTarget,
        _ => throw new ArgumentOutOfRangeException(nameof(ButtonSize), ButtonSize, null),
    };

    protected override float HorizontalVisibleInset => (MinimumSize.X - VisibleControlSize) * 0.5f;

    protected override HorizontalAlignment DisplayIconAlignment => HorizontalAlignment.Center;
}

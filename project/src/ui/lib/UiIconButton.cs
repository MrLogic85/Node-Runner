using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Textless icon-button foundation with canonical padding and a square touch target.</summary>
public abstract partial class UiIconButton : UiButton
{
    private string _accessibleLabel = string.Empty;

    protected UiIconButton()
    {
        IconAlignment = HorizontalAlignment.Center;
        IconId = UiIconId.More;
        HorizontalPadding = UiSpace.Space2;
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

    protected override string DisplayText => string.Empty;

    protected override string AccessibleDescription => AccessibleLabel;

    protected override UiIconSize DisplayIconSize => UiIconSize.ExtraLarge;

    protected override Vector2 MinimumSize => new(Tokens.TouchTarget, Tokens.TouchTarget);

    protected override float HorizontalVisibleInset =>
        (Tokens.TouchTarget - UiComponentContracts.IconButtonVisibleSize) * 0.5f;
}

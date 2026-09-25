using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Non-interactive menu divider using the menu surface border style.</summary>
[Tool]
[GlobalClass]
public partial class UiMenuItemDivider : UiMenuItem
{
    private float VerticalPadding =>
        SizeVariant == MenuItemSize.Compact
            ? Tokens.Space1
            : Tokens.Space2;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        RefreshItem();
    }

    public override Vector2 _GetMinimumSize() =>
        new(0, (VerticalPadding * 2) + Tokens.StrokeHair);

    public override void _Draw()
    {
        var y = Size.Y * 0.5f;
        DrawLine(
            new Vector2(HorizontalPadding, y),
            new Vector2(Mathf.Max(HorizontalPadding, Size.X - HorizontalPadding), y),
            Tokens.Edge,
            Tokens.StrokeHair);
    }

    protected override void RefreshItem()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
        RefreshLayout();
    }
}

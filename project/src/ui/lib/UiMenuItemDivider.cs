using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Non-interactive menu divider: a hairline in <c>line</c>, as in the reference menus.</summary>
[Tool]
[GlobalClass]
public partial class UiMenuItemDivider : UiMenuItem
{
    private float VerticalPadding =>
        SizeVariant == MenuItemSize.Compact
            ? UiSize.Space.S1
            : UiSize.Space.S2;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        RefreshItem();
    }

    public override Vector2 _GetMinimumSize() =>
        new(0, (VerticalPadding * 2) + UiSize.Stroke.Hair);

    public override void _Draw()
    {
        var y = Size.Y * 0.5f;
        DrawLine(
            new Vector2(HorizontalPadding, y),
            new Vector2(Mathf.Max(HorizontalPadding, Size.X - HorizontalPadding), y),
            UiThemeLookup.Color(this, UiTokens.Color.Line),
            UiSize.Stroke.Hair);
    }

    protected override void RefreshItem()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
        RefreshLayout();
    }
}

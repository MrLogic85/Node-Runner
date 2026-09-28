using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference button bar (<c>ComponentToolbars</c>): vertical, down the left edge, a fixed
/// <see cref="UiLayout.ButtonBarWidth"/> wide with a divider on its right edge. It only lays out:
/// the screen authors the buttons in <c>%ButtonBarContent</c> and decides what they do,
/// including which is selected or locked.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiButtonBar : MarginContainer
{
    private const float _unbounded = -1;

    public override Vector2 _GetMaximumSize() => new(UiLayout.ButtonBarWidth, _unbounded);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(UiLayout.ButtonBarWidth, 0);
        var padding = GetNode<MarginContainer>("%ButtonBarPadding");
        padding.AddThemeConstantOverride("margin_top", UiSize.Space.S1);
        padding.AddThemeConstantOverride("margin_bottom", UiSize.Space.S1);
        GetNode<VBoxContainer>("%ButtonBarContent").AddThemeConstantOverride("separation", UiSize.Space.S1);
    }
}

using Godot;

namespace NodeRunner.Ui.Lib;

[Tool]
[GlobalClass]
public partial class UiFrame : PanelContainer
{
    private bool _ready;

    private ColorRect? _background;
    private UiCard? _card;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _card = GetNode<UiCard>("%Card");
        _background = GetNode<ColorRect>("%Background");
        base._Ready();
        _ready = true;
        ApplyTokens();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
            ApplyTokens();
    }

    private void ApplyTokens()
    {
        if (!_ready)
            return;

        _background?.Color = UiThemeLookup.Color(this, UiTokens.Color.Background);
    }
}

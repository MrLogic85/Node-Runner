using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Token-backed modal surface for focused settings and confirmations. The scene that uses it
/// authors the title and body as children.
/// </summary>
public partial class UiSheet : UiCard
{
    public override void _Ready()
    {
        Kind = CardVariant.Frame;
        SizeVariant = CardSize.Flush;
        Glow = true;
        base._Ready();
    }

    protected override StyleBoxFlat CreateStyle()
    {
        var style = base.CreateStyle();
        style.BorderColor = UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
        return style;
    }
}

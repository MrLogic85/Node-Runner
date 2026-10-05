using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The start sign in the Training arena (#848): its child <see cref="UiSignpost"/>, <c>Signpost</c>,
/// stands on the ground at 0 m and points the way that counts, since a run's shown distance never
/// goes below 0 (#725). It stands behind every creature. The sign is outside the screen's Controls,
/// so it takes the project theme, Neon, like the arena (<see cref="NodeRunner.Theme.VisualTheme.Neon"/>).
/// </summary>
public partial class ArenaStartSign : ArenaMark
{
    private UiSignpost Sign => GetNode<UiSignpost>("Signpost");

    public override void _Process(double delta) => OnChanged();

    // The sign is drawn at screen size from this node's origin, so the node scales and moves to put
    // the sign's foot on the ground.
    protected override void OnChanged()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var screenScale = ScreenScale(VisibleArea());
        Scale = new Vector2(screenScale, screenScale);
        Position = new Vector2((float)StartX, -Theme.GroundEdgeWidth / 2) - (Sign.Foot * screenScale);
    }
}

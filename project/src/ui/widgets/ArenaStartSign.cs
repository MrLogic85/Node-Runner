using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The start sign in the Training arena (#848): its child <see cref="UiSignpost"/>, <c>Signpost</c>,
/// stands on the ground at 0 m and points the way that counts, since a run's shown distance never
/// goes below 0 (#725). It stands behind every creature and is half a metre tall in the world, so
/// it zooms with the world like the creatures do (#882). The sign is outside the screen's Controls,
/// so it takes the project theme, Neon, like the arena (<see cref="NodeRunner.Theme.VisualTheme.Neon"/>).
/// </summary>
public partial class ArenaStartSign : ArenaMark
{
    // The sign's height in the world, post included, in metres: an owner decision (#882).
    private const double _heightMetres = 0.5;

    private UiSignpost Sign => GetNode<UiSignpost>("Signpost");

    public override void _Ready()
    {
        Sign.Resized += OnChanged;
        OnChanged();
    }

    // The sign is drawn at UI size from this node's origin, so the node scales it to its height and
    // moves it to put the sign's foot on the ground.
    protected override void OnChanged()
    {
        if (!IsInsideTree() || Sign.Size.Y <= 0)
        {
            return;
        }

        var worldScale = (float)(_heightMetres * Metres.WorldUnitsPerMetre) / Sign.Size.Y;
        Scale = new Vector2(worldScale, worldScale);
        Position = new Vector2((float)StartX, -Theme.GroundEdgeWidth / 2) - (Sign.Foot * worldScale);
    }
}

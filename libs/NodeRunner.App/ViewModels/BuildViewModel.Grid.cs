using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The open Creation's grid: its size draws the grid, and with snap on, placed and moved joints
/// land on it. See `docs/BUILD_MODE.md` → "Interactions".
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>The open Creation's grid (<see cref="UseGrid"/>); a change raises PropertyChanged for it.</summary>
    public BuildGridDef Grid { get; private set; } = BuildGridDef.Default;

    /// <summary>The grid's cell size in canvas units.</summary>
    public double GridStep => Metres.WorldUnitsFromCentimetres(Grid.SizeCm);

    /// <summary>Whether placed and moved joints land on the grid.</summary>
    public bool SnapToGrid => Grid.Snap;

    /// <summary>
    /// Draws the grid at <paramref name="grid"/>'s size and snaps joints to it when it says so. It
    /// moves no placed joint: snapping applies when a joint is next placed or moved. It is no Undo
    /// step, locked or not (`docs/BUILD_MODE.md` → "Interactions").
    /// </summary>
    public void UseGrid(BuildGridDef grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid == Grid)
        {
            return;
        }

        Grid = grid;
        OnPropertyChanged(nameof(Grid));
    }

    /// <summary>Uses the grid size at <paramref name="index"/> in <see cref="BuildGridDef.SizesCm"/>, keeping the snap.</summary>
    public void ChooseGridSize(int index) => UseGrid(Grid with { SizeCm = BuildGridSizes.SizeCmAt(index) });

    /// <summary>Turns snapping on or off, keeping the grid size.</summary>
    public void ChooseSnapToGrid(bool snap) => UseGrid(Grid with { Snap = snap });

    // The grid point nearest position that keeps a ring of radius inside BuildArea. One always
    // exists: the area's middle is on every grid, and the largest ring leaves room around it.
    private Vector2D OnGrid(Vector2D position, double radius) => new(
        OnGridLine(position.X, BuildArea.Min.X + radius, BuildArea.Max.X - radius),
        OnGridLine(position.Y, BuildArea.Min.Y + radius, BuildArea.Max.Y - radius));

    private double OnGridLine(double coordinate, double min, double max)
    {
        var line = Math.Clamp(
            Math.Round(coordinate / GridStep, MidpointRounding.AwayFromZero),
            Math.Ceiling(min / GridStep),
            Math.Floor(max / GridStep));

        // Adding 0 turns −0 into 0, as CreatureBuilder.OnWholeUnits does.
        return (line * GridStep) + 0.0;
    }
}

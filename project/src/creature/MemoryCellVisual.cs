using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Memory cell's <see cref="MemoryCellPart"/> in Training: a child of its beam's body where
/// <see cref="Domain.SensorPicture.Centre"/> puts it, turned so its top faces the beam's built up side.
/// Each frame its needle shows the cell's m, the one the network reads on the next tick.
/// </summary>
public partial class MemoryCellVisual : MemoryCellPart, IShadowVisual
{
    /// <summary>A Memory cell is not drawn on a shadow, as a sensor is not; hiding also stops its reads.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    /// <summary>Reads the cell's m from its creature.</summary>
    public required Func<double> ReadValue { get; init; }

    public bool IsShadow
    {
        get => !Visible;
        set
        {
            Visible = !value;
            SetProcess(!value);
        }
    }

    // Value redraws only when m changes, so a cell that holds is not redrawn.
    public override void _Process(double delta) => Value = ReadValue();
}

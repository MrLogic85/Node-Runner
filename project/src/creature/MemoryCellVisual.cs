using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A Memory cell's <see cref="MemoryCellPart"/> in Training: a child of its beam's body where
/// <see cref="Domain.SensorPicture.Centre"/> puts it, turned so its top faces the beam's built up side.
/// It draws no live value; the part callout and Status panel show it.
/// </summary>
public partial class MemoryCellVisual : MemoryCellPart, IShadowVisual
{
    /// <summary>A Memory cell is not drawn on a shadow, as a sensor is not.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    public bool IsShadow
    {
        get => !Visible;
        set => Visible = !value;
    }
}

using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// A Memory cell's picture, centred on its origin with its top up, in its beam's
/// <see cref="DrawSlot.Sensor"/> slot; selected, it is drawn in <c>halo</c> (<see cref="SensorDrawing.DrawMemoryCell"/>).
/// </summary>
public partial class MemoryCellPart : PartVisual
{
    public MemoryCellPart()
        : base(DrawSlot.Sensor)
    {
    }

    public override void _Draw() => SensorDrawing.DrawMemoryCell(this, Godot.Transform2D.Identity, Theme, Selected);
}

using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// A Memory cell's picture, centred on its origin with its top up, in its beam's
/// <see cref="DrawSlot.Sensor"/> slot; selected, it is drawn in <c>halo</c> (<see cref="SensorDrawing.DrawMemoryCell"/>).
/// </summary>
public partial class MemoryCellPart : PartVisual
{
    private double _value;

    public MemoryCellPart()
        : base(DrawSlot.Sensor)
    {
    }

    /// <summary>The m its needle shows; 0 is upright.</summary>
    public double Value
    {
        get => _value;
        set => Change(ref _value, value);
    }

    public override void _Draw() => SensorDrawing.DrawMemoryCell(this, Godot.Transform2D.Identity, Theme, Value, Selected);
}

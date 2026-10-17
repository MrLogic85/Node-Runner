using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A sensor's <see cref="SensorPart"/> in Training: a child of the beam body where
/// <see cref="Domain.SensorPicture.Centre"/> puts it, turned so its top faces the beam's built up side. The Accelerometer's weight follows
/// the live proof mass every frame. A camera's rays are drawn by <see cref="CameraRaysVisual"/>.
/// </summary>
public partial class SensorVisual : SensorPart, IShadowVisual
{
    /// <summary>Sensors are not drawn on a shadow; hiding also stops the Accelerometer's redraws.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    public AccelerometerSensor? Accelerometer { get; init; }

    public bool IsShadow
    {
        get => !Visible;
        set
        {
            Visible = !value;
            SetProcess(!value);
        }
    }

    public override void _Process(double delta)
    {
        if (Accelerometer is not null)
        {
            WeightOffset = Mechanics.Accelerometer.WeightOffset(Accelerometer.CurrentProofMass);
            QueueRedraw();
        }
    }
}

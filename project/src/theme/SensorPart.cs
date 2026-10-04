using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// A sensor's picture (#767), centred on its origin with its top up; selected, it is drawn in
/// <c>halo</c> (<see cref="SensorDrawing"/>).
/// </summary>
public partial class SensorPart : PartVisual
{
    private SensorKind _kind;
    private Vector2D _weightOffset;
    private Vector2 _cameraAim = Vector2.Down;

    public SensorPart()
        : base(CreatureLayers.Sensors, CreatureLayers.SelectedSensors)
    {
    }

    public SensorKind Kind
    {
        get => _kind;
        set => Change(ref _kind, value);
    }

    /// <summary>Where an Accelerometer's weight sits (<see cref="Accelerometer.WeightOffset"/>).</summary>
    public Vector2D WeightOffset
    {
        get => _weightOffset;
        set => Change(ref _weightOffset, value);
    }

    /// <summary>The middle of a camera's ray fan in this picture's frame, the way the camera looks.</summary>
    public Vector2 CameraAim
    {
        get => _cameraAim;
        set => Change(ref _cameraAim, value);
    }

    public override void _Draw()
    {
        switch (Kind)
        {
            case SensorKind.Accelerometer:
                SensorDrawing.DrawAccelerometer(this, Transform2D.Identity, Theme, WeightOffset, Selected);
                break;
            case SensorKind.Camera:
                SensorDrawing.DrawCamera(this, Transform2D.Identity, Theme, CameraAim, Selected);
                break;
        }
    }
}

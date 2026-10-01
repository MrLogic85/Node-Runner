using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// A sensor's picture on its beam in Training (#576): a child of the beam body at the beam's
/// midpoint, turned so its top faces the beam's built up side. The Accelerometer's weight follows
/// the live proof mass every frame; a selected camera also draws its rays to what they see.
/// </summary>
public partial class SensorVisual : Node2D
{
    private bool _isSelected;

    public required VisualTheme Theme { get; init; }

    public AccelerometerSensor? Accelerometer { get; init; }

    public CameraSensor? Camera { get; init; }

    /// <summary>The middle of the camera's ray fan in this picture's frame, the way the camera looks.</summary>
    public Vector2 CameraAim { get; init; } = Vector2.Down;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        if (Accelerometer is not null || (IsSelected && Camera is not null))
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (IsSelected && Camera is not null)
        {
            SensorDrawing.DrawRays(this, Theme, ToLocal(Camera.GlobalOrigin), Camera.GlobalRayEnds.Select(ToLocal));
        }

        if (Accelerometer is not null)
        {
            SensorDrawing.DrawAccelerometer(this, Theme, Domain.Accelerometer.WeightOffset(Accelerometer.CurrentProofMass), IsSelected);
        }
        else
        {
            SensorDrawing.DrawCamera(this, Theme, CameraAim, IsSelected);
        }
    }
}

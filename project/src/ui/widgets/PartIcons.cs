using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The glyph for each kind of part, shared by Build's side panel and Training's part callout
/// (#1064), so a part looks the same wherever it is named.
/// </summary>
public static class PartIcons
{
    public static UiIconId For(PartKind kind) => kind switch
    {
        PartKind.Node => UiIconId.Joint,
        PartKind.Beam => UiIconId.Beam,
        PartKind.Accelerometer => UiIconId.PartAccelerometer,
        PartKind.Camera => UiIconId.PartCamera,
        PartKind.Servo => UiIconId.PartServo,
        PartKind.Piston => UiIconId.PartPiston,
        PartKind.Spring => UiIconId.PartSpring,
        PartKind.Wheel => UiIconId.PartWheel,
        PartKind.TouchSensor => UiIconId.PartTouch,
        PartKind.Rubber => UiIconId.PartRubber,
        PartKind.VelocityMotor => UiIconId.PartVelocity,
        PartKind.Brake => UiIconId.PartBrake,
        PartKind.AngleSensor => UiIconId.PartAngleSensor,
        PartKind.Stepper => UiIconId.PartStepper,
        _ => UiIconId.None,
    };
}

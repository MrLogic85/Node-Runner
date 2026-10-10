using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The glyph for each kind of part, shared by Build's side panel and Training's part callout
/// (#1064), so a part looks the same wherever it is named.
/// </summary>
public static class PartIcons
{
    public static UiIconId For(PartSettingsKind kind) => kind switch
    {
        PartSettingsKind.Node => UiIconId.Joint,
        PartSettingsKind.Beam => UiIconId.Beam,
        PartSettingsKind.Accelerometer => UiIconId.PartAccelerometer,
        PartSettingsKind.Camera => UiIconId.PartCamera,
        PartSettingsKind.Servo => UiIconId.PartServo,
        PartSettingsKind.Piston => UiIconId.PartPiston,
        PartSettingsKind.Spring => UiIconId.PartSpring,
        PartSettingsKind.Wheel => UiIconId.PartWheel,
        _ => UiIconId.None,
    };
}

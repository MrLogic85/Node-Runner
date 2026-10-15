namespace NodeRunner.App.ViewModels;

/// <summary>
/// One short line on what each part does. The same line shows under the part's row in the
/// Links list and as the note in its Part settings, so a part reads the same everywhere.
/// </summary>
public static class PartInfo
{
    public static UiText Beam { get; } = UiText.Plain("A rigid beam.");

    public static UiText Piston { get; } = UiText.Plain("Extends and retracts.");

    public static UiText Spring { get; } = UiText.Plain("Springs back and damps movement.");

    public static UiText Servo { get; } = UiText.Plain("Turns and holds an angle.");

    public static UiText VelocityMotor { get; } = UiText.Plain("Spins round and round.");

    public static UiText Stepper { get; } = UiText.Plain("A motor that ticks like a clock.");

    public static UiText Brake { get; } = UiText.Plain("Slows or locks its joint.");

    public static UiText Wheel { get; } = UiText.Plain("Rolls freely, a motor can drive it.");

    public static UiText Accelerometer { get; } = UiText.Plain("Measures a beam's acceleration.");

    public static UiText Camera { get; } = UiText.Plain("Measures distance to the ground.");

    public static UiText Touch { get; } = UiText.Plain("Signals when a joint is touching the ground.");

    public static UiText AngleSensor { get; } = UiText.Plain("Measures a joint's angle and velocity.");

    public static UiText Rubber { get; } = UiText.Plain("Doubles a joint's friction.");
}

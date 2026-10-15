namespace NodeRunner.App.ViewModels;

/// <summary>
/// One short line on what each part does. The same line shows under the part's row in the
/// Links list and as the note in its Part settings, so a part reads the same everywhere.
/// </summary>
public static class PartInfo
{
    public static UiText Beam { get; } = UiText.Plain("A rigid rod.");

    public static UiText Piston { get; } = UiText.Plain("Extends and retracts.");

    public static UiText Spring { get; } = UiText.Plain("Extends and retracts toward its built length.");

    public static UiText Servo { get; } = UiText.Plain("Turns its joint to a target angle and holds it there.");

    public static UiText VelocityMotor { get; } = UiText.Plain("Turns one side round and round. The brain picks how fast and which way.");

    public static UiText Stepper { get; } = UiText.Plain("A motor that ticks like a clock.");

    public static UiText Brake { get; } = UiText.Plain("Slows its joint's turning, or holds it still when Locked.");

    public static UiText Wheel { get; } = UiText.Plain("Rolls freely on the ground. Add a Motor, Servo or Stepper to power it.");

    public static UiText Accelerometer { get; } = UiText.Plain("Measures its beam's acceleration.");

    public static UiText Camera { get; } = UiText.Plain("Its rays see how near the ground is.");

    public static UiText Touch { get; } = UiText.Plain("Feels how near its joint is to the ground.");

    public static UiText AngleSensor { get; } = UiText.Plain("Tells the brain the angle between its two sides, and how fast it turns.");

    public static UiText Rubber { get; } = UiText.Plain("Makes its joint grip the ground.");
}

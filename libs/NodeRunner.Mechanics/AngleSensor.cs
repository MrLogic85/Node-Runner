namespace NodeRunner.Mechanics;

/// <summary>
/// The readings of a <see cref="Domain.AngleSensorDef"/> (#1204). Its angle is the one between its
/// sides, measured with <see cref="MotorSideGeometry"/> as a Velocity motor measures its own, so on the
/// same sides both read the same sine and cosine. Its speed is scaled by <see cref="SpeedScale"/>, its
/// own constant, so retuning any motor never changes what a trained brain reads. It applies no force.
/// </summary>
public static class AngleSensor
{
    /// <summary>The speed that reads tanh(1) ≈ 0.76: 2π rad/s, 60 rpm, counter-clockwise positive.</summary>
    public const double SpeedScale = 2 * Math.PI;

    /// <summary>The angle inputs: the sine and cosine of the angle between the sides, read as a Velocity motor reads them.</summary>
    public static (double Sin, double Cos) AngleInputs(double angle) => VelocityMotor.AngleInputs(angle);

    /// <summary>The speed input, −1…1 and 0 at rest: tanh of the Target's speed against the Fixed side over <see cref="SpeedScale"/>.</summary>
    /// <param name="speed">Angular speed in radians per second; counter-clockwise positive.</param>
    public static double SpeedInput(double speed) => MotorLoop.SpeedInput(speed, SpeedScale);
}

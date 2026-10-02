namespace NodeRunner.Domain;

/// <summary>
/// The brain conventions of a motor on a joint (#534): its port channels and how its readings and
/// target are scaled. Until motors become parts (#450), a motorized <see cref="NodeConnectionDef"/>
/// is the motor: its ports belong to the joint's node and are keyed by the beam it turns.
/// <list type="bullet">
/// <item>Angle input: how far the turning beam has rotated from its built pose relative to the
/// locked beam, counter-clockwise on screen positive, wrapped at ±180° and scaled to −1…1.
/// 0 means as built.</item>
/// <item>Speed input: <c>tanh(v / maxSpeed)</c>, counter-clockwise positive.</item>
/// <item>Target output: the speed to chase, −1…1 of the maximum, counter-clockwise positive.</item>
/// </list>
/// Rotations come in the creature's y-down frame, where a positive angle turns clockwise on screen.
/// Stateless and shared by the sim and tests, like <see cref="MotorTopology"/>.
/// </summary>
public static class JointMotor
{
    public static string AngleChannel(int beamId) => $"angle:{beamId}";

    public static string SpeedChannel(int beamId) => $"speed:{beamId}";

    public static string TargetChannel(int beamId) => $"target:{beamId}";

    /// <param name="relativeRotation">The turning beam's rotation minus the locked beam's, in radians.</param>
    /// <param name="builtRelativeRotation">The same difference in the built pose.</param>
    public static double AngleInput(double relativeRotation, double builtRelativeRotation) =>
        -Math.IEEERemainder(relativeRotation - builtRelativeRotation, Math.Tau) / Math.PI;

    /// <param name="relativeAngularVelocity">The turning beam's angular velocity minus the locked beam's, in radians per second.</param>
    /// <param name="maxAngularVelocity">The motor's top speed, in radians per second.</param>
    public static double SpeedInput(double relativeAngularVelocity, double maxAngularVelocity) =>
        Math.Tanh(-relativeAngularVelocity / maxAngularVelocity);

    /// <summary>The relative angular velocity, in the y-down frame, that the brain's <paramref name="target"/> asks for.</summary>
    public static double TargetAngularVelocity(double target, double maxAngularVelocity) =>
        -Math.Clamp(target, -1, 1) * maxAngularVelocity;
}

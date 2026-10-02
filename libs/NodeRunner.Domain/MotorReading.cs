namespace NodeRunner.Domain;

/// <summary>
/// One model output the creature drove this tick (a motor relation's target angular velocity,
/// or a Piston's position or strength, in the output's range) and the torque or force that part
/// last applied. See docs/CREATURE_MODEL.md for the model-output mapping. <see cref="GroupKind"/>
/// and <see cref="GroupIndex"/> identify which part, like <see cref="SensorReading"/>: a Piston
/// gives two readings with the same group. Presentation (combining them into a display label) is
/// the App layer's job (see MappingViewModel), not the creature's.
/// </summary>
public readonly record struct MotorReading(string GroupKind, int GroupIndex, double Target, double Torque)
{
    public const string MotorRelationKind = "Motor relation";

    public const string PistonKind = "Piston";
}

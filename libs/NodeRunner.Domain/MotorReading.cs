namespace NodeRunner.Domain;

/// <summary>
/// One motor relation's current model output (target angular velocity in
/// [-1, 1]) and the torque that motor relation last applied to chase it.
/// See docs/CREATURE_MODEL.md for the model-output-to-motor-relation
/// mapping. <see cref="GroupIndex"/> identifies which motor relation;
/// presentation (combining it into a display label) is the App layer's job
/// (see MappingViewModel), not the creature's.
/// </summary>
public readonly record struct MotorReading(int GroupIndex, double Target, double Torque);

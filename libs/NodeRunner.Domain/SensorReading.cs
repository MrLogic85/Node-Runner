namespace NodeRunner.Domain;

/// <summary>
/// One sensor value read from a creature's cores (and motor-relation
/// proprioception) for a given physics tick. Purely descriptive — see
/// docs/CREATURE_MODEL.md for what each sensor measures. <see cref="GroupKind"/>
/// and <see cref="GroupIndex"/> identify which core/motor relation the
/// reading came from; presentation (combining them into a display label)
/// is the App layer's job (see MappingViewModel), not the creature's.
/// </summary>
public readonly record struct SensorReading(string GroupKind, int GroupIndex, string Name, double Value);

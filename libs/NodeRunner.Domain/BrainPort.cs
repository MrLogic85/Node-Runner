namespace NodeRunner.Domain;

/// <summary>
/// One brain channel a part declares (#534): which part, which of its channels and which way the
/// signal goes. <see cref="Channel"/> is a machine key that never changes; display names are
/// separate. See <see cref="BrainPorts"/> and docs/CREATURE_MODEL.md.
/// </summary>
public readonly record struct BrainPort(int PartId, string Channel, PortDirection Direction);

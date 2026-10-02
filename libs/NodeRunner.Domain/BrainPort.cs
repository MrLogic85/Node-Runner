namespace NodeRunner.Domain;

/// <summary>
/// One brain channel a part declares (#534): which part, which of its channels, which way the
/// signal goes and what it carries (#535). <see cref="Channel"/> is a machine key that never
/// changes; display names are separate. See <see cref="BrainPorts"/> and docs/CREATURE_MODEL.md.
/// </summary>
public readonly record struct BrainPort(int PartId, string Channel, PortDirection Direction, PortSignal Signal)
{
    public static BrainPort Input(int partId, string channel) => new(partId, channel, PortDirection.Input, PortSignal.Reading);

    public static BrainPort Output(int partId, string channel, PortSignal signal)
    {
        if (signal == PortSignal.Reading)
        {
            throw new ArgumentOutOfRangeException(nameof(signal), signal, "An output drives a velocity, position or strength.");
        }

        return new(partId, channel, PortDirection.Output, signal);
    }
}

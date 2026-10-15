using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the network's ports are called, in port order (<see cref="NetworkPorts"/>): each port's
/// <see cref="PortDisplay.Label"/> filled with its part's name, such as "Accel 1: along" or "Piston 1: strength".
/// </summary>
public sealed record NetworkPortLabels(IReadOnlyList<UiText> Inputs, IReadOnlyList<UiText> Outputs)
{
    public static NetworkPortLabels Empty { get; } = new([], []);

    public static NetworkPortLabels For(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        UiText Label(NetworkPort port)
        {
            var part = creature.FindPart(port.PartId)!;
            return PartKindDisplays.Of(part.PartKind).Port(port).Label(PartNames.Display(creature.Parts, port.PartId));
        }

        var layout = NetworkPorts.Of(creature);
        return new NetworkPortLabels(
            layout.Inputs.Select(Label).ToArray(),
            layout.Outputs.Select(Label).ToArray());
    }
}

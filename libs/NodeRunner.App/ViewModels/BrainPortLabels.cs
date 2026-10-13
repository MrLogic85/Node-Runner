using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What the brain's ports are called, in port order (<see cref="BrainPorts"/>): each port's
/// <see cref="PortDisplay.Label"/> filled with its part's name, such as "Accel 1: along" or "Piston 1: strength".
/// </summary>
public sealed record BrainPortLabels(IReadOnlyList<UiText> Inputs, IReadOnlyList<UiText> Outputs)
{
    public static BrainPortLabels Empty { get; } = new([], []);

    public static BrainPortLabels For(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        UiText Label(BrainPort port)
        {
            var part = creature.FindPart(port.PartId)!;
            return PartKindDisplays.Of(part.PartKind).Port(port).Label(PartNames.Display(creature.Parts, port.PartId));
        }

        var layout = BrainPorts.Of(creature);
        return new BrainPortLabels(
            layout.Inputs.Select(Label).ToArray(),
            layout.Outputs.Select(Label).ToArray());
    }
}

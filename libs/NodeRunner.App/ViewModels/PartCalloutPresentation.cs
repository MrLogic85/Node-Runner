using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>One port in the part callout: its short label, the range its bar spans, and its live value or null.</summary>
public sealed record PartCalloutPort(BrainPort Port, UiText Label, PortRange Range, double? Value);

/// <summary>
/// The tapped part's callout in Training and Simulate (#388, #1064). A part with brain ports
/// shows its glyph and name, then its senses and its outputs with their live values; a Wheel
/// shows its glyph, its name and that it has no brain ports; a Joint, Beam or Spring shows only
/// its name. Built from one <see cref="BrainPortValues"/> sample, the same one BrainFocus reads,
/// so the callout never runs the brain. See docs/WORLD_VISUALS.md → "Part callout".
/// </summary>
public sealed record PartCalloutPresentation(
    PartSettingsKind Kind,
    UiText Name,
    bool ShowsGlyph,
    IReadOnlyList<PartCalloutPort> Senses,
    IReadOnlyList<PartCalloutPort> Outputs,
    UiText? Note)
{
    private static readonly UiText _noBrainPorts = UiText.Plain("No brain ports");

    /// <summary>
    /// The callout for <paramref name="selection"/> on <paramref name="creature"/>. Its ports and
    /// their values come from <paramref name="values"/>, sampled with the creature's layout.
    /// </summary>
    public static PartCalloutPresentation For(CreatureDef creature, CreatureElementSelection selection, BrainPortValues values)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(values);

        var name = PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, creature.Wheels, selection.Id);
        var kind = KindOf(creature, selection);
        switch (kind)
        {
            case PartSettingsKind.Node or PartSettingsKind.Beam or PartSettingsKind.Spring:
                return new PartCalloutPresentation(kind, name, ShowsGlyph: false, [], [], null);
            case PartSettingsKind.Wheel:
                return new PartCalloutPresentation(kind, name, ShowsGlyph: true, [], [], _noBrainPorts);
        }

        var ports = values.Of(selection.Id);
        PartCalloutPort[] Of(PortDirection direction) => ports
            .Where(value => value.Port.Direction == direction)
            .Select(value => new PartCalloutPort(value.Port, BrainPortDisplay.ShortLabel(kind, value.Port), BrainPortDisplay.Range(kind, value.Port), BrainPortDisplay.ShownValue(kind, value.Port, value.Value)))
            .ToArray();
        return new PartCalloutPresentation(kind, name, ShowsGlyph: true, Of(PortDirection.Input), Of(PortDirection.Output), null);
    }

    private static PartSettingsKind KindOf(CreatureDef creature, CreatureElementSelection selection) => selection.Kind switch
    {
        CreatureElementKind.Node => PartSettingsKind.Node,
        CreatureElementKind.Beam => PartSettingsKind.Beam,
        CreatureElementKind.Sensor => creature.Sensors.Single(sensor => sensor.Id == selection.Id).Kind == SensorKind.Accelerometer
            ? PartSettingsKind.Accelerometer
            : PartSettingsKind.Camera,
        CreatureElementKind.Servo => PartSettingsKind.Servo,
        CreatureElementKind.Piston => PartSettingsKind.Piston,
        CreatureElementKind.Spring => PartSettingsKind.Spring,
        CreatureElementKind.Wheel => PartSettingsKind.Wheel,
        _ => throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Unknown part kind."),
    };
}

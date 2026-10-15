using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>One port in the part callout: its short label, the range its bar spans, and its live value or null.</summary>
public sealed record PartCalloutPort(NetworkPort Port, UiText Label, PortRange Range, double? Value);

/// <summary>
/// The tapped part's callout in Training and Simulate (#388, #1064). A part with network ports
/// shows its glyph and name, then its inputs and its outputs with their live values; a Wheel, Rubber or Brake
/// shows its glyph, its name and that it has no network ports; a Joint, Beam or Spring shows only
/// its name. Built from one <see cref="NetworkPortValues"/> sample, the same one NetworkFocus reads,
/// so the callout never runs the network. See docs/WORLD_VISUALS.md → "Part callout".
/// </summary>
public sealed record PartCalloutPresentation(
    PartKind Kind,
    UiText Name,
    bool ShowsGlyph,
    IReadOnlyList<PartCalloutPort> Inputs,
    IReadOnlyList<PartCalloutPort> Outputs,
    UiText? Note)
{
    private static readonly UiText _noNetworkPorts = UiText.Plain("No network ports");

    /// <summary>
    /// The word before the inputs' line (#1150), short for Inputs: the values the part gives the
    /// network. It names the line, so its colour is never the only cue.
    /// </summary>
    public static UiText InputsHeading { get; } = UiText.Plain("In").InContext("part callout");

    /// <summary>The word before the outputs' line (#1150), short for Outputs: the values the network sets.</summary>
    public static UiText OutputsHeading { get; } = UiText.Plain("Out").InContext("part callout");

    /// <summary>
    /// The callout for <paramref name="selection"/> on <paramref name="creature"/>. Its ports and
    /// their values come from <paramref name="values"/>, sampled with the creature's layout.
    /// </summary>
    public static PartCalloutPresentation For(CreatureDef creature, CreatureElementSelection selection, NetworkPortValues values)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(values);

        var name = PartNames.Display(creature.Parts, selection.Id);
        var kind = (creature.FindPart(selection.Id) ?? throw new ArgumentException($"Part {selection.Id} is not on the creature.", nameof(selection))).PartKind;
        switch (kind)
        {
            case PartKind.Node or PartKind.Beam or PartKind.Spring:
                return new PartCalloutPresentation(kind, name, ShowsGlyph: false, [], [], null);
            case PartKind.Wheel or PartKind.Rubber or PartKind.Brake:
                return new PartCalloutPresentation(kind, name, ShowsGlyph: true, [], [], _noNetworkPorts);
        }

        var ports = values.Of(selection.Id);
        PartCalloutPort[] Of(PortDirection direction) => ports
            .Where(value => value.Port.Direction == direction)
            .Select(value => new PartCalloutPort(value.Port, NetworkPortDisplay.ShortLabel(kind, value.Port), NetworkPortDisplay.Range(kind, value.Port), value.Value))
            .ToArray();
        return new PartCalloutPresentation(kind, name, ShowsGlyph: true, Of(PortDirection.Input), Of(PortDirection.Output), null);
    }

}

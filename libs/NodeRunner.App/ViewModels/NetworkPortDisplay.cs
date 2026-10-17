using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>How a port's value is drawn: a bar centred on 0 for −1…1, a bar filling from empty for 0…1.</summary>
public enum PortRange
{
    MinusOneToOne,
    ZeroToOne,
}

/// <summary>
/// A port's short label and bar range in the part callout, from its kind's entry in
/// <see cref="PartKindDisplays"/>. A port with no entry throws.
/// </summary>
public static class NetworkPortDisplay
{
    public static PortRange Range(PartKind kind, NetworkPort port) => PartKindDisplays.Of(kind).Port(port).Range;

    /// <summary>What <paramref name="port"/> is called beside its bar, on a part of <paramref name="kind"/>.</summary>
    public static UiText ShortLabel(PartKind kind, NetworkPort port) => PartKindDisplays.Of(kind).Port(port).ShortLabel;
}

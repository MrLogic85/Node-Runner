using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The names parts show: a part's own name if it has one, else its kind's default (<see cref="PartKindDisplay.DefaultName"/>),
/// such as "Joint 2" or "Accel 1", numbered among the parts of its kind in list order. Names are labels only (#220).
/// A default name is translated; an own name is the player's and shows as written (#757).
/// </summary>
public static class PartNames
{
    public static UiText Display(IReadOnlyList<IPartDef> parts, int partId) =>
        Own(parts, partId) is { } own ? UiText.AsWritten(own) : Default(parts, partId);

    public static UiText Default(IReadOnlyList<IPartDef> parts, int partId)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var part = parts.FirstOrDefault(part => part.Id == partId)
            ?? throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
        var number = parts.Where(other => other.PartKind == part.PartKind).TakeWhile(other => other.Id != partId).Count() + 1;
        return PartKindDisplays.Of(part.PartKind).DefaultName(number);
    }

    public static string? Own(IReadOnlyList<IPartDef> parts, int partId)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.FirstOrDefault(part => part.Id == partId)?.Name;
    }
}

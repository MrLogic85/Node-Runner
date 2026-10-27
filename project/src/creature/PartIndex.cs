using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// Something kept for each part of a creature, found by the part's id, so a selection reaches its
/// visual or anchor without naming its kind. A part is added once, and a lookup finds nothing for an
/// id it does not hold or that names another kind.
/// </summary>
public sealed class PartIndex<TValue>
{
    private readonly Dictionary<int, (IPartDef Part, TValue Value)> _byId = [];

    /// <summary>Every part added, with its value.</summary>
    public IEnumerable<(IPartDef Part, TValue Value)> Entries => _byId.Values;

    public void Add(IPartDef part, TValue value)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (!_byId.TryAdd(part.Id, (part, value)))
        {
            throw new ArgumentException($"Part id {part.Id} is indexed twice, as a {_byId[part.Id].Part.PartKind} and a {part.PartKind}.", nameof(part));
        }
    }

    /// <summary>The part <paramref name="selection"/> names, with its value; false when no part of that kind has its id.</summary>
    public bool TryOf(CreatureElementSelection selection, out (IPartDef Part, TValue Value) entry)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return _byId.TryGetValue(selection.Id, out entry) && PartKinds.Of(entry.Part.PartKind).Element == selection.Kind;
    }

    /// <summary>The <paramref name="parts"/> not added, each as its kind and id, so a part kind left out shows where the index is built.</summary>
    public IReadOnlyList<string> Missing(IEnumerable<IPartDef> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return [.. parts.Where(part => !_byId.ContainsKey(part.Id)).Select(part => $"{part.PartKind} {part.Id}")];
    }
}

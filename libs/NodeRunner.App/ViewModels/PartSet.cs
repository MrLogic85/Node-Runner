using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The parts a Select selection or box holds, each once by its kind and id.</summary>
public sealed class PartSet
{
    private readonly HashSet<CreatureElementSelection> _parts;

    public PartSet(IEnumerable<CreatureElementSelection> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        _parts = [.. parts];
        if (_parts.Contains(null!))
        {
            throw new ArgumentException("A part set holds no null part.", nameof(parts));
        }
    }

    public static PartSet None { get; } = new([]);

    public int Count => _parts.Count;

    /// <summary>Every part in the set, kind by kind in <see cref="CreatureElementKind"/> order, then by id.</summary>
    public IEnumerable<CreatureElementSelection> Parts => _parts.OrderBy(part => part.Kind).ThenBy(part => part.Id);

    public static PartSet Of(CreatureElementSelection part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return new([part]);
    }

    public bool Contains(CreatureElementSelection part) => _parts.Contains(part);

    public bool Contains(CreatureElementKind kind, int id) => _parts.Contains(new CreatureElementSelection(kind, id));

    /// <summary>The ids of the parts of <paramref name="kind"/> in the set.</summary>
    public IReadOnlySet<int> IdsOf(CreatureElementKind kind) => _parts.Where(part => part.Kind == kind).Select(part => part.Id).ToHashSet();

    /// <summary>Whether <paramref name="other"/> holds exactly the same parts.</summary>
    public bool SetEquals(PartSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _parts.SetEquals(other._parts);
    }
}

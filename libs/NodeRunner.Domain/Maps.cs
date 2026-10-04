namespace NodeRunner.Domain;

/// <summary>
/// Every map, by id (#443). 0.13 has only Flat; more maps and the map loop come with #540.
/// </summary>
public static class Maps
{
    public static MapDef Flat { get; } = new(MapIds.Flat, new FlatGround());

    public static IReadOnlyList<MapDef> All { get; } = [Flat];

    /// <summary>The map Train setup selects and Training runs on: Flat until map choice (#540).</summary>
    public static MapDef Default => Flat;

    /// <summary>The map with <paramref name="id"/>; an unknown id fails loud.</summary>
    public static MapDef Get(string id) =>
        All.FirstOrDefault(map => map.Id == id)
        ?? throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown map.");
}

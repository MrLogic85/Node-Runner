namespace NodeRunner.Domain;

/// <summary>
/// A map a creation trains and simulates on (#443): a stable <see cref="Id"/> saved with its
/// training records, the <see cref="Name"/> the player reads and the <see cref="Ground"/> the
/// arena builds. The UI picks a map's glyph by its id.
/// </summary>
public sealed record MapDef
{
    public MapDef(string id, string name, MapGround ground)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(ground);

        Id = id;
        Name = name;
        Ground = ground;
    }

    public string Id { get; }

    public string Name { get; }

    public MapGround Ground { get; }
}

namespace NodeRunner.Domain;

/// <summary>
/// A map a creation trains and simulates on (#443): a stable <see cref="Id"/> saved with its
/// training records and the <see cref="Ground"/> the arena builds. The UI picks a map's name and
/// glyph by its id, so the name the player reads is translated with the rest of the UI (#759).
/// </summary>
public sealed record MapDef
{
    public MapDef(string id, MapGround ground)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(ground);

        Id = id;
        Ground = ground;
    }

    public string Id { get; }

    public MapGround Ground { get; }
}

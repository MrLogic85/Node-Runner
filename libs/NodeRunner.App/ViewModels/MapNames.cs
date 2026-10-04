using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>The name the player reads for each map in <see cref="Maps"/>, by its id.</summary>
public static class MapNames
{
    public static UiText Of(string mapId) => Maps.Get(mapId).Id switch
    {
        MapIds.Flat => UiText.Plain("Flat ground"),
        _ => throw new ArgumentOutOfRangeException(nameof(mapId), mapId, "This map has no name yet."),
    };
}

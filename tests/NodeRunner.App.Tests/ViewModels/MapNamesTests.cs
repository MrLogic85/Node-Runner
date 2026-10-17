using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class MapNamesTests
{
    [Fact]
    public void Of_NamesEveryMap_AndThrowsForAnUnknownOne()
    {
        Maps.All.ShouldAllBe(map => !string.IsNullOrWhiteSpace(MapNames.Of(map.Id).Message));
        MapNames.Of(MapIds.Flat).ShouldBe(UiText.Plain("Flat ground"));
        Should.Throw<ArgumentOutOfRangeException>(() => MapNames.Of("moon"));
    }
}

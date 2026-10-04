using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class MapNamesTests
{
    [Fact]
    public void Of_NamesEveryMap() =>
        Maps.All.ShouldAllBe(map => !string.IsNullOrWhiteSpace(MapNames.Of(map.Id).Message));

    [Fact]
    public void Of_Flat_IsFlatGround() => MapNames.Of(MapIds.Flat).ShouldBe(UiText.Plain("Flat ground"));

    [Fact]
    public void Of_AnUnknownMap_Throws() => Should.Throw<ArgumentOutOfRangeException>(() => MapNames.Of("moon"));
}

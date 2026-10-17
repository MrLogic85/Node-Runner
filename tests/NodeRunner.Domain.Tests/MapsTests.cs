namespace NodeRunner.Domain.Tests;

public sealed class MapsTests
{
    [Fact]
    public void Flat_IsTheOnlyMap_LevelEverywhere_AndFoundByItsStableId()
    {
        (Maps.Flat.Id, Maps.Default, Maps.Get("map-flat")).ShouldBe(("map-flat", Maps.Flat, Maps.Flat));
        Maps.All.ShouldBe([Maps.Flat]);
        new[] { 0, -1234.5, 98765.25 }.Select(Maps.Flat.Ground.HeightAt).ShouldAllBe(height => height == 0);
        Refuses.OutOfRange(() => Maps.Get("flat"));
        Refuses.Invalid(() => new MapDef(" ", new FlatGround()));
        Should.Throw<ArgumentNullException>(() => new MapDef("map-flat", null!));
    }
}

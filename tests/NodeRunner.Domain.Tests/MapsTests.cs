namespace NodeRunner.Domain.Tests;

public sealed class MapsTests
{
    [Fact]
    public void Flat_HasItsStableId()
    {
        Maps.Flat.Id.ShouldBe("map-flat");
        Maps.All.ShouldBe([Maps.Flat]);
        Maps.Default.ShouldBeSameAs(Maps.Flat);
    }

    [Fact]
    public void Get_FindsAMapById_AndFailsLoudOnAnUnknownOne()
    {
        Maps.Get("map-flat").ShouldBeSameAs(Maps.Flat);
        Should.Throw<ArgumentOutOfRangeException>(() => Maps.Get("flat"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1234.5)]
    [InlineData(98765.25)]
    public void FlatGround_IsLevelEverywhere_AndTheSameEveryTime(double x)
    {
        var ground = Maps.Flat.Ground;

        ground.HeightAt(x).ShouldBe(0);
        ground.HeightAt(x).ShouldBe(ground.HeightAt(x));
    }

    [Fact]
    public void MapDef_RequiresAnIdAndAGround()
    {
        Should.Throw<ArgumentException>(() => new MapDef(" ", new FlatGround()));
        Should.Throw<ArgumentNullException>(() => new MapDef("map-flat", null!));
    }
}

using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class EvolutionSetupTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(TrainSettingsDef.MinShadows, 30)]
    [InlineData(TrainSettingsDef.MaxShadows, 10)]
    public void For_SetsThePopulationAndTrialTicks_AndBreedsANextGeneration(int? shadows, int? seconds)
    {
        var settings = shadows is { } count ? new TrainSettingsDef(count, seconds!.Value) : null;
        var expected = settings ?? TrainSettingsDef.Default;

        var setup = EvolutionSetup.For(settings, 60);

        setup.Population.ShouldBe(expected.Shadows);
        setup.TrialTicks.ShouldBe(expected.RunLengthSeconds * 60);
        var genomes = Enumerable.Range(0, expected.Shadows).Select(i => new[] { (double)i, -i }).ToArray();
        var fitness = Enumerable.Range(0, expected.Shadows).Select(i => (double)i).ToArray();
        setup.Algorithm.NextGeneration(genomes, fitness, new Random(1)).Length.ShouldBe(expected.Shadows);
    }
}

using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class EvolutionSetupTests
{
    [Fact]
    public void For_NoSettings_UsesTheDefault()
    {
        var setup = EvolutionSetup.For(null, 60);

        setup.Population.ShouldBe(TrainSettingsDef.Default.Shadows);
        setup.TrialTicks.ShouldBe(TrainSettingsDef.Default.RunLengthSeconds * 60);
    }

    [Fact]
    public void For_Settings_SetsThePopulationAndTrialTicks()
    {
        var setup = EvolutionSetup.For(new TrainSettingsDef(TrainSettingsDef.MaxShadows, 30), 60);

        setup.Population.ShouldBe(TrainSettingsDef.MaxShadows);
        setup.TrialTicks.ShouldBe(1800);
    }

    [Theory]
    [InlineData(TrainSettingsDef.MinShadows)]
    [InlineData(TrainSettingsDef.MaxShadows)]
    public void For_AnyShadowsCount_BreedsANextGeneration(int shadows)
    {
        var setup = EvolutionSetup.For(new TrainSettingsDef(shadows, 10), 60);
        var genomes = Enumerable.Range(0, shadows).Select(i => new[] { (double)i, -i }).ToArray();
        var fitness = Enumerable.Range(0, shadows).Select(i => (double)i).ToArray();

        var next = setup.Algorithm.NextGeneration(genomes, fitness, new Random(1));

        next.Length.ShouldBe(shadows);
    }
}

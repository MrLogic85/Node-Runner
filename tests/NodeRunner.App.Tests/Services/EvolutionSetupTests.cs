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
        setup.NextTrialTicks(new Random(1)).ShouldBe(expected.RunLengthSeconds * 60);
        var genomes = Enumerable.Range(0, expected.Shadows).Select(i => new[] { (double)i, -i }).ToArray();
        var fitness = Enumerable.Range(0, expected.Shadows).Select(i => (double)i).ToArray();
        setup.Algorithm.NextGeneration(genomes, fitness, new Random(1)).Length.ShouldBe(expected.Shadows);
    }

    // A Fixed length, with or without a kept range, leaves the RNG where it was, so seeded runs train as before.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NextTrialTicks_WhenFixed_RunsTheFixedLength_AndTakesNoRngValue(bool keepsARange)
    {
        var settings = new TrainSettingsDef(8, 20, RunLengthMode.Fixed, keepsARange ? new RunLengthRangeDef(5, 60) : null);
        var setup = EvolutionSetup.For(settings, 60);
        var rng = new Random(3);

        setup.NextTrialTicks(rng).ShouldBe(20 * 60);

        rng.Next().ShouldBe(new Random(3).Next());
    }

    // Ranged draws one length a generation from its range, in ticks, the same for the same seed.
    [Fact]
    public void NextTrialTicks_WhenRanged_DrawsAStepFromTheRange_RepeatablyForASeed()
    {
        var settings = new TrainSettingsDef(8, 20, RunLengthMode.Ranged, new RunLengthRangeDef(10, 25));
        var setup = EvolutionSetup.For(settings, 60);

        int[] Lengths(int seed)
        {
            var rng = new Random(seed);
            return Enumerable.Range(0, 200).Select(_ => setup.NextTrialTicks(rng)).ToArray();
        }

        Lengths(7).ShouldBe(Lengths(7));
        Lengths(7).Distinct().Order().ShouldBe([600, 900, 1200, 1500]);
    }

    [Fact]
    public void DrawSeconds_WithEqualEnds_RunsThatLength_AndTakesOneRngValue()
    {
        var rng = new Random(5);

        EvolutionSetup.DrawSeconds(new RunLengthRangeDef(30, 30), rng).ShouldBe(30);

        var expected = new Random(5);
        expected.Next(1);
        rng.Next().ShouldBe(expected.Next());
    }
}

namespace NodeRunner.Domain.Tests;

public sealed class TrainSettingsDefTests
{
    [Theory]
    [InlineData(TrainSettingsDef.MinShadows, TrainSettingsDef.MinRunLengthSeconds)]
    [InlineData(TrainSettingsDef.MaxShadows, TrainSettingsDef.MaxRunLengthSeconds)]
    public void Constructor_AcceptsTheEnds(int shadows, int runLength)
    {
        var settings = new TrainSettingsDef(shadows, runLength);

        settings.Shadows.ShouldBe(shadows);
        settings.RunLengthSeconds.ShouldBe(runLength);
    }

    [Theory]
    [InlineData(TrainSettingsDef.MinShadows - 1, 10)]
    [InlineData(TrainSettingsDef.MaxShadows + 1, 10)]
    [InlineData(8, TrainSettingsDef.MinRunLengthSeconds - 1)]
    [InlineData(8, TrainSettingsDef.MaxRunLengthSeconds + 1)]
    public void Constructor_RejectsValuesOutsideTheRange(int shadows, int runLength)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainSettingsDef(shadows, runLength));
    }

    [Fact]
    public void Default_IsEightShadowsForTenSeconds()
    {
        TrainSettingsDef.Default.ShouldBe(new TrainSettingsDef(8, 10));
    }
}

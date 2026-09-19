namespace NodeRunner.Domain.Tests;

public sealed class ProgressionDefTests
{
    [Fact]
    public void DefaultProgressionHasNoUnlocks()
    {
        var progression = new ProgressionDef();

        progression.ExtraCoreUnlocked.ShouldBeFalse();
        progression.ExtraCoreUnlockedAtGeneration.ShouldBeNull();
    }

    [Fact]
    public void UnlockedProgressionRequiresGeneration()
    {
        Should.Throw<ArgumentException>(() => new ProgressionDef(true));
        Should.Throw<ArgumentOutOfRangeException>(() => new ProgressionDef(false, 0));
    }
}

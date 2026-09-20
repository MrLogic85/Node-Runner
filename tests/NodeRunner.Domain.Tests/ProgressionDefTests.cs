namespace NodeRunner.Domain.Tests;

public sealed class ProgressionDefTests
{
    [Fact]
    public void DefaultProgressionHasNoUnlocks()
    {
        var progression = new ProgressionDef();

        progression.ExtraCoreUnlocked.ShouldBeFalse();
        progression.ExtraCoreUnlockedAtGeneration.ShouldBeNull();
        progression.ExtraCoreUnlockedByCreationId.ShouldBeNull();
        progression.DefaultCreationsSeeded.ShouldBeFalse();
    }

    [Fact]
    public void UnlockedProgressionRequiresGeneration()
    {
        Should.Throw<ArgumentException>(() => new ProgressionDef(true));
        Should.Throw<ArgumentOutOfRangeException>(() => new ProgressionDef(false, 0));
    }

    [Fact]
    public void UnlockedProgressionCanRecordCreationAttribution()
    {
        var creationId = Guid.NewGuid();

        var progression = new ProgressionDef(true, 12, creationId);

        progression.ExtraCoreUnlocked.ShouldBeTrue();
        progression.ExtraCoreUnlockedAtGeneration.ShouldBe(12);
        progression.ExtraCoreUnlockedByCreationId.ShouldBe(creationId);
    }

    [Fact]
    public void ProgressionCanRecordDefaultCreationSeeding()
    {
        var progression = new ProgressionDef(defaultCreationsSeeded: true);

        progression.DefaultCreationsSeeded.ShouldBeTrue();
    }
}

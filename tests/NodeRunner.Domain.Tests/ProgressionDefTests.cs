namespace NodeRunner.Domain.Tests;

public sealed class ProgressionDefTests
{
    [Fact]
    public void DefaultProgressionIsNotSeeded()
    {
        new ProgressionDef().DefaultCreationsSeeded.ShouldBeFalse();
    }

    [Fact]
    public void ProgressionCanRecordDefaultCreationSeeding()
    {
        var progression = new ProgressionDef(DefaultCreationsSeeded: true);

        progression.DefaultCreationsSeeded.ShouldBeTrue();
    }
}

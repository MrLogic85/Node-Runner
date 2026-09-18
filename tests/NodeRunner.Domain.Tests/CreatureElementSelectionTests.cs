namespace NodeRunner.Domain.Tests;

public sealed class CreatureElementSelectionTests
{
    [Fact]
    public void Constructor_WithNegativeIndex_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CreatureElementSelection(CreatureElementKind.Beam, -1));
    }
}

namespace NodeRunner.ML.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void TestInfrastructure_IsWorking()
    {
        var answer = 42;
        answer.ShouldBe(42);
    }
}

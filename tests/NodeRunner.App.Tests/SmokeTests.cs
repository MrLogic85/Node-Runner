namespace NodeRunner.App.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void TestInfrastructure_IsWorking()
    {
        var answer = 42;
        answer.ShouldBe(42);
    }

    [Fact]
    public void NSubstitute_CanCreateMock()
    {
        var fake = Substitute.For<IExampleService>();
        fake.GetAnswer().Returns(42);

        fake.GetAnswer().ShouldBe(42);
    }

    public interface IExampleService
    {
        int GetAnswer();
    }
}

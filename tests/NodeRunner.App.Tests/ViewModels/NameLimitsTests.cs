using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NameLimitsTests
{
    // An emoji counts as one character and is never cut in half.
    [Theory]
    [InlineData("Copy of Walker", 14, "Copy of Walker")]
    [InlineData("Copy of Walker", 7, "Copy of")]
    [InlineData("Front leg left", 10, "Front leg")]
    [InlineData("🦀🦀🦀", 2, "🦀🦀")]
    public void Fit_CutsOnlyANameOverTheLimit_WithoutATrailingSpace(string name, int limit, string expected)
    {
        NameLimits.Fit(name, limit).ShouldBe(expected);
    }

    [Fact]
    public void Fit_ALimitBelowOne_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NameLimits.Fit("Walker", 0));
    }
}

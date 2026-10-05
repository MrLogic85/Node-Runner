using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NameLimitsTests
{
    [Theory]
    [InlineData("Walker", 10, "Walker")]
    [InlineData("Copy of Walker", 14, "Copy of Walker")]
    [InlineData("Copy of Walker", 7, "Copy of")]
    [InlineData("Front leg left", 10, "Front leg")]
    public void Fit_CutsOnlyANameOverTheLimit_WithoutATrailingSpace(string name, int limit, string expected)
    {
        NameLimits.Fit(name, limit).ShouldBe(expected);
    }

    [Fact]
    public void Fit_CountsAnEmojiAsOneCharacter_AndNeverCutsItInHalf()
    {
        NameLimits.Fit("🦀🦀🦀", 2).ShouldBe("🦀🦀");
        NameLimits.Fit("🦀🦀", 2).ShouldBe("🦀🦀");
    }

    [Fact]
    public void Fit_ALimitBelowOne_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => NameLimits.Fit("Walker", 0));
    }
}

using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiSizeChoiceTests
{
    [Theory]
    [InlineData(280, 300, 280)]
    [InlineData(280, 250, 250)]
    [InlineData(280, 10, 50)]
    public void Auto_IsTheScreensDefaultWithinTheRange(int auto, int max, int effective)
    {
        UiSizeChoice.Auto.Resolve(auto, max).ShouldBe(effective);
    }

    [Theory]
    [InlineData(155, 155)]
    [InlineData(300, 300)]
    [InlineData(10, 50)]
    public void Max_FollowsTheWindowsMax(int max, int effective)
    {
        UiSizeChoice.Max.Resolve(280, max).ShouldBe(effective);
    }

    [Theory]
    [InlineData(400, 300, 300)]
    [InlineData(100, 300, 100)]
    [InlineData(20, 300, 50)]
    [InlineData(155, 155, 155)]
    [InlineData(155, 300, 155)]
    public void Fixed_IsKeptAndLimitedToTheRange(int percent, int max, int effective)
    {
        UiSizeChoice.Fixed(percent).Resolve(280, max).ShouldBe(effective);
    }

    [Fact]
    public void Fixed_SnapsToTheStep_SoEqualSizesAreEqualChoices()
    {
        UiSizeChoice.Fixed(102).ShouldBe(UiSizeChoice.Fixed(100));
        UiSizeChoice.Fixed(102).Percent.ShouldBe(100);
    }

    [Fact]
    public void Max_IsNotTheFixedSizeItResolvesTo()
    {
        UiSizeChoice.Max.ShouldNotBe(UiSizeChoice.Fixed(300));
        UiSizeChoice.Auto.ShouldNotBe(UiSizeChoice.Fixed(280));
    }

    [Theory]
    [InlineData(100, 155, true)]
    [InlineData(100, 95, false)]
    [InlineData(50, 10, true)]
    public void Fits_IsFalseOnlyForAFixedSizeAboveMax(int percent, int max, bool fits)
    {
        UiSizeChoice.Fixed(percent).Fits(max).ShouldBe(fits);
        UiSizeChoice.Auto.Fits(max).ShouldBeTrue();
        UiSizeChoice.Max.Fits(max).ShouldBeTrue();
    }
}

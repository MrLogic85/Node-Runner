namespace NodeRunner.Domain.Tests;

public sealed class SettingsDefTests
{
    [Fact]
    public void Default_IsAutoUsePhoneWithSoundsAndTrainSetupsDefaultShadows()
    {
        SettingsDef.Default.ShouldBe(new SettingsDef(null, ThemeChoice.UsePhone, sounds: true, TrainSettingsDef.Default.Shadows));
    }

    // A UI size snaps to the 5% grid (halves up) and never below the smallest; Auto stays null.
    [Theory]
    [InlineData(null, null)]
    [InlineData(280, 280)]
    [InlineData(282, 280)]
    [InlineData(283, 285)]
    [InlineData(2, SettingsDef.MinUiSizePercent)]
    [InlineData(-100, SettingsDef.MinUiSizePercent)]
    [InlineData(int.MaxValue, 2147483645)]
    public void UiSize_SnapsToTheGrid_AtLeastTheSmallest(int? saved, int? expected)
    {
        new SettingsDef(uiSizePercent: saved).UiSizePercent.ShouldBe(expected);
        (SettingsDef.Default with { UiSizePercent = saved }).UiSizePercent.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, TrainSettingsDef.MinShadows)]
    [InlineData(32, 32)]
    [InlineData(1000, TrainSettingsDef.MaxShadows)]
    public void DefaultShadows_ClampsToTrainSetupsRange(int saved, int expected)
    {
        new SettingsDef(defaultShadows: saved).DefaultShadows.ShouldBe(expected);
        (SettingsDef.Default with { DefaultShadows = saved }).DefaultShadows.ShouldBe(expected);
    }

    [Fact]
    public void Theme_RejectsAValueOutsideTheChoices()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SettingsDef(theme: (ThemeChoice)7));
    }
}

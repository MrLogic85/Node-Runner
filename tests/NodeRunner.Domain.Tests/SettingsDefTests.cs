namespace NodeRunner.Domain.Tests;

public sealed class SettingsDefTests
{
    [Fact]
    public void Default_IsAutoUsePhoneWithSoundsTrainSetupsDefaultShadowsAndAHalfMetreGridWithoutSnap()
    {
        SettingsDef.Default.ShouldBe(new SettingsDef(null, ThemeChoice.UsePhone, sounds: true, TrainSettingsDef.Default.Shadows, gridSizeCm: 50, snapToGrid: false));
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

    // A grid size snaps to the nearest of 25, 50 and 100 cm; a tie goes to the larger.
    [Theory]
    [InlineData(25, 25)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(37, 25)]
    [InlineData(38, 50)]
    [InlineData(75, 100)]
    [InlineData(0, 25)]
    [InlineData(int.MinValue, 25)]
    [InlineData(int.MaxValue, 100)]
    public void GridSize_SnapsToTheNearestChoice(int saved, int expected)
    {
        new SettingsDef(gridSizeCm: saved).GridSizeCm.ShouldBe(expected);
        (SettingsDef.Default with { GridSizeCm = saved }).GridSizeCm.ShouldBe(expected);
    }

    [Fact]
    public void Theme_RejectsAValueOutsideTheChoices()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SettingsDef(theme: (ThemeChoice)7));
    }
}

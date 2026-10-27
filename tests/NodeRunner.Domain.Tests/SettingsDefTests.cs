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

    // A hand-edited size moves to the nearest choice as a Creation's grid does (BuildGridDefTests), and becomes a new Creation's grid.
    [Fact]
    public void GridSize_SnapsToTheNearestChoice_AndMakesTheNewCreationGrid()
    {
        new SettingsDef(gridSizeCm: 37, snapToGrid: true).Grid.ShouldBe(new BuildGridDef(25, snap: true));
        (SettingsDef.Default with { GridSizeCm = 75 }).GridSizeCm.ShouldBe(100);
    }

    [Fact]
    public void Theme_RejectsAValueOutsideTheChoices()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SettingsDef(theme: (ThemeChoice)7));
    }
}

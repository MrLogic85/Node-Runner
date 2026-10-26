using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiAppThemeTests
{
    [Theory]
    [InlineData(true, UiTokenType.Neon)]
    [InlineData(false, UiTokenType.Paper)]
    [InlineData(null, UiTokenType.Neon)]
    public void Phone_IsNeonWhenDarkAndPaperWhenLight_NeonWithoutAMode(bool? phoneDark, UiTokenType palette)
    {
        UiAppTheme.Resolve(UiThemeChoice.Phone, phoneDark).ShouldBe(palette);
    }

    [Theory]
    [InlineData(UiThemeChoice.Neon, UiTokenType.Neon)]
    [InlineData(UiThemeChoice.Paper, UiTokenType.Paper)]
    public void APalette_IsKeptWhateverThePhonesMode(UiThemeChoice choice, UiTokenType palette)
    {
        UiAppTheme.Resolve(choice, phoneDark: true).ShouldBe(palette);
        UiAppTheme.Resolve(choice, phoneDark: false).ShouldBe(palette);
        UiAppTheme.Resolve(choice, phoneDark: null).ShouldBe(palette);
    }
}

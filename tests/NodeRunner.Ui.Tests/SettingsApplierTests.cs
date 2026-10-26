using NodeRunner.Domain;
using NodeRunner.Hosts;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class SettingsApplierTests
{
    // Over every saved value, so one added without a UI choice fails here rather than at start.
    [Fact]
    public void ThemeOf_GivesTheUiChoiceForEverySavedOne()
    {
        Enum.GetValues<ThemeChoice>().ToDictionary(saved => saved, SettingsApplier.ThemeOf).ShouldBe(new Dictionary<ThemeChoice, UiThemeChoice>
        {
            [ThemeChoice.Neon] = UiThemeChoice.Neon,
            [ThemeChoice.Paper] = UiThemeChoice.Paper,
            [ThemeChoice.UsePhone] = UiThemeChoice.Phone,
        });
    }
}

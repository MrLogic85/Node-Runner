using NodeRunner.Domain;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Tests;

public sealed class ThemeChoicesTests
{
    // Over every saved value, so one added without a UI choice fails here rather than at start.
    [Fact]
    public void ToUi_GivesTheUiChoiceForEverySavedOne()
    {
        Enum.GetValues<ThemeChoice>().ToDictionary(saved => saved, ThemeChoices.ToUi).ShouldBe(new Dictionary<ThemeChoice, UiThemeChoice>
        {
            [ThemeChoice.Neon] = UiThemeChoice.Neon,
            [ThemeChoice.Paper] = UiThemeChoice.Paper,
            [ThemeChoice.UsePhone] = UiThemeChoice.Phone,
        });
    }

    // A theme card saves the theme it draws.
    [Fact]
    public void FromUi_UndoesToUiForEveryUiChoice()
    {
        Enum.GetValues<UiThemeChoice>().ShouldAllBe(ui => ThemeChoices.ToUi(ThemeChoices.FromUi(ui)) == ui);
    }
}

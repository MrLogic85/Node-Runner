using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>The saved theme choice and the UI library's, one for one.</summary>
public static class ThemeChoices
{
    public static UiThemeChoice ToUi(ThemeChoice theme) => theme switch
    {
        ThemeChoice.Neon => UiThemeChoice.Neon,
        ThemeChoice.Paper => UiThemeChoice.Paper,
        ThemeChoice.UsePhone => UiThemeChoice.Phone,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };

    public static ThemeChoice FromUi(UiThemeChoice theme) => theme switch
    {
        UiThemeChoice.Neon => ThemeChoice.Neon,
        UiThemeChoice.Paper => ThemeChoice.Paper,
        UiThemeChoice.Phone => ThemeChoice.UsePhone,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };
}

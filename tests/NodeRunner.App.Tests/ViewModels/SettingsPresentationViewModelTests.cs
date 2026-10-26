using System.ComponentModel;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SettingsPresentationViewModelTests
{
    [Fact]
    public void ChooseTheme_SavesItAndKeepsTheOtherSettings()
    {
        var repository = new InMemorySettingsRepository();
        repository.Save(SettingsDef.Default with { Theme = ThemeChoice.Neon, DefaultShadows = 20 });
        var settings = new SettingsService(repository);
        using var presentation = new SettingsPresentationViewModel(settings);
        presentation.Theme.ShouldBe(ThemeChoice.Neon);

        presentation.ChooseTheme(ThemeChoice.Paper);

        presentation.Theme.ShouldBe(ThemeChoice.Paper);
        repository.Load().ShouldBe(SettingsDef.Default with { Theme = ThemeChoice.Paper, DefaultShadows = 20 });
    }

    // The screen redraws on any change to the settings, also one made elsewhere, and not after it closes.
    [Fact]
    public void SettingsChange_RaisesPropertyChangedUntilDisposed()
    {
        var settings = new SettingsService(new InMemorySettingsRepository());
        var presentation = new SettingsPresentationViewModel(settings);
        var raised = 0;
        presentation.PropertyChanged += (_, _) => raised++;

        settings.Update(SettingsDef.Default with { Theme = ThemeChoice.Paper });

        raised.ShouldBe(1);
        presentation.Theme.ShouldBe(ThemeChoice.Paper);

        presentation.Dispose();
        settings.Update(SettingsDef.Default with { Theme = ThemeChoice.Neon });

        raised.ShouldBe(1);
    }

    [Fact]
    public void ChooseTheme_WhenSavingFails_KeepsThePreviousThemeAndRaisesNothing()
    {
        var repository = Substitute.For<ISettingsRepository>();
        repository.Load().Returns(SettingsDef.Default with { Theme = ThemeChoice.Neon });
        repository.When(r => r.Save(Arg.Any<SettingsDef>())).Do(_ => throw new IOException("Disk full."));
        using var presentation = new SettingsPresentationViewModel(new SettingsService(repository));
        var raised = new List<PropertyChangedEventArgs>();
        presentation.PropertyChanged += (_, args) => raised.Add(args);

        Should.Throw<IOException>(() => presentation.ChooseTheme(ThemeChoice.Paper));

        presentation.Theme.ShouldBe(ThemeChoice.Neon);
        raised.ShouldBeEmpty();
    }
}

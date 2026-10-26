using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Services;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Current_IsWhatTheRepositoryLoaded()
    {
        var repository = new InMemorySettingsRepository();
        var saved = SettingsDef.Default with { Theme = ThemeChoice.Paper };
        repository.Save(saved);

        new SettingsService(repository).Current.ShouldBe(saved);
    }

    // A change saves and is raised once; the same settings again do neither.
    [Fact]
    public void Update_SavesAndRaisesExactlyWhenTheSettingsChange()
    {
        var repository = new InMemorySettingsRepository();
        var service = new SettingsService(repository);
        var raised = new List<SettingsDef>();
        service.Changed += (_, settings) => raised.Add(settings);
        var changed = SettingsDef.Default with { DefaultShadows = 20 };

        service.Update(SettingsDef.Default);
        service.Update(changed);
        service.Update(changed with { });

        raised.ShouldBe([changed]);
        service.Current.ShouldBe(changed);
        repository.Load().ShouldBe(changed);
    }

    [Fact]
    public void Update_WhenSavingFails_KeepsTheCurrentSettingsAndRaisesNothing()
    {
        var repository = Substitute.For<ISettingsRepository>();
        repository.Load().Returns(SettingsDef.Default);
        repository.When(r => r.Save(Arg.Any<SettingsDef>())).Do(_ => throw new IOException("Disk full."));
        var service = new SettingsService(repository);
        var raised = 0;
        service.Changed += (_, _) => raised++;

        Should.Throw<IOException>(() => service.Update(SettingsDef.Default with { Sounds = false }));

        service.Current.ShouldBe(SettingsDef.Default);
        raised.ShouldBe(0);
    }
}

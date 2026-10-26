using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-settings-{Guid.NewGuid():N}");

    private string SettingsPath => Path.Combine(_directory, FileSettingsRepository.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, ThemeChoice.Neon, true, 8)]
    [InlineData(150, ThemeChoice.Paper, false, 2)]
    [InlineData(300, ThemeChoice.UsePhone, true, 100)]
    public void Save_ThenLoad_RoundTripsEverySetting(int? uiSize, ThemeChoice theme, bool sounds, int shadows)
    {
        var settings = new SettingsDef(uiSize, theme, sounds, shadows);

        new FileSettingsRepository(new TestStorageLocation(_directory)).Save(settings);

        new FileSettingsRepository(new TestStorageLocation(_directory)).Load().ShouldBe(settings);
    }

    // A saved number out of range loads clamped or snapped, and the file is left as it was.
    [Fact]
    public void Load_OfOutOfRangeNumbers_ClampsThem()
    {
        const string json = """{ "formatVersion": 1, "uiSizePercent": 12, "theme": "paper", "sounds": false, "defaultShadows": 500 }""";
        WriteSettings(json);

        var loaded = new FileSettingsRepository(new TestStorageLocation(_directory)).Load();

        loaded.ShouldBe(new SettingsDef(SettingsDef.MinUiSizePercent, ThemeChoice.Paper, sounds: false, TrainSettingsDef.MaxShadows));
        File.ReadAllText(SettingsPath).ShouldBe(json);
        QuarantinedFiles().ShouldBeEmpty();
    }

    // A missing file loads the defaults; one that fails to load (an unknown theme, a missing or
    // unknown field, not JSON) is quarantined and loads the defaults too.
    [Theory]
    [InlineData(null)]
    [InlineData("""{ "uiSizePercent": null, "theme": "dark", "sounds": true, "defaultShadows": 8 }""")]
    [InlineData("""{ "uiSizePercent": null, "theme": "neon", "sounds": true }""")]
    [InlineData("""{ "uiSizePercent": null, "theme": "neon", "sounds": true, "defaultShadows": 8, "volume": 1 }""")]
    [InlineData("""{ "uiSizePercent": "big", "theme": "neon", "sounds": true, "defaultShadows": 8 }""")]
    [InlineData("{ not valid json")]
    [InlineData("{}")]
    public void Load_OfAMissingOrBadFile_LoadsTheDefaults(string? json)
    {
        if (json is not null)
        {
            WriteSettings(json);
        }

        var loaded = new FileSettingsRepository(new TestStorageLocation(_directory)).Load();

        loaded.ShouldBe(SettingsDef.Default);
        QuarantinedFiles().Length.ShouldBe(json is null ? 0 : 1);
    }

    private void WriteSettings(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, json);
    }

    private string[] QuarantinedFiles() =>
        Directory.Exists(_directory) ? Directory.GetFiles(_directory, $"{FileSettingsRepository.FileName}.corrupt-*") : [];
}

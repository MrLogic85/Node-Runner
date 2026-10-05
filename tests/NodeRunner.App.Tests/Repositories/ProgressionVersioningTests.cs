using System.Text.Json.Nodes;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Guards docs/SAVE_FORMAT.md → "Versions and migration" for <c>progression.json</c>.
/// SaveExamples/0.13.0/progression.json was written by the 0.13.0 build and must keep loading;
/// never edit it.
/// </summary>
public sealed class ProgressionVersioningTests : IDisposable
{
    private const string _versionField = VersionedSaveFile<ProgressionDef>.VersionField;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-progression-versioning-{Guid.NewGuid():N}");

    private string ProgressionPath => Path.Combine(_directory, "progression.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Fixture013_LoadsWithoutAResetAndIsWrittenBackInTheCurrentVersion()
    {
        var original = Fixture013();
        WriteProgression(original);

        var progression = new FileProgressionRepository(new TestStorageLocation(_directory)).Load();

        progression.DefaultCreationsSeeded.ShouldBeTrue();
        QuarantinedFiles().ShouldBeEmpty();
        var written = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        written.First().Key.ShouldBe(_versionField);
        written[_versionField]!.GetValue<int>().ShouldBe(FileProgressionRepository.Format.CurrentVersion);
        written.Remove(_versionField);
        JsonNode.DeepEquals(written, JsonNode.Parse(original)).ShouldBeTrue();
        new FileProgressionRepository(new TestStorageLocation(_directory)).Load().ShouldBe(progression);
    }

    [Fact]
    public void Save_WritesTheVersionFirst()
    {
        new FileProgressionRepository(new TestStorageLocation(_directory)).Save(new ProgressionDef(DefaultCreationsSeeded: true));

        var written = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        written.First().Key.ShouldBe(_versionField);
        written[_versionField]!.GetValue<int>().ShouldBe(FileProgressionRepository.Format.CurrentVersion);
    }

    [Fact]
    public void Load_InTheCurrentVersion_LeavesTheFileAlone()
    {
        var repository = new FileProgressionRepository(new TestStorageLocation(_directory));
        repository.Save(new ProgressionDef(DefaultCreationsSeeded: true));
        var stamp = File.GetLastWriteTimeUtc(ProgressionPath).AddHours(-1);
        File.SetLastWriteTimeUtc(ProgressionPath, stamp);

        repository.Load().DefaultCreationsSeeded.ShouldBeTrue();

        File.GetLastWriteTimeUtc(ProgressionPath).ShouldBe(stamp);
    }

    [Fact]
    public void Load_WritesAMigratedFileBackInTheNewVersion()
    {
        var format = new VersionedSaveFile<ProgressionDef>([file =>
        {
            file["defaultCreationsSeeded"] = file["seeded"]!.DeepClone();
            file.Remove("seeded");
        }]);
        WriteProgression("""{ "seeded": true }""");

        new FileProgressionRepository(new TestStorageLocation(_directory), format).Load().DefaultCreationsSeeded.ShouldBeTrue();

        var written = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(2);
        written.ContainsKey("seeded").ShouldBeFalse();
        QuarantinedFiles().ShouldBeEmpty();
    }

    [Fact]
    public void Load_OfAFileNewerThanTheApp_QuarantinesItAndStartsFresh()
    {
        var file = JsonNode.Parse(Fixture013())!.AsObject();
        file.Insert(0, _versionField, FileProgressionRepository.Format.CurrentVersion + 1);
        WriteProgression(file.ToJsonString());

        new FileProgressionRepository(new TestStorageLocation(_directory)).Load().ShouldBe(new ProgressionDef());

        File.Exists(ProgressionPath).ShouldBeFalse();
        File.ReadAllText(QuarantinedFiles().ShouldHaveSingleItem()).ShouldBe(file.ToJsonString());
    }

    [Fact]
    public void Load_OfAFileWhoseMigrationFails_QuarantinesItAndStartsFresh()
    {
        var format = new VersionedSaveFile<ProgressionDef>([_ => throw new InvalidDataException("Cannot migrate.")]);
        WriteProgression(Fixture013());

        new FileProgressionRepository(new TestStorageLocation(_directory), format).Load().ShouldBe(new ProgressionDef());

        File.Exists(ProgressionPath).ShouldBeFalse();
        File.ReadAllText(QuarantinedFiles().ShouldHaveSingleItem()).ShouldBe(Fixture013());
    }

    [Fact]
    public void WriteBack_ThatFails_StillLoadsTheProgression()
    {
        // The migration runs before the write-back; a folder in the file's place makes the write fail.
        var format = new VersionedSaveFile<ProgressionDef>([_ =>
        {
            File.Delete(ProgressionPath);
            Directory.CreateDirectory(ProgressionPath);
        }]);
        WriteProgression(Fixture013());

        new FileProgressionRepository(new TestStorageLocation(_directory), format).Load().DefaultCreationsSeeded.ShouldBeTrue();

        QuarantinedFiles().ShouldBeEmpty();
    }

    private static string Fixture013() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "0.13.0", "progression.json"));

    private string[] QuarantinedFiles() => Directory.GetFileSystemEntries(_directory, "progression.json.corrupt-*");

    private void WriteProgression(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ProgressionPath, json);
    }
}

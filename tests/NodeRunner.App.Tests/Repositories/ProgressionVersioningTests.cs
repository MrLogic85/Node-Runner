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
    public void Fixture013_LoadsWithoutAReset_IsWrittenBackInTheCurrentVersion_AndThenLeftAlone()
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
        var stamp = File.GetLastWriteTimeUtc(ProgressionPath).AddHours(-1);
        File.SetLastWriteTimeUtc(ProgressionPath, stamp);
        new FileProgressionRepository(new TestStorageLocation(_directory)).Load().ShouldBe(progression);
        File.GetLastWriteTimeUtc(ProgressionPath).ShouldBe(stamp);
    }

    // A migrated file is written back in the new version; a write-back that fails (a folder in the file's
    // place) still loads; a file newer than the app, or whose migration fails, is quarantined.
    [Theory]
    [InlineData("migrated", true)]
    [InlineData("write-back fails", true)]
    [InlineData("newer", false)]
    [InlineData("migration fails", false)]
    public void Load_OfAnOlderOrNewerFile_WritesItBackOrQuarantinesIt(string file, bool loads)
    {
        var json = file == "migrated" ? """{ "seeded": true }""" : Fixture013();
        if (file == "newer")
        {
            var newer = JsonNode.Parse(json)!.AsObject();
            newer.Insert(0, _versionField, FileProgressionRepository.Format.CurrentVersion + 1);
            json = newer.ToJsonString();
        }

        var format = new VersionedSaveFile<ProgressionDef>([progression =>
        {
            switch (file)
            {
                case "migrated":
                    progression["defaultCreationsSeeded"] = progression["seeded"]!.DeepClone();
                    progression.Remove("seeded");
                    break;
                case "write-back fails":
                    File.Delete(ProgressionPath);
                    Directory.CreateDirectory(ProgressionPath);
                    break;
                case "migration fails":
                    throw new InvalidDataException("Cannot migrate.");
            }
        }]);
        WriteProgression(json);

        var loaded = new FileProgressionRepository(new TestStorageLocation(_directory), file == "newer" ? FileProgressionRepository.Format : format).Load();

        loaded.ShouldBe(loads ? new ProgressionDef(DefaultCreationsSeeded: true) : new ProgressionDef());
        if (loads)
        {
            QuarantinedFiles().ShouldBeEmpty();
        }
        else
        {
            File.Exists(ProgressionPath).ShouldBeFalse();
            File.ReadAllText(QuarantinedFiles().ShouldHaveSingleItem()).ShouldBe(json);
        }

        if (file == "migrated")
        {
            var written = JsonNode.Parse(File.ReadAllText(ProgressionPath))!.AsObject();
            written[_versionField]!.GetValue<int>().ShouldBe(2);
            written.ContainsKey("seeded").ShouldBeFalse();
        }
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

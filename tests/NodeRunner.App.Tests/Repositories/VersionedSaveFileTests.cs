using System.Text.Json;
using System.Text.Json.Nodes;
using NodeRunner.App.Repositories;

namespace NodeRunner.App.Tests.Repositories;

public sealed class VersionedSaveFileTests
{
    private const string _versionField = VersionedSaveFile<Sample>.VersionField;

    [Fact]
    public void Serialize_WritesTheCurrentVersionFirst_AndReadsBackNotOutdated()
    {
        var format = new VersionedSaveFile<Sample>([_ => { }]);
        var json = format.Serialize(new Sample("Walker"));

        var load = format.Deserialize(json, "sample");

        var file = JsonNode.Parse(json)!.AsObject();
        file.Select(property => property.Key).ShouldBe([_versionField, "name"]);
        file[_versionField]!.GetValue<int>().ShouldBe(2);
        load.ShouldBe(new VersionedLoad<Sample>(new Sample("Walker"), IsOutdated: false));
    }

    // A file without a version is the baseline, and outdated even while the baseline is current.
    [Theory]
    [InlineData("""{ "formatVersion": 2, "title": "Walker" }""", true, "2,3")]
    [InlineData("""{ "title": "Walker" }""", true, "1,2,3")]
    [InlineData("""{ "name": "Walker" }""", false, "")]
    public void Migrations_RunInOrderFromTheFilesVersion_BeforeTheStrictLoad(string json, bool withMigrations, string expected)
    {
        // The last migration renames "title" to "name".
        var ran = new List<int>();
        SaveMigration[] migrations = [.. Enumerable.Range(1, withMigrations ? 3 : 0).Select(step => (SaveMigration)(file =>
        {
            ran.Add(step);
            if (step == 3)
            {
                file["name"] = file["title"]!.DeepClone();
                file.Remove("title");
            }
        }))];
        var format = new VersionedSaveFile<Sample>(migrations);

        var load = format.Deserialize(json, "sample");

        load.ShouldBe(new VersionedLoad<Sample>(new Sample("Walker"), IsOutdated: true));
        string.Join(',', ran).ShouldBe(expected);
    }

    // Each load the format cannot read fails as bad data naming the file; a migration's bad-data failure
    // stays bad data, and its bug stays a bug.
    [Theory]
    [InlineData("""{ "formatVersion": 3, "name": "Walker" }""")]
    [InlineData("""{ "formatVersion": 0, "name": "Walker" }""")]
    [InlineData("""{ "formatVersion": "1", "name": "Walker" }""")]
    [InlineData("""{ "formatVersion": {}, "name": "Walker" }""")]
    [InlineData("""{ "formatVersion": null, "name": "Walker" }""")]
    [InlineData("[]")]
    public void Deserialize_AFileItCannotRead_FailsAsBadData(string json)
    {
        var format = new VersionedSaveFile<Sample>([_ => { }]);

        Should.Throw<InvalidDataException>(() => format.Deserialize(json, "sample")).Message.ShouldContain("'sample'");
    }

    [Fact]
    public void Deserialize_LoadsStrictly_AndKeepsAMigrationsFailure()
    {
        var current = new VersionedSaveFile<Sample>([_ => { }]);
        var badData = new VersionedSaveFile<Sample>([_ => throw new InvalidDataException("no title")]);
        var bug = new VersionedSaveFile<Sample>([_ => throw new InvalidOperationException("bug")]);

        Should.Throw<JsonException>(() => current.Deserialize("""{ "formatVersion": 2, "name": "Walker", "title": "Old" }""", "sample"))
            .Message.ShouldContain("title");
        Should.Throw<InvalidDataException>(() => badData.Deserialize("""{ "name": "Walker" }""", "sample")).Message.ShouldBe("no title");
        Should.Throw<InvalidOperationException>(() => bug.Deserialize("""{ "name": "Walker" }""", "sample"));
    }

    public sealed record Sample(string Name);
}

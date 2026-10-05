using System.Text.Json;
using System.Text.Json.Nodes;
using NodeRunner.App.Repositories;

namespace NodeRunner.App.Tests.Repositories;

public sealed class VersionedSaveFileTests
{
    private const string _versionField = VersionedSaveFile<Sample>.VersionField;

    [Fact]
    public void Serialize_WritesTheCurrentVersionFirst()
    {
        var format = new VersionedSaveFile<Sample>([_ => { }]);

        var file = JsonNode.Parse(format.Serialize(new Sample("Walker")))!.AsObject();

        file.Select(property => property.Key).ShouldBe([_versionField, "name"]);
        file[_versionField]!.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public void Deserialize_InTheCurrentVersion_IsNotOutdated()
    {
        var format = new VersionedSaveFile<Sample>([_ => { }]);

        var load = format.Deserialize(format.Serialize(new Sample("Walker")), "sample");

        load.Value.ShouldBe(new Sample("Walker"));
        load.IsOutdated.ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_WithoutAVersion_IsTheBaselineAndOutdated()
    {
        var format = new VersionedSaveFile<Sample>([]);

        var load = format.Deserialize("""{ "name": "Walker" }""", "sample");

        format.CurrentVersion.ShouldBe(VersionedSaveFile<Sample>.BaselineVersion);
        load.Value.ShouldBe(new Sample("Walker"));
        load.IsOutdated.ShouldBeTrue();
    }

    [Fact]
    public void Migrations_RunInOrderFromTheFilesVersion()
    {
        var ran = new List<int>();
        var format = new VersionedSaveFile<Sample>([_ => ran.Add(1), _ => ran.Add(2), _ => ran.Add(3)]);

        var load = format.Deserialize("""{ "formatVersion": 2, "name": "Walker" }""", "sample");

        ran.ShouldBe([2, 3]);
        load.IsOutdated.ShouldBeTrue();
    }

    [Fact]
    public void Migrations_AllRunForAFileWithoutAVersion()
    {
        var ran = new List<int>();
        var format = new VersionedSaveFile<Sample>([_ => ran.Add(1), _ => ran.Add(2)]);

        format.Deserialize("""{ "name": "Walker" }""", "sample");

        ran.ShouldBe([1, 2]);
        format.CurrentVersion.ShouldBe(3);
    }

    [Fact]
    public void Migration_ChangesTheShapeBeforeTheStrictLoad()
    {
        var format = new VersionedSaveFile<Sample>([file =>
        {
            file["name"] = file["title"]!.DeepClone();
            file.Remove("title");
        }]);

        format.Deserialize("""{ "title": "Walker" }""", "sample").Value.ShouldBe(new Sample("Walker"));
    }

    [Fact]
    public void Deserialize_StillLoadsStrictlyAfterTheMigrations()
    {
        var format = new VersionedSaveFile<Sample>([_ => { }]);

        Should.Throw<JsonException>(() => format.Deserialize("""{ "name": "Walker", "title": "Old" }""", "sample"))
            .Message.ShouldContain("title");
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"1\"")]
    [InlineData("1.5")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Deserialize_WithAVersionItCannotRead_Fails(string version)
    {
        var format = new VersionedSaveFile<Sample>([_ => { }]);

        Should.Throw<InvalidDataException>(() => format.Deserialize($$"""{ "formatVersion": {{version}}, "name": "Walker" }""", "sample"))
            .Message.ShouldContain("'sample'");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("1")]
    public void Deserialize_WhenTheFileIsNotAnObject_Fails(string json)
    {
        var format = new VersionedSaveFile<Sample>([]);

        Should.Throw<InvalidDataException>(() => format.Deserialize(json, "sample"));
    }

    [Fact]
    public void Migration_ThatFindsBadData_FailsTheLoad()
    {
        var format = new VersionedSaveFile<Sample>([_ => throw new InvalidDataException("no title")]);

        Should.Throw<InvalidDataException>(() => format.Deserialize("""{ "name": "Walker" }""", "sample"))
            .Message.ShouldBe("no title");
    }

    [Fact]
    public void Migration_ThatHasABug_IsNotTurnedIntoBadData()
    {
        var format = new VersionedSaveFile<Sample>([_ => throw new InvalidOperationException("bug")]);

        Should.Throw<InvalidOperationException>(() => format.Deserialize("""{ "name": "Walker" }""", "sample"));
    }

    public sealed record Sample(string Name);
}

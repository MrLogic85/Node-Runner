using System.Text;
using System.Text.Json.Nodes;
using NodeRunner.App.Builders;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Each format version's share dictionary (#899; docs/SAVE_FORMAT.md → "Share code"). A new
/// version's is made with <c>NODE_RUNNER_UPDATE_SHARE_DICTIONARY=1 dotnet test tests/NodeRunner.App.Tests
/// --filter ShareDictionaryTests</c>, then a second run builds it in. It never overwrites one.
/// </summary>
public sealed class ShareDictionaryTests
{
    private const string _updateVariable = "NODE_RUNNER_UPDATE_SHARE_DICTIONARY";

    // Share codes began with format version 4.
    private const int _firstVersion = 4;

    // The 0.13.0 walker's code in version 4. It must read for good, so version 4's dictionary never changes.
    private const string _walkerInVersion4 = "NR4.qx4tzUZoaYZcCGHJOZbEJF2cGcfC1NzQxAhOGpGQj3SoknMpcABKRkbPV4ZE5isAYb1wbQ";

    public static TheoryData<int> Versions() => new(Enumerable.Range(_firstVersion, FileCreationRepository.Format.CurrentVersion - _firstVersion + 1));

    [Fact]
    public void TheCurrentVersion_HasAShareDictionary()
    {
        var version = FileCreationRepository.Format.CurrentVersion;
        var path = Path.Combine(FindRepositoryRoot(), "libs", "NodeRunner.App", "Repositories", "ShareDictionaries", CreationShareCode.DictionaryName(version));
        if (Environment.GetEnvironmentVariable(_updateVariable) == "1" && !File.Exists(path))
        {
            File.WriteAllBytes(path, CreationShareCode.Json(Sample()));
        }

        CreationShareCode.Dictionary(version).ShouldNotBeNull(
            $"Format version {version} has no share dictionary. Run the App tests with {_updateVariable}=1, then again to build it in.");
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void EachShareDictionary_IsACreationFileInItsVersion(int version)
    {
        var json = Encoding.UTF8.GetString(CreationShareCode.Dictionary(version).ShouldNotBeNull());

        JsonNode.Parse(json)![VersionedSaveFile<CreationDef>.VersionField]!.GetValue<int>().ShouldBe(version);
        FileCreationRepository.Format.Deserialize(json, CreationShareCode.DictionaryName(version)).Value.Creature.Nodes.ShouldNotBeEmpty();
    }

    [Fact]
    public void ACodeFromVersion4_StillReads()
    {
        var build = CreationShareCode.Read(_walkerInVersion4).Build.ShouldNotBeNull();

        build.Name.ShouldBe("Walker");
        build.Creature.Pistons.Count.ShouldBe(2);
    }

    // A walker with every kind of part at its settings as Build adds it, so a code's JSON finds most
    // of its keys and values in the dictionary.
    private static CreationDef Sample()
    {
        var builder = new CreatureBuilder();
        var back = builder.AddNode(new Vector2D(0, -120));
        var front = builder.AddNode(new Vector2D(200, -120));
        var backFoot = builder.AddNode(new Vector2D(-40, 0));
        var frontFoot = builder.AddNode(new Vector2D(240, 0));
        var spine = builder.AddBeam(back, front);
        var backLeg = builder.AddBeam(back, backFoot);
        builder.AddBeam(front, frontFoot);
        builder.AddSensor(spine, SensorKind.Accelerometer, out _, out _).ShouldBeTrue();
        builder.AddSensor(backLeg, SensorKind.Camera, out _, out _).ShouldBeTrue();
        builder.AddPiston(back, frontFoot);
        builder.AddSpring(front, backFoot);
        builder.AddServo(back);
        builder.AddServo(front);
        return new CreationDef(Guid.NewGuid(), "Walker", builder.Build());
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NodeRunner.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find NodeRunner.slnx.");
    }
}

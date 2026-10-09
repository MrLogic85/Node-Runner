using System.Text;
using System.Text.Json.Nodes;
using NodeRunner.App.Builders;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>A Creation's build as a share code (#899; docs/SAVE_FORMAT.md → "Share code").</summary>
public sealed class CreationShareCodeTests
{
    [Fact]
    public void Code_RoundTripsTheBuild_WithItsNamesAndSettings_ButNotTheTraining()
    {
        var trained = LoadFixture013("trained-walker.creation.json").WithTrainSettings(new TrainSettingsDef(20, 30));
        trained.Training.ShouldNotBeNull();

        var read = CreationShareCode.Read(CreationShareCode.Create(trained));

        read.Refusal.ShouldBeNull();
        var build = read.Build.ShouldNotBeNull();
        build.Training.ShouldBeNull();
        build.TrainSettings.ShouldBeNull();
        build.Name.ShouldBe(trained.Name);
        SaveJson.Serialize(build.Creature).ShouldBe(SaveJson.Serialize(trained.Creature));
    }

    [Fact]
    public void EveryExample_AndASavedWalker_StaysWithinWhatBuildMakes_SoItShares()
    {
        foreach (var example in CreationExamples.All)
        {
            CreationShareCode.Read(CreationShareCode.Create(new CreationDef(example.Id, "Example", example.Creature))).Refusal.ShouldBeNull();
        }

        CreationShareCode.Read(Pack(Fixture013("trained-walker.creation.json"))).Refusal.ShouldBeNull();
    }

    [Fact]
    public void Code_RoundTripsAWheel_WithItsRadiusAndGrip()
    {
        var walker = LoadFixture013("walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var wheel = builder.AddWheel(walker.Creature.Nodes[0].Id);
        builder.SetParameter(wheel, PartParameterId.WheelRadius, 70);
        builder.SetParameter(wheel, PartParameterId.Grip, 0.3);
        var rolling = walker.WithCreature(builder.Build(), training: null);

        var build = CreationShareCode.Read(CreationShareCode.Create(rolling)).Build.ShouldNotBeNull();

        SaveJson.Serialize(build.Creature).ShouldBe(SaveJson.Serialize(rolling.Creature));
        build.Creature.Wheels.ShouldHaveSingleItem().Radius.ShouldBe(70);
    }

    [Fact]
    public void Code_IsItsVersionAndUrlSafeBase64_ShortEnoughToTypeIntoAChat()
    {
        var code = CreationShareCode.Create(LoadFixture013("trained-walker.creation.json"));

        code.ShouldMatch($"^NR{FileCreationRepository.Format.CurrentVersion}\\.[A-Za-z0-9_-]+$");
        code.Length.ShouldBeLessThan(200);
    }

    [Fact]
    public void Read_IgnoresWhiteSpaceAChatAppAddsToTheCode()
    {
        var code = CreationShareCode.Create(LoadFixture013("walker.creation.json"));
        var wrapped = $"  {code[..20]}\n{code[20..40]} \r\n{code[40..]}\n";

        CreationShareCode.Read(wrapped).Build.ShouldNotBeNull();
    }

    [Fact]
    public void Read_ACodeInAnOlderVersion_MigratesItLikeASavedFile()
    {
        var code = Pack(Fixture013("walker.creation.json"));

        var build = CreationShareCode.Read(code).Build.ShouldNotBeNull();

        build.Creature.Servos.ShouldBeEmpty();
        build.Creature.Wheels.ShouldBeEmpty();
        build.Creature.Pistons.ShouldAllBe(piston => piston.Start == 0.5);
    }

    [Fact]
    public void Read_ACodeFromANewerVersion_IsRefusedAsNewer_ThoughThisAppHasNoDictionaryForIt()
    {
        var newer = FileCreationRepository.Format.CurrentVersion + 1;

        CreationShareCode.Read($"NR{newer}.AAAA").Refusal.ShouldBe(ShareCodeRefusal.NewerVersion);
    }

    [Fact]
    public void Read_ACodeWhoseFileIsFromANewerVersion_IsRefusedAsNewer()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file[VersionedSaveFile<CreationDef>.VersionField] = FileCreationRepository.Format.CurrentVersion + 1;

        CreationShareCode.Read(Pack(file.ToJsonString())).Refusal.ShouldBe(ShareCodeRefusal.NewerVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public void Read_NothingPasted_IsRefusedAsEmpty(string? text) =>
        CreationShareCode.Read(text).Refusal.ShouldBe(ShareCodeRefusal.Empty);

    [Theory]
    [InlineData("hello")]
    [InlineData("https://example.com/walker")]
    [InlineData("eyJmb3JtYXRWZXJzaW9uIjo0fQ==")]
    [InlineData("NR")]
    [InlineData("NR.abc")]
    [InlineData("NRx.abc")]
    [InlineData("nr4.abc")]
    [InlineData("NR99999999999.abc")]
    public void Read_TextThatIsNotACode_IsRefusedAsNotACreation(string text) =>
        CreationShareCode.Read(text).Refusal.ShouldBe(ShareCodeRefusal.NotACreation);

    [Fact]
    public void Read_ACodeCutShortOfItsBuild_IsRefusedAsDamaged()
    {
        var code = CreationShareCode.Create(LoadFixture013("walker.creation.json"));

        CreationShareCode.Read(code[..(code.Length / 2)]).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
        CreationShareCode.Read(code[..^8]).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Theory]
    [InlineData("NR3.AAAA")]
    [InlineData("NR0.AAAA")]
    public void Read_ACodeFromAVersionWithoutADictionary_IsRefusedAsDamaged(string code) =>
        CreationShareCode.Read(code).Refusal.ShouldBe(ShareCodeRefusal.Damaged);

    [Theory]
    [InlineData('+')]
    [InlineData('/')]
    [InlineData('=')]
    public void Read_ACodeWithACharacterOutsideUrlSafeBase64_IsRefusedAsDamaged(char character) =>
        CreationShareCode.Read(CreationShareCode.Create(LoadFixture013("walker.creation.json")) + character).Refusal.ShouldBe(ShareCodeRefusal.Damaged);

    [Fact]
    public void Read_ACodeWithAChangedCharacter_IsRefusedAsDamaged()
    {
        var code = CreationShareCode.Create(LoadFixture013("walker.creation.json")).ToCharArray();
        var middle = (code.Length + 4) / 2;
        code[middle] = code[middle] == 'A' ? 'B' : 'A';

        CreationShareCode.Read(new string(code)).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{"formatVersion": 4, "id": "0d6c3c1e-2b7a-4f5e-9a51-7b1f6d2e8c40", "name": "Walker", "creature": null, "training": null, "trainSettings": null}""")]
    [InlineData("""{"formatVersion": "four"}""")]
    [InlineData("""{"formatVersion": 0}""")]
    [InlineData("""{"formatVersion": 1, "creature": {"pistons": []}, "training": []}""")]
    [InlineData("""{"formatVersion": 1, "creature": {"pistons": [{"id": 1, "stroke": 0.3}]}, "training": {"brain": {"neurons": [], "connections": [{"from": 1, "to": 2, "weight": 1}]}}}""")]
    public void Read_ACodeThatFailsToLoad_IsRefusedAsDamaged(string json) =>
        CreationShareCode.Read(Pack(json)).Refusal.ShouldBe(ShareCodeRefusal.Damaged);

    [Fact]
    public void Read_ACodeWithAnUnknownField_IsRefusedAsDamaged()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file["extra"] = 1;

        CreationShareCode.Read(Pack(file.ToJsonString())).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Fact]
    public void Read_ACodeLongerThanTheCap_IsRefused_ThoughItWouldLoad()
    {
        // A name of random letters barely packs, and loads cut to the name limit.
        var random = new Random(899);
        var name = new string([.. Enumerable.Range(0, 2 * CreationShareCode.MaxLength).Select(_ => (char)random.Next('a', 'z' + 1))]);
        var file = WalkerFile();
        file["name"] = name;
        var json = file.ToJsonString();
        var code = Pack(json);

        code.Length.ShouldBeGreaterThan(CreationShareCode.MaxLength);
        Encoding.UTF8.GetByteCount(json).ShouldBeLessThan(CreationShareCode.MaxJsonBytes);
        CreationShareCode.Read(code).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, ShareCodeRefusal.Damaged)]
    public void Read_ACodeThatUnpacksBeyondTheCap_IsRefused(int beyondTheCap, ShareCodeRefusal? refusal)
    {
        var code = Pack(PaddedWalker(CreationShareCode.MaxJsonBytes + beyondTheCap));

        code.Length.ShouldBeLessThan(CreationShareCode.MaxLength);
        CreationShareCode.Read(code).Refusal.ShouldBe(refusal);
    }

    [Fact]
    public void Read_ACodeThatIsNotUtf8_IsRefusedAsDamaged()
    {
        CreationShareCode.Read(Pack([0xFF, 0xFE, 0x7B])).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Fact]
    public void Read_ABuildWithNoJoints_IsRefusedAsNothingToBuild()
    {
        var empty = new CreationDef(Guid.NewGuid(), "Empty", new CreatureDef([], [], []));

        CreationShareCode.Read(CreationShareCode.Create(empty)).Refusal.ShouldBe(ShareCodeRefusal.NothingToBuild);
    }

    [Fact]
    public void Read_ANameLongerThanTheLimit_IsCut()
    {
        var walker = LoadFixture013("walker.creation.json");

        var build = CreationShareCode.Read(CreationShareCode.Create(walker.WithName(new string('W', 100)))).Build.ShouldNotBeNull();

        build.Name.ShouldBe(new string('W', NameLimits.Creation));
    }

    [Fact]
    public void Read_ANameThatIsBlankOnceCut_IsRefusedAsDamaged()
    {
        var walker = LoadFixture013("walker.creation.json");

        CreationShareCode.Read(CreationShareCode.Create(walker.WithName(new string(' ', NameLimits.Creation) + "x")))
            .Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Theory]
    [InlineData("nodes", "position", "x", 100_000.0)]
    [InlineData("pistons", null, "strength", 1e300)]
    [InlineData("pistons", null, "maxSpeed", 0.0)]
    public void Read_ABuildThatBuildCouldNotMake_IsRefusedAsDamaged(string parts, string? inner, string field, double value)
    {
        var file = WalkerFile();
        var part = file["creature"]![parts]![0]!;
        (inner is null ? part : part[inner]!)[field] = value;

        CreationShareCode.Read(Pack(file.ToJsonString())).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    [Fact]
    public void Read_AWheelBuildCouldNotMake_IsRefusedAsDamaged()
    {
        var file = WalkerFile();
        var creature = file["creature"]!;
        var id = creature["nextPartId"]!.GetValue<int>();
        creature["nextPartId"] = id + 1;
        var wheel = new JsonObject { ["id"] = id, ["nodeId"] = creature["nodes"]![0]!["id"]!.GetValue<int>(), ["name"] = null, ["radius"] = WheelDef.MaxRadius, ["grip"] = 0.8 };
        creature["wheels"] = new JsonArray(wheel);
        CreationShareCode.Read(Pack(file.ToJsonString())).Refusal.ShouldBeNull();

        wheel["radius"] = WheelDef.MaxRadius + 1;

        CreationShareCode.Read(Pack(file.ToJsonString())).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    private static JsonObject WalkerFile() =>
        JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();

    // The walker's file with white space added to it after its last brace, so it loads.
    private static string PaddedWalker(int bytes)
    {
        var json = WalkerFile().ToJsonString();
        return json + new string(' ', bytes - Encoding.UTF8.GetByteCount(json));
    }

    private static string Pack(string json) => Pack(Encoding.UTF8.GetBytes(json));

    private static string Pack(byte[] bytes) => CreationShareCode.Pack(FileCreationRepository.Format.CurrentVersion, bytes);

    private static CreationDef LoadFixture013(string name) =>
        FileCreationRepository.Format.Deserialize(Fixture013(name), name).Value;

    private static string Fixture013(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "0.13.0", name));
}

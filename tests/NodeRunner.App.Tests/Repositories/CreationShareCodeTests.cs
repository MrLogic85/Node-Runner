using System.Text;
using System.Text.Json.Nodes;
using NodeRunner.App.Builders;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>A Creation's build as a share code (docs/SAVE_FORMAT.md → "Share code").</summary>
public sealed class CreationShareCodeTests
{
    // A code is its version and URL-safe base64, short enough to type into a chat; white space a chat
    // app adds is ignored, a name beyond the limit is cut, and the training and the grid stay behind.
    [Fact]
    public void Code_OfATrainedWalker_ReadsAsItsBuild_WithoutTheTrainingOrTheGrid()
    {
        var trained = LoadFixture013("trained-walker.creation.json").WithTrainSettings(new TrainSettingsDef(20, 30));
        trained.Training.ShouldNotBeNull();
        var code = CreationShareCode.Create(trained);
        code.ShouldMatch($"^NR{FileCreationRepository.Format.CurrentVersion}\\.[A-Za-z0-9_-]+$");
        code.Length.ShouldBeLessThan(200);

        var read = CreationShareCode.Read($"  {code[..20]}\n{code[20..40]} \r\n{code[40..]}\n");

        (read.Refusal, read.UpdatedFrom).ShouldBe((null, null));
        var build = read.Build.ShouldNotBeNull();
        (build.Training, build.TrainSettings, build.Name).ShouldBe((null, null, trained.Name));
        CreationShareCode.Create(trained with { Grid = new BuildGridDef(25, snap: true) }).ShouldBe(code);
        SaveJson.Serialize(build.Creature).ShouldBe(SaveJson.Serialize(CreationShareCode.Rounded(trained).Creature));
        CreationShareCode.Read(CreationShareCode.Create(trained.WithName(new string('W', 100)))).Build.ShouldNotBeNull()
            .Name.ShouldBe(new string('W', NameLimits.Creation));
    }

    [Fact]
    public void EveryExample_AndASavedWalker_StaysWithinWhatBuildMakes_SoItShares()
    {
        foreach (var example in CreationExamples.All)
        {
            CreationShareCode.Read(CreationShareCode.Create(new CreationDef(example.Id, "Example", example.Creature))).Refusal.ShouldBeNull();
        }

        CreationShareCode.Read(Pack(FileCreationRepository.Format.Serialize(LoadFixture013("trained-walker.creation.json")))).Refusal.ShouldBeNull();
    }

    // A hinge part's side can be a Wheel or missing, which the every-part pin does not hold; the
    // rest reads back as Rounded leaves it.
    [Fact]
    public void Code_KeepsAHingePartsWheelSide_AndItsMissingSides()
    {
        var walker = LoadFixture013("walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var joints = builder.Nodes.Where(node => builder.LinksAt(node.Id).Count >= 2).Select(node => node.Id).ToArray();
        var wheel = builder.AddWheel(joints[0]);
        builder.AddBrake(joints[1], builder.LinksAt(joints[1])[0].Id, targetLinkId: null);
        builder.AddAngleSensor(joints[0], wheel, builder.LinksAt(joints[0])[0].Id);
        builder.AddAngleSensor(joints[2], fixedLinkId: null, builder.LinksAt(joints[2])[0].Id);
        var sided = walker.WithCreature(builder.Build(), training: null);

        var build = CreationShareCode.Read(CreationShareCode.Create(sided)).Build.ShouldNotBeNull().Creature;

        SaveJson.Serialize(build).ShouldBe(SaveJson.Serialize(CreationShareCode.Rounded(sided).Creature));
        build.IsWheel(build.AngleSensors[0].FixedLinkId!.Value).ShouldBeTrue();
        (build.Brakes.Single().TargetLinkId, build.AngleSensors[1].FixedLinkId).ShouldBe((null, null));
    }

    [Fact]
    public void Code_OfTwoHandPlacedJointsAndASpring_IsAtMost100Characters()
    {
        var builder = new CreatureBuilder();
        var left = builder.AddNode(new Vector2D(-87.31275, 12.8812));
        var right = builder.AddNode(new Vector2D(64.0391, -23.5517));
        builder.AddBeam(left, right);
        builder.AddSpring(left, right);

        CreationShareCode.Create(new CreationDef(Guid.NewGuid(), "Untitled Creation", builder.Build()))
            .Length.ShouldBeLessThanOrEqualTo(100);
    }

    // Joints go on whole units and settings to 3 decimals, except where rounding would take a setting
    // off its slider; a default that is not exact is left out and reads back exact.
    [Fact]
    public void Code_RoundsWhereTheSlidersAllow()
    {
        var builder = new CreatureBuilder();
        var left = builder.AddNode(new Vector2D(0, 0));
        var right = builder.AddNode(new Vector2D(100, 0));
        builder.SetParameter(builder.AddPiston(left, right), PartParameterId.Stroke, 6.0 / 7);
        builder.SetParameter(builder.AddServo(left), PartParameterId.Range, 2.0071286397934789);
        var built = builder.Build();
        var moved = new CreatureDef(
            [new NodeDef(left, new Vector2D(-0.4, 12.5)), new NodeDef(right, new Vector2D(99.6, -12.5))],
            built.Beams,
            built.Sensors,
            built.Servos,
            built.Pistons,
            built.Springs,
            built.Wheels,
            built.NextPartId);

        var rounded = CreationShareCode.Read(CreationShareCode.Create(new CreationDef(Guid.NewGuid(), "Walker", moved))).Build.ShouldNotBeNull().Creature;

        rounded.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 13), new Vector2D(100, -13)]);
        rounded.Pistons.ShouldHaveSingleItem().Stroke.ShouldBe(0.857);
        rounded.Servos.ShouldHaveSingleItem().Range.ShouldBe(2.007);

        var arms = new CreatureBuilder();
        var smallest = Math.PI / 9;
        CreatureBuilder.IsOnItsSlider(PartParameterId.Range, Math.Round(smallest, 3)).ShouldBeFalse();
        foreach (var x in new[] { 0, 300 })
        {
            var joint = arms.AddNode(new Vector2D(x, 0));
            arms.AddBeam(joint, arms.AddNode(new Vector2D(x + 100, 0)));
            arms.AddBeam(joint, arms.AddNode(new Vector2D(x, 100)));
            var servo = arms.AddServo(joint);
            if (x == 0)
            {
                arms.SetParameter(servo, PartParameterId.Range, smallest);
            }
        }

        var servos = CreationShareCode.Read(CreationShareCode.Create(new CreationDef(Guid.NewGuid(), "Arms", arms.Build()))).Build.ShouldNotBeNull().Creature.Servos;

        servos.Select(servo => servo.Range).ShouldBe([smallest, Math.PI]);
        servos[1].MaxSpeed.ShouldBe(2 * Math.PI);
    }

    [Fact]
    // Numbered in the same order, the network's ports keep theirs (NetworkPorts).
    public void Code_OfPartsWithGapsInTheirIds_ReadsAsTheSameBuild_NumberedInTheSameOrder()
    {
        var builder = new CreatureBuilder();
        var nodes = Enumerable.Range(0, 5).Select(index => builder.AddNode(new Vector2D(index * 60, index % 2 * 40))).ToArray();
        var beams = Enumerable.Range(1, 4).Select(index => builder.AddBeam(nodes[index - 1], nodes[index])).ToArray();
        builder.AddSensor(beams[0], SensorKind.Camera, out var camera, out _).ShouldBeTrue();
        builder.SetParameter(camera, PartParameterId.Aim, -0.5);
        builder.AddPiston(nodes[0], nodes[2]);
        builder.AddSensor(beams[2], SensorKind.Accelerometer, out _, out _).ShouldBeTrue();
        builder.AddServo(nodes[1]);
        builder.AddSpring(nodes[2], nodes[4]);
        builder.AddWheel(nodes[4]);
        builder.AddRubber(nodes[3]);
        builder.AddVelocityMotor(nodes[3]);
        builder.SetParameter(builder.AddBrake(nodes[2]), PartParameterId.BrakeStrength, BrakeDef.LockedStrength);
        builder.AddAngleSensor(nodes[0]);
        builder.RemoveNode(builder.AddNode(new Vector2D(300, 300)));
        builder.RemoveBeam(beams[3]);
        builder.AddBeam(nodes[3], nodes[4]);
        var gappy = builder.Build();

        var build = CreationShareCode.Read(CreationShareCode.Create(new CreationDef(Guid.NewGuid(), "Gappy", gappy)))
            .Build.ShouldNotBeNull().Creature;

        var renumbered = AllIds(gappy).Order().Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index + 1);
        SaveJson.Serialize(build).ShouldBe(SaveJson.Serialize(Renumbered(gappy, renumbered)));
    }

    // Whole-number settings that are counts, not part ids.
    private static readonly string[] _countFields = ["rays", "stepsPerTurn"];

    [Fact]
    public void PartIdFields_AreEveryPartsIdAndReference()
    {
        var partTypes = typeof(CreatureDef).GetProperties()
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0]);
        var idFields = partTypes.SelectMany(type => type.GetProperties())
            .Where(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
            .Select(property => char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
            .Except(_countFields);

        idFields.ToHashSet().ShouldBe(CreationShareCode.PartIdFields, ignoreOrder: true);
    }

    [Fact]
    public void Read_ACodeHoldingAnOlderFile_MigratesItLikeASavedFile()
    {
        var build = CreationShareCode.Read(Pack(Fixture013("walker.creation.json"))).Build.ShouldNotBeNull();

        build.Creature.Servos.ShouldBeEmpty();
        build.Creature.Wheels.ShouldBeEmpty();
        build.Creature.Pistons.ShouldAllBe(piston => piston.Start == 0.5);
    }

    // A Wheel's grip, left out at the template's 0.8 or written out, goes like a saved file's.
    [Theory]
    [InlineData(null)]
    [InlineData(1.0)]
    public void Read_AVersion8CodeWithAWheelsGrip_DropsIt_AndKeepsTheWheel(double? grip)
    {
        var sample = SampleLine(8);
        if (grip is { } value)
        {
            sample["creature"]!["wheels"]![0]!["grip"] = value;
        }

        var read = CreationShareCode.Read(CreationShareCode.Pack(8, Encoding.UTF8.GetBytes(sample.ToJsonString())));

        read.UpdatedFrom.ShouldBe(8);
        read.Build.ShouldNotBeNull().Creature.Wheels.ShouldBe([new WheelDef(15, 3, radius: 60)]);
    }

    // One row for each way Read refuses what was pasted.
    [Theory]
    [InlineData(null, ShareCodeRefusal.Empty)]
    [InlineData(" \n\t ", ShareCodeRefusal.Empty)]
    [InlineData("hello", ShareCodeRefusal.NotACreation)]
    [InlineData("NR", ShareCodeRefusal.NotACreation)]
    [InlineData("NR.abc", ShareCodeRefusal.NotACreation)]
    [InlineData("NRx.abc", ShareCodeRefusal.NotACreation)]
    [InlineData("NR99999999999.abc", ShareCodeRefusal.NotACreation)]
    [InlineData("a newer version, though this app has no dictionary for it", ShareCodeRefusal.NewerVersion)]
    [InlineData("a file from a newer version", ShareCodeRefusal.NewerVersion)]
    [InlineData("NR3.AAAA", ShareCodeRefusal.Damaged)]
    [InlineData("cut in half", ShareCodeRefusal.Damaged)]
    [InlineData("cut short", ShareCodeRefusal.Damaged)]
    [InlineData("a character outside URL-safe base64", ShareCodeRefusal.Damaged)]
    [InlineData("a changed character", ShareCodeRefusal.Damaged)]
    [InlineData("json:not json", ShareCodeRefusal.Damaged)]
    [InlineData("json:[1, 2, 3]", ShareCodeRefusal.Damaged)]
    [InlineData("""json:{"formatVersion": 4, "id": "0d6c3c1e-2b7a-4f5e-9a51-7b1f6d2e8c40", "name": "Walker", "creature": null, "training": null, "trainSettings": null}""", ShareCodeRefusal.Damaged)]
    [InlineData("""json:{"formatVersion": "four"}""", ShareCodeRefusal.Damaged)]
    [InlineData("""json:{"formatVersion": 0}""", ShareCodeRefusal.Damaged)]
    [InlineData("""json:{"formatVersion": 1, "creature": {"pistons": []}, "training": []}""", ShareCodeRefusal.Damaged)]
    [InlineData("""json:{"formatVersion": 1, "creature": {"pistons": [{"id": 1, "stroke": 0.3}]}, "training": {"brain": {"neurons": [], "connections": [{"from": 1, "to": 2, "weight": 1}]}}}""", ShareCodeRefusal.Damaged)]
    [InlineData("a Wheel's grip, an unknown field since #1144", ShareCodeRefusal.Damaged)]
    [InlineData("longer than the cap, though it would load", ShareCodeRefusal.Damaged)]
    [InlineData("unpacking to the cap", null)]
    [InlineData("unpacking beyond the cap", ShareCodeRefusal.Damaged)]
    [InlineData("empty parts filling out beyond the cap", ShareCodeRefusal.Damaged)]
    [InlineData("not UTF-8", ShareCodeRefusal.Damaged)]
    [InlineData("a name blank once cut", ShareCodeRefusal.Damaged)]
    [InlineData("no joints", ShareCodeRefusal.NothingToBuild)]
    public void Read_WhatCannotBeACreation_IsRefused(string? pasted, ShareCodeRefusal? refusal) =>
        CreationShareCode.Read(Pasted(pasted)).Refusal.ShouldBe(refusal);

    // Build's sliders hold every setting, and its area every joint, so only a code changed by hand
    // holds another.
    [Theory]
    [InlineData("nodes", "x", 100_000.0)]
    [InlineData("pistons", "maxSpeed", 0.0)]
    [InlineData("wheels", "radius", WheelDef.MaxRadius + 1)]
    public void Read_ABuildThatBuildCouldNotMake_IsRefusedAsDamaged(string parts, string field, double value)
    {
        var file = WalkerFileWith(parts, out var part);
        part[field] = value;

        CreationShareCode.Read(Pack(file.ToJsonString())).Refusal.ShouldBe(ShareCodeRefusal.Damaged);
    }

    private static string? Pasted(string? key)
    {
        var walkerCode = CreationShareCode.Create(LoadFixture013("walker.creation.json"));
        switch (key)
        {
            case "a newer version, though this app has no dictionary for it":
                return $"NR{FileCreationRepository.Format.CurrentVersion + 1}.AAAA";
            case "a file from a newer version":
                var newer = WalkerFile();
                newer[VersionedSaveFile<CreationDef>.VersionField] = FileCreationRepository.Format.CurrentVersion + 1;
                return Pack(newer.ToJsonString());
            case "cut in half":
                return walkerCode[..(walkerCode.Length / 2)];
            case "cut short":
                return walkerCode[..^8];
            case "a character outside URL-safe base64":
                return walkerCode + '+';
            case "a changed character":
                var code = walkerCode.ToCharArray();
                var middle = (code.Length + 4) / 2;
                code[middle] = code[middle] == 'A' ? 'B' : 'A';
                return new string(code);
            case "a Wheel's grip, an unknown field since #1144":
                var gripping = WalkerFileWith("wheels", out var wheel);
                wheel["grip"] = 0.8;
                return Pack(gripping.ToJsonString());
            case "longer than the cap, though it would load":
                // A name of random letters barely packs, and loads cut to the name limit.
                var random = new Random(899);
                var named = WalkerFile();
                named["name"] = new string([.. Enumerable.Range(0, 2 * CreationShareCode.MaxLength).Select(_ => (char)random.Next('a', 'z' + 1))]);
                Encoding.UTF8.GetByteCount(named.ToJsonString()).ShouldBeLessThan(CreationShareCode.MaxJsonBytes);
                var tooLong = Pack(named.ToJsonString());
                tooLong.Length.ShouldBeGreaterThan(CreationShareCode.MaxLength);
                return tooLong;
            case "unpacking to the cap":
                return Packed(PaddedWalker(CreationShareCode.MaxJsonBytes));
            case "unpacking beyond the cap":
                return Packed(PaddedWalker(CreationShareCode.MaxJsonBytes + 1));
            case "empty parts filling out beyond the cap":
                var parts = string.Join(',', Enumerable.Repeat("{}", CreationShareCode.MaxJsonBytes / 3));
                return Packed("{\"name\":\"x\",\"creature\":{\"nodes\":[" + parts + "]}}");
            case "not UTF-8":
                return Pack([0xFF, 0xFE, 0x7B]);
            case "a name blank once cut":
                return CreationShareCode.Create(LoadFixture013("walker.creation.json").WithName(new string(' ', NameLimits.Creation) + "x"));
            case "no joints":
                return CreationShareCode.Create(new CreationDef(Guid.NewGuid(), "Empty", new CreatureDef([], [], [])));
            case { } text when text.StartsWith("json:", StringComparison.Ordinal):
                return Pack(text["json:".Length..]);
            default:
                return key;
        }
    }

    // json packed in a code that stays within the cap on its length.
    private static string Packed(string json)
    {
        var code = Pack(json);
        code.Length.ShouldBeLessThan(CreationShareCode.MaxLength);
        return code;
    }

    // The walker's file with part one of parts to change: a joint's position, its piston, or a Wheel
    // added at its first joint.
    private static JsonObject WalkerFileWith(string parts, out JsonObject part)
    {
        var file = WalkerFile();
        var creature = file["creature"]!.AsObject();
        if (parts != "wheels")
        {
            part = parts == "nodes" ? creature["nodes"]![0]!["position"]!.AsObject() : creature["pistons"]![0]!.AsObject();
            return file;
        }

        var id = creature["nextPartId"]!.GetValue<int>();
        creature["nextPartId"] = id + 1;
        part = new JsonObject { ["id"] = id, ["nodeId"] = creature["nodes"]![0]!["id"]!.GetValue<int>(), ["name"] = null, ["radius"] = WheelDef.MinRadius };
        creature[parts] = new JsonArray(part);
        return file;
    }

    private static IEnumerable<int> AllIds(CreatureDef creature) =>
        creature.Nodes.Select(part => part.Id)
            .Concat(creature.Beams.Select(part => part.Id))
            .Concat(creature.Sensors.Select(part => part.Id))
            .Concat(creature.Servos.Select(part => part.Id))
            .Concat(creature.Pistons.Select(part => part.Id))
            .Concat(creature.Springs.Select(part => part.Id))
            .Concat(creature.Wheels.Select(part => part.Id))
            .Concat(creature.TouchSensors.Select(part => part.Id))
            .Concat(creature.Rubbers.Select(part => part.Id))
            .Concat(creature.VelocityMotors.Select(part => part.Id))
            .Concat(creature.Brakes.Select(part => part.Id))
            .Concat(creature.AngleSensors.Select(part => part.Id));

    // creature with every part id, and every reference to one, swapped through ids.
    private static CreatureDef Renumbered(CreatureDef creature, Dictionary<int, int> ids)
    {
        var json = JsonNode.Parse(SaveJson.Serialize(creature))!.AsObject();
        foreach (var part in json.Select(field => field.Value).OfType<JsonArray>().SelectMany(list => list).OfType<JsonObject>())
        {
            foreach (var (field, value) in part.ToArray())
            {
                if (CreationShareCode.PartIdFields.Contains(field) && value is not null)
                {
                    part[field] = ids[value.GetValue<int>()];
                }
            }
        }

        json["nextPartId"] = ids.Count + 1;
        return SaveJson.Deserialize<CreatureDef>(json.ToJsonString(), "renumbered");
    }

    private static JsonObject WalkerFile() =>
        JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();

    // The walker's file with white space added to it after its last brace, so it loads.
    private static string PaddedWalker(int bytes)
    {
        var json = WalkerFile().ToJsonString();
        return json + new string(' ', bytes - Encoding.UTF8.GetByteCount(json));
    }

    // The varied creation in version's share dictionary, as a code in that version holds it.
    private static JsonObject SampleLine(int version) =>
        JsonNode.Parse(Encoding.UTF8.GetString(CreationShareCode.Dictionary(version)!).Split('\n')[1])!.AsObject();

    private static string Pack(string json) => Pack(Encoding.UTF8.GetBytes(json));

    private static string Pack(byte[] bytes) => CreationShareCode.Pack(FileCreationRepository.Format.CurrentVersion, bytes);

    private static CreationDef LoadFixture013(string name) =>
        FileCreationRepository.Format.Deserialize(Fixture013(name), name).Value;

    private static string Fixture013(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "0.13.0", name));
}

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

    // Share codes began with format version 6, in 0.14.0.
    private const int _firstVersion = 6;

    // The 0.13.0 walker's code in version 6. It must read for good, so version 6's dictionary never changes.
    private const string _walkerInVersion6 = "NR6.IzL5wlMFlioTIYFRjWELI0vsxQIiRCxMzUExjyoQW1sLACFCsRE";

    // A joint, a beam and a 0.7 m Wheel at 30 % grip, coded in version 8 before #1144 dropped Grip.
    // It must read for good, so version 8's dictionary never changes.
    private const string _rollerInVersion8 = "NR8.gyfzoPycHCKTObiGxpY8DWDVN2b6M8biAxOUCgXmAXOkHG8MciMAlxLLLg";

    // Two joints, a beam, a Touch sensor and a Wheel, coded in version 9 before #1142 added Rubbers.
    // It must read for good, so version 9's dictionary never changes.
    private const string _feelerInVersion9 = "NR9.q8bSBIEmfLdUUHwQlfDBFTW2BGsAq8UxU6QxFj8heckIpzeQSgpDkB8AJ7jUBQ";

    // Two joints, a beam, a Wheel with a Servo and a Rubber, coded in version 10 before #454 added Velocity motors.
    // It must read for good, so version 10's dictionary never changes.
    private const string _spinnerInVersion10 = "NR10.g-eEYGC5kEdkVgDX89iSsAGsEYCZRo2xRKEpWgwao8egCZagQSp4DLH5Eqm0MAJ5EQAN-Pb1";

    // Three joints, two beams, a Wheel and a Velocity motor, coded in version 11 before #1039 added Brakes.
    // It must read for good, so version 11's dictionary never changes.
    private const string _rollerInVersion11 = "NR11.g-evoPycHCLzF7hBgy1fGMBaO1gkIVJAJVhyhQkoS2AGLVIJZ4QnCMxxV-Mm6CFgCvI0ACeIRoA";

    // Three joints, two beams and a Brake, coded in version 12 before #1204 added Angle sensors.
    // It must read for good, so version 12's dictionary never changes.
    private const string _benderInVersion12 = "NR12.g2d3p9S8FCKzO7gBhy2bGsBad1gkIVJAJVgyqQkoh2IGjhnu9ogJug9NQb4BAFDjhSs";

    // Three joints, two beams and an Angle sensor, coded in version 13 before #1036 added Steppers.
    // It must read for good, so version 13's dictionary never changes.
    private const string _benderInVersion13 = "NR13.gxciTql5KUQWIuA2HLbMbwBr4GGRhEgBlWDJ-iagfI_Lj2a4GyYm6F40BfkJAPybjmA";

    public static TheoryData<int> Versions() => new(Enumerable.Range(_firstVersion, FileCreationRepository.Format.CurrentVersion - _firstVersion + 1));

    [Fact]
    public void TheCurrentVersion_HasAShareDictionary()
    {
        var version = FileCreationRepository.Format.CurrentVersion;
        var path = Path.Combine(TestRepository.Root(), "libs", "NodeRunner.App", "Repositories", "ShareDictionaries", CreationShareCode.DictionaryName(version));
        if (Environment.GetEnvironmentVariable(_updateVariable) == "1" && !File.Exists(path))
        {
            File.WriteAllBytes(path, CreationShareCode.MakeDictionary(Template(), Sample()));
        }

        CreationShareCode.Dictionary(version).ShouldNotBeNull(
            $"Format version {version} has no share dictionary. Run the App tests with {_updateVariable}=1, then again to build it in.");
    }

    // Its first line is the template, and its second the sample as a code in that version holds it,
    // which still reads, with its 0.6 m Wheel.
    [Theory]
    [MemberData(nameof(Versions))]
    public void EachShareDictionary_StartsWithACreationFileInItsVersion_AndItsSampleReads(int version)
    {
        var lines = Encoding.UTF8.GetString(CreationShareCode.Dictionary(version).ShouldNotBeNull()).Split('\n');

        JsonNode.Parse(lines[0])![VersionedSaveFile<CreationDef>.VersionField]!.GetValue<int>().ShouldBe(version);
        FileCreationRepository.Format.Deserialize(lines[0], CreationShareCode.DictionaryName(version)).Value.Creature.Nodes.ShouldNotBeEmpty();
        var read = CreationShareCode.Read(CreationShareCode.Pack(version, Encoding.UTF8.GetBytes(lines[1])));
        read.Refusal.ShouldBeNull();
        read.UpdatedFrom.ShouldBe(version < FileCreationRepository.Format.CurrentVersion ? version : null);
        read.Build.ShouldNotBeNull().Creature.Wheels.ShouldHaveSingleItem().Radius.ShouldBe(60);
    }

    [Fact]
    public void ACodeFromVersion6_StillReads_AsTheBuildItWasMadeFrom()
    {
        var walker = FileCreationRepository.Format.Deserialize(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "0.13.0", "walker.creation.json")),
            "walker").Value;

        var read = CreationShareCode.Read(_walkerInVersion6);

        var build = read.Build.ShouldNotBeNull();
        build.Name.ShouldBe("Walker");
        SaveJson.Serialize(build.Creature).ShouldBe(SaveJson.Serialize(CreationShareCode.Rounded(walker).Creature));
        read.UpdatedFrom.ShouldBe(_firstVersion < FileCreationRepository.Format.CurrentVersion ? _firstVersion : null);
    }

    // Each pinned code was made just before the next version added a kind of part (or #1144 dropped
    // Grip); it reads as the build it was made from, other than its nodes and beams.
    public static TheoryData<string, int, string?, Vector2D[], IPartDef[]> OldCodes() => new()
    {
        { _rollerInVersion8, 8, null, [new(0, 0), new(100, 0)], [new WheelDef(4, 1, radius: 70)] },
        { _feelerInVersion9, 9, "Feeler", [new(0, 0), new(100, 0)], [new TouchSensorDef(4, 1), new WheelDef(5, 2)] },
        { _spinnerInVersion10, 10, "Spinner", [new(0, 0), new(100, 0)], [new WheelDef(4, 1), new ServoDef(5, 1, 3, 4), new RubberDef(6, 2)] },
        { _rollerInVersion11, 11, "Roller", [new(0, 0), new(100, 0), new(0, 100)], [new WheelDef(6, 2), new VelocityMotorDef(7, 1, 4, 5)] },
        { _benderInVersion12, 12, "Bender", [new(0, 0), new(100, 0), new(0, 100)], [new BrakeDef(6, 1, 4, 5)] },
        { _benderInVersion13, 13, "Bender", [new(0, 0), new(100, 0), new(0, 100)], [new AngleSensorDef(6, 1, 4, 5)] },
    };

    [Theory]
    [MemberData(nameof(OldCodes))]
    public void APinnedCodeFromAnOlderVersion_StillReads(string code, int version, string? name, Vector2D[] nodes, IPartDef[] otherParts)
    {
        var read = CreationShareCode.Read(code);

        var build = read.Build.ShouldNotBeNull();
        if (name is not null)
        {
            build.Name.ShouldBe(name);
        }

        build.Creature.Nodes.Select(node => node.Position).ShouldBe(nodes);
        build.Creature.Parts.Where(part => part is not NodeDef and not BeamDef).OrderBy(part => part.Id).ShouldBe(otherParts);
        read.UpdatedFrom.ShouldBe(version);
    }

    // A walker with every kind of part at its settings as Build adds it and named as a new creation
    // is (NewCreationWorkflow.UntitledName), so a code leaves out what it didn't change.
    private static CreationDef Template()
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
        builder.AddAngleSensor(front);
        builder.AddWheel(frontFoot);
        builder.AddTouchSensor(backFoot);
        builder.AddRubber(backFoot);
        builder.AddVelocityMotor(frontFoot);
        builder.AddBrake(backFoot);
        var belly = builder.AddNode(new Vector2D(100, 0));
        builder.AddBeam(backFoot, belly);
        builder.AddBeam(frontFoot, belly);
        builder.AddStepper(belly);
        return new CreationDef(Guid.NewGuid(), "Untitled Creation", builder.Build());
    }

    // A walker drawn by hand with every kind of part, some of them changed, so a code finds its own
    // shape in the dictionary: joints off the template's, and settings and ids left out or not.
    private static CreationDef Sample()
    {
        var builder = new CreatureBuilder();
        var back = builder.AddNode(new Vector2D(-153, -84));
        var front = builder.AddNode(new Vector2D(61, -77));
        var backFoot = builder.AddNode(new Vector2D(-171, 32));
        var frontFoot = builder.AddNode(new Vector2D(118, 26));
        var tail = builder.AddNode(new Vector2D(-38, 95));
        var spine = builder.AddBeam(back, front);
        var backLeg = builder.AddBeam(back, backFoot);
        builder.AddBeam(front, frontFoot);
        builder.AddBeam(backFoot, tail);
        builder.AddSensor(spine, SensorKind.Camera, out var camera, out _).ShouldBeTrue();
        builder.SetParameter(camera, PartParameterId.Rays, 5);
        builder.SetParameter(camera, PartParameterId.CameraRange, 350);
        builder.AddSensor(backLeg, SensorKind.Accelerometer, out _, out _).ShouldBeTrue();
        var piston = builder.AddPiston(front, tail);
        builder.SetParameter(piston, PartParameterId.Strength, 20000);
        builder.SetParameter(piston, PartParameterId.Stroke, 0.75);
        var spring = builder.AddSpring(frontFoot, tail);
        builder.SetParameter(spring, PartParameterId.Stiffness, 250);
        builder.SetParameter(spring, PartParameterId.CoilLength, 0.45);
        var servo = builder.AddServo(back);
        builder.SetParameter(servo, PartParameterId.ServoStrength, 800000);
        var wheel = builder.AddWheel(backFoot);
        builder.SetParameter(wheel, PartParameterId.WheelRadius, 60);
        builder.AddTouchSensor(backFoot);
        builder.AddTouchSensor(frontFoot);
        builder.AddRubber(backFoot);
        builder.AddRubber(frontFoot);
        var velocityMotor = builder.AddVelocityMotor(front);
        builder.SetParameter(velocityMotor, PartParameterId.MotorMaxSpeed, 4);
        builder.SetParameter(builder.AddBrake(backFoot, backLeg, wheel), PartParameterId.BrakeStrength, BrakeDef.LockedStrength);
        builder.SetParameter(builder.AddBrake(frontFoot), PartParameterId.BrakeStrength, 1200000);
        builder.Rename(builder.AddAngleSensor(tail), "Tail");
        var hip = builder.AddNode(new Vector2D(-47, -12));
        builder.AddBeam(back, hip);
        builder.AddBeam(hip, tail);
        var stepper = builder.AddStepper(hip);
        builder.SetParameter(stepper, PartParameterId.StepsPerTurn, 12);
        builder.SetParameter(stepper, PartParameterId.MaxFrequency, 1.5);
        return new CreationDef(Guid.NewGuid(), "Walker", builder.Build());
    }
}

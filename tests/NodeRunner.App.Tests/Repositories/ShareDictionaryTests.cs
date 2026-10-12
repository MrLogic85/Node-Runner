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

    public static TheoryData<int> Versions() => new(Enumerable.Range(_firstVersion, FileCreationRepository.Format.CurrentVersion - _firstVersion + 1));

    [Fact]
    public void TheCurrentVersion_HasAShareDictionary()
    {
        var version = FileCreationRepository.Format.CurrentVersion;
        var path = Path.Combine(FindRepositoryRoot(), "libs", "NodeRunner.App", "Repositories", "ShareDictionaries", CreationShareCode.DictionaryName(version));
        if (Environment.GetEnvironmentVariable(_updateVariable) == "1" && !File.Exists(path))
        {
            File.WriteAllBytes(path, CreationShareCode.MakeDictionary(Template(), Sample()));
        }

        CreationShareCode.Dictionary(version).ShouldNotBeNull(
            $"Format version {version} has no share dictionary. Run the App tests with {_updateVariable}=1, then again to build it in.");
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void EachShareDictionary_StartsWithACreationFileInItsVersion(int version)
    {
        var json = Encoding.UTF8.GetString(CreationShareCode.Dictionary(version).ShouldNotBeNull()).Split('\n')[0];

        JsonNode.Parse(json)![VersionedSaveFile<CreationDef>.VersionField]!.GetValue<int>().ShouldBe(version);
        FileCreationRepository.Format.Deserialize(json, CreationShareCode.DictionaryName(version)).Value.Creature.Nodes.ShouldNotBeEmpty();
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

    [Fact]
    public void ACodeFromVersion8_WithAGrippyWheel_StillReads_WithoutItsGrip()
    {
        var read = CreationShareCode.Read(_rollerInVersion8);

        var creature = read.Build.ShouldNotBeNull().Creature;
        creature.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);
        creature.Beams.ShouldHaveSingleItem().Id.ShouldBe(3);
        var wheel = creature.Wheels.ShouldHaveSingleItem();
        (wheel.Id, wheel.NodeId, wheel.Radius).ShouldBe((4, 1, 70.0));
        read.UpdatedFrom.ShouldBe(8);
    }

    [Fact]
    public void ACodeFromVersion9_StillReads_WithoutRubbers()
    {
        var read = CreationShareCode.Read(_feelerInVersion9);

        var build = read.Build.ShouldNotBeNull();
        build.Name.ShouldBe("Feeler");
        var creature = build.Creature;
        creature.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);
        creature.TouchSensors.ShouldBe([new TouchSensorDef(4, 1)]);
        creature.Wheels.ShouldBe([new WheelDef(5, 2)]);
        creature.Rubbers.ShouldBeEmpty();
        read.UpdatedFrom.ShouldBe(9);
    }

    [Fact]
    public void ACodeFromVersion10_StillReads_WithoutVelocityMotors()
    {
        var read = CreationShareCode.Read(_spinnerInVersion10);

        var build = read.Build.ShouldNotBeNull();
        build.Name.ShouldBe("Spinner");
        var creature = build.Creature;
        creature.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);
        creature.Wheels.ShouldBe([new WheelDef(4, 1)]);
        creature.Servos.ShouldBe([new ServoDef(5, 1, 3, 4)]);
        creature.Rubbers.ShouldBe([new RubberDef(6, 2)]);
        creature.VelocityMotors.ShouldBeEmpty();
        read.UpdatedFrom.ShouldBe(10);
    }

    [Fact]
    public void ACodeFromVersion11_StillReads_WithoutBrakes()
    {
        var read = CreationShareCode.Read(_rollerInVersion11);

        var build = read.Build.ShouldNotBeNull();
        build.Name.ShouldBe("Roller");
        var creature = build.Creature;
        creature.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0), new Vector2D(0, 100)]);
        creature.Wheels.ShouldBe([new WheelDef(6, 2)]);
        creature.VelocityMotors.ShouldBe([new VelocityMotorDef(7, 1, 4, 5)]);
        creature.Brakes.ShouldBeEmpty();
        read.UpdatedFrom.ShouldBe(11);
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
        builder.AddServo(front);
        builder.AddWheel(frontFoot);
        builder.AddTouchSensor(backFoot);
        builder.AddRubber(backFoot);
        builder.AddVelocityMotor(frontFoot);
        builder.AddBrake(backFoot);
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

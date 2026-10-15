using System.Text.Json.Nodes;
using NodeRunner.App.Builders;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;
using NodeRunner.ML.Networks;

namespace NodeRunner.App.Tests.Repositories;

/// <summary>
/// Guards docs/SAVE_FORMAT.md → "Versions and migration" for <c>creation.json</c>. The files in
/// SaveExamples/0.13.0/ were written by the 0.13.0 build and must keep loading; never edit them.
/// </summary>
public sealed class CreationVersioningTests : IDisposable
{
    private const string _versionField = VersionedSaveFile<CreationDef>.VersionField;

    private static readonly IEqualityComparer<CreationDef> _creationComparer =
        EqualityComparer<CreationDef>.Create((a, b) => SaveJson.Serialize(a) == SaveJson.Serialize(b), creation => creation.Id.GetHashCode());

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"node-runner-versioning-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("walker.creation.json")]
    [InlineData("trained-walker.creation.json")]
    public void Fixture013_LoadsAndIsWrittenBackInTheCurrentVersion(string fixture)
    {
        var original = Fixture013(fixture);
        var path = WriteCreation(original);

        var loaded = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written.First().Key.ShouldBe(_versionField);
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written.Remove(_versionField);
        var expected = JsonNode.Parse(original)!.AsObject();
        expected["creature"]!.AsObject()["servos"] = new JsonArray();
        expected["creature"]!.AsObject()["wheels"] = new JsonArray();
        expected["creature"]!.AsObject()["touchSensors"] = new JsonArray();
        expected["creature"]!.AsObject()["rubbers"] = new JsonArray();
        expected["creature"]!.AsObject()["velocityMotors"] = new JsonArray();
        expected["creature"]!.AsObject()["brakes"] = new JsonArray();
        expected["creature"]!.AsObject()["angleSensors"] = new JsonArray();
        expected["creature"]!.AsObject()["steppers"] = new JsonArray();
        foreach (var piston in expected["creature"]!["pistons"]!.AsArray())
        {
            piston!["stroke"] = 2 * 0.3 / (1 - 0.3);
            piston["start"] = 0.5;
        }

        // Its sensor is an Accelerometer, which has no Camera settings (#578).
        foreach (var sensor in expected["creature"]!["sensors"]!.AsArray())
        {
            sensor!["rays"] = null;
            sensor["spread"] = null;
            sensor["range"] = null;
        }

        // The network's Piston length weights move too; Fixture013_ItsTrainedNetworkDrivesItsPistonsAsBefore covers them.
        written["training"]?.AsObject().Remove("network");
        expected["training"]?.AsObject().Remove("brain");
        JsonNode.DeepEquals(written, expected).ShouldBeTrue();
        new FileCreationRepository(new TestStorageLocation(_directory)).Get(loaded.Id).ShouldBe(loaded, _creationComparer);
    }

    // #870: ±s of the built length becomes 2s / (1 − s) of the shortest, from the middle; a Piston
    // can now at most double, so ±50% becomes 100%.
    [Theory]
    [InlineData(0.1, 2.0 / 9)]
    [InlineData(0.3, 6.0 / 7)]
    [InlineData(0.5, 1)]
    public void Fixture013_ItsPistonsKeepTheirShortestAndLongestLengths_UpToDoubling(double oldStroke, double stroke)
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["creature"]!["pistons"]![0]!["stroke"] = oldStroke;
        file["creature"]!["pistons"]![1]!.AsObject().Remove("stroke");
        WriteCreation(file.ToJsonString());

        var pistons = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Pistons;

        pistons[0].Stroke.ShouldBe(stroke, tolerance: 1e-12);
        pistons[0].Start.ShouldBe(0.5);
        pistons[1].Stroke.ShouldBe(6.0 / 7, tolerance: 1e-12);
        pistons[1].Start.ShouldBe(0.5);
    }

    // #835: a Spring had no travel; it gets the widest one with its drawn length in the middle.
    [Fact]
    public void Fixture013_WithASpring_GivesItTheWidestTravelAroundItsDrawnLength()
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["creature"]!["springs"] = JsonNode.Parse("""[{ "id": 50, "nodeA": 3, "nodeB": 4, "name": null, "stiffness": 300, "damping": 5 }]""");
        file["creature"]!["nextPartId"] = 51;
        WriteCreation(file.ToJsonString());

        var spring = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Springs.ShouldHaveSingleItem();

        spring.ShouldBe(new SpringDef(50, 3, 4, null, 300, 5, stroke: 1, coilLength: 0.55));
    }

    // #578: a Camera saved before it had settings keeps today's fan.
    [Fact]
    public void AVersion5Save_WithACamera_GivesItTodaysFan()
    {
        var file = Version5Walker();
        file["creature"]!["sensors"]![0]!["kind"] = "camera";
        file["creature"]!["sensors"]![0]!["aim"] = 0.25;
        WriteCreation(file.ToJsonString());

        var camera = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Sensors.ShouldHaveSingleItem();

        camera.ShouldBe(new SensorDef(8, 5, SensorKind.Camera, aim: 0.25, rays: 3, spread: Math.PI / 2, range: 220));
        NetworkPorts.SensorPorts(camera).Select(port => port.Channel).ShouldBe(["left1", "centre", "right1", "hit"]);
    }

    [Fact]
    public void AVersion5Save_WithAnAccelerometer_LoadsWithoutCameraSettings()
    {
        WriteCreation(Version5Walker().ToJsonString());

        var sensor = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Sensors.ShouldHaveSingleItem();

        sensor.Rays.ShouldBeNull();
        sensor.Spread.ShouldBeNull();
        sensor.Range.ShouldBeNull();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    public void AVersion5Save_WithASensorThatIsNotAnObject_IsSkipped_AndTheOthersStillList(string sensor)
    {
        var bad = Version5Walker();
        bad["id"] = Guid.NewGuid().ToString();
        bad["creature"]!["sensors"] = JsonNode.Parse($"[{sensor}]");
        WriteCreation(bad.ToJsonString());
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();
    }

    [Fact]
    public void AVersion6Save_WithACameraOffItsSettings_IsSkipped()
    {
        var file = Version6Walker();
        file["creature"]!["sensors"]![0]!["kind"] = "camera";
        file["creature"]!["sensors"]![0]!["rays"] = 3;
        WriteCreation(file.ToJsonString());
        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Sensors[0].Rays.ShouldBe(3);

        file["creature"]!["sensors"]![0]!["rays"] = 4;
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // #1043: a creature saved before Touch sensors has none.
    [Fact]
    public void AVersion6Save_GetsNoTouchSensors_AndIsWrittenBackInTheCurrentVersion()
    {
        var path = WriteCreation(Version6Walker().ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.TouchSensors.ShouldBeEmpty();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written["creature"]!["touchSensors"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void ATouchSensor_SurvivesASaveAndALoad()
    {
        var walker = LoadFixture013("walker.creation.json");
        var foot = walker.Creature.Nodes[0].Id;
        var creature = new CreatureDef(
            walker.Creature.Nodes,
            walker.Creature.Beams,
            walker.Creature.Sensors,
            walker.Creature.Servos,
            walker.Creature.Pistons,
            walker.Creature.Springs,
            walker.Creature.Wheels,
            [new TouchSensorDef(walker.Creature.NextPartId, foot, "Toe")],
            walker.Creature.NextPartId + 1);
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.Save(walker.WithCreature(creature, training: null));

        repository.Get(walker.Id)!.Creature.TouchSensors.ShouldBe([new TouchSensorDef(walker.Creature.NextPartId, foot, "Toe")]);
    }

    [Fact]
    public void AVersion3Save_WhoseCreatureIsNotAnObject_IsSkipped_AndTheOthersStillList()
    {
        var bad = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        bad["id"] = Guid.NewGuid().ToString();
        bad["creature"] = new JsonArray();
        WriteCreation(WithVersion(bad.ToJsonString(), 3));
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    public void AVersion3Save_WithASpringThatIsNotAnObject_IsSkipped_AndTheOthersStillList(string spring)
    {
        var bad = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        bad["id"] = Guid.NewGuid().ToString();
        bad["creature"]!["springs"] = JsonNode.Parse($"[{spring}]");
        WriteCreation(WithVersion(bad.ToJsonString(), 3));
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("\"wide\"")]
    public void Fixture013_WithAPistonStrokeItCouldNotHaveSaved_IsSkipped(string oldStroke)
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["creature"]!["pistons"]![0]!["stroke"] = JsonNode.Parse(oldStroke);
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // #870: a Piston's length input went from −1…1 around its built length to 0…1 over its travel,
    // and #1129: its target length output from tanh −1…1 to sigmoid 0…1. A migrated network must ask
    // for the same lengths and strengths in the same pose, and still compile to resume training.
    [Fact]
    public void Fixture013_ItsTrainedNetworkDrivesItsPistonsAsBefore()
    {
        var original = Fixture013("trained-walker.creation.json");
        var oldNetwork = SaveJson.Deserialize<NeuralNetworkDef>(JsonNode.Parse(original)!["training"]!["brain"]!.ToJsonString(), "brain");
        WriteCreation(original);
        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();
        var ports = NetworkPorts.Of(creation.Creature);
        var oldPorts = ports with
        {
            Outputs = [.. ports.Outputs.Select(port => port.Signal == PortSignal.Length ? port with { Signal = PortSignal.Position } : port)],
        };
        var before = DirectNetwork.Build(oldNetwork, oldPorts);
        var after = DirectNetwork.Build(creation.Training!.Network, ports);
        var random = new Random(870);

        for (var pose = 0; pose < 20; pose++)
        {
            var oldInputs = ports.Inputs.Select(_ => (random.NextDouble() * 2) - 1).ToArray();
            var newInputs = ports.Inputs
                .Select((port, i) => port.Channel == NetworkPorts.PistonLengthChannel ? (oldInputs[i] + 1) / 2 : oldInputs[i])
                .ToArray();

            var oldOutputs = before.Forward(oldInputs)
                .Select((output, o) => ports.Outputs[o].Signal == PortSignal.Length ? (output + 1) / 2 : output)
                .ToArray();

            after.Forward(newInputs).ShouldBe(oldOutputs, tolerance: 1e-12);
        }

        ports.Outputs.ShouldContain(port => port.Signal == PortSignal.Length);
        Should.NotThrow(() => DirectNetwork.Compile(creation.Training.Network, ports));
    }

    // #1129: version 7 saved a Piston's target length as tanh; version 8 doubles its bias and every
    // weight into it, enabled or not, under sigmoid. Its other neurons and connections are untouched.
    [Fact]
    public void AVersion7Save_TurnsItsPistonTargetLengthsSigmoid_WithTheirBiasAndWeightsDoubled()
    {
        var current = CurrentTrainedWalker();
        var file = current.DeepClone().AsObject();
        file[_versionField] = 7;
        var network = NetworkUnderItsOldKey(file);
        network["connections"]!.AsArray()[0]!["enabled"] = false;
        current["training"]!["network"]!["connections"]!.AsArray()[0]!["enabled"] = false;
        var targets = new HashSet<int>();
        foreach (var neuron in network["neurons"]!.AsArray())
        {
            if (neuron!["channel"]!.GetValue<string>() == NetworkPorts.PistonPositionChannel)
            {
                neuron["activation"] = "tanh";
                neuron["bias"] = neuron["bias"]!.GetValue<double>() / 2;
                targets.Add(neuron["id"]!.GetValue<int>());
            }
        }

        foreach (var connection in network["connections"]!.AsArray().Where(connection => targets.Contains(connection!["to"]!.GetValue<int>())))
        {
            connection!["weight"] = connection["weight"]!.GetValue<double>() / 2;
        }

        targets.Count.ShouldBe(2);
        targets.ShouldContain(network["connections"]![0]!["to"]!.GetValue<int>());
        var path = WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        var written = JsonNode.Parse(File.ReadAllText(path))!;
        JsonNode.DeepEquals(written["training"]!["network"], current["training"]!["network"]).ShouldBeTrue();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
    }

    [Theory]
    [InlineData("training", "5")]
    [InlineData("creature", "[1]")]
    [InlineData("pistons", "[5]")]
    public void AVersion7Save_ItCouldNotHaveSaved_IsSkipped(string field, string value)
    {
        var file = CurrentTrainedWalker();
        file[_versionField] = 7;
        NetworkUnderItsOldKey(file);
        var parent = field == "pistons" ? file["creature"]!.AsObject() : file;
        parent[field] = JsonNode.Parse(value);
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion7Save_WithoutTraining_LoadsAsItWas()
    {
        var current = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        var file = current.DeepClone().AsObject();
        file[_versionField] = 7;
        var path = WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path)), current).ShouldBeTrue();
    }

    // #1144: version 8 saved each Wheel's grip; version 9 drops it and keeps everything else.
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void AVersion8Save_DropsEachWheelsGrip_AndKeepsTheRest(double grip)
    {
        var current = TrainedWalkerOnWheels();
        var file = current.DeepClone().AsObject();
        file[_versionField] = 8;
        NetworkUnderItsOldKey(file);
        foreach (var wheel in file["creature"]!["wheels"]!.AsArray())
        {
            wheel!["grip"] = grip;
        }

        var path = WriteCreation(file.ToJsonString());

        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        creation.Creature.Wheels.ShouldBe([new WheelDef(11, 1, "Back", radius: 70), new WheelDef(12, 3)]);
        creation.Training.ShouldNotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path)), current).ShouldBeTrue();
    }

    [Fact]
    public void ASaveInTheCurrentVersion_WithAWheelsGrip_IsSkipped()
    {
        var file = TrainedWalkerOnWheels();
        file["creature"]!["wheels"]![0]!["grip"] = 0.8;
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // #1142: a creature saved before Rubbers has none.
    [Fact]
    public void AVersion9Save_GetsNoRubbers_AndIsWrittenBackInTheCurrentVersion()
    {
        var path = WriteCreation(Version9Walker().ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Rubbers.ShouldBeEmpty();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written["creature"]!["rubbers"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion9Save_WithoutACreature_IsSkipped()
    {
        var file = Version9Walker();
        file.Remove("creature");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // The strict load (docs/SAVE_FORMAT.md): a current file must hold every array its version has.
    [Fact]
    public void ASaveInTheCurrentVersion_WithoutRubbers_IsSkipped()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file["creature"]!.AsObject().Remove("rubbers");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // #454: a creature saved before Velocity motors has none.
    [Fact]
    public void AVersion10Save_GetsNoVelocityMotors_AndIsWrittenBackInTheCurrentVersion()
    {
        var path = WriteCreation(Version10Walker().ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.VelocityMotors.ShouldBeEmpty();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written["creature"]!["velocityMotors"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion10Save_WithoutACreature_IsSkipped()
    {
        var file = Version10Walker();
        file.Remove("creature");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void ASaveInTheCurrentVersion_WithoutVelocityMotors_IsSkipped()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file["creature"]!.AsObject().Remove("velocityMotors");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void AVelocityMotor_SurvivesASaveAndALoad()
    {
        var walker = LoadFixture013("walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var id = builder.AddVelocityMotor(walker.Creature.Nodes[0].Id);
        builder.Rename(id, "Drive");
        builder.SetParameter(id, PartParameterId.MotorMaxSpeed, 3);
        var creature = builder.Build();
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.Save(walker.WithCreature(creature, training: null));

        repository.Get(walker.Id)!.Creature.VelocityMotors.ShouldBe(creature.VelocityMotors);
        creature.VelocityMotors.ShouldHaveSingleItem().MaxSpeed.ShouldBe(3);
    }

    // #1036: a creature saved before Steppers has none.
    [Fact]
    public void AVersion13Save_GetsNoSteppers_AndIsWrittenBackInTheCurrentVersion()
    {
        var path = WriteCreation(Version13Walker().ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Steppers.ShouldBeEmpty();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written["creature"]!["steppers"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion13Save_WithoutACreature_IsSkipped()
    {
        var file = Version13Walker();
        file.Remove("creature");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void ASaveInTheCurrentVersion_WithoutSteppers_IsSkipped()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file["creature"]!.AsObject().Remove("steppers");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void AStepper_WithItsSettings_AWheelSide_OrAMissingSide_SurvivesASaveAndALoad()
    {
        var walker = LoadFixture013("walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var joints = builder.Nodes.Where(node => builder.LinksAt(node.Id).Count >= 2).Select(node => node.Id).ToArray();
        var named = builder.AddStepper(joints[0]);
        builder.Rename(named, "Ratchet");
        builder.SetParameter(named, PartParameterId.ServoStrength, 300000);
        builder.SetParameter(named, PartParameterId.StepsPerTurn, 12);
        builder.SetParameter(named, PartParameterId.MaxFrequency, 1.5);
        var wheel = builder.AddWheel(joints[1]);
        builder.AddStepper(joints[1], builder.LinksAt(joints[1])[0].Id, wheel);
        builder.AddStepper(joints[2], fixedLinkId: null, builder.LinksAt(joints[2])[0].Id);
        var creature = builder.Build();
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.Save(walker.WithCreature(creature, training: null));

        repository.Get(walker.Id)!.Creature.Steppers.ShouldBe(creature.Steppers);
        creature.Steppers.Select(stepper => (stepper.Name, stepper.TargetLinkId == wheel, stepper.FixedLinkId is null))
            .ShouldBe([("Ratchet", false, false), (null, true, false), (null, false, true)]);
        creature.Steppers[0].Strength.ShouldBe(300000);
        creature.Steppers[0].StepsPerTurn.ShouldBe(12);
        creature.Steppers[0].MaxFrequency.ShouldBe(1.5);
    }

    // #1263: version 14 saved a Stepper's frequency output as sigmoid, its power output as tanh and its
    // running frequency as an input. Version 15 makes the power neuron its signed frequency output and
    // drops the old frequency neurons with every connection to or from them; nothing else changes.
    [Fact]
    public void AVersion14Save_MakesAStepperPowerItsFrequency_AndDropsItsOldFrequencyNeurons()
    {
        var (expected, stepperId) = TrainedWalkerWithAStepper();
        var network = expected["training"]!["network"]!.AsObject();
        var inputs = NeuronIds(network, "input");
        var outputs = NeuronIds(network, "output");
        var next = network["nextNeuronId"]!.GetValue<int>();
        var (power, oldInput, oldFrequency) = (next, next + 1, next + 2);
        network["neurons"]!.AsArray().Add(Neuron(power, "output", stepperId, NetworkPorts.StepperFrequencyChannel, -0.7, "tanh"));
        foreach (var input in inputs)
        {
            network["connections"]!.AsArray().Add(Connection(input, power, 0.1 * input));
        }

        network["nextNeuronId"] = next + 3;
        var file = expected.DeepClone().AsObject();
        file[_versionField] = 14;
        var old = NetworkUnderItsOldKey(file);
        old["neurons"]!.AsArray().Single(neuron => neuron!["id"]!.GetValue<int>() == power)!["channel"] = "power";
        old["neurons"]!.AsArray().Add(Neuron(oldInput, "input", stepperId, "frequency", 0, "identity"));
        old["neurons"]!.AsArray().Add(Neuron(oldFrequency, "output", stepperId, "frequency", 0.4, "sigmoid"));
        foreach (var output in (int[])[.. outputs, power, oldFrequency])
        {
            old["connections"]!.AsArray().Add(Connection(oldInput, output, 0.9));
        }

        foreach (var input in inputs)
        {
            old["connections"]!.AsArray().Add(Connection(input, oldFrequency, -0.3));
        }

        var path = WriteCreation(file.ToJsonString());

        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        var written = JsonNode.Parse(File.ReadAllText(path))!;
        JsonNode.DeepEquals(written["training"]!["network"], expected["training"]!["network"]).ShouldBeTrue();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        Should.NotThrow(() => DirectNetwork.Compile(creation.Training!.Network, NetworkPorts.Of(creation.Creature)));
    }

    // #1308: version 15 saved the network as training.brain; version 16 moves it, unchanged, to training.network.
    [Fact]
    public void AVersion15Save_MovesItsNetworkToItsNewKey_AndKeepsTheRestOfItsTraining()
    {
        var current = CurrentTrainedWalker();
        var file = current.DeepClone().AsObject();
        file[_versionField] = 15;
        NetworkUnderItsOldKey(file);
        var path = WriteCreation(file.ToJsonString());

        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        creation.Training!.Network.Connections.ShouldNotBeEmpty();
        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path)), current).ShouldBeTrue();
    }

    [Fact]
    public void AVersion15Save_WithBothItsOldAndNewNetworkKeys_IsSkipped()
    {
        var file = CurrentTrainedWalker();
        file[_versionField] = 15;
        file["training"]!["brain"] = file["training"]!["network"]!.DeepClone();
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion15Save_WithoutTraining_LoadsAsItWas()
    {
        var current = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        var file = current.DeepClone().AsObject();
        file[_versionField] = 15;
        var path = WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path)), current).ShouldBeTrue();
    }

    [Fact]
    public void AVersion14Save_WithoutSteppers_IsSkipped()
    {
        var (file, _) = TrainedWalkerWithAStepper();
        file[_versionField] = 14;
        NetworkUnderItsOldKey(file);
        file["creature"]!.AsObject().Remove("steppers");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // #1204: a creature saved before Angle sensors has none.
    [Fact]
    public void AVersion12Save_GetsNoAngleSensors_AndIsWrittenBackInTheCurrentVersion()
    {
        var path = WriteCreation(Version12Walker().ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.AngleSensors.ShouldBeEmpty();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written["creature"]!["angleSensors"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion12Save_WithoutACreature_IsSkipped()
    {
        var file = Version12Walker();
        file.Remove("creature");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void ASaveInTheCurrentVersion_WithoutAngleSensors_IsSkipped()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file["creature"]!.AsObject().Remove("angleSensors");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void AnAngleSensor_WithTwoLinks_AWheelSide_OrAMissingSide_SurvivesASaveAndALoad()
    {
        var walker = LoadFixture013("walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var joints = builder.Nodes.Where(node => builder.LinksAt(node.Id).Count >= 2).Select(node => node.Id).ToArray();
        var named = builder.AddAngleSensor(joints[0]);
        builder.Rename(named, "Knee");
        var wheel = builder.AddWheel(joints[1]);
        builder.AddAngleSensor(joints[1], builder.LinksAt(joints[1])[0].Id, wheel);
        builder.AddAngleSensor(joints[2], fixedLinkId: null, builder.LinksAt(joints[2])[0].Id);
        var creature = builder.Build();
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.Save(walker.WithCreature(creature, training: null));

        repository.Get(walker.Id)!.Creature.AngleSensors.ShouldBe(creature.AngleSensors);
        creature.AngleSensors.Select(sensor => (sensor.Name, sensor.TargetLinkId == wheel, sensor.FixedLinkId is null))
            .ShouldBe([("Knee", false, false), (null, true, false), (null, false, true)]);
    }

    // #1039: a creature saved before Brakes has none.
    [Fact]
    public void AVersion11Save_GetsNoBrakes_AndIsWrittenBackInTheCurrentVersion()
    {
        var path = WriteCreation(Version11Walker().ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Brakes.ShouldBeEmpty();

        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
        written["creature"]!["brakes"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public void AVersion11Save_WithoutACreature_IsSkipped()
    {
        var file = Version11Walker();
        file.Remove("creature");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void ASaveInTheCurrentVersion_WithoutBrakes_IsSkipped()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file["creature"]!.AsObject().Remove("brakes");
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    // A Locked Brake's infinite Strength is saved as "Infinity" (docs/SAVE_FORMAT.md).
    [Fact]
    public void ABrake_LockedOrWithAWheelSide_SurvivesASaveAndALoad()
    {
        var walker = LoadFixture013("walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var joints = builder.Nodes.Where(node => builder.LinksAt(node.Id).Count >= 2).Select(node => node.Id).ToArray();
        var locked = builder.AddBrake(joints[0]);
        builder.SetParameter(locked, PartParameterId.BrakeStrength, BrakeDef.LockedStrength);
        builder.Rename(locked, "Lock");
        var wheel = builder.AddWheel(joints[1]);
        builder.SetParameter(builder.AddBrake(joints[1], builder.LinksAt(joints[1])[0].Id, wheel), PartParameterId.BrakeStrength, 700000);
        var creature = builder.Build();
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.Save(walker.WithCreature(creature, training: null));

        repository.Get(walker.Id)!.Creature.Brakes.ShouldBe(creature.Brakes);
        creature.Brakes.Select(brake => (brake.Name, brake.Locked)).ShouldBe([("Lock", true), (null, false)]);
        File.ReadAllText(CreationPath(walker.Id)).ShouldContain("\"strength\": \"Infinity\"");
    }

    [Fact]
    public void ARubber_SurvivesASaveAndALoad()
    {
        var walker = LoadFixture013("walker.creation.json");
        var foot = walker.Creature.Nodes[0].Id;
        var creature = new CreatureDef(
            walker.Creature.Nodes,
            walker.Creature.Beams,
            walker.Creature.Sensors,
            walker.Creature.Servos,
            walker.Creature.Pistons,
            walker.Creature.Springs,
            walker.Creature.Wheels,
            walker.Creature.TouchSensors,
            [new RubberDef(walker.Creature.NextPartId, foot, "Sole")],
            walker.Creature.NextPartId + 1);
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        repository.Save(walker.WithCreature(creature, training: null));

        repository.Get(walker.Id)!.Creature.Rubbers.ShouldBe([new RubberDef(walker.Creature.NextPartId, foot, "Sole")]);
    }

    [Theory]
    [InlineData("wheels", "[5]")]
    [InlineData("wheels", "[null]")]
    [InlineData("wheels", "null")]
    [InlineData("creature", "[1]")]
    public void AVersion8Save_ItCouldNotHaveSaved_IsSkipped(string field, string value)
    {
        var file = TrainedWalkerOnWheels();
        file[_versionField] = 8;
        NetworkUnderItsOldKey(file);
        var parent = field == "wheels" ? file["creature"]!.AsObject() : file;
        parent[field] = JsonNode.Parse(value);
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("neurons", "\"many\"")]
    [InlineData("connections", "[{ \"from\": \"three\", \"to\": 7, \"weight\": 1, \"enabled\": true }]")]
    [InlineData("connections", "[{ \"from\": 3, \"to\": 7, \"weight\": \"heavy\", \"enabled\": true }]")]
    public void Fixture013_WithANetworkItCouldNotHaveSaved_IsSkipped(string field, string value)
    {
        var file = JsonNode.Parse(Fixture013("trained-walker.creation.json"))!.AsObject();
        file["training"]!["brain"]![field] = JsonNode.Parse(value);
        WriteCreation(file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();
    }

    [Fact]
    public void Fixture013_KeepsItsTraining()
    {
        WriteCreation(Fixture013("trained-walker.creation.json"));

        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        creation.Training.ShouldNotBeNull();
        creation.Training.Generation.ShouldBeGreaterThan(1);
        creation.Training.Network.Connections.ShouldNotBeEmpty();
    }

    [Fact]
    public void AVersion4Save_LoadsWithNoWheels_AndAVersion5SaveMustListThem()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = LoadFixture013("walker.creation.json");
        repository.Save(creation);
        var path = CreationPath(creation.Id);
        var file = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        file["creature"]!.AsObject().Remove("wheels");
        file[_versionField] = 4;
        File.WriteAllText(path, file.ToJsonString());

        new FileCreationRepository(new TestStorageLocation(_directory)).Get(creation.Id).ShouldNotBeNull().Creature.Wheels.ShouldBeEmpty();

        file[_versionField] = 5;
        File.WriteAllText(path, file.ToJsonString());
        new FileCreationRepository(new TestStorageLocation(_directory)).Get(creation.Id).ShouldBeNull();
    }

    [Fact]
    public void AWheel_RoundTripsWithItsRadius_ButNotItsWeight()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creature = new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(200, 0))],
            [new BeamDef(3, 1, 2)],
            [],
            [],
            [],
            [],
            [new WheelDef(4, 2, "Front", radius: 70)],
            nextPartId: 5);
        var creation = new CreationDef(Guid.NewGuid(), "Roller", creature);

        repository.Save(creation);

        var wheel = JsonNode.Parse(File.ReadAllText(CreationPath(creation.Id)))!["creature"]!["wheels"]!.AsArray().ShouldHaveSingleItem()!.AsObject();
        wheel.Select(field => field.Key).ShouldBe(["id", "nodeId", "name", "radius"]);
        repository.Get(creation.Id).ShouldBe(creation, _creationComparer);
        repository.Get(creation.Id)!.Creature.Wheels.ShouldBe(creature.Wheels);
    }

    [Fact]
    public void Save_WritesTheVersionFirst()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = LoadFixture013("walker.creation.json");

        repository.Save(creation);

        var written = JsonNode.Parse(File.ReadAllText(CreationPath(creation.Id)))!.AsObject();
        written.First().Key.ShouldBe(_versionField);
        written[_versionField]!.GetValue<int>().ShouldBe(FileCreationRepository.Format.CurrentVersion);
    }

    [Fact]
    public void Load_InTheCurrentVersion_LeavesTheFileAlone()
    {
        var repository = new FileCreationRepository(new TestStorageLocation(_directory));
        var creation = LoadFixture013("walker.creation.json");
        repository.Save(creation);
        var path = CreationPath(creation.Id);
        var written = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, written.AddHours(-1));

        repository.Get(creation.Id).ShouldNotBeNull();

        File.GetLastWriteTimeUtc(path).ShouldBe(written.AddHours(-1));
    }

    [Fact]
    public void Repository_WritesAMigratedFileBackInTheNewVersion()
    {
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            RenameField("title", "name")(file);
            AddMissingArrays(file);
        }]);
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["title"] = file["name"]!.DeepClone();
        file.Remove("name");
        var path = WriteCreation(file.ToJsonString());

        var creation = new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldHaveSingleItem();

        creation.Name.ShouldBe("Walker");
        var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        written[_versionField]!.GetValue<int>().ShouldBe(2);
        written.ContainsKey("title").ShouldBeFalse();
        written["name"]!.GetValue<string>().ShouldBe("Walker");
    }

    [Fact]
    public void Repository_SkipsAFileNewerThanTheApp_AndLeavesItUntouched()
    {
        var path = WriteCreation(WithVersion(Fixture013("walker.creation.json"), FileCreationRepository.Format.CurrentVersion + 1));
        var before = File.ReadAllBytes(path);

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();

        File.ReadAllBytes(path).ShouldBe(before);
    }

    [Fact]
    public void Repository_SkipsAFileWhoseMigrationFails_AndLeavesItUntouched()
    {
        var format = new VersionedSaveFile<CreationDef>([_ => throw new InvalidDataException("Cannot migrate.")]);
        var path = WriteCreation(Fixture013("walker.creation.json"));
        var before = File.ReadAllBytes(path);

        new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldBeEmpty();

        File.ReadAllBytes(path).ShouldBe(before);
    }

    [Fact]
    public void Repository_SkipsAFileThatFailsTheStrictLoad_AndLeavesItUntouched()
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["unknown"] = 1;
        var path = WriteCreation(file.ToJsonString());
        var before = File.ReadAllBytes(path);

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldBeEmpty();

        File.ReadAllBytes(path).ShouldBe(before);
    }

    [Fact]
    public void WriteBack_WhenASaveReplacedTheFileAfterItWasRead_KeepsTheSave()
    {
        var path = CreationPath(Guid.Parse(JsonNode.Parse(Fixture013("walker.creation.json"))!["id"]!.GetValue<string>()));
        const string saved = "saved in the meantime";
        // The migration runs between the read and the write-back, so it stands in for a Save that lands there.
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            File.WriteAllText(path, saved);
            AddMissingArrays(file);
        }]);
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldHaveSingleItem();

        File.ReadAllText(path).ShouldBe(saved);
    }

    [Fact]
    public void WriteBack_ThatFails_StillLoadsTheCreation()
    {
        var path = CreationPath(Guid.Parse(JsonNode.Parse(Fixture013("walker.creation.json"))!["id"]!.GetValue<string>()));
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            File.Delete(path);
            AddMissingArrays(file);
        }]);
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory), format).List().ShouldHaveSingleItem().Name.ShouldBe("Walker");
    }

    private static SaveMigration RenameField(string from, string to) => file =>
    {
        var value = file[from]!.DeepClone();
        file.Remove(from);
        file[to] = value;
    };

    // The arrays a 0.13.0 file lacks that loading now requires.
    private static void AddMissingArrays(JsonObject file)
    {
        file["creature"]!.AsObject()["servos"] = new JsonArray();
        file["creature"]!.AsObject()["wheels"] = new JsonArray();
        file["creature"]!.AsObject()["touchSensors"] = new JsonArray();
        file["creature"]!.AsObject()["rubbers"] = new JsonArray();
        file["creature"]!.AsObject()["velocityMotors"] = new JsonArray();
        file["creature"]!.AsObject()["brakes"] = new JsonArray();
        file["creature"]!.AsObject()["angleSensors"] = new JsonArray();
        file["creature"]!.AsObject()["steppers"] = new JsonArray();
    }

    private static CreationDef LoadFixture013(string name) =>
        FileCreationRepository.Format.Deserialize(Fixture013(name), name).Value;

    // The trained 0.13.0 walker as the current version writes it, with sigmoid Piston target lengths (#1129).
    private static JsonObject CurrentTrainedWalker() =>
        JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("trained-walker.creation.json")))!.AsObject();

    // The trained walker with two Wheels, one named and resized, as the current version writes it.
    private static JsonObject TrainedWalkerOnWheels()
    {
        var walker = LoadFixture013("trained-walker.creation.json");
        var creature = walker.Creature;
        var wheels = new CreatureDef(
            creature.Nodes,
            creature.Beams,
            creature.Sensors,
            creature.Servos,
            creature.Pistons,
            creature.Springs,
            [new WheelDef(11, 1, "Back", radius: 70), new WheelDef(12, 3)],
            creature.TouchSensors,
            nextPartId: 13);
        return JsonNode.Parse(FileCreationRepository.Format.Serialize(walker.WithCreature(wheels, walker.Training)))!.AsObject();
    }

    // The trained walker with a Stepper its network has no neurons for yet, as the current version writes it.
    private static (JsonObject File, int StepperId) TrainedWalkerWithAStepper()
    {
        var walker = LoadFixture013("trained-walker.creation.json");
        var builder = new CreatureBuilder(walker.Creature);
        var joint = builder.Nodes.First(node => builder.LinksAt(node.Id).Count >= 2).Id;
        var stepperId = builder.AddStepper(joint);
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(walker.WithCreature(builder.Build(), walker.Training)))!.AsObject();
        return (file, stepperId);
    }

    // Moves a current file's network back under the key versions before 16 saved it in (#1308).
    private static JsonObject NetworkUnderItsOldKey(JsonObject file)
    {
        var training = file["training"]!.AsObject();
        var network = training["network"]!.AsObject();
        training.Remove("network");
        training["brain"] = network;
        return network;
    }

    private static int[] NeuronIds(JsonObject network, string kind) =>
        [.. network["neurons"]!.AsArray().Where(neuron => neuron!["kind"]!.GetValue<string>() == kind).Select(neuron => neuron!["id"]!.GetValue<int>())];

    private static JsonObject Neuron(int id, string kind, int partId, string channel, double bias, string activation) => new()
    {
        ["id"] = id,
        ["kind"] = kind,
        ["partId"] = partId,
        ["channel"] = channel,
        ["layer"] = kind == "input" ? 0 : 1,
        ["bias"] = bias,
        ["activation"] = activation,
    };

    private static JsonObject Connection(int from, int to, double weight) => new()
    {
        ["from"] = from,
        ["to"] = to,
        ["weight"] = weight,
        ["enabled"] = true,
    };

    // The 0.13.0 walker as version 13 wrote it, before Steppers (#1036).
    private static JsonObject Version13Walker()
    {
        var file = JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
        file[_versionField] = 13;
        file["creature"]!.AsObject().Remove("steppers");
        return file;
    }

    // The 0.13.0 walker as version 12 wrote it, before Angle sensors (#1204).
    private static JsonObject Version12Walker()
    {
        var file = Version13Walker();
        file[_versionField] = 12;
        file["creature"]!.AsObject().Remove("angleSensors");
        return file;
    }

    // The 0.13.0 walker as version 11 wrote it, before Brakes (#1039).
    private static JsonObject Version11Walker()
    {
        var file = Version12Walker();
        file[_versionField] = 11;
        file["creature"]!.AsObject().Remove("brakes");
        return file;
    }

    // The 0.13.0 walker as version 10 wrote it, before Velocity motors (#454).
    private static JsonObject Version10Walker()
    {
        var file = Version11Walker();
        file[_versionField] = 10;
        file["creature"]!.AsObject().Remove("velocityMotors");
        return file;
    }

    // The 0.13.0 walker as version 9 wrote it, before Rubbers (#1142).
    private static JsonObject Version9Walker()
    {
        var file = Version10Walker();
        file[_versionField] = 9;
        file["creature"]!.AsObject().Remove("rubbers");
        return file;
    }

    // The 0.13.0 walker as version 6 wrote it, before Touch sensors (#1043).
    private static JsonObject Version6Walker()
    {
        var file = Version9Walker();
        file[_versionField] = 6;
        file["creature"]!.AsObject().Remove("touchSensors");
        return file;
    }

    // The Walker as version 5 wrote it: also its sensors without Camera settings.
    private static JsonObject Version5Walker()
    {
        var file = Version6Walker();
        file[_versionField] = 5;
        foreach (var sensor in file["creature"]!["sensors"]!.AsArray())
        {
            sensor!.AsObject().Remove("rays");
            sensor.AsObject().Remove("spread");
            sensor.AsObject().Remove("range");
        }

        return file;
    }

    private static string WithVersion(string json, int version)
    {
        var file = JsonNode.Parse(json)!.AsObject();
        file.Insert(0, _versionField, version);
        return file.ToJsonString();
    }

    private static string Fixture013(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Repositories", "SaveExamples", "0.13.0", name));

    private string WriteCreation(string json)
    {
        var id = Guid.Parse(JsonNode.Parse(json)!["id"]!.GetValue<string>());
        var path = CreationPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    private string CreationPath(Guid id) => Path.Combine(_directory, id.ToString("N"), FileCreationRepository.CreationFileName);
}

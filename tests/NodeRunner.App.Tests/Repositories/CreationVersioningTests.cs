using System.Globalization;
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

    // Loading the 0.13.0 files runs every migration but #1263's and #1308's, and writes them back
    // version first; a file in the current version is then left alone.
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
        AddMissingArrays(expected);
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
        var writtenAt = File.GetLastWriteTimeUtc(path).AddHours(-1);
        File.SetLastWriteTimeUtc(path, writtenAt);
        new FileCreationRepository(new TestStorageLocation(_directory)).Get(loaded.Id).ShouldBe(loaded, _creationComparer);
        File.GetLastWriteTimeUtc(path).ShouldBe(writtenAt);
    }

    // #870: ±s of the built length becomes 2s / (1 − s) of the shortest, from the middle; a Piston
    // can now at most double, so ±50% becomes 100%. A Piston without a stroke had ±30%.
    [Theory]
    [InlineData(0.1, 2.0 / 9)]
    [InlineData(0.5, 1)]
    public void Fixture013_ItsPistonsKeepTheirShortestAndLongestLengths_UpToDoubling(double oldStroke, double stroke)
    {
        var file = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        file["creature"]!["pistons"]![0]!["stroke"] = oldStroke;
        file["creature"]!["pistons"]![1]!.AsObject().Remove("stroke");
        WriteCreation(file.ToJsonString());

        var pistons = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Creature.Pistons;

        pistons.Select(piston => piston.Stroke).ShouldBe([stroke, 6.0 / 7], tolerance: 1e-12);
        pistons.ShouldAllBe(piston => piston.Start == 0.5);
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

    // #578: a Camera saved before it had settings keeps today's fan; an Accelerometer gets none.
    [Fact]
    public void AVersion5Save_GivesACameraTodaysFan_AndAnAccelerometerNoCameraSettings()
    {
        WriteCreation(Version5Walker().ToJsonString());
        var file = Version5Walker();
        file["id"] = Guid.NewGuid().ToString();
        file["creature"]!["sensors"]![0]!["kind"] = "camera";
        file["creature"]!["sensors"]![0]!["aim"] = 0.25;
        WriteCreation(file.ToJsonString());

        var sensors = new FileCreationRepository(new TestStorageLocation(_directory)).List().Select(creation => creation.Creature.Sensors.ShouldHaveSingleItem()).ToArray();

        var camera = sensors.Single(sensor => sensor.Kind == SensorKind.Camera);
        camera.ShouldBe(new SensorDef(8, 5, SensorKind.Camera, aim: 0.25, rays: 3, spread: Math.PI / 2, range: 220));
        NetworkPorts.SensorPorts(camera).Select(port => port.Channel).ShouldBe(["left1", "centre", "right1", "hit"]);
        var accelerometer = sensors.Single(sensor => sensor.Kind == SensorKind.Accelerometer);
        (accelerometer.Rays, accelerometer.Spread, accelerometer.Range).ShouldBe((null, null, null));
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
        creation.Training.ShouldNotBeNull().Generation.ShouldBeGreaterThan(1);
        var ports = NetworkPorts.Of(creation.Creature);
        var oldPorts = ports with
        {
            Outputs = [.. ports.Outputs.Select(port => port.Signal == PortSignal.Length ? port with { Signal = PortSignal.Position } : port)],
        };
        var before = DirectNetwork.Build(oldNetwork, oldPorts);
        var after = DirectNetwork.Build(creation.Training.Network, ports);
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

    // The network migrations (#1129, #1263, #1308) pass over a save without training.
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
    [Fact]
    public void AVersion8Save_DropsEachWheelsGrip_AndKeepsTheRest()
    {
        var current = TrainedWalkerOnWheels();
        var file = current.DeepClone().AsObject();
        file[_versionField] = 8;
        NetworkUnderItsOldKey(file);
        foreach (var wheel in file["creature"]!["wheels"]!.AsArray())
        {
            wheel!["grip"] = 0.8;
        }

        var path = WriteCreation(file.ToJsonString());

        var creation = new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem();

        creation.Creature.Wheels.ShouldBe([new WheelDef(11, 1, "Back", radius: 70), new WheelDef(12, 3)]);
        creation.Training.ShouldNotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(path)), current).ShouldBeTrue();
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

    // A save its version could not have written, one newer than the app or one that fails the strict
    // load is skipped and left as it is, and the others still list. One row for each way a migration
    // or the strict load refuses one (docs/SAVE_FORMAT.md → "Versions and migration").
    [Theory]
    [InlineData("v3", "creature=[]")]
    [InlineData("v3", "creature.springs=[5]")]
    [InlineData("013", "creature.pistons.0.stroke=0")]
    [InlineData("013", "creature.pistons.0.stroke=1")]
    [InlineData("013", "creature.pistons.0.stroke=\"wide\"")]
    [InlineData("013 trained", "training.brain.neurons=\"many\"")]
    [InlineData("013 trained", "training.brain.connections=[{ \"from\": \"three\", \"to\": 7, \"weight\": 1, \"enabled\": true }]")]
    [InlineData("013 trained", "training.brain.connections=[{ \"from\": 3, \"to\": 7, \"weight\": \"heavy\", \"enabled\": true }]")]
    [InlineData("v5", "creature.sensors=[5]")]
    [InlineData("v5", "-creature.wheels")]
    [InlineData("v6", "creature.sensors.0.kind=\"camera\";creature.sensors.0.rays=4")]
    [InlineData("v7", "training=5")]
    [InlineData("v7", "creature=[1]")]
    [InlineData("v7", "creature.pistons=[5]")]
    [InlineData("v7", "training.brain.neurons.0.kind=5;training.brain.neurons.1.kind={}")]
    [InlineData("v8", "creature.wheels=[5]")]
    [InlineData("v8", "creature.wheels=null")]
    [InlineData("v8", "creature=[1]")]
    [InlineData("v9", "-creature")]
    [InlineData("v10", "-creature")]
    [InlineData("v11", "-creature")]
    [InlineData("v12", "-creature")]
    [InlineData("v13", "-creature")]
    [InlineData("v14", "-creature.steppers")]
    [InlineData("v15", "training.network=@training.brain")]
    [InlineData("current", "-creature.steppers")]
    [InlineData("current", "unknown=1")]
    [InlineData("newer", "")]
    public void ASaveItsVersionCouldNotHaveWritten_IsSkipped_AndLeftAsItIs(string save, string changes)
    {
        var file = Save(save);
        file["id"] = Guid.NewGuid().ToString();
        foreach (var change in changes.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            Apply(file, change);
        }

        var path = WriteCreation(file.ToJsonString());
        var before = File.ReadAllBytes(path);
        WriteCreation(Fixture013("walker.creation.json"));

        new FileCreationRepository(new TestStorageLocation(_directory)).List().ShouldHaveSingleItem().Name.ShouldBe("Walker");

        File.ReadAllBytes(path).ShouldBe(before);
    }

    // A migrated file is written back in the new version, unless a Save replaced it after it was
    // read; a write-back that fails still loads the creation, and a migration that fails skips it.
    [Theory]
    [InlineData("migrated")]
    [InlineData("saved in the meantime")]
    [InlineData("write-back fails")]
    [InlineData("migration fails")]
    public void WriteBack_OfAMigratedFile(string outcome)
    {
        var walker = JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
        var path = CreationPath(Guid.Parse(walker["id"]!.GetValue<string>()));
        const string saved = "saved in the meantime";
        // The migration runs between the read and the write-back, so it stands in for what lands there.
        var format = new VersionedSaveFile<CreationDef>([file =>
        {
            switch (outcome)
            {
                case "migrated":
                    RenameField("title", "name")(file);
                    break;
                case "saved in the meantime":
                    File.WriteAllText(path, saved);
                    break;
                case "write-back fails":
                    File.Delete(path);
                    break;
                default:
                    throw new InvalidDataException("Cannot migrate.");
            }

            AddMissingArrays(file);
        }]);
        if (outcome == "migrated")
        {
            walker["title"] = walker["name"]!.DeepClone();
            walker.Remove("name");
        }

        WriteCreation(walker.ToJsonString());
        var before = File.ReadAllText(path);

        var listed = new FileCreationRepository(new TestStorageLocation(_directory), format).List();

        switch (outcome)
        {
            case "migrated":
                listed.ShouldHaveSingleItem().Name.ShouldBe("Walker");
                var written = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                written[_versionField]!.GetValue<int>().ShouldBe(2);
                written.ContainsKey("title").ShouldBeFalse();
                written["name"]!.GetValue<string>().ShouldBe("Walker");
                break;
            case "saved in the meantime":
                listed.ShouldHaveSingleItem();
                File.ReadAllText(path).ShouldBe(saved);
                break;
            case "write-back fails":
                listed.ShouldHaveSingleItem().Name.ShouldBe("Walker");
                break;
            default:
                listed.ShouldBeEmpty();
                File.ReadAllText(path).ShouldBe(before);
                break;
        }
    }

    // A save as version key wrote it; "013" is the 0.13.0 walker's file, from before versions.
    private static JsonObject Save(string key)
    {
        switch (key)
        {
            case "013":
                return JsonNode.Parse(Fixture013("walker.creation.json"))!.AsObject();
            case "013 trained":
                return JsonNode.Parse(Fixture013("trained-walker.creation.json"))!.AsObject();
            case "v3":
                return JsonNode.Parse(WithVersion(Fixture013("walker.creation.json"), 3))!.AsObject();
            case "v5":
                return Version5Walker();
            case "v6":
                return Version6Walker();
            case "v7" or "v15":
                var trained = CurrentTrainedWalker();
                trained[_versionField] = key == "v7" ? 7 : 15;
                NetworkUnderItsOldKey(trained);
                return trained;
            case "v8":
                var onWheels = TrainedWalkerOnWheels();
                onWheels[_versionField] = 8;
                NetworkUnderItsOldKey(onWheels);
                return onWheels;
            case "v14":
                var (withAStepper, _) = TrainedWalkerWithAStepper();
                withAStepper[_versionField] = 14;
                NetworkUnderItsOldKey(withAStepper);
                return withAStepper;
            case "current":
                return JsonNode.Parse(FileCreationRepository.Format.Serialize(LoadFixture013("walker.creation.json")))!.AsObject();
            case "newer":
                return JsonNode.Parse(WithVersion(Fixture013("walker.creation.json"), FileCreationRepository.Format.CurrentVersion + 1))!.AsObject();
            default:
                var version = int.Parse(key[1..], CultureInfo.InvariantCulture);
                var file = Version13Walker();
                foreach (var (since, array) in new[] { (12, "angleSensors"), (11, "brakes"), (10, "velocityMotors"), (9, "rubbers") })
                {
                    if (version <= since)
                    {
                        file["creature"]!.AsObject().Remove(array);
                    }
                }

                file[_versionField] = version;
                return file;
        }
    }

    // change is "path=json", setting it, or "-path", removing it; a value "@path" copies another field.
    private static void Apply(JsonObject file, string change)
    {
        var remove = change.StartsWith('-');
        var (path, value) = remove ? (change[1..], null) : (change[..change.IndexOf('=')], change[(change.IndexOf('=') + 1)..]);
        var keys = path.Split('.');
        JsonNode parent = file;
        foreach (var key in keys[..^1])
        {
            parent = int.TryParse(key, out var index) ? parent[index]! : parent[key]!;
        }

        if (remove)
        {
            parent.AsObject().Remove(keys[^1]);
            return;
        }

        var node = value!.StartsWith('@')
            ? value[1..].Split('.').Aggregate((JsonNode)file, (node, key) => node[key]!).DeepClone()
            : JsonNode.Parse(value);
        if (int.TryParse(keys[^1], out var at))
        {
            parent[at] = node;
        }
        else
        {
            parent[keys[^1]] = node;
        }
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

    // The 0.13.0 walker as version 6 wrote it, before Touch sensors (#1043).
    private static JsonObject Version6Walker()
    {
        var file = Version13Walker();
        foreach (var array in (string[])["angleSensors", "brakes", "velocityMotors", "rubbers"])
        {
            file["creature"]!.AsObject().Remove(array);
        }

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

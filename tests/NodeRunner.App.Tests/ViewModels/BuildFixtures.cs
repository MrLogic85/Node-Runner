using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

internal static class BuildFixtures
{
    public const string LockedText = "Locked: would change the network";

    public static CreatureElementSelection Joint(int id) => new(CreatureElementKind.Node, id);

    public static CreatureElementSelection Beam(int id) => new(CreatureElementKind.Beam, id);

    public static CreatureElementSelection Element(BuildPart part, int id) => new(Enum.Parse<CreatureElementKind>(part.ToString()), id);

    public static PartSet Set(params CreatureElementSelection[] parts)
    {
        HashSet<int> Ids(CreatureElementKind kind) => [.. parts.Where(part => part.Kind == kind).Select(part => part.Id)];
        return new PartSet(
            Ids(CreatureElementKind.Node), Ids(CreatureElementKind.Beam), Ids(CreatureElementKind.Sensor), Ids(CreatureElementKind.Servo),
            Ids(CreatureElementKind.Piston), Ids(CreatureElementKind.Spring), Ids(CreatureElementKind.Wheel), Ids(CreatureElementKind.TouchSensor),
            Ids(CreatureElementKind.Rubber), Ids(CreatureElementKind.VelocityMotor), Ids(CreatureElementKind.Brake), Ids(CreatureElementKind.AngleSensor),
            Ids(CreatureElementKind.Stepper));
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (200,0); beam 4 joins 1–2 and beam 5 joins 2–3; then <paramref name="parts"/>.</summary>
    public static CreatureDef TwoBeamsDef(params IPartDef[] parts) => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)),
            new BeamDef(4, 1, 2), new BeamDef(5, 2, 3), .. parts,
        ],
        nextPartId: parts.Length == 0 ? 6 : Math.Max(6, parts.Max(part => part.Id) + 1));

    public static BuildViewModel TwoBeams(params IPartDef[] parts) => Loaded(TwoBeamsDef(parts));

    public static BuildViewModel Loaded(CreatureDef creature, bool locked = false)
    {
        var build = new BuildViewModel();
        build.Load(creature, locked);
        return build;
    }

    /// <summary>Joints 1 (0,0), 2 (100,0) and 3 (0,100) placed one by one, so each is an undo step.</summary>
    public static (BuildViewModel Build, BuildGestures Gestures) ThreeLooseJoints(BuildTool tool)
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.PlaceNode(new Vector2D(0, 100));
        build.ActiveTool = tool;
        return (build, new BuildGestures(build));
    }

    /// <summary>Joints at (0,0), (0,100) and (100,100); a Camera on beam 1–2 and an Accelerometer on beam 2–3.</summary>
    public static (BuildViewModel Build, int Camera, int Accelerometer) DownwardBeamWithCameraAndAccelerometer()
    {
        var builder = new CreatureBuilder();
        builder.AddNode(new Vector2D(0, 0));
        builder.AddNode(new Vector2D(0, 100));
        builder.AddNode(new Vector2D(100, 100));
        builder.AddSensor(builder.AddBeam(1, 2), SensorKind.Camera, out var camera, out _).ShouldBeTrue();
        builder.AddSensor(builder.AddBeam(2, 3), SensorKind.Accelerometer, out var accelerometer, out _).ShouldBeTrue();
        return (new BuildViewModel(builder), camera, accelerometer);
    }

    /// <summary>A selection of exactly one part, as a tap on it with nothing selected leaves.</summary>
    public static void SelectOnly(this BuildViewModel build, CreatureElementKind kind, int id) =>
        build.ReplaceSelection(PartSet.Of(new CreatureElementSelection(kind, id)));

    public static void Tap(this BuildGestures gestures, Vector2D position)
    {
        gestures.Press(position);
        gestures.Release(position);
    }

    public static void DragAcross(this BuildGestures gestures, Vector2D from, Vector2D to)
    {
        gestures.Press(from);
        gestures.Drag(new Vector2D((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        gestures.Drag(to);
        gestures.Release(to);
    }

    public static Func<int> CountAnatomyChanges(this BuildViewModel build)
    {
        var changes = 0;
        build.AnatomyChanged += (_, _) => changes++;
        return () => changes;
    }
}

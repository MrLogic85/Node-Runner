using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class CreatureInspectorViewModelTests
{
    [Fact]
    public void Constructor_WithoutSelection_ShowsTapHint()
    {
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), new SelectionViewModel());

        inspector.Title.ShouldBe("Creature inspector");
        inspector.Role.ShouldBe("Tap a node, sensor or beam to inspect it.");
        inspector.Values.ShouldBe("The creature is built from nodes and beams; sensors sit on beams.");
    }

    [Fact]
    public void Selection_WithNode_ShowsPhysicalAttachmentPoint()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Node, 1));

        inspector.Title.ShouldBe("Node 1");
        inspector.Role.ShouldBe("A physical attachment point. Beams meet here and can rotate relative to each other.");
        inspector.Values.ShouldBe("Position: (0, 0)\nRadius: 10");
    }

    [Fact]
    public void Selection_WithBeam_ShowsRigidConnection()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Beam, 101));

        inspector.Title.ShouldBe("Beam 1");
        inspector.Role.ShouldBe("A rigid, fixed-length connection. It never stretches or compresses.");
        inspector.Values.ShouldBe("Connects: Node 1 to Node 2\nLength: 20");
    }

    [Fact]
    public void Selection_WithSensor_ShowsItsKindAndBeam()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Sensor, 201));

        inspector.Title.ShouldBe("Accelerometer");
        inspector.Values.ShouldBe("On: Beam 1");
    }

    private static CreatureDef CreateCreature()
    {
        return new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0), 10), new NodeDef(2, new Vector2D(20, 0), 10)],
            [new BeamDef(101, 1, 2)],
            [new SensorDef(201, 101, SensorKind.Accelerometer)]);
    }
}

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
        inspector.Role.ShouldBe("Tap a node, beam, or core to inspect it.");
        inspector.Values.ShouldBe("The creature is built from nodes, beams, and cores.");
    }

    [Fact]
    public void Selection_WithNode_ShowsPhysicalAttachmentPoint()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Node, 0));

        inspector.Title.ShouldBe("Node 1");
        inspector.Role.ShouldBe("A physical attachment point. Beams meet here and can rotate relative to each other.");
        inspector.Values.ShouldBe("Position: (0, 0)\nRadius: 10");
    }

    [Fact]
    public void Selection_WithBeam_ShowsRigidConnection()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Beam, 0));

        inspector.Title.ShouldBe("Beam 1");
        inspector.Role.ShouldBe("A rigid, fixed-length connection. It never stretches or compresses.");
        inspector.Values.ShouldBe("Connects: Node 1 to Node 2\nLength: 20");
    }

    [Fact]
    public void Selection_WithCore_ShowsSensorPackage()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Core, 0));

        inspector.Title.ShouldBe("Core 1");
        inspector.Role.ShouldBe("A sensor package. Not the brain itself — it feeds sensor readings (rays, pitch, elevation, speed) to the model.");
        inspector.Values.ShouldBe("Mounted on: Node 1");
    }

    [Fact]
    public void Clear_AfterSelection_RestoresTapHint()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);
        selection.Select(new CreatureElementSelection(CreatureElementKind.Core, 0));

        selection.Clear();

        inspector.Title.ShouldBe("Creature inspector");
        inspector.Role.ShouldBe("Tap a node, beam, or core to inspect it.");
        inspector.Values.ShouldBe("The creature is built from nodes, beams, and cores.");
    }

    private static CreatureDef CreateCreature()
    {
        return new CreatureDef(
            [new NodeDef(new Vector2D(0, 0), 10), new NodeDef(new Vector2D(20, 0), 10)],
            [new BeamDef(0, 1)],
            [new CoreDef(0)]);
    }
}

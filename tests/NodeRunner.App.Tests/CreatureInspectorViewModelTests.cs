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
        inspector.Role.ShouldBe("Tap a joint, bone, or muscle to inspect it.");
        inspector.Values.ShouldBe("The creature is built from joints, bones, and muscles.");
    }

    [Fact]
    public void Selection_WithJoint_ShowsPhysicalNodeValues()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Joint, 0));

        inspector.Title.ShouldBe("Joint 1");
        inspector.Role.ShouldBe("A moving physical node that other parts attach to.");
        inspector.Values.ShouldBe("Position: (0, 0)\nRadius: 10");
    }

    [Fact]
    public void Selection_WithBone_ShowsPassiveConnection()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Bone, 0));

        inspector.Title.ShouldBe("Bone 1");
        inspector.Role.ShouldBe("A passive structural connection that helps the creature keep its shape.");
        inspector.Values.ShouldBe("Connects: Joint 1 to Joint 2\nBrain output: none");
    }

    [Fact]
    public void Selection_WithMuscle_ShowsActuatorAndBrainOutput()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);

        selection.Select(new CreatureElementSelection(CreatureElementKind.Muscle, 0));

        inspector.Title.ShouldBe("Muscle 1");
        inspector.Role.ShouldBe("An active actuator. The brain changes it to move the body.");
        inspector.Values.ShouldBe("Connects: Joint 1 to Joint 2\nBrain output: 1\nRest length: 20\nMax force: 100");
    }

    [Fact]
    public void Clear_AfterSelection_RestoresTapHint()
    {
        var selection = new SelectionViewModel();
        using var inspector = new CreatureInspectorViewModel(CreateCreature(), selection);
        selection.Select(new CreatureElementSelection(CreatureElementKind.Muscle, 0));

        selection.Clear();

        inspector.Title.ShouldBe("Creature inspector");
        inspector.Role.ShouldBe("Tap a joint, bone, or muscle to inspect it.");
        inspector.Values.ShouldBe("The creature is built from joints, bones, and muscles.");
    }

    private static CreatureDef CreateCreature()
    {
        return new CreatureDef(
            [new JointDef(new Vector2D(0, 0), 10), new JointDef(new Vector2D(20, 0), 10)],
            [new BoneDef(0, 1)],
            [new MuscleDef(0, 1, 20, 100)]);
    }
}

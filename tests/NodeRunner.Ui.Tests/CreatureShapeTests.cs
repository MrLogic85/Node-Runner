using NodeRunner.Domain;
using NodeRunner.Domain.Tests;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Tests;

/// <summary>A <see cref="CreatureShape"/> takes one part list, in any kind order, and gives each kind's parts in the order it was given them.</summary>
public sealed class CreatureShapeTests
{
    [Fact]
    public void Each_kinds_list_keeps_the_order_its_parts_are_given_in()
    {
        var creature = EveryPartKind.Creature;

        var shape = new CreatureShape(creature.Parts.Reverse());

        shape.PartById.Count.ShouldBe(creature.PartCount);
        shape.PartById[21].ShouldBeOfType<StepperDef>();
        shape.Nodes.ShouldBe(creature.Nodes.Reverse());
        shape.Beams.ShouldBe(creature.Beams.Reverse());
        shape.Sensors.ShouldBe(creature.Sensors.Reverse());
        shape.HingeParts.Select(part => part.Id).ShouldBe([9, 15, 21, 16, 17]);
    }
}

namespace NodeRunner.Domain.Tests;

public sealed class CreatureElementSelectionTests
{
    [Fact]
    public void Constructor_WithNegativeIndex_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CreatureElementSelection(CreatureElementKind.Beam, -1));
    }

    [Fact]
    public void Selection_Of_APart_UsesItsElementKind()
    {
        EveryPartKind.Creature.Parts.Select(CreatureElementSelection.Of).Select(selection => (selection.Kind, selection.Id)).ShouldBe(
        [
            (CreatureElementKind.Node, 1), (CreatureElementKind.Node, 2), (CreatureElementKind.Node, 3), (CreatureElementKind.Node, 4), (CreatureElementKind.Node, 18),
            (CreatureElementKind.Beam, 5), (CreatureElementKind.Beam, 6), (CreatureElementKind.Beam, 19), (CreatureElementKind.Beam, 20),
            (CreatureElementKind.Sensor, 7), (CreatureElementKind.Sensor, 8),
            (CreatureElementKind.Servo, 9), (CreatureElementKind.Piston, 10), (CreatureElementKind.Spring, 11),
            (CreatureElementKind.Wheel, 12), (CreatureElementKind.TouchSensor, 13), (CreatureElementKind.Rubber, 14),
            (CreatureElementKind.VelocityMotor, 15), (CreatureElementKind.Brake, 16), (CreatureElementKind.AngleSensor, 17),
            (CreatureElementKind.Stepper, 21),
        ]);
    }
}

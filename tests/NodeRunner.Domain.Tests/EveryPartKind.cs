namespace NodeRunner.Domain.Tests;

/// <summary>
/// A creature with at least one part of every <see cref="PartKind"/>, ids 1–22. NodeRunner.Ui.Tests links
/// this file too, so both test projects cover the same kinds.
/// </summary>
internal static class EveryPartKind
{
    public static readonly CreatureDef Creature = new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(0, 100)),
            new NodeDef(4, new Vector2D(100, 100)), new NodeDef(18, new Vector2D(200, 100)),
            new BeamDef(5, 1, 2), new BeamDef(6, 1, 3), new BeamDef(19, 4, 18), new BeamDef(20, 2, 18),
            new SensorDef(7, 5, SensorKind.Accelerometer), new SensorDef(8, 6, SensorKind.Camera, rays: 3),
            new ServoDef(9, 1, 5, 6), new PistonDef(10, 2, 4), new SpringDef(11, 3, 4), new WheelDef(12, 1),
            new TouchSensorDef(13, 1), new RubberDef(14, 1), new VelocityMotorDef(15, 2), new BrakeDef(16, 3),
            new AngleSensorDef(17, 4), new StepperDef(21, 18, 19, 20),
            new MemoryCellDef(22, 19),
        ],
        nextPartId: 23);

    /// <summary>Whether <see cref="Creature"/> still holds every kind, so a test over it covers a new one.</summary>
    public static bool HoldsEveryKind => Creature.Parts.Select(part => part.PartKind).Distinct().Count() == Enum.GetValues<PartKind>().Length;
}

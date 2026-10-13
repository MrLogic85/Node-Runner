namespace NodeRunner.Domain.Tests;

/// <summary>A creature with at least one part of every <see cref="PartKind"/>, ids 1–17.</summary>
internal static class EveryPartKind
{
    public static readonly CreatureDef Creature = new(
        nodes: [new(1, new Vector2D(0, 0)), new(2, new Vector2D(100, 0)), new(3, new Vector2D(0, 100)), new(4, new Vector2D(100, 100))],
        beams: [new(5, 1, 2), new(6, 1, 3)],
        sensors: [new(7, 5, SensorKind.Accelerometer), new(8, 6, SensorKind.Camera, rays: 3)],
        servos: [new(9, 1, 5, 6)],
        pistons: [new(10, 2, 4)],
        springs: [new(11, 3, 4)],
        wheels: [new(12, 1)],
        touchSensors: [new(13, 1)],
        rubbers: [new(14, 1)],
        velocityMotors: [new(15, 2)],
        brakes: [new(16, 3)],
        angleSensors: [new(17, 4)],
        nextPartId: 18);
}

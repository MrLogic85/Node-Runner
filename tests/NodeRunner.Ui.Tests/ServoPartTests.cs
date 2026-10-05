using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class ServoPartTests
{
    [Theory]
    [InlineData(52, 24, false, 8)]
    [InlineData(52, 44, false, 0)]
    [InlineData(70, 0, true, 0)]
    [InlineData(20, 0, false, 8)]
    [InlineData(9, 0, false, 0)]
    public void HousingReachFor_LeavesRoomForSensorsAndPistonCylinders(
        float freeLength,
        float sensorLength,
        bool pistonCylinderEnd,
        float expected)
    {
        ServoPart.HousingReachFor(freeLength, sensorLength, pistonCylinderEnd).ShouldBe(expected);
    }
}

using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class MetresTests
{
    [Theory]
    [InlineData(250, 2.5)]
    [InlineData(0, 0.0)]
    [InlineData(-125, -1.3)]
    [InlineData(1309.7, 13.1)]
    public void Number_ConvertsWorldUnitsToMetresToOneDecimal(double worldUnits, double expected)
    {
        Metres.Number(worldUnits).ShouldBe(new FixedNumber(expected, 1));
    }

    [Fact]
    public void WithUnitAndSpeedWithUnit_AddTheUnit()
    {
        Metres.WithUnit(250).ShouldBe(UiText.Format("{0} m", new FixedNumber(2.5, 1)));
        Metres.SpeedWithUnit(310).ShouldBe(UiText.Format("{0} m/s", new FixedNumber(3.1, 1)));
    }
}

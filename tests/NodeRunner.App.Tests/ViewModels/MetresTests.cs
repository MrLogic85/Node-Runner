using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class MetresTests
{
    [Theory]
    [InlineData(250, "2.5")]
    [InlineData(0, "0.0")]
    [InlineData(-125, "-1.3")]
    public void Format_ConvertsWorldUnitsToMetres(double worldUnits, string expected)
    {
        Metres.Format(worldUnits).ShouldBe(expected);
    }

    [Fact]
    public void FormatWithUnit_AddsTheUnit()
    {
        Metres.FormatWithUnit(250).ShouldBe("2.5 m");
    }
}

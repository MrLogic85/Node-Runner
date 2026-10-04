using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class FixedNumberTests
{
    [Theory]
    [InlineData(13.097, 1, 13.1)]
    [InlineData(0.25, 1, 0.3)]
    [InlineData(-0.25, 1, -0.3)]
    [InlineData(249.5, 0, 250)]
    [InlineData(2, 3, 2)]
    public void KeepsTheValueRoundedAsShown(double value, int decimals, double expected)
    {
        var number = new FixedNumber(value, decimals);

        number.Value.ShouldBe(expected);
        number.Decimals.ShouldBe(decimals);
    }

    [Fact]
    public void NumbersThatReadTheSame_AreEqual() =>
        new FixedNumber(4.04, 1).ShouldBe(new FixedNumber(3.96, 1));

    [Fact]
    public void ANegativeThatRoundsToZero_IsZero() =>
        double.IsNegative(new FixedNumber(-0.01, 1).Value).ShouldBeFalse();

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, -1)]
    [InlineData(1, FixedNumber.MaxDecimals + 1)]
    public void RejectsAValueOrDecimalsItCannotShow(double value, int decimals) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new FixedNumber(value, decimals));
}

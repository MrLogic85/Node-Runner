using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class FixedNumberTests
{
    // Numbers that read the same are equal, and a negative that rounds to zero is zero.
    [Theory]
    [InlineData(13.097, 1, 13.1)]
    [InlineData(-0.25, 1, -0.3)]
    [InlineData(249.5, 0, 250)]
    [InlineData(2, 3, 2)]
    [InlineData(3.96, 1, 4.04)]
    [InlineData(-0.01, 1, 0)]
    public void KeepsTheValueRoundedAsShown(double value, int decimals, double readsAs)
    {
        var number = new FixedNumber(value, decimals);

        number.ShouldBe(new FixedNumber(readsAs, decimals));
        number.Value.ShouldBe(Math.Round(readsAs, decimals, MidpointRounding.AwayFromZero));
        number.Decimals.ShouldBe(decimals);
        double.IsNegative(number.Value).ShouldBe(number.Value < 0);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, -1)]
    [InlineData(1, FixedNumber.MaxDecimals + 1)]
    public void RejectsAValueOrDecimalsItCannotShow(double value, int decimals) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new FixedNumber(value, decimals));
}

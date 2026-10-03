using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartParametersTests
{
    [Fact]
    public void ANewPiston_ShowsTheDefaultsInNewtonsPercentAndMetresPerSecond()
    {
        var piston = new PistonDef(3, 1, 2);

        PartParameters.SliderOver(PartParameterId.Strength, [piston.Strength])
            .ShouldBe(new ParameterSlider(PartParameterId.Strength, "Max strength", "150 N", Position(PartParameters.Strength, 150), Position(PartParameters.Strength, 150)));
        PartParameters.SliderOver(PartParameterId.Stroke, [piston.Stroke]).Readout.ShouldBe("±30%");
        PartParameters.SliderOver(PartParameterId.MaxSpeed, [piston.MaxSpeed]).Readout.ShouldBe("2.0 m/s");
    }

    [Fact]
    public void SliderPositions_GiveWholeStepsInWorldUnits()
    {
        PartParameters.ValueAt(PartParameterId.Strength, 0).ShouldBe(2000);
        PartParameters.ValueAt(PartParameterId.Strength, 1).ShouldBe(40000);
        PartParameters.ValueAt(PartParameterId.Stroke, 0.51).ShouldBe(0.3);
        PartParameters.ValueAt(PartParameterId.MaxSpeed, 0.5).ShouldBe(230, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(PartParameterId.Strength, 15000)]
    [InlineData(PartParameterId.Stroke, 0.45)]
    [InlineData(PartParameterId.MaxSpeed, 120)]
    public void ASharedValue_RoundTripsThroughItsSlider(PartParameterId id, double value)
    {
        var slider = PartParameters.SliderOver(id, [value, value]);

        slider.ValuesDiffer.ShouldBeFalse();
        PartParameters.ValueAt(id, slider.High).ShouldBe(value, tolerance: 1e-9);
    }

    [Fact]
    public void Aim_IsSetOnTheCanvas_OnePartAtATime()
    {
        PartParameters.Aim.InPanel.ShouldBeFalse();
        PartParameters.Aim.MultiEditable.ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => PartParameters.SliderOver(PartParameterId.Aim, [0.0]));
    }

    [Fact]
    public void EveryId_HasItsParameter()
    {
        Enum.GetValues<PartParameterId>().ShouldAllBe(id => PartParameters.Of(id).Id == id);
    }

    private static double Position(PartParameter parameter, double shown) => parameter.Slider!.Range.Position(shown);
}

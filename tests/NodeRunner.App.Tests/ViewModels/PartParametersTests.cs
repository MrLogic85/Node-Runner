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
            .ShouldBe(new ParameterSlider(PartParameterId.Strength, UiText.Plain("Max strength"), UiText.Format("{0} N", new FixedNumber(150, 0)), Position(PartParameters.Strength, 150), Position(PartParameters.Strength, 150), 10.0 / 380));
        PartParameters.SliderOver(PartParameterId.Stroke, [piston.Stroke]).Readout.ShouldBe(UiText.Format("±{0}%", new FixedNumber(30, 0)));
        PartParameters.SliderOver(PartParameterId.MaxSpeed, [piston.MaxSpeed]).Readout.ShouldBe(UiText.Format("{0} m/s", new FixedNumber(2, 1)));
    }

    [Fact]
    public void ASpring_ShowsItsStiffnessInNewtonsPerMetreAndDampingInPercent()
    {
        PartParameters.SliderOver(PartParameterId.Stiffness, [SpringDef.DefaultStiffness]).Readout.ShouldBe(UiText.Format("{0} N/m", new FixedNumber(400, 0)));
        PartParameters.SliderOver(PartParameterId.Damping, [SpringDef.DefaultDamping]).Readout.ShouldBe(UiText.Format("{0}%", new FixedNumber(30, 0)));
        PartParameters.ValueAt(PartParameterId.Stiffness, 0).ShouldBe(50);
        PartParameters.ValueAt(PartParameterId.Stiffness, 1).ShouldBe(2000);
        PartParameters.ValueAt(PartParameterId.Damping, 0).ShouldBe(0);
        PartParameters.ValueAt(PartParameterId.Damping, 1).ShouldBe(1);
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
    [InlineData(PartParameterId.Stiffness, 650)]
    [InlineData(PartParameterId.Damping, 0.45)]
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

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
        PartParameters.SliderOver(PartParameterId.Stroke, [piston.Stroke]).Readout.ShouldBe(UiText.Format("{0}%", new FixedNumber(50, 0)));
        PartParameters.SliderOver(PartParameterId.StartPosition, [piston.Start]).Readout.ShouldBe(UiText.Format("{0}%", new FixedNumber(50, 0)));
        PartParameters.SliderOver(PartParameterId.MaxSpeed, [piston.MaxSpeed]).Readout.ShouldBe(UiText.Format("{0} m/s", new FixedNumber(2, 1)));
        PartParameters.SliderOver(PartParameterId.RiseTime, [piston.RiseTime]).Readout.ShouldBe(UiText.Format("{0} s", new FixedNumber(0.2, 1)));
    }

    [Fact]
    public void RiseTime_SnapsToItsFourStops_EvenlySpacedAlongTheSlider()
    {
        // 0.1 and 0.2 s get as much slider as 0.5 and 1 s (#801).
        double[] stops = [0.1, 0.2, 0.5, 1];
        stops.Select((stop, i) => PartParameters.ValueAt(PartParameterId.RiseTime, i / 3.0)).ShouldBe(stops);
        PartParameters.ValueAt(PartParameterId.RiseTime, 0.2).ShouldBe(0.2);
        PartParameters.ValueAt(PartParameterId.RiseTime, 0.9).ShouldBe(1);
        PartParameters.SliderOver(PartParameterId.RiseTime, [0.5]).High.ShouldBe(2.0 / 3, tolerance: 1e-9);
        PartParameters.SliderOver(PartParameterId.RiseTime, [0.5]).Step.ShouldBe(1.0 / 3, tolerance: 1e-9);
    }

    [Fact]
    public void ASteppedSlider_PlacesAValueBetweenTwoStopsBetweenTheirPositions()
    {
        var range = SettingRange.Of(0.1, 0.2, 0.5, 1);

        range.Position(0.35).ShouldBe((1 + 0.5) / 3, tolerance: 1e-9);
        range.Position(0.05).ShouldBe(0);
        range.Position(3).ShouldBe(1);
    }

    [Theory]
    [InlineData(new[] { 0.5 })]
    [InlineData(new[] { 0.1, 0.5, 0.2 })]
    [InlineData(new[] { 0.1, 0.1 })]
    public void ASteppedSlider_RejectsFewerThanTwoOrNonIncreasingStops(double[] stops)
    {
        Should.Throw<ArgumentException>(() => SettingRange.Of(stops));
    }

    [Fact]
    public void ASpring_ShowsItsStiffnessInNewtonsPerMetreAndDampingInNewtonSecondsPerMetre()
    {
        var spring = new SpringDef(4, 1, 2);

        PartParameters.SliderOver(PartParameterId.Stiffness, [SpringDef.DefaultStiffness]).Readout.ShouldBe(UiText.Format("{0} N/m", new FixedNumber(400, 0)));
        PartParameters.SliderOver(PartParameterId.Damping, [SpringDef.DefaultDamping]).Readout.ShouldBe(UiText.Format("{0} N·s/m", new FixedNumber(10, 0)));
        PartParameters.ValueAt(PartParameterId.Stiffness, 0).ShouldBe(50);
        PartParameters.ValueAt(PartParameterId.Stiffness, 1).ShouldBe(2000);
        PartParameters.ValueAt(PartParameterId.Damping, 0).ShouldBe(0);
        PartParameters.ValueAt(PartParameterId.Damping, 1).ShouldBe(100);
        PartParameters.SliderOver(PartParameterId.Stroke, [spring.Stroke]).Readout.ShouldBe(UiText.Format("{0}%", new FixedNumber(100, 0)));
        PartParameters.SliderOver(PartParameterId.CoilLength, [spring.CoilLength]).Readout.ShouldBe(UiText.Format("{0}%", new FixedNumber(67, 0)));
    }

    [Fact]
    public void CoilLength_RunsFromZeroToAHundredPercent_InStepsOfOne()
    {
        PartParameters.ValueAt(PartParameterId.CoilLength, 0).ShouldBe(SpringDef.MinCoilLength);
        PartParameters.ValueAt(PartParameterId.CoilLength, 1).ShouldBe(SpringDef.MaxCoilLength);
        PartParameters.ValueAt(PartParameterId.CoilLength, 0.334).ShouldBe(0.33, tolerance: 1e-9);
    }

    [Fact]
    public void SliderPositions_GiveWholeStepsInWorldUnits()
    {
        PartParameters.ValueAt(PartParameterId.Strength, 0).ShouldBe(2000);
        PartParameters.ValueAt(PartParameterId.Strength, 1).ShouldBe(40000);
        PartParameters.ValueAt(PartParameterId.Stroke, 0).ShouldBe(0.1);
        PartParameters.ValueAt(PartParameterId.Stroke, 0.51).ShouldBe(0.55);
        PartParameters.ValueAt(PartParameterId.Stroke, 1).ShouldBe(1);
        PartParameters.ValueAt(PartParameterId.MaxSpeed, 0.5).ShouldBe(230, tolerance: 1e-9);
    }

    [Theory]
    [InlineData(PartParameterId.Strength, 15000)]
    [InlineData(PartParameterId.Stroke, 0.45)]
    [InlineData(PartParameterId.MaxSpeed, 120)]
    [InlineData(PartParameterId.Stiffness, 650)]
    [InlineData(PartParameterId.Damping, 45)]
    [InlineData(PartParameterId.RiseTime, 0.5)]
    [InlineData(PartParameterId.CoilLength, 0.35)]
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

    [Fact]
    public void EveryPanelSetting_HasAHelpTextOfItsOwn()
    {
        // Touching a slider shows this text (#867).
        var scales = Enum.GetValues<PartParameterId>()
            .Select(PartParameters.Of)
            .Where(parameter => parameter.InPanel)
            .Select(parameter => parameter.Slider!)
            .ToArray();

        scales.ShouldNotBeEmpty();
        scales.ShouldAllBe(scale => !string.IsNullOrWhiteSpace(scale.Help.Message) && scale.Help.Message != scale.Label.Message);
    }

    private static double Position(PartParameter parameter, double shown) => parameter.Slider!.Range.Position(shown);
}

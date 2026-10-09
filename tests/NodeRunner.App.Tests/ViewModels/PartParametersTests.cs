using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartParametersTests
{
    [Fact]
    public void EverySetting_IsBasicOrAdvanced_AsDesigned()
    {
        // #903, #989: tuning values sit under Advanced; the rest are basic.
        PartParameterId[] advanced =
        [
            PartParameterId.StartPosition,
            PartParameterId.ServoStartPosition,
            PartParameterId.MaxSpeed,
            PartParameterId.AngularMaxSpeed,
            PartParameterId.RiseTime,
            PartParameterId.Damping,
            PartParameterId.CoilLength,
        ];

        foreach (var id in Enum.GetValues<PartParameterId>())
        {
            PartParameters.Of(id).Advanced.ShouldBe(advanced.Contains(id), id.ToString());
        }
    }

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
    public void ANewWheel_ShowsItsRadiusInMetresAndItsGripInPercent()
    {
        var wheel = new WheelDef(5, 1);

        PartParameters.SliderOver(PartParameterId.WheelRadius, [wheel.Radius]).Readout.ShouldBe(UiText.Format("{0} m", new FixedNumber(0.4, 1)));
        PartParameters.SliderOver(PartParameterId.Grip, [wheel.Grip]).Readout.ShouldBe(UiText.Format("{0}%", new FixedNumber(80, 0)));
        PartParameters.WheelRadius.Slider!.Help.ShouldBe(UiText.Plain("Bigger wheels roll over bumps but weigh more"));
        PartParameters.Grip.Slider!.Help.ShouldBe(UiText.Plain("How well the tyre holds the ground. Low grip slides"));
    }

    [Fact]
    public void WheelRadius_RunsFromFortyCentimetresToAMetre_InStepsOfTen_AndGripInStepsOfTenPercent()
    {
        PartParameters.ValueAt(PartParameterId.WheelRadius, 0).ShouldBe(WheelDef.MinRadius);
        PartParameters.ValueAt(PartParameterId.WheelRadius, 1).ShouldBe(WheelDef.MaxRadius);
        PartParameters.ValueAt(PartParameterId.WheelRadius, 0.26).ShouldBe(60, tolerance: 1e-9);
        PartParameters.ValueAt(PartParameterId.Grip, 0).ShouldBe(0);
        PartParameters.ValueAt(PartParameterId.Grip, 1).ShouldBe(1);
        PartParameters.ValueAt(PartParameterId.Grip, 0.34).ShouldBe(0.3, tolerance: 1e-9);
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
    [InlineData(PartParameterId.WheelRadius, 70)]
    [InlineData(PartParameterId.Grip, 0.6)]
    [InlineData(PartParameterId.Rays, 5)]
    [InlineData(PartParameterId.Spread, Math.PI / 4)]
    [InlineData(PartParameterId.CameraRange, 220)]
    [InlineData(PartParameterId.CameraRange, 370)]
    public void ASharedValue_RoundTripsThroughItsSlider(PartParameterId id, double value)
    {
        var slider = PartParameters.SliderOver(id, [value, value]);

        slider.ValuesDiffer.ShouldBeFalse();
        PartParameters.ValueAt(id, slider.High).ShouldBe(value, tolerance: 1e-9);
    }

    [Fact]
    public void Rays_SnapToOneThreeOrFive_AndChangeTheBrainsPorts()
    {
        double[] stops = [1, 3, 5];
        stops.Select((stop, i) => PartParameters.ValueAt(PartParameterId.Rays, i / 2.0)).ShouldBe(stops);
        PartParameters.ValueAt(PartParameterId.Rays, 0.2).ShouldBe(1);
        PartParameters.ValueAt(PartParameterId.Rays, 0.3).ShouldBe(3);
        PartParameters.SliderOver(PartParameterId.Rays, [3]).Readout.ShouldBe(UiText.Format("{0}", new FixedNumber(3, 0)));
        PartParameters.Rays.ChangesPorts.ShouldBeTrue();
        PartParameters.Rays.MultiEditable.ShouldBeTrue();
        Enum.GetValues<PartParameterId>().Where(id => PartParameters.Of(id).ChangesPorts).ShouldBe([PartParameterId.Rays]);
    }

    [Fact]
    public void Spread_RunsFrom15To90Degrees_InStepsOf5()
    {
        PartParameters.ValueAt(PartParameterId.Spread, 0).ShouldBe(15 * Math.PI / 180, tolerance: 1e-12);
        PartParameters.ValueAt(PartParameterId.Spread, 1).ShouldBe(Math.PI / 2, tolerance: 1e-12);
        PartParameters.SliderOver(PartParameterId.Spread, [SensorDef.DefaultSpread]).Readout.ShouldBe(UiText.Format("{0}°", new FixedNumber(90, 0)));
        PartParameters.SliderOver(PartParameterId.Spread, [SensorDef.DefaultSpread]).Step.ShouldBe(5.0 / 75, tolerance: 1e-12);
    }

    [Fact]
    public void CameraRange_RunsFrom1To4Metres_InStepsOfATenth_WithTheDefaultOnAStep()
    {
        PartParameters.ValueAt(PartParameterId.CameraRange, 0).ShouldBe(100, tolerance: 1e-9);
        PartParameters.ValueAt(PartParameterId.CameraRange, 1).ShouldBe(400, tolerance: 1e-9);
        PartParameters.SliderOver(PartParameterId.CameraRange, [SensorDef.DefaultRange]).Step.ShouldBe(0.1 / 3, tolerance: 1e-12);
        var slider = PartParameters.SliderOver(PartParameterId.CameraRange, [SensorDef.DefaultRange]);
        slider.Readout.ShouldBe(UiText.Format("{0} m", new FixedNumber(2.2, 1)));
        PartParameters.ValueAt(PartParameterId.CameraRange, slider.High).ShouldBe(SensorDef.DefaultRange, tolerance: 1e-9);
        (slider.High / slider.Step).ShouldBe(Math.Round(slider.High / slider.Step), tolerance: 1e-9);
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

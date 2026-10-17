using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartParametersTests
{
    [Fact]
    public void EverySetting_IsBasicOrAdvanced_AsDesigned_AndEveryPanelSettingHasAHelpTextOfItsOwn()
    {
        // #903, #989, #1189, #1296: the split docs/BUILD_MODE.md → "Parameters" owns.
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
            PartParameters.Of(id).Id.ShouldBe(id);
            PartParameters.Of(id).Advanced.ShouldBe(advanced.Contains(id), id.ToString());
        }

        // Touching a slider shows this text (#867).
        var scales = Enum.GetValues<PartParameterId>().Select(PartParameters.Of).Where(parameter => parameter.InPanel).Select(parameter => parameter.Slider!).ToArray();
        scales.Length.ShouldBe(Enum.GetValues<PartParameterId>().Length - 1);
        scales.ShouldAllBe(scale => !string.IsNullOrWhiteSpace(scale.Help.Message) && scale.Help.Message != scale.Label.Message);
        Enum.GetValues<PartParameterId>().Where(id => PartParameters.Of(id).ChangesPorts).ShouldBe([PartParameterId.Rays]);
    }

    [Fact]
    public void Aim_IsSetOnTheCanvas_OnePartAtATime()
    {
        PartParameters.Aim.InPanel.ShouldBeFalse();
        PartParameters.Aim.MultiEditable.ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => PartParameters.SliderOver(PartParameterId.Aim, [0.0]));
    }

    // Each setting reads in its unit and runs between its ends; the value read sits on a step, so
    // it comes back from the slider unchanged.
    [Fact]
    public void EverySlider_ReadsInItsUnit_RunsBetweenItsEnds_AndGivesBackAValueOnAStep()
    {
        var rpm = Math.Tau / 60;
        var degrees = Math.PI / 180;
        (PartParameterId Id, double Value, UiText Readout, double Min, double Max)[] sliders =
        [
            (PartParameterId.Strength, PistonDef.DefaultStrength, UiText.Format("{0} N", new FixedNumber(150, 0)), 2000, 40000),
            (PartParameterId.ServoStrength, StepperDef.DefaultStrength, UiText.Format("{0} N·m", new FixedNumber(150, 0)), 50000, 2000000),
            (PartParameterId.MotorStrength, VelocityMotorDef.DefaultStrength, UiText.Format("{0} N·m", new FixedNumber(40, 0)), 50000, 400000),
            (PartParameterId.BrakeStrength, BrakeDef.DefaultStrength, UiText.Format("{0} N·m", new FixedNumber(200, 0)), 200000, BrakeDef.LockedStrength),
            (PartParameterId.StepsPerTurn, StepperDef.DefaultStepsPerTurn, UiText.Format("{0}", new FixedNumber(8, 0)), 2, 24),
            (PartParameterId.MaxFrequency, StepperDef.DefaultMaxFrequency, UiText.Format("{0} Hz", new FixedNumber(2, 1)), 1, 5),
            (PartParameterId.Stroke, PistonDef.DefaultStroke, UiText.Format("{0}%", new FixedNumber(50, 0)), 0.1, PistonDef.MaxStroke),
            (PartParameterId.Range, ServoDef.DefaultRange, UiText.Format("{0}°", new FixedNumber(180, 0)), 20 * degrees, 2 * Math.PI),
            (PartParameterId.StartPosition, PistonDef.DefaultStart, UiText.Format("{0}%", new FixedNumber(50, 0)), 0, 1),
            (PartParameterId.ServoStartPosition, ServoDef.DefaultStart, UiText.Format("{0}%", new FixedNumber(50, 0)), 0, 1),
            (PartParameterId.MaxSpeed, PistonDef.DefaultMaxSpeed, UiText.Format("{0} m/s", new FixedNumber(2, 1)), 50, 400),
            (PartParameterId.AngularMaxSpeed, ServoDef.DefaultMaxSpeed, UiText.Format("{0} rpm", new FixedNumber(60, 0)), 5 * rpm, 120 * rpm),
            (PartParameterId.MotorMaxSpeed, VelocityMotorDef.DefaultMaxSpeed, UiText.Format("{0} rpm", new FixedNumber(60, 0)), 10 * rpm, 240 * rpm),
            (PartParameterId.RiseTime, PistonDef.DefaultRiseTime, UiText.Format("{0} s", new FixedNumber(0.2, 1)), 0.1, 1),
            (PartParameterId.Stiffness, SpringDef.DefaultStiffness, UiText.Format("{0} N/m", new FixedNumber(400, 0)), 50, 2000),
            (PartParameterId.Damping, SpringDef.DefaultDamping, UiText.Format("{0} N·s/m", new FixedNumber(10, 0)), 0, 100),
            (PartParameterId.CoilLength, 0.35, UiText.Format("{0}%", new FixedNumber(35, 0)), SpringDef.MinCoilLength, SpringDef.MaxCoilLength),
            (PartParameterId.WheelRadius, WheelDef.DefaultRadius, UiText.Format("{0} m", new FixedNumber(0.4, 1)), WheelDef.MinRadius, WheelDef.MaxRadius),
            (PartParameterId.Rays, 3, UiText.Format("{0}", new FixedNumber(3, 0)), 1, 5),
            (PartParameterId.Spread, SensorDef.DefaultSpread, UiText.Format("{0}°", new FixedNumber(90, 0)), 15 * degrees, 90 * degrees),
            (PartParameterId.CameraRange, SensorDef.DefaultRange, UiText.Format("{0} m", new FixedNumber(2.2, 1)), 100, 1000),
        ];
        sliders.Select(slider => slider.Id).ShouldBe(Enum.GetValues<PartParameterId>().Where(id => PartParameters.Of(id).InPanel), ignoreOrder: true);

        foreach (var (id, value, readout, min, max) in sliders)
        {
            var slider = PartParameters.SliderOver(id, [value, value]);
            slider.Readout.ShouldBe(readout, id.ToString());
            slider.ValuesDiffer.ShouldBeFalse(id.ToString());
            PartParameters.ValueAt(id, slider.High).ShouldBe(value, tolerance: 1e-6, id.ToString());
            (slider.High / slider.Step).ShouldBe(Math.Round(slider.High / slider.Step), tolerance: 1e-9, id.ToString());
            PartParameters.ValueAt(id, 0).ShouldBe(min, tolerance: 1e-9, id.ToString());
            (PartParameters.ValueAt(id, 1) is var top && (top == max || Math.Abs(top - max) < 1e-9)).ShouldBeTrue(id.ToString());
        }
    }

    [Fact]
    public void ALinearSlider_SnapsToWholeSteps_AndShowsWhereEachEndOfASpanSits()
    {
        PartParameters.SliderOver(PartParameterId.Strength, [PistonDef.DefaultStrength, 30000])
            .ShouldBe(new ParameterSlider(PartParameterId.Strength, UiText.Plain("Max strength"), UiText.Format("{0}–{1} N", new FixedNumber(150, 0), new FixedNumber(300, 0)), 13.0 / 38, 28.0 / 38, 10.0 / 380));
        PartParameters.ValueAt(PartParameterId.Stroke, 0.51).ShouldBe(1.05);
        PartParameters.ValueAt(PartParameterId.WheelRadius, 0.26).ShouldBe(60, tolerance: 1e-9);
        PartParameters.Stroke.Slider!.Range.ShouldBe(new SettingRange(10, 200, 5));
        PartParameters.SliderOver(PartParameterId.Stroke, [0.3, SpringDef.MaxStroke]).Readout
            .ShouldBe(UiText.Format("{0}–{1}%", new FixedNumber(30, 0), new FixedNumber(200, 0)));
    }

    // The old slider reached 0.2 Hz (#1296): such a saved value reads as it is, at the slider's low end.
    [Fact]
    public void MaxFrequency_UnderTheSlider_ReadsAsSaved_AtTheLowEnd()
    {
        var slider = PartParameters.SliderOver(PartParameterId.MaxFrequency, [0.5]);

        slider.Readout.ShouldBe(UiText.Format("{0} Hz", new FixedNumber(0.5, 1)));
        slider.High.ShouldBe(0);
    }

    [Fact]
    public void ASteppedSlider_SnapsToItsStops_EvenlySpacedAlongTheSlider_AndPlacesAValueBetweenTwoStopsBetweenThem()
    {
        // 0.1 and 0.2 s get as much slider as 0.5 and 1 s (#801).
        double[] stops = [0.1, 0.2, 0.5, 1];
        stops.Select((stop, i) => PartParameters.ValueAt(PartParameterId.RiseTime, i / 3.0)).ShouldBe(stops);
        PartParameters.ValueAt(PartParameterId.RiseTime, 0.2).ShouldBe(0.2);
        PartParameters.ValueAt(PartParameterId.RiseTime, 0.9).ShouldBe(1);
        PartParameters.SliderOver(PartParameterId.RiseTime, [0.5]).Step.ShouldBe(1.0 / 3, tolerance: 1e-9);
        var range = SettingRange.Of(stops);
        range.Position(0.35).ShouldBe((1 + 0.5) / 3, tolerance: 1e-9);
        range.Position(0.05).ShouldBe(0);
        range.Position(3).ShouldBe(1);
    }

    [Theory]
    [InlineData(new[] { 0.5 })]
    [InlineData(new[] { 0.1, 0.5, 0.2 })]
    public void ASteppedSlider_RejectsFewerThanTwoOrNonIncreasingStops(double[] stops)
    {
        Should.Throw<ArgumentException>(() => SettingRange.Of(stops));
    }

    // 20–400 N·m in steps of 20, then one step more that reads Locked: an infinite Strength (#1041).
    [Fact]
    public void BrakeStrength_ReadsLocked_AtItsTopStep_AlsoAtTheTopOfASpan()
    {
        var locked = PartParameters.SliderOver(PartParameterId.BrakeStrength, [BrakeDef.LockedStrength]);
        locked.Readout.ShouldBe(UiText.Plain("Locked"));
        locked.High.ShouldBe(1);
        locked.Step.ShouldBe(1.0 / 20, tolerance: 1e-12);
        PartParameters.ValueAt(PartParameterId.BrakeStrength, 1 - locked.Step).ShouldBe(4000000, tolerance: 1e-6);
        PartParameters.SliderOver(PartParameterId.BrakeStrength, [4000000]).Readout.ShouldBe(UiText.Format("{0} N·m", new FixedNumber(400, 0)));
        var span = PartParameters.SliderOver(PartParameterId.BrakeStrength, [BrakeDef.LockedStrength, 600000]);
        span.Readout.ShouldBe(UiText.Format("{0} N·m–Locked", new FixedNumber(60, 0)));
        span.High.ShouldBe(1);
        PartParameters.SliderOver(PartParameterId.BrakeStrength, [600000, 4000000]).Readout
            .ShouldBe(UiText.Format("{0}–{1} N·m", new FixedNumber(60, 0), new FixedNumber(400, 0)));
    }

    // The old 30–720°/s are exactly 5–120 rpm, so every saved Servo still fits; a Brake fits up to
    // 400 N·m or Locked.
    [Theory]
    [InlineData(PartParameterId.AngularMaxSpeed, 30 * Math.PI / 180, true)]
    [InlineData(PartParameterId.AngularMaxSpeed, 720 * Math.PI / 180, true)]
    [InlineData(PartParameterId.BrakeStrength, 200000.0, true)]
    [InlineData(PartParameterId.BrakeStrength, 4000000.0, true)]
    [InlineData(PartParameterId.BrakeStrength, BrakeDef.LockedStrength, true)]
    [InlineData(PartParameterId.BrakeStrength, 4100000.0, false)]
    [InlineData(PartParameterId.BrakeStrength, 199999.0, false)]
    public void IsOnItsSlider_KeepsEverySavedValueTheSliderCanShow(PartParameterId id, double value, bool onIt)
    {
        CreatureBuilder.IsOnItsSlider(id, value).ShouldBe(onIt);
    }
}

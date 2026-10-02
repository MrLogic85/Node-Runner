using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PistonSettingsTests
{
    [Fact]
    public void For_ANewPiston_ShowsTheDefaultsInNewtonsPercentAndMetresPerSecond()
    {
        var settings = PistonSettings.For(new PistonDef(3, 1, 2));

        settings.Strength.ShouldBe(new PartSlider("Max strength", "150 N", PistonSettings.Strength.Position(150)));
        settings.Stroke.ShouldBe(new PartSlider("Stroke", "±30%", PistonSettings.Stroke.Position(30)));
        settings.MaxSpeed.ShouldBe(new PartSlider("Max speed", "2.0 m/s", PistonSettings.MaxSpeed.Position(2)));
    }

    [Fact]
    public void SliderPositions_GiveWholeStepsInWorldUnits()
    {
        PistonSettings.StrengthAt(0).ShouldBe(2000);
        PistonSettings.StrengthAt(1).ShouldBe(40000);
        PistonSettings.StrokeAt(0.51).ShouldBe(0.3);
        PistonSettings.MaxSpeedAt(0.5).ShouldBe(230, tolerance: 1e-9);
    }

    [Fact]
    public void ADefaultPiston_RoundTripsThroughItsSliders()
    {
        var piston = new PistonDef(3, 1, 2);
        var settings = PistonSettings.For(piston);

        PistonSettings.StrengthAt(settings.Strength.Position).ShouldBe(piston.Strength, tolerance: 1e-9);
        PistonSettings.StrokeAt(settings.Stroke.Position).ShouldBe(piston.Stroke, tolerance: 1e-9);
        PistonSettings.MaxSpeedAt(settings.MaxSpeed.Position).ShouldBe(piston.MaxSpeed, tolerance: 1e-9);
    }
}

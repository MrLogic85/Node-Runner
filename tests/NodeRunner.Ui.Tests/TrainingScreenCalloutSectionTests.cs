using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

/// <summary>How a selected part becomes its section of the Training part callout (#1064, #1066).</summary>
public sealed class TrainingScreenCalloutSectionTests
{
    [Fact]
    public void CalloutSection_ShowsInputsInAccentThenOutputsInOutput()
    {
        var servo = new PartCalloutPresentation(
            PartSettingsKind.Servo,
            UiText.AsWritten("Knee"),
            ShowsGlyph: true,
            [
                new PartCalloutPort(BrainPort.Input(1, "angle"), UiText.Plain("angle"), PortRange.MinusOneToOne, -0.5),
                new PartCalloutPort(BrainPort.Input(1, "speed"), UiText.Plain("speed"), PortRange.MinusOneToOne, null),
            ],
            [
                new PartCalloutPort(BrainPort.Output(1, "angle", PortSignal.Position), UiText.Plain("angle"), PortRange.MinusOneToOne, 0.25),
                new PartCalloutPort(BrainPort.Output(1, "strength", PortSignal.Strength), UiText.Plain("strength"), PortRange.ZeroToOne, 0.75),
            ],
            null);

        var lines = Lines(servo)!;

        lines.Select(line => line.Color).ShouldBe([UiTokens.Color.Accent, UiTokens.Color.Output]);
        lines.ShouldAllBe(line => line.Note == null);
        lines[0].Meters[0].ShouldBe(new UiCalloutMeter("angle", -0.5, Centred: true));
        lines[0].Meters[1].Label.ShouldBe("speed");
        double.IsNaN(lines[0].Meters[1].Value).ShouldBeTrue();
        lines[1].Meters.ShouldBe([new UiCalloutMeter("angle", 0.25, Centred: true), new UiCalloutMeter("strength", 0.75, Centred: false)]);
    }

    [Fact]
    public void CalloutSection_ForAPartWithOnlyInputs_HasNoOutputLine()
    {
        var camera = new PartCalloutPresentation(
            PartSettingsKind.Camera,
            UiText.AsWritten("Eye"),
            ShowsGlyph: true,
            [new PartCalloutPort(BrainPort.Input(1, "hit"), UiText.Plain("hit"), PortRange.ZeroToOne, 1)],
            [],
            null);

        var lines = Lines(camera)!;

        lines.ShouldHaveSingleItem().Color.ShouldBe(UiTokens.Color.Accent);
    }

    [Fact]
    public void CalloutSection_ForAWheel_IsOneMutedNote()
    {
        var wheel = new PartCalloutPresentation(PartSettingsKind.Wheel, UiText.AsWritten("Wheel"), ShowsGlyph: true, [], [], UiText.Plain("No brain ports"));

        var line = Lines(wheel)!.ShouldHaveSingleItem();

        line.Note.ShouldBe("No brain ports");
        line.Color.ShouldBe(UiTokens.Color.Muted);
        line.Meters.ShouldBeEmpty();
    }

    [Fact]
    public void CalloutSection_ForANameOnlyPart_HasNoLines()
    {
        var beam = new PartCalloutPresentation(PartSettingsKind.Beam, UiText.AsWritten("Shin"), ShowsGlyph: false, [], [], null);

        Lines(beam).ShouldBeNull();
    }

    [Fact]
    public void CalloutSection_ShowsTheGlyphAndName_OrTheNameAlone()
    {
        var wheel = new PartCalloutPresentation(PartSettingsKind.Wheel, UiText.AsWritten("Rear"), ShowsGlyph: true, [], [], UiText.Plain("No brain ports"));
        var beam = new PartCalloutPresentation(PartSettingsKind.Beam, UiText.AsWritten("Shin"), ShowsGlyph: false, [], [], null);

        var wheelSection = Section(wheel, "Rear");
        var beamSection = Section(beam, "Shin");

        (wheelSection.IconId, wheelSection.Text).ShouldBe((UiIconId.PartWheel, "Rear"));
        (beamSection.IconId, beamSection.Text).ShouldBe((UiIconId.None, "Shin"));
    }

    // Translation needs Godot, so the tests show each text's English message.
    private static UiCalloutSection Section(PartCalloutPresentation callout, string name) =>
        TrainingScreen.CalloutSection(callout, name, callout.Note is { } note ? () => note.Message : null, port => port.Label.Message);

    private static IReadOnlyList<UiCalloutLine>? Lines(PartCalloutPresentation callout) => Section(callout, "Part").Lines;
}

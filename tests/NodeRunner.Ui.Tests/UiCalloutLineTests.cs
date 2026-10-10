using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>When a refreshed callout can update its rows in place (#1064).</summary>
public sealed class UiCalloutLineTests
{
    [Fact]
    public void SameShape_ComparesLineKindsAndMeterCounts_NotValues()
    {
        IReadOnlyList<UiCalloutLine> live = [UiCalloutLine.OfMeters(UiTokens.Color.Accent, [new UiCalloutMeter("along", 0.2, Centred: true)])];
        IReadOnlyList<UiCalloutLine> later = [UiCalloutLine.OfMeters(UiTokens.Color.Accent, [new UiCalloutMeter("along", -0.7, Centred: true)])];
        IReadOnlyList<UiCalloutLine> longer = [UiCalloutLine.OfMeters(UiTokens.Color.Accent, [new UiCalloutMeter("along", 0, Centred: true), new UiCalloutMeter("across", 0, Centred: true)])];

        UiCalloutLine.SameShape(live, later).ShouldBeTrue();
        UiCalloutLine.SameShape(live, longer).ShouldBeFalse();
        UiCalloutLine.SameShape(live, [UiCalloutLine.OfNote("No brain ports")]).ShouldBeFalse();
        UiCalloutLine.SameShape(null, []).ShouldBeTrue();
        UiCalloutLine.SameShape([], null).ShouldBeTrue();
    }
}

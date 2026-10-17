using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class DistanceRulerTests
{
    [Fact]
    public void Fill_LabelsEveryMetreFromTheStartAndTicksEveryHalf_ReplacingWhatTheListHeld()
    {
        var marks = new List<RulerMark> { new(1, true, 7) };

        DistanceRuler.Fill(startX: 250, left: 100, right: 460, metresPerLabel: 1, marks);

        marks.ShouldBe(
        [
            new RulerMark(100, false, null),
            new RulerMark(150, true, -1),
            new RulerMark(200, false, null),
            new RulerMark(250, true, 0),
            new RulerMark(300, false, null),
            new RulerMark(350, true, 1),
            new RulerMark(400, false, null),
            new RulerMark(450, true, 2),
        ]);
    }

    // A label step with a 5 gets a long tick per label and a short one every fifth; any other a
    // long tick every power of ten and a short one halfway, so at 2 m a long tick goes unlabelled.
    [Theory]
    [InlineData(2, 50, 100)]
    [InlineData(5, 100, 500)]
    [InlineData(10, 500, 1000)]
    [InlineData(50, 1000, 5000)]
    [InlineData(200, 5000, 10000)]
    public void Fill_ThinsTheTicksOutAsTheLabelsDo(int metresPerLabel, double tickGap, double longTickGap)
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX: 0, left: 0, right: 20_000, metresPerLabel, marks);

        (marks[1].X - marks[0].X).ShouldBe(tickGap);
        var longTicks = marks.Where(mark => mark.IsMajor).ToArray();
        (longTicks[1].X - longTicks[0].X).ShouldBe(longTickGap);
        longTicks.Where(mark => mark.LabelMetre is not null).Select(mark => mark.LabelMetre!.Value)
            .ShouldBe(Enumerable.Range(0, (200 / metresPerLabel) + 1).Select(label => (long)label * metresPerLabel));
    }

    [Theory]
    [InlineData(-120, 0, 0)]
    [InlineData(30, 40, 0)]
    [InlineData(30, 20, 50)]
    public void FillHeights_CountsUpFromTheGround_KeepingAMarkBelowTheFloorWhileItsLabelStillReachesAboveIt(double floor, double labelReach, double firstHeight)
    {
        var marks = new List<RulerMark>();

        DistanceRuler.FillHeights(floor, labelReach, high: 160, metresPerLabel: 1, marks);

        marks[0].X.ShouldBe(firstHeight);
        marks[^1].ShouldBe(new RulerMark(150, false, null));
    }

    [Fact]
    public void Fill_CountsFarFromTheStart()
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX: 0, left: 99_990, right: 100_010, metresPerLabel: 1, marks);

        marks.ShouldBe([new RulerMark(100_000, true, 1000)]);
    }

    [Theory]
    [InlineData(double.NaN, 0, 100)]
    [InlineData(0, double.NegativeInfinity, 100)]
    [InlineData(0, 100, 0)]
    [InlineData(0, 0, 1e9)]
    [InlineData(0, 1e25, 1e25)]
    [InlineData(-1.7e308, 1.7e308, 1.7e308)]
    public void Fill_ListsNothingForAnUnusableRange(double startX, double left, double right)
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX, left, right, metresPerLabel: 1, marks);

        marks.ShouldBeEmpty();
    }

    [Fact]
    public void Fill_RejectsALabelStepBelowOne()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DistanceRuler.Fill(0, 0, 100, metresPerLabel: 0, []));
    }

    [Fact]
    public void Label_ShowsTheMetreWithItsUnit()
    {
        DistanceRuler.Label(-12).ShouldBe(UiText.Format("{0} m", -12L));
    }

    // The fewest of 1, 2, 5, 10, 20… metres that leave the room; thinner at once, but closer
    // again only with a fifth more room to spare; every metre for an unusable size.
    [Theory]
    [InlineData(100, 40, 1, 1)]
    [InlineData(39, 40, 1, 2)]
    [InlineData(5, 40, 1, 10)]
    [InlineData(41, 40, 2, 2)]
    [InlineData(48, 40, 2, 1)]
    [InlineData(30, 40, 5, 2)]
    [InlineData(0, 40, 1, 1)]
    [InlineData(100, double.PositiveInfinity, 1, 1)]
    [InlineData(100, double.NaN, 1, 1)]
    public void MetresPerLabel_PicksTheFewestMetresThatLeaveRoom(double metreLength, double labelRoom, int shown, int expected)
    {
        DistanceRuler.MetresPerLabel(metreLength, labelRoom, shown).ShouldBe(expected);
    }
}

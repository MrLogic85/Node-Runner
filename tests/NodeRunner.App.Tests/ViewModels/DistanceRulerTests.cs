using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class DistanceRulerTests
{
    [Fact]
    public void Fill_LabelsEveryMetreFromTheStartAndTicksEveryHalf()
    {
        var marks = new List<RulerMark>();

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

    [Fact]
    public void Fill_ReplacesWhatTheListHeld()
    {
        var marks = new List<RulerMark> { new(1, true, 7) };

        DistanceRuler.Fill(startX: 0, left: 1, right: 49, metresPerLabel: 1, marks);

        marks.ShouldBeEmpty();
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
    [InlineData(0, -1e25, 1e25)]
    [InlineData(-1.7e308, 1.7e308, 1.7e308)]
    public void Fill_ListsNothingForAnUnusableRange(double startX, double left, double right)
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX, left, right, metresPerLabel: 1, marks);

        marks.ShouldBeEmpty();
    }

    [Fact]
    public void Fill_LabelsOnlyMultiplesOfTheLabelStep()
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX: 0, left: -200, right: 500, metresPerLabel: 2, marks);

        marks.Where(mark => mark.IsMajor).Select(mark => mark.LabelMetre).ShouldBe(
            [-2, null, 0, null, 2, null, 4, null]);
        marks.Count.ShouldBe(15);
    }

    [Fact]
    public void Label_ShowsTheMetreWithItsUnit()
    {
        DistanceRuler.Label(-12).ShouldBe(UiText.Format("{0} m", -12L));
    }

    [Fact]
    public void Fill_RejectsALabelStepBelowOne()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DistanceRuler.Fill(0, 0, 100, metresPerLabel: 0, []));
    }

    [Theory]
    [InlineData(100, 40, 1)]
    [InlineData(40, 40, 1)]
    [InlineData(39, 40, 2)]
    [InlineData(25, 60, 5)]
    [InlineData(5, 40, 10)]
    [InlineData(1, 150, 200)]
    public void MetresPerLabel_PicksTheFewestMetresThatLeaveRoom(double metreLength, double labelRoom, int expected)
    {
        DistanceRuler.MetresPerLabel(metreLength, labelRoom).ShouldBe(expected);
    }

    [Theory]
    [InlineData(39, 40, 1, 2)]
    [InlineData(41, 40, 2, 2)]
    [InlineData(47, 40, 2, 2)]
    [InlineData(48, 40, 2, 1)]
    [InlineData(41, 40, 1, 1)]
    [InlineData(100, 40, 5, 1)]
    [InlineData(30, 40, 5, 2)]
    [InlineData(5, 40, 2, 10)]
    public void MetresPerLabel_ThinsAtOnceButComesBackOnlyWithRoomToSpare(
        double metreLength, double labelRoom, int shown, int expected)
    {
        DistanceRuler.MetresPerLabel(metreLength, labelRoom, shown).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(double.NaN, 40)]
    [InlineData(100, double.PositiveInfinity)]
    [InlineData(100, double.NaN)]
    public void MetresPerLabel_LabelsEveryMetreForAnUnusableSize(double metreLength, double labelRoom)
    {
        DistanceRuler.MetresPerLabel(metreLength, labelRoom).ShouldBe(1);
    }
}

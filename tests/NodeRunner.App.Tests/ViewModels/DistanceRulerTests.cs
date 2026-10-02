using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class DistanceRulerTests
{
    [Fact]
    public void Fill_LabelsEveryMetreFromTheStartAndTicksEveryHalf()
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX: 250, left: 100, right: 460, marks);

        marks.ShouldBe(
        [
            new RulerMark(100, false, string.Empty),
            new RulerMark(150, true, "-1 m"),
            new RulerMark(200, false, string.Empty),
            new RulerMark(250, true, "0 m"),
            new RulerMark(300, false, string.Empty),
            new RulerMark(350, true, "1 m"),
            new RulerMark(400, false, string.Empty),
            new RulerMark(450, true, "2 m"),
        ]);
    }

    [Fact]
    public void Fill_ReplacesWhatTheListHeld()
    {
        var marks = new List<RulerMark> { new(1, true, "stale") };

        DistanceRuler.Fill(startX: 0, left: 1, right: 49, marks);

        marks.ShouldBeEmpty();
    }

    [Fact]
    public void Fill_CountsFarFromTheStart()
    {
        var marks = new List<RulerMark>();

        DistanceRuler.Fill(startX: 0, left: 99_990, right: 100_010, marks);

        marks.ShouldBe([new RulerMark(100_000, true, "1000 m")]);
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

        DistanceRuler.Fill(startX, left, right, marks);

        marks.ShouldBeEmpty();
    }
}

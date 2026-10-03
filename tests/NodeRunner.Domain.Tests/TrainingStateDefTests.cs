namespace NodeRunner.Domain.Tests;

public sealed class TrainingStateDefTests
{
    private static TrainingRunDef RunOf(double distance, string mapId = MapIds.Flat) => new(distance, 1, 0, mapId);

    [Fact]
    public void BestOn_IsTheBestOnlyOnItsOwnMap()
    {
        var state = TestTraining.State(3, bestDistance: 50);

        state.BestOn(MapIds.Flat).ShouldBe(state.Best);
        state.BestOn("map-hills").ShouldBeNull();
    }

    [Fact]
    public void Record_FirstGeneration_IsBothLatestAndBest()
    {
        var state = TrainingStateDef.Record(null, TestTraining.Brain, 1, RunOf(120));

        state.Generation.ShouldBe(1);
        state.Latest.ShouldBe(RunOf(120));
        state.Best.ShouldBe(new TrainingBestDef(1, 120, MapIds.Flat));
    }

    [Fact]
    public void Record_WorseGeneration_ReplacesLatestAndKeepsTheBest()
    {
        var previous = TrainingStateDef.Record(null, TestTraining.Brain, 4, RunOf(300));

        var state = TrainingStateDef.Record(previous, TestTraining.Brain, 5, RunOf(250));

        state.Generation.ShouldBe(5);
        state.Latest.Distance.ShouldBe(250);
        state.Best.ShouldBe(new TrainingBestDef(4, 300, MapIds.Flat));
    }

    [Fact]
    public void Record_EqualGeneration_KeepsTheEarlierBest()
    {
        var previous = TrainingStateDef.Record(null, TestTraining.Brain, 4, RunOf(300));

        TrainingStateDef.Record(previous, TestTraining.Brain, 5, RunOf(300)).Best.Generation.ShouldBe(4);
    }

    [Fact]
    public void Record_BetterGeneration_RaisesBoth()
    {
        var previous = TrainingStateDef.Record(null, TestTraining.Brain, 4, RunOf(300));

        var state = TrainingStateDef.Record(previous, TestTraining.Brain, 5, RunOf(350));

        state.Latest.Distance.ShouldBe(350);
        state.Best.ShouldBe(new TrainingBestDef(5, 350, MapIds.Flat));
    }

    [Fact]
    public void Record_TheBestsFrontDistanceIsTheFurthestWhicheverRunHoldsTheScore()
    {
        var previous = TrainingStateDef.Record(null, TestTraining.Brain, 4, new TrainingRunDef(300, 1, 0, MapIds.Flat, frontDistance: 310));

        var further = TrainingStateDef.Record(previous, TestTraining.Brain, 5, new TrainingRunDef(250, 1, 0, MapIds.Flat, frontDistance: 400));
        var better = TrainingStateDef.Record(previous, TestTraining.Brain, 5, new TrainingRunDef(350, 1, 0, MapIds.Flat, frontDistance: 200));

        further.Best.ShouldBe(new TrainingBestDef(4, 300, MapIds.Flat, 400));
        better.Best.ShouldBe(new TrainingBestDef(5, 350, MapIds.Flat, 310));
    }

    [Fact]
    public void Record_AfterASaveFromBeforeFrontDistance_TakesTheLatestFront()
    {
        var previous = new TrainingStateDef(TestTraining.Brain, 4, RunOf(300), new TrainingBestDef(4, 300, MapIds.Flat));

        TrainingStateDef.Record(previous, TestTraining.Brain, 5, new TrainingRunDef(250, 1, 0, MapIds.Flat, frontDistance: 120)).Best
            .ShouldBe(new TrainingBestDef(4, 300, MapIds.Flat, 120));
    }

    [Fact]
    public void Record_OnAnotherMap_StartsThatMapsFrontDistance()
    {
        var previous = TrainingStateDef.Record(null, TestTraining.Brain, 4, new TrainingRunDef(300, 1, 0, MapIds.Flat, frontDistance: 310));

        TrainingStateDef.Record(previous, TestTraining.Brain, 5, new TrainingRunDef(100, 1, 0, "hills", frontDistance: 90)).Best
            .FrontDistance.ShouldBe(90);
    }

    [Fact]
    public void Record_OnAnotherMap_StartsThatMapsBest()
    {
        var previous = TrainingStateDef.Record(null, TestTraining.Brain, 4, RunOf(300));

        TrainingStateDef.Record(previous, TestTraining.Brain, 5, RunOf(100, "hills")).Best
            .ShouldBe(new TrainingBestDef(5, 100, "hills"));
    }

    [Fact]
    public void RejectsABestFromAnUnfinishedGeneration()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new TrainingStateDef(TestTraining.Brain, 3, RunOf(1), new TrainingBestDef(4, 1, MapIds.Flat)));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new TrainingStateDef(TestTraining.Brain, 0, RunOf(1), new TrainingBestDef(1, 1, MapIds.Flat)));
    }

    [Fact]
    public void Best_RejectsInvalidValues()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingBestDef(0, 1, MapIds.Flat));
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingBestDef(1, -1, MapIds.Flat));
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingBestDef(1, double.NaN, MapIds.Flat));
        Should.Throw<ArgumentException>(() => new TrainingBestDef(1, 1, " "));
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingBestDef(1, 1, MapIds.Flat, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new TrainingBestDef(1, 1, MapIds.Flat, double.PositiveInfinity));
    }
}

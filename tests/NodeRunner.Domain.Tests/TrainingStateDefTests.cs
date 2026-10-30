namespace NodeRunner.Domain.Tests;

public sealed class TrainingStateDefTests
{
    private static TrainingRunDef RunOf(double distance, double? front = null, string mapId = MapIds.Flat) => new(distance, 1, 0, mapId, front);

    // Generation 4 ran 300 on Flat, with the front at previousFront; generation 5 then runs latest.
    // A tie keeps the earlier best; the front is the furthest on the map, whichever run holds the score.
    [Fact]
    public void Record_KeepsTheBestPerMap_AndTheFurthestFront()
    {
        TrainingStateDef.Record(null, TestTraining.Network, 1, RunOf(120)).Best.ShouldBe(new TrainingBestDef(1, 120, MapIds.Flat));
        foreach (var (previousFront, latest, expected) in new (double?, TrainingRunDef, TrainingBestDef)[]
        {
            (null, RunOf(250), new(4, 300, MapIds.Flat)),
            (null, RunOf(300), new(4, 300, MapIds.Flat)),
            (null, RunOf(350), new(5, 350, MapIds.Flat)),
            (310, RunOf(250, 400), new(4, 300, MapIds.Flat, 400)),
            (310, RunOf(350, 200), new(5, 350, MapIds.Flat, 310)),
            (null, RunOf(250, 120), new(4, 300, MapIds.Flat, 120)),
            (310, RunOf(100, 90, "hills"), new(5, 100, "hills", 90)),
        })
        {
            var previous = TrainingStateDef.Record(null, TestTraining.Network, 4, RunOf(300, previousFront));
            var state = TrainingStateDef.Record(previous, TestTraining.Network, 5, latest);

            (state.Generation, state.Latest, state.Best).ShouldBe((5, latest, expected), $"{previousFront} then {latest}");
        }
    }

    [Fact]
    public void BestOn_IsTheBestOnlyOnItsOwnMap_AndARunShowsItsFront()
    {
        var state = TestTraining.State(3, bestDistance: 50);

        state.BestOn(MapIds.Flat).ShouldBe(state.Best);
        state.BestOn("map-hills").ShouldBeNull();
        RunOf(120.5, 140).ShownDistance.ShouldBe(140);
        RunOf(120.5).ShownDistance.ShouldBe(120.5);
    }

    [Fact]
    public void Constructors_RefuseValuesOutOfRange()
    {
        Refuses.OutOfRange(() => new TrainingStateDef(TestTraining.Network, 3, RunOf(1), new TrainingBestDef(4, 1, MapIds.Flat)));
        Refuses.OutOfRange(() => new TrainingStateDef(TestTraining.Network, 0, RunOf(1), new TrainingBestDef(1, 1, MapIds.Flat)));
        Refuses.OutOfRange(() => new TrainingBestDef(0, 1, MapIds.Flat));
        Refuses.OutOfRange(() => new TrainingBestDef(1, -1, MapIds.Flat));
        Refuses.OutOfRange(() => new TrainingBestDef(1, double.NaN, MapIds.Flat));
        Refuses.Invalid(() => new TrainingBestDef(1, 1, " "));
        Refuses.OutOfRange(() => new TrainingBestDef(1, 1, MapIds.Flat, -1));
        Refuses.OutOfRange(() => new TrainingBestDef(1, 1, MapIds.Flat, double.PositiveInfinity));
        Refuses.OutOfRange(() => new TrainingRunDef(-1, 0, 0, MapIds.Flat));
        Refuses.OutOfRange(() => new TrainingRunDef(double.NaN, 0, 0, MapIds.Flat));
        Refuses.OutOfRange(() => new TrainingRunDef(0, double.PositiveInfinity, 0, MapIds.Flat));
        Refuses.OutOfRange(() => new TrainingRunDef(0, 0, -1, MapIds.Flat));
        Refuses.OutOfRange(() => new TrainingRunDef(0, 0, 0, MapIds.Flat, -1));
        Refuses.Invalid(() => new TrainingRunDef(0, 0, 0, " "));
        Refuses.OutOfRange(() => new TrainSettingsDef(TrainSettingsDef.MinShadows - 1, 10));
        Refuses.OutOfRange(() => new TrainSettingsDef(TrainSettingsDef.MaxShadows + 1, 10));
        Refuses.OutOfRange(() => new TrainSettingsDef(8, TrainSettingsDef.MinRunLengthSeconds - 1));
        Refuses.OutOfRange(() => new TrainSettingsDef(8, TrainSettingsDef.MaxRunLengthSeconds + 1));
        Refuses.OutOfRange(() => new TrainSettingsDef(8, 10, (RunLengthMode)2, null));
        Refuses.Invalid(() => new TrainSettingsDef(8, 10, RunLengthMode.Ranged, null));
    }

    [Fact]
    public void TrainSettings_DefaultToEightShadowsForTenSeconds_AndAcceptTheEnds()
    {
        (TrainSettingsDef.Default.Shadows, TrainSettingsDef.Default.RunLengthSeconds).ShouldBe((8, 10));
        Should.NotThrow(() => new TrainSettingsDef(TrainSettingsDef.MinShadows, TrainSettingsDef.MinRunLengthSeconds));
        Should.NotThrow(() => new TrainSettingsDef(TrainSettingsDef.MaxShadows, TrainSettingsDef.MaxRunLengthSeconds));
    }
}

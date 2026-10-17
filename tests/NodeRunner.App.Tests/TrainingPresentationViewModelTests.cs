using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class TrainingPresentationViewModelTests
{
    [Theory]
    [InlineData(true, 1310.0, 6, 13.1)]
    [InlineData(false, double.NaN, 5, null)]
    public void Update_ShowsTheRacingGenerationCountedFromOneAndTheBestMarker(bool isTrialActive, double bestShownDistance, int shownGeneration, double? bestMetres)
    {
        var presentation = new TrainingPresentationViewModel();
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        presentation.Update(5, 8, 1280, bestShownDistance, 840, 4, isTrialActive, [10.1, 11.2]);

        raised.ShouldBeTrue();
        presentation.BestShownDistance.ShouldBe(bestShownDistance);
        presentation.GenerationText.ShouldBe(UiText.Format("Generation {0}", shownGeneration));
        presentation.BestMarkerText.ShouldBe(bestMetres is { } metres ? UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(metres, 1))) : null);
    }

    [Theory]
    [InlineData(MapIds.Flat, 420.0, 4.2)]
    [InlineData(MapIds.Flat, null, null)]
    [InlineData("map-hills", 420.0, null)]
    public void Saved_ShowsTheSavedBestFrontOnThisMapOnly(string mapId, double? frontDistance, double? bestMetres)
    {
        var training = new TrainingStateDef(TestTraining.Network, 3, TestTraining.Run, new TrainingBestDef(2, 300, MapIds.Flat, frontDistance));

        var presentation = TrainingPresentationViewModel.Saved(training, mapId);

        presentation.BestShownDistance.ShouldBe(mapId == MapIds.Flat ? frontDistance ?? double.NaN : double.NaN);
        presentation.BestMarkerText.ShouldBe(bestMetres is { } metres ? UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(metres, 1))) : null);
    }

    [Fact]
    public void Source_RefreshesTheStateOnProgress_UntilDisposed()
    {
        var source = new FakeTrainingProgressSource
        {
            Generation = 7,
            ShadowCount = 8,
            BestFitness = 2130,
            BestShownDistance = 2190,
            BestGeneration = 7,
            MeanFitness = 10.5,
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);
        var raised = 0;
        presentation.PropertyChanged += (_, _) => raised++;

        source.Generation = 8;
        source.MeanFitness = 5.1;
        source.CompletedFitness = [9.4];
        source.RaiseProgressChanged();
        presentation.Dispose();
        presentation.Dispose();
        source.Generation = 9;
        source.RaiseProgressChanged();

        raised.ShouldBe(1);
        (presentation.Generation, presentation.ShadowCount, presentation.BestFitness, presentation.MeanFitness).ShouldBe((8, 8, 2130, 5.1));
        presentation.CompletedFitness.ShouldBe([9.4]);
        presentation.IsTrialActive.ShouldBeTrue();
        presentation.BestGeneration.ShouldBe(7, "The best outlives worse generations.");
        presentation.BestMarkerText.ShouldBe(UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(21.9, 1))));
        source.IsDisposed.ShouldBeTrue();
        source.ProgressChangedSubscriberCount.ShouldBe(0);
    }

    [Fact]
    public void Shadows_MarkFollowedLeaderAndPreviousBestSeparately()
    {
        var source = new FakeTrainingProgressSource
        {
            ShadowCount = 4,
            IsTrialActive = true,
            HasPreviousBest = true,
            ShadowDistances = [1.5, 3.2, double.NaN, 2.0],
        };
        var presentation = new TrainingPresentationViewModel(source);

        presentation.Shadows.ShouldBe(
        [
            new ShadowStanding(1, 1.5, IsFollowed: true, IsLeader: false, IsPreviousBest: true),
            new ShadowStanding(2, 3.2, IsFollowed: false, IsLeader: true, IsPreviousBest: false),
            new ShadowStanding(3, double.NaN, IsFollowed: false, IsLeader: false, IsPreviousBest: false),
            new ShadowStanding(4, 2.0, IsFollowed: false, IsLeader: false, IsPreviousBest: false),
        ]);
        presentation.FollowedShadow.ShouldBe(1);
    }

    [Fact]
    public void Follow_ChangesOnlyWhenThePlayerPicks()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 2, 3] };
        var presentation = new TrainingPresentationViewModel(source);

        presentation.Follow(3);
        source.ShadowDistances = [9, 2, 3];

        presentation.FollowedShadow.ShouldBe(3);
        presentation.Shadows.Single(shadow => shadow.IsLeader).Number.ShouldBe(1);
        presentation.Shadows.Single(shadow => shadow.IsFollowed).Number.ShouldBe(3);
        presentation.Shadows.ShouldAllBe(shadow => !shadow.IsPreviousBest);
        Should.Throw<ArgumentOutOfRangeException>(() => presentation.Follow(0));
    }

    [Fact]
    public void WithoutASource_ThereAreNoShadowsToFollowNorARun()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Shadows.ShouldBeEmpty();
        presentation.FollowedShadow.ShouldBe(0);
        presentation.RunProgress.ShouldBeNull();
        presentation.Strip.Cells.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => presentation.Follow(1));
    }

    [Fact]
    public void Strip_DrawsOnlyThePageItShows_AsThePlayerPagesSortsOrResizes_AndANewGenerationStartsOver()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 5, 3, 2, 4, 6, 9, 8, 7] };
        using var presentation = new TrainingPresentationViewModel(source);
        var drawn = new List<IReadOnlyList<int>> { source.Drawn };

        source.RaiseProgressChanged();
        presentation.ShowBetterShadows();
        drawn.Add(source.Drawn);
        presentation.ShowWorseShadows();
        drawn.Add(source.Drawn);
        presentation.SortShadows();
        drawn.Add(source.Drawn);
        var sortedCells = presentation.Strip.Cells.Select(cell => cell.Number).ToArray();
        presentation.StripPlaces = 4;
        drawn.Add(source.Drawn);
        source.Generation = 1;
        source.RaiseProgressChanged();
        drawn.Add(source.Drawn);

        drawn.ShouldBe([[0, 1, 2, 3, 4, 5], [0, 1, 2, 3, 4, 5], [3, 4, 5, 6, 7, 8], [6, 7, 8, 5, 1, 4], [6, 7], [0, 1]]);
        source.DrawOnlyCalls.ShouldBe(5, "Only a change in the drawn shadows tells the source.");
        sortedCells.ShouldBe([5, 2, 6, 9, 8, 7]);
    }

    [Fact]
    public void Strip_CapsTheFollowedCell_OnceItsRunTears()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 2, 3] };
        var presentation = new TrainingPresentationViewModel(source);
        presentation.Follow(2);

        presentation.Strip.Cells.ShouldAllBe(cell => !cell.IsCapped);
        source.FollowedCrashed = true;
        presentation.Strip.Cells.Where(cell => cell.IsCapped).Select(cell => cell.Number).ShouldBe([2], "A tear can come in any tick.");
    }

    [Theory]
    [InlineData(new double[0], -1)]
    [InlineData(new[] { double.NaN, -1.0, -0.5 }, 2)]
    [InlineData(new[] { 2.0, 2.0, 1.0 }, 0)]
    public void Leader_IsTheFurthestRunningShadow(double[] distances, int leader)
    {
        TrainingPresentationViewModel.Leader(distances).ShouldBe(leader);
    }

    [Theory]
    [InlineData(150, 600, 0.25)]
    [InlineData(700, 600, 1.0)]
    [InlineData(10, 0, null)]
    [InlineData(null, 0, null)]
    public void RunProgress_IsTheShareOfTheRunGone_OrNullWithoutARunWithALength(int? elapsed, int length, double? progress)
    {
        var source = new FakeTrainingProgressSource { FollowedRun = elapsed is { } ticks ? new RunClock(ticks, length) : null };

        var presentation = new TrainingPresentationViewModel(source);

        presentation.RunProgress.ShouldBe(progress);
    }

    [Fact]
    public void CrashText_ReadsCrashAndTheDistance()
    {
        TrainingPresentationViewModel.CrashText(342).ShouldBe(UiText.Format("Crash {0}", UiText.Format("{0} m", new FixedNumber(3.4, 1))));
    }

    private sealed class FakeTrainingProgressSource : ITrainingProgressSource
    {
        private Action? _progressChanged;

        public event Action? ProgressChanged
        {
            add
            {
                _progressChanged += value;
                ProgressChangedSubscriberCount++;
            }

            remove
            {
                _progressChanged -= value;
                ProgressChangedSubscriberCount--;
            }
        }

        public int ProgressChangedSubscriberCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public int Generation { get; set; }

        public int ShadowCount { get; set; }

        public double BestFitness { get; set; }

        public int BestGeneration { get; set; }

        public double BestShownDistance { get; set; } = double.NaN;

        public double MeanFitness { get; set; }

        public IReadOnlyList<double> CompletedFitness { get; set; } = [];

        public bool IsTrialActive { get; set; }

        public int FollowedShadow { get; set; }

        public bool HasPreviousBest { get; set; }

        public IReadOnlyList<double> ShadowDistances { get; set; } = [];

        public bool FollowedCrashed { get; set; }

        public RunClock? FollowedRun { get; set; }

        public IReadOnlyList<int> Drawn { get; private set; } = [];

        public int DrawOnlyCalls { get; private set; }

        public void Follow(int shadow)
        {
            FollowedShadow = shadow;
        }

        public void DrawOnly(IReadOnlyList<int> shadows)
        {
            Drawn = shadows;
            DrawOnlyCalls++;
        }

        public void RaiseProgressChanged()
        {
            _progressChanged?.Invoke();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}

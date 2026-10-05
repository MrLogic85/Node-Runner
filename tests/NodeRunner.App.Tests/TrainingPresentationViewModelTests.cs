using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class TrainingPresentationViewModelTests
{
    [Fact]
    public void Update_ExposesLearnerFacingTrainingState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 8, 1280, 1310, 840, 4, true, [10.1, 11.2]);

        presentation.Generation.ShouldBe(5);
        presentation.ShadowCount.ShouldBe(8);
        presentation.BestFitness.ShouldBe(1280);
        presentation.BestShownDistance.ShouldBe(1310);
        presentation.MeanFitness.ShouldBe(840);
        presentation.BestGeneration.ShouldBe(4);
        presentation.IsTrialActive.ShouldBeTrue();
        presentation.CompletedFitness.ShouldBe([10.1, 11.2]);
        presentation.GenerationText.ShouldBe(UiText.Format("Generation {0}", 6));
        presentation.BestMarkerText.ShouldBe(UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(13.1, 1))));
    }

    [Fact]
    public void Saved_ShowsTheSavedBestOnThisMap()
    {
        var training = new TrainingStateDef(TestTraining.Brain, 3, TestTraining.Run, new TrainingBestDef(2, 300, MapIds.Flat, frontDistance: 420));

        var presentation = TrainingPresentationViewModel.Saved(training, MapIds.Flat);

        presentation.BestShownDistance.ShouldBe(420);
        presentation.BestMarkerText.ShouldBe(UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(4.2, 1))));
    }

    [Theory]
    [InlineData(MapIds.Flat, null)]
    [InlineData("map-hills", 420.0)]
    public void Saved_HidesTheMarkerWithoutABestFrontOnThisMap(string mapId, double? frontDistance)
    {
        var training = new TrainingStateDef(TestTraining.Brain, 3, TestTraining.Run, new TrainingBestDef(2, 300, MapIds.Flat, frontDistance));

        var presentation = TrainingPresentationViewModel.Saved(training, mapId);

        presentation.BestShownDistance.ShouldBe(double.NaN);
        presentation.BestMarkerText.ShouldBeNull();
    }

    [Fact]
    public void Update_ExposesInactiveAndNoBestState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 8, double.NegativeInfinity, double.NaN, 0, 0, false, []);

        presentation.GenerationText.ShouldBe(UiText.Format("Generation {0}", 5));
        presentation.BestMarkerText.ShouldBeNull();
    }

    [Fact]
    public void Update_NotifiesSubscribers()
    {
        var presentation = new TrainingPresentationViewModel();
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        presentation.Update(1, 2, 0, 0, 0, 0, true, []);

        raised.ShouldBeTrue();
    }

    [Fact]
    public void SourceProgressChanged_RefreshesStateFromTrainingSource()
    {
        var source = new FakeTrainingProgressSource
        {
            Generation = 2,
            ShadowCount = 4,
            BestFitness = double.NegativeInfinity,
            MeanFitness = 0,
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        source.Generation = 3;
        source.BestFitness = 9.4;
        source.MeanFitness = 5.1;
        source.CompletedFitness = [9.4];
        source.RaiseProgressChanged();

        raised.ShouldBeTrue();
        presentation.Generation.ShouldBe(3);
        presentation.BestFitness.ShouldBe(9.4);
        presentation.MeanFitness.ShouldBe(5.1);
        presentation.CompletedFitness.ShouldBe([9.4]);
    }

    [Fact]
    public void BestGeneration_ComesFromTheSourceAndOutlivesWorseGenerations()
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

        presentation.BestGeneration.ShouldBe(7);
        presentation.BestMarkerText.ShouldBe(UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(21.9, 1))));

        source.Generation = 8;
        source.RaiseProgressChanged();

        presentation.Generation.ShouldBe(8);
        presentation.BestGeneration.ShouldBe(7);
        presentation.BestMarkerText.ShouldBe(UiText.Format("Best {0}", UiText.Format("{0} m", new FixedNumber(21.9, 1))));
    }

    [Fact]
    public void Dispose_UnsubscribesAndDisposesSource()
    {
        var source = new FakeTrainingProgressSource
        {
            Generation = 1,
            ShadowCount = 2,
            BestFitness = 4,
            MeanFitness = 3,
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        presentation.Dispose();
        source.Generation = 2;
        source.RaiseProgressChanged();

        source.IsDisposed.ShouldBeTrue();
        source.ProgressChangedSubscriberCount.ShouldBe(0);
        raised.ShouldBeFalse();
        presentation.Generation.ShouldBe(1);
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
        Should.Throw<ArgumentOutOfRangeException>(() => presentation.Follow(0));
    }

    [Fact]
    public void Strip_ShowsTheSourcesShadows_AndSortsThemOnRequest()
    {
        var source = new FakeTrainingProgressSource
        {
            IsTrialActive = true,
            BestFitness = double.NegativeInfinity,
            ShadowDistances = [1, 5, 3, 2, 4, 6, 9, 8, 7],
        };
        var presentation = new TrainingPresentationViewModel(source);

        presentation.Strip.Cells.Select(cell => cell.Number).ShouldBe([6, 5, 4, 3, 2, 1]);
        presentation.SortShadows();
        presentation.Strip.Cells.Select(cell => cell.Number).ShouldBe([5, 2, 6, 9, 8, 7]);
        presentation.ShowWorseShadows();
        presentation.Strip.Trailing.ShouldBe(ShadowStripTrailing.Better);
        presentation.ShowBetterShadows();
        presentation.Strip.Trailing.ShouldBe(ShadowStripTrailing.Sort);
    }

    [Fact]
    public void Training_DrawsEveryShadow_WhileTheyAllFitOnTheStrip()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 2, 3] };

        using var presentation = new TrainingPresentationViewModel(source);

        source.Drawn.ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Training_DrawsOnlyTheStripsPage_AsThePlayerPagesSortsOrResizes()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 5, 3, 2, 4, 6, 9, 8, 7] };
        using var presentation = new TrainingPresentationViewModel(source);
        source.Drawn.ShouldBe([0, 1, 2, 3, 4, 5]);

        presentation.ShowWorseShadows();
        source.Drawn.ShouldBe([3, 4, 5, 6, 7, 8]);

        presentation.ShowBetterShadows();
        source.Drawn.ShouldBe([0, 1, 2, 3, 4, 5]);

        presentation.SortShadows();
        source.Drawn.ShouldBe([6, 7, 8, 5, 1, 4]);

        presentation.StripPlaces = 4;
        source.Drawn.ShouldBe([6, 7]);
    }

    [Fact]
    public void ANewGeneration_DrawsTheFirstPageAgain()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 5, 3, 2, 4, 6, 9, 8, 7] };
        using var presentation = new TrainingPresentationViewModel(source);
        presentation.ShowWorseShadows();

        source.Generation = 1;
        source.RaiseProgressChanged();

        source.Drawn.ShouldBe([0, 1, 2, 3, 4, 5]);
    }

    [Fact]
    public void Training_TellsTheSourceOnlyWhenTheDrawnShadowsChange()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [1, 5, 3, 2, 4, 6, 9, 8, 7] };
        using var presentation = new TrainingPresentationViewModel(source);

        source.RaiseProgressChanged();
        presentation.ShowBetterShadows();

        source.DrawOnlyCalls.ShouldBe(1);
    }

    [Fact]
    public void FreshGenerationZero_FollowsShadowOneWithoutAPreviousBest()
    {
        var source = new FakeTrainingProgressSource { ShadowDistances = [0, 0] };
        var presentation = new TrainingPresentationViewModel(source);

        presentation.FollowedShadow.ShouldBe(1);
        presentation.Shadows.ShouldAllBe(shadow => !shadow.IsPreviousBest);
    }

    [Theory]
    [InlineData(new double[0], -1)]
    [InlineData(new[] { double.NaN, double.NaN }, -1)]
    [InlineData(new[] { double.NaN, -1.0, -0.5 }, 2)]
    [InlineData(new[] { 2.0, 2.0, 1.0 }, 0)]
    public void Leader_IsTheFurthestRunningShadow(double[] distances, int leader)
    {
        TrainingPresentationViewModel.Leader(distances).ShouldBe(leader);
    }

    [Fact]
    public void WithoutASource_ThereAreNoShadowsToFollow()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Shadows.ShouldBeEmpty();
        presentation.FollowedShadow.ShouldBe(0);
        Should.Throw<InvalidOperationException>(() => presentation.Follow(1));
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

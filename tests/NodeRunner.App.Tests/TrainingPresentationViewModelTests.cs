using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

public sealed class TrainingPresentationViewModelTests
{
    [Fact]
    public void Update_ExposesLearnerFacingTrainingState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 8, 1280, 840, 4, true, [10.1, 11.2]);

        presentation.Generation.ShouldBe(5);
        presentation.ShadowCount.ShouldBe(8);
        presentation.BestFitness.ShouldBe(1280);
        presentation.MeanFitness.ShouldBe(840);
        presentation.BestGeneration.ShouldBe(4);
        presentation.IsTrialActive.ShouldBeTrue();
        presentation.CompletedFitness.ShouldBe([10.1, 11.2]);
        presentation.GenerationText.ShouldBe("Generation 5 · 8 shadows racing");
        presentation.BestFitnessText.ShouldBe("Best: 12.8 m (gen 4)");
        presentation.MeanFitnessText.ShouldBe("Mean: 8.4 m");
    }

    [Fact]
    public void Update_ExposesInactiveAndNoBestState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 8, double.NegativeInfinity, 0, 0, false, []);

        presentation.GenerationText.ShouldBe("Generation 5 · Training finished");
        presentation.BestFitnessText.ShouldBe("Best: —");
        presentation.MeanFitnessText.ShouldBe("Mean: 0.0 m");
    }

    [Fact]
    public void Update_NotifiesSubscribers()
    {
        var presentation = new TrainingPresentationViewModel();
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        presentation.Update(1, 2, 0, 0, 0, true, []);

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
            BestGeneration = 7,
            MeanFitness = 10.5,
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);

        presentation.BestGeneration.ShouldBe(7);
        presentation.BestFitnessText.ShouldBe("Best: 21.3 m (gen 7)");

        source.Generation = 8;
        source.RaiseProgressChanged();

        presentation.Generation.ShouldBe(8);
        presentation.BestGeneration.ShouldBe(7);
        presentation.BestFitnessText.ShouldBe("Best: 21.3 m (gen 7)");
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

        public double MeanFitness { get; set; }

        public IReadOnlyList<double> CompletedFitness { get; set; } = [];

        public bool IsTrialActive { get; set; }

        public int FollowedShadow { get; set; }

        public bool HasPreviousBest { get; set; }

        public IReadOnlyList<double> ShadowDistances { get; set; } = [];

        public void Follow(int shadow)
        {
            FollowedShadow = shadow;
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

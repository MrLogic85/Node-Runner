using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

public sealed class TrainingPresentationViewModelTests
{
    [Fact]
    public void Update_ExposesLearnerFacingTrainingState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 3, 8, 12.8, 8.4, "Quick", 4, true, 2, [10.1, 11.2]);

        presentation.Generation.ShouldBe(5);
        presentation.Candidate.ShouldBe(3);
        presentation.Population.ShouldBe(8);
        presentation.BestFitness.ShouldBe(12.8);
        presentation.MeanFitness.ShouldBe(8.4);
        presentation.Profile.ShouldBe("Quick");
        presentation.BestGeneration.ShouldBe(4);
        presentation.IsTrialActive.ShouldBeTrue();
        presentation.CompletedCandidateCount.ShouldBe(2);
        presentation.CompletedFitness.ShouldBe([10.1, 11.2]);
        presentation.GenerationText.ShouldBe("Generation 5 · try 3 of 8");
        presentation.BestFitnessText.ShouldBe("Best: 12.8 (gen 4)");
        presentation.MeanFitnessText.ShouldBe("Mean: 8.4");
    }

    [Fact]
    public void Update_ExposesInactiveAndNoBestState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 0, 8, double.NegativeInfinity, 0, "Quick", 0, false, 0, []);

        presentation.GenerationText.ShouldBe("Generation 5 · session complete");
        presentation.BestFitnessText.ShouldBe("Best: —");
        presentation.MeanFitnessText.ShouldBe("Mean: 0.0");
    }

    [Fact]
    public void Update_NotifiesSubscribers()
    {
        var presentation = new TrainingPresentationViewModel();
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        presentation.Update(1, 1, 2, 0, 0, "Standard");

        raised.ShouldBeTrue();
    }

    [Fact]
    public void SourceProgressChanged_RefreshesStateFromTrainingSource()
    {
        var source = new FakeTrainingProgressSource
        {
            Generation = 2,
            CurrentCandidate = 1,
            PopulationSize = 4,
            BestFitness = double.NegativeInfinity,
            MeanFitness = 0,
            Profile = "Standard",
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        source.Generation = 3;
        source.CurrentCandidate = 2;
        source.BestFitness = 9.4;
        source.MeanFitness = 5.1;
        source.CompletedCandidateCount = 1;
        source.CompletedFitness = [9.4];
        source.RaiseProgressChanged();

        raised.ShouldBeTrue();
        presentation.Generation.ShouldBe(3);
        presentation.Candidate.ShouldBe(2);
        presentation.BestFitness.ShouldBe(9.4);
        presentation.MeanFitness.ShouldBe(5.1);
        presentation.CompletedCandidateCount.ShouldBe(1);
        presentation.CompletedFitness.ShouldBe([9.4]);
    }

    [Fact]
    public void NewBestFound_RecordsBestGenerationFromSource()
    {
        var source = new FakeTrainingProgressSource
        {
            Generation = 7,
            CurrentCandidate = 4,
            PopulationSize = 8,
            BestFitness = 21.3,
            MeanFitness = 10.5,
            Profile = "Deep",
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);

        source.RaiseNewBestFound();

        presentation.BestGeneration.ShouldBe(7);
        presentation.BestFitnessText.ShouldBe("Best: 21.3 (gen 7)");

        source.Generation = 8;
        source.CurrentCandidate = 1;
        source.RaiseProgressChanged();

        presentation.Generation.ShouldBe(8);
        presentation.BestGeneration.ShouldBe(7);
        presentation.BestFitnessText.ShouldBe("Best: 21.3 (gen 7)");
    }

    [Fact]
    public void Dispose_UnsubscribesAndDisposesSource()
    {
        var source = new FakeTrainingProgressSource
        {
            Generation = 1,
            CurrentCandidate = 1,
            PopulationSize = 2,
            BestFitness = 4,
            MeanFitness = 3,
            Profile = "Standard",
            IsTrialActive = true,
        };
        var presentation = new TrainingPresentationViewModel(source);
        var raised = false;
        presentation.PropertyChanged += (_, _) => raised = true;

        presentation.Dispose();
        source.Generation = 2;
        source.RaiseProgressChanged();
        source.RaiseNewBestFound();

        source.IsDisposed.ShouldBeTrue();
        source.ProgressChangedSubscriberCount.ShouldBe(0);
        source.NewBestFoundSubscriberCount.ShouldBe(0);
        raised.ShouldBeFalse();
        presentation.Generation.ShouldBe(1);
    }

    private sealed class FakeTrainingProgressSource : ITrainingProgressSource
    {
        private Action? _progressChanged;
        private Action? _newBestFound;

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

        public event Action? NewBestFound
        {
            add
            {
                _newBestFound += value;
                NewBestFoundSubscriberCount++;
            }

            remove
            {
                _newBestFound -= value;
                NewBestFoundSubscriberCount--;
            }
        }

        public int ProgressChangedSubscriberCount { get; private set; }

        public int NewBestFoundSubscriberCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public int Generation { get; set; }

        public int CurrentCandidate { get; set; }

        public int PopulationSize { get; set; }

        public double BestFitness { get; set; }

        public double MeanFitness { get; set; }

        public IReadOnlyList<double> CompletedFitness { get; set; } = [];

        public int CompletedCandidateCount { get; set; }

        public bool IsTrialActive { get; set; }

        public string Profile { get; set; } = "Standard";

        public void RaiseProgressChanged()
        {
            _progressChanged?.Invoke();
        }

        public void RaiseNewBestFound()
        {
            _newBestFound?.Invoke();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}

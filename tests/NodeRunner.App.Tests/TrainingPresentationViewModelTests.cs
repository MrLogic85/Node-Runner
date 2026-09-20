using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

public sealed class TrainingPresentationViewModelTests
{
    [Fact]
    public void Update_ExposesLearnerFacingTrainingState()
    {
        var presentation = new TrainingPresentationViewModel();

        presentation.Update(5, 3, 8, 12.8, 8.4, "Quick");

        presentation.Generation.ShouldBe(5);
        presentation.Candidate.ShouldBe(3);
        presentation.Population.ShouldBe(8);
        presentation.BestFitness.ShouldBe(12.8);
        presentation.MeanFitness.ShouldBe(8.4);
        presentation.Profile.ShouldBe("Quick");
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
}

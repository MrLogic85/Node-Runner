using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class UnlockProgressPresentationViewModelTests
{
    [Fact]
    public void Update_WhenLocked_ShowsBoundedProgressTowardThreshold()
    {
        var viewModel = new UnlockProgressPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(new ProgressionDef(), bestFitness: 12.5, threshold: 50);

        viewModel.IsUnlocked.ShouldBeFalse();
        viewModel.Title.ShouldBe("Next unlock");
        viewModel.Detail.ShouldBe("Reach 50.0 m to unlock one extra core slot · 12.5/50.0 m");
        viewModel.Progress.ShouldBe(0.25);
        notifications.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenUnlocked_ShowsEarnedGeneration()
    {
        var viewModel = new UnlockProgressPresentationViewModel();

        viewModel.Update(new ProgressionDef(true, 7), bestFitness: 4, threshold: 50);

        viewModel.IsUnlocked.ShouldBeTrue();
        viewModel.Title.ShouldBe("Extra core unlocked");
        viewModel.Detail.ShouldBe("Extra core slot available · earned generation 7");
        viewModel.Progress.ShouldBe(1);
    }

    [Fact]
    public void Update_WithSameState_DoesNotNotifyAgain()
    {
        var viewModel = new UnlockProgressPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(new ProgressionDef(), bestFitness: double.NegativeInfinity, threshold: 50);
        viewModel.Update(new ProgressionDef(), bestFitness: 0, threshold: 50);

        notifications.ShouldBe(1);
    }
}

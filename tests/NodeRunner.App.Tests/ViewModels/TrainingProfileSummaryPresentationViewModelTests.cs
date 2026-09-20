using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class TrainingProfileSummaryPresentationViewModelTests
{
    [Fact]
    public void Update_FormatsLearnerFacingProfileSummary()
    {
        var viewModel = new TrainingProfileSummaryPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(16, 20, 0.06, "blended genes");

        viewModel.Detail.ShouldBe("16 candidates · 20s · 6% mutation · blended genes");
        notifications.ShouldBe(1);
    }

    [Fact]
    public void Update_WithSameSummary_DoesNotNotifyAgain()
    {
        var viewModel = new TrainingProfileSummaryPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(8, 10, 0.10, "uniform genes");

        notifications.ShouldBe(0);
    }
}

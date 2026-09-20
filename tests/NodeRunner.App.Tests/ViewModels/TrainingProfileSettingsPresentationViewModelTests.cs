using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class TrainingProfileSettingsPresentationViewModelTests
{
    [Fact]
    public void Update_StoresProfileOptionsAndSelection()
    {
        var viewModel = new TrainingProfileSettingsPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(
            new[]
            {
                new TrainingProfileOptionPresentation("Quick", "4 candidates · 3s · 20% mutation · uniform genes"),
                new TrainingProfileOptionPresentation("Standard", "8 candidates · 10s · 10% mutation · uniform genes"),
            },
            1);

        viewModel.SelectedIndex.ShouldBe(1);
        viewModel.Options.Select(option => option.Name).ShouldBe(new[] { "Quick", "Standard" });
        notifications.ShouldBe(1);
    }

    [Fact]
    public void Update_WithSameOptions_DoesNotNotifyAgain()
    {
        var viewModel = new TrainingProfileSettingsPresentationViewModel();
        viewModel.Update(
            new[] { new TrainingProfileOptionPresentation("Standard", "8 candidates") },
            0);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(
            new[] { new TrainingProfileOptionPresentation("Standard", "8 candidates") },
            0);

        notifications.ShouldBe(0);
    }
}

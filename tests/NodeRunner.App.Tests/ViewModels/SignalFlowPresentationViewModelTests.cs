using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    [Fact]
    public void Update_WithALiveBrain_NotesEachStage()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(readings: 4, pistons: 2, distance: 4225);

        viewModel.SensesNote.ShouldBe(Readings(4));
        viewModel.OutputsNote.ShouldBe(Motors(2));
        viewModel.DistanceNote.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        changed.ShouldBe([null], "The counts changed, so every note is new.");
    }

    [Fact]
    public void Update_WithOneOfEach_UsesTheSingular()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(1, 1, 0);

        viewModel.SensesNote.ShouldBe(Readings(1));
        viewModel.OutputsNote.ShouldBe(Motors(1));
        viewModel.DistanceNote.ShouldBe(UiText.Format("{0} m", new FixedNumber(0, 1)));
    }

    [Fact]
    public void Update_WithANegativeCount_Throws()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.Update(-1, 0, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.Update(0, -1, 0));
    }

    [Fact]
    public void Update_WithNoReadings_LeavesTheNotesEmpty()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(0, 0, double.NaN);

        viewModel.SensesNote.ShouldBeNull();
        viewModel.OutputsNote.ShouldBeNull();
        viewModel.DistanceNote.ShouldBeNull();
    }

    [Fact]
    public void Update_WithTheSameNotes_DoesNotNotifyAgain()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(0, 1, 301);

        notifications.ShouldBe(0);
    }

    [Fact]
    public void Update_WhenOnlyTheDistanceMoves_NotifiesOnce()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 1, 400);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceNote)]);
        viewModel.DistanceNote.ShouldBe(UiText.Format("{0} m", new FixedNumber(4, 1)));
    }

    [Fact]
    public void Update_WhenOnlyTheOutputsCountChanges_NotifiesEveryNote()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 2, 300);

        viewModel.OutputsNote.ShouldBe(Motors(2));
        changed.ShouldBe([null], "The Outputs count changed, so the screen must rebuild the count notes.");
    }

    private static UiText Readings(int count) => UiText.Counted("{0} reading", "{0} readings", count);

    private static UiText Motors(int count) => UiText.Counted("{0} motor", "{0} motors", count);
}

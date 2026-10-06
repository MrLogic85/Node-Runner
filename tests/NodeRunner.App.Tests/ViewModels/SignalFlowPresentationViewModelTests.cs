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
        viewModel.OutputsNote.ShouldBe(Pistons(2));
        viewModel.DistanceNote.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        changed.ShouldBe([null], "The counts changed, so every note is new.");
    }

    [Fact]
    public void Update_WithOneOfEach_UsesTheSingular()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(1, 1, 0);

        viewModel.SensesNote.ShouldBe(Readings(1));
        viewModel.OutputsNote.ShouldBe(Pistons(1));
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

        viewModel.OutputsNote.ShouldBe(Pistons(2));
        changed.ShouldBe([null], "The Outputs count changed, so the screen must rebuild the count notes.");
    }

    [Fact]
    public void Update_WithTimeLeft_ShowsWholeSecondsRoundedUp()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(0, 1, 0, secondsLeft: 20);
        viewModel.TimeLeft.ShouldBe(Seconds(20), "A run starts at its full length.");

        viewModel.Update(0, 1, 0, secondsLeft: 19.98);
        viewModel.TimeLeft.ShouldBe(Seconds(20), "Part of a second still counts as that second.");

        viewModel.Update(0, 1, 0, secondsLeft: 0.02);
        viewModel.TimeLeft.ShouldBe(Seconds(1), "The last second shows 1 s, never 0 s.");
    }

    [Fact]
    public void Update_WhenTheNextRunStarts_StartsAgainAtTheFullLength()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300, secondsLeft: 0.5);

        viewModel.Update(0, 1, 0, secondsLeft: 20);

        viewModel.TimeLeft.ShouldBe(Seconds(20));
    }

    [Fact]
    public void Update_WithNoTimeLimit_ShowsNoTimeLeft()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300, secondsLeft: 4);

        viewModel.Update(0, 1, 300);

        viewModel.TimeLeft.ShouldBeNull();
    }

    [Fact]
    public void Update_WithNegativeTimeLeft_Throws()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.Update(0, 0, 0, secondsLeft: -1));
    }

    [Fact]
    public void Update_WhenOnlyTheTimeLeftTicksDown_NotifiesItAlone()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300, secondsLeft: 5);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 1, 300, secondsLeft: 4.5);
        viewModel.Update(0, 1, 300, secondsLeft: 3.9);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.TimeLeft)], "Within the same whole second nothing changes.");
    }

    [Fact]
    public void Update_WhenTheDistanceAndTimeLeftMove_NamesBoth()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300, secondsLeft: 5);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 1, 400, secondsLeft: 4);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceNote), nameof(SignalFlowPresentationViewModel.TimeLeft)]);
    }

    private static UiText Readings(int count) => UiText.Counted("{0} reading", "{0} readings", count);

    private static UiText Pistons(int count) => UiText.Counted("{0} piston", "{0} pistons", count);

    private static UiText Seconds(int count) => UiText.Format("{0} s", count);
}

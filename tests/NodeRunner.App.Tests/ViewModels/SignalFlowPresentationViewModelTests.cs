using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    [Fact]
    public void Update_WithALiveNetwork_NotesEachStage()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(readings: 4, parts: 2, distance: 4225);

        viewModel.InputsNote.ShouldBe(Readings(4));
        viewModel.OutputsNote.ShouldBe(Parts(2));
        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.ShouldBe([null], "The counts changed, so every note is new.");
    }

    [Fact]
    public void Update_WithOneOfEach_UsesTheSingular()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(1, 1, 0);

        viewModel.InputsNote.ShouldBe(Readings(1));
        viewModel.OutputsNote.ShouldBe(Parts(1));
        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(0, 1)));
    }

    [Fact]
    public void Update_AfterATear_ReadsWhereTheDistanceFroze()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(0, 1, 340, crashed: true);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("Crash {0}", UiText.Format("{0} m", new FixedNumber(3.4, 1))));
        viewModel.DistanceTorn.ShouldBeTrue();
    }

    [Fact]
    public void Update_WithNoTry_HasNoReadout()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 340, crashed: true);

        viewModel.Update(0, 1, double.NaN, crashed: true);

        viewModel.DistanceReadout.ShouldBeNull("No try runs.");
        viewModel.DistanceTorn.ShouldBeFalse("There is nothing to show in danger.");
    }

    [Fact]
    public void Update_WithALiveDistance_ReadsTheDistanceWithoutACrash()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(readings: 2, parts: 1, distance: 1240);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(12.4, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
    }

    [Fact]
    public void Update_WhenANewTryFollowsATear_ReadsLiveAgain()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 340, crashed: true);

        viewModel.Update(0, 1, 100);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(1, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
    }

    [Fact]
    public void Update_WhenTheRunTearsAtTheSameDistance_NotifiesThatOnlyTheDistanceChanged()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 340);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 1, 340, crashed: true);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
        viewModel.DistanceTorn.ShouldBeTrue();
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

        viewModel.InputsNote.ShouldBeNull();
        viewModel.OutputsNote.ShouldBeNull();
        viewModel.DistanceReadout.ShouldBeNull();
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

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(4, 1)));
    }

    [Fact]
    public void Update_WhenOnlyTheOutputsCountChanges_NotifiesEveryNote()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(0, 1, 300);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 2, 300);

        viewModel.OutputsNote.ShouldBe(Parts(2));
        changed.ShouldBe([null], "The Outputs count changed, so the screen must rebuild the count notes.");
    }

    private static UiText Readings(int count) => UiText.Counted("{0} reading", "{0} readings", count);

    private static UiText Parts(int count) => UiText.Counted("{0} part", "{0} parts", count);
}

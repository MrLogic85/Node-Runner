using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    // The distance changes every tick and the counts rarely, so a distance-only change says so and
    // a count change rebuilds every note.
    [Fact]
    public void Update_NotesEachStage_AndNotifiesOnlyWhatChanged()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, 0, double.NaN);

        viewModel.InputsNote.ShouldBeNull();
        viewModel.OutputsNote.ShouldBeNull();
        viewModel.DistanceReadout.ShouldBeNull();
        changed.ShouldBeEmpty();

        viewModel.Update(readings: 4, parts: 2, distance: 4225);

        viewModel.InputsNote.ShouldBe(Readings(4));
        viewModel.OutputsNote.ShouldBe(Parts(2));
        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.ShouldBe([null]);

        viewModel.Update(4, 2, 4226);
        viewModel.Update(4, 2, 340);

        changed.ShouldBe([null, nameof(SignalFlowPresentationViewModel.DistanceReadout)]);

        viewModel.Update(4, 2, 340, crashed: true);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("Crash {0}", UiText.Format("{0} m", new FixedNumber(3.4, 1))));
        viewModel.DistanceTorn.ShouldBeTrue();
        changed.ShouldBe([null, nameof(SignalFlowPresentationViewModel.DistanceReadout), nameof(SignalFlowPresentationViewModel.DistanceReadout)]);

        viewModel.Update(4, 2, double.NaN, crashed: true);

        viewModel.DistanceReadout.ShouldBeNull("No try runs.");
        viewModel.DistanceTorn.ShouldBeFalse("There is nothing to show in danger.");

        viewModel.Update(4, 2, 340, crashed: true);
        viewModel.Update(4, 2, 100);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(1, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.Clear();

        viewModel.Update(1, 2, 100);
        viewModel.Update(1, 1, 100);

        viewModel.InputsNote.ShouldBe(Readings(1));
        viewModel.OutputsNote.ShouldBe(Parts(1));
        changed.ShouldBe([null, null]);
    }

    [Fact]
    public void Update_WithoutAListener_StillUpdates_AndWithANegativeCountThrows()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(1, 1, 100);
        viewModel.InputsNote.ShouldBe(Readings(1));

        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.Update(-1, 0, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.Update(0, -1, 0));
    }

    private static UiText Readings(int count) => UiText.Counted("{0} reading", "{0} readings", count);

    private static UiText Parts(int count) => UiText.Counted("{0} part", "{0} parts", count);
}

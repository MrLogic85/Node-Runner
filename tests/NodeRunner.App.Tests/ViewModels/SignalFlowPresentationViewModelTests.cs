using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    [Fact]
    public void Update_WithLiveReadings_NotesEachStage()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(
            [
                new SensorReading("Accelerometer", 1, "along", 0.25),
                new SensorReading("Accelerometer", 1, "across", -0.50),
                new SensorReading(MotorReading.PistonKind, 1, "length", 0.75),
                new SensorReading(MotorReading.PistonKind, 1, "speed", -2.0),
            ],
            [new MotorReading(MotorReading.PistonKind, 1, -0.6, 12.4), new MotorReading(MotorReading.PistonKind, 2, 0.2, 3.1)],
            distance: 4225);

        viewModel.SensesNote.ShouldBe(Readings(4));
        viewModel.OutputsNote.ShouldBe(Pistons(2));
        viewModel.DistanceNote.ShouldBe("42.3 m");
        changed.ShouldBe([null], "The counts changed, so every note is new.");
    }

    [Fact]
    public void Update_WithOneOfEach_UsesTheSingular()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update([new SensorReading("Accelerometer", 1, "across", 0.1)], [new MotorReading(MotorReading.PistonKind, 1, 0.2, 1)], 0);

        viewModel.SensesNote.ShouldBe(Readings(1));
        viewModel.OutputsNote.ShouldBe(Pistons(1));
        viewModel.DistanceNote.ShouldBe("0.0 m");
    }

    [Fact]
    public void Update_CountsDrivenParts_SoAPistonsTwoOutputsAreOnePiston()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(
            [],
            [
                new MotorReading(MotorReading.PistonKind, 1, 0.5, 100),
                new MotorReading(MotorReading.PistonKind, 1, 0.1, 100),
            ],
            0);

        viewModel.OutputsNote.ShouldBe(Pistons(1));
    }

    [Fact]
    public void Update_WithNoReadings_LeavesTheNotesEmpty()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update([], [], double.NaN);

        viewModel.SensesNote.ShouldBeNull();
        viewModel.OutputsNote.ShouldBeNull();
        viewModel.DistanceNote.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WithTheSameNotes_DoesNotNotifyAgain()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update([], [new MotorReading(MotorReading.PistonKind, 1, 0.2, 1)], 300);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update([], [new MotorReading(MotorReading.PistonKind, 1, -0.4, 2)], 301);

        notifications.ShouldBe(0);
    }

    [Fact]
    public void Update_WhenOnlyTheDistanceMoves_NotifiesOnce()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update([], [new MotorReading(MotorReading.PistonKind, 1, 0.2, 1)], 300);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update([], [new MotorReading(MotorReading.PistonKind, 1, 0.2, 1)], 400);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceNote)]);
        viewModel.DistanceNote.ShouldBe("4.0 m");
    }

    [Fact]
    public void Update_WhenOnlyTheOutputsCountChanges_NotifiesEveryNote()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update([], [new MotorReading(MotorReading.PistonKind, 1, 0.2, 1)], 300);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(
            [],
            [new MotorReading(MotorReading.PistonKind, 1, 0.2, 1), new MotorReading(MotorReading.PistonKind, 2, 0.2, 1)],
            300);

        viewModel.OutputsNote.ShouldBe(Pistons(2));
        changed.ShouldBe([null], "The Outputs count changed, so the screen must rebuild the count notes.");
    }

    private static UiText Readings(int count) => UiText.Counted("{0} reading", "{0} readings", count);

    private static UiText Pistons(int count) => UiText.Counted("{0} piston", "{0} pistons", count);
}

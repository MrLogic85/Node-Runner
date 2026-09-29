using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    [Fact]
    public void Update_WithLiveReadings_NotesEachStage()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(
            [
                new SensorReading("Core", 1, "Ray down", 0.25),
                new SensorReading("Core", 1, "Pitch", -0.50),
                new SensorReading("Motor relation", 1, "angle", 0.75),
                new SensorReading("Motor relation", 1, "angular velocity", -2.0),
            ],
            [new MotorReading(1, -0.6, 12.4), new MotorReading(2, 0.2, 3.1)],
            distance: 42.25);

        viewModel.SensesNote.ShouldBe("4 readings");
        viewModel.OutputsNote.ShouldBe("2 motors");
        viewModel.DistanceNote.ShouldBe("42.3 m");
        notifications.ShouldBe(1);
    }

    [Fact]
    public void Update_WithOneOfEach_UsesTheSingular()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update([new SensorReading("Core", 1, "Pitch", 0.1)], [new MotorReading(1, 0.2, 1)], 0);

        viewModel.SensesNote.ShouldBe("1 reading");
        viewModel.OutputsNote.ShouldBe("1 motor");
        viewModel.DistanceNote.ShouldBe("0.0 m");
    }

    [Fact]
    public void Update_WithNoReadings_LeavesTheNotesEmpty()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update([], [], double.NaN);

        viewModel.SensesNote.ShouldBeEmpty();
        viewModel.OutputsNote.ShouldBeEmpty();
        viewModel.DistanceNote.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WithTheSameNotes_DoesNotNotifyAgain()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update([], [new MotorReading(1, 0.2, 1)], 3);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update([], [new MotorReading(1, -0.4, 2)], 3.01);

        notifications.ShouldBe(0);
    }

    [Fact]
    public void Update_WhenOnlyTheDistanceMoves_NotifiesOnce()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update([], [new MotorReading(1, 0.2, 1)], 3);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update([], [new MotorReading(1, 0.2, 1)], 4);

        notifications.ShouldBe(1);
        viewModel.DistanceNote.ShouldBe("4.0 m");
    }
}

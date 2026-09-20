using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    [Fact]
    public void Update_WithLiveReadings_FormatsFourStageSummaries()
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
            bestFitness: 42.25,
            meanFitness: 8.5);

        viewModel.SeesSummary.ShouldBe("4 live sensor values");
        viewModel.DecidesSummary.ShouldBe("Brain maps 4 inputs to 2 motor targets");
        viewModel.TwistsSummary.ShouldBe("2 live motor targets");
        viewModel.ScoresSummary.ShouldBe("Best 42.3 m · mean 8.5 m");
        viewModel.SensorCount.ShouldBe(4);
        viewModel.MotorCount.ShouldBe(2);
        viewModel.SensorRows.Select(row => row.Label).ShouldBe(["Core 1 Ray down", "Core 1 Pitch", "Motor relation 1 angle"]);
        viewModel.SensorRows.Select(row => row.ValueText).ShouldBe(["0.25", "-0.50", "0.75"]);
        viewModel.SensorRows.Select(row => row.Fill).ShouldBe([0.25, 0.5, 0.75]);
        viewModel.MotorRows.Select(row => row.Label).ShouldBe(["Motor relation 1", "Motor relation 2"]);
        viewModel.MotorRows.Select(row => row.ValueText).ShouldBe(["-0.60", "0.20"]);
        viewModel.MotorRows.Select(row => row.Fill).ShouldBe([0.6, 0.2]);
        notifications.ShouldBe(1);
    }

    [Fact]
    public void Update_WithNoReadings_ShowsWaitingState()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update([], [], double.NegativeInfinity, 0);

        viewModel.SensorRows.ShouldBeEmpty();
        viewModel.MotorRows.ShouldBeEmpty();
        viewModel.SensorCount.ShouldBe(0);
        viewModel.MotorCount.ShouldBe(0);
        viewModel.SeesSummary.ShouldBe("No sensors are active yet");
        viewModel.DecidesSummary.ShouldBe("Brain waits for a complete body");
        viewModel.TwistsSummary.ShouldBe("No motor relations yet");
        viewModel.ScoresSummary.ShouldBe("Mean 0.0 m · best pending");
    }
}

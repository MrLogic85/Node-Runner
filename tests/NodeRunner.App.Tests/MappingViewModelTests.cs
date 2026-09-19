using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests;

public sealed class MappingViewModelTests
{
    [Fact]
    public void Constructor_BeforeUpdate_ShowsEmptyState()
    {
        var mapping = new MappingViewModel();

        mapping.SensorsText.ShouldBe("No sensors yet.");
        mapping.OutputsText.ShouldBe("No motor relations yet.");
    }

    [Fact]
    public void Update_WithEmptyLists_ShowsEmptyState()
    {
        var mapping = new MappingViewModel();

        mapping.Update([], []);

        mapping.SensorsText.ShouldBe("No sensors yet.");
        mapping.OutputsText.ShouldBe("No motor relations yet.");
    }

    [Fact]
    public void Update_WithSensors_FormatsOneLinePerReading()
    {
        var mapping = new MappingViewModel();

        mapping.Update(
            [new SensorReading("Core", 1, "Ray down", 0.5), new SensorReading("Core", 1, "Pitch", -0.25)],
            []);

        mapping.SensorsText.ShouldBe("Core 1 Ray down: 0.50\nCore 1 Pitch: -0.25");
    }

    [Fact]
    public void Update_WithMotors_FormatsTargetAndTorque()
    {
        var mapping = new MappingViewModel();

        mapping.Update([], [new MotorReading(1, 0.75, 1200)]);

        mapping.OutputsText.ShouldBe("Motor relation 1 \u2192 target 0.75, torque 1200");
    }

    [Fact]
    public void Update_FiresPropertyChanged()
    {
        var mapping = new MappingViewModel();
        var raised = false;
        mapping.PropertyChanged += (_, _) => raised = true;

        mapping.Update([new SensorReading("Core", 1, "s", 1)], []);

        raised.ShouldBeTrue();
    }
}

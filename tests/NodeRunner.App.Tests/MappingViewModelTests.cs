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
        mapping.OutputsText.ShouldBe("No moving parts yet.");
    }

    [Fact]
    public void Update_WithEmptyLists_ShowsEmptyState()
    {
        var mapping = new MappingViewModel();

        mapping.Update([], []);

        mapping.SensorsText.ShouldBe("No sensors yet.");
        mapping.OutputsText.ShouldBe("No moving parts yet.");
    }

    [Fact]
    public void Update_WithSensors_FormatsOneLinePerReading()
    {
        var mapping = new MappingViewModel();

        mapping.Update(
            [new SensorReading("Accelerometer", 1, "along", 0.5), new SensorReading("Accelerometer", 1, "across", -0.25)],
            []);

        mapping.SensorsText.ShouldBe("Accelerometer 1 · along: 0.50\nAccelerometer 1 · across: -0.25");
    }

    [Fact]
    public void Update_WithOutputs_FormatsTargetAndForce()
    {
        var mapping = new MappingViewModel();

        mapping.Update([], [new MotorReading(MotorReading.PistonKind, 1, 0.75, 1200)]);

        mapping.OutputsText.ShouldBe("Piston 1 \u2192 target 0.75, force 1200");
    }

    [Fact]
    public void Update_FiresPropertyChanged()
    {
        var mapping = new MappingViewModel();
        var raised = false;
        mapping.PropertyChanged += (_, _) => raised = true;

        mapping.Update([new SensorReading("Accelerometer", 1, "s", 1)], []);

        raised.ShouldBeTrue();
    }
}

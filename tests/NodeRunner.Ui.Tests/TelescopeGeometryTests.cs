using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

public sealed class TelescopeGeometryTests
{
    // The approved table on #1210: each row is a length within a link's shortest and longest, with
    // what covers each end, and where the cylinder, the stage's mouth and the cap or seat end up.
    [Theory]
    [InlineData("plain, 100%, shortest", false, 90, 90, 150, 15, 15, 45, 50, 75)]
    [InlineData("plain, 100%, half out", false, 120, 90, 150, 15, 15, 45, 65, 105)]
    [InlineData("plain, 100%, longest", false, 150, 90, 150, 15, 15, 45, 80, 135)]
    [InlineData("plain, 10%, shortest", false, 90, 90, 96, 15, 15, 21, 26, 75)]
    [InlineData("plain, 10%, longest", false, 96, 90, 96, 15, 15, 21, 29, 81)]
    [InlineData("plain, 200%, shortest", false, 90, 90, 210, 15, 15, 65, 70, 75)]
    [InlineData("plain, 200%, longest", false, 210, 90, 210, 15, 15, 65, 130, 195)]
    [InlineData("72 plain, 10% Start 0, shortest", false, 72, 72, 76.2, 15, 15, 21, 26, 57)]
    [InlineData("72 plain, 10% Start 0, longest", false, 76.2, 72, 76.2, 15, 15, 21, 28.1, 61.2)]
    [InlineData("72 plain, shortest 51", false, 51, 51, 72, 15, 15, 25.5, 30.5, 36)]
    [InlineData("72 plain, shortest 51, longest", false, 72, 51, 72, 15, 15, 25.5, 41, 57)]
    [InlineData("Servo Fixed on A, shortest", false, 73.2, 73.2, 94.8, 35, 15, 41, 46, 58.2)]
    [InlineData("Servo Fixed on A, half out", false, 84, 73.2, 94.8, 35, 15, 41, 51.4, 69)]
    [InlineData("Servo Fixed on A, longest", false, 94.8, 73.2, 94.8, 35, 15, 41, 56.8, 79.8)]
    [InlineData("Servo Fixed on both, shortest", false, 82.8, 82.8, 109.2, 35, 35, 39.8, 43.8, 47.8)]
    [InlineData("Servo Fixed on both, half out", false, 96, 82.8, 109.2, 35, 35, 39.8, 50.4, 61)]
    [InlineData("Servo Fixed on both, longest", false, 109.2, 82.8, 109.2, 35, 35, 39.8, 57, 74.2)]
    [InlineData("130, Servo Fixed on A, half out", false, 130, 110, 150, 35, 15, 41, 56, 115)]
    [InlineData("Servo Fixed on B, shortest", false, 73.2, 73.2, 94.8, 15, 35, 25.8, 30.8, 38.2)]
    [InlineData("Servo Fixed on B, half out", false, 84, 73.2, 94.8, 15, 35, 25.8, 36.2, 49)]
    [InlineData("Servo Fixed on B, longest", false, 94.8, 73.2, 94.8, 15, 35, 25.8, 41.6, 59.8)]
    [InlineData("Spring 84, Fixed on A, shortest", true, 57, 57, 84, 35, 15, 38, 39.667, 42)]
    [InlineData("Spring 84, Fixed on A, longest", true, 84, 57, 84, 35, 15, 38, 53.167, 69)]
    public void Place_PutsEachPartWhereTheApprovedTableSays(
        string row, bool spring, float length, float shortest, float longest, float coveredA, float coveredB, float cylinderEnd, float stageEnd, float cap)
    {
        var telescope = TelescopeGeometry.Place(spring ? TelescopeGeometry.Spring : TelescopeGeometry.Piston, length, shortest, longest, coveredA, coveredB);

        telescope.Start.ShouldBe(coveredA, row);
        telescope.CylinderEnd.ShouldBe(cylinderEnd, 0.01f, row);
        telescope.StageEnd.ShouldBe(stageEnd, 0.01f, row);
        telescope.Cap.ShouldBe(cap, 0.01f, row);
        telescope.HasCylinder.ShouldBeTrue(row);
        telescope.HasStage.ShouldBeTrue(row);
    }

    [Fact]
    public void Place_LeavesATubeOut_WhenItWouldEndBeforeItStarts()
    {
        // A Spring of 96 between two Servos Fixed on it: its seat B reaches back past the housing
        // at A when shortest, and only the stage comes out as it lengthens.
        var shortest = TelescopeGeometry.Place(TelescopeGeometry.Spring, 63, 63, 96, 35, 35);
        var longest = TelescopeGeometry.Place(TelescopeGeometry.Spring, 96, 63, 96, 35, 35);

        shortest.HasCylinder.ShouldBeFalse();
        shortest.HasStage.ShouldBeFalse();
        shortest.CylinderEnd.ShouldBe(28, 0.01f);
        shortest.StageEnd.ShouldBe(28, 0.01f);
        shortest.Cap.ShouldBe(28, 0.01f);
        longest.HasCylinder.ShouldBeFalse();
        longest.HasStage.ShouldBeTrue();
        longest.StageEnd.ShouldBe(51.5f, 0.01f);
        longest.Cap.ShouldBe(61, 0.01f);
    }

    // Nothing draws past what covers joint B, and the stage slides no further than its travel, as
    // when Training pushes a link past its stops.
    [Theory]
    [InlineData("Spring 96 between Servos, just out", true, 65, 63, 96, 35, 35, 30, 30, 30)]
    [InlineData("plain Piston, pushed under its shortest", false, 60, 90, 150, 15, 15, 45, 45, 45)]
    [InlineData("plain Piston, pulled past its longest", false, 170, 90, 150, 15, 15, 45, 80, 155)]
    public void Place_KeepsEachTubeWithinJointB_AndTheStageWithinItsTravel(
        string row, bool spring, float length, float shortest, float longest, float coveredA, float coveredB, float cylinderEnd, float stageEnd, float cap)
    {
        var telescope = TelescopeGeometry.Place(spring ? TelescopeGeometry.Spring : TelescopeGeometry.Piston, length, shortest, longest, coveredA, coveredB);

        telescope.CylinderEnd.ShouldBe(cylinderEnd, 0.01f, row);
        telescope.StageEnd.ShouldBe(stageEnd, 0.01f, row);
        telescope.Cap.ShouldBe(cap, 0.01f, row);
    }

    [Fact]
    public void Place_KeepsTheSameSizes_AtEveryLength()
    {
        var shortest = TelescopeGeometry.Place(TelescopeGeometry.Piston, 90, 90, 150, 15, 15);
        var longest = TelescopeGeometry.Place(TelescopeGeometry.Piston, 150, 90, 150, 15, 15);

        longest.CylinderEnd.ShouldBe(shortest.CylinderEnd);
        (longest.StageEnd - shortest.StageEnd).ShouldBe(30, 0.01f);
    }
}

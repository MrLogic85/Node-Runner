using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildGridTests
{
    private static readonly CanvasRect _area = BuildViewModel.BuildArea;

    [Fact]
    public void WithoutSnap_JointsGoWhereTheyArePut_OnTheDefaultGrid()
    {
        var build = new BuildViewModel();
        build.GridStep.ShouldBe(BuildGridDef.DefaultSizeCm);
        build.SnapToGrid.ShouldBeFalse();

        build.UseGrid(new BuildGridDef(25, snap: false));
        build.PlaceNode(new Vector2D(13, 7));
        build.MoveNode(1, new Vector2D(31, -12));

        build.GridStep.ShouldBe(25);
        build.Nodes.Single().Position.ShouldBe(new Vector2D(31, -12));
    }

    // Each Creation opens on its own grid, and changing the grid moves no joint already placed.
    [Fact]
    public void LoadCreation_UsesItsGrid_AndAGridChangeMovesNoJoint()
    {
        var build = new BuildViewModel();
        var creature = new CreatureDef([new NodeDef(1, new Vector2D(13, 7))], nextPartId: 2);

        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Fine", creature) { Grid = new BuildGridDef(25, snap: true) });
        build.GridStep.ShouldBe(25);
        build.SnapToGrid.ShouldBeTrue();
        build.Nodes.Single().Position.ShouldBe(new Vector2D(13, 7));

        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Coarse", creature) { Grid = new BuildGridDef(100, snap: false) });
        build.GridStep.ShouldBe(100);
        build.SnapToGrid.ShouldBeFalse();

        build.UseGrid(new BuildGridDef(50, snap: true));
        build.Nodes.Single().Position.ShouldBe(new Vector2D(13, 7));
    }

    // Like zoom, the grid is not part of the creature: no Undo step, and a locked Creation changes it.
    [Fact]
    public void ChooseGridSizeAndSnap_OnALockedCreation_ChangeTheGrid_WithoutAnUndoStep()
    {
        var build = Loaded(new CreatureDef([new NodeDef(1, new Vector2D(0, 0))], nextPartId: 2), locked: true);
        var changed = new List<string?>();
        build.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        build.ChooseGridSize(2);
        build.ChooseSnapToGrid(true);

        build.Grid.ShouldBe(new BuildGridDef(100, snap: true));
        changed.ShouldBe([nameof(BuildViewModel.Grid), nameof(BuildViewModel.Grid)]);
        (build.CanUndo, build.CanRedo).ShouldBe((false, false));
        build.IsLocked.ShouldBeTrue();

        build.ChooseGridSize(2);
        build.ChooseSnapToGrid(true);
        changed.Count.ShouldBe(2);

        build.ChooseGridSize(0);
        build.Grid.ShouldBe(new BuildGridDef(25, snap: true));
    }

    // The nearest grid point, a half away from 0; at an edge the nearest one that keeps the ring inside.
    [Theory]
    [InlineData(25, 13, 7, 25, 0)]
    [InlineData(50, 13, 7, 0, 0)]
    [InlineData(50, -25, 26, -50, 50)]
    [InlineData(100, 49, 51, 0, 100)]
    [InlineData(25, 600, -300, 575, -275)]
    [InlineData(50, 600, -300, 550, -250)]
    [InlineData(100, 600, -300, 500, -200)]
    [InlineData(25, -1000, 1000, -575, 275)]
    public void WithSnap_APlacedOrMovedJoint_LandsOnTheNearestGridPointInside(int size, double x, double y, double gridX, double gridY)
    {
        var build = new BuildViewModel();
        build.UseGrid(new BuildGridDef(size, snap: true));

        build.PlaceNode(new Vector2D(x, y));
        build.Nodes.Single().Position.ShouldBe(new Vector2D(gridX, gridY));

        build.MoveNode(1, new Vector2D(0, 0));
        build.MoveNode(1, new Vector2D(x, y));
        build.Nodes.Single().Position.ShouldBe(new Vector2D(gridX, gridY));
    }

    // Every frame of a drag starts from the snapshot; the joint nearest a grid point decides.
    [Fact]
    public void WithSnap_AGroupDrag_PutsTheJointNearestTheGridOnIt_EachFrame_KeepingItsShape()
    {
        var build = Pair(new Vector2D(0, 0), new Vector2D(110, 0));
        var start = build.SnapshotSelection();

        // Joint 2 at (140, 12) is nearer (150, 0) than joint 1 at (30, 12) is to (50, 0).
        build.TranslateSelection(start, new Vector2D(30, 12));
        Positions(build).ShouldBe([new Vector2D(40, 0), new Vector2D(150, 0)]);

        // Joint 1 at (5, 0) is nearer (0, 0) than joint 2 at (115, 0) is to (100, 0).
        build.TranslateSelection(start, new Vector2D(5, 0));
        Positions(build).ShouldBe([new Vector2D(0, 0), new Vector2D(110, 0)]);
    }

    [Fact]
    public void WithSnap_AGroupDrag_BreaksATieByTheLowestJointId()
    {
        var build = Pair(new Vector2D(0, 0), new Vector2D(130, 0));

        // Both joints are 10 from a grid point: joint 1's (0, 0) wins over joint 2's (150, 0).
        build.TranslateSelection(build.SnapshotSelection(), new Vector2D(10, 0));

        Positions(build).ShouldBe([new Vector2D(0, 0), new Vector2D(130, 0)]);
    }

    [Fact]
    public void WithSnap_AGroupDrag_StaysInside_EvenOffTheGrid()
    {
        var build = Pair(new Vector2D(0, 0), new Vector2D(100, 0));

        build.TranslateSelection(build.SnapshotSelection(), new Vector2D(10_000, 0));

        Positions(build).ShouldBe([new Vector2D(_area.Max.X - NodeDef.PlainJointRadius - 100, 0), new Vector2D(_area.Max.X - NodeDef.PlainJointRadius, 0)]);
    }

    [Fact]
    public void WithSnap_TurningAndScaling_DoNotSnap()
    {
        var build = Pair(new Vector2D(0, 0), new Vector2D(100, 0));

        build.RotateSelection(build.SnapshotSelection(), Math.PI / 6).ShouldBeTrue();
        Positions(build).ShouldBe([new Vector2D(7, -25), new Vector2D(93, 25)]);

        build.ScaleSelection(build.SnapshotSelection(), 1.5);
        Positions(build).ShouldBe([new Vector2D(-15, -38), new Vector2D(115, 38)]);
    }

    // The biggest ring, a 1 m Wheel's, still finds a grid point inside next to the edge.
    [Fact]
    public void WithSnap_AJointWithTheLargestRing_LandsOnTheGridInside()
    {
        var wheel = new WheelDef(2, 1, radius: WheelDef.MaxRadius);
        var build = Loaded(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), wheel], nextPartId: 3));
        build.UseGrid(new BuildGridDef(100, snap: true));

        build.MoveNode(1, new Vector2D(10_000, -10_000));

        build.Nodes.Single().Position.ShouldBe(new Vector2D(_area.Max.X - WheelDef.MaxRadius, _area.Min.Y + WheelDef.MaxRadius));
    }

    // At least half a metre in whole steps, so the copy never covers its original.
    [Theory]
    [InlineData(25, 50)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public void Copy_GoesAsideByWholeGridSteps_AtLeastHalfAMetre(int size, double copyStep)
    {
        var build = Pair(new Vector2D(0, 0), new Vector2D(100, 0), size);
        build.CopyStep.ShouldBe(copyStep);

        build.CopySelectedParts();

        Positions(build).Skip(2).ShouldBe([new Vector2D(copyStep, copyStep), new Vector2D(100 + copyStep, copyStep)]);
    }

    [Fact]
    public void Copy_AtTheEdge_TakesAnotherDiagonalOfTheSameStep()
    {
        var build = Pair(new Vector2D(400, 200), new Vector2D(500, 200), 100);

        build.CopySelectedParts();

        Positions(build).Skip(2).ShouldBe([new Vector2D(300, 100), new Vector2D(400, 100)]);
    }

    private static BuildViewModel Pair(Vector2D first, Vector2D second, int size = 50)
    {
        var build = Loaded(new CreatureDef([new NodeDef(1, first), new NodeDef(2, second)], nextPartId: 3));
        build.UseGrid(new BuildGridDef(size, snap: true));
        build.ReplaceSelection([1, 2]);
        return build;
    }

    private static Vector2D[] Positions(BuildViewModel build) => [.. build.Nodes.Select(node => node.Position)];
}

using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using static NodeRunner.App.Tests.ViewModels.BuildFixtures;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BuildViewModelTests
{
    private static readonly CanvasRect _area = BuildViewModel.BuildArea;
    private static readonly double _r = NodeDef.PlainJointRadius;

    [Fact]
    public void Joints_LandOnWholeUnits_InsideTheBuildArea_AndEachPlaceOrMoveRedraws()
    {
        var build = new BuildViewModel();
        var changes = build.CountAnatomyChanges();
        build.PlaceNode(new Vector2D(3, 4)).ShouldBe(1);
        build.Nodes.Single().Position.ShouldBe(new Vector2D(3, 4));
        build.MoveNode(1, new Vector2D(10, 20));
        build.Nodes.Single().Position.ShouldBe(new Vector2D(10, 20));
        changes().ShouldBe(2);
        build.MoveNode(1, new Vector2D(_area.Min.X - 500, 40));
        build.Nodes[0].Position.ShouldBe(new Vector2D(_area.Min.X + _r, 40));
        build.PlaceNode(new Vector2D(_area.Max.X + 500, _area.Min.Y - 500));
        build.Nodes[1].Position.ShouldBe(new Vector2D(_area.Max.X - _r, _area.Min.Y + _r));

        foreach (var (x, y, wholeX, wholeY) in new[] { (12.4, -7.6, 12.0, -8.0), (0.5, -0.5, 1.0, -1.0), (-0.4, 0.4, 0.0, 0.0) })
        {
            var id = build.PlaceNode(new Vector2D(x, y));
            build.Nodes[^1].Position.ShouldBe(new Vector2D(wholeX, wholeY));
            build.MoveNode(id, new Vector2D(x + 100, y));
            build.Nodes[^1].Position.ShouldBe(new Vector2D(wholeX + 100, wholeY));
        }

        build = SelectedPair();
        build.TranslateSelection(build.SnapshotSelection(), new Vector2D(_area.Max.X, 30));
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(_area.Max.X - _r - 100, 30), new Vector2D(_area.Max.X - _r, 30)]);

        // An older save may hold joints off whole units; Build keeps them until one moves.
        var fractional = new CreatureDef([new NodeDef(1, new Vector2D(0.1, 0.3)), new NodeDef(2, new Vector2D(100.1, 0.3))], [], [], 3);
        build = new BuildViewModel(new CreatureBuilder(fractional));
        build.ReplaceSelection([1, 2]);
        build.CopySelectedParts();
        build.Nodes.Skip(2).Select(node => node.Position).ShouldAllBe(position => position.X == Math.Round(position.X) && position.Y == Math.Round(position.Y));
        build = new BuildViewModel(new CreatureBuilder(fractional));
        build.ReplaceSelection([1, 2]);
        build.TranslateSelection(build.SnapshotSelection(), new Vector2D(1500.3, 0));
        build.Nodes[1].Position.X.ShouldBe(_area.Max.X - _r);
        build.Nodes[0].Position.X.ShouldBe(_area.Max.X - _r - 100, 1e-9);
    }

    [Fact]
    public void ASelection_TurnsAndScalesAboutThePivotOfItsJoints_WithinTheBuildArea_AndRestores()
    {
        var build = SelectedPair();
        var start = build.SnapshotSelection();
        start.Pivot.ShouldBe(new Vector2D(50, 0));
        foreach (var radians in new[] { Math.PI / 2, -Math.PI / 2, 3 * Math.PI / 2 })
        {
            build.RotateSelection(start, radians);
            build.Nodes[0].Position.X.ShouldBe(50, 1e-9);
            build.Nodes[0].Position.Y.ShouldBe(-50 * Math.Sin(radians), 1e-9);
            build.Nodes[1].Position.Y.ShouldBe(50 * Math.Sin(radians), 1e-9);
        }

        for (var i = 0; i < 1000; i++)
        {
            build.RotateSelection(start, i * 0.37);
        }

        build.RotateSelection(start, 0);
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);

        // 50 ∓ 50·cos 30° = 6.70… and 93.30…; ∓ 50·sin 30° = ∓25.
        build.RotateSelection(start, Math.PI / 6);
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(7, -25), new Vector2D(93, 25)]);

        foreach (var (factor, applied) in new[] { (2.0, 2.0), (0.5, 0.5), (100, BuildViewModel.MaxSelectionScale), (-3, BuildViewModel.MinSelectionScale) })
        {
            build.ScaleSelection(start, factor);
            build.Nodes[0].Position.X.ShouldBe(Math.Round(50 - (50 * applied), MidpointRounding.AwayFromZero));
            build.Nodes[1].Position.X.ShouldBe(Math.Round(50 + (50 * applied), MidpointRounding.AwayFromZero));
        }

        Should.Throw<ArgumentOutOfRangeException>(() => build.ScaleSelection(start, double.NaN));
        build.RestoreSelection(start);
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(0, 0), new Vector2D(100, 0)]);

        build = new BuildViewModel();
        build.PlaceNode(new Vector2D(_area.Max.X - 18, -100));
        build.PlaceNode(new Vector2D(_area.Max.X - 18, 100));
        build.PlaceNode(new Vector2D(10, 10));
        build.PlaceNode(new Vector2D(10, 10));
        build.ReplaceSelection([1, 2]);
        build.RotateSelection(build.SnapshotSelection(), Math.PI / 4).ShouldBeFalse();
        build.Nodes.Take(2).Select(node => node.Position).ShouldBe([new Vector2D(_area.Max.X - 18, -100), new Vector2D(_area.Max.X - 18, 100)]);
        build.ReplaceSelection([3, 4]);
        build.ScaleSelection(build.SnapshotSelection(), 3);
        build.Nodes.Skip(2).ShouldAllBe(node => node.Position == new Vector2D(10, 10));

        build = Loaded(new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0)), new BeamDef(101, 1, 2)]), locked: true);
        build.ReplaceSelection([1, 2]);
        build.ScaleSelection(build.SnapshotSelection(), 2);
        build.Nodes.Select(node => node.Position).ShouldBe([new Vector2D(-10, 0), new Vector2D(30, 0)]);
        build.RotateSelection(build.SnapshotSelection(), Math.PI).ShouldBeTrue();
        build.Nodes[0].Position.X.ShouldBe(30, 1e-9);
    }

    [Fact]
    public void ALooseJoint_ShowsTheLooseMark_UnlessAServoWheelOrBrakeDrawsItsDanger()
    {
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(3, new Vector2D(200, 0)), new NodeDef(4, new Vector2D(300, 0)),
                new NodeDef(5, new Vector2D(400, 0)), new NodeDef(10, new Vector2D(500, 0)), new NodeDef(11, new Vector2D(600, 0)),
                new BeamDef(6, 4, 5), new ServoDef(7, 2), new WheelDef(8, 3), new TouchSensorDef(9, 10), new BrakeDef(12, 11),
            ],
            nextPartId: 13));

        new[] { 1, 2, 3, 4, 10, 11 }.Select(build.IsLoose).ShouldBe([true, true, true, false, true, true]);
        new[] { 1, 2, 3, 4, 10, 11 }.Select(build.ShowsLooseMark).ShouldBe([true, false, false, false, true, false]);
    }

    [Fact]
    public void ASelection_IsToggledReplacedAndCleared_RaisingWhatChanged_AndDeleteTakesWhatGoesWithIt()
    {
        var build = Loaded(Carrier());
        build.ToggleSelected(Joint(1));
        build.ToggleSelected(Beam(101));
        build.ToggleSelected(new(CreatureElementKind.Piston, 301));
        build.ToggleSelected(new(CreatureElementKind.Sensor, 201));
        build.SelectedPartCount.ShouldBe(4);
        build.ToggleSelected(Beam(101));
        build.Selection.Parts.ShouldBe([Joint(1), new(CreatureElementKind.Sensor, 201), new(CreatureElementKind.Piston, 301)], ignoreOrder: true);

        var raised = new List<string?>();
        build.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        var changes = build.CountAnatomyChanges();
        build.SelectOnly(CreatureElementKind.Beam, 101);
        (build.SelectedNodeCount, build.SelectedPartCount, build.SingleSelected).ShouldBe((0, 1, Beam(101)));
        new[] { nameof(BuildViewModel.SelectedNodeCount), nameof(BuildViewModel.SelectedPartCount), nameof(BuildViewModel.SingleSelected) }.ShouldBeSubsetOf(raised);
        changes().ShouldBe(1);
        raised.Clear();
        build.ClearSelection();
        (build.SelectedPartCount, build.SingleSelected).ShouldBe((0, null));
        raised.ShouldContain(nameof(BuildViewModel.SingleSelected));
        changes().ShouldBe(2);

        build.SelectOnly(CreatureElementKind.Sensor, 201);
        build.SingleSelectedId(CreatureElementKind.Sensor).ShouldBe(201);
        build.SelectOnly(CreatureElementKind.Beam, 102);
        (build.SingleSelectedId(CreatureElementKind.Sensor), build.SelectedPartCount).ShouldBe((null, 1));
        build.DeleteSelectedParts();
        build.Nodes.Count.ShouldBe(3);
        build.Undo();

        build.ReplaceSelection(Set(Joint(3), Beam(101), Beam(102), new(CreatureElementKind.Sensor, 201), new(CreatureElementKind.Piston, 301)));
        build.DeleteSelectedParts();
        build.Nodes.Select(node => node.Id).ShouldBe([1, 2]);
        (build.Beams.Count, build.Sensors.Count, build.Pistons.Count, build.SelectedPartCount).ShouldBe((0, 0, 0, 0));
    }

    [Fact]
    public void SetParameter_ChangesOnlyThatSetting_OnEverySelectedPart_OnceForAValue_EvenWhenLocked()
    {
        var pistons = new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(0, 90)), new NodeDef(4, new Vector2D(90, 90)),
                new PistonDef(301, 1, 3, stroke: 0.2), new PistonDef(302, 2, 4, stroke: 0.4), new PistonDef(303, 1, 4),
            ]);
        foreach (var locked in new[] { false, true })
        {
            var build = Loaded(pistons, locked);
            build.ReplaceSelection(Set(new(CreatureElementKind.Piston, 301), new(CreatureElementKind.Piston, 302)));
            var changes = build.CountAnatomyChanges();
            build.SetParameter(PartParameterId.Strength, 20000);
            build.SetParameter(PartParameterId.MaxSpeed, 100);
            build.SetParameter(PartParameterId.MaxSpeed, 100);
            build.Pistons.Select(piston => (piston.Strength, piston.Stroke, piston.MaxSpeed)).ShouldBe(
                [(20000, 0.2, 100), (20000, 0.4, 100), (PistonDef.DefaultStrength, PistonDef.DefaultStroke, PistonDef.DefaultMaxSpeed)]);
            build.SetParameter(PartParameterId.Stroke, 0.5);
            build.Pistons.Select(piston => piston.Stroke).ShouldBe([0.5, 0.5, PistonDef.DefaultStroke]);
            changes().ShouldBe(3);
        }

        var carrier = Loaded(Carrier());
        carrier.ReplaceSelection(Set(Joint(1), new(CreatureElementKind.Piston, 301)));
        Should.Throw<InvalidOperationException>(() => carrier.SetParameter(PartParameterId.Strength, 20000));
    }

    [Fact]
    public void ACamera_IsAimedAndTuned_ButALockedOneKeepsItsRays()
    {
        var build = Loaded(TwoCameras());
        build.ReplaceSelection(Set(new(CreatureElementKind.Sensor, 201), new(CreatureElementKind.Sensor, 202)));
        build.SetParameter(PartParameterId.Rays, 5);
        build.SetParameter(PartParameterId.Spread, Math.PI / 3);
        build.SetParameter(PartParameterId.CameraRange, 300);
        build.Sensors.ShouldAllBe(sensor => sensor.Rays == 5 && sensor.Spread == Math.PI / 3 && sensor.Range == 300);
        build.SelectedValuesOf(PartParameterId.Rays).ShouldBe([5.0, 5.0]);
        build.SelectOnly(CreatureElementKind.Sensor, 201);
        var changes = build.CountAnatomyChanges();
        build.SetParameter(PartParameterId.Aim, 1);
        build.SetParameter(PartParameterId.Aim, 1);
        build.Sensors[0].Aim.ShouldBe(1);
        changes().ShouldBe(1);

        build = Loaded(TwoCameras(), locked: true);
        build.SelectOnly(CreatureElementKind.Sensor, 201);
        build.ShownParameters.ShouldBe([PartParameterId.Aim, PartParameterId.Rays, PartParameterId.Spread, PartParameterId.CameraRange]);
        build.EditableParameters.ShouldBe([PartParameterId.Aim, PartParameterId.Spread, PartParameterId.CameraRange]);
        build.CanEdit(PartParameterId.Rays).ShouldBeFalse();
        build.SelectedValuesOf(PartParameterId.Rays).ShouldBe([3.0]);
        Should.Throw<InvalidOperationException>(() => build.SetParameter(PartParameterId.Rays, 5));
        build.SetParameter(PartParameterId.Aim, 1);
        build.SetParameter(PartParameterId.Spread, Math.PI / 4);
        build.SetParameter(PartParameterId.CameraRange, 150);
        build.Sensors[0].ShouldBe(new SensorDef(201, 101, SensorKind.Camera, aim: 1, rays: 3, spread: Math.PI / 4, range: 150));
        build.Unlock();
        build.SetParameter(PartParameterId.Rays, 5);
        build.Sensors[0].Rays.ShouldBe(5);
    }

    [Fact]
    public void ACamerasSpread_HasEffect_OnlyWhileASelectedCameraHasMoreThanOneRay()
    {
        foreach (var (rays, effect) in new[] { (1, false), (3, true) })
        {
            var build = Loaded(TwoCameras(rays));
            build.SelectOnly(CreatureElementKind.Sensor, 201);
            build.HasEffect(PartParameterId.Spread).ShouldBe(effect);
            build.HasEffect(PartParameterId.CameraRange).ShouldBeTrue();
            build.HasEffect(PartParameterId.Rays).ShouldBeTrue();
        }

        var mixed = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)), new BeamDef(101, 1, 2), new BeamDef(102, 2, 3),
                new SensorDef(201, 101, SensorKind.Camera, rays: 1), new SensorDef(202, 102, SensorKind.Camera, rays: 3),
            ]));
        mixed.ReplaceSelection(Set(new(CreatureElementKind.Sensor, 201), new(CreatureElementKind.Sensor, 202)));
        mixed.HasEffect(PartParameterId.Spread).ShouldBeTrue();
    }

    [Theory]
    [InlineData(new[] { 301 }, "Strength,Stroke,StartPosition,MaxSpeed,RiseTime")]
    [InlineData(new[] { 301, 302 }, "Strength,Stroke,StartPosition,MaxSpeed,RiseTime")]
    [InlineData(new[] { 301, 1 }, "")]
    [InlineData(new[] { 301, 101 }, "")]
    [InlineData(new[] { 201 }, "Aim,Rays,Spread,CameraRange")]
    [InlineData(new[] { 201, 202 }, "Rays,Spread,CameraRange")]
    [InlineData(new[] { 401, 402 }, "Stiffness,Damping,Stroke,CoilLength")]
    [InlineData(new[] { 301, 401 }, "Stroke")]
    [InlineData(new int[0], "")]
    public void EditableParameters_AreOnePartsOwn_OrThoseEverySelectedPartHasAndCanShare(int[] parts, string editable)
    {
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)), new BeamDef(101, 1, 2), new BeamDef(102, 2, 3),
                new SensorDef(201, 101, SensorKind.Camera), new SensorDef(202, 102, SensorKind.Camera),
                new PistonDef(301, 1, 2), new PistonDef(302, 2, 3), new SpringDef(401, 1, 3), new SpringDef(402, 2, 3),
            ],
            nextPartId: 403));
        build.ReplaceSelection(Set([.. parts.Select(id => new CreatureElementSelection(
            id switch { < 100 => CreatureElementKind.Node, < 200 => CreatureElementKind.Beam, < 300 => CreatureElementKind.Sensor, < 400 => CreatureElementKind.Piston, _ => CreatureElementKind.Spring },
            id))]));

        string.Join(',', build.EditableParameters).ShouldBe(editable);
    }

    [Fact]
    public void Tools_ClearTheSelection_ButKeepThePickedLink_WhichLoadResetsToABeam()
    {
        foreach (var tool in new[] { BuildTool.Parts, BuildTool.Select, BuildTool.Beam })
        {
            var build = Loaded(TwoCameras());
            build.PickTool(BuildTool.Select);
            build.SelectOnly(CreatureElementKind.Sensor, 201);
            build.PickTool(tool);
            (build.SelectedPartCount, build.ActiveTool).ShouldBe((0, tool));
        }

        var links = new BuildViewModel();
        links.ActiveTool.ShouldBe(BuildTool.Joint);
        links.ActiveTool = BuildTool.Beam;
        links.PickLink(BuildLink.Wing);
        links.PickedLink.ShouldBe(BuildLink.Beam);
        links.PickLink(BuildLink.Beam);
        links.PickedLink.ShouldBeNull();
        foreach (var other in new[] { BuildTool.Parts, BuildTool.Joint, BuildTool.Select })
        {
            links.ActiveTool = other;
            links.ActiveTool = BuildTool.Beam;
            links.PickedLink.ShouldBeNull();
        }

        links.PickLink(BuildLink.Spring);
        links.PickedLink.ShouldBe(BuildLink.Spring);
        foreach (var other in new[] { BuildTool.Parts, BuildTool.Joint, BuildTool.Select })
        {
            links.ActiveTool = other;
            links.ActiveTool = BuildTool.Beam;
        }

        links.PlaceNode(new Vector2D(0, 0));
        links.ReplaceSelection([1]);
        links.PickTool(BuildTool.Beam);
        links.ReplaceSelection([1]);
        links.ClearSelection();
        links.PickedLink.ShouldBe(BuildLink.Spring);
        links.ClearPickedLink();
        links.ActiveTool = BuildTool.Select;
        links.Load(new CreatureBuilder().Build());
        (links.ActiveTool, links.PickedLink).ShouldBe((BuildTool.Joint, BuildLink.Beam));
    }

    [Fact]
    public void Load_LocksExactlyATrainedCreation_ShowsItsNameAndGeneration_AndUnlockRaises()
    {
        var build = new BuildViewModel();
        build.IsActive.ShouldBeFalse();
        var raised = new List<string?>();
        build.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        build.IsActive = false;
        raised.ShouldBeEmpty();
        build.IsActive = true;
        raised.ShouldContain(nameof(BuildViewModel.IsActive));

        var creature = new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(20, 0)), new BeamDef(3, 1, 2)]);
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", creature, null));
        build.IsLocked.ShouldBeFalse();
        build.LoadCreation(new CreationDef(Guid.NewGuid(), "Worm", creature, TestTraining.State(1, 1, TestTraining.Run)));
        build.IsLocked.ShouldBeTrue();
        build.Load(creature, locked: true, creationName: "Worm", training: TestTraining.State(9, 1, TestTraining.Run));
        (build.CreationName, build.TrainingGeneration).ShouldBe(("Worm", 9));
        build.MoveNode(1, new Vector2D(5, 5));
        build.Nodes[0].Position.ShouldBe(new Vector2D(5, 5));
        var added = build.PlaceNode(new Vector2D(30, 0));
        build.CanConnectLink(BuildLink.Beam, 2, added, out _).ShouldBeTrue();
        build.ConnectLink(BuildLink.Beam, 2, added).ShouldNotBeNull();
        (build.Nodes.Count, build.Beams.Count, build.IsLocked).ShouldBe((3, 2, true));

        raised.Clear();
        build.Unlock();
        build.IsLocked.ShouldBeFalse();
        raised.ShouldContain(nameof(BuildViewModel.IsLocked));
        build.SetCreationName("Hopper");
        build.CreationName.ShouldBe("Hopper");
        build.Load(new CreatureDef([], [], []));
        build.CreationName.ShouldBeEmpty();
    }

    [Fact]
    public void HitTests_FindTheNearestJointOrBeamWithinReach_AndLinksJoinTwoJointsOnce()
    {
        var build = new BuildViewModel();
        build.TryFindNodeNear(new Vector2D(0, 0), 10, out var node).ShouldBeFalse();
        node.ShouldBe(-1);
        var a = build.PlaceNode(new Vector2D(0, 0));
        var b = build.PlaceNode(new Vector2D(5, 0));
        build.PlaceNode(new Vector2D(100, 100));
        build.TryFindNodeNear(new Vector2D(6, 0), 10, out node).ShouldBeTrue();
        node.ShouldBe(b);
        build.TryFindNodeNear(new Vector2D(50, 50), 10, out node).ShouldBeFalse();
        node.ShouldBe(-1);

        var changes = build.CountAnatomyChanges();
        build.ConnectLink(BuildLink.Beam, a, a).ShouldBeNull();
        build.ConnectLink(BuildLink.Beam, a, b).ShouldNotBeNull();
        build.ConnectLink(BuildLink.Beam, a, b).ShouldBeNull();
        build.Beams.Select(beam => (beam.NodeA, beam.NodeB)).ShouldBe([(a, b)]);
        changes().ShouldBe(1);
        build.TryFindBeamNear(new Vector2D(3, 0), 2, out var beamId).ShouldBeTrue();
        beamId.ShouldBe(build.Beams[0].Id);
        build.TryFindBeamNear(new Vector2D(3, 50), 2, out beamId).ShouldBeFalse();
        beamId.ShouldBe(-1);
    }

    [Fact]
    public void ASensor_SitsMidwayBetweenWhatCoversItsBeamsEnds_AndIsHitOnlyOnItsPicture()
    {
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(100, 0)), new NodeDef(6, new Vector2D(100, 100)), new BeamDef(3, 1, 2), new BeamDef(7, 2, 6),
                new SensorDef(4, 3, SensorKind.Accelerometer), new SensorDef(5, 7, SensorKind.Camera),
            ]));
        build.DrawnPartAt(new Vector2D(50, 0), PlacingTargets.None).ShouldBe(new CreatureElementSelection(CreatureElementKind.Sensor, 4));
        build.DrawnPartAt(new Vector2D(100, 50), PlacingTargets.None).ShouldBe(new CreatureElementSelection(CreatureElementKind.Sensor, 5));
        build.DrawnPartAt(new Vector2D(50, SensorPicture.AccelerometerSize), PlacingTargets.None).ShouldBeNull();

        // A Wheel's size does not move the sensor; it is see-through under its links.
        build = Loaded(SensorLink(new WheelDef(5, 1, radius: 60)));
        build.SensorCentre(4).ShouldBe(new Vector2D(100, 0));
        build.SensorCentreOn(3).ShouldBe(new Vector2D(100, 0));

        // A Servo (27) on joint 1 and a hub (15) on joint 2, 200 apart: edges at 27 and 185, centre at 106.
        build = Loaded(SensorLink(new ServoDef(5, 1)));
        build.SensorCentre(4).ShouldBe(new Vector2D(106, 0));
        build.SensorCentreOn(3).ShouldBe(new Vector2D(106, 0));
        // The 24-wide picture covers 94–118 shifted, 88–112 unshifted: only the shift explains these.
        build.DrawnPartAt(new Vector2D(115, 0), PlacingTargets.None).ShouldBe(new CreatureElementSelection(CreatureElementKind.Sensor, 4));
        build.DrawnPartAt(new Vector2D(91, 0), PlacingTargets.None).ShouldBe(Beam(3));
    }

    [Fact]
    public void ShownCameraRays_LeaveTheSensorsCentre_TurnWithTheBeam_AndHideWithTheCamerasSettings()
    {
        var (build, camera, accelerometer) = DownwardBeamWithCameraAndAccelerometer();
        build.ToggleSelected(new(CreatureElementKind.Sensor, camera));
        build.SetParameter(PartParameterId.Aim, Math.PI / 4);
        build.SetParameter(PartParameterId.Spread, Math.PI / 2);
        build.SetParameter(PartParameterId.CameraRange, 300);

        var (origin, ends) = build.ShownCameraRays().ShouldHaveSingleItem();
        origin.ShouldBe(new Vector2D(0, 50));
        // Aimed 45° off the downward beam, the outer rays 45° either side of that: straight down,
        // 45° towards −x (300 × cos 45° ≈ 212.1) and level towards −x.
        ends.Select(end => (Math.Round(end.X, 1), Math.Round(end.Y, 1))).ShouldBe([(0, 350), (-212.1, 262.1), (-300, 50)], ignoreOrder: true);
        build.ToggleSelected(new(CreatureElementKind.Sensor, accelerometer));
        build.ShownCameraRays().ShouldBeEmpty();

        build = Loaded(SensorLink(new ServoDef(5, 1), SensorKind.Camera));
        build.ToggleSelected(new(CreatureElementKind.Sensor, 4));
        build.ShownCameraRays().ShouldHaveSingleItem().Origin.ShouldBe(new Vector2D(106, 0));
    }

    [Fact]
    public void Leaving_HandsOverAConnectedCreature_ButSnapshotKeepsAnyDrawing()
    {
        var build = new BuildViewModel();
        build.TryLeave(out var creature, out var errors).ShouldBeTrue();
        creature.ShouldBeNull();
        errors.ShouldBeEmpty();

        build.PlaceNode(new Vector2D(0, 0));
        build.TryLeave(out creature, out errors).ShouldBeFalse();
        creature.ShouldBeNull();
        errors.ShouldNotBeEmpty();
        build.TryGetTrainableCreature(out creature).ShouldBeFalse();
        creature.ShouldBeNull();
        build.Snapshot().Nodes.Count.ShouldBe(1);

        build.PlaceNode(new Vector2D(90, 0));
        build.ConnectLink(BuildLink.Beam, 1, 2);
        build.TryLeave(out creature, out errors).ShouldBeTrue();
        creature!.Nodes.Count.ShouldBe(2);
        errors.ShouldBeEmpty();
        var third = build.PlaceNode(new Vector2D(180, 20));
        build.ConnectLink(BuildLink.Beam, 2, third);
        build.ConnectLink(BuildLink.Piston, 1, third);
        build.TryGetTrainableCreature(out creature).ShouldBeTrue();
        creature!.Beams.Count.ShouldBe(2);
    }

    [Fact]
    public void CanvasNotes_MarkEachLinkTooShortForWhatCoversItsEnds()
    {
        // 60 units is long enough between plain joints (56) but not with a Servo on one (68);
        // a Piston's telescope needs 16 units more than a Beam, 72 between plain joints.
        var build = Loaded(new CreatureDef(
            [
                new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(60, 0)), new NodeDef(3, new Vector2D(0, 200)), new NodeDef(4, new Vector2D(50, 200)),
                new NodeDef(5, new Vector2D(160, 0)), new NodeDef(6, new Vector2D(160, 0)), new NodeDef(7, new Vector2D(60, 100)),
                new BeamDef(10, 1, 2), new BeamDef(11, 1, 3), new BeamDef(12, 3, 4), new BeamDef(13, 2, 5), new BeamDef(14, 5, 6), new BeamDef(15, 2, 7),
                new PistonDef(16, 7, 3),
            ],
            nextPartId: 17));
        static CanvasNote TooShort(CreatureElementKind kind, int id) => new(CanvasNoteKind.Danger, new CreatureElementSelection(kind, id), UiText.Plain("Too short"));
        build.CanvasNotes().ShouldBe([TooShort(CreatureElementKind.Beam, 12)]);

        build.PlacePart(BuildPart.Servo, Joint(1)).ShouldNotBeNull();
        build.CanvasNotes().ShouldContain(TooShort(CreatureElementKind.Beam, 10));
        build.Undo();
        build.CanvasNotes().ShouldNotContain(TooShort(CreatureElementKind.Beam, 10));

        build = Loaded(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(60, 0)), new NodeDef(3, new Vector2D(0, 200)), new BeamDef(5, 1, 3), new BeamDef(6, 2, 3), new PistonDef(7, 1, 2)],
            nextPartId: 8));
        build.CanvasNotes().ShouldBe([TooShort(CreatureElementKind.Piston, 7)]);
    }

    [Fact]
    public void ShowTrainingBlockers_NotesEachLooseJoint_UntilJoined_AndLeavesLaterOnesForTheNextTap()
    {
        var build = new BuildViewModel();
        var first = build.PlaceNode(new Vector2D(0, 0));
        var second = build.PlaceNode(new Vector2D(200, 0));
        var changed = new List<string?>();
        build.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        build.CanvasNotes().ShouldBeEmpty();

        build.ShowTrainingBlockers();
        changed.ShouldContain(nameof(BuildViewModel.CanvasNotes));
        build.CanvasNotes().ShouldBe([NotConnected(first), NotConnected(second)]);
        build.ConnectLink(BuildLink.Beam, first, second).ShouldNotBeNull();
        build.CanvasNotes().ShouldBeEmpty();
        var loose = build.PlaceNode(new Vector2D(400, 0));
        build.CanvasNotes().ShouldBeEmpty();
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldBe([NotConnected(loose)]);

        build.Load(new CreatureDef([new NodeDef(1, new Vector2D(0, 0))], [], []));
        build.CanvasNotes().ShouldBeEmpty();
    }

    [Fact]
    public void ShowTrainingBlockers_NotesEveryPieceAtItsJointNearestAnother_UntilJoinedOrTouched()
    {
        var build = Loaded(TwoPieces());
        build.CanvasNotes().ShouldBeEmpty();
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldBe([NotConnected(2), NotConnected(4)]);
        new BuildGestures(build).Press(new Vector2D(2000, 2000));
        build.CanvasNotes().ShouldBeEmpty();
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldBe([NotConnected(2), NotConnected(4)]);
        build.Load(TwoPieces());
        build.CanvasNotes().ShouldBeEmpty();

        build.ShowTrainingBlockers();
        var spring = build.ConnectLink(BuildLink.Spring, 2, 4).ShouldNotBeNull();
        build.CanvasNotes().ShouldBeEmpty();
        build.Undo();
        build.Pieces().Count.ShouldBe(2);
        build.CanvasNotes().ShouldBeEmpty();
        build.Redo();
        build.SelectOnly(CreatureElementKind.Spring, spring);
        build.DeleteSelectedParts();
        build.Pieces().Count.ShouldBe(2);
        build.CanvasNotes().ShouldBeEmpty();

        build.ConnectLink(BuildLink.Beam, 2, 4).ShouldNotBeNull();
        var a = build.PlaceNode(new Vector2D(600, 0));
        build.ConnectLink(BuildLink.Beam, a, build.PlaceNode(new Vector2D(800, 0))).ShouldNotBeNull();
        build.CanvasNotes().ShouldBeEmpty();
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldBe([NotConnected(2), NotConnected(a)]);
    }

    [Fact]
    public void ACanvasTouch_HidesTheNotConnectedNotes_ButNotTooShort()
    {
        var build = Loaded(new CreatureDef(
            [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(50, 0)), new NodeDef(3, new Vector2D(400, 0)), new BeamDef(5, 1, 2)]));
        var tooShort = new CanvasNote(CanvasNoteKind.Danger, Beam(5), UiText.Plain("Too short"));
        build.ShowTrainingBlockers();
        build.CanvasNotes().ShouldBe([NotConnected(3), tooShort]);
        var changed = new List<string?>();
        build.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        new BuildGestures(build).Press(new Vector2D(2000, 2000));

        changed.ShouldContain(nameof(BuildViewModel.CanvasNotes));
        build.CanvasNotes().ShouldBe([tooShort]);
    }

    private static BuildViewModel SelectedPair()
    {
        var build = new BuildViewModel();
        build.PlaceNode(new Vector2D(0, 0));
        build.PlaceNode(new Vector2D(100, 0));
        build.ReplaceSelection([1, 2]);
        return build;
    }

    private static CreatureDef Carrier() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)), new BeamDef(101, 1, 2), new BeamDef(102, 2, 3),
            new SensorDef(201, 101, SensorKind.Accelerometer), new PistonDef(301, 1, 3),
        ]);

    private static CreatureDef TwoCameras(int rays = 3) => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(90, 0)), new NodeDef(3, new Vector2D(180, 0)), new BeamDef(101, 1, 2), new BeamDef(102, 2, 3),
            new SensorDef(201, 101, SensorKind.Camera, rays: rays), new SensorDef(202, 102, SensorKind.Camera, rays: rays),
        ]);

    // Joints 1 and 2, 200 apart, joined by beam 3 with sensor 4 on it; then a part on joint 1.
    private static CreatureDef SensorLink(IPartDef onJoint, SensorKind kind = SensorKind.Accelerometer) => new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(200, 0)), new BeamDef(3, 1, 2), new SensorDef(4, 3, kind), onJoint],
        nextPartId: 6);

    // Joints 1–2 and 3–4: joint 2 and joint 4 are the two pieces' nearest joints.
    private static CreatureDef TwoPieces() => new(
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(200, 100)), new NodeDef(3, new Vector2D(0, 300)), new NodeDef(4, new Vector2D(200, 300)),
            new BeamDef(5, 1, 2), new BeamDef(6, 4, 3),
        ]);

    private static CanvasNote NotConnected(int nodeId) => new(CanvasNoteKind.Danger, Joint(nodeId), UiText.Plain("Not connected"));
}

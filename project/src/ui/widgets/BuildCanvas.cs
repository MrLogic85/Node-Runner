using System.ComponentModel;
using Godot;
using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Renders the anatomy placed so far in Build through the zoom and pan of
/// <see cref="BuildGestures.View"/>, and forwards every pointer to
/// <see cref="BuildGestures"/>, which decides what the active
/// <see cref="BuildTool"/> does and when to zoom or pan. Binds to
/// <see cref="BuildViewModel"/> per `project/src/ui/AGENTS.md`; does
/// not own any anatomy state itself. See docs/BUILD_MODE.md.
/// </summary>
public partial class BuildCanvas : Node2D
{
    private const int _mousePointer = -1;

    // A refused Piston target's ring is dashed (#451): this many dashes, each this many segments.
    private const int _refusedRingDashes = 12;
    private const int _refusedDashSegments = 6;

    // The reference's Select frame and box: dashes 5 on 4 off, and 10-square corners rounded 2.
    private const float _frameDash = 5;
    private const float _frameGap = 4;
    private const float _frameCornerSquare = 10;
    private const float _frameCornerRadius = 2;

    private BuildViewModel? _viewModel;
    private BuildGestures? _gestures;
    private bool _viewFitted;
    private Control? _slot;
    private readonly BuildSensorMotion _sensorMotion = new();
    private double _gravity;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    /// <summary>The Select handles, authored in the slot above the canvas; placed here, hit-tested by <see cref="BuildGestures"/>.</summary>
    [Export]
    public UiSelectionHandle? MoveHandle { get; set; }

    [Export]
    public UiSelectionHandle? RotateHandle { get; set; }

    [Export]
    public UiSelectionHandle? ScaleHandle { get; set; }

    /// <summary>Turns a selected Camera (#594); placed and hit-tested like the Select handles.</summary>
    [Export]
    public UiSelectionHandle? AimHandle { get; set; }

    /// <summary>Shows <see cref="BuildViewModel.CanvasNotes"/> beside their parts, authored in the slot above the canvas.</summary>
    [Export]
    public UiCalloutLayer? CalloutLayer { get; set; }

    /// <summary>
    /// Takes a part dropped from the Parts tray (#376), authored over the slot. It lets touches
    /// through to the canvas and only catches the pointer while a part is being dragged.
    /// </summary>
    [Export]
    public Control? PartDropZone { get; set; }

    private const string _partDragKey = "build_part";
    private const double _placementNoteSeconds = 3;
    private BuildPart? _partDrag;
    private CreatureElementSelection? _dropHover;

    /// <summary>What a tray row hands Godot's drag-and-drop when a part is lifted out of it.</summary>
    public static Variant PartDragData(BuildPart part) =>
        new Godot.Collections.Dictionary { [_partDragKey] = (int)part };

    /// <summary>The part a drag carries, if it is a part from the tray.</summary>
    public static bool TryReadPartDrag(Variant data, out BuildPart part)
    {
        part = default;
        if (data.VariantType != Variant.Type.Dictionary
            || !data.AsGodotDictionary().TryGetValue(_partDragKey, out var value)
            || value.VariantType != Variant.Type.Int)
        {
            return false;
        }

        part = (BuildPart)value.AsInt32();
        return Enum.IsDefined(part);
    }

    public BuildViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            Unbind();
            _viewModel = value;
            _viewFitted = false;
            if (_viewModel is not null)
            {
                _viewModel.AnatomyChanged += OnAnatomyChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _gestures = new BuildGestures(_viewModel);
                _gestures.Changed += OnGesturesChanged;
                _gestures.View.Changed += OnGesturesChanged;
            }

            QueueRedraw();
        }
    }

    public override void _EnterTree()
    {
        _slot = GetParent() as Control;
        if (_slot is not null)
        {
            _slot.Resized += QueueRedraw;
        }
    }

    public override void _Ready()
    {
        _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsDouble();
        if (PartDropZone is { } zone)
        {
            zone.MouseFilter = Control.MouseFilterEnum.Ignore;
            zone.SetDragForwarding(
                new Callable(),
                Callable.From<Vector2, Variant, bool>(CanDropPart),
                Callable.From<Vector2, Variant>(DropPart));
        }
    }

    /// <summary>Swings each Accelerometer's weight as its beam moves, and draws again while one is still moving (#576).</summary>
    public override void _Process(double delta)
    {
        if (_viewModel is null)
        {
            return;
        }

        TrackPartDrag();
        if (delta <= 0 || _gravity <= 0)
        {
            return;
        }

        if (_sensorMotion.Advance(AccelerometerPoses(), delta, _gravity))
        {
            QueueRedraw();
        }
    }

    /// <summary>
    /// While a tray part is dragged, opens the drop zone and follows what it is over, so the
    /// drawing can show where it may go; Godot's drag ends on its own when the finger lifts.
    /// </summary>
    private void TrackPartDrag()
    {
        var viewport = GetViewport();
        BuildPart? part = viewport.GuiIsDragging() && TryReadPartDrag(viewport.GuiGetDragData(), out var dragged) && !_viewModel!.IsMoveOnly
            ? dragged
            : null;
        CreatureElementSelection? hover = null;
        if (part is not null && _slot is { } slot && _gestures is { } gestures
            && new Rect2(Vector2.Zero, slot.Size).HasPoint(slot.GetLocalMousePosition()))
        {
            hover = gestures.DropTargetAt(ToDomain(GetLocalMousePosition()));
        }

        if (PartDropZone is { } zone)
        {
            zone.MouseFilter = part is null ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
        }

        if (part != _partDrag || hover != _dropHover)
        {
            _partDrag = part;
            _dropHover = hover;
            QueueRedraw();
        }
    }

    private bool CanDropPart(Vector2 atPosition, Variant data) =>
        _viewModel is { IsMoveOnly: false } && _gestures is not null && TryReadPartDrag(data, out _);

    /// <summary>Places the dropped part where it landed; a refused drop's note goes after a moment, or at the next touch.</summary>
    private void DropPart(Vector2 atPosition, Variant data)
    {
        if (_gestures is null || !TryReadPartDrag(data, out var part))
        {
            return;
        }

        _gestures.DropPart(part, ToDomain(Transform.AffineInverse() * atPosition));
        if (_viewModel!.PlacementNote is { } note)
        {
            var viewModel = _viewModel;
            GetTree().CreateTimer(_placementNoteSeconds).Timeout += () =>
            {
                if (ReferenceEquals(viewModel.PlacementNote, note))
                {
                    viewModel.DismissPlacementNote();
                }
            };
        }
    }

    public override void _ExitTree()
    {
        if (_slot is not null)
        {
            _slot.Resized -= QueueRedraw;
            _slot = null;
        }

        Unbind();
    }

    private void Unbind()
    {
        if (_viewModel is not null)
        {
            _viewModel.AnatomyChanged -= OnAnatomyChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (_gestures is not null)
        {
            _gestures.Changed -= OnGesturesChanged;
            _gestures.View.Changed -= OnGesturesChanged;
            _gestures = null;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_viewModel is null || _gestures is null || !_viewModel.IsActive)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventScreenTouch { Pressed: true } touch:
                _gestures.Press(ToView(touch.Position), touch.Index);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenTouch touch:
                if (touch.Canceled)
                {
                    _gestures.Cancel();
                }

                _gestures.Release(ToView(touch.Position), touch.Index);
                break;
            case InputEventScreenDrag drag:
                _gestures.Drag(ToView(drag.Position), drag.Index);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouse mouse when mouse.Device == InputEvent.DeviceIdEmulation:
                // Godot's mouse copy of a touch; the touch itself arrives as ScreenTouch/ScreenDrag.
                break;
            default:
                HandleMouse(inputEvent);
                break;
        }
    }

    private void HandleMouse(InputEvent inputEvent)
    {
        if (PointerInput.TryGetPressPosition(inputEvent, out var pressPosition))
        {
            _gestures!.Press(ToView(pressPosition), _mousePointer);
            GetViewport().SetInputAsHandled();
        }
        else if (PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            _gestures!.Drag(ToView(dragPosition), _mousePointer);
            GetViewport().SetInputAsHandled();
        }
        else if (PointerInput.TryGetReleasePosition(inputEvent, out var releasePosition))
        {
            _gestures!.Release(ToView(releasePosition), _mousePointer);
        }
    }

    public override void _Draw()
    {
        if (_viewModel is null || _gestures is null)
        {
            LayoutSelectionHandles();
            LayoutCanvasNotes();
            return;
        }

        UpdateView();
        LayoutSelectionHandles();
        LayoutCanvasNotes();
        DrawThroughView();
        DrawBuildGrid();
        DrawAreaCorners();
        DrawSelectionBox();
        DrawBeamPreview();
        DrawRigidTriangles();

        // A dragged Select box shows what it would catch; the selection is cleared meanwhile (#704).
        var caught = _gestures.SelectionBoxCatches;
        var selected = caught.Count > 0 ? caught : _viewModel.Selection;
        foreach (var beam in _viewModel.Beams)
        {
            var nodeA = NodeById(beam.NodeA);
            var nodeB = NodeById(beam.NodeB);
            // One whose rings meet is too short, and still drawn, in danger, from centre to centre.
            var (start, end) = JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(nodeA.Position), (float)nodeA.Radius, ToGodot(nodeB.Position), (float)nodeB.Radius)
                ?? (ToGodot(nodeA.Position), ToGodot(nodeB.Position));

            DrawPlacingFeedback(beam, start, end);
            // A beam too short for training (#593) is drawn in danger until its joints move apart.
            var color = CreatureReadiness.IsTooShort(nodeA, nodeB) ? Theme.Danger : Theme.Beam;
            using (var pen = ViewPen())
            {
                pen.Line(start, end, color, Stroke(Theme.BeamWidth));
            }

            if (selected.Beams.Contains(beam.Id))
            {
                SelectionDrawing.DrawBeam(this, ViewTransform(), Theme.SelectionGlow, Stroke(Theme.SelectedBeamOffset), Stroke(Theme.SelectedBeamLineWidth), start, end);
            }
        }

        DrawPistons(selected);
        DrawSensors(selected);

        for (var nodeIndex = 0; nodeIndex < _viewModel.Nodes.Count; nodeIndex++)
        {
            var node = _viewModel.Nodes[nodeIndex];
            var position = ToGodot(node.Position);
            var isSelected = selected.Nodes.Contains(node.Id);
            var look = isSelected ? JointLook.Selected : ShowsAsLoose(node.Id) ? JointLook.Loose : JointLook.Plain;
            JointDrawing.DrawPlain(this, Theme, ViewTransform(), position, (float)node.Radius, look);
            if (isSelected)
            {
                SelectionDrawing.DrawJoint(this, Theme, ViewTransform(), position, (float)(node.Radius * BuildGestures.SelectedHaloScale));
            }
        }

        DrawSelectedCameraRays();
        DrawInvalidNodeMarkers();
        DrawInvalidBeamMarkers();

        DrawBeamEndRings();
        DrawSelectionFrame();
    }

    /// <summary>Each Piston over the beams (#451); one too short to train is drawn in danger, like a beam.</summary>
    private void DrawPistons(PartSet selected)
    {
        var viewTransform = ViewTransform();
        var showStroke = _viewModel!.CanEdit(PartParameterId.Stroke);
        foreach (var piston in _viewModel.Pistons)
        {
            var nodeA = NodeById(piston.NodeA);
            var nodeB = NodeById(piston.NodeB);
            var built = DistanceBetween(nodeA, nodeB);
            PistonDrawing.Draw(
                this,
                viewTransform,
                Theme,
                ToGodot(nodeA.Position),
                ToGodot(nodeB.Position),
                (float)nodeA.Radius,
                (float)nodeB.Radius,
                (float)Piston.ShortestLength(built, piston.Stroke),
                (float)Piston.LongestLength(built, piston.Stroke),
                CreatureReadiness.IsTooShort(nodeA, nodeB) ? Theme.Danger : Theme.MotorAccent,
                selected.Pistons.Contains(piston.Id),
                showStroke);
        }
    }

    private static double DistanceBetween(NodeDef a, NodeDef b)
    {
        var dx = b.Position.X - a.Position.X;
        var dy = b.Position.Y - a.Position.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Each sensor as a picture at the middle of its beam (#576), upright on the beam's built up
    /// side: the Accelerometer with its weight where <see cref="BuildSensorMotion"/> has it, and
    /// the camera looking along its rays (drawn later, over the joints).
    /// </summary>
    private void DrawSensors(PartSet selected)
    {
        var viewTransform = ViewTransform();
        foreach (var sensor in _viewModel!.Sensors)
        {
            var beam = _viewModel.Beams[_viewModel.BeamIndexOf(sensor.BeamId)];
            DrawSensor(beam, sensor.Kind, sensor.Aim, sensor.Id, selected.Sensors.Contains(sensor.Id), viewTransform);
        }

        // The sensor a tray drag would place on the free beam under the finger (#376).
        if (_partDrag is { } part && PartTray.SensorKindOf(part) is { } kind
            && _dropHover is { Kind: CreatureElementKind.Beam } hover
            && _viewModel.CanPlacePart(part, hover, out _))
        {
            DrawSensor(_viewModel.Beams[_viewModel.BeamIndexOf(hover.Id)], kind, null, null, false, viewTransform);
        }
    }

    /// <summary>The Camera's rays while its aim can be set, over the joints so the creature never hides them (#623).</summary>
    private void DrawSelectedCameraRays()
    {
        if (_viewModel!.AimableCameraId is not { } id
            || _viewModel.Sensors.Single(sensor => sensor.Id == id) is not { Kind: SensorKind.Camera } camera)
        {
            return;
        }

        var beam = _viewModel.Beams[_viewModel.BeamIndexOf(camera.BeamId)];
        var nodeA = NodeById(beam.NodeA).Position;
        var nodeB = NodeById(beam.NodeB).Position;
        var middle = (ToGodot(nodeA) + ToGodot(nodeB)) / 2;
        var beamRotation = (float)CameraRays.BeamAngle(nodeA, nodeB);
        var aim = camera.Aim ?? CameraRays.DefaultAim(nodeA, nodeB);
        SensorDrawing.DrawRays(this, ViewTransform(), Theme, middle, Enumerable.Range(0, CameraRays.RayCount)
            .Select(ray => middle + ToGodot(CameraRays.LocalRayTarget(ray, aim)).Rotated(beamRotation)));
    }

    private void DrawSensor(BeamDef beam, SensorKind kind, double? aim, int? sensorId, bool selected, Transform2D viewTransform)
    {
        var nodeA = NodeById(beam.NodeA).Position;
        var nodeB = NodeById(beam.NodeB).Position;
        var start = ToGodot(nodeA);
        var end = ToGodot(nodeB);
        var beamRotation = (float)CameraRays.BeamAngle(nodeA, nodeB);
        var upSign = Accelerometer.UpSign(nodeA, nodeB);
        var pictureRotation = beamRotation + (upSign == 1 ? Mathf.Pi : 0);
        var middle = (start + end) / 2;
        var cameraAim = aim ?? CameraRays.DefaultAim(nodeA, nodeB);
        var pictureTransform = viewTransform * new Transform2D(pictureRotation, middle);
        if (kind == SensorKind.Accelerometer)
        {
            var weight = (sensorId is { } id ? _sensorMotion.WeightOffset(id) : null)
                ?? Accelerometer.RestWeightOffset(beamRotation, upSign);
            SensorDrawing.DrawAccelerometer(this, pictureTransform, Theme, weight, selected);
        }
        else
        {
            SensorDrawing.DrawCamera(this, pictureTransform, Theme, Vector2.FromAngle((float)cameraAim + beamRotation - pictureRotation), selected);
        }

        DrawThroughView();
    }

    /// <summary>
    /// While a tray part is dragged (#376), a beam that would take it shows the <c>halo</c>, and
    /// one that would refuse it a dashed <c>danger</c> stroke, both under the beam.
    /// </summary>
    private void DrawPlacingFeedback(BeamDef beam, Vector2 start, Vector2 end)
    {
        if (_partDrag is not { } part || start == end)
        {
            return;
        }

        var width = Stroke(Theme.BeamWidth * 2.2f);
        using var pen = ViewPen();
        if (_viewModel!.CanPlacePart(part, new CreatureElementSelection(CreatureElementKind.Beam, beam.Id), out _))
        {
            pen.Line(start, end, Theme.SelectionGlow, width);
        }
        else
        {
            pen.DashedLine(start, end, Theme.Danger, width, dash: Theme.BeamWidth * 2);
        }
    }

    /// <summary>Where each Accelerometer's beam is now, in creature units, for <see cref="BuildSensorMotion"/>.</summary>
    private List<SensorPose> AccelerometerPoses()
    {
        var poses = new List<SensorPose>();
        foreach (var sensor in _viewModel!.Sensors)
        {
            if (sensor.Kind != SensorKind.Accelerometer)
            {
                continue;
            }

            var beam = _viewModel.Beams[_viewModel.BeamIndexOf(sensor.BeamId)];
            var nodeA = NodeById(beam.NodeA).Position;
            var nodeB = NodeById(beam.NodeB).Position;
            var midpoint = new Vector2D((nodeA.X + nodeB.X) / 2, (nodeA.Y + nodeB.Y) / 2);
            poses.Add(new SensorPose(sensor.Id, midpoint, Math.Atan2(nodeB.Y - nodeA.Y, nodeB.X - nodeA.X), Accelerometer.UpSign(nodeA, nodeB)));
        }

        return poses;
    }

    /// <summary>
    /// The Select frame, corner squares and rotate stem, drawn last at screen size in window
    /// pixels, turned with the group.
    /// </summary>
    private void DrawSelectionFrame()
    {
        if (_gestures!.SelectionFrame is not { } frame)
        {
            return;
        }

        var view = _gestures.View;
        var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity);
        // The canvas node is scaled in the scene; a screen-size length is this many of its units.
        var unit = 1 / Scale.X;
        var width = UiSize.Stroke.SelectionFrame * unit * UiPixelSpace.ScaleOf(toPixels);
        var rect = RectFromPoints(ToGodot(view.ToView(frame.Min)), ToGodot(view.ToView(frame.Max)));
        var center = rect.GetCenter();
        var turned = toPixels * new Transform2D((float)_gestures.SelectionFrameAngle, center) * new Transform2D(0, -center);
        UiDashedBorder.DrawRoundedRect(this, rect, UiSize.Radius.Small * unit, Theme.SelectionGlow, width, turned, _frameDash * unit, _frameGap * unit);

        var squareSize = Vector2.One * (_frameCornerSquare * unit);
        foreach (var corner in _gestures.FrameCornerSquares)
        {
            var square = new Rect2(ToGodot(view.ToView(corner)) - (squareSize / 2), squareSize);
            var outline = UiDashedBorder.RoundedRectPoints(square, _frameCornerRadius * unit).Select(point => turned * point).ToArray();
            DrawColoredPolygon(outline[..^1], Theme.SelectionCornerFill);
            DrawPolyline(outline, Theme.SelectionGlow, width, antialiased: true);
        }

        if (_gestures.RotateStem is { } stem)
        {
            DrawLine(toPixels * ToGodot(view.ToView(stem.From)), toPixels * ToGodot(view.ToView(stem.To)), Theme.SelectionGlow, width, antialiased: true);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }

    /// <summary>
    /// Hands each canvas note to the callout layer at its part, in the slot's coordinates: a beam's
    /// note leaves from its middle on its upper side, past its joints; a node's from above it.
    /// </summary>
    private void LayoutCanvasNotes()
    {
        if (CalloutLayer is null)
        {
            return;
        }

        if (_viewModel is null || _gestures is null)
        {
            CalloutLayer.SetCallouts([]);
            return;
        }

        var placements = new List<UiCalloutLayout.Placement>();
        foreach (var note in _viewModel.CanvasNotes())
        {
            if (TryPlaceNote(note, out var anchor, out var direction, out var clearance))
            {
                placements.Add(new UiCalloutLayout.Placement(anchor, direction, clearance, CalloutKindOf(note.Kind), IconOf(note.Kind), note.Text));
            }
        }

        CalloutLayer.SetCallouts(placements);
    }

    private bool TryPlaceNote(CanvasNote note, out Vector2 anchor, out Vector2 direction, out float clearance)
    {
        var view = _gestures!.View;
        Vector2 ToSlot(Vector2D position) => Transform * ToGodot(view.ToView(position));
        float OnScreen(double length) => (float)(length * view.Zoom) * Scale.X;
        switch (note.Target.Kind)
        {
            case CreatureElementKind.Beam or CreatureElementKind.Piston:
                var (nodeA, nodeB) = note.Target.Kind == CreatureElementKind.Beam
                    ? (_viewModel!.Beams[_viewModel.BeamIndexOf(note.Target.Id)].NodeA, _viewModel.Beams[_viewModel.BeamIndexOf(note.Target.Id)].NodeB)
                    : (_viewModel!.Pistons[_viewModel.PistonIndexOf(note.Target.Id)].NodeA, _viewModel.Pistons[_viewModel.PistonIndexOf(note.Target.Id)].NodeB);
                var a = NodeById(nodeA);
                var b = NodeById(nodeB);
                var start = ToSlot(a.Position);
                var end = ToSlot(b.Position);
                anchor = (start + end) / 2;
                direction = (end - start).Normalized().Orthogonal();
                if (direction.Y > 0 || (Mathf.IsZeroApprox(direction.Y) && direction.X < 0))
                {
                    direction = -direction;
                }

                clearance = OnScreen(Math.Max(a.Radius, b.Radius));
                return true;
            case CreatureElementKind.Node:
                var node = NodeById(note.Target.Id);
                anchor = ToSlot(node.Position);
                direction = Vector2.Up;
                clearance = OnScreen(node.Radius);
                return true;
            default:
                anchor = direction = Vector2.Zero;
                clearance = 0;
                return false;
        }
    }

    private static UiCallout.CalloutKind CalloutKindOf(CanvasNoteKind kind) => kind switch
    {
        CanvasNoteKind.Danger => UiCallout.CalloutKind.Danger,
        CanvasNoteKind.Ok => UiCallout.CalloutKind.Ok,
        _ => UiCallout.CalloutKind.Warning,
    };

    private static UiIconId IconOf(CanvasNoteKind kind) => kind == CanvasNoteKind.Ok ? UiIconId.None : UiIconId.Warn;

    /// <summary>Shows the handles <see cref="BuildGestures.SelectionHandles"/> lists, centred on their spots, and hides the rest.</summary>
    private void LayoutSelectionHandles()
    {
        var shown = _gestures?.SelectionHandles ?? [];
        foreach (var (handle, control) in new[] { (SelectionHandle.Move, MoveHandle), (SelectionHandle.Rotate, RotateHandle), (SelectionHandle.Scale, ScaleHandle), (SelectionHandle.Aim, AimHandle) })
        {
            if (control is null)
            {
                continue;
            }

            var index = shown.ToList().FindIndex(entry => entry.Handle == handle);
            control.Visible = index >= 0;
            if (index >= 0)
            {
                control.Position = (Transform * ToGodot(_gestures!.View.ToView(shown[index].Position))) - (control.Size / 2);
            }

            if (handle == SelectionHandle.Scale)
            {
                // The Scale glyph's arrows run along the frame's diagonal, so they turn with it.
                control.PivotOffset = control.Size / 2;
                control.Rotation = (float)(_gestures?.SelectionFrameAngle ?? 0);
            }
        }
    }

    private void DrawBeamPreview()
    {
        if (_viewModel is null || _gestures?.BeamStartNodeId is not { } start || _gestures.BeamEnd is not { } end)
        {
            return;
        }

        // A Piston drag over a joint that would refuse it turns danger (#451).
        var refused = _gestures.RefusedTargetNodeId;
        var from = NodeById(start);
        var target = (_gestures.BeamTargetNodeId ?? refused) is { } id ? NodeById(id) : null;
        var to = target?.Position ?? end;
        var color = refused is null ? Theme.SelectionGlow : Theme.Danger;
        // Like a beam, it starts at the joint's ring, and ends at the target's ring or the finger.
        if (JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(from.Position), (float)from.Radius, ToGodot(to), (float)(target?.Radius ?? 0)) is (var lineStart, var lineEnd))
        {
            using var pen = ViewPen();
            pen.DashedLine(lineStart, lineEnd, color, Stroke(Theme.BeamWidth), 8);
        }
    }

    private void DrawBeamEndRings()
    {
        if (_viewModel is null || _gestures is null)
        {
            return;
        }

        using var pen = ViewPen();
        foreach (var nodeId in new[] { _gestures.BeamStartNodeId, _gestures.BeamTargetNodeId })
        {
            if (nodeId is { } id)
            {
                var node = NodeById(id);
                pen.Ring(ToGodot(node.Position), (float)node.Radius * 1.65f, Theme.SelectionGlow, Stroke(Theme.MotorSignalWidth));
            }
        }

        if (_gestures.RefusedTargetNodeId is { } refused)
        {
            var node = NodeById(refused);
            var radius = (float)node.Radius * 1.65f;
            for (var dash = 0; dash < _refusedRingDashes; dash++)
            {
                var from = dash * Mathf.Tau / _refusedRingDashes;
                pen.Arc(ToGodot(node.Position), radius, from, from + (Mathf.Tau / _refusedRingDashes / 2), _refusedDashSegments, Theme.Danger, Stroke(Theme.MotorSignalWidth));
            }
        }
    }

    private void DrawSelectionBox()
    {
        if (_gestures?.SelectionBox is not { } box)
        {
            return;
        }

        // At screen size like the Select frame, in window pixels so its edges are smooth.
        var viewTransform = ViewTransform();
        var rect = RectFromPoints(viewTransform * ToGodot(box.Start), viewTransform * ToGodot(box.End));
        var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity);
        var unit = 1 / Scale.X;
        var radius = UiSize.Radius.Small * unit;
        if (rect.HasArea())
        {
            var fill = UiDashedBorder.RoundedRectPoints(rect, radius).Select(point => toPixels * point).ToArray();
            DrawColoredPolygon(fill[..^1], Theme.SelectionFill);
        }

        var width = UiSize.Stroke.SelectionFrame * unit * UiPixelSpace.ScaleOf(toPixels);
        UiDashedBorder.DrawRoundedRect(this, rect, radius, Theme.SelectionGlow, width, toPixels, _frameDash * unit, _frameGap * unit);
        DrawSetTransformMatrix(viewTransform);
    }

    /// <summary>The rigid hatch goes under the beams, so it shows only between them.</summary>
    private void DrawRigidTriangles()
    {
        if (!TryBuildDrawableTopology(out var creature) || creature is null)
        {
            return;
        }

        foreach (var triangle in RigidTriangles.Of(creature))
        {
            DrawRigidTriangle(creature, triangle);
        }
    }

    private bool TryBuildDrawableTopology(out CreatureDef? creature)
    {
        creature = null;
        if (_viewModel is null)
        {
            return false;
        }

        var drawableBeamSourceIndices = new List<int>();
        var drawableNodeSourceIndices = new SortedSet<int>();
        for (var beamIndex = 0; beamIndex < _viewModel.Beams.Count; beamIndex++)
        {
            var beam = _viewModel.Beams[beamIndex];
            if (NodeById(beam.NodeA).Position == NodeById(beam.NodeB).Position)
            {
                continue;
            }

            drawableBeamSourceIndices.Add(beamIndex);
            drawableNodeSourceIndices.Add(_viewModel.NodeIndexOf(beam.NodeA));
            drawableNodeSourceIndices.Add(_viewModel.NodeIndexOf(beam.NodeB));
        }

        if (drawableBeamSourceIndices.Count == 0)
        {
            return false;
        }

        var nodes = drawableNodeSourceIndices
            .Select(sourceIndex => _viewModel.Nodes[sourceIndex])
            .ToArray();
        var beams = drawableBeamSourceIndices
            .Select(beamIndex => _viewModel.Beams[beamIndex])
            .ToArray();
        var drawableBeamIds = drawableBeamSourceIndices
            .Select(beamIndex => _viewModel.Beams[beamIndex].Id)
            .ToHashSet();
        var sensors = _viewModel.Sensors
            .Where(sensor => drawableBeamIds.Contains(sensor.BeamId))
            .ToArray();

        try
        {
            creature = new CreatureDef(nodes, beams, sensors);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void DrawRigidTriangle(CreatureDef creature, RigidTriangleDef triangle)
    {
        var a = ToGodot(creature.Nodes[triangle.NodeA].Position);
        var b = ToGodot(creature.Nodes[triangle.NodeB].Position);
        var c = ToGodot(creature.Nodes[triangle.NodeC].Position);
        // Every joint is plain today, so all three share one radius.
        foreach (var (start, end) in TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing, (float)creature.Nodes[triangle.NodeA].Radius))
        {
            DrawLine(start, end, Theme.RigidHatch, -1);
        }
    }

    private void DrawInvalidNodeMarkers()
    {
        if (_viewModel is null)
        {
            return;
        }

        using var pen = ViewPen();
        foreach (var node in _viewModel.Nodes)
        {
            if (!ShowsAsLoose(node.Id))
            {
                continue;
            }

            var position = ToGodot(node.Position);
            var radius = (float)node.Radius * 1.55f;
            pen.Ring(position, radius, Theme.Danger, Stroke(Theme.MotorSignalWidth));
            pen.Line(position + new Vector2(-radius * 0.45f, -radius * 0.45f), position + new Vector2(radius * 0.45f, radius * 0.45f), Theme.Danger, Stroke(Theme.MotorSignalWidth));
            pen.Line(position + new Vector2(radius * 0.45f, -radius * 0.45f), position + new Vector2(-radius * 0.45f, radius * 0.45f), Theme.Danger, Stroke(Theme.MotorSignalWidth));
        }
    }

    // Joined to nothing. The beam drag's own rings replace the warning on the joints being joined.
    private bool ShowsAsLoose(int nodeId) =>
        !_viewModel!.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
        && !_viewModel.Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId)
        && nodeId != _gestures?.BeamStartNodeId
        && nodeId != _gestures?.BeamTargetNodeId;

    private void DrawInvalidBeamMarkers()
    {
        if (_viewModel is null)
        {
            return;
        }

        using var pen = ViewPen();
        foreach (var beam in _viewModel.Beams)
        {
            var start = NodeById(beam.NodeA);
            var end = NodeById(beam.NodeB);
            if (start.Position != end.Position)
            {
                continue;
            }

            var position = ToGodot(start.Position);
            var radius = (float)Math.Max(start.Radius, end.Radius) * 1.95f;
            pen.Ring(position, radius, Theme.Danger, Stroke(Theme.MotorSignalWidth));
            pen.Line(position + new Vector2(-radius * 0.55f, 0), position + new Vector2(radius * 0.55f, 0), Theme.Danger, Stroke(Theme.MotorSignalWidth));
            pen.Line(position + new Vector2(0, -radius * 0.55f), position + new Vector2(0, radius * 0.55f), Theme.Danger, Stroke(Theme.MotorSignalWidth));
        }
    }

    private static Rect2 RectFromPoints(Vector2 first, Vector2 second)
    {
        var min = new Vector2(Mathf.Min(first.X, second.X), Mathf.Min(first.Y, second.Y));
        var max = new Vector2(Mathf.Max(first.X, second.X), Mathf.Max(first.Y, second.Y));
        return new Rect2(min, max - min);
    }

    private void OnAnatomyChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnGesturesChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(BuildViewModel.SelectedNodeCount) or nameof(BuildViewModel.PlacementNote))
        {
            QueueRedraw();
        }

        if (eventArgs.PropertyName == nameof(BuildViewModel.ActiveTool))
        {
            _gestures?.Cancel();
            QueueRedraw();
        }
    }

    /// <summary>
    /// Tells the view what part of it the clipping slot shows, and fits the
    /// creation the first time that is known: by the first draw the host has
    /// loaded the creation and the containers have sized the slot. A slot
    /// resize (such as the Parts panel collapsing) redraws, so the view's
    /// limits follow at once.
    /// </summary>
    private void UpdateView()
    {
        if (_slot is not { } slot || slot.Size == Vector2.Zero)
        {
            return;
        }

        var toView = Transform.AffineInverse();
        _gestures!.View.UiScale = UiScale.FactorOf(this);
        _gestures.View.VisibleArea = new CanvasRect(ToDomain(toView * Vector2.Zero), ToDomain(toView * slot.Size));
        if (!_viewFitted)
        {
            _viewFitted = true;
            _gestures.View.Fit();
        }
    }

    /// <summary>
    /// A faint blueprint grid over the Build area, the only place joints can
    /// go: fixed <see cref="BuildViewModel.BuildGridStep"/> cells that
    /// zoom with the picture, drawn as hairlines that stay one pixel wide.
    /// </summary>
    private void DrawBuildGrid()
    {
        var view = _gestures!.View;
        var area = BuildViewModel.BuildArea;
        var step = BuildViewModel.BuildGridStep;

        var shown = view.VisibleArea is { } visible
            ? new CanvasRect(view.ToCanvas(visible.Min), view.ToCanvas(visible.Max))
            : area;
        var top = (float)area.Min.Y;
        var bottom = (float)area.Max.Y;
        var left = (float)area.Min.X;
        var right = (float)area.Max.X;
        for (var x = area.Min.X; x <= area.Max.X; x += step)
        {
            if (x >= shown.Min.X && x <= shown.Max.X)
            {
                DrawLine(new Vector2((float)x, top), new Vector2((float)x, bottom), Theme.ArenaGrid, -1);
            }
        }

        for (var y = area.Min.Y; y <= area.Max.Y; y += step)
        {
            if (y >= shown.Min.Y && y <= shown.Max.Y)
            {
                DrawLine(new Vector2(left, (float)y), new Vector2(right, (float)y), Theme.ArenaGrid, -1);
            }
        }

        DrawRect(new Rect2(left, top, right - left, bottom - top), Theme.ArenaGrid, filled: false, width: -1);
    }

    /// <summary>Marks the corners of the Build area, zooming with the rest of the picture.</summary>
    private void DrawAreaCorners()
    {
        var area = BuildViewModel.BuildArea;
        var topLeft = ToGodot(area.Min);
        var bottomRight = ToGodot(area.Max);
        // Two grid cells, so the marks end on a grid line.
        var length = (float)(2 * BuildViewModel.BuildGridStep);
        using var pen = ViewPen();
        foreach (var (corner, inward) in new[]
        {
            (topLeft, new Vector2(1, 1)),
            (new Vector2(bottomRight.X, topLeft.Y), new Vector2(-1, 1)),
            (new Vector2(topLeft.X, bottomRight.Y), new Vector2(1, -1)),
            (bottomRight, new Vector2(-1, -1)),
        })
        {
            pen.Polyline(
                [corner + new Vector2(inward.X * length, 0), corner, corner + new Vector2(0, inward.Y * length)],
                Theme.AreaCorner,
                Stroke(Theme.AreaCornerWidth));
        }
    }

    /// <summary>Draws everything after this in canvas units, zoomed and panned by the view.</summary>
    private void DrawThroughView() => DrawSetTransformMatrix(ViewTransform());

    /// <summary>Draws strokes given in creature units in window pixels, so they stay smooth at any zoom (#733).</summary>
    private UiPixelPen ViewPen() => UiPixelPen.Begin(this, ViewTransform());

    /// <summary>The map from creature units to the canvas: the view's zoom, then its offset.</summary>
    private Transform2D ViewTransform() =>
        new(0, Vector2.One * (float)_gestures!.View.Zoom, 0, ToGodot(_gestures.View.Offset));

    /// <summary>
    /// The width every line is drawn at (`docs/UI_DIRECTION.md` → Departures
    /// from the reference); to keep lines at their screen width instead, return
    /// <c>width / (float)_gestures.View.Zoom</c> here.
    /// </summary>
    private float Stroke(float width) => width;

    private Vector2D ToView(Vector2 screenPosition) => ToDomain(ToCanvasLocal(screenPosition));

    private Vector2 ToCanvasLocal(Vector2 screenPosition)
    {
        // Regular ToLocal()/GetGlobalTransform() do not include the
        // viewport's stretch transform (see [display] in project.godot), so
        // raw screen-pixel input positions need GetGlobalTransformWithCanvas
        // to land on the right spot. Mirrors TrainingHost._UnhandledInput's
        // creature-selection math.
        return GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
    }

    private static Vector2D ToDomain(Vector2 position)
    {
        return new Vector2D(position.X, position.Y);
    }

    private NodeDef NodeById(int nodeId)
    {
        return _viewModel!.Nodes[_viewModel.NodeIndexOf(nodeId)];
    }

    private static Vector2 ToGodot(Vector2D position)
    {
        return new Vector2((float)position.X, (float)position.Y);
    }
}

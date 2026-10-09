using System.ComponentModel;
using Godot;
using NodeRunner.App.Builders;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
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
/// It lives in the world of a <see cref="UiWorldView"/> (#769), so its creature is drawn with the
/// same part visuals and layers as Training's (<see cref="CreatureParts"/>) without any of them
/// drawing over the handles and notes in the slot. Its own marks go under the creature (grid,
/// Select box, beam preview) or on a <see cref="ViewLayer"/> under the links or over the creature.
/// </summary>
public partial class BuildCanvas : Node2D
{
    private const int _mousePointer = -1;

    // The reference's Select frame and box: dashes 5 on 4 off, and 10-square corners rounded 2.
    private const float _frameDash = 5;
    private const float _frameGap = 4;
    private const float _frameCornerSquare = 10;
    private const float _frameCornerRadius = 2;

    private BuildViewModel? _viewModel;
    private BuildGestures? _gestures;
    private bool _viewFitted;
    private Control? _slot;
    private readonly CreatureParts _creature = new();
    private readonly ViewLayer _underlay = ViewLayer.Underlay();
    private readonly ViewLayer _overlay = ViewLayer.Overlay();
    private readonly BuildSensorMotion _sensorMotion = new();
    private double _gravity;

    public BuildCanvas()
    {
        AddChild(_creature, @internal: InternalMode.Front);
        AddChild(_underlay, @internal: InternalMode.Front);
        AddChild(_overlay, @internal: InternalMode.Front);
        _underlay.Draw += () => DrawUnderlay(_underlay);
        _overlay.Draw += () => DrawOverlay(_overlay);
    }

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    /// <summary>The view this canvas is the world of; its parent is the slot the handles and notes share.</summary>
    [Export]
    public UiWorldView? WorldView { get; set; }

    /// <summary>The selection handles, authored in the slot above the canvas; placed here, hit-tested by <see cref="BuildGestures"/>.</summary>
    [Export]
    public UiSelectionHandle? MoveHandle { get; set; }

    [Export]
    public UiSelectionHandle? RotateHandle { get; set; }

    [Export]
    public UiSelectionHandle? ScaleHandle { get; set; }

    /// <summary>Turns a selected Camera (#594); placed and hit-tested like the selection handles.</summary>
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

    // The canvas notes are laid out, in the player's language, as the canvas draws.
    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            QueueRedraw();
        }
    }

    public override void _EnterTree()
    {
        if (WorldView is null)
        {
            GD.PushError($"{Name} needs its WorldView: the UiWorldView it is the world of (#769).");
            return;
        }

        _slot = WorldView.GetParent() as Control;
        WorldView.Resized += QueueRedraw;
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
        // Godot drags in the window, not in the world this canvas lives in.
        var viewport = GetTree().Root;
        BuildPart? part = viewport.GuiIsDragging() && TryReadPartDrag(viewport.GuiGetDragData(), out var dragged) && !_viewModel!.IsLocked
            ? dragged
            : null;
        CreatureElementSelection? hover = null;
        if (part is not null && _slot is { } slot && _gestures is { } gestures
            && new Rect2(Vector2.Zero, slot.Size).HasPoint(slot.GetLocalMousePosition()))
        {
            hover = gestures.DropTargetAt(ToDomain(SlotTransform().AffineInverse() * slot.GetLocalMousePosition()));
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
        _viewModel is { IsLocked: false } && _gestures is not null && TryReadPartDrag(data, out _);

    /// <summary>Places the dropped part where it landed; a refused drop's note goes after a moment, or at the next touch.</summary>
    private void DropPart(Vector2 atPosition, Variant data)
    {
        if (_gestures is null || !TryReadPartDrag(data, out var part))
        {
            return;
        }

        _gestures.DropPart(part, ToDomain(SlotTransform().AffineInverse() * atPosition));
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
        if (WorldView is not null)
        {
            WorldView.Resized -= QueueRedraw;
        }

        _slot = null;

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
        _underlay.QueueRedraw();
        _overlay.QueueRedraw();
        if (_viewModel is null || _gestures is null)
        {
            LayoutSelectionHandles();
            LayoutCanvasNotes();
            return;
        }

        UpdateView();
        LayoutSelectionHandles();
        LayoutCanvasNotes();
        ShowCreature();
        DrawBuildGrid();
        DrawAreaCorners();
        DrawSelectionBox();
    }

    /// <summary>Hands the creature its parts as they are now, through the view's zoom and pan.</summary>
    private void ShowCreature()
    {
        // A dragged Select box shows what it would catch; the selection is cleared meanwhile (#704).
        if (_viewModel is null || _gestures is null)
        {
            return;
        }

        var caught = _gestures.SelectionBoxCatches;
        var selected = caught.Count > 0 ? caught : _viewModel.Selection;
        // A sensor being moved looks selected, so its preview never reads as a copy (#806).
        if (_gestures.MovingSensorId is { } moving && !selected.Sensors.Contains(moving))
        {
            selected = selected with { Sensors = new HashSet<int>(selected.Sensors) { moving } };
        }

        (BeamDef, SensorKind, double?)? previewSensor = null;
        int? previewServoNode = null;
        if (_gestures.MovedSensorPreview is { } moved)
        {
            previewSensor = (_viewModel.Beams[_viewModel.BeamIndexOf(moved.BeamId)], moved.Kind, moved.Aim);
        }
        else if (_partDrag is { } part && PartTray.SensorKindOf(part) is { } kind
            && _dropHover is { Kind: CreatureElementKind.Beam } hover
            && _viewModel.CanPlacePart(part, hover, out _))
        {
            previewSensor = (_viewModel.Beams[_viewModel.BeamIndexOf(hover.Id)], kind, null);
        }
        else if (_partDrag == BuildPart.Servo
            && _dropHover is { Kind: CreatureElementKind.Node } servoHover
            && _viewModel.CanPlacePart(BuildPart.Servo, servoHover, out _))
        {
            previewServoNode = servoHover.Id;
        }

        _creature.Transform = ViewTransform();
        _creature.Theme = Theme;
        _creature.Show(
            new CreatureShape(_viewModel.Nodes, _viewModel.Beams, _viewModel.Servos, _viewModel.Pistons, _viewModel.Springs, _viewModel.Sensors),
            new CreatureMarks(
                selected,
                ShowsAsLoose,
                _sensorMotion.WeightOffset,
                previewSensor,
                previewServoNode,
                ShowsTooShort: true));
    }

    /// <summary>What goes under the links: the placing feedback of a tray part dragged or picked or a sensor moved, and the beam a link drag replaces.</summary>
    private void DrawUnderlay(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures is null)
        {
            return;
        }

        BuildServoDrawing.DrawSelectedBands(canvas, _viewModel, Theme, ViewTransform());
        DrawReplacedBeam(canvas);
        foreach (var beam in _viewModel.Beams)
        {
            var nodeA = NodeById(beam.NodeA);
            var nodeB = NodeById(beam.NodeB);
            var (start, end) = JointDrawing.BeamSpan(Theme.JointRingWidth, ToGodot(nodeA.Position), (float)_viewModel.NodeRadius(nodeA.Id), ToGodot(nodeB.Position), (float)_viewModel.NodeRadius(nodeB.Id))
                ?? (ToGodot(nodeA.Position), ToGodot(nodeB.Position));
            DrawPlacingFeedback(canvas, beam, start, end);
        }

        BuildServoDrawing.DrawPlacingFeedback(canvas, _viewModel, PlacingPart, Theme, ViewTransform());
    }

    /// <summary>What goes over the whole creature: the aimed camera's rays, warnings, the beam drag's rings and the selection frame.</summary>
    private void DrawOverlay(CanvasItem canvas)
    {
        if (_viewModel is null || _gestures is null)
        {
            return;
        }
        DrawSelectedCameraRays(canvas);
        DrawSelectedTravels(canvas);
        DrawInvalidNodeMarkers(canvas);
        DrawInvalidBeamMarkers(canvas);
        DrawBeamPreview(canvas);
        DrawBeamEndRings(canvas);
        DrawSensorMoveLine(canvas);
        DrawSelectionFrame(canvas);
    }

    /// <summary>The Camera's rays while its aim can be set, over the joints so the creature never hides them (#623).</summary>
    private void DrawSelectedCameraRays(CanvasItem canvas)
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
        var aim = camera.Aim ?? SensorDef.DefaultAim(nodeA, nodeB);
        SensorDrawing.DrawRays(canvas, ViewTransform(), Theme, middle, Enumerable.Range(0, CameraRays.RayCount)
            .Select(ray => middle + ToGodot(CameraRays.LocalRayTarget(ray, aim)).Rotated(beamRotation)));
    }

    /// <summary>
    /// A selected Piston's or Spring's shortest and longest length while its Stroke, Start position
    /// or Coil length can be set (#704, #870, #835), and a Spring's rest length, over the joints so the
    /// one at joint B is never hidden.
    /// </summary>
    private void DrawSelectedTravels(CanvasItem canvas)
    {
        var stroke = _viewModel!.CanEdit(PartParameterId.Stroke);
        if (stroke || _viewModel.CanEdit(PartParameterId.StartPosition))
        {
            var selected = _viewModel.Selection.Pistons;
            foreach (var piston in _viewModel.Pistons.Where(piston => selected.Contains(piston.Id)))
            {
                var (built, radii) = (BuiltLength(piston.NodeA, piston.NodeB), JointRadii(piston.NodeA, piston.NodeB));
                DrawTravel(canvas, piston.NodeA, piston.NodeB, Piston.ShortestLength(piston, built, radii), Piston.LongestLength(piston, built, radii), PistonDrawing.TickHalf(Theme), rest: null);
            }
        }

        if (stroke || _viewModel.CanEdit(PartParameterId.CoilLength))
        {
            var selected = _viewModel.Selection.Springs;
            foreach (var spring in _viewModel.Springs.Where(spring => selected.Contains(spring.Id)))
            {
                var (built, radii) = (BuiltLength(spring.NodeA, spring.NodeB), JointRadii(spring.NodeA, spring.NodeB));
                DrawTravel(canvas, spring.NodeA, spring.NodeB, Spring.ShortestLength(spring, built, radii), Spring.LongestLength(spring, built, radii), SpringDrawing.SeatHalf(Theme), Spring.RestLength(spring, built, radii));
            }
        }
    }

    private double JointRadii(int nodeA, int nodeB) => _viewModel!.NodeRadius(nodeA) + _viewModel.NodeRadius(nodeB);

    private double BuiltLength(int nodeA, int nodeB)
    {
        var a = NodeById(nodeA).Position;
        var b = NodeById(nodeB).Position;
        return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    }

    private void DrawTravel(CanvasItem canvas, int nodeA, int nodeB, double shortest, double longest, float tickHalf, double? rest) =>
        PistonDrawing.DrawStroke(canvas, ViewTransform(), Theme, ToGodot(NodeById(nodeA).Position), ToGodot(NodeById(nodeB).Position), (float)shortest, (float)longest, tickHalf, (float?)rest);

    /// <summary>
    /// While a tray part is dragged (#376) or picked (#1016), or a sensor is moved (#806), a beam that would take it
    /// shows the <c>halo</c>, and one that would refuse it a dashed <c>danger</c> stroke, both under the beam.
    /// </summary>
    private void DrawPlacingFeedback(CanvasItem canvas, BeamDef beam, Vector2 start, Vector2 end)
    {
        if (BeamTakesPlacing(beam.Id) is not { } takes || start == end)
        {
            return;
        }

        var width = Stroke(Theme.BeamWidth * 2.2f);
        using var pen = ViewPen(canvas);
        if (takes)
        {
            pen.Line(start, end, Theme.SelectionGlow, width);
        }
        else
        {
            pen.DashedLine(start, end, Theme.Danger, width, dash: Theme.BeamWidth * 2);
        }
    }

    /// <summary>Whether the beam would take the sensor a drag moves or the <see cref="PlacingPart"/>; null while neither is placed.</summary>
    private bool? BeamTakesPlacing(int beamId)
    {
        if (_gestures!.MovingSensorId is { } sensor)
        {
            return _viewModel!.BeamTakesMovingSensor(sensor, beamId);
        }

        return PlacingPart is { } part && part != BuildPart.Servo
            ? _viewModel!.CanPlacePart(part, new CreatureElementSelection(CreatureElementKind.Beam, beamId), out _)
            : null;
    }

    /// <summary>
    /// The tray part being placed: one dragged from the tray, or one picked to tap into place, which
    /// shows where it goes the same way from the moment it is picked (#1016). Null while a sensor is
    /// moved, so only the move's targets show.
    /// </summary>
    private BuildPart? PlacingPart => _gestures?.MovingSensorId is null ? _partDrag ?? _viewModel?.PickedPart : null;


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
    /// The selection frame, corner squares and rotate stem, drawn last at screen size in window
    /// pixels, turned with the group.
    /// </summary>
    private void DrawSelectionFrame(CanvasItem canvas)
    {
        if (_gestures!.SelectionFrame is not { } frame)
        {
            return;
        }

        var view = _gestures.View;
        var toPixels = UiPixelSpace.Enter(canvas, Transform2D.Identity);
        // The canvas node is scaled in the scene; a screen-size length is this many of its units.
        var unit = 1 / SlotTransform().Scale.X;
        var width = UiSize.Stroke.SelectionFrame * unit * UiPixelSpace.ScaleOf(toPixels);
        var rect = RectFromPoints(ToGodot(view.ToView(frame.Min)), ToGodot(view.ToView(frame.Max)));
        var center = rect.GetCenter();
        var turned = toPixels * new Transform2D((float)_gestures.SelectionFrameAngle, center) * new Transform2D(0, -center);
        UiDashedBorder.DrawRoundedRect(canvas, rect, UiSize.Radius.Small * unit, Theme.SelectionGlow, width, turned, _frameDash * unit, _frameGap * unit);

        var squareSize = Vector2.One * (_frameCornerSquare * unit);
        foreach (var corner in _gestures.FrameCornerSquares)
        {
            var square = new Rect2(ToGodot(view.ToView(corner)) - (squareSize / 2), squareSize);
            var outline = UiDashedBorder.RoundedRectPoints(square, _frameCornerRadius * unit).Select(point => turned * point).ToArray();
            canvas.DrawColoredPolygon(outline[..^1], Theme.SelectionCornerFill);
            canvas.DrawPolyline(outline, Theme.SelectionGlow, width, antialiased: true);
        }

        if (_gestures.RotateStem is { } stem)
        {
            canvas.DrawLine(toPixels * ToGodot(view.ToView(stem.From)), toPixels * ToGodot(view.ToView(stem.To)), Theme.SelectionGlow, width, antialiased: true);
        }

        canvas.DrawSetTransformMatrix(Transform2D.Identity);
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
                var text = UiTextTranslation.Source(note.Text)();
                placements.Add(new UiCalloutLayout.Placement(anchor, direction, clearance, CalloutKindOf(note.Kind), IconOf(note.Kind), text));
            }
        }

        CalloutLayer.SetCallouts(placements);
    }

    private bool TryPlaceNote(CanvasNote note, out Vector2 anchor, out Vector2 direction, out float clearance)
    {
        var view = _gestures!.View;
        var toSlot = SlotTransform();
        Vector2 ToSlot(Vector2D position) => toSlot * ToGodot(view.ToView(position));
        float OnScreen(double length) => (float)(length * view.Zoom) * toSlot.Scale.X;
        var joints = CanvasNoteTargets.JointIds(note.Target, _viewModel!);
        if (joints.Count == 2)
        {
            var a = NodeById(joints[0]);
            var b = NodeById(joints[1]);
            var start = ToSlot(a.Position);
            var end = ToSlot(b.Position);
            anchor = (start + end) / 2;
            direction = (end - start).Normalized().Orthogonal();
            if (direction.Y > 0 || (Mathf.IsZeroApprox(direction.Y) && direction.X < 0))
            {
                direction = -direction;
            }

            clearance = OnScreen(Math.Max(_viewModel!.NodeRadius(a.Id), _viewModel.NodeRadius(b.Id)));
            return true;
        }

        if (joints.Count == 1)
        {
            var node = NodeById(joints[0]);
            anchor = ToSlot(node.Position);
            direction = Vector2.Up;
            clearance = OnScreen(_viewModel!.NodeRadius(node.Id));
            return true;
        }

        anchor = direction = Vector2.Zero;
        clearance = 0;
        return false;
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
                control.Position = (SlotTransform() * ToGodot(_gestures!.View.ToView(shown[index].Position))) - (control.Size / 2);
            }

            if (handle == SelectionHandle.Scale)
            {
                // The Scale glyph's arrows run along the frame's diagonal, so they turn with it.
                control.PivotOffset = control.Size / 2;
                control.Rotation = (float)(_gestures?.SelectionFrameAngle ?? 0);
            }
        }
    }

    private void DrawSelectionBox()
    {
        if (_gestures?.SelectionBox is not { } box)
        {
            return;
        }

        // At screen size like the selection frame, in window pixels so its edges are smooth.
        var viewTransform = ViewTransform();
        var rect = RectFromPoints(viewTransform * ToGodot(box.Start), viewTransform * ToGodot(box.End));
        var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity);
        var unit = 1 / SlotTransform().Scale.X;
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

    private void DrawInvalidNodeMarkers(CanvasItem canvas)
    {
        if (_viewModel is null)
        {
            return;
        }

        using var pen = ViewPen(canvas);
        foreach (var node in _viewModel.Nodes)
        {
            if (!ShowsAsLoose(node.Id))
            {
                continue;
            }

            var position = ToGodot(node.Position);
            var radius = (float)_viewModel.NodeRadius(node.Id) * 1.55f;
            pen.Ring(position, radius, Theme.Danger, Stroke(Theme.MotorSignalWidth));
            pen.Line(position + new Vector2(-radius * 0.45f, -radius * 0.45f), position + new Vector2(radius * 0.45f, radius * 0.45f), Theme.Danger, Stroke(Theme.MotorSignalWidth));
            pen.Line(position + new Vector2(radius * 0.45f, -radius * 0.45f), position + new Vector2(-radius * 0.45f, radius * 0.45f), Theme.Danger, Stroke(Theme.MotorSignalWidth));
        }
    }

    // Joined to nothing. The beam drag's own rings replace the warning on the joints being joined.
    private bool ShowsAsLoose(int nodeId) =>
        _viewModel!.IsLoose(nodeId)
        && nodeId != _gestures?.BeamStartNodeId
        && nodeId != _gestures?.BeamTargetNodeId;

    private void DrawInvalidBeamMarkers(CanvasItem canvas)
    {
        if (_viewModel is null)
        {
            return;
        }

        using var pen = ViewPen(canvas);
        foreach (var beam in _viewModel.Beams)
        {
            var start = NodeById(beam.NodeA);
            var end = NodeById(beam.NodeB);
            if (start.Position != end.Position)
            {
                continue;
            }

            var position = ToGodot(start.Position);
            var radius = (float)Math.Max(_viewModel.NodeRadius(start.Id), _viewModel.NodeRadius(end.Id)) * 1.95f;
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
        if (eventArgs.PropertyName is nameof(BuildViewModel.SelectedNodeCount) or nameof(BuildViewModel.PlacementNote) or nameof(BuildViewModel.CanvasNotes) or nameof(BuildViewModel.PickedPart))
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

        var toView = SlotTransform().AffineInverse();
        _gestures!.View.UiScale = UiScale.FactorOf(this);
        _gestures.View.VisibleArea = new CanvasRect(ToDomain(toView * Vector2.Zero), ToDomain(toView * slot.Size));
        if (!_viewFitted)
        {
            _viewFitted = true;
            _gestures.View.Fit();
        }
    }

    /// <summary>The <see cref="BuildGrid"/> where the view shows it, zooming with the picture.</summary>
    private void DrawBuildGrid()
    {
        var view = _gestures!.View;
        var shown = view.VisibleArea is { } visible
            ? new CanvasRect(view.ToCanvas(visible.Min), view.ToCanvas(visible.Max))
            : BuildViewModel.BuildArea;
        BuildGrid.Draw(this, ViewTransform(), shown, Theme.ArenaGrid);
    }

    /// <summary>Marks the corners of the Build area, zooming with the rest of the picture.</summary>
    private void DrawAreaCorners()
    {
        var area = BuildViewModel.BuildArea;
        var topLeft = ToGodot(area.Min);
        var bottomRight = ToGodot(area.Max);
        // Two grid cells, so the marks end on a grid line.
        var length = (float)(2 * BuildViewModel.BuildGridStep);
        using var pen = ViewPen(this);
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

    /// <summary>Draws strokes given in creature units on <paramref name="canvas"/> in window pixels, so they stay smooth at any zoom (#733).</summary>
    private UiPixelPen ViewPen(CanvasItem canvas) => UiPixelPen.Begin(canvas, ViewTransform());

    /// <summary>
    /// The map from this canvas to the slot the handles and notes are laid out in: through the
    /// world view, which shows this canvas's world at the slot's scale.
    /// </summary>
    private Transform2D SlotTransform() =>
        WorldView!.GetTransform() * WorldView.LocalFromWorld * GetGlobalTransform();

    /// <summary>The map from creature units to the canvas: the view's zoom, then its offset.</summary>
    private Transform2D ViewTransform() =>
        new(0, Vector2.One * (float)_gestures!.View.Zoom, 0, ToGodot(_gestures.View.Offset));

    /// <summary>
    /// The width every line is drawn at (`docs/WORLD_VISUALS.md` → Lines and
    /// zoom); to keep lines at their screen width instead, return
    /// <c>width / (float)_gestures.View.Zoom</c> here.
    /// </summary>
    private float Stroke(float width) => width;

    private Vector2D ToView(Vector2 screenPosition) => ToDomain(ToCanvasLocal(screenPosition));

    private Vector2 ToCanvasLocal(Vector2 screenPosition)
    {
        // UiWorldView pushes input into the world viewport, which removes its stretch, so it arrives
        // in the world's own coordinates with the canvas transform still applied: plain ToLocal() skips that.
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

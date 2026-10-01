using System.ComponentModel;
using Godot;
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
    private const double _moveGhostSeconds = 1.8;
    private static readonly Vector2 _rigidLabelOffset = new(12, -12);
    private const string _rigidLabelText = "Rigid: no joints";
    private const float _rigidLabelPadding = UiSize.Space.S1;
    private const int _rigidLabelFontSize = 18;
    private const int _mousePointer = -1;

    private BuildViewModel? _viewModel;
    private BuildGestures? _gestures;
    private readonly Dictionary<int, Vector2D> _ghostNodePositions = [];
    private int _ghostVersion;
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
            ClearMoveGhosts();
            if (_viewModel is not null)
            {
                _viewModel.AnatomyChanged += OnAnatomyChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _gestures = new BuildGestures(_viewModel);
                _gestures.Changed += OnGesturesChanged;
                _gestures.View.Changed += OnGesturesChanged;
                _gestures.NodeDragStarting += OnNodeDragStarting;
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
            _gestures.NodeDragStarting -= OnNodeDragStarting;
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
                ScheduleMoveGhostClear();
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
            ScheduleMoveGhostClear();
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
        DrawMoveGhosts();
        DrawSelectionBox();
        DrawBeamPreview();

        foreach (var beam in _viewModel.Beams)
        {
            var nodeA = NodeById(beam.NodeA);
            var nodeB = NodeById(beam.NodeB);
            var start = ToGodot(nodeA.Position);
            var end = ToGodot(nodeB.Position);
            if (_viewModel.SingleSelectedBeamId == beam.Id)
            {
                DrawLine(start, end, Theme.SelectionGlow, Stroke(Theme.BeamWidth * 2.2f), antialiased: false);
            }

            DrawPlacingFeedback(beam, start, end);
            // A beam too short for training (#593) is drawn in danger until its joints move apart.
            var color = CreatureReadiness.IsTooShort(nodeA, nodeB) ? Theme.Danger : Theme.Beam;
            DrawLine(start, end, color, Stroke(Theme.BeamWidth), antialiased: false);
        }

        DrawTopologyFeedback();
        DrawSensors();

        for (var nodeIndex = 0; nodeIndex < _viewModel.Nodes.Count; nodeIndex++)
        {
            var node = _viewModel.Nodes[nodeIndex];
            var position = ToGodot(node.Position);
            if (_viewModel.SelectedNodeIds.Contains(node.Id))
            {
                DrawCircle(position, (float)(node.Radius * BuildGestures.SelectedHaloScale), Theme.SelectionGlow);
            }

            DrawCircle(
                position,
                (float)node.Radius * 1.18f,
                UiGlow.FromBase(Theme.GroundEdge, Theme.EffectsEnabled));
            DrawCircle(position, (float)node.Radius, Theme.NodeFill);
        }

        DrawInvalidNodeMarkers();
        DrawInvalidBeamMarkers();

        DrawMotorCenterMarkers();

        DrawBeamEndRings();
        DrawSelectionFrame();
    }

    /// <summary>
    /// Each sensor as a picture at the middle of its beam (#576), upright on the beam's built up
    /// side: the Accelerometer with its weight where <see cref="BuildSensorMotion"/> has it, and
    /// the LOS camera looking along its rays, which it draws when selected.
    /// </summary>
    private void DrawSensors()
    {
        var view = _gestures!.View;
        var viewTransform = new Transform2D(0, Vector2.One * (float)view.Zoom, 0, ToGodot(view.Offset));
        var rays = Enumerable.Range(0, LineOfSight.RayCount)
            .Select(ray => ToGodot(LineOfSight.LocalRayTarget(ray, 0)))
            .ToArray();
        foreach (var sensor in _viewModel!.Sensors)
        {
            var beam = _viewModel.Beams[_viewModel.BeamIndexOf(sensor.BeamId)];
            DrawSensor(beam, sensor.Kind, sensor.Id, _viewModel.SingleSelectedSensorId == sensor.Id, viewTransform, rays);
        }

        // The sensor a tray drag would place on the free beam under the finger (#376).
        if (_partDrag is { } part && PartTray.SensorKindOf(part) is { } kind
            && _dropHover is { Kind: CreatureElementKind.Beam } hover
            && _viewModel.CanPlacePart(part, hover, out _))
        {
            DrawSensor(_viewModel.Beams[_viewModel.BeamIndexOf(hover.Id)], kind, null, false, viewTransform, rays);
        }
    }

    private void DrawSensor(BeamDef beam, SensorKind kind, int? sensorId, bool selected, Transform2D viewTransform, Vector2[] rays)
    {
        var nodeA = NodeById(beam.NodeA).Position;
        var nodeB = NodeById(beam.NodeB).Position;
        var start = ToGodot(nodeA);
        var end = ToGodot(nodeB);
        var beamRotation = start == end ? 0 : (end - start).Angle();
        var upSign = Accelerometer.UpSign(nodeA, nodeB);
        var pictureRotation = beamRotation + (upSign == 1 ? Mathf.Pi : 0);
        var middle = (start + end) / 2;
        if (selected && kind == SensorKind.LineOfSight)
        {
            SensorDrawing.DrawRays(this, Theme, middle, rays.Select(ray => middle + ray));
        }

        DrawSetTransformMatrix(viewTransform * new Transform2D(pictureRotation, middle));
        if (kind == SensorKind.Accelerometer)
        {
            var weight = (sensorId is { } id ? _sensorMotion.WeightOffset(id) : null)
                ?? Accelerometer.RestWeightOffset(beamRotation, upSign);
            SensorDrawing.DrawAccelerometer(this, Theme, weight, selected);
        }
        else
        {
            SensorDrawing.DrawLos(this, Theme, SensorDrawing.Aim(rays).Rotated(-pictureRotation), selected);
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
        if (_viewModel!.CanPlacePart(part, new CreatureElementSelection(CreatureElementKind.Beam, beam.Id), out _))
        {
            DrawLine(start, end, Theme.SelectionGlow, width, antialiased: false);
        }
        else
        {
            DrawDashedLine(start, end, Theme.Danger, width, dash: Theme.BeamWidth * 2, aligned: true, antialiased: false);
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
    /// The dashed frame and rotate stem around a Select selection, drawn last
    /// and at screen size like its handles.
    /// </summary>
    private void DrawSelectionFrame()
    {
        if (_gestures!.SelectionFrame is not { } frame)
        {
            return;
        }

        var view = _gestures.View;
        DrawSetTransform(Vector2.Zero);
        var rect = RectFromPoints(ToGodot(view.ToView(frame.Min)), ToGodot(view.ToView(frame.Max)));
        // The canvas node is scaled in the scene; undo it so the frame is a true screen-size hairline.
        var width = UiSize.Stroke.SelectionFrame / Scale.X;
        UiDashedBorder.DrawRoundedRect(this, rect, UiSize.Radius.Small / Scale.X, Theme.SelectionGlow, width);
        foreach (var (handle, position) in _gestures.SelectionHandles)
        {
            if (handle == SelectionHandle.Rotate)
            {
                var top = new Vector2(rect.GetCenter().X, rect.Position.Y);
                DrawLine(top, ToGodot(view.ToView(position)), Theme.SelectionGlow, width, antialiased: false);
            }
        }
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
            case CreatureElementKind.Beam:
                var beam = _viewModel!.Beams[_viewModel.BeamIndexOf(note.Target.Id)];
                var a = NodeById(beam.NodeA);
                var b = NodeById(beam.NodeB);
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
        foreach (var (handle, control) in new[] { (SelectionHandle.Move, MoveHandle), (SelectionHandle.Rotate, RotateHandle), (SelectionHandle.Scale, ScaleHandle) })
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
        }
    }

    private void DrawBeamPreview()
    {
        if (_viewModel is null || _gestures?.BeamStartNodeId is not { } start || _gestures.BeamEnd is not { } end)
        {
            return;
        }

        var to = _gestures.BeamTargetNodeId is { } target ? NodeById(target).Position : end;
        DrawDashedLine(ToGodot(NodeById(start).Position), ToGodot(to), Theme.SelectionGlow, Stroke(Theme.BeamWidth), 8, antialiased: false);
    }

    private void DrawBeamEndRings()
    {
        if (_viewModel is null || _gestures is null)
        {
            return;
        }

        foreach (var nodeId in new[] { _gestures.BeamStartNodeId, _gestures.BeamTargetNodeId })
        {
            if (nodeId is { } id)
            {
                var node = NodeById(id);
                DrawArc(ToGodot(node.Position), (float)node.Radius * 1.65f, 0, Mathf.Tau, 32, Theme.SelectionGlow, Stroke(Theme.MotorSignalWidth), antialiased: false);
            }
        }
    }

    private void DrawSelectionBox()
    {
        if (_gestures?.SelectionBox is not { } box)
        {
            return;
        }

        var rect = RectFromPoints(ToGodot(box.Start), ToGodot(box.End));
        var fill = Theme.SelectionGlow;
        fill.A = 0.16f;
        DrawRect(rect, fill, filled: true);
        UiDashedBorder.DrawRoundedRect(this, rect, 0, Theme.SelectionGlow, Stroke(2));
    }

    private void DrawMoveGhosts()
    {
        if (_viewModel is null || _ghostNodePositions.Count == 0)
        {
            return;
        }

        foreach (var beam in _viewModel.Beams)
        {
            if (!_ghostNodePositions.TryGetValue(beam.NodeA, out var startPosition)
                && !_ghostNodePositions.TryGetValue(beam.NodeB, out var endPosition))
            {
                continue;
            }

            startPosition = _ghostNodePositions.GetValueOrDefault(beam.NodeA, NodeById(beam.NodeA).Position);
            endPosition = _ghostNodePositions.GetValueOrDefault(beam.NodeB, NodeById(beam.NodeB).Position);
            DrawDashedLine(ToGodot(startPosition), ToGodot(endPosition), Theme.SelectionGlow, Stroke(4), 8, antialiased: false);
        }

        foreach (var (nodeId, position) in _ghostNodePositions)
        {
            if (!_viewModel.Nodes.Any(node => node.Id == nodeId))
            {
                continue;
            }

            var radius = (float)NodeById(nodeId).Radius;
            DrawArc(ToGodot(position), radius * 1.35f, 0, Mathf.Tau, 32, Theme.SelectionGlow, Stroke(2), antialiased: false);
        }
    }

    private void DrawTopologyFeedback()
    {
        if (!TryBuildDrawableTopology(out var creature) || creature is null)
        {
            return;
        }

        foreach (var triangle in MotorTopology.BuildRigidTriangles(creature))
        {
            DrawRigidTriangle(creature, triangle);
        }

        foreach (var connection in MotorTopology.BuildNodeConnections(creature).Where(connection => connection.IsMotorized))
        {
            DrawMotorRelation(creature, connection);
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
        var fill = new Color(Theme.SelectionGlow.R, Theme.SelectionGlow.G, Theme.SelectionGlow.B, 0.10f);
        DrawColoredPolygon([a, b, c], fill);

        var center = (a + b + c) / 3f;
        DrawLine(a.Lerp(center, 0.35f), b.Lerp(center, 0.35f), Theme.SelectionGlow, Stroke(2), antialiased: false);
        DrawLine(b.Lerp(center, 0.35f), c.Lerp(center, 0.35f), Theme.SelectionGlow, Stroke(2), antialiased: false);
        DrawLine(c.Lerp(center, 0.35f), a.Lerp(center, 0.35f), Theme.SelectionGlow, Stroke(2), antialiased: false);

        // The label is text, not part of the picture: it keeps its size at any zoom.
        var view = _gestures!.View;
        var labelPosition = (center * (float)view.Zoom) + ToGodot(view.Offset) + _rigidLabelOffset;
        DrawSetTransform(Vector2.Zero);
        var font = ThemeDB.FallbackFont;
        var textSize = font.GetStringSize(_rigidLabelText, HorizontalAlignment.Left, -1, _rigidLabelFontSize);
        var textTop = labelPosition - new Vector2(0, font.GetAscent(_rigidLabelFontSize));
        var box = new Rect2(textTop, textSize).Grow(_rigidLabelPadding);
        DrawRect(box, Theme.ArenaBackground.WithAlpha(0.86f));
        DrawString(font, labelPosition, _rigidLabelText, HorizontalAlignment.Left, -1, _rigidLabelFontSize, Theme.GroundEdge);
        DrawThroughView();
    }

    private void DrawMotorRelation(CreatureDef creature, NodeConnectionDef connection)
    {
        var node = creature.Nodes[connection.NodeIndex];
        var center = ToGodot(node.Position);
        var start = BeamAngleFromNode(creature, creature.Beams[connection.ReferenceBeamIndex], connection.NodeIndex);
        var end = BeamAngleFromNode(creature, creature.Beams[connection.OtherBeamIndex], connection.NodeIndex);
        var delta = Mathf.Wrap(end - start, -Mathf.Pi, Mathf.Pi);
        var arcStart = delta < 0 ? start + delta : start;
        var arcEnd = delta < 0 ? start : start + delta;

        var radius = (float)node.Radius * 2.0f;
        DrawArc(center, radius, arcStart, arcEnd, 28, Theme.MotorAccent, Stroke(Theme.MotorSignalWidth), antialiased: false);
    }

    private void DrawMotorCenterMarkers()
    {
        if (!TryBuildDrawableTopology(out var creature) || creature is null)
        {
            return;
        }

        foreach (var nodeIndex in MotorTopology.BuildNodeConnections(creature)
            .Where(connection => connection.IsMotorized)
            .Select(connection => connection.NodeIndex)
            .Distinct())
        {
            var node = creature.Nodes[nodeIndex];
            DrawArc(ToGodot(node.Position), (float)node.Radius * 0.72f, 0, Mathf.Tau, 32, Theme.MotorAccent, Stroke(Theme.MotorSignalWidth), antialiased: false);
        }
    }

    private static float BeamAngleFromNode(CreatureDef creature, BeamDef beam, int nodeIndex)
    {
        var nodeId = creature.Nodes[nodeIndex].Id;
        var otherNodeId = beam.NodeA == nodeId ? beam.NodeB : beam.NodeA;
        var node = creature.Nodes[nodeIndex].Position;
        var other = creature.Nodes[creature.NodeIndexOf(otherNodeId)].Position;
        return Mathf.Atan2((float)(other.Y - node.Y), (float)(other.X - node.X));
    }

    private void DrawInvalidNodeMarkers()
    {
        if (_viewModel is null)
        {
            return;
        }

        for (var nodeIndex = 0; nodeIndex < _viewModel.Nodes.Count; nodeIndex++)
        {
            // The beam drag's own rings replace the warning on the joints being joined.
            var nodeId = _viewModel.Nodes[nodeIndex].Id;
            if (_viewModel.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
                || nodeId == _gestures?.BeamStartNodeId
                || nodeId == _gestures?.BeamTargetNodeId)
            {
                continue;
            }

            var node = _viewModel.Nodes[nodeIndex];
            var position = ToGodot(node.Position);
            var radius = (float)node.Radius * 1.55f;
            DrawArc(position, radius, 0, Mathf.Tau, 32, Theme.Danger, Stroke(Theme.MotorSignalWidth), antialiased: false);
            DrawLine(position + new Vector2(-radius * 0.45f, -radius * 0.45f), position + new Vector2(radius * 0.45f, radius * 0.45f), Theme.Danger, Stroke(Theme.MotorSignalWidth), antialiased: false);
            DrawLine(position + new Vector2(radius * 0.45f, -radius * 0.45f), position + new Vector2(-radius * 0.45f, radius * 0.45f), Theme.Danger, Stroke(Theme.MotorSignalWidth), antialiased: false);
        }
    }

    private void DrawInvalidBeamMarkers()
    {
        if (_viewModel is null)
        {
            return;
        }

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
            DrawArc(position, radius, 0, Mathf.Tau, 32, Theme.Danger, Stroke(Theme.MotorSignalWidth), antialiased: false);
            DrawLine(position + new Vector2(-radius * 0.55f, 0), position + new Vector2(radius * 0.55f, 0), Theme.Danger, Stroke(Theme.MotorSignalWidth), antialiased: false);
            DrawLine(position + new Vector2(0, -radius * 0.55f), position + new Vector2(0, radius * 0.55f), Theme.Danger, Stroke(Theme.MotorSignalWidth), antialiased: false);
        }
    }

    private void CaptureMoveGhosts(IReadOnlyCollection<int> movingNodes)
    {
        if (_viewModel is null || !_viewModel.IsMoveOnly)
        {
            return;
        }

        _ghostNodePositions.Clear();
        _ghostVersion++;
        foreach (var id in movingNodes)
        {
            if (_viewModel.Nodes.Any(node => node.Id == id))
            {
                _ghostNodePositions[id] = NodeById(id).Position;
            }
        }
    }

    private static Rect2 RectFromPoints(Vector2 first, Vector2 second)
    {
        var min = new Vector2(Mathf.Min(first.X, second.X), Mathf.Min(first.Y, second.Y));
        var max = new Vector2(Mathf.Max(first.X, second.X), Mathf.Max(first.Y, second.Y));
        return new Rect2(min, max - min);
    }

    private void ScheduleMoveGhostClear()
    {
        if (_ghostNodePositions.Count == 0 || GetTree() is not { } tree)
        {
            return;
        }

        var version = _ghostVersion;
        tree.CreateTimer(_moveGhostSeconds).Timeout += () =>
        {
            if (_ghostVersion != version)
            {
                return;
            }

            ClearMoveGhosts();
        };
    }

    private void ClearMoveGhosts()
    {
        if (_ghostNodePositions.Count == 0)
        {
            return;
        }

        _ghostVersion++;
        _ghostNodePositions.Clear();
        QueueRedraw();
    }

    private void OnAnatomyChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnGesturesChanged(object? sender, EventArgs eventArgs)
    {
        QueueRedraw();
    }

    private void OnNodeDragStarting(object? sender, IReadOnlyCollection<int> movingNodes)
    {
        CaptureMoveGhosts(movingNodes);
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

        if (eventArgs.PropertyName == nameof(BuildViewModel.IsMoveOnly) && _viewModel?.IsMoveOnly != true)
        {
            ClearMoveGhosts();
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
        _gestures!.View.VisibleArea = new CanvasRect(ToDomain(toView * Vector2.Zero), ToDomain(toView * slot.Size));
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
        foreach (var (corner, inward) in new[]
        {
            (topLeft, new Vector2(1, 1)),
            (new Vector2(bottomRight.X, topLeft.Y), new Vector2(-1, 1)),
            (new Vector2(topLeft.X, bottomRight.Y), new Vector2(1, -1)),
            (bottomRight, new Vector2(-1, -1)),
        })
        {
            DrawPolyline(
                [corner + new Vector2(inward.X * length, 0), corner, corner + new Vector2(0, inward.Y * length)],
                Theme.AreaCorner,
                Stroke(Theme.AreaCornerWidth));
        }
    }

    /// <summary>Draws everything after this in canvas units, zoomed and panned by the view.</summary>
    private void DrawThroughView()
    {
        var view = _gestures!.View;
        DrawSetTransform(ToGodot(view.Offset), 0, Vector2.One * (float)view.Zoom);
    }

    /// <summary>
    /// The width every line is drawn at (`docs/UI_DIRECTION.md` → Reference
    /// flow overrides); to keep lines at their screen width instead, return
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

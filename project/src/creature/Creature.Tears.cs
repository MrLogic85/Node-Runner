using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

// Beams tear from their joints (#962): each tick, before anything senses or drives, every beam end still
// pinned is checked by the rule in Mechanics.Tear, in beam order, end A before end B. A torn end's pin
// lets go of both bodies and the end gets a collider so it lands (#1288); a beam torn at both ends leaves
// the creature and falls on its own. Everything else keeps its slot, so network ports and the arrays the
// creature runs on never shift. How a tear looks (#1257) is docs/WORLD_VISUALS.md → Tear.
public partial class Creature
{
    private const double _hatchFadeSeconds = 0.2;

    // Each beam end as 2 × beam index + 0 for end A, 1 for end B.
    private PinJoint2D[] _beamPins = [];
    private int[] _beamEndNodes = [];
    private int[] _endTicksOver = [];
    private bool[] _endTorn = [];
    private bool[] _beamGone = [];
    private CollisionShape2D?[] _beamShapes = [];
    private int[] _hatchBeams = [];
    private bool[] _hatchGone = [];
    private bool[] _nodePulsed = [];
    private readonly List<TearPulseVisual> _tearPulses = [];
    private int[] _sensorBeams = [];

    // After the first tear the centre, bounds and lowest point follow the main piece only.
    private bool _hasTorn;
    private bool[] _nodeInMainPiece = [];

    /// <summary>How many beam ends have torn from their joints since the creature was last built (#962).</summary>
    public int TornEnds { get; private set; }

    /// <summary>
    /// The centre X, front X and lowest point Y the creature had at its latest tear, measured as they are
    /// measured from then on: the same bodies, now counted for the main piece only. A trial checks the
    /// next sample against this one (TrialController).
    /// </summary>
    public (float CenterX, float FrontX, float LowestPointY) TearSample { get; private set; }

    private void ResetTears(CreatureDef definition)
    {
        var ends = 2 * definition.Beams.Count;
        _endTicksOver = new int[ends];
        _endTorn = new bool[ends];
        _beamGone = new bool[definition.Beams.Count];
        _beamShapes = new CollisionShape2D?[definition.Beams.Count];
        _hatchGone = new bool[_hatchVisuals.Length];
        _nodePulsed = new bool[definition.Nodes.Count];
        _tearPulses.Clear();
        _beamEndNodes = new int[ends];
        for (var i = 0; i < definition.Beams.Count; i++)
        {
            _beamEndNodes[2 * i] = definition.NodeIndexOf(definition.Beams[i].NodeA);
            _beamEndNodes[(2 * i) + 1] = definition.NodeIndexOf(definition.Beams[i].NodeB);
        }

        _hasTorn = false;
        _nodeInMainPiece = new bool[definition.Nodes.Count];
        TornEnds = 0;
        TearSample = default;
    }

    private void CheckTears()
    {
        if (_beamBodies.Length == 0)
        {
            return;
        }

        ReadBodies();
        var tore = false;
        for (var beam = 0; beam < _beamBodies.Length; beam++)
        {
            if (_beamGone[beam])
            {
                continue;
            }

            // The ends sit half a length either way along the body's x axis. Written out in floats, as this
            // runs for every end of every shadow each tick: with Transform2D * Vector2 and DistanceTo the check
            // took 0.19 ms a tick for 59 shadows of 19 beams on a desktop debug build, about 8 % of the tick;
            // written out, 0.03 ms (#1256).
            var transform = _beamTransforms[beam];
            var halfLength = _beamHalfLengths[beam];
            var alongX = transform.X.X * halfLength;
            var alongY = transform.X.Y * halfLength;
            tore |= CheckEnd(2 * beam, transform.Origin.X - alongX, transform.Origin.Y - alongY);
            tore |= CheckEnd((2 * beam) + 1, transform.Origin.X + alongX, transform.Origin.Y + alongY);
        }

        if (tore)
        {
            AfterTear();
        }
    }

    private bool CheckEnd(int end, float endX, float endY)
    {
        if (_endTorn[end])
        {
            return false;
        }

        var node = _nodePositions[_beamEndNodes[end]];
        var driftX = endX - node.X;
        var driftY = endY - node.Y;
        var drift = MathF.Sqrt((driftX * driftX) + (driftY * driftY));
        _endTicksOver[end] = Tear.NextTicksOver(_endTicksOver[end], drift);
        if (!Tear.LetsGo(_endTicksOver[end]))
        {
            return false;
        }

        // A pin with no bodies stops holding in this tick's step, with no kick (#1255).
        var pin = _beamPins[end];
        pin.NodeA = new NodePath();
        pin.NodeB = new NodePath();
        _endTorn[end] = true;
        TornEnds++;

        var beam = end / 2;
        var isA = end % 2 == 0;
        if (isA)
        {
            _beamVisuals[beam].TornA = true;
            _beamKnockouts[beam].TornA = true;
        }
        else
        {
            _beamVisuals[beam].TornB = true;
            _beamKnockouts[beam].TornB = true;
        }

        Flash(_beamVisuals[beam], isA);
        Pulse(_beamEndNodes[end]);
        if (!_endTorn[2 * beam] || !_endTorn[(2 * beam) + 1])
        {
            LandEnd(beam, isA);
        }
        else
        {
            // Nothing holds it any more: it falls on its own, as a loose joint does (#1257).
            _beamGone[beam] = true;
            LetFall(beam);
            for (var sensor = 0; sensor < _sensors.Length; sensor++)
            {
                if (_sensorBeams[sensor] == beam && _sensors[sensor] is CameraSensor camera)
                {
                    camera.IsGone = true;
                }
            }
        }

        return true;
    }

    // Rare, so it may allocate.
    private void AfterTear()
    {
        var definition = Definition!;
        var torn = new HashSet<int>();
        for (var beam = 0; beam < _beamBodies.Length; beam++)
        {
            if (_endTorn[2 * beam] || _endTorn[(2 * beam) + 1])
            {
                torn.Add(definition.Beams[beam].Id);
            }
        }

        bool TornSide(IHingePartDef part) => torn.Contains(part.FixedLinkId!.Value) || torn.Contains(part.TargetLinkId!.Value);

        foreach (var servo in _servos.Where(servo => !servo.IsPassive && TornSide(servo.Definition)))
        {
            servo.GoPassive();
        }

        foreach (var motor in _velocityMotors.Where(motor => !motor.IsPassive && TornSide(motor.Definition)))
        {
            motor.GoPassive();
        }

        foreach (var brake in _brakes.Where(brake => !brake.IsPassive && TornSide(brake.Definition)))
        {
            brake.GoPassive();
        }

        foreach (var angleSensor in _angleSensors.Where(angleSensor => !angleSensor.IsPassive && TornSide(angleSensor.Definition)))
        {
            angleSensor.GoPassive();
        }

        foreach (var stepper in _steppers.Where(stepper => !stepper.IsPassive && TornSide(stepper.Definition)))
        {
            stepper.GoPassive();
        }

        // A triangle with a torn beam can fold, so it is no longer rigid.
        for (var hatch = 0; hatch < _hatchVisuals.Length; hatch++)
        {
            for (var side = 0; side < 3 && !_hatchGone[hatch]; side++)
            {
                if (torn.Contains(definition.Beams[_hatchBeams[(3 * hatch) + side]].Id))
                {
                    _hatchGone[hatch] = true;
                    FadeOut(_hatchVisuals[hatch]);
                }
            }
        }

        FindMainPiece(definition);
        _hasTorn = true;
        TearSample = (Centre.X, Bounds.End.X, LowestPointY);
    }

    // The followed creature marks where a joint let go, once per joint; a shadow does not (#1257).
    private void Pulse(int node)
    {
        if (_isShadow || _nodePulsed[node])
        {
            return;
        }

        _nodePulsed[node] = true;
        var pulse = new TearPulseVisual { Theme = Theme, Radius = _nodeColliderRadii[node], VisibilityLayer = ArenaVisibility.Both };
        _nodeBodies[node].AddChild(pulse);
        _tearPulses.Add(pulse);
    }

    // On the followed creature a torn end flashes danger for as long as the pulse runs, then is a plain
    // beam end again: the tear is said once (#1257). A shadow's ends do not flash.
    private void Flash(BeamVisual visual, bool isA)
    {
        if (_isShadow)
        {
            return;
        }

        SetFlash(visual, isA, on: true);
        var tween = visual.CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenInterval(TearPulseVisual.Seconds);
        tween.TweenCallback(Callable.From(() => SetFlash(visual, isA, on: false)));
    }

    private static void SetFlash(BeamVisual visual, bool isA, bool on)
    {
        if (isA)
        {
            visual.FlashA = on;
        }
        else
        {
            visual.FlashB = on;
        }
    }

    // An intact beam has no collider (.claude/rules/creature.md rule 3). A torn end needs one so it lands instead of
    // passing through the ground: a circle as wide as the beam (#1288).
    private void LandEnd(int beam, bool isA)
    {
        var halfLength = _beamHalfLengths[beam];
        BeamShape(beam).Shape = new CircleShape2D { Radius = Theme.BeamWidth / 2 };
        BeamShape(beam).Position = new Vector2(isA ? -halfLength : halfLength, 0);
    }

    // Loose at both ends, the whole beam lands: its one shape becomes a capsule along it (#1257).
    private void LetFall(int beam)
    {
        var halfWidth = Theme.BeamWidth / 2;
        var shape = BeamShape(beam);
        shape.Shape = new CapsuleShape2D { Radius = halfWidth, Height = 2 * (_beamHalfLengths[beam] + halfWidth) };
        shape.Position = Vector2.Zero;
        shape.Rotation = Mathf.Pi / 2;
    }

    // A beam keeps one shape at most, so its end circle never stays behind under the capsule.
    private CollisionShape2D BeamShape(int beam)
    {
        if (_beamShapes[beam] is not { } shape)
        {
            shape = new CollisionShape2D();
            _beamBodies[beam].AddChild(shape);
            _beamShapes[beam] = shape;
        }

        return shape;
    }

    // On the followed creature a rigid triangle's hatch fades out over 200 ms (12 ticks), short enough
    // that the folding triangle does not carry its lines out of itself; on a shadow it goes at once.
    private void FadeOut(RigidHatchVisual hatch)
    {
        if (_isShadow)
        {
            hatch.Visible = false;
            return;
        }

        var tween = hatch.CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenProperty(hatch, "modulate:a", 0f, _hatchFadeSeconds);
        tween.TweenCallback(Callable.From(() => hatch.Visible = false));
    }

    private void FindMainPiece(CreatureDef definition)
    {
        Array.Clear(_nodeInMainPiece);
        foreach (var nodeId in Tear.MainPiece(definition, _endTorn))
        {
            _nodeInMainPiece[definition.NodeIndexOf(nodeId)] = true;
        }
    }
}

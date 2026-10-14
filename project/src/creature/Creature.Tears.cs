using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

// Beams tear from their joints (#962): each tick, before anything senses or drives, every beam end still
// pinned is checked by the rule in Mechanics.Tear, in beam order, end A before end B. A torn end's pin
// lets go of both bodies; a beam torn at both ends leaves the world. Everything else keeps its slot, so
// brain ports and the arrays the creature runs on never shift.
public partial class Creature
{
    // Each beam end as 2 × beam index + 0 for end A, 1 for end B.
    private PinJoint2D[] _beamPins = [];
    private int[] _beamEndNodes = [];
    private int[] _endTicksOver = [];
    private bool[] _endTorn = [];
    private bool[] _beamGone = [];
    private int[] _hatchBeams = [];
    private int[] _sensorBeams = [];

    // After the first tear the centre, bounds and lowest point follow the main piece only.
    private bool _hasTorn;
    private bool[] _nodeInMainPiece = [];
    private bool[] _beamCounted = [];
    private int _countedBeams;

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
        _beamEndNodes = new int[ends];
        for (var i = 0; i < definition.Beams.Count; i++)
        {
            _beamEndNodes[2 * i] = definition.NodeIndexOf(definition.Beams[i].NodeA);
            _beamEndNodes[(2 * i) + 1] = definition.NodeIndexOf(definition.Beams[i].NodeB);
        }

        _hasTorn = false;
        _nodeInMainPiece = new bool[definition.Nodes.Count];
        _beamCounted = new bool[definition.Beams.Count];
        _countedBeams = 0;
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

            var transform = _beamTransforms[beam];
            var halfLength = _beamHalfLengths[beam];
            tore |= CheckEnd(2 * beam, transform * new Vector2(-halfLength, 0));
            tore |= CheckEnd((2 * beam) + 1, transform * new Vector2(halfLength, 0));
        }

        if (tore)
        {
            AfterTear();
        }
    }

    private bool CheckEnd(int end, Vector2 beamEnd)
    {
        if (_endTorn[end])
        {
            return false;
        }

        var drift = beamEnd.DistanceTo(_nodePositions[_beamEndNodes[end]]);
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
        if (_endTorn[2 * beam] && _endTorn[(2 * beam) + 1])
        {
            // A body taken out of the world with nothing joined to it: it costs no physics and draws nothing.
            _beamGone[beam] = true;
            _beamBodies[beam].Visible = false;
            _beamBodies[beam].ProcessMode = ProcessModeEnum.Disabled;
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

        // A triangle with a torn beam can fold, so it is no longer rigid.
        for (var hatch = 0; hatch < _hatchVisuals.Length; hatch++)
        {
            for (var side = 0; side < 3; side++)
            {
                if (torn.Contains(definition.Beams[_hatchBeams[(3 * hatch) + side]].Id))
                {
                    _hatchVisuals[hatch].Visible = false;
                }
            }
        }

        FindMainPiece(definition);
        _hasTorn = true;
        TearSample = (CenterOfMass.X, Bounds.End.X, LowestPointY);
    }

    // The main piece by the one-piece check (#930) over the links still joining their nodes: every Piston
    // and Spring, and every beam with neither end torn. With none left, the first joint stands for it.
    private void FindMainPiece(CreatureDef definition)
    {
        var holding = definition.Beams.Where((_, beam) => !_endTorn[2 * beam] && !_endTorn[(2 * beam) + 1]);
        var main = Tear.MainPiece(LinkRef.Pieces(definition.Nodes, LinkRef.All(holding, definition.Pistons, definition.Springs)));
        Array.Clear(_nodeInMainPiece);
        foreach (var nodeId in main)
        {
            _nodeInMainPiece[definition.NodeIndexOf(nodeId)] = true;
        }

        if (main.Count == 0)
        {
            _nodeInMainPiece[0] = true;
        }

        // A beam counts while one of its ends still holds on to the main piece, dangling or not.
        _countedBeams = 0;
        for (var beam = 0; beam < _beamBodies.Length; beam++)
        {
            _beamCounted[beam] = HoldsOnToMainPiece(2 * beam) || HoldsOnToMainPiece((2 * beam) + 1);
            _countedBeams += _beamCounted[beam] ? 1 : 0;
        }
    }

    private bool HoldsOnToMainPiece(int end) => !_endTorn[end] && _nodeInMainPiece[_beamEndNodes[end]];
}

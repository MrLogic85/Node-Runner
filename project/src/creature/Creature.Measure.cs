using Godot;

namespace NodeRunner.Creature;

// Where the creature is, for the trial's score and the camera: its centre, bounds and lowest point, all
// from one read of its bodies per physics tick. After a tear (#962) they follow the main piece
// (Creature.Tears.cs).
public partial class Creature
{
    // Where every body is this physics tick (ReadBodies).
    private Transform2D[] _beamTransforms = [];
    private Vector2[] _nodePositions = [];
    private ulong _bodiesReadTick = ulong.MaxValue;

    /// <summary>
    /// The Y of the creature's lowest physical point: the bottom of the lowest
    /// node collider. An intact beam has no collider; a torn beam's shape is left
    /// out on purpose: the score freezes at the first tear, and after it only the
    /// blow-up check reads this, which follows the main piece's nodes
    /// (docs/TRAINING_LOOP.md → Trial → Tears, #1288). Godot's Y grows downward, so this
    /// is the largest Y. Returns negative infinity for a creature with no nodes.
    /// After a tear (#962), of the main piece's nodes only.
    /// </summary>
    public float LowestPointY
    {
        get
        {
            ReadBodies();
            var lowest = float.NegativeInfinity;
            for (var i = 0; i < _nodeBodies.Length; i++)
            {
                if (_hasTorn && !_nodeInMainPiece[i])
                {
                    continue;
                }

                lowest = Math.Max(lowest, _nodePositions[i].Y + _nodeColliderRadii[i]);
            }

            return lowest;
        }
    }

    /// <summary>
    /// The box around the creature's node colliders, in global coordinates: what the Training
    /// camera fits in view (#675). Intact beams run between nodes, so the nodes bound them; the
    /// free tip of a torn beam is left out, as in <see cref="LowestPointY"/>. An empty
    /// rectangle for a creature with no nodes. After a tear (#962), round the main piece's nodes only.
    /// </summary>
    public Rect2 Bounds
    {
        get
        {
            ReadBodies();
            Rect2? bounds = null;
            for (var i = 0; i < _nodeBodies.Length; i++)
            {
                if (_hasTorn && !_nodeInMainPiece[i])
                {
                    continue;
                }

                bounds = bounds is { } box ? box.Merge(NodeBox(i)) : NodeBox(i);
            }

            return bounds ?? default;
        }
    }

    /// <summary>
    /// The creature's centre: the average position of its nodes, which every creature has, whatever
    /// links join them (#1303). Vector2.Zero for a creature with no nodes. After a tear (#962), of
    /// the main piece's nodes only.
    /// </summary>
    public Vector2 Centre
    {
        get
        {
            ReadBodies();
            var sum = Vector2.Zero;
            var count = 0;
            for (var i = 0; i < _nodeBodies.Length; i++)
            {
                if (_hasTorn && !_nodeInMainPiece[i])
                {
                    continue;
                }

                sum += _nodePositions[i];
                count++;
            }

            return count == 0 ? Vector2.Zero : sum / count;
        }
    }

    private Rect2 NodeBox(int index)
    {
        var radius = _nodeColliderRadii[index];
        return new Rect2(_nodePositions[index] - new Vector2(radius, radius), new Vector2(radius, radius) * 2);
    }

    // Reads every body's place once per physics tick, in global coordinates. Within a tick nothing moves
    // a body but a reset, which reads again, so the trial's sample, the camera and the tear check all
    // share one read (#1256): each read crosses into Godot, and with every shadow's every body it adds up.
    // Outside a physics tick it reads afresh, as the step may have moved them.
    private void ReadBodies()
    {
        var inTick = Engine.IsInPhysicsFrame();
        var tick = inTick ? Engine.GetPhysicsFrames() : ulong.MaxValue;
        if (inTick && tick == _bodiesReadTick && _beamTransforms.Length == _beamBodies.Length && _nodePositions.Length == _nodeBodies.Length)
        {
            return;
        }

        if (_beamTransforms.Length != _beamBodies.Length)
        {
            _beamTransforms = new Transform2D[_beamBodies.Length];
        }

        if (_nodePositions.Length != _nodeBodies.Length)
        {
            _nodePositions = new Vector2[_nodeBodies.Length];
        }

        for (var i = 0; i < _beamBodies.Length; i++)
        {
            _beamTransforms[i] = _beamBodies[i].GlobalTransform;
        }

        for (var i = 0; i < _nodeBodies.Length; i++)
        {
            _nodePositions[i] = _nodeBodies[i].GlobalPosition;
        }

        _bodiesReadTick = tick;
    }
}

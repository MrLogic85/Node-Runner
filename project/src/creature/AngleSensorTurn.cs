using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// The turn an Angle sensor's arc is on in Training, from the angle its sides counted across whole
/// turns since the built pose (<see cref="HingePartSides.ContinuousAngle"/>), which follows every physics tick
/// however many a frame runs.
/// </summary>
public static class AngleSensorTurn
{
    /// <summary>
    /// The turn for <paramref name="angle"/>, the sides' reading now (<see cref="Theme.AngleSensorGeometry.Angle"/>),
    /// after they turned <paramref name="turned"/> radians counter-clockwise on screen from the built pose, which
    /// read <paramref name="restAngle"/> on <paramref name="restTurn"/> (<see cref="Theme.AngleSensorGeometry.RestTurn"/>).
    /// </summary>
    public static int At(float restAngle, int restTurn, double turned, float angle)
    {
        var counted = restAngle + (restTurn * Math.Tau) + turned;
        var turn = (int)Math.Floor(counted / Math.Tau);

        // The count is a physics tick behind the bodies and rounded apart from the reading, so the
        // Target side may have just passed the Fixed side; a tick moves it under half a turn.
        return (angle - Mathf.PosMod(counted, Math.Tau)) switch
        {
            < -Math.PI => turn + 1,
            > Math.PI => turn - 1,
            _ => turn,
        };
    }
}

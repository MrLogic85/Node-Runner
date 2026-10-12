namespace NodeRunner.Theme;

/// <summary>
/// A Velocity motor (#454), drawn for now as a Servo without its range band: the housing on the
/// Fixed side, the ring, and the horn along the Target. Its own look replaces this class later.
/// </summary>
public partial class VelocityMotorPart : ServoPart
{
    protected override bool DrawsRange => false;
}

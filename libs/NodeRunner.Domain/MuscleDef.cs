namespace NodeRunner.Domain;

public sealed record MuscleDef
{
    public MuscleDef(int jointA, int jointB, double restLength, double maxForce)
    {
        if (jointA < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jointA), "Joint index must be non-negative.");
        }

        if (jointB < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jointB), "Joint index must be non-negative.");
        }

        if (jointA == jointB)
        {
            throw new ArgumentException("A muscle must connect two different joints.");
        }

        if (!double.IsFinite(restLength) || restLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(restLength), "Muscle rest length must be finite and positive.");
        }

        if (!double.IsFinite(maxForce) || maxForce <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxForce), "Muscle max force must be finite and positive.");
        }

        JointA = jointA;
        JointB = jointB;
        RestLength = restLength;
        MaxForce = maxForce;
    }

    public int JointA { get; }

    public int JointB { get; }

    public double RestLength { get; }

    public double MaxForce { get; }
}

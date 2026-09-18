namespace NodeRunner.Domain;

public sealed record BoneDef
{
    public BoneDef(int jointA, int jointB)
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
            throw new ArgumentException("A bone must connect two different joints.");
        }

        JointA = jointA;
        JointB = jointB;
    }

    public int JointA { get; }

    public int JointB { get; }
}

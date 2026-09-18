using System.Collections.ObjectModel;

namespace NodeRunner.Domain;

public sealed record CreatureDef
{
    private readonly ReadOnlyCollection<JointDef> _joints;
    private readonly ReadOnlyCollection<BoneDef> _bones;
    private readonly ReadOnlyCollection<MuscleDef> _muscles;

    public CreatureDef(IReadOnlyList<JointDef> joints, IReadOnlyList<BoneDef> bones, IReadOnlyList<MuscleDef> muscles)
    {
        ArgumentNullException.ThrowIfNull(joints);
        ArgumentNullException.ThrowIfNull(bones);
        ArgumentNullException.ThrowIfNull(muscles);

        if (joints.Count < 2)
        {
            throw new ArgumentException("A creature needs at least two joints.", nameof(joints));
        }

        foreach (var bone in bones)
        {
            ValidateJointIndex(bone.JointA, joints.Count);
            ValidateJointIndex(bone.JointB, joints.Count);
        }

        foreach (var muscle in muscles)
        {
            ValidateJointIndex(muscle.JointA, joints.Count);
            ValidateJointIndex(muscle.JointB, joints.Count);
        }

        _joints = Array.AsReadOnly(joints.ToArray());
        _bones = Array.AsReadOnly(bones.ToArray());
        _muscles = Array.AsReadOnly(muscles.ToArray());
    }

    public IReadOnlyList<JointDef> Joints => _joints;

    public IReadOnlyList<BoneDef> Bones => _bones;

    public IReadOnlyList<MuscleDef> Muscles => _muscles;

    private static void ValidateJointIndex(int index, int jointCount)
    {
        if (index >= jointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Joint index must point to an existing joint.");
        }
    }
}

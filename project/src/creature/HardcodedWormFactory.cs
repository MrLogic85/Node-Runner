using NodeRunner.Domain;

namespace NodeRunner.Creature;

public static class HardcodedWormFactory
{
    public static CreatureDef Create()
    {
        const double radius = 18;
        const double spacing = 56;
        const double y = 0;

        var joints = new[]
        {
            new JointDef(new Vector2D(0, y), radius),
            new JointDef(new Vector2D(spacing, y), radius),
            new JointDef(new Vector2D(spacing * 2, y), radius),
            new JointDef(new Vector2D(spacing * 3, y), radius),
            new JointDef(new Vector2D(spacing * 4, y), radius),
        };

        var bones = new[]
        {
            new BoneDef(0, 1),
            new BoneDef(1, 2),
            new BoneDef(2, 3),
            new BoneDef(3, 4),
        };

        var muscles = new[]
        {
            new MuscleDef(0, 1, spacing, 900),
            new MuscleDef(1, 2, spacing, 900),
            new MuscleDef(2, 3, spacing, 900),
            new MuscleDef(3, 4, spacing, 900),
        };

        return new CreatureDef(joints, bones, muscles);
    }
}

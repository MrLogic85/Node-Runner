using Godot;

namespace NodeRunner.Creature;

public partial class JointVisual : Node2D
{
    public float Radius { get; set; }

    public bool IsHead { get; set; }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, new Color(0.93f, 0.72f, 0.45f));

        if (!IsHead)
        {
            return;
        }

        var eyeOffsetX = Radius * 0.35f;
        var eyeOffsetY = -Radius * 0.25f;
        var eyeRadius = Radius * 0.22f;
        DrawCircle(new Vector2(-eyeOffsetX, eyeOffsetY), eyeRadius, Colors.White);
        DrawCircle(new Vector2(eyeOffsetX, eyeOffsetY), eyeRadius, Colors.White);
        DrawCircle(new Vector2(-eyeOffsetX, eyeOffsetY), eyeRadius * 0.45f, Colors.Black);
        DrawCircle(new Vector2(eyeOffsetX, eyeOffsetY), eyeRadius * 0.45f, Colors.Black);
    }
}

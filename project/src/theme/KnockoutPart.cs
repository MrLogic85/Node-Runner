using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// The arena background around a beam or joint (#818), a little wider than it and under the whole
/// creature, so the followed creature stands out from the shadows behind it. A round-ended stroke
/// from <see cref="A"/> to <see cref="B"/>: a beam's line, or a joint's disc when the two are the same.
/// </summary>
public partial class KnockoutPart : PartVisual
{
    private Vector2 _a;
    private Vector2 _b;
    private float _radius;

    public KnockoutPart()
        : base(CreatureLayers.Knockout, CreatureLayers.Knockout)
    {
    }

    public Vector2 A
    {
        get => _a;
        set => Change(ref _a, value);
    }

    public Vector2 B
    {
        get => _b;
        set => Change(ref _b, value);
    }

    /// <summary>Half the width of the part it outlines, without the outline: a joint's radius or half a beam's width.</summary>
    public float Radius
    {
        get => _radius;
        set => Change(ref _radius, value);
    }

    public override void _Draw()
    {
        var radius = Radius + Theme.KnockoutWidth;
        using var pen = UiPixelPen.Begin(this);
        pen.Disc(A, radius, Theme.ArenaBackground);
        if (A != B)
        {
            pen.Line(A, B, Theme.ArenaBackground, radius * 2);
            pen.Disc(B, radius, Theme.ArenaBackground);
        }
    }
}

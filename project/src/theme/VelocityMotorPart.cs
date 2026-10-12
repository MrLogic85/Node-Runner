using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// A Velocity motor (#454): an electric motor in section that stands in for its joint's hub
/// (<see cref="HubMotorGeometry"/>). Its magnets turn with the Target side and its coils with the
/// Fixed side; nothing else moves. Each is drawn once on its own layer behind the part, which
/// turns it by <see cref="TargetAngle"/> or <see cref="FixedAngle"/>, so a moving creature never
/// redraws it. The part itself draws its marks over both.
/// </summary>
public partial class VelocityMotorPart : PartVisual
{
    private static readonly Vector2[][] _magnetShapes = [.. Enumerable.Range(0, HubMotorGeometry.MagnetCount).Select(HubMotorGeometry.MagnetShape)];
    private static readonly Vector2[][] _magnetOutlines = [.. Enumerable.Range(0, HubMotorGeometry.MagnetCount).Select(HubMotorGeometry.MagnetOutline)];
    private static readonly Vector2[][] _coils = HubMotorGeometry.CoilStrokes();

    // The layers draw behind the part, the magnets over the base the coils' layer draws; the
    // coils end inside the magnets, so the two never overlap and either may turn alone.
    private readonly PartLayer _coilLayer = new() { Name = "Coils", ShowBehindParent = true };
    private readonly PartLayer _magnetLayer = new() { Name = "Magnets", ShowBehindParent = true };
    private bool _hasFixed = true;
    private bool _hasTarget = true;
    private bool _simplified;

    public VelocityMotorPart()
        : base(DrawSlot.Over)
    {
        _coilLayer.Drawing = DrawCoilLayer;
        _magnetLayer.Drawing = DrawMagnetLayer;
        AddChild(_coilLayer);
        AddChild(_magnetLayer);
    }

    /// <summary>The Fixed side's direction, which the coils turn with, in the part's frame.</summary>
    public float FixedAngle
    {
        get => _coilLayer.Rotation;
        set => _coilLayer.Rotation = value;
    }

    /// <summary>The Target side's direction, which the magnets turn with, in the part's frame.</summary>
    public float TargetAngle
    {
        get => _magnetLayer.Rotation;
        set => _magnetLayer.Rotation = value;
    }

    public bool HasFixed
    {
        get => _hasFixed;
        set => Change(ref _hasFixed, value);
    }

    public bool HasTarget
    {
        get => _hasTarget;
        set => Change(ref _hasTarget, value);
    }

    /// <summary>A Training shadow's look: the outer ring and a circle round the coils, which do not turn.</summary>
    public bool Simplified
    {
        get => _simplified;
        set => Change(ref _simplified, value);
    }

    /// <summary>A motor missing a link cannot train, so the whole part draws in danger (#1111).</summary>
    private bool Missing => !HasFixed || !HasTarget;

    private Color Line => Missing ? Theme.Danger : Theme.MotorAccent;

    protected override void Changed()
    {
        base.Changed();
        _coilLayer.QueueRedraw();
        _magnetLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        if (Simplified)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(this);
        if (Selected)
        {
            JointMotorDrawing.DrawHalo(pen, Theme, HubMotorGeometry.Radius);
        }

        if (Missing)
        {
            JointMotorDrawing.DrawDangerBadge(pen, Theme, HubMotorGeometry.Radius);
        }
    }

    private void DrawCoilLayer(UiPixelPen pen)
    {
        if (Simplified)
        {
            pen.Ring(Vector2.Zero, HubMotorGeometry.RingRadius, Line, Theme.JointRingWidth);
            pen.Ring(Vector2.Zero, HubMotorGeometry.CoilOuterRadius, Line, Theme.MotorSignalWidth);
            return;
        }

        pen.Disc(Vector2.Zero, HubMotorGeometry.Radius, Theme.ArenaBackground);
        pen.Disc(Vector2.Zero, HubMotorGeometry.Radius, Theme.ServoPanelFill);
        pen.Ring(Vector2.Zero, HubMotorGeometry.RingRadius, Line, Theme.JointRingWidth);
        pen.Strokes(_coils, Line, Theme.MotorSignalWidth);
        pen.Disc(Vector2.Zero, HubMotorGeometry.CentreRadius, Theme.ServoPanelFill);
        pen.Ring(Vector2.Zero, HubMotorGeometry.CentreRadius, Line, Theme.MotorSignalWidth);
        pen.Disc(Vector2.Zero, HubMotorGeometry.AxleRadius, Line);
    }

    // The outlined magnets lie on the panel fill the coils' layer draws under them.
    private void DrawMagnetLayer(UiPixelPen pen)
    {
        if (Simplified)
        {
            return;
        }

        for (var index = 0; index < _magnetShapes.Length; index++)
        {
            if (HubMotorGeometry.IsSolid(index))
            {
                pen.Polygon(_magnetShapes[index], Line);
            }
        }

        pen.Strokes(_magnetOutlines, Line, Theme.MotorSignalWidth);
    }
}

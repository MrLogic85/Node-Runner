using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The UI size (#299): one factor, 50% to 400%, that makes everything around the arena and the
/// Build canvas bigger or smaller, touch targets included. It is applied once, as the root
/// window's <see cref="Window.ContentScaleFactor"/>; Godot multiplies it into the
/// <c>canvas_items</c> stretch, so the visible canvas gets smaller in canvas units as the UI grows.
/// Tokens, scenes and components never multiply by it. The two views of a world undo it instead
/// (<see cref="UiWorldView"/> and the Build canvas's zoom), so they keep their size on screen and
/// only get the space that is left. Registered as the <see cref="AutoloadName"/> autoload; Settings
/// (#201) chooses the value.
/// </summary>
public sealed partial class UiScale : Node
{
    public const string AutoloadName = "UiScale";
    public const int MinPercent = 50;
    public const int MaxPercent = 400;
    public const int DefaultPercent = 100;
    public const int DoublePercent = 200;

    /// <summary>The finest step: Settings' minus and plus buttons step by this much.</summary>
    public const int StepPercent = 5;

    private const float _percentPerFactor = 100;

    /// <summary>The four marked steps on Settings' UI size slider.</summary>
    public static IReadOnlyList<int> MarkedPercents { get; } = [MinPercent, DefaultPercent, DoublePercent, MaxPercent];

    [Signal]
    public delegate void ChangedEventHandler();

    /// <summary>The UI size now, as a percentage on the <see cref="StepPercent"/> grid.</summary>
    public int Percent { get; private set; } = DefaultPercent;

    /// <summary>The UI size now, as a factor: 1 at 100%.</summary>
    public float Factor => FactorFor(Percent);

    /// <summary>The autoload, or null in the editor, where it does not run.</summary>
    public static UiScale? Of(Node from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return Engine.IsEditorHint() ? null : from.GetNode<UiScale>("/root/" + AutoloadName);
    }

    /// <summary>The factor in effect for <paramref name="from"/>'s tree: 1 where there is no autoload.</summary>
    public static float FactorOf(Node from) => Of(from)?.Factor ?? 1;

    /// <summary>
    /// Sets the UI size, snapped to the <see cref="StepPercent"/> grid within
    /// <see cref="MinPercent"/>..<see cref="MaxPercent"/>. Godot lays every control out again at
    /// the new size; the theme-change notification then lets controls that rasterize icons or
    /// indicators redo them at the new pixel density.
    /// </summary>
    public void SetPercent(int percent)
    {
        var snapped = Snap(percent);
        if (snapped == Percent)
        {
            return;
        }

        Percent = snapped;
        var root = GetTree().Root;
        root.ContentScaleFactor = Factor;
        foreach (var child in root.GetChildren())
        {
            child.PropagateNotification((int)Control.NotificationThemeChanged);
        }

        EmitSignal(SignalName.Changed);
    }

    /// <summary>Rounds to the nearest <see cref="StepPercent"/> (halves round up) and clamps to the range.</summary>
    public static int Snap(double percent)
    {
        if (!double.IsFinite(percent))
        {
            return DefaultPercent;
        }

        var stepped = Math.Round(percent / StepPercent, MidpointRounding.AwayFromZero) * StepPercent;
        return (int)Math.Clamp(stepped, MinPercent, MaxPercent);
    }

    /// <summary>The factor for <paramref name="percent"/>, after <see cref="Snap"/>.</summary>
    public static float FactorFor(double percent) => Snap(percent) / _percentPerFactor;

    /// <summary>
    /// How big a length of <paramref name="units"/> canvas units shows at <paramref name="percent"/>,
    /// measured on the 640 x 360 reference canvas: a 48 unit touch target is 24 at 50% and 96 at 200%.
    /// </summary>
    public static float OnReferenceCanvas(float units, double percent) => units * FactorFor(percent);

    /// <summary>
    /// Window pixels per canvas unit, everything included: the <c>canvas_items</c> stretch times
    /// the UI size. Rasterized icons and indicators are drawn at this density to stay sharp.
    /// </summary>
    public static float PixelsPerUnit(Vector2I windowPixels, Vector2 visibleUnits) =>
        windowPixels.X > 0 && visibleUnits.X > 0 ? windowPixels.X / visibleUnits.X : 1;

    /// <summary>Window pixels per canvas unit for the running game's root window.</summary>
    public static float PixelsPerUnit()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        return root is null ? 1 : PixelsPerUnit(DisplayServer.WindowGetSize(), root.GetVisibleRect().Size);
    }

    /// <summary>Whole pixels for a raster of <paramref name="units"/> canvas units: rounded to nearest, at least 1.</summary>
    public static int RasterPixels(float units, float pixelsPerUnit) =>
        Math.Max(1, (int)MathF.Round(units * pixelsPerUnit, MidpointRounding.AwayFromZero));
}

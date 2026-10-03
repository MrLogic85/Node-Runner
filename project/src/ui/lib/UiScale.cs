using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The UI size (#299, #738): device pixels per canvas unit, as a percentage, applied once as the root
/// window's <see cref="Window.ContentScaleFactor"/> (<see cref="RootFactor"/>). Registered as the
/// <see cref="AutoloadName"/> autoload. See "UI size" in docs/UI_DIRECTION.md.
/// </summary>
public sealed partial class UiScale : Node
{
    public const string AutoloadName = "UiScale";
    public const int MinPercent = 50;
    public const int OnePixelPercent = 100;

    /// <summary>The finest step: Settings' minus and plus buttons step by this much.</summary>
    public const int StepPercent = 5;

    private const float _percentPerPixel = 100;

    // Android's dp: one density-independent pixel is a pixel on a 160 dpi screen.
    private const float _dpDpi = 160;
    private const float _floorTolerance = 1e-4f;

    [Signal]
    public delegate void ChangedEventHandler();

    /// <summary>The size the player chose.</summary>
    public UiSizeChoice Chosen { get; private set; } = UiSizeChoice.Auto;

    /// <summary>This screen's default: one canvas unit per dp.</summary>
    public int AutoPercent { get; private set; } = OnePixelPercent;

    /// <summary>The largest size at which the layouts fit this window's safe area.</summary>
    public int MaxPercent { get; private set; } = OnePixelPercent;

    /// <summary>The UI size in effect: <see cref="Chosen"/> on this window.</summary>
    public int Percent { get; private set; } = OnePixelPercent;

    /// <summary>The root window's content scale that gives <see cref="Percent"/> on this window.</summary>
    public float RootFactor { get; private set; } = 1;

    /// <summary>The autoload, or null in the editor, where it does not run.</summary>
    public static UiScale? Of(Node from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return Engine.IsEditorHint() ? null : from.GetNode<UiScale>("/root/" + AutoloadName);
    }

    /// <summary>The root factor in effect for <paramref name="from"/>'s tree: 1 where there is no autoload.</summary>
    public static float FactorOf(Node from) => Of(from)?.RootFactor ?? 1;

    public override void _Ready()
    {
        GetViewport().SizeChanged += Refresh;
        UiSafeArea.Of(this)?.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Refresh;
        UiSafeArea.Of(this)?.Changed -= Refresh;
    }

    /// <summary>
    /// Chooses a size. Godot lays every control out again and re-rasterizes fonts and
    /// <see cref="DpiTexture"/> icons.
    /// </summary>
    public void Choose(UiSizeChoice choice)
    {
        if (choice == Chosen)
        {
            return;
        }

        Chosen = choice;
        Refresh(chosen: true);
    }

    private void Refresh() => Refresh(chosen: false);

    private void Refresh(bool chosen)
    {
        var window = new Rect2I(DisplayServer.WindowGetPosition(), DisplayServer.WindowGetSize());
        if (window.Size.X <= 0 || window.Size.Y <= 0)
        {
            return;
        }

        var safeArea = DisplayServer.GetDisplaySafeArea().Intersection(window);
        var auto = AutoPercentFor(DensityScaleFor(
            OS.HasFeature("mobile"), DisplayServer.ScreenGetDpi(), DisplayServer.ScreenGetScale()));
        var max = MaxPercentFor(safeArea.HasArea() ? safeArea.Size : window.Size);
        var percent = Chosen.Resolve(auto, max);
        var factor = RootFactorFor(percent, window.Size);
        var rescaled = !Mathf.IsEqualApprox(factor, RootFactor);
        if (!chosen && !rescaled && auto == AutoPercent && max == MaxPercent && percent == Percent)
        {
            return;
        }

        // All state is set first: the new factor resizes the viewport, which calls back in here.
        AutoPercent = auto;
        MaxPercent = max;
        Percent = percent;
        RootFactor = factor;
        if (rescaled)
        {
            GetTree().Root.ContentScaleFactor = factor;
        }

        EmitSignal(SignalName.Changed);
    }

    /// <summary>
    /// Pixels per dp. Android buckets and caps <paramref name="screenScale"/> (about 1.8 on an S25), so
    /// a phone uses its <paramref name="dpi"/>. A desktop's scale is its HiDPI factor (2 on a Retina
    /// Mac, whose window size is in pixels). 1 when the value is unusable.
    /// </summary>
    public static float DensityScaleFor(bool mobile, int dpi, float screenScale)
    {
        var scale = mobile ? dpi / _dpDpi : screenScale;
        return float.IsFinite(scale) && scale > 0 ? scale : 1;
    }

    /// <summary>The <c>canvas_items</c> stretch Godot applies to a window before the root factor.</summary>
    public static float BaseStretch(Vector2I windowPixels) =>
        Mathf.Min(
            (float)windowPixels.X / UiLayout.CanvasWidth,
            (float)windowPixels.Y / UiLayout.CanvasHeight);

    /// <summary>Auto for a screen with <paramref name="densityScale"/> pixels per dp.</summary>
    public static int AutoPercentFor(float densityScale) => Snap(densityScale * _percentPerPixel);

    /// <summary>
    /// The largest size on the <see cref="StepPercent"/> grid at which a 640 x 360 canvas fits
    /// <paramref name="safePixels"/>, and never below <see cref="MinPercent"/>.
    /// </summary>
    public static int MaxPercentFor(Vector2I safePixels)
    {
        var fit = BaseStretch(safePixels) * _percentPerPixel / StepPercent;
        return Math.Max(MinPercent, (int)Math.Floor(fit + _floorTolerance) * StepPercent);
    }

    /// <summary>The root window's content scale that makes one canvas unit <paramref name="percent"/>/100 pixels.</summary>
    public static float RootFactorFor(int percent, Vector2I windowPixels) =>
        percent / _percentPerPixel / BaseStretch(windowPixels);

    /// <summary>Rounds to the nearest <see cref="StepPercent"/> (halves round up), at least <see cref="MinPercent"/>.</summary>
    public static int Snap(double percent)
    {
        if (!double.IsFinite(percent))
        {
            return OnePixelPercent;
        }

        var stepped = Math.Round(percent / StepPercent, MidpointRounding.AwayFromZero) * StepPercent;
        return (int)Math.Clamp(stepped, MinPercent, int.MaxValue);
    }
}

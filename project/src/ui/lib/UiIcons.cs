using System.Text;
using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical UI glyphs. Their SVG sources are white and receive colour from their parent control.</summary>
public enum UiIconId
{
    None = -1,
    Back, Beam, Bolt, Build, Chart, Check, ChevronDown, ChevronRight, Copy, Core, Edit, Flag, Gear,
    Height, Joint, Lock, Map, Menu, Model, More, Move, Mute, Pause, Phone, Play, Plus, Restart, Rotate,
    Scale, Select, Shadow, Sound, Speed, Stop, Trash, Trophy, Unlock, Warn, Close, Eye, PartBattery,
    PartBeam, PartBrake, PartCore, PartFuel, PartGenerator, PartCamera, PartNode, PartPiston,
    PartServo, PartSpring, PartStepper, PartVelocity, PartWheel, PartWing, Distance, TopSpeed, Elevation,
    MapFlat, MapHills, MapStairs, PartAccelerometer
}

/// <summary>The only permitted display sizes for canonical icons.</summary>
public enum UiIconSize
{
    Small,
    Standard,
    Large,
    ExtraLarge,
}

/// <summary>Central icon registry, resource loader, and control tinting helpers.</summary>
public static class UiIcons
{
    public const string UiRoot = "res://assets/icons/ui/";
    public const string PartRoot = "res://assets/icons/parts/";
    /// <summary>Side of the viewBox every UI icon SVG is authored on.</summary>
    public const float UiSourceSize = 24;
    private const float _partSourceSize = 20;

    private static readonly string[] _iconStates =
        ["icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color"];
    private static readonly Dictionary<(string Path, int PixelSize), Texture2D> _textures = [];
    private static float _cachedPixelsPerUnit = float.NaN;

    public static IReadOnlyList<UiIconId> AllIds { get; } = Enum.GetValues<UiIconId>()
        .Where(icon => icon != UiIconId.None).ToArray();

    public static int Pixels(UiIconSize size) => size switch
    {
        UiIconSize.Small => UiSize.Icon.Small,
        UiIconSize.Standard => UiSize.Icon.Default,
        UiIconSize.Large => UiSize.Icon.Large,
        UiIconSize.ExtraLarge => UiSize.Icon.ExtraLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Only canonical icon sizes are supported."),
    };

    /// <summary>The icon's raster size in window pixels at <paramref name="pixelsPerUnit"/> (see <see cref="UiScale.PixelsPerUnit()"/>).</summary>
    public static int RasterPixels(UiIconSize size, float pixelsPerUnit) => UiScale.RasterPixels(Pixels(size), pixelsPerUnit);

    public static string PathFor(UiIconId icon)
    {
        var source = SourceFor(icon);
        return source.Root + source.FileName;
    }

    /// <summary>A part glyph, drawn on a 20 grid, rather than a UI icon.</summary>
    public static bool IsPartGlyph(UiIconId icon) => icon != UiIconId.None && SourceFor(icon).Root == PartRoot;

    /// <summary>Part glyphs no longer read at <see cref="UiIconSize.Small"/>; every other pairing is allowed.</summary>
    public static bool IsAllowed(UiIconId icon, UiIconSize size) => !(IsPartGlyph(icon) && size == UiIconSize.Small);

    public static Texture2D Load(UiIconId icon, UiIconSize size)
    {
        if (!IsAllowed(icon, size))
        {
            throw new ArgumentException($"{icon} is a part glyph and is never drawn at {size}.", nameof(size));
        }

        var source = SourceFor(icon);
        return Load(source.Root + source.FileName, Pixels(size), source.SourceSize);
    }

    /// <summary>
    /// Pins one tint on the icon's normal, hover, pressed and disabled colors. It does not follow
    /// a Theme swap, so library components use the themed overload and a Theme variation
    /// instead (#338).
    /// </summary>
    public static void Apply(Button button, UiIconId icon, UiIconSize size, Color tint)
    {
        Apply(button, Load(icon, size), size, tint);
    }

    /// <summary>Applies an icon whose colors come from the button's inherited Theme.</summary>
    public static void Apply(Button button, UiIconId icon, UiIconSize size)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.Icon = Load(icon, size);
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", Pixels(size));
        foreach (var state in _iconStates)
        {
            button.RemoveThemeColorOverride(state);
        }
    }

    private static void Apply(Button button, Texture2D icon, UiIconSize size, Color tint)
    {
        button.Icon = icon;
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", Pixels(size));
        button.AddThemeColorOverride("icon_normal_color", tint);
        button.AddThemeColorOverride("icon_hover_color", tint);
        button.AddThemeColorOverride("icon_pressed_color", tint);
        button.AddThemeColorOverride("icon_disabled_color", tint);
    }

    public static TextureRect Create(UiIconId icon, UiIconSize size, Color tint) =>
        Create(Load(icon, size), size, tint);

    private static IconSource SourceFor(UiIconId icon) => icon switch
    {
        UiIconId.Back => Ui("back.svg"),
        UiIconId.Beam => Ui("beam.svg"),
        UiIconId.Bolt => Ui("bolt.svg"),
        UiIconId.Build => Ui("build.svg"),
        UiIconId.Chart => Ui("chart.svg"),
        UiIconId.Check => Ui("check.svg"),
        UiIconId.ChevronDown => Ui("chev-d.svg"),
        UiIconId.ChevronRight => Ui("chev-r.svg"),
        UiIconId.Copy => Ui("copy.svg"),
        UiIconId.Core => Ui("core.svg"),
        UiIconId.Edit => Ui("edit.svg"),
        UiIconId.Flag => Ui("flag.svg"),
        UiIconId.Gear => Ui("gear.svg"),
        UiIconId.Height => Ui("height.svg"),
        UiIconId.Joint => Ui("joint.svg"),
        UiIconId.Lock => Ui("lock.svg"),
        UiIconId.Map => Ui("map.svg"),
        UiIconId.Menu => Ui("menu.svg"),
        UiIconId.Model => Ui("model.svg"),
        UiIconId.More => Ui("more.svg"),
        UiIconId.Move => Ui("move.svg"),
        UiIconId.Mute => Ui("mute.svg"),
        UiIconId.Pause => Ui("pause.svg"),
        UiIconId.Phone => Ui("phone.svg"),
        UiIconId.Play => Ui("play.svg"),
        UiIconId.Plus => Ui("plus.svg"),
        UiIconId.Restart => Ui("restart.svg"),
        UiIconId.Rotate => Ui("rotate.svg"),
        UiIconId.Scale => Ui("scale.svg"),
        UiIconId.Select => Ui("select.svg"),
        UiIconId.Shadow => Ui("shadow.svg"),
        UiIconId.Sound => Ui("sound.svg"),
        UiIconId.Speed => Ui("speed.svg"),
        UiIconId.Stop => Ui("stop.svg"),
        UiIconId.Trash => Ui("trash.svg"),
        UiIconId.Trophy => Ui("trophy.svg"),
        UiIconId.Unlock => Ui("unlock.svg"),
        UiIconId.Warn => Ui("warn.svg"),
        UiIconId.Close => Ui("x.svg"),
        UiIconId.Eye => Ui("eye.svg"),
        UiIconId.PartBattery => Part("battery.svg"),
        UiIconId.PartBeam => Part("beam.svg"),
        UiIconId.PartBrake => Part("brake.svg"),
        UiIconId.PartCore => Part("core.svg"),
        UiIconId.PartFuel => Part("fuel.svg"),
        UiIconId.PartGenerator => Part("generator.svg"),
        UiIconId.PartCamera => Part("los.svg"),
        UiIconId.PartNode => Part("node.svg"),
        UiIconId.PartPiston => Part("piston.svg"),
        UiIconId.PartServo => Part("servo.svg"),
        UiIconId.PartSpring => Part("spring.svg"),
        UiIconId.PartStepper => Part("stepper.svg"),
        UiIconId.PartVelocity => Part("velocity.svg"),
        UiIconId.PartWheel => Part("wheel.svg"),
        UiIconId.PartWing => Part("wing.svg"),
        UiIconId.Distance => Ui("distance.svg"),
        UiIconId.TopSpeed => Ui("top-speed.svg"),
        UiIconId.Elevation => Ui("elevation.svg"),
        UiIconId.MapFlat => Ui("map-flat.svg"),
        UiIconId.MapHills => Ui("map-hills.svg"),
        UiIconId.MapStairs => Ui("map-stairs.svg"),
        UiIconId.PartAccelerometer => Part("accelerometer.svg"),
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown UI icon."),
    };

    private static IconSource Ui(string fileName) => new(UiRoot, fileName, UiSourceSize);
    private static IconSource Part(string fileName) => new(PartRoot, fileName, _partSourceSize);

    private readonly record struct IconSource(string Root, string FileName, float SourceSize);

    private static Texture2D Load(string path, int logicalPixels, float sourceSize)
    {
        var pixelsPerUnit = UiScale.PixelsPerUnit();
        if (!Mathf.IsEqualApprox(pixelsPerUnit, _cachedPixelsPerUnit))
        {
            _textures.Clear();
            _cachedPixelsPerUnit = pixelsPerUnit;
        }

        var physicalPixels = UiScale.RasterPixels(logicalPixels, pixelsPerUnit);
        var key = (path, physicalPixels);
        if (_textures.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var resourceName = path["res://assets/".Length..];
        using var stream = typeof(UiIcons).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return HandleLoadFailure(path, $"Embedded canonical SVG source could not be loaded: {resourceName}");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var source = reader.ReadToEnd();
        var image = new Image();
        var error = image.LoadSvgFromString(source, physicalPixels / sourceSize);
        if (error != Error.Ok)
        {
            return HandleLoadFailure(path, $"Canonical SVG could not be rasterized at {physicalPixels}px: {path} ({error})");
        }

        var texture = ImageTexture.CreateFromImage(image);
        _textures.Add(key, texture);
        return texture;
    }

    private static Texture2D HandleLoadFailure(string path, string message)
    {
#if DEBUG
        throw new InvalidOperationException(message);
#else
        GD.PushError(message);
        var fallback = ResourceLoader.Load<Texture2D>(path);
        if (fallback is not null)
        {
            return fallback;
        }

        GD.PushError($"Imported icon fallback could not be loaded: {path}");
        throw new InvalidOperationException(message);
#endif
    }

    private static TextureRect Create(Texture2D texture, UiIconSize size, Color tint)
    {
        var pixels = Pixels(size);
        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(pixels, pixels),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SelfModulate = tint,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }
}

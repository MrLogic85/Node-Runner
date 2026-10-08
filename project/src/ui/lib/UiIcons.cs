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
    MapFlat, MapHills, MapStairs, PartAccelerometer, ChevronLeft, Sort, Parts, Undo, Redo, PartPulse, PartTouch, BrainMark
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
    /// <summary>One folder holds every icon; the file name's prefix says its kind (docs/UI_DIRECTION.md).</summary>
    public const string Root = "res://assets/icons/";
    public const string IconPrefix = "icon_";
    public const string PartPrefix = "part_";
    public const string MarkPrefix = "mark_";
    /// <summary>Side of the viewBox every UI icon SVG is authored on.</summary>
    public const float UiSourceSize = 24;
    private const float _partSourceSize = 20;

    /// <summary>
    /// Icons sample Linear (#734). An icon rasterized for the UI size lands on fractional device
    /// pixels, where the project's Nearest doubles or drops rows. Where it is already pixel-aligned,
    /// Linear draws it exactly the same. Text keeps Nearest: see "Icon filtering" in
    /// project/src/ui/lib/AGENTS.md.
    /// </summary>
    public const CanvasItem.TextureFilterEnum IconFilter = CanvasItem.TextureFilterEnum.Linear;

    /// <summary>The project's filter, which text under an <see cref="UseIconFilter"/> item restores.</summary>
    public const CanvasItem.TextureFilterEnum TextFilter = CanvasItem.TextureFilterEnum.Nearest;

    private static readonly string[] _iconStates =
        ["icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color"];
    private static readonly Dictionary<(string Path, int Pixels), Texture2D> _textures = [];

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

    public static string PathFor(UiIconId icon) => Root + SourceFor(icon).FileName;

    /// <summary>A part glyph, drawn on a 20 grid, rather than a UI icon.</summary>
    public static bool IsPartGlyph(UiIconId icon) => icon != UiIconId.None && SourceFor(icon).FileName.StartsWith(PartPrefix, StringComparison.Ordinal);

    /// <summary>Part glyphs no longer read at <see cref="UiIconSize.Small"/>; every other pairing is allowed.</summary>
    public static bool IsAllowed(UiIconId icon, UiIconSize size) => !(IsPartGlyph(icon) && size == UiIconSize.Small);

    public static Texture2D Load(UiIconId icon, UiIconSize size)
    {
        if (!IsAllowed(icon, size))
        {
            throw new ArgumentException($"{icon} is a part glyph and is never drawn at {size}.", nameof(size));
        }

        var source = SourceFor(icon);
        return Load(Root + source.FileName, Pixels(size), source.SourceSize);
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
        button.AddThemeColorOverride("icon_hover_pressed_color", tint);
        button.AddThemeColorOverride("icon_disabled_color", tint);
    }

    public static TextureRect Create(UiIconId icon, UiIconSize size, Color tint) =>
        Create(Load(icon, size), size, tint);

    /// <summary>
    /// Samples <paramref name="item"/>'s textures with <see cref="IconFilter"/>. Only for an item whose
    /// own textures are all icons. Its children inherit the filter, so a text child takes
    /// <see cref="UseTextFilter"/>.
    /// </summary>
    public static void UseIconFilter(CanvasItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.TextureFilter = IconFilter;
    }

    /// <summary>
    /// For a component's <c>_ValidateProperty</c> when it sets <see cref="IconFilter"/> on itself: keeps
    /// the derived filter out of the Inspector and saved scenes.
    /// </summary>
    public static void HideIconFilter(Godot.Collections.Dictionary property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (property["name"].AsStringName() == CanvasItem.PropertyName.TextureFilter)
        {
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
        }
    }

    /// <summary>Gives text under an <see cref="UseIconFilter"/> item the project's <see cref="TextFilter"/> back.</summary>
    public static void UseTextFilter(CanvasItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.TextureFilter = TextFilter;
    }

    private static IconSource SourceFor(UiIconId icon) => icon switch
    {
        UiIconId.Back => Icon("back"),
        UiIconId.Beam => Icon("beam"),
        UiIconId.Bolt => Icon("bolt"),
        UiIconId.Build => Icon("build"),
        UiIconId.Chart => Icon("chart"),
        UiIconId.Check => Icon("check"),
        UiIconId.ChevronDown => Icon("chev_d"),
        UiIconId.ChevronRight => Icon("chev_r"),
        UiIconId.Copy => Icon("copy"),
        UiIconId.Core => Icon("core"),
        UiIconId.Edit => Icon("edit"),
        UiIconId.Flag => Icon("flag"),
        UiIconId.Gear => Icon("gear"),
        UiIconId.Height => Icon("height"),
        UiIconId.Joint => Icon("joint"),
        UiIconId.Lock => Icon("lock"),
        UiIconId.Map => Icon("map"),
        UiIconId.Menu => Icon("menu"),
        UiIconId.Model => Icon("model"),
        UiIconId.More => Icon("more"),
        UiIconId.Move => Icon("move"),
        UiIconId.Mute => Icon("mute"),
        UiIconId.Pause => Icon("pause"),
        UiIconId.Phone => Icon("phone"),
        UiIconId.Play => Icon("play"),
        UiIconId.Plus => Icon("plus"),
        UiIconId.Restart => Icon("restart"),
        UiIconId.Rotate => Icon("rotate"),
        UiIconId.Scale => Icon("scale"),
        UiIconId.Select => Icon("select"),
        UiIconId.Shadow => Icon("shadow"),
        UiIconId.Sound => Icon("sound"),
        UiIconId.Speed => Icon("speed"),
        UiIconId.Stop => Icon("stop"),
        UiIconId.Trash => Icon("trash"),
        UiIconId.Trophy => Icon("trophy"),
        UiIconId.Unlock => Icon("unlock"),
        UiIconId.Warn => Icon("warn"),
        UiIconId.Close => Icon("x"),
        UiIconId.Eye => Icon("eye"),
        UiIconId.PartBattery => Part("battery"),
        UiIconId.PartBeam => Part("beam"),
        UiIconId.PartBrake => Part("brake"),
        UiIconId.PartCore => Part("core"),
        UiIconId.PartFuel => Part("fuel"),
        UiIconId.PartGenerator => Part("generator"),
        UiIconId.PartCamera => Part("los"),
        UiIconId.PartNode => Part("node"),
        UiIconId.PartPiston => Part("piston"),
        UiIconId.PartServo => Part("servo"),
        UiIconId.PartSpring => Part("spring"),
        UiIconId.PartStepper => Part("stepper"),
        UiIconId.PartVelocity => Part("velocity"),
        UiIconId.PartWheel => Part("wheel"),
        UiIconId.PartWing => Part("wing"),
        UiIconId.Distance => Icon("distance"),
        UiIconId.TopSpeed => Icon("top_speed"),
        UiIconId.Elevation => Icon("elevation"),
        UiIconId.MapFlat => Icon("map_flat"),
        UiIconId.MapHills => Icon("map_hills"),
        UiIconId.MapStairs => Icon("map_stairs"),
        UiIconId.PartAccelerometer => Part("accelerometer"),
        UiIconId.ChevronLeft => Icon("chev_l"),
        UiIconId.Sort => Icon("sort"),
        UiIconId.Parts => Icon("parts"),
        UiIconId.Undo => Icon("undo"),
        UiIconId.Redo => Icon("redo"),
        UiIconId.PartPulse => Part("pulse"),
        UiIconId.PartTouch => Part("touch"),
        UiIconId.BrainMark => Mark("brain"),
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown UI icon."),
    };

    private static IconSource Icon(string name) => new(IconPrefix + name + ".svg", UiSourceSize);
    private static IconSource Mark(string name) => new(MarkPrefix + name + ".svg", UiSourceSize);
    private static IconSource Part(string name) => new(PartPrefix + name + ".svg", _partSourceSize);

    private readonly record struct IconSource(string FileName, float SourceSize);

    // A DpiTexture is sized in canvas units and re-rasterizes itself for the viewport's
    // oversampling, which includes the stretch and the UI size.
    private static Texture2D Load(string path, int pixels, float sourceSize)
    {
        var key = (path, pixels);
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
        var texture = DpiTexture.CreateFromString(reader.ReadToEnd(), pixels / sourceSize);
        if (texture.GetWidth() == 0)
        {
            return HandleLoadFailure(path, $"Canonical SVG could not be rasterized at {pixels}px: {path}");
        }

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
            TextureFilter = IconFilter,
        };
    }
}

using System.Text;
using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical UI glyphs. Their SVG sources are white and receive colour from their parent control.</summary>
public enum UiIconId
{
    None = -1,
    Back, Beam, Bolt, Build, Chart, Check, ChevronDown, ChevronRight, Copy, Core, Edit, Flag, Gear,
    Height, Joint, Lock, Map, Menu, Model, More, Move, Mute, Pause, Phone, Play, Plus, Restart, Rotate,
    Scale, Select, Shadow, Sound, Speed, Stop, Trash, Trophy, Unlock, Warn, Close, Eye,
}

/// <summary>Canonical 20-grid glyphs for build parts.</summary>
public enum UiPartIconId
{
    Battery, Beam, Brake, Core, Fuel, Generator, LineOfSight, Node, Piston, Servo, Spring, Stepper,
    Velocity, Wheel, Wing,
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
    private const float _uiSourceSize = 24;
    private const float _partSourceSize = 20;
    private static readonly Dictionary<(string Path, int PixelSize), Texture2D> _textures = [];
    private static float _cachedUiScale = float.NaN;

    public static IReadOnlyList<UiIconId> AllUiIds { get; } = Enum.GetValues<UiIconId>()
        .Where(icon => icon != UiIconId.None).ToArray();
    public static IReadOnlyList<UiPartIconId> AllPartIds { get; } = Enum.GetValues<UiPartIconId>();

    public static int Pixels(UiIconSize size) => size switch
    {
        UiIconSize.Small => 12,
        UiIconSize.Standard => 16,
        UiIconSize.Large => 20,
        UiIconSize.ExtraLarge => 24,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Only canonical icon sizes are supported."),
    };

    public static int RasterPixels(UiIconSize size, int windowWidth, int windowHeight) =>
        Mathf.Max(1, Mathf.RoundToInt(Pixels(size) * Math.Min(
            windowWidth / UiTokens.LogicalCanvasWidth,
            windowHeight / UiTokens.LogicalCanvasHeight)));

    public static string PathFor(UiIconId icon) => UiRoot + (icon switch
    {
        UiIconId.Back => "back.svg",
        UiIconId.Beam => "beam.svg",
        UiIconId.Bolt => "bolt.svg",
        UiIconId.Build => "build.svg",
        UiIconId.Chart => "chart.svg",
        UiIconId.Check => "check.svg",
        UiIconId.ChevronDown => "chev-d.svg",
        UiIconId.ChevronRight => "chev-r.svg",
        UiIconId.Copy => "copy.svg",
        UiIconId.Core => "core.svg",
        UiIconId.Edit => "edit.svg",
        UiIconId.Flag => "flag.svg",
        UiIconId.Gear => "gear.svg",
        UiIconId.Height => "height.svg",
        UiIconId.Joint => "joint.svg",
        UiIconId.Lock => "lock.svg",
        UiIconId.Map => "map.svg",
        UiIconId.Menu => "menu.svg",
        UiIconId.Model => "model.svg",
        UiIconId.More => "more.svg",
        UiIconId.Move => "move.svg",
        UiIconId.Mute => "mute.svg",
        UiIconId.Pause => "pause.svg",
        UiIconId.Phone => "phone.svg",
        UiIconId.Play => "play.svg",
        UiIconId.Plus => "plus.svg",
        UiIconId.Restart => "restart.svg",
        UiIconId.Rotate => "rotate.svg",
        UiIconId.Scale => "scale.svg",
        UiIconId.Select => "select.svg",
        UiIconId.Shadow => "shadow.svg",
        UiIconId.Sound => "sound.svg",
        UiIconId.Speed => "speed.svg",
        UiIconId.Stop => "stop.svg",
        UiIconId.Trash => "trash.svg",
        UiIconId.Trophy => "trophy.svg",
        UiIconId.Unlock => "unlock.svg",
        UiIconId.Warn => "warn.svg",
        UiIconId.Close => "x.svg",
        UiIconId.Eye => "eye.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown UI icon."),
    });

    public static string PathFor(UiPartIconId icon) => PartRoot + (icon switch
    {
        UiPartIconId.Battery => "battery.svg",
        UiPartIconId.Beam => "beam.svg",
        UiPartIconId.Brake => "brake.svg",
        UiPartIconId.Core => "core.svg",
        UiPartIconId.Fuel => "fuel.svg",
        UiPartIconId.Generator => "generator.svg",
        UiPartIconId.LineOfSight => "los.svg",
        UiPartIconId.Node => "node.svg",
        UiPartIconId.Piston => "piston.svg",
        UiPartIconId.Servo => "servo.svg",
        UiPartIconId.Spring => "spring.svg",
        UiPartIconId.Stepper => "stepper.svg",
        UiPartIconId.Velocity => "velocity.svg",
        UiPartIconId.Wheel => "wheel.svg",
        UiPartIconId.Wing => "wing.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown part icon."),
    });

    public static Texture2D Load(UiIconId icon, UiIconSize size) =>
        Load(PathFor(icon), Pixels(size), _uiSourceSize);

    public static Texture2D Load(UiPartIconId icon, UiIconSize size) =>
        Load(PathFor(icon), Pixels(size), _partSourceSize);

    public static void Apply(Button button, UiIconId icon, UiIconSize size, Color tint)
    {
        Apply(button, Load(icon, size), size, tint);
    }

    public static void Apply(Button button, UiPartIconId icon, UiIconSize size, Color tint)
    {
        Apply(button, Load(icon, size), size, tint);
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

    public static TextureRect Create(UiPartIconId icon, UiIconSize size, Color tint) =>
        Create(Load(icon, size), size, tint);

    private static Texture2D Load(string path, int logicalPixels, float sourceSize)
    {
        var uiScale = UiScale();
        if (!Mathf.IsEqualApprox(uiScale, _cachedUiScale))
        {
            _textures.Clear();
            _cachedUiScale = uiScale;
        }

        var physicalPixels = Mathf.Max(1, Mathf.RoundToInt(logicalPixels * uiScale));
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

    private static float UiScale()
    {
        var windowSize = DisplayServer.WindowGetSize();
        return Mathf.Min(
            windowSize.X / UiTokens.LogicalCanvasWidth,
            windowSize.Y / UiTokens.LogicalCanvasHeight);
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

using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical UI glyphs. Their SVG sources are white and receive colour from their parent control.</summary>
public enum UiIconId
{
    Back, Beam, Bolt, Brain, Build, Chart, Check, ChevronDown, ChevronRight, Copy, Core, Edit, Flag, Gear,
    Height, Joint, Lock, Map, Menu, More, Move, Mute, Padlock, Pause, Phone, Play, Plus, Restart, Rotate,
    Scale, Select, Shadow, Sound, Speed, Stop, Trash, Trophy, Unlock, Warn, Close,
}

/// <summary>Canonical 20-grid glyphs for build parts.</summary>
public enum UiPartIconId
{
    Battery, Beam, Brake, Core, Damper, Engine, Fuel, LineOfSight, Node, Piston, Servo, Spring, Stepper,
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

    public static IReadOnlyList<UiIconId> AllUiIds { get; } = Enum.GetValues<UiIconId>();
    public static IReadOnlyList<UiPartIconId> AllPartIds { get; } = Enum.GetValues<UiPartIconId>();

    public static int Pixels(UiIconSize size) => size switch
    {
        UiIconSize.Small => 12,
        UiIconSize.Standard => 16,
        UiIconSize.Large => 20,
        UiIconSize.ExtraLarge => 24,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Only canonical icon sizes are supported."),
    };

    public static string PathFor(UiIconId icon) => UiRoot + (icon switch
    {
        UiIconId.Back => "back.svg",
        UiIconId.Beam => "beam.svg",
        UiIconId.Bolt => "bolt.svg",
        UiIconId.Brain => "brain.svg",
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
        UiIconId.More => "more.svg",
        UiIconId.Move => "move.svg",
        UiIconId.Mute => "mute.svg",
        UiIconId.Padlock => "padlock.svg",
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
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown UI icon."),
    });

    public static string PathFor(UiPartIconId icon) => PartRoot + (icon switch
    {
        UiPartIconId.Battery => "battery.svg",
        UiPartIconId.Beam => "beam.svg",
        UiPartIconId.Brake => "brake.svg",
        UiPartIconId.Core => "core.svg",
        UiPartIconId.Damper => "damper.svg",
        UiPartIconId.Engine => "engine.svg",
        UiPartIconId.Fuel => "fuel.svg",
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

    public static Texture2D? Load(UiIconId icon) => Load(PathFor(icon));
    public static Texture2D? Load(UiPartIconId icon) => Load(PathFor(icon));

    public static void Apply(Button button, UiIconId icon, UiIconSize size, Color tint)
    {
        var texture = Require(Load(icon), PathFor(icon));
        button.Icon = texture;
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", Pixels(size));
        button.AddThemeColorOverride("icon_normal_color", tint);
        button.AddThemeColorOverride("icon_hover_color", tint);
        button.AddThemeColorOverride("icon_pressed_color", tint);
        button.AddThemeColorOverride("icon_disabled_color", tint);
    }

    public static TextureRect Create(UiIconId icon, UiIconSize size, Color tint) =>
        Create(Require(Load(icon), PathFor(icon)), size, tint);

    public static TextureRect Create(UiPartIconId icon, UiIconSize size, Color tint) =>
        Create(Require(Load(icon), PathFor(icon)), size, tint);

    private static Texture2D? Load(string path)
    {
        var texture = ResourceLoader.Load<Texture2D>(path);
        if (texture is not null)
        {
            return texture;
        }

        GD.PushError($"Canonical icon resource could not be loaded: {path}");
        return null;
    }

    private static Texture2D Require(Texture2D? texture, string path)
    {
        if (texture is not null)
        {
            return texture;
        }

        throw new InvalidOperationException($"Canonical icon resource could not be loaded: {path}");
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

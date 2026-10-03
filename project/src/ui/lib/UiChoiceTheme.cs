using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Reference indicator textures supplied through Godot's native choice theme slots.</summary>
internal static class UiChoiceTheme
{
    private const string _svgNamespace = "http://www.w3.org/2000/svg";

    public static Godot.Theme Create(Control control, bool isSwitch)
    {
        try
        {
            return CreateTheme(control, isSwitch);
        }
        catch (Exception error) when (error is InvalidOperationException or XmlException or IOException)
        {
#if DEBUG
            throw new InvalidOperationException("Unable to create native choice theme.", error);
#else
            GD.PushError($"Unable to create native choice theme; using Godot defaults: {error}");
            return new Godot.Theme();
#endif
        }
    }

    private static Godot.Theme CreateTheme(Control control, bool isSwitch)
    {
        var theme = new Godot.Theme();
        var type = isSwitch ? "CheckButton" : "CheckBox";
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            theme.SetStylebox(state, type, new StyleBoxEmpty());
        }
        theme.SetConstant("h_separation", type, UiSize.Space.S2);
        theme.SetConstant("check_v_offset", type, 0);
        theme.SetColor(isSwitch ? "button_checked_color" : "checkbox_checked_color", type, Godot.Colors.White);
        theme.SetColor(isSwitch ? "button_unchecked_color" : "checkbox_unchecked_color", type, Godot.Colors.White);

        foreach (var on in new[] { false, true })
        {
            var shapes = IndicatorShapes(control, isSwitch, on);
            var name = on ? "checked" : "unchecked";
            var enabled = Texture(shapes, isSwitch, mirrored: false, dimmed: false);
            theme.SetIcon(name, type, enabled);
            if (isSwitch)
            {
                theme.SetIcon(name + "_mirrored", type, Texture(shapes, isSwitch, mirrored: true, dimmed: false));
                theme.SetIcon(name + "_disabled_mirrored", type, Texture(shapes, isSwitch, mirrored: true, dimmed: true));
            }
            else
            {
                theme.SetIcon("radio_" + name, type, enabled);
            }

            var disabled = Texture(shapes, isSwitch, mirrored: false, dimmed: true);
            theme.SetIcon(name + "_disabled", type, disabled);
            if (!isSwitch)
            {
                theme.SetIcon("radio_" + name + "_disabled", type, disabled);
            }
        }

        return theme;
    }

    // A DpiTexture in canvas units: Godot re-rasterizes it for the stretch and the UI size.
    private static DpiTexture Texture(XElement[] shapes, bool isSwitch, bool mirrored, bool dimmed)
    {
        XNamespace ns = _svgNamespace;
        var size = UiChoiceStyle.IndicatorSize(isSwitch);
        // Group opacity dims the finished indicator, not each overlapping shape.
        var group = new XElement(ns + "g", shapes);
        if (mirrored)
        {
            group.SetAttributeValue("transform", $"translate({N(size.X)} 0) scale(-1 1)");
        }
        if (dimmed)
        {
            group.SetAttributeValue("opacity", N(UiChoiceStyle.DisabledOpacity));
        }

        var root = new XElement(ns + "svg",
            new XAttribute("width", N(size.X)), new XAttribute("height", N(size.Y)),
            new XAttribute("viewBox", $"0 0 {N(size.X)} {N(size.Y)}"),
            group);
        var texture = DpiTexture.CreateFromString(root.ToString());
        if (texture.GetWidth() == 0)
        {
            throw new InvalidOperationException("Unable to rasterize choice theme.");
        }
        texture.SetSizeOverride(new Vector2I((int)size.X, (int)size.Y));
        return texture;
    }

    private static XElement[] IndicatorShapes(Control control, bool isSwitch, bool on)
    {
        XNamespace ns = _svgNamespace;
        var size = UiChoiceStyle.IndicatorSize(isSwitch);
        var colors = UiChoiceStyle.Resolve(control, isSwitch, on);
        var stroke = UiSize.Stroke.Signal;
        var radius = isSwitch ? UiSize.Radius.Large : UiSize.Radius.Small;
        var shapes = new List<XElement>
        {
            new(ns + "rect",
                new XAttribute("x", stroke / 2), new XAttribute("y", stroke / 2),
                new XAttribute("width", size.X - stroke), new XAttribute("height", size.Y - stroke),
                new XAttribute("rx", radius - stroke / 2),
                new XAttribute("fill", "#" + colors.Background.ToHtml(false)),
                new XAttribute("fill-opacity", colors.Background.A),
                new XAttribute("stroke", "#" + colors.Border.ToHtml(false)),
                new XAttribute("stroke-width", stroke)),
        };
        if (isSwitch)
        {
            var center = UiChoiceStyle.ThumbCenter(new Rect2(Vector2.Zero, size), on);
            shapes.Add(new XElement(ns + "circle",
                new XAttribute("cx", center.X), new XAttribute("cy", center.Y),
                new XAttribute("r", UiSize.Icon.Default / 2),
                new XAttribute("fill", "#" + colors.Mark.ToHtml(false))));
        }
        else if (on)
        {
            var resource = UiIcons.PathFor(UiIconId.Check)["res://assets/".Length..];
            using var stream = typeof(UiChoiceTheme).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Missing canonical choice icon: {resource}");
            var check = XElement.Load(stream);
            // Godot's SVG rasterizer does not support nested SVG viewports.
            var glyph = new XElement(ns + "g",
                check.Attributes().Where(a => !a.IsNamespaceDeclaration
                    && a.Name.LocalName is not ("width" or "height" or "viewBox")),
                check.Nodes());
            glyph.SetAttributeValue("stroke", "#" + colors.Mark.ToHtml(false));
            glyph.SetAttributeValue("transform",
                $"translate({N((size.X - UiSize.Icon.Default) / 2)} {N((size.Y - UiSize.Icon.Default) / 2)}) scale({N(UiSize.Icon.Default / UiIcons.UiSourceSize)})");
            shapes.Add(glyph);
        }

        return [.. shapes];
    }

    private static string N(float value) => value.ToString(CultureInfo.InvariantCulture);
}

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Reference indicator textures supplied through Godot's native choice theme slots.</summary>
internal static class UiChoiceTheme
{
    public static Godot.Theme Create(UiTokens tokens, bool isSwitch)
    {
        try
        {
            return CreateTheme(tokens, isSwitch);
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

    private static Godot.Theme CreateTheme(UiTokens tokens, bool isSwitch)
    {
        var theme = new Godot.Theme();
        var type = isSwitch ? "CheckButton" : "CheckBox";
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            theme.SetStylebox(state, type, new StyleBoxEmpty());
        }
        theme.SetConstant("h_separation", type, (int)tokens.Space2);
        theme.SetConstant("check_v_offset", type, 0);
        theme.SetColor(isSwitch ? "button_checked_color" : "checkbox_checked_color", type, Godot.Colors.White);
        theme.SetColor(isSwitch ? "button_unchecked_color" : "checkbox_unchecked_color", type, Godot.Colors.White);

        foreach (var on in new[] { false, true })
        {
            using var image = RenderIndicator(tokens, isSwitch, on);
            var size = UiChoiceStyle.IndicatorSize(tokens, isSwitch);
            var name = on ? "checked" : "unchecked";
            var enabled = Texture(image, size);
            theme.SetIcon(name, type, enabled);
            if (isSwitch)
            {
                using var mirrored = (Image)image.Duplicate();
                mirrored.FlipX();
                theme.SetIcon(name + "_mirrored", type, Texture(mirrored, size));
                Dim(mirrored);
                theme.SetIcon(name + "_disabled_mirrored", type, Texture(mirrored, size));
            }
            else
            {
                theme.SetIcon("radio_" + name, type, enabled);
            }

            // Dim the finished texture, not overlapping vector primitives.
            Dim(image);
            var disabled = Texture(image, size);
            theme.SetIcon(name + "_disabled", type, disabled);
            if (!isSwitch)
            {
                theme.SetIcon("radio_" + name + "_disabled", type, disabled);
            }
        }

        return theme;
    }

    private static ImageTexture Texture(Image image, Vector2 size)
    {
        var texture = ImageTexture.CreateFromImage(image);
        texture.SetSizeOverride(new Vector2I((int)size.X, (int)size.Y));
        return texture;
    }

    private static void Dim(Image image)
    {
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var color = image.GetPixel(x, y);
                color.A *= UiChoiceStyle.DisabledOpacity;
                image.SetPixel(x, y, color);
            }
        }
    }

    private static Image RenderIndicator(UiTokens tokens, bool isSwitch, bool on)
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        var size = UiChoiceStyle.IndicatorSize(tokens, isSwitch);
        var colors = UiChoiceStyle.Resolve(tokens, isSwitch, on);
        var stroke = tokens.StrokeSignal;
        var radius = isSwitch ? tokens.RadiusLarge : tokens.RadiusSmall;
        var root = new XElement(ns + "svg",
            new XAttribute("width", size.X), new XAttribute("height", size.Y),
            new XAttribute("viewBox", $"0 0 {N(size.X)} {N(size.Y)}"),
            new XElement(ns + "rect",
                new XAttribute("x", stroke / 2), new XAttribute("y", stroke / 2),
                new XAttribute("width", size.X - stroke), new XAttribute("height", size.Y - stroke),
                new XAttribute("rx", radius - stroke / 2),
                new XAttribute("fill", "#" + colors.Background.ToHtml(false)),
                new XAttribute("fill-opacity", colors.Background.A),
                new XAttribute("stroke", "#" + colors.Border.ToHtml(false)),
                new XAttribute("stroke-width", stroke)));
        if (isSwitch)
        {
            var center = UiChoiceStyle.ThumbCenter(tokens, new Rect2(Vector2.Zero, size), on);
            root.Add(new XElement(ns + "circle",
                new XAttribute("cx", center.X), new XAttribute("cy", center.Y),
                new XAttribute("r", tokens.Icon / 2),
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
                $"translate({N((size.X - tokens.Icon) / 2)} {N((size.Y - tokens.Icon) / 2)}) scale({N(tokens.Icon / 24)})");
            root.Add(glyph);
        }

        var window = DisplayServer.WindowGetSize();
        var scale = Mathf.Max(1, Mathf.Min(window.X / UiTokens.LogicalCanvasWidth, window.Y / UiTokens.LogicalCanvasHeight));
        var image = new Image();
        var error = image.LoadSvgFromString(root.ToString(), scale);
        if (error != Error.Ok)
        {
            image.Dispose();
            throw new InvalidOperationException($"Unable to rasterize choice theme: {error}");
        }
        return image;
    }

    private static string N(float value) => value.ToString(CultureInfo.InvariantCulture);
}

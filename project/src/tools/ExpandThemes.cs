using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Tools;

/// <summary>
/// Regenerates the palette-derived color items in every theme file after a palette color changed:
/// <c>Godot --headless --path project res://scenes/tools/ExpandThemes.tscn</c>.
/// Effects Lite is written from Neon's palette with effects off, so it has no authored values of its own.
/// </summary>
public partial class ExpandThemes : Node
{
    public override void _Ready()
    {
        var failed = false;
        try
        {
            var neon = UiThemes.Neon;
            var paper = UiThemes.Paper;
            UiThemeExpander.Expand(neon);
            UiThemeExpander.Expand(paper);
            var lite = LoadOrCreate(UiThemes.PathFor(UiTokenType.Light));
            UiThemeExpander.ExpandVariant(neon, lite, UiTokens.Flag.EffectsEnabled, enabled: false);
            failed |= !Save(neon, UiThemes.PathFor(UiTokenType.Neon));
            failed |= !Save(paper, UiThemes.PathFor(UiTokenType.Paper));
            failed |= !Save(lite, UiThemes.PathFor(UiTokenType.Light));
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            failed = true;
        }
        GetTree().Quit(failed ? 1 : 0);
    }

    private static Godot.Theme LoadOrCreate(string path) =>
        ResourceLoader.Exists(path) ? GD.Load<Godot.Theme>(path) : new Godot.Theme();

    private static bool Save(Godot.Theme theme, string path)
    {
        // Outside the editor the saver drops the file's uid; restore it so references stay stable.
        // Font ext_resource uids are also dropped; they are optional and the editor re-adds them.
        var uid = ResourceLoader.GetResourceUid(path);
        var error = ResourceSaver.Save(theme, path);
        if (error == Error.Ok && uid != ResourceUid.InvalidId)
        {
            error = ResourceSaver.SetUid(path, uid);
        }
        if (error != Error.Ok)
        {
            GD.PushError($"Could not save {path}: {error}");
            return false;
        }
        GD.Print($"Expanded {path}");
        return true;
    }
}

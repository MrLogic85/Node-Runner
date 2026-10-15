using System.Globalization;
using Godot;

namespace NodeRunner.Tools;

/// <summary>
/// Runs a scene for some frames, saves what the window shows as a PNG and quits, so an agent can see a
/// desktop check; <c>.claude/skills/desktop-check</c> has the command. Godot's Movie Maker
/// (<c>--write-movie</c>) is not enough: it writes every frame at the 640 × 360 base size, whatever
/// <c>--resolution</c> says. The scene is added beside this node, so <c>CurrentScene</c> is the tool, and
/// errors that the scene itself logs do not change the exit code.
/// </summary>
public partial class Screenshot : Node
{
    private const int _defaultFrames = 60;
    private const string _usage = "Usage: -- <res://scene.tscn> <absolute path.png> [frames, at least 1]";

    private string _output = "";
    private int _framesLeft;

    public override void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            if (args.Length is < 2 or > 3 || !Path.IsPathRooted(args[1]))
            {
                throw new ArgumentException(_usage);
            }
            var scene = ResourceLoader.Load<PackedScene>(args[0])
                ?? throw new ArgumentException($"No scene at {args[0]}. {_usage}");
            _output = args[1];
            var frames = args.Length == 3 ? int.Parse(args[2], CultureInfo.InvariantCulture) : _defaultFrames;
            if (frames < 1)
            {
                throw new ArgumentException(_usage);
            }
            // The scene joins the tree at the end of the first frame, so its first drawn frame is read one later.
            _framesLeft = frames + 1;
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, scene.Instantiate());
        }
        catch (Exception exception)
        {
            Finish(exception.ToString());
        }
    }

    public override void _Process(double delta)
    {
        if (--_framesLeft > 0)
        {
            return;
        }
        try
        {
            var image = GetViewport().GetTexture().GetImage();
            if (image is null || image.IsEmpty())
            {
                Finish("The window has no image; a --headless run renders nothing.");
                return;
            }
            var error = image.SavePng(_output);
            Finish(error == Error.Ok ? null : $"Could not save {_output}: {error}");
        }
        catch (Exception exception)
        {
            Finish(exception.ToString());
        }
    }

    private void Finish(string? failure)
    {
        SetProcess(false);
        if (failure is null)
        {
            GD.Print($"Saved {_output}");
        }
        else
        {
            GD.PushError(failure);
        }
        GetTree().Quit(failure is null ? 0 : 1);
    }
}

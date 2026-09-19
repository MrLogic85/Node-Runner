using Godot;
using NodeRunner.App.Services;

namespace NodeRunner.Managers;

/// <summary>
/// Godot autoload exposing the app's single seeded RNG source (see
/// project/src/managers/AGENTS.md). Reseeds itself with a time-based seed on
/// startup so out-of-the-box runs vary, but logs the seed so any run can be
/// reproduced by reseeding with the same value.
/// </summary>
public partial class RngProvider : Node, IRngProvider
{
    public int Seed { get; private set; }

    public Random Random { get; private set; } = new();

    public override void _Ready()
    {
        Reseed(System.Environment.TickCount);
    }

    public void Reseed(int seed)
    {
        Seed = seed;
        Random = new Random(seed);
        GD.Print($"Node Runner RNG seed: {seed}");
    }
}

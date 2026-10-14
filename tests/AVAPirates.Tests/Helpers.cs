using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.World;

namespace AVAPirates.Tests;

internal static class Helpers
{
    /// <summary>A folder for saved games that isn't the user's own.</summary>
    public static SaveStore TempSaves() =>
        new(Path.Combine(Path.GetTempPath(), "avapirates-tests-" + Guid.NewGuid().ToString("N")));

    /// <summary>A new career, in its first port, with the welcome read.</summary>
    public static GameEngine NewCareer(Nation homeland = Nation.Fennick, int seed = 1, int difficulty = 1)
    {
        var g = new GameEngine(seed, TempSaves());
        g.StartCareer("Ash", homeland, difficulty);
        g.CloseAll();
        g.PortMenu();
        return g;
    }

    /// <summary>A new career, already out at sea.</summary>
    public static GameEngine AtSea(Nation homeland = Nation.Fennick, int seed = 1)
    {
        var g = NewCareer(homeland, seed);
        g.LeavePort();
        g.Traffic.Ships.Clear();
        return g;
    }

    /// <summary>Runs the clock for a while, a sixtieth of a second at a time.</summary>
    public static void Run(GameEngine g, double seconds)
    {
        for (double t = 0; t < seconds; t += GameEngine.Step) g.Update(GameEngine.Step);
    }

    public static Port Port(string name) => GameEngine.Map.Ports.First(p => p.Name == name);
}

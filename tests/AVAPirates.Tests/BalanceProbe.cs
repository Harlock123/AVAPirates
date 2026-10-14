using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Sea;

namespace AVAPirates.Tests;

/// <summary>
/// Measures how sea fights and sword fights tend to go, for tuning, with the computer's own helm
/// and sword standing in for the player. Runs only with PIRATES_BALANCE=1, and prints a table.
/// </summary>
public sealed class BalanceProbe(ITestOutputHelper output)
{
    [Fact]
    public void Measure()
    {
        if (Environment.GetEnvironmentVariable("PIRATES_BALANCE") != "1") return;
        foreach (var (mine, crew, theirs, theirCrew, role) in new[]
        {
            (ShipKind.Sloop, 50, ShipKind.Merchantman, 30, Role.Merchant),
            (ShipKind.Sloop, 50, ShipKind.Sloop, 50, Role.Pirate),
            (ShipKind.Brigantine, 100, ShipKind.Brigantine, 90, Role.Warship),
            (ShipKind.Brigantine, 100, ShipKind.Frigate, 160, Role.Warship),
            (ShipKind.Frigate, 180, ShipKind.Galleon, 180, Role.Treasure),
        })
        {
            var ends = new Dictionary<BattleEnd, int>();
            double time = 0, hull = 0;
            int n = 60;
            for (int i = 0; i < n; i++)
            {
                var g = new GameEngine(i + 1, Helpers.TempSaves());
                g.StartCareer("Probe", Nation.Fennick, 1);
                g.CloseAll();
                g.World.Fleet[0] = Ship.Make(mine, "Probe");
                g.World.Crew = crew;
                g.LeavePort();
                var foe = new SeaShip { Id = 1, Nation = role == Role.Pirate ? Nation.Outlaw : Nation.Valdoro, Role = role, Ship = Ship.Make(theirs, "Foe"), Crew = theirCrew };
                g.Attack(foe);
                var b = g.Battle!;
                for (int t = 0; t < 60 * 180 && b.End == BattleEnd.None; t++) b.Tick(GameEngine.Step, g.AutoHelm());
                ends[b.End] = ends.GetValueOrDefault(b.End) + 1;
                time += b.Time;
                hull += b.Player.Ship.Hull / (double)b.Player.Ship.Type.Hull;
            }
            output.WriteLine($"{mine} vs {role} {theirs}: " + string.Join(", ", ends.OrderBy(e => e.Key).Select(e => $"{e.Key} {e.Value * 100 / n}%")) + $"  avg {time / n:0}s, hull left {hull / n:P0}");
        }
        foreach (double skill in new[] { 0.35, 0.52, 0.69, 0.86 })
        foreach (var (my, their) in new[] { (100, 100), (150, 60), (50, 120) })
        {
            int won = 0, n = 100;
            double time = 0;
            for (int i = 0; i < n; i++)
            {
                var g = new GameEngine(i + 7, Helpers.TempSaves());
                g.StartCareer("Probe", Nation.Fennick, 1);
                g.CloseAll();
                g.StartDuel("Foe", my, their, skill);
                var d = g.Duel!;
                for (int t = 0; t < 60 * 120 && d.End == DuelEnd.None; t++) d.Tick(GameEngine.Step, g.AutoGuard());
                if (d.End == DuelEnd.Won) won++;
                time += d.Time;
            }
            output.WriteLine($"duel skill {skill:0.00}, crews {my} v {their}: won {won * 100 / n}%  avg {time / n:0.0}s");
        }
    }
}

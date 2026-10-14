using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAPirates.Sea;
using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Views;
using AVAPirates.World;

namespace AVAPirates.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set PIRATES_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, Helpers.TempSaves());
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("PIRATES_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAPirates-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }


    [AvaloniaFact]
    public void Title_and_a_new_captain()
    {
        var (w, c, g) = Open();
        c.Tick(1.5);
        Shot(w, c, "title");
        g.Press("Enter");
        g.Type("Ash");
        g.Press("Enter");
        g.Top!.Selected = 1;
        Shot(w, c, "flag");
    }

    [AvaloniaFact]
    public void A_port_and_its_people()
    {
        var (w, c, g) = Open();
        g.StartCareer("Ash", Nation.Fennick, 1);
        c.Tick(0.2);
        Shot(w, c, "welcome");
        g.CloseAll();
        g.PortMenu();
        Shot(w, c, "port");
        g.Press("1");
        Shot(w, c, "governor");
        g.Press("Esc");
        g.Press("Esc");
        g.PortMenu();
        g.Top!.Selected = 1;
        g.Press("Enter");
        Shot(w, c, "tavern");
        g.PortMenu();
        g.World.Cargo[1] = 12;
        g.Top!.Selected = 2;
        g.Press("Enter");
        Shot(w, c, "merchant");
    }

    [AvaloniaFact]
    public void At_sea()
    {
        var (w, c, g) = Open(4);
        g.StartCareer("Ash", Nation.Fennick, 1);
        g.CloseAll();
        g.World.Fleet[0] = Ship.Make(ShipKind.Brigantine, "Merry Lark");
        g.World.MapPieces = 2;
        g.BuryTreasure();
        g.LeavePort();
        for (int i = 0; i < 60 * 6; i++) { g.Update(GameEngine.Step); }
        c.Tick(0.01);
        Shot(w, c, "sea");
        g.Press("M");
        c.Tick(0.01);
        Shot(w, c, "chart");
        g.Press("M");
        var s = g.Traffic.Ships.First(s => s.Role == AVAPirates.Sea.Role.Merchant);
        g.Meet(s);
        c.Tick(0.01);
        Shot(w, c, "encounter");
    }

    [AvaloniaFact]
    public void A_sea_fight_and_a_boarding()
    {
        var (w, c, g) = Open(7);
        g.StartCareer("Ash", Nation.Fennick, 1);
        g.CloseAll();
        g.World.Fleet[0] = Ship.Make(ShipKind.Frigate, "Dauntless");
        g.World.Crew = 160;
        g.LeavePort();
        var foe = new AVAPirates.Sea.SeaShip { Id = 999, Nation = Nation.Valdoro, Role = AVAPirates.Sea.Role.Warship, Ship = Ship.Make(ShipKind.Brigantine, "Saint Orla"), Crew = 90, X = g.World.X, Y = g.World.Y };
        g.Attack(foe);
        var b = g.Battle!;
        for (int i = 0; i < 60 * 12 && b.End == AVAPirates.Sea.BattleEnd.None; i++)
        {
            var h = g.AutoHelm();
            g.Held.Clear();
            if (h.Left) g.Held.Add("Left");
            if (h.Right) g.Held.Add("Right");
            if (h.Fire) g.Press("Space");
            g.Update(GameEngine.Step);
            if (b.Balls.Count > 6 && b.Time > 4) break;
        }
        c.Tick(0.001);
        Shot(w, c, "battle");
        g.StartDuel("the captain of the Saint Orla", 120, 80, 0.5, boarding: foe);
        var d = g.Duel!;
        for (int i = 0; i < 60 * 2; i++) { g.Held.Clear(); g.Held.Add("Right"); g.Update(GameEngine.Step); }
        g.Held.Clear();
        g.Held.Add("Up");
        g.Press("Space");
        for (int i = 0; i < 8; i++) g.Update(GameEngine.Step);
        c.Tick(0.001);
        Shot(w, c, "duel");
    }

    [AvaloniaFact]
    public void Storming_a_town_and_a_promotion()
    {
        var (w, c, g) = Open(9);
        g.StartCareer("Ash", Nation.Fennick, 1);
        g.CloseAll();
        g.World.Fleet[0] = Ship.Make(ShipKind.Frigate, "Dauntless");
        g.World.Crew = 120;
        g.LeavePort();
        var town = GameEngine.Map.Ports.First(p => g.World.PortNation[p.Id] == Nation.Valdoro && p.Size == 3);
        g.AttackTown(town);
        var d = g.Duel!;
        for (int i = 0; i < 60 * 3 && d.End == DuelEnd.None; i++) { d.Tick(GameEngine.Step, g.AutoGuard()); }
        c.Tick(0.001);
        Shot(w, c, "storm");
        g.Duel!.Enemy.Morale = 0;
        for (int i = 0; i < 200; i++) g.Update(GameEngine.Step);
        c.Tick(0.001);
        Shot(w, c, "sacked");
        g.CloseAll();
        var home = GameEngine.Map.Ports.First(p => g.World.PortNation[p.Id] == Nation.Fennick);
        g.World.Favour[(int)Nation.Fennick] = 15;
        g.EnterPort(home, welcomed: true);
        g.Press("1");
        c.Tick(0.2);
        Shot(w, c, "promotion");
    }

    [AvaloniaFact]
    public void Retirement()
    {
        var (w, c, g) = Open();
        g.StartCareer("Ash", Nation.Lusande, 1);
        g.CloseAll();
        g.World.Wealth = 9000;
        g.World.Rank[2] = 5;
        g.World.Land = 400;
        g.World.ShipsTaken = 14;
        g.World.Day = 360 * 18;
        g.Retire();
        c.Tick(0.2);
        Shot(w, c, "retired");
    }
}

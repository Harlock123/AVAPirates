using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

public sealed partial class GameEngine
{
    public static readonly string[] DifficultyNames = ["Apprentice", "Journeyman", "Adventurer", "Swashbuckler"];

    private string _newName = "";

    public void ShowTitle()
    {
        Mode = Mode.Title;
        Demo = null;
        Battle = null;
        Duel = null;
        Idle = 0;
        Held.Clear();
        CloseAll();
        Play(Sfx.MusicStop);
        Play(Sfx.TitleTune);
        var p = new Panel { Title = "AVAPirates" };
        p.Add("Begin a new career", NewCareer);
        p.Add("Continue a saved career", LoadMenu, Saves.Any());
        p.Add("How to play", Help);
        p.Add("Quit", () => QuitRequested?.Invoke());
        Push(p);
    }

    private void NewCareer()
    {
        var p = new Panel { Title = "A new captain", Entry = "", Prompt = "Your name:", Back = Pop };
        p.Say("You have a little money, a small ship and a crew who'll follow anyone for a share of the plunder. What do they call you?");
        p.OnEntry = name =>
        {
            if (name.Length == 0) return;
            _newName = name;
            Pop();
            ChooseNation();
        };
        Push(p);
    }

    private void ChooseNation()
    {
        var p = new Panel { Title = "Under which flag?", Back = Pop };
        p.Say("You'll start in one of its ports, with a letter from its governor. Its enemies will be yours, at first.");
        foreach (var n in Nations.Crowns)
        {
            var nation = n;
            var info = Nations.Get(n);
            p.Add(info.Name, () => { Pop(); ChooseDifficulty(nation); }, note: info.About);
        }
        Push(p);
    }

    private void ChooseDifficulty(Nation n)
    {
        var p = new Panel { Title = "How hard a life?", Back = Pop };
        p.Say("The harder it is, the better the swordsmen you'll meet, and the bigger the captain's share of the plunder.");
        string[] notes =
        [
            "Clumsy swordsmen and timid gunners. A captain's share of a tenth.",
            "A fair fight. A captain's share of a fifth.",
            "Sharp blades and sharper gunners. A captain's share of a quarter.",
            "The best in the isles. A captain's share of a third.",
        ];
        for (int i = 0; i < 4; i++)
        {
            int d = i;
            p.Add(DifficultyNames[i], () => StartCareer(_newName, n, d), note: notes[i]);
        }
        p.Selected = 1;
        Push(p);
    }

    /// <summary>Starts a career: a sloop, a small crew, and a berth in the homeland's greatest port.</summary>
    public void StartCareer(string name, Nation homeland, int difficulty = 1)
    {
        var w = new WorldState
        {
            Captain = name,
            Homeland = homeland,
            Difficulty = difficulty,
            Crew = 40,
            Food = 60,
            Gold = 300,
            Morale = 75,
            Seed = Rng.State,
            WindAngle = Rng.Next(360),
        };
        w.Fleet.Add(Ship.Make(ShipKind.Sloop, Traffic.NameShip()));
        w.PortNation = Map.Ports.Select(p => p.Nation).ToArray();
        w.PortWealth = Map.Ports.Select(p => p.Wealth).ToArray();
        w.PortSoldiers = Map.Ports.Select(p => p.Soldiers).ToArray();
        w.SetWar(Nation.Valdoro, Nation.Fennick, true);
        w.SetWar(Nation.Lusande, Nation.Brannmark, Rng.Chance(50));
        w.SetWar(Nation.Valdoro, Nation.Lusande, Rng.Chance(30));
        foreach (var (rival, ship, bounty) in Traffic.RivalNames) w.Rivals.Add(new Rival { Name = rival, Ship = ship, Bounty = bounty });
        w.Favour[(int)homeland] = 1;
        World = w;
        _lastMonth = 0;
        Traffic = new Traffic(Map, Rng);
        Log.Clear();
        CloseAll();
        Held.Clear();
        var home = Map.Ports.Where(p => w.PortNation[p.Id] == homeland).OrderByDescending(p => p.Size).ThenBy(p => p.Id).First();
        w.X = home.DockX + 0.5;
        w.Y = home.DockY + 0.5;
        Traffic.Populate(w, all: true);
        w.Note($"Captain {name} takes command of the sloop {w.Flagship.Name}, at {home.Name}.");
        Say($"Captain {name}, of the sloop {w.Flagship.Name}.", 0xFFFFD860);
        EnterPort(home, welcomed: true);
        Tell(home.Name, $"The governor of {home.Name} has given you a letter: you sail with {Nations.Get(homeland).Name}'s blessing. " +
            $"{Nations.Get(homeland).Name}'s enemies are fair game, and every prize you take from them raises you in its favour. " +
            "Your crew have signed on for a share of whatever you win. Keep them fed, keep them busy, and share the plunder out before they lose patience.", kind: PanelKind.Port);
    }

    // ───────────────────────────── help ─────────────────────────────

    public void Help()
    {
        var p = new Panel { Title = "How to play", Kind = PanelKind.Big, Back = Pop };
        p.Say("Sail the Saltwind Isles, take prizes, find treasure, rise in the crowns' favour, and retire rich - before your crew, your enemies or old age catch up with you.");
        p.Say("At sea: Left and Right steer; Up hoists sail, Down takes it in. The wind's arrow is by the compass: you go fastest across it, slower before it, and hardly at all into it. Sail into a port to go ashore. Steer into another ship to meet her. M shows the chart; D digs for treasure on a beach; Esc for the menu.");
        p.Say("Sea fights: Left and Right steer, Up and Down set full sail or battle sail (slower, but turns tighter), Space fires whichever broadside faces her. Run alongside to board her.");
        p.Say("Sword fights: hold Up to guard high, Down to guard low, neither for the middle. Left and Right step. Space lunges at the height you're guarding. A lunge where they're guarding is parried - look for the gap. Drive them back over the rail, or break their heart.");
        p.Say("Ports: the governor grants ranks and land for prizes taken from his crown's enemies; the tavern signs on men and hears rumours (and sells pieces of treasure maps); the merchant buys and sells; the shipwright mends ships. Divide the plunder to bank your share and keep the crew happy.");
        p.Add("Close", Pop);
        Push(p);
    }

    // ───────────────────────────── the attract demo ─────────────────────────────

    public void StartDemo()
    {
        Rng = new Rng((ulong)(Clock * 1000) + 11);
        StartCareer("Demo", Nation.Fennick, 1);
        World.Fleet[0] = Ship.Make(ShipKind.Brigantine, "Merry Lark");
        World.Crew = 100;
        CloseAll();
        LeavePort();
        Demo = new DemoPilot(this);
    }

    public void EndDemo()
    {
        Demo = null;
        World = new WorldState();
        ShowTitle();
    }
}

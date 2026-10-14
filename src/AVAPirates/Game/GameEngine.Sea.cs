using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

public sealed partial class GameEngine
{
    /// <summary>Squares a second the fleet is making through the water.</summary>
    public double Speed { get; private set; }
    public bool ShowChart { get; set; }
    /// <summary>A moment of red when the fleet runs aground.</summary>
    public double Aground { get; private set; }
    private double _windTimer = 12;
    private readonly HashSet<int> _passed = [];
    private int _lastMonth = -1;

    /// <summary>Puts to sea from the port the fleet is in.</summary>
    public void LeavePort()
    {
        CloseAll();
        if (World.Crew < 8)
        {
            Tell("Not enough hands", "You need at least eight men to work a ship. Sign some on at the tavern.", PortMenu);
            return;
        }
        Mode = Mode.Sea;
        InPort = null;
        Speed = 0;
        World.SailsUp = true;
        // Point her out to sea.
        double best = World.Heading;
        int most = -1;
        for (int a = 0; a < 360; a += 45)
        {
            var (vx, vy) = Sailing.Vector(a);
            int open = 0;
            for (int i = 1; i <= 4; i++) if (Map.Water(World.X + vx * i, World.Y + vy * i)) open++;
            if (open > most) { most = open; best = a; }
        }
        World.Heading = best;
        _passed.Clear();
        foreach (var s in Traffic.Ships.Where(s => Traffic.Dist(s, World.X, World.Y) < 3)) _passed.Add(s.Id);
        Play(Sfx.MusicStop);
        Play(Sfx.SeaMusic);
        Say($"The {World.Flagship.Name} puts to sea. The wind is from the {Sailing.Compass(World.WindAngle + 180)}.");
    }

    private void SeaKey(string key)
    {
        switch (key)
        {
            case "M" or "Tab": ShowChart = !ShowChart; Play(Sfx.Menu); break;
            case "D": Dig(); break;
            case "Enter": TryPort(); break;
            case "Esc": SeaMenu(); break;
            case "H": Help(); break;
            case "J": JournalPanel(); break;
            case "S": StatusPanel(); break;
        }
    }

    private void SeaTick(double dt)
    {
        var w = World;
        if (Aground > 0) Aground -= dt;
        // The helm and the sails.
        if (Held.Contains("Left")) w.Heading = Sailing.Norm(w.Heading - w.Flagship.Type.Agility * 0.8 * dt);
        if (Held.Contains("Right")) w.Heading = Sailing.Norm(w.Heading + w.Flagship.Type.Agility * 0.8 * dt);
        if (Pressed("Up")) w.SailsUp = true;
        if (Pressed("Down")) w.SailsUp = false;
        double target = w.SailsUp ? w.FleetSpeed * Sailing.PointOfSail(w.Heading, w.WindAngle) * w.WindStrength : 0;
        Speed += (target - Speed) * Math.Min(1, dt * 1.2);
        var (vx, vy) = Sailing.Vector(w.Heading);
        double nx = w.X + vx * Speed * dt, ny = w.Y + vy * Speed * dt;
        if (Map.Water(nx, ny)) (w.X, w.Y) = (nx, ny);
        else
        {
            // Land ahead: a port, if there's one here, or else run aground and stop.
            if (NearPort(1.6) is { } port)
            {
                ArriveAt(port);
                return;
            }
            if (Speed > 0.4) { Aground = 0.25; Play(Sfx.Bump); }
            Speed = 0;
        }
        // The wind shifts now and then.
        _windTimer -= dt;
        if (_windTimer <= 0)
        {
            _windTimer = 10 + Rng.Next(15);
            w.WindAngle = Sailing.Norm(w.WindAngle + Rng.Range(-30, 30));
            w.WindStrength = Math.Clamp(w.WindStrength + Rng.Range(-15, 15) / 100.0, 0.7, 1.25);
        }
        PassTime(dt / WorldState.SecondsPerDay);
        if (Mode != Mode.Sea || Top is not null) return;
        Traffic.Update(dt, w);
        if (Rng.Next(600) == 0) Traffic.Populate(w);
        // Ships close enough to meet.
        foreach (var s in Traffic.Ships.ToList())
        {
            double d = Traffic.Dist(s, w.X, w.Y);
            if (d > 3.5) _passed.Remove(s.Id);
            if (d < 1.1 && !_passed.Contains(s.Id))
            {
                _passed.Add(s.Id);
                Meet(s);
                return;
            }
        }
    }

    /// <summary>The port whose quay is within a distance of the fleet, if any.</summary>
    public Port? NearPort(double within) =>
        Map.Ports.OrderBy(p => Math.Abs(p.DockX + 0.5 - World.X) + Math.Abs(p.DockY + 0.5 - World.Y))
            .FirstOrDefault(p => Math.Sqrt(Math.Pow(p.DockX + 0.5 - World.X, 2) + Math.Pow(p.DockY + 0.5 - World.Y, 2)) < within
                || Math.Sqrt(Math.Pow(p.X + 0.5 - World.X, 2) + Math.Pow(p.Y + 0.5 - World.Y, 2)) < within);

    private void TryPort()
    {
        if (NearPort(2.2) is { } port) ArriveAt(port);
        else Say("There's no port close enough.");
    }

    // ───────────────────────────── time ─────────────────────────────

    /// <summary>Days go by: food is eaten, and each new month brings its news.</summary>
    public void PassTime(double days)
    {
        var w = World;
        w.Day += days;
        w.Food -= days * w.Crew / 40.0;
        if (w.Food < 0)
        {
            w.Food = 0;
            if (Rng.Next(100) < 40 * days + 1) w.Morale = Math.Max(0, w.Morale - 1);
        }
        int month = (int)(w.Day / WorldState.DaysPerMonth);
        while (_lastMonth < month)
        {
            _lastMonth++;
            NewMonth(_lastMonth % 12);
        }
    }

    /// <summary>A new month begins (0 is the first of the year).</summary>
    private void NewMonth(int month)
    {
        var w = World;
        // The crew grow restless the longer the plunder goes unshared, and quicker hungry.
        double months = (w.Day - w.LastDivision) / WorldState.DaysPerMonth;
        int drop = 3 + (months > 4 ? (int)(months - 4) * 2 : 0) + (w.Food <= 0 ? 8 : 0) - (w.Gold > w.Crew * 40 ? 2 : 0);
        w.Morale = Math.Clamp(w.Morale - Math.Max(1, drop), 0, 100);
        if (w.Morale < 20 && w.Crew > 0)
        {
            int gone = Math.Max(1, w.Crew / 8);
            w.Crew -= gone;
            Say($"{gone} men desert, grumbling about their share.", 0xFFFF8060);
        }
        if (w.Morale <= 5 && Mode == Mode.Sea && !InDemo) Mutiny();
        // The crowns make war and peace.
        if (Rng.Chance(18))
        {
            var a = Nations.Crowns[Rng.Next(4)];
            var b = Nations.Crowns.Where(n => n != a).ElementAt(Rng.Next(3));
            bool war = !w.AtWar(a, b);
            if (a == Nation.Valdoro && b == Nation.Fennick || b == Nation.Valdoro && a == Nation.Fennick) war = Rng.Chance(80);
            if (w.AtWar(a, b) != war)
            {
                w.SetWar(a, b, war);
                string news = war ? $"{Nations.Get(a).Name} has declared war on {Nations.Get(b).Name}!" : $"{Nations.Get(a).Name} and {Nations.Get(b).Name} have made peace.";
                Say(news, 0xFF80C0FF);
                w.Note(news);
            }
        }
        // Ports recover; the anger of the crowns cools, slowly.
        for (int i = 0; i < Map.Ports.Count; i++)
        {
            w.PortWealth[i] = Math.Min(Map.Ports[i].Wealth * 2, w.PortWealth[i] + Map.Ports[i].Wealth / 20 + 20);
            w.PortSoldiers[i] = Math.Min(Map.Ports[i].Soldiers, w.PortSoldiers[i] + 5);
        }
        for (int i = 0; i < 5; i++) w.Anger[i] = Math.Max(0, w.Anger[i] - 2);
        // Twice a year the treasure galleon sails.
        if (month is 3 or 9 && Traffic.SendTreasure(w) is { } galleon)
        {
            w.Flags.Add("galleon");
            Say("Word on the wind: the Valdoran treasure galleon has sailed.", 0xFFFFD860);
            w.Note($"The treasure galleon sailed, bound for {Map.Ports[galleon.Destination].Name}.");
        }
        if (month == 0) Say($"A new year: you are {w.Age}.", 0xFFC0C0C0);
    }

    private void Mutiny()
    {
        var w = World;
        int lost = w.Crew / 2;
        int gold = w.Gold / 2;
        w.Crew -= lost;
        w.Gold -= gold;
        w.Morale = 45;
        w.Note($"Mutiny! {lost} men took {gold} gold and a boat.");
        Play(Sfx.Defeat);
        Tell("Mutiny!", $"The crew have had enough. {lost} of them take {gold} gold of the plunder and row off in the longboat. " +
            "The rest stay, for now. Share out the plunder in port before it happens again.");
    }

    // ───────────────────────────── ships met at sea ─────────────────────────────

    public void Meet(SeaShip s)
    {
        var w = World;
        Speed *= 0.3;
        bool hostile = Traffic.Hostile(s, w);
        var info = Nations.Get(s.Nation);
        var p = new Panel { Title = Cap(s.Describe()), Kind = PanelKind.Encounter, Back = null };
        p.Say(hostile
            ? $"{Cap(s.Describe())} bears down on you, guns run out! She carries about {Round(s.Crew)} men and {s.Ship.Guns} guns."
            : $"You come up with {s.Describe()}{(s.Destination >= 0 ? $", bound for {Map.Ports[s.Destination].Name}" : "")}. She carries about {Round(s.Crew)} men and {s.Ship.Guns} guns.");
        if (s.Nation != Nation.Outlaw)
        {
            string stand = w.AtWar(w.Homeland, s.Nation) ? $"{Nations.Get(w.Homeland).Name} is at war with {info.Name}." : $"{Nations.Get(w.Homeland).Name} is at peace with {info.Name}.";
            p.Say(stand + (w.Anger[(int)s.Nation] >= 30 ? $" {info.Name} has a price on your head." : ""));
        }
        else if (s.Role == Role.Rival) p.Say($"There's a bounty of {w.Rivals[s.RivalIndex].Bounty} gold on {s.Captain}.");
        p.Add(hostile ? "Fight her" : "Attack her", () => { Pop(); Attack(s); });
        if (hostile)
            p.Add("Try to outrun her", () =>
            {
                Pop();
                double mine = w.FleetSpeed * Sailing.PointOfSail(w.Heading, w.WindAngle);
                double theirs = s.Ship.Type.Speed * Sailing.PointOfSail(s.Heading, w.WindAngle);
                if (Rng.Next(100) < 35 + (int)((mine - theirs) * 40))
                {
                    Say("You show her your stern, and leave her behind.");
                    s.Route.Clear();
                    s.Next = 0;
                }
                else
                {
                    Say("She's too quick for you - she's alongside!", 0xFFFF8060);
                    Attack(s);
                }
            });
        else
        {
            p.Add("Hail her for news", () => { Pop(); Say(Rumour(), 0xFFA0D0FF); });
            p.Add("Sail on", Pop);
        }
        Push(p);
    }

    private int Round(int n) => n < 10 ? n : (n + 5) / 10 * 10;

    public static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ───────────────────────────── buried treasure ─────────────────────────────

    /// <summary>How far off the map's X might be, with the pieces you have: the more pieces, the closer it's drawn.</summary>
    public static int Vagueness(int pieces) => pieces switch { <= 1 => 18, 2 => 10, 3 => 5, _ => 0 };

    /// <summary>Sets where the treasure on your map lies: on a beach, somewhere quiet.</summary>
    public void BuryTreasure()
    {
        var w = World;
        for (int tries = 0; tries < 1000; tries++)
        {
            int x = Rng.Range(4, SeaMap.Width - 5), y = Rng.Range(4, SeaMap.Height - 5);
            if (Map[x, y] != Terrain.Sand || Map.Ports.Any(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y) < 8)) continue;
            if (!new[] { (0, 1), (1, 0), (0, -1), (-1, 0) }.Any(d => Map.Water(x + d.Item1, y + d.Item2))) continue;
            w.TreasureX = x;
            w.TreasureY = y;
            return;
        }
    }

    /// <summary>Sends a landing party to dig on the nearest beach.</summary>
    public void Dig()
    {
        var w = World;
        if (w.MapPieces == 0 || w.TreasureX < 0)
        {
            Say("You have no map to dig by.");
            return;
        }
        PassTime(1);
        Play(Sfx.Dig);
        double d = Math.Sqrt(Math.Pow(w.TreasureX + 0.5 - w.X, 2) + Math.Pow(w.TreasureY + 0.5 - w.Y, 2));
        if (d < 2.2)
        {
            int gold = 2500 + Rng.Next(3000) + 600 * w.Difficulty;
            w.Gold += gold;
            w.TreasuresFound++;
            w.MapPieces = 0;
            w.TreasureX = w.TreasureY = -1;
            w.Morale = Math.Min(100, w.Morale + 15);
            w.Note($"Dug up a buried treasure: {gold} gold.");
            Play(Sfx.Coin);
            Tell("Treasure!", $"The spades strike wood. Under the sand is an iron-bound chest, and in it {gold} gold. The crew cheer themselves hoarse.");
            return;
        }
        Say(d < 8 ? "Nothing here - but the landmarks look familiar. It's close." : "The landing party digs all day, and finds nothing.");
    }
}

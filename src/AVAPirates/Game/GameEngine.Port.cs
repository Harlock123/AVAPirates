using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

public sealed partial class GameEngine
{
    public const int MapPiecePrice = 450;

    /// <summary>The port the fleet is in.</summary>
    public Port? InPort { get; private set; }
    /// <summary>In by stealth: only the tavern and the merchant will deal with you.</summary>
    public bool Sneaked { get; private set; }

    public Nation NationOf(Port p) => World.PortNation[p.Id];

    /// <summary>The fleet comes to a port: welcome, or a hostile reception.</summary>
    public void ArriveAt(Port port)
    {
        Speed = 0;
        var n = NationOf(port);
        if (World.Welcome(n))
        {
            EnterPort(port, welcomed: true);
            return;
        }
        var p = new Panel { Title = port.Name, Kind = PanelKind.Port, Scene = "harbour" };
        p.Say($"{port.Name} is a {Nations.Get(n).Adjective} {SizeName(port)}, and {Nations.Get(n).Name} has a price on your head. The fort's guns are trained on the harbour mouth.");
        p.Add("Slip in by night", () =>
        {
            Pop();
            if (Rng.Next(100) < 70 - World.Anger[(int)n] / 4 - 5 * World.Difficulty)
            {
                Say("You slip in under cover of dark, unrecognised.", 0xFFA0D0FF);
                EnterPort(port, welcomed: false);
            }
            else
            {
                int lost = Math.Max(1, World.Crew / 10);
                World.Crew -= lost;
                Play(Sfx.Cannon);
                Tell("Spotted!", $"A sentry raises the alarm, and the fort opens fire. You get away, but {lost} men are lost.");
                World.X -= Sailing.Vector(World.Heading).X;
                World.Y -= Sailing.Vector(World.Heading).Y;
            }
        });
        p.Add($"Attack the town ({World.PortSoldiers[port.Id]} soldiers)", () => { Pop(); AttackTown(port); }, World.Crew >= 20, World.Crew >= 20 ? null : "You need twenty men at least.");
        p.Add("Sail on", () => { Pop(); BackOff(); });
        Push(p);
    }

    /// <summary>Turns the fleet away from the shore it just touched.</summary>
    private void BackOff()
    {
        World.Heading = Sailing.Norm(World.Heading + 180);
        var (vx, vy) = Sailing.Vector(World.Heading);
        for (int i = 0; i < 4 && Map.Water(World.X + vx, World.Y + vy); i++) { World.X += vx * 0.5; World.Y += vy * 0.5; }
    }

    public void EnterPort(Port port, bool welcomed)
    {
        Mode = Mode.Port;
        InPort = port;
        Sneaked = !welcomed;
        Speed = 0;
        Held.Clear();
        World.X = port.DockX + 0.5;
        World.Y = port.DockY + 0.5;
        World.Flags.RemoveWhere(f => f.StartsWith("visit:"));
        Play(Sfx.MusicStop);
        Play(Sfx.Bell);
        Play(Sfx.PortMusic);
        PortMenu();
    }

    public static string SizeName(Port p) => p.Size switch { 1 => "settlement", 2 => "town", _ => "city" };

    public void PortMenu()
    {
        CloseAll();
        var port = InPort!;
        Show(() =>
        {
            var w = World;
            var n = NationOf(port);
            var p = new Panel { Title = port.Name, Kind = PanelKind.Port, Scene = "harbour" };
            p.Say(n == Nation.Outlaw
                ? $"{port.Name}: a haven of the Brotherhood, where no crown's law runs and nobody asks where your gold came from."
                : $"{port.Name}, a {Nations.Get(n).Adjective} {SizeName(port)} of about {w.PortSoldiers[port.Id] * 12 + port.Size * 300} souls." + (Sneaked ? " You keep your hat pulled low." : ""));
            bool official = !Sneaked && n != Nation.Outlaw;
            p.Add("Visit the governor", Governor, official, official ? null : n == Nation.Outlaw ? "Havens have no governor." : "Not while they'd hang you.");
            p.Add("Go to the tavern", Tavern);
            p.Add("Trade with the merchant", Merchant);
            p.Add("See the shipwright", Shipwright, !Sneaked, Sneaked ? "He'd know you." : null);
            p.Add("Divide the plunder", DividePlunder, !Sneaked && w.Gold > 0, w.Gold > 0 ? $"{w.Gold} gold to share out among the captain and {w.Crew} men." : "Nothing to share yet.");
            p.Add("Check the ship and crew", StatusPanel);
            p.Add("Set sail", LeavePort);
            p.Add("Menu (save, load, quit)", SeaMenu);
            p.Selected = Math.Min(p.Selected, p.Options.Count - 1);
            return p;
        });
    }

    // ───────────────────────────── the governor ─────────────────────────────

    private void Governor()
    {
        var w = World;
        var port = InPort!;
        var n = NationOf(port);
        int ni = (int)n;
        var lines = new List<string>();
        int rank = Nations.RankFor(w.Favour[ni]);
        if (rank > w.Rank[ni])
        {
            var (title, _, land) = Nations.Ranks[rank];
            int gained = 0;
            for (int r = w.Rank[ni] + 1; r <= rank; r++) gained += Nations.Ranks[r].Land;
            w.Rank[ni] = rank;
            w.Land += gained;
            Play(Sfx.Fanfare);
            w.Note($"Made {title} by {Nations.Get(n).Name}.");
            lines.Add($"\"For your service to {Nations.Get(n).Name}, I name you {title}" + (gained > 0 ? $", with {gained} acres of land.\"" : ".\""));
        }
        else if (w.Rank[ni] > 0) lines.Add($"\"Welcome, {Nations.Ranks[w.Rank[ni]].Title} {w.Captain}.\"");
        else lines.Add($"\"Captain {w.Captain}. I know your name - just.\"");
        var enemies = Nations.Crowns.Where(o => w.AtWar(n, o)).Select(o => Nations.Get(o).Name).ToList();
        lines.Add(enemies.Count > 0
            ? $"\"{Nations.Get(n).Name} is at war with {string.Join(" and ", enemies)}. Bring me their ships, and pirates too, and you'll rise in our favour.\""
            : $"\"{Nations.Get(n).Name} is at peace with every crown. Pirates are fair game, though - the Brotherhood is everyone's enemy.\"");
        int next = Nations.Ranks.Skip(w.Rank[ni] + 1).FirstOrDefault().Favour;
        if (next > 0) lines.Add($"(Your favour with {Nations.Get(n).Name}: {w.Favour[ni]}. The next title needs {next}.)");
        var p = new Panel { Title = $"The governor of {port.Name}", Kind = PanelKind.Port, Scene = "governor", Back = Pop };
        foreach (var l in lines) p.Say(l);
        int anger = w.Anger[ni];
        if (anger > 0)
        {
            int fine = anger * 20;
            p.Add($"Pay {fine} gold to have your offences forgotten", () =>
            {
                if (w.Gold < fine) { Say("You haven't that much in the purse."); return; }
                w.Gold -= fine;
                w.Anger[ni] = 0;
                Play(Sfx.Coin);
                Say($"{Nations.Get(n).Name} forgives you, for now.", 0xFFA0D0FF);
                Pop();
            });
        }
        p.Add("Take your leave", Pop);
        Push(p);
    }

    // ───────────────────────────── the tavern ─────────────────────────────

    /// <summary>Men the tavern has to offer: more in big ports, and for a captain with a name and a cheerful crew.</summary>
    public int Recruits(Port port)
    {
        var w = World;
        int fame = w.ShipsTaken + w.TownsSacked * 3 + w.Rivals.Count(r => r.Beaten) * 3;
        int men = port.Size * 12 + 8 + Math.Min(40, fame * 2) + (w.Morale - 50) / 4;
        if (NationOf(port) == Nation.Outlaw) men += 15;
        if (w.Flags.Contains("visit:recruited")) men = 0;
        return Math.Max(0, Math.Min(men, w.CrewSpace - w.Crew));
    }

    private void Tavern()
    {
        var port = InPort!;
        var w = World;
        if (!w.Flags.Contains("visit:rumour")) w.Flags.Add("visit:rumour:" + Rumour());
        bool mapOffered = w.MapPieces < 4 && (port.Id + (int)(w.Day / 30)) % 3 != 0;
        Show(() =>
        {
            var p = new Panel { Title = $"The tavern at {port.Name}", Kind = PanelKind.Port, Scene = "tavern", Back = Pop };
            string rumour = w.Flags.FirstOrDefault(f => f.StartsWith("visit:rumour:"))?["visit:rumour:".Length..] ?? "";
            p.Say("Smoke, noise and sailors between ships. Over the din you hear: " + rumour);
            int men = Recruits(port);
            p.Add(men > 0 ? $"Sign on {men} men" : "Nobody here wants to sign on", () =>
            {
                w.Crew += men;
                w.Flags.Add("visit:recruited");
                Play(Sfx.Coin);
                Say($"{men} men sign the articles.", 0xFFA0D0FF);
            }, men > 0, w.Crew >= w.CrewSpace ? "Your ships hold no more men." : null);
            if (mapOffered)
                p.Add($"Buy a piece of a treasure map ({MapPiecePrice} gold)", () =>
                {
                    if (w.Gold < MapPiecePrice) { Say("You haven't the gold."); return; }
                    w.Gold -= MapPiecePrice;
                    w.MapPieces++;
                    if (w.TreasureX < 0) BuryTreasure();
                    w.Flags.Add("visit:mapbought");
                    Play(Sfx.Coin);
                    w.Note($"Bought a piece of a treasure map ({w.MapPieces} of 4).");
                    Say($"An old sailor sells you a torn piece of a map. You have {w.MapPieces} of the four.", 0xFFFFD860);
                }, !w.Flags.Contains("visit:mapbought"), "A torn corner of an old map, with an X somewhere on it. The more pieces, the better you'll know where.");
            p.Add("Listen for more news", () => { w.Flags.RemoveWhere(f => f.StartsWith("visit:rumour")); w.Flags.Add("visit:rumour"); w.Flags.Add("visit:rumour:" + Rumour()); });
            p.Add("Leave", Pop);
            return p;
        });
    }

    /// <summary>Something overheard: the wars, the galleon, a famous pirate, prices, or where a treasure map points.</summary>
    public string Rumour()
    {
        var w = World;
        int r = Rng.Next(6);
        switch (r)
        {
            case 0:
            {
                var a = Nations.Crowns[Rng.Next(4)];
                var foes = Nations.Crowns.Where(o => w.AtWar(a, o)).ToList();
                return foes.Count > 0 ? $"\"{Nations.Get(a).Name} and {Nations.Get(foes[0]).Name} are at each other's throats.\"" : $"\"{Nations.Get(a).Name} is at peace with everyone, they say. It won't last.\"";
            }
            case 1:
                if (Traffic.Ships.FirstOrDefault(s => s.Role == Role.Treasure) is { } g)
                    return $"\"The treasure galleon's at sea, bound for {Map.Ports[g.Destination].Name}. Somebody's going to get rich.\"";
                return $"\"The Valdoran treasure galleon sails in {WorldState.MonthNames[3]} and {WorldState.MonthNames[9]}. Every sailor knows it.\"";
            case 2:
            {
                var rivals = w.Rivals.Where(x => !x.Beaten).ToList();
                if (rivals.Count == 0) return "\"They say every famous pirate in the isles has met their match. Can't think who'd have done that.\"";
                var rv = rivals[Rng.Next(rivals.Count)];
                if (Traffic.Ships.FirstOrDefault(s => s.RivalIndex == w.Rivals.IndexOf(rv)) is { } at)
                    return $"\"{rv.Name} was seen off {NearestPortName(at.X, at.Y)} not long ago. {rv.Bounty} gold on that head.\"";
                return $"\"{rv.Name} is lying low in one of the havens. There's {rv.Bounty} gold on that head.\"";
            }
            case 3:
            {
                var good = (Good)Rng.Next(4);
                var dear = Map.Ports.Where(p => p.Wants == good).OrderBy(_ => Rng.Next(100)).FirstOrDefault();
                return dear is null ? "\"Trade's slow.\"" : $"\"They'll pay well for {good.ToString().ToLowerInvariant()} in {dear.Name}.\"";
            }
            case 4 when w.MapPieces > 0 && w.TreasureX >= 0:
                return $"\"Treasure? Folk say the old buccaneers buried theirs on the beaches near {NearestPortName(w.TreasureX, w.TreasureY)}.\"";
            default:
            {
                var rich = Map.Ports.Where(p => w.PortNation[p.Id] != Nation.Outlaw).OrderByDescending(p => w.PortWealth[p.Id]).First();
                return $"\"{rich.Name}'s the richest town in the isles now. Fat, and lazy with it.\"";
            }
        }
    }

    public string NearestPortName(double x, double y) =>
        Map.Ports.OrderBy(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y)).First().Name;

    // ───────────────────────────── the merchant ─────────────────────────────

    /// <summary>What a port's merchant asks for a ton of something (and pays a little less).</summary>
    public int Price(Port port, Good g)
    {
        double f = port.Grows == g ? 0.6 : port.Wants == g ? 1.6 : 1.0;
        f *= 1 + (port.Size - 2) * 0.08;
        f *= 0.95 + ((port.Id * 7 + (int)g * 13 + (int)(World.Day / 30)) % 10) / 100.0;
        return Math.Max(5, (int)(Goods.Base[(int)g] * f));
    }

    public int SellPrice(Port port, Good g) => Price(port, g) * 85 / 100;

    public const int FoodPrice = 2;

    private void Merchant()
    {
        var port = InPort!;
        var w = World;
        Show(() =>
        {
            var p = new Panel { Title = $"The merchant of {port.Name}", Kind = PanelKind.Port, Scene = "merchant", Back = Pop };
            p.Say($"Hold: {w.CargoUsed} of {w.CargoSpace} tons. Purse: {w.Gold} gold. Food: {(int)w.Food} barrels ({FoodDays()} days).");
            foreach (var g in Enum.GetValues<Good>())
            {
                var good = g;
                int buy = Price(port, good), sell = SellPrice(port, good);
                string tag = port.Grows == good ? "  (grown here: cheap)" : port.Wants == good ? "  (wanted here: dear)" : "";
                p.AddAdjust($"{good,-8} buy {buy,3}  sell {sell,3}   you have {w.Cargo[(int)good],3}{tag}", d =>
                {
                    if (d > 0 && w.Gold >= buy && w.CargoUsed < w.CargoSpace) { w.Gold -= buy; w.Cargo[(int)good]++; Play(Sfx.Coin); }
                    else if (d < 0 && w.Cargo[(int)good] > 0) { w.Gold += sell; w.Cargo[(int)good]--; Play(Sfx.Coin); }
                }, () =>
                {
                    // Enter sells the lot.
                    int n = w.Cargo[(int)good];
                    if (n == 0) return;
                    w.Gold += n * sell;
                    w.Cargo[(int)good] = 0;
                    Play(Sfx.Coin);
                    Say($"Sold {n} tons of {good.ToString().ToLowerInvariant()} for {n * sell} gold.");
                }, "Right buys a ton, Left sells one, Enter sells all you have.");
            }
            int barrels = Math.Max(0, (int)(w.Crew / 40.0 * 60 - w.Food));
            p.Add(barrels > 0 ? $"Food for two months: {barrels} barrels for {barrels * FoodPrice} gold" : "Your larder is full", () =>
            {
                int cost = barrels * FoodPrice;
                if (w.Gold < cost) { Say("You haven't the gold."); return; }
                w.Gold -= cost;
                w.Food += barrels;
                Play(Sfx.Coin);
            }, barrels > 0);
            foreach (var s in w.Fleet.Skip(1).ToList())
            {
                var ship = s;
                p.Add($"Sell the {ship.Type.Name.ToLowerInvariant()} {ship.Name} ({ship.Worth} gold)", () =>
                {
                    w.Fleet.Remove(ship);
                    w.Gold += ship.Worth;
                    w.Crew = Math.Min(w.Crew, w.CrewSpace);
                    if (w.CargoUsed > w.CargoSpace) Jettison();
                    Play(Sfx.Coin);
                    Say($"Sold the {ship.Name} for {ship.Worth} gold.");
                });
            }
            p.Add("Leave", Pop);
            return p;
        });
    }

    public int FoodDays() => World.Crew == 0 ? 999 : (int)(World.Food / (World.Crew / 40.0));

    /// <summary>Throws over the side what the hold can't carry.</summary>
    private void Jettison()
    {
        var w = World;
        while (w.CargoUsed > w.CargoSpace)
        {
            int most = Array.IndexOf(w.Cargo, w.Cargo.Max());
            w.Cargo[most]--;
        }
    }

    // ───────────────────────────── the shipwright ─────────────────────────────

    private void Shipwright()
    {
        var w = World;
        Show(() =>
        {
            var p = new Panel { Title = $"The shipwright of {InPort!.Name}", Kind = PanelKind.Port, Scene = "shipwright", Back = Pop };
            p.Say("\"Let's see what you've done to her this time.\"");
            int cost = w.Fleet.Sum(s => s.RepairCost);
            p.Add(cost > 0 ? $"Repair the fleet ({cost} gold)" : "Nothing needs mending", () =>
            {
                if (w.Gold < cost) { Say("You haven't the gold."); return; }
                w.Gold -= cost;
                foreach (var s in w.Fleet)
                {
                    s.Hull = s.Type.Hull;
                    s.Sails = 100;
                    s.Guns = s.Type.Guns;
                }
                PassTime(Math.Min(10, cost / 150.0));
                Play(Sfx.Coin);
                Say("The fleet is as good as new.");
            }, cost > 0);
            for (int i = 1; i < w.Fleet.Count; i++)
            {
                int at = i;
                var s = w.Fleet[i];
                p.Add($"Make the {s.Type.Name.ToLowerInvariant()} {s.Name} your flagship", () =>
                {
                    (w.Fleet[0], w.Fleet[at]) = (w.Fleet[at], w.Fleet[0]);
                    Say($"You move your flag to the {w.Flagship.Name}.");
                }, note: $"{s.Type.About} {s.Guns} guns, hull {s.Hull}/{s.Type.Hull}.");
            }
            p.Add("Leave", Pop);
            return p;
        });
    }
}

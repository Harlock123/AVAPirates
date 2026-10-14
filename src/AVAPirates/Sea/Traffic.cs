using AVAPirates.Rules;
using AVAPirates.World;

namespace AVAPirates.Sea;

public enum Role { Merchant, Warship, Pirate, Hunter, Treasure, Rival }

/// <summary>A ship going about her business on the open sea, seen from your own deck.</summary>
public sealed class SeaShip
{
    public int Id { get; init; }
    public Nation Nation { get; init; }
    public Role Role { get; init; }
    public required Ship Ship { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Heading { get; set; }
    public List<(int X, int Y)> Route { get; set; } = [];
    public int Next { get; set; }
    public int Destination { get; init; } = -1;
    public int Crew { get; set; }
    public int Gold { get; set; }
    public int[] Cargo { get; init; } = new int[4];
    /// <summary>The captain's name, for named pirates and hunters.</summary>
    public string Captain { get; init; } = "";
    public int RivalIndex { get; init; } = -1;
    public double RouteAge { get; set; }

    public string Describe() => Role switch
    {
        Role.Rival => $"{Captain}'s {Ship.Type.Name.ToLowerInvariant()}, the {Ship.Name}",
        Role.Pirate => $"a pirate {Ship.Type.Name.ToLowerInvariant()}",
        Role.Hunter => $"a {Nations.Get(Nation).Adjective} pirate hunter",
        Role.Treasure => $"the {Nations.Get(Nation).Adjective} treasure galleon",
        Role.Warship => $"a {Nations.Get(Nation).Adjective} {Ship.Type.Name.ToLowerInvariant()} of war",
        _ => $"a {Nations.Get(Nation).Adjective} {Ship.Type.Name.ToLowerInvariant()}",
    };
}

/// <summary>
/// The other ships of the isles: merchants carrying goods between friendly ports, warships on
/// patrol, pirates out of the Brotherhood's havens, the treasure galleon twice a year, famous
/// rivals, and - once you have made enemies - hunters sent after you.
/// </summary>
public sealed class Traffic(SeaMap map, Rng rng)
{
    public const int Target = 14;
    public List<SeaShip> Ships { get; } = [];
    private int _nextId = 1;

    private static readonly string[] ShipNames =
    [
        "Gannet", "Merry Lark", "Saint Orla", "Fair Wind", "Dauntless", "Silver Cod", "Wanderer", "Brave Jenny", "Sea Hound",
        "Nightjar", "Constance", "Golden Hind", "Marigold", "Osprey", "Resolute", "Corsair", "Grey Widow", "Kestrel", "Mermaid",
        "Blue Heron", "Endurance", "Swift", "Valiant", "Amity", "Black Swan", "Sparrowhawk", "Dove", "Tempest", "Lantern",
    ];

    public static readonly (string Name, ShipKind Ship, int Bounty)[] RivalNames =
    [
        ("Black Morwenna", ShipKind.Brigantine, 1500),
        ("Red Hask", ShipKind.Barque, 1200),
        ("Silas Teague", ShipKind.Sloop, 1000),
        ("Anouk the Grey", ShipKind.Frigate, 2500),
        ("Jory Flint", ShipKind.Brigantine, 1800),
        ("Captain Grisholt", ShipKind.Frigate, 3000),
    ];

    public string NameShip() => ShipNames[rng.Next(ShipNames.Length)];

    /// <summary>Keeps the sea busy: tops up the traffic, now and then, with whatever is about.</summary>
    public void Populate(WorldState w, bool all = false)
    {
        int guard = 0;
        while (Ships.Count < Target && guard++ < 40)
        {
            if (Spawn(w) is not { } s) continue;
            Ships.Add(s);
            if (!all) break;
            // Spread a fresh sea's ships along their routes.
            s.Next = rng.Next(Math.Max(1, s.Route.Count));
            if (s.Route.Count > 0) (s.X, s.Y) = (s.Route[s.Next].X + 0.5, s.Route[s.Next].Y + 0.5);
        }
    }

    private SeaShip? Spawn(WorldState w)
    {
        var ports = map.Ports;
        int roll = rng.Next(100);
        // Hunters come for you once a crown is angry enough.
        var angry = Nations.Crowns.Where(n => w.Anger[(int)n] >= 40).ToList();
        if (angry.Count > 0 && roll < 12 && !Ships.Any(s => s.Role == Role.Hunter))
        {
            var n = angry[rng.Next(angry.Count)];
            var from = ports.Where(p => w.PortNation[p.Id] == n).OrderBy(_ => rng.Next(100)).FirstOrDefault();
            if (from is null) return null;
            return Make(w, n, Role.Hunter, rng.Chance(50) ? ShipKind.Frigate : ShipKind.Brigantine, from, null, "Captain " + NameShip());
        }
        if (roll < 22)
        {
            // A pirate, out of a haven, or one of the famous rivals.
            var haven = ports.Where(p => w.PortNation[p.Id] == Nation.Outlaw).OrderBy(_ => rng.Next(100)).FirstOrDefault() ?? ports[rng.Next(ports.Count)];
            var target = ports[rng.Next(ports.Count)];
            int ri = w.Rivals.FindIndex(r => !r.Beaten && !Ships.Any(s => s.RivalIndex == w.Rivals.IndexOf(r)));
            if (ri >= 0 && rng.Chance(35))
            {
                var r = w.Rivals[ri];
                var rs = Make(w, Nation.Outlaw, Role.Rival, r.Ship, haven, target, r.Name, ri);
                return rs;
            }
            return Make(w, Nation.Outlaw, Role.Pirate, rng.Chance(60) ? ShipKind.Sloop : ShipKind.Barque, haven, target);
        }
        var crown = Nations.Crowns[rng.Next(4)];
        if (rng.Chance(45)) crown = Nation.Valdoro;
        var own = ports.Where(p => w.PortNation[p.Id] == crown).ToList();
        if (own.Count < 2) return null;
        var a = own[rng.Next(own.Count)];
        if (roll < 42)
        {
            var b = own.Where(p => p != a).OrderBy(_ => rng.Next(100)).First();
            return Make(w, crown, Role.Warship, rng.Chance(60) ? ShipKind.Frigate : ShipKind.Brigantine, a, b);
        }
        // A merchant, bound for a friendly port.
        var dest = ports.Where(p => p != a && w.PortNation[p.Id] != Nation.Outlaw && !w.AtWar(crown, w.PortNation[p.Id])).OrderBy(_ => rng.Next(100)).FirstOrDefault();
        if (dest is null) return null;
        var kind = rng.Next(10) switch { < 5 => ShipKind.Merchantman, < 8 => ShipKind.Barque, _ => ShipKind.Galleon };
        return Make(w, crown, Role.Merchant, kind, a, dest);
    }

    /// <summary>Sends the treasure galleon out from one Valdoran port to the greatest of them.</summary>
    public SeaShip? SendTreasure(WorldState w)
    {
        var own = map.Ports.Where(p => w.PortNation[p.Id] == Nation.Valdoro).ToList();
        if (own.Count < 2) return null;
        var home = own.OrderByDescending(p => p.Size).ThenBy(p => p.Id).First();
        var from = own.Where(p => p != home).OrderByDescending(p => Math.Abs(p.X - home.X) + Math.Abs(p.Y - home.Y)).First();
        var s = Make(w, Nation.Valdoro, Role.Treasure, ShipKind.Galleon, from, home);
        if (s is not null) Ships.Add(s);
        return s;
    }

    private SeaShip? Make(WorldState w, Nation n, Role role, ShipKind kind, Port from, Port? to, string captain = "", int rival = -1)
    {
        var start = (from.DockX, from.DockY);
        var route = to is null ? [start] : map.Route(start, (to.DockX, to.DockY));
        if (route.Count == 0) return null;
        var t = ShipType.Get(kind);
        var s = new SeaShip
        {
            Id = _nextId++,
            Nation = n,
            Role = role,
            Ship = Ship.Make(kind, rival >= 0 ? RivalShipName(rival) : NameShip()),
            X = start.Item1 + 0.5,
            Y = start.Item2 + 0.5,
            Route = route,
            Destination = to?.Id ?? -1,
            Crew = role switch { Role.Merchant => t.Crew / 3, Role.Treasure => t.Crew * 3 / 4, _ => t.Crew * 4 / 5 } + rng.Next(t.Crew / 5 + 1),
            Captain = captain,
            RivalIndex = rival,
        };
        s.Gold = role switch
        {
            Role.Merchant => 100 + rng.Next(400),
            Role.Treasure => 4000 + rng.Next(4000),
            Role.Pirate => 150 + rng.Next(600),
            Role.Rival => w.Rivals[rival].Bounty / 2 + rng.Next(800),
            _ => 50 + rng.Next(200),
        };
        if (role is Role.Merchant or Role.Treasure)
            for (int i = 0; i < 3; i++) s.Cargo[rng.Next(4)] += rng.Next(t.Cargo / 4);
        if (route.Count > 1) s.Heading = Sailing.Bearing(route[0].X, route[0].Y, route[1].X, route[1].Y);
        return s;
    }

    private static string RivalShipName(int i) => new[] { "Raven's Due", "Scarlet Gull", "Lucky Knave", "Grey Lady", "Flint's Folly", "Iron Maw" }[i % 6];

    /// <summary>Whether a ship means harm to you on sight.</summary>
    public static bool Hostile(SeaShip s, WorldState w) => s.Role switch
    {
        Role.Pirate or Role.Rival or Role.Hunter => true,
        Role.Warship => w.Anger[(int)s.Nation] >= 30,
        _ => false,
    };

    /// <summary>Moves everyone a step: along their routes, or after you if they mean you harm.</summary>
    public void Update(double dt, WorldState w)
    {
        foreach (var s in Ships.ToList())
        {
            s.RouteAge += dt;
            double tx, ty;
            bool chasing = Hostile(s, w) && Dist(s, w.X, w.Y) < 9;
            if (chasing)
            {
                // After you: straight if the way is clear, by a fresh route if not.
                if (s.RouteAge > 3 || s.Route.Count == 0)
                {
                    s.RouteAge = 0;
                    var r = map.Route(((int)s.X, (int)s.Y), ((int)w.X, (int)w.Y));
                    if (r.Count > 0) { s.Route = r; s.Next = 0; }
                }
            }
            if (s.Next >= s.Route.Count)
            {
                if (chasing) { tx = w.X; ty = w.Y; }
                else
                {
                    // Arrived (or nowhere to go): she's gone into port, out of the story.
                    Ships.Remove(s);
                    continue;
                }
            }
            else
            {
                (tx, ty) = (s.Route[s.Next].X + 0.5, s.Route[s.Next].Y + 0.5);
                if (Math.Abs(tx - s.X) + Math.Abs(ty - s.Y) < 0.6) { s.Next++; continue; }
            }
            double want = Sailing.Bearing(s.X, s.Y, tx, ty);
            double turn = Math.Clamp(Sailing.Turn(s.Heading, want), -s.Ship.Type.Agility * dt, s.Ship.Type.Agility * dt);
            s.Heading = Sailing.Norm(s.Heading + turn);
            double speed = s.Ship.Type.Speed * (chasing ? 0.8 : 0.55) * Math.Max(0.45, Sailing.PointOfSail(s.Heading, w.WindAngle)) * w.WindStrength;
            var (vx, vy) = Sailing.Vector(s.Heading);
            double nx = s.X + vx * speed * dt, ny = s.Y + vy * speed * dt;
            if (map.Water(nx, ny)) (s.X, s.Y) = (nx, ny);
            else s.Next = Math.Min(s.Route.Count, s.Next + 1);
        }
    }

    public static double Dist(SeaShip s, double x, double y) => Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y));
}

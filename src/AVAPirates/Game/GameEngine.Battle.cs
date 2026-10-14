using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

public sealed partial class GameEngine
{
    public SeaBattle? Battle { get; private set; }
    public SeaShip? Foe { get; private set; }
    public Duel? Duel { get; private set; }
    /// <summary>The town being stormed, when the duel is with its commandant.</summary>
    public Port? Storming { get; private set; }
    private double _ending;

    /// <summary>How good the other side is with guns and blades: better on harder lives, and on warships.</summary>
    public double FoeSkill(Role role) => Math.Clamp(0.4 + 0.18 * World.Difficulty + role switch
    {
        Role.Merchant or Role.Treasure => -0.1,
        Role.Warship or Role.Hunter => 0.08,
        Role.Rival => 0.15,
        _ => 0,
    }, 0.25, 1);

    /// <summary>The player's lunge: a little slower every year past thirty-five.</summary>
    public double PlayerWindup => 0.2 + Math.Max(0, World.Age - 35) * 0.005;

    /// <summary>Goes into battle with a ship met at sea.</summary>
    public void Attack(SeaShip s)
    {
        CloseAll();
        Foe = s;
        Battle = new SeaBattle(World.Flagship, World.Crew, s.Ship, s.Crew, s.Role, World.WindAngle, World.WindStrength, Rng, FoeSkill(s.Role));
        Battle.Sound += BattleSound;
        Mode = Mode.Battle;
        _ending = 0;
        Held.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.BattleMusic);
    }

    private void BattleSound(string s) => Play(s switch
    {
        "cannon" => Sfx.Cannon,
        "splash" => Sfx.Splash,
        "hit" => Sfx.Crack,
        "sink" => Sfx.Sink,
        "struck" => Sfx.Struck,
        "board" => Sfx.Board,
        "clang" => Sfx.Clang,
        "swish" => Sfx.Swish,
        "overboard" => Sfx.Overboard,
        "yield" => Sfx.Yield,
        _ => Sfx.Stab,
    });

    /// <summary>What the computer does at the helm, for the demo.</summary>
    public Helm AutoHelm()
    {
        var b = Battle!;
        var me = b.Player;
        var them = b.Enemy;
        double d = b.Distance;
        double toThem = Sailing.Bearing(me.X, me.Y, them.X, them.Y);
        double want = d > 260 ? toThem : Sailing.Norm(toThem + (Sailing.Turn(me.Heading, toThem) > 0 ? -90 : 90));
        if (me.Crew > them.Crew * 1.5) want = toThem;
        double turn = Sailing.Turn(me.Heading, want);
        return new Helm(turn < -5, turn > 5, true, false, SeaBattle.Bears(me, them) && d < SeaBattle.BallSpeed * SeaBattle.BallLife * 0.85);
    }

    private void BattleTick(double dt)
    {
        var b = Battle!;
        var helm = InDemo ? AutoHelm() : new Helm(Held.Contains("Left"), Held.Contains("Right"), Pressed("Up"), Pressed("Down"), Pressed("Space", "Enter"));
        b.Tick(dt, helm);
        World.Crew = b.Player.Crew;
        if (b.End == BattleEnd.None) return;
        _ending += dt;
        if (_ending > 1.2) FinishBattle();
    }

    /// <summary>Ends the battle as it stands (tests and the demo use this after fighting it out).</summary>
    public void FinishBattle()
    {
        var b = Battle!;
        var s = Foe!;
        Battle = null;
        Mode = Mode.Sea;
        Held.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.SeaMusic);
        World.Crew = b.Player.Crew;
        s.Crew = b.Enemy.Crew;
        _passed.Add(s.Id);
        switch (b.End)
        {
            case BattleEnd.Boarding:
                StartDuel(s.Role == Role.Rival ? s.Captain : $"the captain of the {s.Ship.Name}", World.Crew, s.Crew, FoeSkill(s.Role), boarding: s);
                break;
            case BattleEnd.Struck:
                Capture(s);
                break;
            case BattleEnd.Sunk:
                Traffic.Ships.Remove(s);
                World.ShipsSunk++;
                Deeds(s, captured: false);
                Play(Sfx.Victory);
                World.Note($"Sank {s.Describe()}.");
                Tell("She's gone", $"The {s.Ship.Name} goes down with everything aboard her. Whatever she carried is at the bottom of the sea now.");
                break;
            case BattleEnd.Escaped:
                Say("You lose sight of each other in the swell.");
                break;
            case BattleEnd.Lost:
                FlagshipLost();
                break;
        }
    }

    /// <summary>A prize: her gold, her cargo, any of her crew who'll sign on, and her hull, if you want it.</summary>
    public void Capture(SeaShip s)
    {
        var w = World;
        Traffic.Ships.Remove(s);
        w.ShipsTaken++;
        int gold = s.Gold;
        w.Gold += gold;
        int took = 0;
        for (int g = 0; g < 4; g++)
        {
            int room = w.CargoSpace - w.CargoUsed;
            int n = Math.Min(room, s.Cargo[g]);
            w.Cargo[g] += n;
            took += n;
        }
        int joiners = Math.Min(w.CrewSpace - w.Crew, s.Crew * (s.Role is Role.Pirate or Role.Rival ? 50 : s.Role is Role.Merchant ? 30 : 20) / 100);
        if (joiners > 0) w.Crew += joiners;
        w.Morale = Math.Min(100, w.Morale + 5 + gold / 400);
        Deeds(s, captured: true);
        var lines = new List<string> { $"The {s.Ship.Name} is yours! In her strongroom: {gold} gold." };
        if (took > 0) lines.Add($"You take {took} tons of cargo into your hold.");
        if (joiners > 0) lines.Add($"{joiners} of her crew sign on with you.");
        if (s.Role == Role.Rival)
        {
            var r = w.Rivals[s.RivalIndex];
            r.Beaten = true;
            w.Gold += r.Bounty;
            lines.Add($"{r.Name} is taken, and the bounty of {r.Bounty} gold is yours.");
            w.Note($"Defeated the pirate {r.Name}.");
        }
        if (s.Role == Role.Treasure) w.Note($"Took the Valdoran treasure galleon, with {gold} gold aboard.");
        else w.Note($"Took {s.Describe()}: {gold} gold.");
        Play(Sfx.Victory);
        var p = new Panel { Title = "A prize!", Kind = PanelKind.Encounter };
        foreach (var l in lines) p.Say(l);
        bool room2 = w.Fleet.Count < 8 && s.Ship.Hull > 0;
        p.Add($"Keep her: add the {s.Ship.Type.Name.ToLowerInvariant()} to your fleet", () =>
        {
            Pop();
            s.Ship.Hull = Math.Max(s.Ship.Hull, s.Ship.Type.Hull / 4);
            w.Fleet.Add(s.Ship);
        }, room2, room2 ? $"{s.Ship.Type.About} Your fleet sails only as fast as its slowest ship." : "Your fleet is as big as it can be.");
        p.Add("Strip her and set her adrift", Pop);
        Push(p);
    }

    /// <summary>What the crowns think of what you did to a ship: her own nation is angered, its enemies grateful.</summary>
    private void Deeds(SeaShip s, bool captured)
    {
        var w = World;
        int weight = s.Role is Role.Warship or Role.Treasure or Role.Hunter or Role.Rival ? 2 : 1;
        if (s.Nation == Nation.Outlaw)
        {
            foreach (var c in Nations.Crowns) w.Favour[(int)c] += weight;
            return;
        }
        w.Anger[(int)s.Nation] += s.Role switch { Role.Treasure => 30, Role.Warship or Role.Hunter => 14, _ => 10 };
        foreach (var c in Nations.Crowns.Where(c => w.AtWar(c, s.Nation)))
            w.Favour[(int)c] += (captured ? 2 : 1) * weight;
    }

    // ───────────────────────────── sword fights ─────────────────────────────

    public void StartDuel(string name, int myCrew, int theirCrew, double skill, SeaShip? boarding = null, Port? town = null)
    {
        CloseAll();
        Foe = boarding;
        Storming = town;
        var (mine, theirs) = Duel.Hearts(myCrew, theirCrew);
        Duel = new Duel(name, mine, theirs, skill, Rng, PlayerWindup);
        Duel.Sound += BattleSound;
        Mode = Mode.Duel;
        _ending = 0;
        Held.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.DuelMusic);
    }

    private Height _autoGuard = Height.Mid;
    private double _autoTimer;
    private bool _autoSaw;

    /// <summary>
    /// What the computer does with the sword, for the demo (and for measuring the balance): about as
    /// well as a fair human swordsman - it guards the right height against a lunge a little over half
    /// the time, and lunges at the gaps in the other's guard more often than not.
    /// </summary>
    public Guard AutoGuard()
    {
        var d = Duel!;
        var e = d.Enemy;
        var me = d.Player;
        if (e.Stance == Stance.Lunging && !e.Struck)
        {
            if (!_autoSaw)
            {
                _autoSaw = true;
                _autoGuard = Rng.Chance(55) ? e.Aim : (Height)Rng.Next(3);
            }
        }
        else _autoSaw = false;
        _autoTimer -= Step;
        bool lunge = false;
        if (_autoTimer <= 0 && !me.Busy && e.X - me.X <= Duel.Reach)
        {
            _autoTimer = 0.45 + Rng.Next(40) / 100.0;
            var others = Enum.GetValues<Height>().Where(h => h != e.Guard).ToList();
            _autoGuard = Rng.Chance(65) ? others[Rng.Next(2)] : (Height)Rng.Next(3);
            lunge = true;
        }
        return new Guard(_autoGuard == Height.High, _autoGuard == Height.Low, false, e.X - me.X > Duel.Reach - 2, lunge);
    }

    private void DuelTick(double dt)
    {
        var d = Duel!;
        var input = InDemo ? AutoGuard() : new Guard(Held.Contains("Up"), Held.Contains("Down"), Held.Contains("Left"), Held.Contains("Right"), Pressed("Space", "Enter"));
        d.Tick(dt, input);
        if (d.End == DuelEnd.None) return;
        _ending += dt;
        if (_ending > 1.4) FinishDuel();
    }

    public void FinishDuel()
    {
        var d = Duel!;
        var w = World;
        Duel = null;
        Mode = Mode.Sea;
        Held.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.SeaMusic);
        bool won = d.End == DuelEnd.Won;
        if (won) w.DuelsWon++;
        if (Storming is { } port)
        {
            Storming = null;
            if (won) Sack(port);
            else
            {
                int lost = w.Crew * 35 / 100;
                w.Crew -= lost;
                w.Anger[(int)NationOf(port)] += 10;
                w.Morale = Math.Max(0, w.Morale - 10);
                Play(Sfx.Defeat);
                Tell("Driven off", $"The garrison throws you back to the boats. {lost} men are left on the beach. You row for the ships.", BackOff);
            }
            return;
        }
        var s = Foe!;
        if (won)
        {
            Capture(s);
            return;
        }
        // Beaten back: they take what they can and cut the grapples.
        int men = w.Crew * 30 / 100, gold = s.Role is Role.Pirate or Role.Rival or Role.Hunter ? w.Gold / 3 : 0;
        w.Crew -= men;
        w.Gold -= gold;
        w.Morale = Math.Max(0, w.Morale - 10);
        Play(Sfx.Defeat);
        Tell("Beaten back", $"Your boarders are thrown back aboard your own ship. {men} men are lost" + (gold > 0 ? $", and {s.Captain switch { "" => "they", var c => c }} carry off {gold} gold of the plunder" : "") + ". The grapples are cut, and she sails off.");
    }

    // ───────────────────────────── storming a town ─────────────────────────────

    public void AttackTown(Port port)
    {
        var w = World;
        int soldiers = w.PortSoldiers[port.Id];
        StartDuel($"the commandant of {port.Name}", w.Crew, soldiers, Math.Clamp(FoeSkill(Role.Warship) + 0.05, 0.25, 1), town: port);
    }

    private void Sack(Port port)
    {
        var w = World;
        var n = NationOf(port);
        int loot = Math.Max(200, w.PortWealth[port.Id] * 45 / 100);
        w.Gold += loot;
        w.PortWealth[port.Id] -= loot;
        w.PortSoldiers[port.Id] /= 2;
        w.TownsSacked++;
        w.Morale = Math.Min(100, w.Morale + 15);
        if (n != Nation.Outlaw)
        {
            w.Anger[(int)n] += 25;
            foreach (var c in Nations.Crowns.Where(c => w.AtWar(c, n))) w.Favour[(int)c] += 4;
        }
        w.Note($"Sacked {port.Name}: {loot} gold.");
        Play(Sfx.Victory);
        Tell($"{port.Name} is sacked", $"The commandant yields his sword, and the town is yours for a night. You carry off {loot} gold before the {Nations.Get(n).Adjective} garrison can rally. " +
            $"{Nations.Get(n).Name} won't forget this.", BackOff);
    }

    // ───────────────────────────── losing the flagship ─────────────────────────────

    private void FlagshipLost()
    {
        var w = World;
        var lostShip = w.Flagship;
        w.Fleet.RemoveAt(0);
        Play(Sfx.Defeat);
        if (w.Fleet.Count > 0)
        {
            w.Crew = Math.Min(w.CrewSpace, w.Crew * 60 / 100);
            if (w.CargoUsed > w.CargoSpace) Jettison();
            w.Note($"The {lostShip.Name} was sunk under you.");
            Tell("Abandon ship!", $"The {lostShip.Name} goes down. You and the men who could swim are pulled aboard the {w.Flagship.Name}, your new flagship.");
            return;
        }
        // Nothing left: washed up, and starting again in a friendly port.
        var home = Map.Ports.Where(p => w.Welcome(NationOf(p))).OrderBy(p => Math.Abs(p.X - w.X) + Math.Abs(p.Y - w.Y)).First();
        w.Fleet.Add(Ship.Make(ShipKind.Sloop, Traffic.NameShip()));
        w.Crew = 12;
        w.Gold = 0;
        w.Cargo = new int[4];
        w.Food = 20;
        w.Morale = 50;
        PassTime(60);
        w.Note($"The {lostShip.Name} was sunk; washed up near {home.Name}.");
        Tell("Shipwrecked", $"The {lostShip.Name} goes down, and the plunder with her. Two months later, a handful of survivors scrape together a sloop at {home.Name}. You'll have to begin again.",
            () => EnterPort(home, welcomed: true));
    }
}

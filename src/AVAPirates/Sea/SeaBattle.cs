using AVAPirates.Rules;

namespace AVAPirates.Sea;

public enum BattleEnd { None, Boarding, Struck, Sunk, Escaped, Lost }

/// <summary>A ship in a sea fight: where she is, how she's going, and the state of her guns.</summary>
public sealed class Fighter
{
    public required Ship Ship { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Heading { get; set; }
    public double Speed { get; set; }
    public bool Reefed { get; set; }
    public int Crew { get; set; }
    public int StartCrew { get; init; }
    /// <summary>Seconds until each broadside is loaded again: port (left), starboard (right).</summary>
    public double ReloadPort { get; set; }
    public double ReloadStarboard { get; set; }
    public double Length => Ship.Type.Length * 4;
}

/// <summary>A cannonball in flight.</summary>
public sealed class Ball
{
    public double X { get; set; }
    public double Y { get; set; }
    public double VX { get; init; }
    public double VY { get; init; }
    public double Life { get; set; }
    public bool FromPlayer { get; init; }
    public double Power { get; init; }
}

/// <summary>Smoke from a gun, a splash, or splinters from a hit, fading as it ages.</summary>
public sealed class Puff(double x, double y, string kind)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public string Kind { get; } = kind;
    public double Age { get; set; }
}

/// <summary>What the player is doing with the helm and the guns this moment.</summary>
public record struct Helm(bool Left, bool Right, bool Full, bool Reef, bool Fire);

/// <summary>
/// A fight between your flagship and another ship, in real time, in the open sea under the same wind
/// as the map. Turn to bring a broadside to bear and fire; the guns reload slower with too few
/// hands. Run alongside to board her (and fight her captain), batter a merchant until she strikes
/// her colours, sink her, or sail clear away.
/// </summary>
public sealed class SeaBattle
{
    public const double BallSpeed = 400, BallLife = 1.2, EscapeDistance = 1050, Traverse = 35;

    private readonly Rng _rng;
    public Fighter Player { get; }
    public Fighter Enemy { get; }
    public Role EnemyRole { get; }
    public double WindAngle { get; }
    public double WindStrength { get; }
    public List<Ball> Balls { get; } = [];
    public List<Puff> Puffs { get; } = [];
    public BattleEnd End { get; private set; }
    public double Time { get; private set; }
    public List<string> Log { get; } = [];
    /// <summary>The enemy's skill at the guns and helm, 0.4 to 1.</summary>
    public double Skill { get; }
    public event Action<string>? Sound;

    public SeaBattle(Ship mine, int crew, Ship theirs, int theirCrew, Role role, double windAngle, double windStrength, Rng rng, double skill = 0.7)
    {
        _rng = rng;
        WindAngle = windAngle;
        WindStrength = Math.Max(0.6, windStrength);
        EnemyRole = role;
        Skill = skill;
        Player = new Fighter { Ship = mine, Crew = crew, StartCrew = crew, X = 0, Y = 300, Heading = Sailing.Norm(windAngle + 90) };
        Enemy = new Fighter { Ship = theirs, Crew = theirCrew, StartCrew = theirCrew, X = 0, Y = -300, Heading = Sailing.Norm(windAngle + (role is Role.Merchant or Role.Treasure ? 0 : -90)) };
        Player.Speed = TopSpeed(Player) * 0.6;
        Enemy.Speed = TopSpeed(Enemy) * 0.6;
    }

    private void Say(string text)
    {
        Log.Add(text);
        if (Log.Count > 30) Log.RemoveAt(0);
    }

    public double Distance => Math.Sqrt(Math.Pow(Player.X - Enemy.X, 2) + Math.Pow(Player.Y - Enemy.Y, 2));

    /// <summary>Units a second at full sail on a heading.</summary>
    public double TopSpeed(Fighter f) =>
        f.Ship.Type.Speed * 26 * Sailing.PointOfSail(f.Heading, WindAngle) * WindStrength * (0.35 + 0.65 * f.Ship.Sails / 100.0) * (f.Reefed ? 0.5 : 1);

    /// <summary>Seconds to load a broadside: slower with fewer than three hands to a gun.</summary>
    public static double ReloadTime(Fighter f) => 5.0 * Math.Clamp(f.Ship.Guns * 3.0 / Math.Max(1, f.Crew), 1, 3);

    /// <summary>Which side of a ship faces a point: +1 starboard (right), -1 port (left), and how squarely (0 dead ahead or astern, 1 abeam).</summary>
    public static (int Side, double Square) Facing(Fighter f, double x, double y)
    {
        double rel = Sailing.Turn(f.Heading, Sailing.Bearing(f.X, f.Y, x, y));
        return (rel >= 0 ? 1 : -1, Math.Sin(Math.Abs(rel) * Math.PI / 180));
    }

    /// <summary>Whether a broadside can be brought to bear on a target: within the guns' training of the beam.</summary>
    public static bool Bears(Fighter f, Fighter target)
    {
        double lead = Lead(f, target);
        double rel = Math.Abs(Sailing.Turn(f.Heading, lead));
        return Math.Abs(rel - 90) <= Traverse;
    }

    public void Tick(double dt, Helm helm)
    {
        if (End != BattleEnd.None) return;
        Time += dt;
        Steer(Player, (helm.Left ? -1 : 0) + (helm.Right ? 1 : 0), dt);
        if (helm.Full) Player.Reefed = false;
        if (helm.Reef) Player.Reefed = true;
        if (helm.Fire) Fire(Player, Enemy);
        Think(dt);
        Move(Player, dt);
        Move(Enemy, dt);
        Player.ReloadPort = Math.Max(0, Player.ReloadPort - dt);
        Player.ReloadStarboard = Math.Max(0, Player.ReloadStarboard - dt);
        Enemy.ReloadPort = Math.Max(0, Enemy.ReloadPort - dt);
        Enemy.ReloadStarboard = Math.Max(0, Enemy.ReloadStarboard - dt);
        FlyBalls(dt);
        foreach (var p in Puffs) p.Age += dt;
        Puffs.RemoveAll(p => p.Age > 1.6);
        CheckEnd();
    }

    private void Steer(Fighter f, int dir, double dt)
    {
        // A ship with way on her answers the helm; one dead in the water barely turns.
        double way = Math.Clamp(f.Speed / 20, 0.35, 1);
        f.Heading = Sailing.Norm(f.Heading + dir * f.Ship.Type.Agility * (f.Reefed ? 1.25 : 1) * way * dt);
    }

    private static void SteerToward(Fighter f, double want, double dt, double agilityScale = 1)
    {
        double way = Math.Clamp(f.Speed / 20, 0.35, 1);
        double max = f.Ship.Type.Agility * way * agilityScale * dt;
        f.Heading = Sailing.Norm(f.Heading + Math.Clamp(Sailing.Turn(f.Heading, want), -max, max));
    }

    private void Move(Fighter f, double dt)
    {
        double target = TopSpeed(f);
        f.Speed += (target - f.Speed) * Math.Min(1, dt * 0.6);
        var (vx, vy) = Sailing.Vector(f.Heading);
        f.X += vx * f.Speed * dt;
        f.Y += vy * f.Speed * dt;
    }

    /// <summary>Fires whichever broadside faces the target better, if it's loaded.</summary>
    public bool Fire(Fighter f, Fighter target)
    {
        var (side, _) = Facing(f, target.X, target.Y);
        if ((side > 0 ? f.ReloadStarboard : f.ReloadPort) > 0)
        {
            // The other broadside, if that one's ready.
            side = -side;
            if ((side > 0 ? f.ReloadStarboard : f.ReloadPort) > 0) return false;
        }
        if (f.Ship.Guns <= 1) return false;
        int perSide = Math.Max(1, f.Ship.Guns / 2);
        int balls = Math.Clamp(perSide / 2, 3, 8);
        double power = perSide / (double)balls;
        var (fx, fy) = Sailing.Vector(f.Heading);
        var (sx, sy) = Sailing.Vector(f.Heading + 90 * side);
        var (vx, vy) = Sailing.Vector(f.Heading);
        // The guns train a little either side of the beam, to lead a moving target.
        double beam = Sailing.Norm(f.Heading + 90 * side);
        double aim = Sailing.Turn(beam, Lead(f, target));
        aim = Math.Abs(aim) <= Traverse ? aim : 0;
        for (int i = 0; i < balls; i++)
        {
            double along = (i / (double)Math.Max(1, balls - 1) - 0.5) * f.Length * 0.7;
            double spread = (_rng.Next(100) - 50) / 50.0 * (f == Player ? 3 : 7 - 5 * Skill);
            var (dx, dy) = Sailing.Vector(beam + aim + spread);
            double bx = f.X + fx * along + sx * f.Length / 6, by = f.Y + fy * along + sy * f.Length / 6;
            Balls.Add(new Ball
            {
                X = bx, Y = by, VX = dx * BallSpeed + vx * f.Speed, VY = dy * BallSpeed + vy * f.Speed,
                Life = BallLife * (0.9 + _rng.Next(20) / 100.0), FromPlayer = f == Player, Power = power,
            });
            Puffs.Add(new Puff(bx + sx * 6, by + sy * 6, "smoke"));
        }
        double reload = ReloadTime(f);
        if (side > 0) f.ReloadStarboard = reload;
        else f.ReloadPort = reload;
        Sound?.Invoke("cannon");
        return true;
    }

    /// <summary>The bearing to where a target will be when a ball fired now reaches her.</summary>
    public static double Lead(Fighter f, Fighter target)
    {
        var (fvx, fvy) = Sailing.Vector(f.Heading);
        var (tvx, tvy) = Sailing.Vector(target.Heading);
        // The ball carries the firing ship's way, so what matters is how the target moves relative to her.
        double rvx = tvx * target.Speed - fvx * f.Speed, rvy = tvy * target.Speed - fvy * f.Speed;
        double px = target.X, py = target.Y;
        for (int i = 0; i < 3; i++)
        {
            double t = Math.Sqrt(Math.Pow(px - f.X, 2) + Math.Pow(py - f.Y, 2)) / BallSpeed;
            px = target.X + rvx * t;
            py = target.Y + rvy * t;
        }
        return Sailing.Bearing(f.X, f.Y, px, py);
    }

    /// <summary>Whether a point lies within a ship's hull (an ellipse along her heading).</summary>
    public static bool Inside(Fighter f, double x, double y)
    {
        var (fx, fy) = Sailing.Vector(f.Heading);
        double dx = x - f.X, dy = y - f.Y;
        double along = dx * fx + dy * fy, across = -dx * fy + dy * fx;
        double a = f.Length / 2, b = f.Length / 4.2;
        return along * along / (a * a) + across * across / (b * b) <= 1;
    }

    private void FlyBalls(double dt)
    {
        foreach (var b in Balls.ToList())
        {
            b.X += b.VX * dt;
            b.Y += b.VY * dt;
            b.Life -= dt;
            var target = b.FromPlayer ? Enemy : Player;
            if (Inside(target, b.X, b.Y))
            {
                Balls.Remove(b);
                Hit(target, b.Power);
                Puffs.Add(new Puff(b.X, b.Y, "hit"));
                continue;
            }
            if (b.Life <= 0)
            {
                Balls.Remove(b);
                Puffs.Add(new Puff(b.X, b.Y, "splash"));
                Sound?.Invoke("splash");
            }
        }
    }

    private void Hit(Fighter f, double power)
    {
        var s = f.Ship;
        s.Hull = Math.Max(0, s.Hull - (int)Math.Ceiling(power * 2.0));
        if (_rng.Chance(30)) s.Sails = Math.Max(0, s.Sails - (int)(power * 2.5));
        if (_rng.Chance(45)) f.Crew = Math.Max(0, f.Crew - _rng.Range(1, (int)Math.Ceiling(power) + 1));
        if (_rng.Chance(7) && s.Guns > 2) s.Guns--;
        Sound?.Invoke("hit");
    }

    // ───────────────────────────── the other captain ─────────────────────────────

    private double _thinkFire;

    private void Think(double dt)
    {
        var e = Enemy;
        var p = Player;
        double d = Distance;
        double toPlayer = Sailing.Bearing(e.X, e.Y, p.X, p.Y);
        bool timid = EnemyRole is Role.Merchant or Role.Treasure;
        double ratio = e.Crew / (double)Math.Max(1, p.Crew);
        double want;
        if (timid)
        {
            // Away from you, on the best point of sail that opens the distance.
            double away = Sailing.Norm(toPlayer + 180);
            want = away;
            double best = double.MinValue;
            for (int a = -90; a <= 90; a += 15)
            {
                double h = Sailing.Norm(away + a);
                double score = Sailing.PointOfSail(h, WindAngle) * Math.Cos(a * Math.PI / 180);
                if (score > best) { best = score; want = h; }
            }
        }
        else if (ratio > 1.4 || EnemyRole == Role.Rival && ratio > 1.1)
            want = toPlayer; // Close and board: they have the men for it.
        else if (d > 320)
            want = Sailing.Norm(toPlayer + (Sailing.Turn(e.Heading, toPlayer) > 0 ? -25 : 25));
        else if (d < 170)
            want = Sailing.Norm(toPlayer + 180 + (Sailing.Turn(e.Heading, toPlayer) > 0 ? -60 : 60));
        else
        {
            // Lay her broadside to you.
            double a = Sailing.Norm(toPlayer + 90), b = Sailing.Norm(toPlayer - 90);
            want = Sailing.Between(e.Heading, a) < Sailing.Between(e.Heading, b) ? a : b;
        }
        SteerToward(e, want, dt, 0.6 + 0.4 * Skill);
        _thinkFire -= dt;
        if (_thinkFire <= 0)
        {
            _thinkFire = 0.25;
            if (Bears(e, p) && d < BallSpeed * BallLife * (0.7 + 0.25 * Skill)) Fire(e, p);
        }
    }

    private void CheckEnd()
    {
        if (Enemy.Ship.Hull <= 0)
        {
            End = BattleEnd.Sunk;
            Say($"The {Enemy.Ship.Name} goes down!");
            Sound?.Invoke("sink");
            return;
        }
        if (Player.Ship.Hull <= 0)
        {
            End = BattleEnd.Lost;
            Say($"The {Player.Ship.Name} is sinking!");
            Sound?.Invoke("sink");
            return;
        }
        bool timid = EnemyRole is Role.Merchant or Role.Treasure;
        if (timid && (Enemy.Ship.Hull < Enemy.Ship.Type.Hull * 0.4 || Enemy.Crew < Enemy.StartCrew / 3))
        {
            End = BattleEnd.Struck;
            Say($"The {Enemy.Ship.Name} strikes her colours!");
            Sound?.Invoke("struck");
            return;
        }
        if (Distance < (Player.Length + Enemy.Length) / 2 * 0.55)
        {
            End = BattleEnd.Boarding;
            Say("Grapples away - boarders!");
            Sound?.Invoke("board");
            return;
        }
        if (Distance > EscapeDistance)
        {
            End = BattleEnd.Escaped;
            Say("The ships lose sight of each other.");
        }
    }
}

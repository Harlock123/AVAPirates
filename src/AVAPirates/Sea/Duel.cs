using AVAPirates.Rules;

namespace AVAPirates.Sea;

public enum Height { High, Mid, Low }

public enum Stance { Ready, Lunging, Recovering, Staggered }

public enum DuelEnd { None, Won, Lost }

/// <summary>A swordsman on the deck: where they stand, how they guard, and how much fight is left in them.</summary>
public sealed class Fencer
{
    public double X { get; set; }
    public Height Guard { get; set; } = Height.Mid;
    public Stance Stance { get; set; }
    public double Timer { get; set; }
    public Height Aim { get; set; }
    public bool Struck { get; set; }
    /// <summary>The will to fight on, 0 to 100; at 0 they yield.</summary>
    public double Morale { get; set; } = 100;
    /// <summary>Seconds the lunge takes to land.</summary>
    public double Windup { get; init; } = 0.22;
    public bool Busy => Stance != Stance.Ready;
}

/// <summary>What the player is doing with their feet and sword this moment.</summary>
public record struct Guard(bool Up, bool Down, bool Back, bool Forward, bool Lunge);

/// <summary>
/// Captain against captain, on a deck with a rail at either end. Hold Up to guard high, Down to guard
/// low, neither to guard the middle; step back and forward; lunge at the height you're guarding. A
/// lunge at the height the other is guarding is parried. Any other lands, and drives them back and
/// takes the heart out of them. Whoever goes over the rail, or loses heart entirely, has lost.
/// </summary>
public sealed class Duel
{
    public const double Reach = 21, Closest = 13, Step = 30, Recover = 0.32, Stagger = 0.4;

    private readonly Rng _rng;
    public Fencer Player { get; }
    public Fencer Enemy { get; }
    public string EnemyName { get; }
    public double Skill { get; }
    public DuelEnd End { get; private set; }
    public double Time { get; private set; }
    public int Hits { get; private set; }
    public int Parries { get; private set; }
    public event Action<string>? Sound;
    /// <summary>The other swordsman stands still and does nothing (for trying the rules out).</summary>
    public bool Passive { get; set; }

    private double _think;
    private double _react;
    private double _watch;

    /// <param name="playerHeart">The player's starting morale: more with more men behind them.</param>
    /// <param name="enemyHeart">The other's starting morale.</param>
    /// <param name="skill">The other's skill with a blade, 0.3 to 1.</param>
    /// <param name="playerWindup">The player's lunge: slower with age.</param>
    public Duel(string enemyName, double playerHeart, double enemyHeart, double skill, Rng rng, double playerWindup = 0.22)
    {
        _rng = rng;
        EnemyName = enemyName;
        Skill = Math.Clamp(skill, 0.2, 1);
        Player = new Fencer { X = 36, Morale = Math.Clamp(playerHeart, 20, 100), Windup = playerWindup };
        Enemy = new Fencer { X = 62, Morale = Math.Clamp(enemyHeart, 20, 100), Windup = 0.32 - 0.12 * Skill };
    }

    /// <summary>Starting heart from how the two crews compare: a big crew behind you is worth a lot.</summary>
    public static (double Player, double Enemy) Hearts(int myCrew, int theirCrew)
    {
        double r = (myCrew + 1.0) / (theirCrew + 1.0);
        double p = 55 + 45 * Math.Clamp((r - 0.5) / 1.5, 0, 1);
        double e = 55 + 45 * Math.Clamp((1 / r - 0.5) / 1.5, 0, 1);
        return (p, e);
    }

    public void Tick(double dt, Guard input)
    {
        if (End != DuelEnd.None) return;
        Time += dt;
        var p = Player;
        if (!p.Busy)
        {
            p.Guard = input.Up ? Height.High : input.Down ? Height.Low : Height.Mid;
            if (input.Lunge) Lunge(p);
            else if (input.Back) p.X -= Step * dt;
            else if (input.Forward) p.X = Math.Min(p.X + Step * dt, Enemy.X - Closest);
        }
        Think(dt);
        Advance(p, Enemy, dt);
        Advance(Enemy, p, dt);
        if (p.X < 2 || p.Morale <= 0)
        {
            End = DuelEnd.Lost;
            Sound?.Invoke(p.X < 2 ? "overboard" : "yield");
        }
        else if (Enemy.X > 98 || Enemy.Morale <= 0)
        {
            End = DuelEnd.Won;
            Sound?.Invoke(Enemy.X > 98 ? "overboard" : "yield");
        }
    }

    private void Lunge(Fencer f)
    {
        f.Stance = Stance.Lunging;
        f.Aim = f.Guard;
        f.Timer = f.Windup;
        f.Struck = false;
        Sound?.Invoke("swish");
    }

    private void Advance(Fencer f, Fencer other, double dt)
    {
        if (f.Stance == Stance.Ready) return;
        f.Timer -= dt;
        if (f.Timer > 0) return;
        switch (f.Stance)
        {
            case Stance.Lunging when !f.Struck:
                f.Struck = true;
                Resolve(f, other);
                if (f.Stance == Stance.Lunging)
                {
                    f.Stance = Stance.Recovering;
                    f.Timer = Recover;
                }
                break;
            default:
                f.Stance = Stance.Ready;
                break;
        }
    }

    /// <summary>A lunge arrives: out of reach it falls short, at the guarded height it's parried, otherwise it lands.</summary>
    private void Resolve(Fencer a, Fencer d)
    {
        bool playerAttacks = a == Player;
        if (Math.Abs(a.X - d.X) > Reach)
        {
            Sound?.Invoke("swish");
            return;
        }
        bool guarded = d.Guard == a.Aim && d.Stance is Stance.Ready or Stance.Recovering;
        if (guarded)
        {
            if (playerAttacks) Parries++;
            Sound?.Invoke("clang");
            a.Stance = Stance.Staggered;
            a.Timer = Stagger * 0.6;
            a.X += playerAttacks ? -3 : 3;
            return;
        }
        if (playerAttacks) Hits++;
        Sound?.Invoke("hit");
        double push = 7;
        d.X += playerAttacks ? push : -push;
        a.X += playerAttacks ? 4 : -4;
        a.X = playerAttacks ? Math.Min(a.X, d.X - Closest) : Math.Max(a.X, d.X + Closest);
        d.Morale -= 10 + _rng.Next(6);
        if (d.Stance == Stance.Lunging) d.Struck = true;
        d.Stance = Stance.Staggered;
        d.Timer = Stagger;
    }

    // ───────────────────────────── the other swordsman ─────────────────────────────

    private void Think(double dt)
    {
        if (Passive) return;
        var e = Enemy;
        var p = Player;
        // Seeing your lunge coming, a good swordsman guards its height in time.
        if (p.Stance == Stance.Lunging && !p.Struck)
        {
            _react -= dt;
            if (_react <= 0 && !e.Busy)
            {
                _react = 99;
                if (_rng.Next(100) < 25 + 60 * Skill) e.Guard = p.Aim;
            }
        }
        else _react = 0.16 - 0.12 * Skill + _rng.Next(6) / 100.0;
        if (e.Busy) return;
        // A good swordsman watches your blade, and guards the height you're holding it at.
        _watch -= dt;
        if (_watch <= 0)
        {
            _watch = 0.42 - 0.3 * Skill + _rng.Next(15) / 100.0;
            if (_rng.Next(100) < 20 + 65 * Skill) e.Guard = p.Guard;
        }
        _think -= dt;
        double gap = e.X - p.X;
        if (gap > Reach - 1)
        {
            e.X = Math.Max(e.X - Step * dt * (0.6 + 0.4 * Skill), p.X + Closest);
            return;
        }
        if (_think > 0) return;
        _think = 0.7 - 0.35 * Skill + _rng.Next(40) / 100.0;
        if (e.Morale < 30 && _rng.Chance(30))
        {
            e.X += 4;
            return;
        }
        // Lunge, at a height you aren't guarding if it can help it.
        var heights = Enum.GetValues<Height>();
        var aim = _rng.Next(100) < 30 + 55 * Skill ? heights.Where(h => h != p.Guard).ElementAt(_rng.Next(2)) : heights[_rng.Next(3)];
        e.Guard = aim;
        Lunge(e);
    }
}

namespace AVAPirates.Rules;

public enum ShipKind { Sloop, Barque, Brigantine, Merchantman, Frigate, Galleon }

/// <summary>
/// A kind of ship. <see cref="Speed"/> is in map squares a second at her best point of sail;
/// <see cref="Agility"/> how fast she turns, in degrees a second. <see cref="Length"/> is for
/// drawing and for when two ships touch.
/// </summary>
public sealed record ShipType(
    ShipKind Kind, string Name, double Speed, double Agility, int Guns, int Crew, int Cargo, int Hull, int Value, double Length, int Masts, string About)
{
    public static readonly IReadOnlyList<ShipType> All =
    [
        new(ShipKind.Sloop, "Sloop", 2.6, 120, 8, 60, 40, 40, 900, 14, 1, "Small and quick, and turns on a coin."),
        new(ShipKind.Barque, "Barque", 2.2, 95, 12, 90, 80, 60, 1400, 17, 2, "A sturdy all-rounder."),
        new(ShipKind.Brigantine, "Brigantine", 2.4, 100, 16, 110, 70, 70, 1800, 18, 2, "Fast, and well armed for her size."),
        new(ShipKind.Merchantman, "Merchantman", 1.8, 70, 10, 60, 160, 90, 1600, 21, 3, "Slow, deep and full of cargo."),
        new(ShipKind.Frigate, "Frigate", 2.2, 80, 28, 200, 100, 110, 3200, 23, 3, "A warship: guns and men in plenty."),
        new(ShipKind.Galleon, "Galleon", 1.6, 55, 32, 240, 220, 160, 4000, 26, 3, "A floating castle, with a hold to match."),
    ];

    public static ShipType Get(ShipKind k) => All[(int)k];
}

/// <summary>A ship of the player's fleet, or met at sea: her kind, her name and how battered she is.</summary>
public sealed class Ship
{
    public ShipKind Kind { get; set; }
    public string Name { get; set; } = "";
    /// <summary>The hull's strength left, out of the type's.</summary>
    public int Hull { get; set; }
    /// <summary>Sails and rigging, 0 to 100.</summary>
    public int Sails { get; set; } = 100;
    public int Guns { get; set; }

    public ShipType Type => ShipType.Get(Kind);

    public static Ship Make(ShipKind k, string name)
    {
        var t = ShipType.Get(k);
        return new Ship { Kind = k, Name = name, Hull = t.Hull, Guns = t.Guns };
    }

    public bool Damaged => Hull < Type.Hull || Sails < 100 || Guns < Type.Guns;

    /// <summary>What she'd fetch in a port, battered as she is.</summary>
    public int Worth => Type.Value * (40 + 60 * Hull / Type.Hull) / 100;

    /// <summary>What mending her would cost at a shipwright.</summary>
    public int RepairCost => (Type.Hull - Hull) * 6 + (100 - Sails) * 3 + (Type.Guns - Guns) * 40;
}

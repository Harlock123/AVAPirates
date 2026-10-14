using System.Text.Json;
using System.Text.Json.Serialization;
using AVAPirates.Rules;

namespace AVAPirates.World;

/// <summary>A famous pirate of the isles, roaming somewhere.</summary>
public sealed class Rival
{
    public string Name { get; set; } = "";
    public ShipKind Ship { get; set; }
    public int Bounty { get; set; }
    public bool Beaten { get; set; }
}

/// <summary>
/// A career, as a saved game remembers it: the captain, the fleet and its crew, the purse and the hold,
/// where the fleet is, the date, the four crowns' opinions of you and of one another, what has
/// become of each port, and the treasure maps.
/// </summary>
public sealed class WorldState
{
    public const double SecondsPerDay = 4;
    public const int DaysPerMonth = 30;
    public const int StartAge = 22;

    public string Captain { get; set; } = "";
    public Nation Homeland { get; set; }
    /// <summary>0 apprentice, 1 journeyman, 2 adventurer, 3 swashbuckler.</summary>
    public int Difficulty { get; set; } = 1;

    public List<Ship> Fleet { get; set; } = [];
    public int Crew { get; set; }
    /// <summary>How the crew feels, 0 (mutinous) to 100 (cheerful).</summary>
    public int Morale { get; set; } = 70;
    /// <summary>Days of food for the crew aboard.</summary>
    public double Food { get; set; }
    /// <summary>The expedition's plunder, to be shared out with the crew.</summary>
    public int Gold { get; set; }
    /// <summary>Your own fortune, banked when the plunder is divided.</summary>
    public int Wealth { get; set; }
    public int[] Cargo { get; set; } = new int[4];

    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Degrees clockwise from north.</summary>
    public double Heading { get; set; }
    public bool SailsUp { get; set; }
    /// <summary>Days since the career began.</summary>
    public double Day { get; set; }
    public double LastDivision { get; set; }
    public double WindAngle { get; set; } = 45;
    public double WindStrength { get; set; } = 1;

    /// <summary>Each crown's gratitude, earned by harrying its enemies.</summary>
    public int[] Favour { get; set; } = new int[4];
    /// <summary>The rank each crown's governors have actually granted.</summary>
    public int[] Rank { get; set; } = new int[4];
    /// <summary>How much each nation hates you, for attacking its ships and towns.</summary>
    public int[] Anger { get; set; } = new int[5];
    public int Land { get; set; }
    /// <summary>Wars between the crowns: War[a * 4 + b].</summary>
    public bool[] War { get; set; } = new bool[16];

    public Nation[] PortNation { get; set; } = [];
    public int[] PortWealth { get; set; } = [];
    public int[] PortSoldiers { get; set; } = [];
    /// <summary>Favour won against each nation since the last report to a governor of a rival crown.</summary>
    public int[] Unreported { get; set; } = new int[5];

    public int MapPieces { get; set; }
    public int TreasureX { get; set; } = -1;
    public int TreasureY { get; set; } = -1;
    public int TreasuresFound { get; set; }
    public List<Rival> Rivals { get; set; } = [];

    public int ShipsTaken { get; set; }
    public int ShipsSunk { get; set; }
    public int TownsSacked { get; set; }
    public int DuelsWon { get; set; }
    public HashSet<string> Flags { get; set; } = [];
    public List<string> Journal { get; set; } = [];
    public ulong Seed { get; set; }
    public bool Retired { get; set; }
    public DateTime Saved { get; set; }

    [JsonIgnore] public Ship Flagship => Fleet[0];
    [JsonIgnore] public int Age => StartAge + (int)(Day / (DaysPerMonth * 12));
    [JsonIgnore] public int Month => (int)(Day / DaysPerMonth) % 12;
    [JsonIgnore] public int Year => 1 + (int)(Day / (DaysPerMonth * 12));
    [JsonIgnore] public int CargoSpace => Fleet.Sum(s => s.Type.Cargo);
    [JsonIgnore] public int CargoUsed => Cargo.Sum();
    [JsonIgnore] public int CrewSpace => Fleet.Sum(s => s.Type.Crew);
    [JsonIgnore] public int Guns => Fleet.Sum(s => s.Guns);
    /// <summary>A fleet sails at the pace of its slowest ship.</summary>
    [JsonIgnore] public double FleetSpeed => Fleet.Count == 0 ? 0 : Fleet.Min(s => s.Type.Speed * (0.4 + 0.6 * s.Sails / 100.0));

    public bool AtWar(Nation a, Nation b) =>
        a != b && (a == Nation.Outlaw || b == Nation.Outlaw || War[(int)a * 4 + (int)b]);

    public void SetWar(Nation a, Nation b, bool war)
    {
        if (a == Nation.Outlaw || b == Nation.Outlaw || a == b) return;
        War[(int)a * 4 + (int)b] = war;
        War[(int)b * 4 + (int)a] = war;
    }

    /// <summary>Whether a nation's ports will let you in: not if it hates you, or you hold no letter and it is outlaw-hunting.</summary>
    public bool Welcome(Nation n) => n == Nation.Outlaw || Anger[(int)n] < 30;

    public static readonly string[] MonthNames = ["Thaw", "Seedtime", "Bloom", "Highsun", "Longday", "Swelter", "Harvest", "Gale", "Fogfall", "Rime", "Deepwinter", "Stillmonth"];

    public string Date => $"{MonthNames[Month]}, year {Year}";

    public void Note(string text)
    {
        Journal.Add($"{Date}: {text}");
        if (Journal.Count > 200) Journal.RemoveAt(0);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static WorldState FromJson(string json) => JsonSerializer.Deserialize<WorldState>(json, Options)
        ?? throw new InvalidDataException("Not a saved game.");
}

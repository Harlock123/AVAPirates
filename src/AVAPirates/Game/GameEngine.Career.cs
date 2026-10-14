using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

public sealed partial class GameEngine
{
    public const int RetireAge = 62;

    /// <summary>The captain's share of the plunder: bigger the harder the life.</summary>
    public double CaptainShare => World.Difficulty switch { 0 => 0.1, 1 => 0.2, 2 => 0.25, _ => 1 / 3.0 };

    /// <summary>Shares out the plunder: the captain banks a share, and the crew take theirs and go ashore.</summary>
    private void DividePlunder()
    {
        var w = World;
        int share = (int)(w.Gold * CaptainShare);
        int each = w.Crew == 0 ? 0 : (w.Gold - share) / w.Crew;
        var p = new Panel { Title = "Dividing the plunder", Kind = PanelKind.Port, Scene = "tavern", Back = Pop };
        p.Say($"{w.Gold} gold to share. Your share as captain is {share}; each of the {w.Crew} men gets {each}.");
        p.Say(each >= 100 ? "The men will be delighted." : each >= 30 ? "The men will be content." : "The men will grumble that it was hardly worth it.");
        p.Say("Most of them will take their money and go ashore. You'll keep a few hands, and the ships.");
        p.Add("Divide it, and go on", () => { Pop(); Divide(); });
        p.Add($"Divide it, and retire ({Age()} years old)", () => { Pop(); Divide(); Retire(); });
        p.Add("Not yet", Pop);
        Push(p);
    }

    private string Age() => World.Age.ToString();

    public void Divide()
    {
        var w = World;
        int share = (int)(w.Gold * CaptainShare);
        int each = w.Crew == 0 ? 0 : (w.Gold - share) / w.Crew;
        w.Wealth += share;
        w.Note($"Divided {w.Gold} gold: {share} to the captain, {each} a man.");
        w.Gold = 0;
        int stay = Math.Min(w.Crew, 6 + (each >= 100 ? w.Crew / 5 : 0));
        w.Crew = stay;
        w.Morale = Math.Clamp(60 + each / 10, 60, 90);
        w.LastDivision = w.Day;
        PassTime(14);
        Play(Sfx.Coin);
        Say($"You bank {share} gold. {stay} men stay aboard.", 0xFFFFD860);
        if (w.Age >= RetireAge && !w.Retired) Retire();
    }

    // ───────────────────────────── retiring ─────────────────────────────

    /// <summary>The career's score: fortune, titles, land, treasure, famous pirates beaten, and the prizes along the way.</summary>
    public int Score()
    {
        var w = World;
        return w.Wealth / 100 + w.Rank.Sum() * 6 + w.Land / 40 + w.TreasuresFound * 10 + w.Rivals.Count(r => r.Beaten) * 12 + w.ShipsTaken + w.TownsSacked * 3
            + w.Difficulty * 10;
    }

    /// <summary>What becomes of a captain, by how the career went.</summary>
    public string Fate()
    {
        var w = World;
        int s = Score();
        var best = Nations.Crowns.OrderByDescending(n => w.Rank[(int)n]).First();
        string title = w.Rank[(int)best] > 0 ? $"{Nations.Ranks[w.Rank[(int)best]].Title} of {Nations.Get(best).Name}" : "";
        return s switch
        {
            >= 160 => $"You retire to a palace and a seat at the Governor-General's table{(title.Length > 0 ? $", {title}" : "")}. Ballads are sung about you in every tavern in the isles.",
            >= 100 => $"You retire a wealthy {(title.Length > 0 ? title : "gentleman")}, with an estate on the hill and a fine view of the harbour.",
            >= 60 => "You buy a comfortable house in town and spend your evenings in the tavern, telling stories nobody quite believes.",
            >= 30 => "You settle down as a harbour pilot. It's honest work, and it doesn't pay much.",
            _ => "You end your days mending nets on the waterfront, for a few coppers and a mug of ale.",
        };
    }

    public void Retire()
    {
        var w = World;
        w.Retired = true;
        Mode = Mode.Retired;
        CloseAll();
        Held.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.Ending);
        w.Note("Retired from the sea.");
        var p = new Panel { Title = $"The retirement of Captain {w.Captain}", Kind = PanelKind.Ending };
        p.Say(Fate());
        p.Say($"At {w.Age}, after {(int)(w.Day / (WorldState.DaysPerMonth * 12))} years at sea: a fortune of {w.Wealth} gold, {w.Land} acres, {w.ShipsTaken} ships taken and {w.ShipsSunk} sunk, " +
              $"{w.TownsSacked} towns sacked, {w.TreasuresFound} treasures found, and {w.Rivals.Count(r => r.Beaten)} of the {w.Rivals.Count} famous pirates beaten.");
        var titles = Nations.Crowns.Where(n => w.Rank[(int)n] > 0).Select(n => $"{Nations.Ranks[w.Rank[(int)n]].Title} of {Nations.Get(n).Name}").ToList();
        if (titles.Count > 0) p.Say("Titles: " + string.Join(", ", titles) + ".");
        p.Say($"Score: {Score()}.");
        p.Add("Return to the title", ShowTitle);
        Push(p);
    }

    // ───────────────────────────── the ship's books ─────────────────────────────

    public void StatusPanel()
    {
        var w = World;
        var p = new Panel { Title = $"Captain {w.Captain}", Kind = PanelKind.Big, Back = Pop };
        p.Say($"{w.Date}. Age {w.Age}. Fortune {w.Wealth} gold, {w.Land} acres. Plunder aboard: {w.Gold} gold.");
        p.Say($"Crew {w.Crew} of {w.CrewSpace} (they are {MoraleWord(w.Morale)}). Food for {FoodDays()} days. Hold: " +
              (w.CargoUsed == 0 ? "empty" : string.Join(", ", Enum.GetValues<Good>().Where(g => w.Cargo[(int)g] > 0).Select(g => $"{w.Cargo[(int)g]} tons of {g.ToString().ToLowerInvariant()}"))) + ".");
        foreach (var s in w.Fleet)
            p.Say($"{(s == w.Flagship ? "Flagship: " : "")}the {s.Type.Name.ToLowerInvariant()} {s.Name}: hull {s.Hull}/{s.Type.Hull}, sails {s.Sails}%, {s.Guns} guns.");
        foreach (var n in Nations.Crowns)
        {
            int i = (int)n;
            string rank = w.Rank[i] > 0 ? Nations.Ranks[w.Rank[i]].Title : "no title";
            string wars = string.Join(", ", Nations.Crowns.Where(o => w.AtWar(n, o)).Select(o => Nations.Get(o).Name));
            p.Say($"{Nations.Get(n).Name}: {rank}, favour {w.Favour[i]}{(w.Anger[i] > 0 ? $", anger {w.Anger[i]}" : "")}. {(wars.Length > 0 ? "At war with " + wars : "At peace")}.");
        }
        if (w.MapPieces > 0) p.Say($"Treasure map: {w.MapPieces} of 4 pieces.");
        p.Add("Close", Pop);
        Push(p);
    }

    public static string MoraleWord(int m) => m switch { >= 80 => "cheerful", >= 60 => "content", >= 40 => "restless", >= 20 => "angry", _ => "mutinous" };

    public void JournalPanel()
    {
        var p = new Panel { Title = "The captain's log", Kind = PanelKind.Big, Back = Pop };
        if (World.Journal.Count == 0) p.Say("Nothing written yet.");
        foreach (var n in Enumerable.Reverse(World.Journal)) p.Add(n, null);
        p.Add("Close", Pop);
        Push(p);
    }

    public void SeaMenu()
    {
        var p = new Panel { Title = "Menu", Back = Pop };
        p.Add("Back to the game", Pop);
        p.Add("The ship's books", StatusPanel);
        p.Add("The captain's log", JournalPanel);
        p.Add("Save the career", SaveMenu, !InDemo);
        p.Add("Load a career", LoadMenu, Saves.Any());
        p.Add("How to play", Help);
        p.Add("Quit to the title", ShowTitle);
        Push(p);
    }

    private void SaveMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "Save", Back = Pop };
            for (int s = 1; s <= SaveStore.Slots; s++)
            {
                int slot = s;
                p.Add(Saves.Describe(slot), () =>
                {
                    World.Seed = Rng.State;
                    World.Flags.Add(Mode == Mode.Port && InPort is not null ? "saved-in-port:" + InPort.Id : "saved-at-sea");
                    Saves.Save(slot, World);
                    World.Flags.RemoveWhere(f => f.StartsWith("saved-"));
                    Say($"Saved in slot {slot}.");
                    Pop();
                });
            }
            p.Add("Cancel", Pop);
            return p;
        });
    }

    public void LoadMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "Load", Back = Pop };
            for (int s = 1; s <= SaveStore.Slots; s++)
            {
                int slot = s;
                p.Add(Saves.Describe(slot), () => Load(slot), Saves.Load(slot) is not null);
            }
            p.Add("Cancel", Pop);
            return p;
        });
    }

    public bool Load(int slot)
    {
        var w = Saves.Load(slot);
        if (w is null) return false;
        World = w;
        Rng = new Rng(w.Seed);
        Traffic = new Traffic(Map, Rng);
        Traffic.Populate(w, all: true);
        Demo = null;
        Battle = null;
        Duel = null;
        CloseAll();
        Log.Clear();
        Held.Clear();
        _lastMonth = (int)(w.Day / WorldState.DaysPerMonth);
        var inPort = w.Flags.FirstOrDefault(f => f.StartsWith("saved-in-port:"));
        w.Flags.RemoveWhere(f => f.StartsWith("saved-"));
        Say("Your career continues.");
        if (inPort is not null) EnterPort(Map.Ports[int.Parse(inPort["saved-in-port:".Length..])], welcomed: true);
        else
        {
            Mode = Mode.Sea;
            InPort = null;
            Play(Sfx.MusicStop);
            Play(Sfx.SeaMusic);
        }
        return true;
    }
}

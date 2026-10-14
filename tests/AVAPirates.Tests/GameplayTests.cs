using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Tests;

public sealed class GameplayTests
{
    private static SeaMap Map => GameEngine.Map;

    private static SeaShip Foe(Role role, ShipKind kind, Nation n, int crew, int gold = 500) =>
        new() { Id = 900 + (int)role, Nation = n, Role = role, Ship = Ship.Make(kind, "Foe"), Crew = crew, Gold = gold };

    // ───────────────────────────── the isles ─────────────────────────────

    [Fact]
    public void The_isles_come_out_the_same_every_time()
    {
        Assert.Equal(SeaMap.Build().Dump(), SeaMap.Build().Dump());
        Assert.InRange(Map.Ports.Count, 22, 24);
    }

    [Fact]
    public void Every_port_can_be_sailed_to_from_every_other()
    {
        var first = Map.Ports[0];
        var sea = Map.Region((x, y) => Map.Water(x, y), first.DockX, first.DockY);
        foreach (var p in Map.Ports)
        {
            Assert.Contains((p.DockX, p.DockY), sea);
            Assert.False(Map.Water(p.X, p.Y));
            Assert.Equal(1, Math.Abs(p.X - p.DockX) + Math.Abs(p.Y - p.DockY));
        }
        Assert.NotEmpty(Map.Route((first.DockX, first.DockY), (Map.Ports[^1].DockX, Map.Ports[^1].DockY)));
    }

    [Fact]
    public void Valdoro_holds_the_most_ports_and_the_Brotherhood_two_havens()
    {
        var counts = Map.Ports.GroupBy(p => p.Nation).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, counts[Nation.Outlaw]);
        Assert.True(counts[Nation.Valdoro] > counts[Nation.Fennick]);
        Assert.True(counts[Nation.Valdoro] > counts[Nation.Lusande]);
        Assert.Equal(Map.Ports.Count, Map.Ports.Select(p => p.Name).Distinct().Count());
    }

    // ───────────────────────────── sailing ─────────────────────────────

    [Fact]
    public void A_ship_goes_fastest_across_the_wind_and_barely_into_it()
    {
        double wind = 90;
        Assert.Equal(1.0, Sailing.PointOfSail(0, wind), 2);
        Assert.True(Sailing.PointOfSail(270, wind) < 0.2);
        Assert.True(Sailing.PointOfSail(90, wind) < Sailing.PointOfSail(30, wind));
        Assert.Equal(180, Sailing.Between(10, 190), 6);
        Assert.Equal(-20, Sailing.Turn(10, 350), 6);
        Assert.Equal(90, Sailing.Bearing(0, 0, 5, 0), 6);
        Assert.Equal(180, Sailing.Bearing(0, 0, 0, 5), 6);
    }

    [Fact]
    public void Steering_and_sails_move_the_fleet_and_the_days_go_by()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        w.WindAngle = Sailing.Norm(w.Heading + 90);
        var (x, y) = (w.X, w.Y);
        double day = w.Day;
        Helpers.Run(g, 2);
        Assert.True(Math.Abs(w.X - x) + Math.Abs(w.Y - y) > 1, "she should have sailed");
        Assert.True(w.Day > day);
        double heading = w.Heading;
        g.Press("Left");
        Helpers.Run(g, 0.5);
        g.Release("Left");
        Assert.True(Sailing.Turn(heading, w.Heading) < -20);
        g.Press("Down");
        g.Release("Down");
        Helpers.Run(g, 6);
        Assert.False(w.SailsUp);
        Assert.True(g.Speed < 0.2);
    }

    [Fact]
    public void Food_is_eaten_at_sea_and_an_empty_larder_sours_the_crew()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        w.Food = 2;
        int morale = w.Morale;
        g.PassTime(40);
        Assert.Equal(0, w.Food);
        Assert.True(w.Morale < morale);
    }

    [Fact]
    public void Sailing_into_a_friendly_port_puts_in_and_hostile_ones_turn_you_away()
    {
        var g = Helpers.AtSea(Nation.Fennick);
        var w = g.World;
        var friend = Map.Ports.First(p => w.PortNation[p.Id] == Nation.Fennick);
        g.ArriveAt(friend);
        Assert.Equal(Mode.Port, g.Mode);
        Assert.Equal(friend, g.InPort);
        g.LeavePort();
        var enemy = Map.Ports.First(p => w.PortNation[p.Id] == Nation.Valdoro);
        w.Anger[(int)Nation.Valdoro] = 50;
        g.ArriveAt(enemy);
        Assert.NotEqual(Mode.Port, g.Mode);
        Assert.Contains(g.Top!.Options, o => o.Label == "Slip in by night");
    }

    // ───────────────────────────── ports ─────────────────────────────

    [Fact]
    public void The_tavern_signs_on_men_once_a_visit_up_to_what_the_ships_hold()
    {
        var g = Helpers.NewCareer();
        var w = g.World;
        int men = g.Recruits(g.InPort!);
        Assert.True(men > 0);
        g.Top!.Selected = 1;
        g.Press("Enter");
        g.Press("1");
        Assert.Equal(40 + men, w.Crew);
        Assert.True(w.Crew <= w.CrewSpace);
        Assert.Equal(0, g.Recruits(g.InPort!));
    }

    [Fact]
    public void The_merchant_buys_cheap_where_goods_grow_and_sells_dear_where_they_re_wanted()
    {
        var g = Helpers.NewCareer();
        var port = Map.Ports.First(p => p.Grows != p.Wants);
        Assert.True(g.Price(port, port.Grows) < Goods.Base[(int)port.Grows]);
        Assert.True(g.Price(port, port.Wants) > Goods.Base[(int)port.Wants]);
        Assert.True(g.SellPrice(port, port.Wants) < g.Price(port, port.Wants));
        // Right buys a ton, Left sells it back.
        var w = g.World;
        g.Top!.Selected = 2;
        g.Press("Enter");
        int gold = w.Gold;
        g.Press("Right");
        Assert.Equal(1, w.Cargo[0]);
        Assert.True(w.Gold < gold);
        g.Press("Left");
        Assert.Equal(0, w.Cargo[0]);
    }

    [Fact]
    public void The_governor_grants_titles_and_land_for_favour_won()
    {
        var g = Helpers.NewCareer(Nation.Fennick);
        var w = g.World;
        w.Favour[(int)Nation.Fennick] = Nations.Ranks[4].Favour;
        g.Press("1");
        Assert.Equal(4, w.Rank[(int)Nation.Fennick]);
        Assert.Equal(Nations.Ranks.Take(5).Sum(r => r.Land), w.Land);
        Assert.Contains("Colonel", g.Top!.Text[0]);
    }

    [Fact]
    public void Dividing_the_plunder_banks_the_captain_s_share_and_cheers_the_crew()
    {
        var g = Helpers.NewCareer(difficulty: 2);
        var w = g.World;
        w.Gold = 10000;
        w.Crew = 50;
        w.Morale = 20;
        g.Divide();
        Assert.Equal(2500, w.Wealth);
        Assert.Equal(0, w.Gold);
        Assert.True(w.Morale >= 60);
        Assert.True(w.Crew < 50);
    }

    [Fact]
    public void The_shipwright_mends_the_fleet_for_a_price()
    {
        var g = Helpers.NewCareer();
        var w = g.World;
        w.Flagship.Hull = 10;
        w.Flagship.Sails = 40;
        int cost = w.Flagship.RepairCost;
        Assert.True(cost > 0);
        w.Gold = 1000;
        g.Top!.Selected = 3;
        g.Press("Enter");
        g.Press("1");
        Assert.Equal(w.Flagship.Type.Hull, w.Flagship.Hull);
        Assert.Equal(1000 - cost, w.Gold);
    }

    // ───────────────────────────── ships met at sea ─────────────────────────────

    [Fact]
    public void Running_into_a_ship_at_sea_meets_her()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        var (vx, vy) = Sailing.Vector(w.Heading);
        var s = Foe(Role.Merchant, ShipKind.Merchantman, Nation.Valdoro, 30);
        s.X = w.X + vx * 0.5;
        s.Y = w.Y + vy * 0.5;
        s.Route = [((int)s.X, (int)s.Y)];
        g.Traffic.Ships.Add(s);
        Helpers.Run(g, 0.1);
        Assert.Equal(PanelKind.Encounter, g.Top?.Kind);
        Assert.Contains(g.Top!.Options, o => o.Label == "Attack her");
    }

    [Fact]
    public void Pirates_and_angered_warships_hunt_you_and_merchants_do_not()
    {
        var w = new WorldState();
        Assert.True(Traffic.Hostile(Foe(Role.Pirate, ShipKind.Sloop, Nation.Outlaw, 40), w));
        Assert.False(Traffic.Hostile(Foe(Role.Merchant, ShipKind.Barque, Nation.Valdoro, 40), w));
        var warship = Foe(Role.Warship, ShipKind.Frigate, Nation.Valdoro, 150);
        Assert.False(Traffic.Hostile(warship, w));
        w.Anger[(int)Nation.Valdoro] = 40;
        Assert.True(Traffic.Hostile(warship, w));
    }

    [Fact]
    public void Taking_an_enemy_merchant_angers_her_crown_and_pleases_its_enemies()
    {
        var g = Helpers.AtSea(Nation.Fennick);
        var w = g.World;
        w.Gold = 0;
        var s = Foe(Role.Merchant, ShipKind.Merchantman, Nation.Valdoro, 30, gold: 400);
        s.Cargo[2] = 10;
        g.Capture(s);
        Assert.Equal(400, w.Gold);
        Assert.Equal(10, w.Cargo[2]);
        Assert.True(w.Anger[(int)Nation.Valdoro] > 0);
        Assert.True(w.Favour[(int)Nation.Fennick] > 1);
        Assert.Equal(1, w.ShipsTaken);
        g.Press("1");
        Assert.Equal(2, w.Fleet.Count);
    }

    [Fact]
    public void Beating_a_famous_pirate_pays_the_bounty_and_every_crown_is_grateful()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        var s = new SeaShip { Id = 77, Nation = Nation.Outlaw, Role = Role.Rival, Ship = Ship.Make(ShipKind.Brigantine, "Raven's Due"), Crew = 80, Gold = 300, Captain = w.Rivals[0].Name, RivalIndex = 0 };
        int favour = w.Favour.Sum();
        g.Capture(s);
        Assert.True(w.Rivals[0].Beaten);
        Assert.Equal(300 + 300 + w.Rivals[0].Bounty, w.Gold);
        Assert.True(w.Favour.Sum() >= favour + 8);
    }

    // ───────────────────────────── sea fights ─────────────────────────────

    [Fact]
    public void Cannonballs_strike_a_ship_shaped_target()
    {
        var f = new Fighter { Ship = Ship.Make(ShipKind.Frigate, "T"), Heading = 90, X = 0, Y = 0 };
        Assert.True(SeaBattle.Inside(f, f.Length * 0.4, 0));
        Assert.False(SeaBattle.Inside(f, 0, f.Length * 0.4));
        Assert.False(SeaBattle.Inside(f, f.Length * 0.6, 0));
    }

    [Fact]
    public void A_broadside_fires_from_the_side_facing_the_enemy_and_must_reload()
    {
        var b = new SeaBattle(Ship.Make(ShipKind.Brigantine, "Me"), 100, Ship.Make(ShipKind.Barque, "Them"), 80, Role.Warship, 0, 1, new Rng(3));
        b.Player.X = 0;
        b.Player.Y = 0;
        b.Player.Heading = 0;
        b.Enemy.X = 200;
        b.Enemy.Y = 0;
        Assert.True(b.Fire(b.Player, b.Enemy));
        Assert.True(b.Player.ReloadStarboard > 0);
        Assert.Equal(0, b.Player.ReloadPort);
        Assert.All(b.Balls, ball => Assert.True(ball.VX > 0));
        // The other broadside still fires; then nothing until they reload.
        Assert.True(b.Fire(b.Player, b.Enemy));
        Assert.False(b.Fire(b.Player, b.Enemy));
        // Too few hands and the guns load slower.
        var thin = new Fighter { Ship = Ship.Make(ShipKind.Frigate, "T"), Crew = 20 };
        var full = new Fighter { Ship = Ship.Make(ShipKind.Frigate, "T"), Crew = 200 };
        Assert.True(SeaBattle.ReloadTime(thin) > SeaBattle.ReloadTime(full));
    }

    [Fact]
    public void A_beaten_merchant_strikes_her_colours()
    {
        var g = Helpers.AtSea();
        g.World.Fleet[0] = Ship.Make(ShipKind.Frigate, "Dauntless");
        g.World.Crew = 180;
        var s = Foe(Role.Merchant, ShipKind.Merchantman, Nation.Valdoro, 40, gold: 600);
        g.Attack(s);
        var b = g.Battle!;
        for (int t = 0; t < 60 * 180 && b.End == BattleEnd.None; t++) b.Tick(GameEngine.Step, g.AutoHelm());
        Assert.True(b.End is BattleEnd.Struck or BattleEnd.Boarding or BattleEnd.Sunk);
    }

    [Fact]
    public void Evenly_matched_ships_fight_it_out_in_reasonable_time_with_either_able_to_win()
    {
        int sunk = 0, lost = 0;
        for (int i = 0; i < 24; i++)
        {
            var g = Helpers.AtSea(seed: i + 1);
            g.World.Fleet[0] = Ship.Make(ShipKind.Brigantine, "Me");
            g.World.Crew = 100;
            g.Attack(Foe(Role.Warship, ShipKind.Brigantine, Nation.Valdoro, 90));
            var b = g.Battle!;
            for (int t = 0; t < 60 * 150 && b.End == BattleEnd.None; t++) b.Tick(GameEngine.Step, g.AutoHelm());
            Assert.NotEqual(BattleEnd.None, b.End);
            if (b.End == BattleEnd.Sunk) sunk++;
            if (b.End == BattleEnd.Lost) lost++;
        }
        Assert.True(sunk >= 5 && lost >= 5, $"sunk {sunk}, lost {lost}");
    }

    [Fact]
    public void A_crew_with_the_numbers_runs_alongside_and_boards()
    {
        var g = Helpers.AtSea();
        g.World.Fleet[0] = Ship.Make(ShipKind.Brigantine, "Me");
        g.World.Crew = 60;
        g.Attack(Foe(Role.Warship, ShipKind.Frigate, Nation.Valdoro, 190));
        var b = g.Battle!;
        for (int t = 0; t < 60 * 90 && b.End == BattleEnd.None; t++)
        {
            // Hold a course across the wind and let her come to you.
            double turn = Sailing.Turn(b.Player.Heading, Sailing.Norm(b.WindAngle + 90));
            b.Tick(GameEngine.Step, new Helm(turn < -3, turn > 3, false, true, false));
        }
        Assert.Equal(BattleEnd.Boarding, b.End);
        Helpers.Run(g, 2);
        Assert.Equal(Mode.Duel, g.Mode);
    }

    [Fact]
    public void Losing_the_only_ship_washes_you_up_poorer_in_a_friendly_port()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        w.Gold = 5000;
        var s = Foe(Role.Warship, ShipKind.Frigate, Nation.Valdoro, 150);
        g.Attack(s);
        w.Flagship.Hull = 0;
        Helpers.Run(g, 2);
        Assert.Equal(0, w.Gold);
        Assert.Single(w.Fleet);
        g.Press("Enter");
        Assert.Equal(Mode.Port, g.Mode);
        Assert.True(w.Welcome(g.NationOf(g.InPort!)));
    }

    // ───────────────────────────── sword fights ─────────────────────────────

    [Fact]
    public void A_lunge_at_the_guarded_height_is_parried_and_any_other_lands()
    {
        var d = new Duel("Foe", 100, 100, 0.01, new Rng(1)) { Passive = true };
        d.Player.X = 50;
        d.Enemy.X = 60;
        d.Enemy.Guard = Height.High;
        // Lunge high, into the guard: parried.
        d.Tick(GameEngine.Step, new Guard(true, false, false, false, true));
        for (int i = 0; i < 20; i++) d.Tick(GameEngine.Step, new Guard(true, false, false, false, false));
        Assert.Equal(1, d.Parries);
        Assert.Equal(0, d.Hits);
        for (int i = 0; i < 60; i++) d.Tick(GameEngine.Step, default);
        // Lunge low, under it: a hit, and they're driven back.
        d.Enemy.Guard = Height.High;
        d.Player.X = d.Enemy.X - 10;
        double before = d.Enemy.X, heart = d.Enemy.Morale;
        d.Tick(GameEngine.Step, new Guard(false, true, false, false, true));
        for (int i = 0; i < 15; i++) d.Tick(GameEngine.Step, new Guard(false, true, false, false, false));
        Assert.True(d.Hits >= 1);
        Assert.True(d.Enemy.X > before);
        Assert.True(d.Enemy.Morale < heart);
    }

    [Fact]
    public void Driving_a_swordsman_over_the_rail_wins()
    {
        var d = new Duel("Foe", 100, 100, 0.01, new Rng(1)) { Passive = true };
        d.Enemy.X = 97.5;
        d.Player.X = 88;
        d.Enemy.Guard = Height.High;
        d.Tick(GameEngine.Step, new Guard(false, true, false, false, true));
        for (int i = 0; i < 20 && d.End == DuelEnd.None; i++) d.Tick(GameEngine.Step, new Guard(false, true, false, false, false));
        Assert.Equal(DuelEnd.Won, d.End);
    }

    [Fact]
    public void A_bigger_crew_behind_you_puts_more_heart_into_you()
    {
        var (mine, theirs) = Duel.Hearts(150, 50);
        Assert.True(mine > theirs);
        var (a, b) = Duel.Hearts(80, 80);
        Assert.Equal(a, b, 3);
    }

    [Fact]
    public void Better_swordsmen_win_more_and_the_crews_matter()
    {
        int Wins(double skill, int my, int their)
        {
            int won = 0;
            for (int i = 0; i < 40; i++)
            {
                var g = Helpers.NewCareer(seed: i + 3);
                g.StartDuel("Foe", my, their, skill);
                var d = g.Duel!;
                for (int t = 0; t < 60 * 120 && d.End == DuelEnd.None; t++) d.Tick(GameEngine.Step, g.AutoGuard());
                if (d.End == DuelEnd.Won) won++;
            }
            return won;
        }
        int easy = Wins(0.4, 100, 100), hard = Wins(0.94, 100, 100);
        Assert.True(easy > hard + 8, $"easy {easy}, hard {hard}");
        Assert.True(Wins(0.76, 150, 50) > Wins(0.76, 50, 150));
    }

    [Fact]
    public void Winning_a_boarding_takes_the_ship_and_losing_one_costs_men()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        var s = Foe(Role.Pirate, ShipKind.Sloop, Nation.Outlaw, 30, gold: 250);
        w.Gold = 900;
        g.StartDuel("the pirate captain", w.Crew, s.Crew, 0.5, boarding: s);
        g.Duel!.Enemy.Morale = 0;
        Helpers.Run(g, 2);
        Assert.Equal(1150, w.Gold);
        Assert.Equal(1, w.ShipsTaken);
        g.CloseAll();
        g.StartDuel("the pirate captain", w.Crew, 30, 0.5, boarding: Foe(Role.Pirate, ShipKind.Sloop, Nation.Outlaw, 30));
        int crew = w.Crew;
        g.Duel!.Player.Morale = 0;
        Helpers.Run(g, 2);
        Assert.True(w.Crew < crew);
        Assert.True(w.Gold < 1150);
    }

    [Fact]
    public void Sacking_a_town_takes_its_gold_and_earns_its_crown_s_hatred()
    {
        var g = Helpers.AtSea(Nation.Fennick);
        var w = g.World;
        var town = Map.Ports.First(p => w.PortNation[p.Id] == Nation.Valdoro);
        int wealth = w.PortWealth[town.Id];
        w.Gold = 0;
        w.Crew = 60;
        g.AttackTown(town);
        Assert.Equal(Mode.Duel, g.Mode);
        g.Duel!.Enemy.Morale = 0;
        Helpers.Run(g, 2);
        Assert.True(w.Gold >= 200);
        Assert.True(w.PortWealth[town.Id] < wealth);
        Assert.True(w.Anger[(int)Nation.Valdoro] >= 25);
        Assert.Equal(1, w.TownsSacked);
        Assert.True(w.Favour[(int)Nation.Fennick] >= 5);
    }

    // ───────────────────────────── the career ─────────────────────────────

    [Fact]
    public void A_new_career_starts_with_a_sloop_in_the_homeland_s_greatest_port()
    {
        var g = Helpers.NewCareer(Nation.Lusande);
        var w = g.World;
        Assert.Equal(Mode.Port, g.Mode);
        Assert.Equal(Nation.Lusande, g.NationOf(g.InPort!));
        Assert.Equal(ShipKind.Sloop, w.Flagship.Kind);
        Assert.Equal(Map.Ports.Where(p => p.Nation == Nation.Lusande).Max(p => p.Size), g.InPort!.Size);
        Assert.True(w.AtWar(Nation.Valdoro, Nation.Fennick));
        Assert.True(w.AtWar(Nation.Lusande, Nation.Outlaw));
        Assert.Equal(6, w.Rivals.Count);
    }

    [Fact]
    public void The_months_bring_restlessness_wars_and_the_treasure_galleon()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        int morale = w.Morale;
        g.PassTime(WorldState.DaysPerMonth * 5 - w.Day + 1);
        Assert.True(w.Morale < morale);
        Assert.Contains(g.Traffic.Ships, s => s.Role == Role.Treasure);
        Assert.Contains("galleon", w.Flags);
    }

    [Fact]
    public void A_crew_left_too_long_without_their_share_mutinies()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        w.Morale = 6;
        w.Gold = 1000;
        g.PassTime(WorldState.DaysPerMonth);
        Assert.Equal("Mutiny!", g.Top?.Title);
        Assert.Equal(500, w.Gold);
    }

    [Fact]
    public void Four_pieces_of_map_lead_to_the_treasure()
    {
        var g = Helpers.AtSea();
        var w = g.World;
        Assert.True(GameEngine.Vagueness(1) > GameEngine.Vagueness(3));
        Assert.Equal(0, GameEngine.Vagueness(4));
        w.MapPieces = 4;
        g.BuryTreasure();
        Assert.Equal(Terrain.Sand, Map[w.TreasureX, w.TreasureY]);
        var (tx, ty) = (w.TreasureX, w.TreasureY);
        var water = new[] { (0, 1), (1, 0), (0, -1), (-1, 0) }.First(d => Map.Water(tx + d.Item1, ty + d.Item2));
        g.Dig();
        Assert.Equal(0, w.TreasuresFound);
        w.X = tx + water.Item1 + 0.5;
        w.Y = ty + water.Item2 + 0.5;
        int gold = w.Gold;
        g.Dig();
        Assert.Equal(1, w.TreasuresFound);
        Assert.True(w.Gold > gold + 2000);
        Assert.Equal(0, w.MapPieces);
    }

    [Fact]
    public void Retiring_reckons_up_a_score_and_a_fate()
    {
        var poor = Helpers.NewCareer();
        poor.Retire();
        Assert.Equal(Mode.Retired, poor.Mode);
        Assert.Contains("nets", poor.Fate());
        var rich = Helpers.NewCareer();
        rich.World.Wealth = 12000;
        rich.World.Rank[1] = 7;
        rich.World.Land = 1200;
        rich.World.TreasuresFound = 2;
        Assert.True(rich.Score() > poor.Score() + 100);
        Assert.Contains("palace", rich.Fate());
    }

    [Fact]
    public void A_career_saves_and_loads_at_sea_and_in_port()
    {
        var g = Helpers.NewCareer(Nation.Brannmark);
        var w = g.World;
        w.Wealth = 1234;
        w.Cargo[3] = 9;
        w.Fleet.Add(Ship.Make(ShipKind.Barque, "Gannet"));
        w.Flags.Add("saved-in-port:" + g.InPort!.Id);
        g.Saves.Save(1, w);
        w.Flags.RemoveWhere(f => f.StartsWith("saved-"));
        var h = new GameEngine(5, g.Saves);
        Assert.True(h.Load(1));
        Assert.Equal(Mode.Port, h.Mode);
        Assert.Equal(1234, h.World.Wealth);
        Assert.Equal(9, h.World.Cargo[3]);
        Assert.Equal(2, h.World.Fleet.Count);
        Assert.StartsWith("Slot 1: Captain Ash", h.Saves.Describe(1));
    }

    [Fact]
    public void The_demo_sails_and_any_key_ends_it()
    {
        var g = new GameEngine(5, Helpers.TempSaves());
        Helpers.Run(g, GameEngine.DemoAfter + 0.1);
        Assert.True(g.InDemo);
        var (x, y) = (g.World.X, g.World.Y);
        Helpers.Run(g, 20);
        Assert.True(g.InDemo);
        Assert.True(Math.Abs(g.World.X - x) + Math.Abs(g.World.Y - y) > 2 || g.Mode is Mode.Battle or Mode.Duel);
        g.Press("Space");
        Assert.False(g.InDemo);
        Assert.Equal(Mode.Title, g.Mode);
    }
}

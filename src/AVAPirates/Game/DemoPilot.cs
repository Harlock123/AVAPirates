using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Game;

/// <summary>
/// Plays the attract demo: a brigantine sails from port to port across the isles, falls on whatever
/// ships it meets (the computer at the helm and with the sword), and takes every prize it can.
/// </summary>
public sealed class DemoPilot(GameEngine game)
{
    private double _next = 1.0;
    private List<(int X, int Y)> _route = [];
    private int _at;
    public double Elapsed { get; private set; }

    public void Update(double dt)
    {
        Elapsed += dt;
        _next -= dt;
        if (game.Top is { } p)
        {
            if (_next > 0) return;
            _next = 1.5;
            // In port, set sail; anywhere else, take the first choice (fight, keep her, continue).
            int pick = game.Mode == Mode.Port ? p.Options.FindIndex(o => o.Label == "Set sail") : -1;
            if (pick < 0) pick = p.Options.FindIndex(o => o.Enabled && o.Act is not null);
            if (pick >= 0) { p.Selected = pick; game.Input("Enter"); }
            else game.Input("Esc");
            return;
        }
        if (game.Mode != Mode.Sea) return;
        Steer();
    }

    private void Steer()
    {
        var w = game.World;
        var map = GameEngine.Map;
        if (_at >= _route.Count)
        {
            var dest = map.Ports[game.Rng.Next(map.Ports.Count)];
            _route = map.Route(((int)w.X, (int)w.Y), (dest.DockX, dest.DockY));
            _at = 0;
            if (_route.Count == 0) return;
        }
        var (tx, ty) = (_route[_at].X + 0.5, _route[_at].Y + 0.5);
        if (Math.Abs(tx - w.X) + Math.Abs(ty - w.Y) < 0.8)
        {
            _at += 2;
            return;
        }
        double turn = Sailing.Turn(w.Heading, Sailing.Bearing(w.X, w.Y, tx, ty));
        game.Held.Remove("Left");
        game.Held.Remove("Right");
        if (turn < -6) game.Held.Add("Left");
        else if (turn > 6) game.Held.Add("Right");
        w.SailsUp = true;
    }
}

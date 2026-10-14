using Avalonia;
using Avalonia.Media;
using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Sea;

namespace AVAPirates.Views;

public sealed partial class GameCanvas
{
    private void DrawBattle(DrawingContext dc)
    {
        var b = Engine.Battle;
        if (b is null) return;
        Box(dc, View.Inflate(4), 0xFF000000);
        double midX = (b.Player.X + b.Enemy.X) / 2, midY = (b.Player.Y + b.Enemy.Y) / 2;
        double zoom = Math.Clamp(560 / (b.Distance + 280), 0.4, 1.3);
        double cx = View.X + View.Width / 2, cy = View.Y + View.Height / 2;
        Point At(double x, double y) => new(cx + (x - midX) * zoom, cy + (y - midY) * zoom);
        using (dc.PushClip(View))
        {
            // The sea, scrolling under the ships.
            double tile = 64 * zoom;
            double offX = -(midX * zoom % tile) - tile, offY = -(midY * zoom % tile) - tile;
            for (double y = View.Y + offY; y < View.Bottom; y += tile)
            for (double x = View.X + offX; x < View.Right; x += tile)
            {
                int ix = (int)Math.Floor((x - View.X + midX * zoom) / tile), iy = (int)Math.Floor((y - View.Y + midY * zoom) / tile);
                Image(dc, Sprites.GroundBitmap("deep", (Wave + ix + iy) & 7), x, y, tile + 0.5);
            }
            var outlaw = Nations.Get(Nation.Outlaw);
            var foe = Nations.Get(Engine.Foe?.Nation ?? Nation.Valdoro);
            foreach (var (f, info) in new[] { (b.Enemy, foe), (b.Player, outlaw) })
            {
                var p = At(f.X, f.Y);
                DrawShip(dc, p.X, p.Y, f.Heading, f.Length * zoom, info.Colour, info.Trim, f.Ship.Type.Masts, true, f.Reefed ? 0.45 : f.Ship.Sails / 100.0);
            }
            foreach (var ball in b.Balls)
            {
                var p = At(ball.X, ball.Y);
                dc.DrawEllipse(Brush(0xFF1A1A1A), null, p, Math.Max(2, 3 * zoom), Math.Max(2, 3 * zoom));
            }
            foreach (var puff in b.Puffs)
            {
                var p = At(puff.X, puff.Y);
                double a = Math.Clamp(1 - puff.Age / 1.6, 0, 1);
                switch (puff.Kind)
                {
                    case "smoke":
                        dc.DrawEllipse(Brush((uint)(a * 170) << 24 | 0x00E8E8E8), null, p, (6 + puff.Age * 18) * zoom, (6 + puff.Age * 18) * zoom);
                        break;
                    case "splash":
                        dc.DrawEllipse(null, Pen((uint)(a * 230) << 24 | 0x00FFFFFF, 2), p, (3 + puff.Age * 14) * zoom, (3 + puff.Age * 14) * zoom);
                        break;
                    default:
                        dc.DrawEllipse(Brush((uint)(a * 230) << 24 | 0x00FF9030), null, p, (4 + puff.Age * 10) * zoom, (4 + puff.Age * 10) * zoom);
                        break;
                }
            }
            if (b.End != BattleEnd.None)
            {
                string banner = b.End switch
                {
                    BattleEnd.Boarding => "BOARD HER!",
                    BattleEnd.Struck => "SHE STRIKES!",
                    BattleEnd.Sunk => "SUNK!",
                    BattleEnd.Lost => "ABANDON SHIP!",
                    _ => "OUT OF RANGE",
                };
                dc.FillRectangle(Brush(0xA0000000), new Rect(View.X, View.Y + View.Height / 2 - 50, View.Width, 100));
                Shadowed(dc, banner, cx, View.Y + View.Height / 2 - 40, 72, b.End is BattleEnd.Lost ? Bad : Gold, 1);
            }
        }
        Text(dc, $"Range {b.Distance:0} yards", Bar.X + 4, Bar.Y, 22, Ink);
        Text(dc, "Left/Right steer  Up full sail  Down battle sail  Space fire", Bar.Right - 4, Bar.Y, 20, Dim, 2);
        DrawFightBooks(dc, b);
        DrawWind(dc, b.Player.Heading, b.Player.Speed / 26 * 1.0);
        DrawMessages(dc, b.Log.Select(l => (l, Ink)).ToList());
    }

    /// <summary>The two ships' state, and your broadsides' loading.</summary>
    private void DrawFightBooks(DrawingContext dc, SeaBattle b)
    {
        var r = Books;
        Box(dc, r);
        double x = r.X + 14, y = r.Y + 8;
        var me = b.Player;
        Text(dc, $"The {me.Ship.Name}", x, y, 24, Gold);
        y += 28;
        Bar3(dc, x, y + 4, "Hull", me.Ship.Hull, me.Ship.Type.Hull);
        Bar3(dc, x, y + 26, "Sails", me.Ship.Sails, 100);
        y += 50;
        Text(dc, $"Crew {me.Crew}   Guns {me.Ship.Guns}", x, y, 22, Ink);
        y += 26;
        foreach (var (label, t) in new[] { ("Port", me.ReloadPort), ("Starboard", me.ReloadStarboard) })
        {
            double full = SeaBattle.ReloadTime(me);
            Text(dc, label, x, y, 20, t <= 0 ? Good : Dim);
            var bar = new Rect(x + 90, y + 6, 146, 10);
            dc.FillRectangle(Brush(0xFF2A2018), bar);
            dc.FillRectangle(Brush(t <= 0 ? Good : Warn), new Rect(bar.X, bar.Y, bar.Width * (1 - t / full), bar.Height));
            y += 22;
        }
        y += 14;
        var them = b.Enemy;
        Text(dc, Engine.Foe is { } f ? GameEngine.Cap(f.Describe()) : "The enemy", x, y, 20, Bad);
        y += 22;
        Bar3(dc, x, y + 4, "Hull", them.Ship.Hull, them.Ship.Type.Hull);
        Bar3(dc, x, y + 26, "Sails", them.Ship.Sails, 100);
        y += 50;
        Text(dc, $"Crew about {them.Crew / 5 * 5}   Guns {them.Ship.Guns}", x, y, 22, Ink);
    }
}

using Avalonia;
using Avalonia.Media;
using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.World;

namespace AVAPirates.Views;

public sealed partial class GameCanvas
{
    private void DrawPort(DrawingContext dc)
    {
        var port = Engine.InPort;
        var top = Engine.Top;
        Box(dc, View.Inflate(4), 0xFF000000);
        using (dc.PushClip(View))
        {
            var scene = new Rect(View.X, View.Y, View.Width, 360);
            var nation = port is null ? Nation.Valdoro : Engine.NationOf(port);
            switch (top?.Scene ?? "harbour")
            {
                case "governor": DrawGovernor(dc, scene, nation); break;
                case "tavern": DrawTavern(dc, scene); break;
                case "merchant": DrawMarket(dc, scene); break;
                case "shipwright": DrawYard(dc, scene); break;
                default: DrawHarbour(dc, scene, nation, port?.Size ?? 2); break;
            }
            dc.FillRectangle(Brush(0xFF120C08), new Rect(View.X, scene.Bottom, View.Width, View.Bottom - scene.Bottom));
        }
        Text(dc, port?.Name ?? "", Bar.X + 4, Bar.Y, 24, Gold);
        Text(dc, port is null ? "" : $"{Nations.Get(Engine.NationOf(port)).Name}  -  {GameEngine.SizeName(port)}", Bar.Right - 4, Bar.Y, 20, Dim, 2);
        DrawBooks(dc);
        DrawWind(dc, Engine.World.Heading, 0);
        DrawMessages(dc, Engine.Log);
        if (top is not null) DrawPanelFor(dc, top);
    }

    /// <summary>A ship seen from the side, for the scenery: hull, masts and sails.</summary>
    private static void DrawSideShip(DrawingContext dc, double x, double y, double len, uint flag, bool sails = true, uint hull = 0xFF4A2A14)
    {
        Poly(dc, hull, 0xFF1A0E06, new Point(x - len / 2, y - len * 0.12), new Point(x + len / 2, y - len * 0.16), new Point(x + len * 0.4, y), new Point(x - len * 0.42, y));
        dc.FillRectangle(Brush(0xFFC8A060), new Rect(x - len * 0.42, y - len * 0.1, len * 0.82, len * 0.02));
        for (int i = 0; i < 3; i++)
        {
            double mx = x - len * 0.25 + i * len * 0.25, h = len * (i == 1 ? 0.75 : 0.6);
            dc.DrawLine(Pen(0xFF3A2410, Math.Max(1.5, len * 0.015)), new Point(mx, y - len * 0.12), new Point(mx, y - len * 0.12 - h));
            if (sails)
            {
                Poly(dc, 0xFFF0EAD8, 0xFF8A8270, new Point(mx - len * 0.1, y - len * 0.12 - h * 0.92), new Point(mx + len * 0.1, y - len * 0.12 - h * 0.92), new Point(mx + len * 0.11, y - len * 0.12 - h * 0.55), new Point(mx - len * 0.11, y - len * 0.12 - h * 0.55));
                Poly(dc, 0xFFF0EAD8, 0xFF8A8270, new Point(mx - len * 0.12, y - len * 0.12 - h * 0.5), new Point(mx + len * 0.12, y - len * 0.12 - h * 0.5), new Point(mx + len * 0.13, y - len * 0.12 - h * 0.12), new Point(mx - len * 0.13, y - len * 0.12 - h * 0.12));
            }
        }
        dc.FillRectangle(Brush(flag), new Rect(x + len * 0.25, y - len * 0.12 - len * 0.6 - len * 0.08, len * 0.1, len * 0.06));
    }

    private void Sky(DrawingContext dc, Rect r, uint top, uint low)
    {
        for (int i = 0; i < 24; i++)
            dc.FillRectangle(Brush(Sprites.Mix(top, low, i / 24.0)), new Rect(r.X, r.Y + i * r.Height / 24, r.Width, r.Height / 24 + 1));
    }

    private void DrawHarbour(DrawingContext dc, Rect r, Nation n, int size)
    {
        var info = Nations.Get(n);
        Sky(dc, new Rect(r.X, r.Y, r.Width, 220), 0xFF5A9AD8, 0xFFF0E0C0);
        dc.FillRectangle(Brush(0xFF2A6A9A), new Rect(r.X, r.Y + 220, r.Width, r.Height - 220));
        for (int i = 0; i < 16; i++)
            dc.DrawLine(Pen(0x70FFFFFF, 2), new Point(r.X + (i * 47 + Engine.Clock * 12) % r.Width, r.Y + 240 + i % 5 * 22), new Point(r.X + (i * 47 + Engine.Clock * 12) % r.Width + 16, r.Y + 240 + i % 5 * 22));
        // The town climbing the hill, with the fort and its flag.
        Poly(dc, 0xFF6A8A50, null, new Point(r.X + 260, r.Y + 220), new Point(r.X + 420, r.Y + 120), new Point(r.Right, r.Y + 90), new Point(r.Right, r.Y + 220));
        var rng = new Random(n.GetHashCode() * 7 + size);
        for (int i = 0; i < 6 + size * 4; i++)
        {
            double hx = r.X + 300 + rng.Next(380), hy = r.Y + 200 - (hx - r.X - 300) * 0.25 - rng.Next(40);
            dc.FillRectangle(Brush(n == Nation.Outlaw ? 0xFF8A7A60 : 0xFFF0E6D0), new Rect(hx, hy, 34, 22));
            Poly(dc, n == Nation.Outlaw ? 0xFF4A3A2A : 0xFFB04A30, null, new Point(hx - 3, hy), new Point(hx + 17, hy - 14), new Point(hx + 37, hy));
            dc.FillRectangle(Brush(0xFF3A2A1A), new Rect(hx + 12, hy + 10, 8, 12));
        }
        dc.FillRectangle(Brush(0xFF9A9080), new Rect(r.Right - 170, r.Y + 110, 140, 70));
        for (int i = 0; i < 6; i++) dc.FillRectangle(Brush(0xFF9A9080), new Rect(r.Right - 170 + i * 25, r.Y + 98, 14, 14));
        dc.DrawLine(Pen(0xFF3A2A1A, 3), new Point(r.Right - 100, r.Y + 98), new Point(r.Right - 100, r.Y + 40));
        double wave = Math.Sin(Engine.Clock * 3) * 3;
        Poly(dc, info.Colour, null, new Point(r.Right - 100, r.Y + 40), new Point(r.Right - 52, r.Y + 44 + wave), new Point(r.Right - 52, r.Y + 72 + wave), new Point(r.Right - 100, r.Y + 68));
        dc.FillRectangle(Brush(info.Trim), new Rect(r.Right - 100, r.Y + 52, 48, 6));
        // The quay and the ships moored at it.
        dc.FillRectangle(Brush(0xFF7A6A50), new Rect(r.X, r.Y + 214, 300, 16));
        DrawSideShip(dc, r.X + 120, r.Y + 300, 200, Nations.Get(Nation.Outlaw).Colour);
        DrawSideShip(dc, r.X + 440, r.Y + 330, 150, info.Colour, sails: false);
    }

    private static void DrawPerson(DrawingContext dc, double x, double ground, uint coat, double scale = 1)
    {
        double s = scale;
        dc.DrawLine(Pen(0xFF2A2018, 7 * s), new Point(x - 8 * s, ground - 60 * s), new Point(x - 10 * s, ground));
        dc.DrawLine(Pen(0xFF2A2018, 7 * s), new Point(x + 8 * s, ground - 60 * s), new Point(x + 10 * s, ground));
        Poly(dc, coat, 0xFF000000, new Point(x - 20 * s, ground - 50 * s), new Point(x + 20 * s, ground - 50 * s), new Point(x + 14 * s, ground - 120 * s), new Point(x - 14 * s, ground - 120 * s));
        dc.DrawEllipse(Brush(0xFFE0B090), Pen(0xFF6A4A30), new Point(x, ground - 134 * s), 12 * s, 13 * s);
        dc.DrawEllipse(Brush(0xFFF0F0F0), null, new Point(x, ground - 144 * s), 15 * s, 7 * s);
    }

    private void DrawGovernor(DrawingContext dc, Rect r, Nation n)
    {
        var info = Nations.Get(n);
        dc.FillRectangle(Brush(0xFF5A3A24), r);
        for (double x = r.X; x < r.Right; x += 60) dc.DrawLine(Pen(0xFF4A2E1A, 2), new Point(x, r.Y), new Point(x, r.Bottom));
        // A window on the harbour, the crown's banner, and the desk.
        var win = new Rect(r.X + 60, r.Y + 50, 200, 160);
        Sky(dc, new Rect(win.X, win.Y, win.Width, 110), 0xFF6AA8E0, 0xFFE8D8B0);
        dc.FillRectangle(Brush(0xFF2A6A9A), new Rect(win.X, win.Y + 110, win.Width, 50));
        DrawSideShip(dc, win.X + 120, win.Y + 140, 70, info.Colour);
        dc.DrawRectangle(null, Pen(0xFF2A1A0A, 8), win);
        dc.DrawLine(Pen(0xFF2A1A0A, 5), new Point(win.X + win.Width / 2, win.Y), new Point(win.X + win.Width / 2, win.Bottom));
        Poly(dc, info.Colour, 0xFF000000, new Point(r.Right - 200, r.Y + 30), new Point(r.Right - 100, r.Y + 30), new Point(r.Right - 100, r.Y + 200), new Point(r.Right - 150, r.Y + 170), new Point(r.Right - 200, r.Y + 200));
        dc.FillRectangle(Brush(info.Trim), new Rect(r.Right - 200, r.Y + 80, 100, 14));
        DrawPerson(dc, r.X + 420, r.Y + 300, info.Colour, 1.3);
        dc.FillRectangle(Brush(0xFF3A2010), new Rect(r.X + 300, r.Y + 250, 260, 110));
        dc.FillRectangle(Brush(0xFF6A4020), new Rect(r.X + 290, r.Y + 240, 280, 18));
        dc.FillRectangle(Brush(0xFFF0E8D0), new Rect(r.X + 340, r.Y + 232, 50, 10));
    }

    private void DrawTavern(DrawingContext dc, Rect r)
    {
        dc.FillRectangle(Brush(0xFF2A1A10), r);
        for (int i = 0; i < 4; i++)
        {
            double lx = r.X + 90 + i * 170;
            double glow = 0.8 + 0.2 * Math.Sin(Engine.Clock * 5 + i);
            dc.DrawEllipse(Brush((uint)(glow * 70) << 24 | 0x00FFB050), null, new Point(lx, r.Y + 70), 70, 50);
            dc.DrawLine(Pen(0xFF1A1008, 2), new Point(lx, r.Y), new Point(lx, r.Y + 58));
            dc.FillRectangle(Brush(0xFFFFD070), new Rect(lx - 7, r.Y + 58, 14, 18));
        }
        for (int t = 0; t < 3; t++)
        {
            double tx = r.X + 80 + t * 220;
            DrawPerson(dc, tx - 40, r.Y + 330, t == 1 ? 0xFF6A3A2A : 0xFF3A4A6A, 0.9);
            DrawPerson(dc, tx + 90, r.Y + 330, t == 0 ? 0xFF4A6A3A : 0xFF7A5A2A, 0.9);
            dc.FillRectangle(Brush(0xFF5A3418), new Rect(tx - 10, r.Y + 260, 110, 14));
            dc.FillRectangle(Brush(0xFF3A2010), new Rect(tx + 40, r.Y + 274, 10, 60));
            foreach (int m in new[] { 10, 50, 80 })
                dc.FillRectangle(Brush(0xFFC0A060), new Rect(tx + m - 4, r.Y + 248, 10, 13));
        }
        for (int b = 0; b < 3; b++)
        {
            dc.DrawEllipse(Brush(0xFF6A4020), Pen(0xFF2A1808, 2), new Point(r.Right - 60 - b * 54, r.Y + 320), 26, 34);
            dc.DrawLine(Pen(0xFF2A1808, 3), new Point(r.Right - 86 - b * 54, r.Y + 308), new Point(r.Right - 34 - b * 54, r.Y + 308));
        }
    }

    private void DrawMarket(DrawingContext dc, Rect r)
    {
        Sky(dc, new Rect(r.X, r.Y, r.Width, 170), 0xFF6AA8E0, 0xFFF0E0C0);
        dc.FillRectangle(Brush(0xFF9A8A6A), new Rect(r.X, r.Y + 170, r.Width, r.Height - 170));
        // A striped awning over the stall.
        for (int i = 0; i < 12; i++)
            Poly(dc, i % 2 == 0 ? 0xFFC03030u : 0xFFF0E8D0u, null, new Point(r.X + 60 + i * 50, r.Y + 70), new Point(r.X + 110 + i * 50, r.Y + 70), new Point(r.X + 110 + i * 50, r.Y + 120), new Point(r.X + 60 + i * 50, r.Y + 120));
        uint[] goods = [0xFFF0F0E8, 0xFF8A5A2A, 0xFFC04020, 0xFF4A6AC0];
        for (int i = 0; i < 8; i++)
        {
            double cx = r.X + 50 + i * 80, cy = r.Y + 250 + (i % 2) * 40;
            if (i % 3 == 0) dc.DrawEllipse(Brush(0xFF6A4020), Pen(0xFF2A1808, 2), new Point(cx + 30, cy + 20), 28, 36);
            else
            {
                dc.FillRectangle(Brush(0xFF9A7040), new Rect(cx, cy, 64, 52));
                dc.DrawRectangle(null, Pen(0xFF4A3018, 2), new Rect(cx, cy, 64, 52));
                dc.FillRectangle(Brush(goods[i % 4]), new Rect(cx + 6, cy - 10, 52, 12));
            }
        }
        DrawPerson(dc, r.X + 360, r.Y + 220, 0xFF2A7040, 1.0);
    }

    private void DrawYard(DrawingContext dc, Rect r)
    {
        Sky(dc, new Rect(r.X, r.Y, r.Width, 200), 0xFF7AAAD8, 0xFFF0E0C0);
        dc.FillRectangle(Brush(0xFF2A6A9A), new Rect(r.X, r.Y + 200, r.Width, 40));
        dc.FillRectangle(Brush(0xFF8A7A5A), new Rect(r.X, r.Y + 240, r.Width, r.Height - 240));
        // A hull on the slipway, half planked, and timber stacked by it.
        double x0 = r.X + 140, y0 = r.Y + 290;
        for (int i = 0; i < 9; i++)
        {
            double fx = x0 + i * 46;
            double h = 110 - Math.Abs(i - 4) * 12;
            dc.DrawLine(Pen(0xFF6A4020, 6), new Point(fx, y0), new Point(fx - 10, y0 - h));
        }
        Poly(dc, 0xFF5A3418, 0xFF2A1808, new Point(x0 - 30, y0 - 70), new Point(x0 + 200, y0 - 80), new Point(x0 + 200, y0), new Point(x0 + 10, y0));
        dc.FillRectangle(Brush(0xFF4A3A2A), new Rect(x0 - 60, y0, 500, 16));
        for (int i = 0; i < 5; i++) dc.FillRectangle(Brush(0xFF9A7040), new Rect(r.Right - 150, r.Y + 330 - i * 12, 120, 10));
        DrawPerson(dc, r.Right - 200, r.Y + 340, 0xFF8A6A3A, 0.9);
    }
}

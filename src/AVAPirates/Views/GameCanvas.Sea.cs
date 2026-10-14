using Avalonia;
using Avalonia.Media;
using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Views;

public sealed partial class GameCanvas
{
    private const double T = 22;

    private int Wave => (int)(Engine.Clock * 2) % 8;

    private static string GroundKind(Terrain t) => t switch
    {
        Terrain.Deep => "deep",
        Terrain.Shallow => "shallow",
        Terrain.Sand => "sand",
        Terrain.Grass => "grass",
        Terrain.Jungle => "jungle",
        Terrain.Hills => "hills",
        _ => "mountain",
    };

    /// <summary>
    /// A ship seen from above: a hull pointed at the bow, her deck, a yard of sail on each mast (furled
    /// when she's not under way), and her colours at the stern.
    /// </summary>
    public static void DrawShip(DrawingContext dc, double cx, double cy, double heading, double len, uint flag, uint trim, int masts, bool sails, double sailState = 1)
    {
        double w = len / 3.2;
        using (dc.PushTransform(Matrix.CreateRotation(heading * Math.PI / 180) * Matrix.CreateTranslation(cx, cy)))
        {
            // Wake.
            dc.DrawEllipse(Brush(0x40FFFFFF), null, new Point(0, len * 0.55), w * 0.45, len * 0.12);
            Poly(dc, 0xFF6A4020, 0xFF2A1808,
                new Point(0, -len / 2), new Point(w / 2, -len / 4), new Point(w / 2, len * 0.32), new Point(w * 0.42, len / 2),
                new Point(-w * 0.42, len / 2), new Point(-w / 2, len * 0.32), new Point(-w / 2, -len / 4));
            Poly(dc, 0xFFB08850, null,
                new Point(0, -len / 2 + 4), new Point(w / 2 - 2.5, -len / 4 + 1), new Point(w / 2 - 2.5, len * 0.3), new Point(w * 0.38, len / 2 - 2.5),
                new Point(-w * 0.38, len / 2 - 2.5), new Point(-w / 2 + 2.5, len * 0.3), new Point(-w / 2 + 2.5, -len / 4 + 1));
            // Masts and sails.
            for (int i = 0; i < masts; i++)
            {
                double my = masts == 1 ? -len * 0.05 : -len * 0.22 + i * len * 0.44 / (masts - 1);
                double span = w * (sails ? 1.45 : 1.1) * (i == masts - 1 && masts > 1 ? 0.85 : 1);
                if (sails)
                {
                    double depth = Math.Max(2, len * 0.07 * Math.Clamp(sailState, 0.3, 1));
                    Poly(dc, 0xFFF4F0E4, 0xFF8A8478, new Point(-span / 2, my - depth * 0.2), new Point(span / 2, my - depth * 0.2), new Point(span / 2.3, my + depth), new Point(-span / 2.3, my + depth));
                }
                else dc.DrawLine(Pen(0xFFD8D0C0, 2), new Point(-span / 2, my), new Point(span / 2, my));
                dc.DrawEllipse(Brush(0xFF3A2410), null, new Point(0, my), 1.8, 1.8);
            }
            // Colours at the stern.
            dc.FillRectangle(Brush(flag), new Rect(-w * 0.3, len / 2 - 2, w * 0.6, len * 0.12));
            dc.FillRectangle(Brush(trim), new Rect(-w * 0.3, len / 2 - 2 + len * 0.045, w * 0.6, len * 0.03));
        }
    }

    private void DrawSea(DrawingContext dc)
    {
        var w = Engine.World;
        var map = GameEngine.Map;
        Box(dc, View.Inflate(4), 0xFF000000);
        using (dc.PushClip(View))
        {
            if (Engine.ShowChart) DrawChart(dc, View);
            else
            {
                double cx = View.X + View.Width / 2, cy = View.Y + View.Height / 2;
                int x0 = (int)Math.Floor(w.X) - 17, y0 = (int)Math.Floor(w.Y) - 17;
                for (int ty = y0; ty <= y0 + 34; ty++)
                for (int tx = x0; tx <= x0 + 34; tx++)
                {
                    double sx = cx + (tx - w.X) * T, sy = cy + (ty - w.Y) * T;
                    var t = map[tx, ty];
                    bool water = SeaMap.IsWater(t);
                    Image(dc, Sprites.GroundBitmap(GroundKind(t), water ? (Wave + tx) % 8 : (tx * 7 + ty * 13) % 3), sx, sy, T + 0.5);
                }
                foreach (var p in map.Ports)
                {
                    double sx = cx + (p.X + 0.5 - w.X) * T, sy = cy + (p.Y + 0.5 - w.Y) * T;
                    if (sx < View.X - 60 || sx > View.Right + 60 || sy < View.Y - 60 || sy > View.Bottom + 60) continue;
                    DrawTown(dc, sx, sy, p, Engine.World.PortNation[p.Id]);
                }
                foreach (var s in Engine.Traffic.Ships)
                {
                    double sx = cx + (s.X - w.X) * T, sy = cy + (s.Y - w.Y) * T;
                    if (sx < View.X - 40 || sx > View.Right + 40 || sy < View.Y - 40 || sy > View.Bottom + 40) continue;
                    var info = Nations.Get(s.Nation);
                    DrawShip(dc, sx, sy, s.Heading, s.Ship.Type.Length * 2, info.Colour, info.Trim, s.Ship.Type.Masts, true);
                    if (Traffic.Hostile(s, w)) dc.DrawEllipse(null, Pen(0xA0FF4040, 1.5), new Point(sx, sy), 26, 26);
                }
                var home = Nations.Get(Nation.Outlaw);
                DrawShip(dc, cx, cy, w.Heading, w.Flagship.Type.Length * 2.2, home.Colour, home.Trim, w.Flagship.Type.Masts, w.SailsUp, w.Flagship.Sails / 100.0);
                if (Engine.Aground > 0) dc.FillRectangle(Brush(0x50FF2020), View);
            }
        }
        string near = Engine.NearPort(2.2) is { } port ? $"Off {port.Name} - Enter to go ashore" : $"Near {Engine.NearestPortName(w.X, w.Y)}";
        Text(dc, near, Bar.X + 4, Bar.Y, 22, Ink);
        Text(dc, $"{(w.SailsUp ? "Under sail" : "Sails furled")}, {Sailing.PointName(w.Heading, w.WindAngle)}   M chart  Esc menu", Bar.Right - 4, Bar.Y, 20, Dim, 2);
        DrawBooks(dc);
        DrawWind(dc, w.Heading, Engine.Speed);
        DrawMessages(dc, Engine.Log);
        if (Engine.Top is { } top) DrawPanelFor(dc, top);
    }

    /// <summary>A town on the shore: rooftops, a church tower, and a flag in its nation's colours.</summary>
    private static void DrawTown(DrawingContext dc, double x, double y, Port p, Nation n)
    {
        var info = Nations.Get(n);
        uint wall = n == Nation.Outlaw ? 0xFF8A7A60 : 0xFFE8DCC0, roof = n == Nation.Outlaw ? 0xFF4A3A2A : 0xFFB04A30;
        int houses = 2 + p.Size;
        for (int i = 0; i < houses; i++)
        {
            double hx = x - 14 + (i % 3) * 10, hy = y - 8 + (i / 3) * 9;
            dc.FillRectangle(Brush(wall), new Rect(hx, hy, 9, 7));
            Poly(dc, roof, null, new Point(hx - 1, hy), new Point(hx + 4.5, hy - 5), new Point(hx + 10, hy));
        }
        dc.FillRectangle(Brush(0xFF9A9080), new Rect(x + 12, y - 14, 6, 16));
        dc.DrawLine(Pen(0xFF3A2A1A, 1.5), new Point(x + 15, y - 14), new Point(x + 15, y - 28));
        dc.FillRectangle(Brush(info.Colour), new Rect(x + 15, y - 28, 12, 8));
        dc.FillRectangle(Brush(info.Trim), new Rect(x + 15, y - 25, 12, 2));
        Shadowed(dc, p.Name, x, y + 6, 18, Ink, 1);
    }

    /// <summary>The whole chart of the isles: every port in its colours, the fleet, and where a treasure map points.</summary>
    private void DrawChart(DrawingContext dc, Rect r)
    {
        var w = Engine.World;
        var map = GameEngine.Map;
        dc.FillRectangle(Brush(0xFFE8D8B0), r);
        double s = Math.Min(r.Width / SeaMap.Width, r.Height / SeaMap.Height);
        double ox = r.X + (r.Width - s * SeaMap.Width) / 2, oy = r.Y + (r.Height - s * SeaMap.Height) / 2;
        for (int y = 0; y < SeaMap.Height; y++)
        for (int x = 0; x < SeaMap.Width; x++)
        {
            var t = map[x, y];
            uint col = t switch
            {
                Terrain.Deep => 0xFFC8D8D0, Terrain.Shallow => 0xFFB0CCC8, Terrain.Sand => 0xFFD8C080, Terrain.Grass => 0xFF9AB070,
                Terrain.Jungle => 0xFF6A9050, Terrain.Hills => 0xFFA89A68, _ => 0xFF8A7A60,
            };
            dc.FillRectangle(Brush(col), new Rect(ox + x * s, oy + y * s, s + 0.3, s + 0.3));
        }
        if (w.MapPieces > 0 && w.TreasureX >= 0)
        {
            int vague = GameEngine.Vagueness(w.MapPieces);
            // The circle is centred near the spot, not on it: the pieces only narrow it down.
            var c = new Point(ox + (w.TreasureX + 0.5 + (w.TreasureY % 5 - 2) * vague / 4.0) * s, oy + (w.TreasureY + 0.5 + (w.TreasureX % 5 - 2) * vague / 4.0) * s);
            if (vague > 0) dc.DrawEllipse(Brush(0x40C03020), Pen(0xFFC03020, 2), c, vague * s, vague * s);
            else
            {
                var p = new Point(ox + (w.TreasureX + 0.5) * s, oy + (w.TreasureY + 0.5) * s);
                dc.DrawLine(Pen(0xFFC03020, 3), new Point(p.X - 7, p.Y - 7), new Point(p.X + 7, p.Y + 7));
                dc.DrawLine(Pen(0xFFC03020, 3), new Point(p.X - 7, p.Y + 7), new Point(p.X + 7, p.Y - 7));
            }
        }
        foreach (var p in map.Ports)
        {
            var info = Nations.Get(w.PortNation[p.Id]);
            var at = new Point(ox + (p.X + 0.5) * s, oy + (p.Y + 0.5) * s);
            dc.DrawRectangle(Brush(info.Colour), Pen(0xFF000000, 1), new Rect(at.X - 4, at.Y - 4, 8, 8));
            Text(dc, p.Name, at.X, at.Y + 4, 16, 0xFF3A2A1A, 1);
        }
        double pulse = 0.5 + 0.5 * Math.Sin(Engine.Clock * 6);
        dc.DrawEllipse(Brush(Sprites.Mix(0xFF202020, 0xFFFFFFFF, pulse)), Pen(0xFF000000, 1.5), new Point(ox + w.X * s, oy + w.Y * s), 5, 5);
        Text(dc, "The Saltwind Isles", r.X + 14, r.Y + 8, 30, 0xFF5A3A1A);
        double lx = r.X + 14;
        foreach (var n in Nations.All)
        {
            dc.DrawRectangle(Brush(n.Colour), Pen(0xFF000000, 1), new Rect(lx, r.Bottom - 28, 12, 12));
            Text(dc, n.Name, lx + 16, r.Bottom - 32, 18, 0xFF3A2A1A);
            lx += 34 + Measure(n.Name, 18);
        }
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Sea;
using AVAPirates.World;

namespace AVAPirates.Views;

public sealed partial class GameCanvas
{
    // The layout: the view of the sea on the left; the captain's books, the wind and the news on the right.
    private static readonly Rect View = new(16, 16, 704, 704);
    private static readonly Rect Books = new(736, 16, 272, 330);
    private static readonly Rect WindBox = new(736, 354, 272, 140);
    private static readonly Rect Messages = new(736, 502, 272, 250);
    private static readonly Rect Bar = new(16, 726, 704, 26);

    private static readonly Typeface Retro = new(new FontFamily("avares://AVAPirates/Assets/Fonts#VT323"));
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width);
        return p;
    }

    private const uint Ground = 0xFF0A0C12, PanelFill = 0xFF1A140E, Frame = 0xFFB08A48, Ink = 0xFFF0E6D0, Gold = 0xFFFFD860,
        Dim = 0xFF9A8E7A, Good = 0xFF60D080, Warn = 0xFFE0C040, Bad = 0xFFE05040, Blue = 0xFF80C0FF;

    private static FormattedText Fmt(string text, double size, uint colour) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));

    private static double Measure(string text, double size) => Fmt(text, size, Ink).Width;

    /// <summary>Text with its top-left corner at a point (or centred, or right-aligned, on it).</summary>
    private static void Text(DrawingContext dc, string text, double x, double y, double size, uint colour, int align = 0)
    {
        var ft = Fmt(text, size, colour);
        double left = align == 0 ? x : align == 1 ? x - ft.Width / 2 : x - ft.Width;
        dc.DrawText(ft, new Point(left, y));
    }

    private static void Shadowed(DrawingContext dc, string text, double x, double y, double size, uint colour, int align = 0)
    {
        Text(dc, text, x + 2, y + 2, size, 0xC0000000, align);
        Text(dc, text, x, y, size, colour, align);
    }

    private static readonly Dictionary<(string, double, double), List<string>> WrapCache = [];

    /// <summary>Splits text into lines that fit a width.</summary>
    private static List<string> Wrap(string text, double width, double size)
    {
        if (WrapCache.TryGetValue((text, width, size), out var cached)) return cached;
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            string tryLine = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(tryLine, size) > width)
            {
                lines.Add(line);
                line = word;
            }
            else line = tryLine;
        }
        if (line.Length > 0) lines.Add(line);
        if (WrapCache.Count > 3000) WrapCache.Clear();
        WrapCache[(text, width, size)] = lines;
        return lines;
    }

    private static void Box(DrawingContext dc, Rect r, uint fill = PanelFill, uint border = Frame)
    {
        dc.FillRectangle(Brush(fill), r);
        dc.DrawRectangle(Pen(border, 2), r.Deflate(1));
        dc.DrawRectangle(Pen(Sprites.Shade(border, 0.5), 1), r.Deflate(4));
    }

    private static void Image(DrawingContext dc, Bitmap b, double x, double y, double size, double alpha = 1)
    {
        if (alpha < 1)
            using (dc.PushOpacity(alpha))
                dc.DrawImage(b, new Rect(0, 0, 16, 16), new Rect(x, y, size, size));
        else dc.DrawImage(b, new Rect(0, 0, 16, 16), new Rect(x, y, size, size));
    }

    private static void Image(DrawingContext dc, Bitmap b, double x, double y, double size)
    {
        dc.DrawImage(b, new Rect(0, 0, 16, 16), new Rect(x, y, size, size));
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, 1.5) : null, g);
    }

    private void DrawScreen(DrawingContext dc)
    {
        dc.FillRectangle(Brush(Ground), new Rect(0, 0, ScreenWidth, ScreenHeight));
        switch (Engine.Mode)
        {
            case Mode.Title: DrawTitle(dc); break;
            case Mode.Retired: DrawRetired(dc); break;
            case Mode.Battle: DrawBattle(dc); break;
            case Mode.Duel: DrawDuel(dc); break;
            case Mode.Port: DrawPort(dc); break;
            default: DrawSea(dc); break;
        }
        if (Engine.InDemo)
        {
            double pulse = 0.6 + 0.4 * Math.Sin(Engine.Clock * 4);
            var r = new Rect(168, 40, 400, 56);
            dc.FillRectangle(Brush(0xC0000000), r);
            Text(dc, "DEMO - PRESS ANY KEY", 368, 52, 34, Sprites.Shade(Gold, pulse), 1);
        }
    }

    // ───────────────────────────── the side panels ─────────────────────────────

    private void DrawBooks(DrawingContext dc)
    {
        var w = Engine.World;
        var r = Books;
        Box(dc, r);
        double x = r.X + 14, y = r.Y + 8;
        Text(dc, $"Captain {w.Captain}", x, y, 26, Gold);
        y += 28;
        Text(dc, $"{w.Date}   age {w.Age}", x, y, 20, Dim);
        y += 26;
        Text(dc, $"The {w.Flagship.Type.Name.ToLowerInvariant()} {w.Flagship.Name}", x, y, 22, Ink);
        y += 22;
        if (w.Fleet.Count > 1)
        {
            Text(dc, $"and {w.Fleet.Count - 1} more ship{(w.Fleet.Count > 2 ? "s" : "")}", x, y, 20, Dim);
            y += 20;
        }
        var s = w.Flagship;
        Bar3(dc, x, y + 4, "Hull", s.Hull, s.Type.Hull);
        Bar3(dc, x, y + 26, "Sails", s.Sails, 100);
        y += 52;
        Text(dc, $"Guns {w.Guns}   Crew {w.Crew}/{w.CrewSpace}", x, y, 22, Ink);
        y += 24;
        Text(dc, $"Crew are {GameEngine.MoraleWord(w.Morale)}", x, y, 22, w.Morale >= 60 ? Good : w.Morale >= 30 ? Warn : Bad);
        y += 24;
        int days = Engine.FoodDays();
        Text(dc, $"Food for {(days > 365 ? "a long time" : days + " days")}", x, y, 22, days < 10 ? Bad : Ink);
        y += 24;
        Text(dc, $"Plunder {w.Gold}", x, y, 24, Gold);
        Text(dc, $"Banked {w.Wealth}", r.Right - 14, y, 22, Dim, 2);
        y += 26;
        Text(dc, $"Hold {w.CargoUsed}/{w.CargoSpace} tons", x, y, 20, Dim);
        if (w.MapPieces > 0) Text(dc, $"Map {w.MapPieces}/4", r.Right - 14, y, 20, Gold, 2);
    }

    private static void Bar3(DrawingContext dc, double x, double y, string label, int value, int max)
    {
        Text(dc, label, x, y - 4, 20, Dim);
        var r = new Rect(x + 56, y + 2, 180, 12);
        dc.FillRectangle(Brush(0xFF2A2018), r);
        double f = Math.Clamp((double)value / Math.Max(1, max), 0, 1);
        dc.FillRectangle(Brush(f > 0.6 ? Good : f > 0.3 ? Warn : Bad), new Rect(r.X, r.Y, r.Width * f, r.Height));
    }

    /// <summary>The wind: an arrow on a compass rose, with the fleet's heading and how she's sailing.</summary>
    private void DrawWind(DrawingContext dc, double heading, double speed)
    {
        var w = Engine.World;
        var r = WindBox;
        Box(dc, r);
        var c = new Point(r.X + 64, r.Y + 70);
        dc.DrawEllipse(Brush(0xFF241A10), Pen(Frame, 1.5), c, 56, 56);
        for (int i = 0; i < 8; i++)
        {
            var (vx, vy) = Sailing.Vector(i * 45);
            double len = i % 2 == 0 ? 50 : 44;
            dc.DrawLine(Pen(0xFF6A5638, 1), new Point(c.X + vx * 30, c.Y + vy * 30), new Point(c.X + vx * len, c.Y + vy * len));
        }
        Text(dc, "N", c.X, c.Y - 54, 18, Gold, 1);
        // The wind, blowing across the rose.
        var (wx, wy) = Sailing.Vector(w.WindAngle);
        var tail = new Point(c.X - wx * 40, c.Y - wy * 40);
        var head = new Point(c.X + wx * 40, c.Y + wy * 40);
        dc.DrawLine(Pen(0xFF80C0FF, 4), tail, head);
        var (px, py) = (-wy, wx);
        Poly(dc, 0xFF80C0FF, null, head, new Point(head.X - wx * 14 + px * 9, head.Y - wy * 14 + py * 9), new Point(head.X - wx * 14 - px * 9, head.Y - wy * 14 - py * 9));
        // Your heading.
        var (hx, hy) = Sailing.Vector(heading);
        dc.DrawLine(Pen(0xFFFFD860, 3), c, new Point(c.X + hx * 34, c.Y + hy * 34));
        double x = r.X + 132;
        Text(dc, "Wind from the", x, r.Y + 8, 20, Blue);
        Text(dc, Sailing.Compass(w.WindAngle + 180), x, r.Y + 28, 22, Ink);
        Text(dc, "Heading", x, r.Y + 56, 20, Gold);
        Text(dc, Sailing.Compass(heading), x, r.Y + 76, 22, Ink);
        Text(dc, $"{speed * 4:0.0} knots", x, r.Y + 106, 20, Dim);
    }

    private void DrawMessages(DrawingContext dc, IReadOnlyList<(string Text, uint Colour)> log)
    {
        var r = Messages;
        Box(dc, r);
        const double size = 20, lh = 20;
        int room = (int)((r.Height - 16) / lh);
        var lines = new List<(string, uint)>();
        for (int i = log.Count - 1; i >= 0 && lines.Count < room; i--)
        {
            var wrapped = Wrap(log[i].Text, r.Width - 20, size);
            for (int j = wrapped.Count - 1; j >= 0 && lines.Count < room; j--) lines.Add((wrapped[j], i == log.Count - 1 ? log[i].Colour : Sprites.Shade(log[i].Colour, 0.7)));
        }
        lines.Reverse();
        for (int i = 0; i < lines.Count; i++) Text(dc, lines[i].Item1, r.X + 10, r.Y + 8 + i * lh, size, lines[i].Item2);
    }

    /// <summary>Where a panel goes over the play screen, by its kind.</summary>
    private void DrawPanelFor(DrawingContext dc, Game.Panel p)
    {
        switch (p.Kind)
        {
            case PanelKind.Big:
                DrawPanel(dc, View, p);
                return;
            case PanelKind.Port:
                DrawPanel(dc, new Rect(28, 372, 680, 340), p);
                return;
            default:
                DrawPanel(dc, new Rect(60, 400, 616, 300), p);
                return;
        }
    }

    /// <summary>A panel: title, wrapped paragraphs, a typing line or the options (scrolled to keep the choice in view), and the chosen option's note.</summary>
    private void DrawPanel(DrawingContext dc, Rect r, Game.Panel p, bool frame = true)
    {
        if (frame) Box(dc, r);
        double x = r.X + 18, width = r.Width - 36, y = r.Y + 10;
        const double size = 22, lh = 23;
        if (p.Title.Length > 0)
        {
            Text(dc, p.Title, x, y, 28, Gold);
            y += 34;
        }
        double textBottom = p.Entry is not null ? r.Bottom - 48 : r.Bottom - lh * Math.Min(Math.Max(1, p.Options.Count), 6) - 14;
        // The text, from the top, as much as fits.
        var all = p.Text.SelectMany(t => Wrap(t, width, size).Select((l, i) => (Line: l, First: i == 0))).ToList();
        int fit = Math.Max(1, (int)((textBottom - y) / lh));
        var shown = all.Take(fit);
        foreach (var (line, _) in shown)
        {
            uint col = line.StartsWith('>') ? Blue : Ink;
            Text(dc, line, x, y, size, col);
            y += lh;
        }
        if (p.Entry is not null)
        {
            double ey = r.Bottom - 42;
            dc.FillRectangle(Brush(0xFF0E0A06), new Rect(x - 4, ey, width + 8, 32));
            string cursor = Engine.Clock % 1 < 0.5 ? "_" : " ";
            Text(dc, $"{p.Prompt} {p.Entry}{cursor}", x + 4, ey + 2, 26, Gold);
            return;
        }
        y += 6;
        string? note = p.Options.Count > 0 && p.Selected < p.Options.Count ? p.Options[p.Selected].Note : null;
        double noteSpace = note is null ? 0 : lh * Math.Min(2, Wrap(note, width, 20).Count) + 6;
        int room = Math.Max(1, (int)((r.Bottom - 10 - noteSpace - y) / lh));
        int first = Math.Clamp(p.Selected - room / 2, 0, Math.Max(0, p.Options.Count - room));
        for (int i = first; i < Math.Min(p.Options.Count, first + room); i++)
        {
            var o = p.Options[i];
            bool sel = i == p.Selected;
            if (sel) dc.FillRectangle(Brush(0xFF3A2C1A), new Rect(x - 6, y, width + 12, lh));
            uint col = !o.Enabled ? 0xFF6A604E : sel ? Gold : o.Act is null ? Dim : Ink;
            string num = o.Act is null ? "" : i < 9 ? $"{i + 1}. " : "   ";
            Text(dc, num + o.Label, x, y, size, col);
            y += lh;
        }
        if (first > 0) Text(dc, "more above", r.Right - 18, r.Y + 12, 20, Dim, 2);
        if (first + room < p.Options.Count) Text(dc, "more below", r.Right - 18, r.Bottom - 24, 20, Dim, 2);
        if (note is not null)
        {
            double ny = r.Bottom - noteSpace - 4;
            foreach (var line in Wrap(note, width, 20).Take(2))
            {
                Text(dc, line, x, ny, 20, Blue);
                ny += lh;
            }
        }
    }

}

internal static class PanelExtensions
{
    public static T Also<T>(this T value, Action<T> act)
    {
        act(value);
        return value;
    }
}

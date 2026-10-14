using Avalonia;
using Avalonia.Media;
using AVAPirates.Game;
using AVAPirates.Rules;

namespace AVAPirates.Views;

public sealed partial class GameCanvas
{
    private void DrawTitle(DrawingContext dc)
    {
        DrawTitleArt(dc, dusk: false);
        var top = Engine.Top;
        if (top is null) return;
        if (Engine.Panels.Count == 1 && top.Title == "AVAPirates")
        {
            var menu = new Game.Panel().Also(p =>
            {
                foreach (var o in top.Options) p.Options.Add(o);
                p.Selected = top.Selected;
            });
            DrawPanel(dc, new Rect(352, 460, 320, 130), menu);
            Text(dc, "Arrows and Enter to choose.", 512, 700, 22, 0xFFE8D8B0, 1);
            Text(dc, "An original adventure on the high seas. Every isle, port, ship and captain here is new.", 512, 726, 20, 0xFFC8B890, 1);
            return;
        }
        DrawPanel(dc, top.Kind == PanelKind.Big ? new Rect(112, 120, 800, 620) : new Rect(162, 400, 700, 330), top);
    }

    /// <summary>The title: a ship under full sail on an evening sea, the sun going down behind her.</summary>
    private void DrawTitleArt(DrawingContext dc, bool dusk)
    {
        uint top = dusk ? 0xFF2A1A3A : 0xFF3A6AA8, mid = dusk ? 0xFFC05A3A : 0xFFE8A060, low = dusk ? 0xFFF0B060 : 0xFFF8E0A0;
        for (int i = 0; i < 40; i++)
        {
            double t = i / 39.0;
            uint c = t < 0.6 ? Sprites.Mix(top, mid, t / 0.6) : Sprites.Mix(mid, low, (t - 0.6) / 0.4);
            dc.FillRectangle(Brush(c), new Rect(0, i * 10, ScreenWidth, 11));
        }
        dc.DrawEllipse(Brush(dusk ? 0xFFFFD080 : 0xFFFFE8B0), null, new Point(780, 390), 70, 70);
        // The sea, with the sun's path on it.
        for (int i = 0; i < 37; i++)
            dc.FillRectangle(Brush(Sprites.Mix(dusk ? 0xFF3A2A4A : 0xFF2A5A8A, 0xFF0A1A2A, i / 37.0)), new Rect(0, 400 + i * 10, ScreenWidth, 11));
        for (int i = 0; i < 24; i++)
        {
            double y = 405 + i * 14, w = 140 - i * 3;
            double shimmer = Math.Sin(Engine.Clock * 2 + i) * 10;
            dc.FillRectangle(Brush(0x70FFE0A0), new Rect(780 - w / 2 + shimmer, y, w, 3));
        }
        // An island on the horizon, and the ship.
        Poly(dc, dusk ? 0xFF2A1A28 : 0xFF2A3A30, null, new Point(60, 400), new Point(140, 360), new Point(210, 375), new Point(300, 352), new Point(380, 400));
        double bob = Math.Sin(Engine.Clock * 1.5) * 3;
        DrawSideShip(dc, 330, 470 + bob, 300, Nations.Get(Nation.Outlaw).Colour, hull: dusk ? 0xFF2A1A10 : 0xFF3A2010);
        Shadowed(dc, "AVAPIRATES", 512, 60, 112, 0xFFFFE070, 1);
        Shadowed(dc, "A Life on the Saltwind Sea", 512, 172, 36, dusk ? 0xFFF0D0B0 : 0xFF2A1A0A, 1);
    }

    private void DrawRetired(DrawingContext dc)
    {
        DrawTitleArt(dc, dusk: true);
        if (Engine.Top is { } p) DrawPanel(dc, new Rect(112, 250, 800, 260), p);
    }
}

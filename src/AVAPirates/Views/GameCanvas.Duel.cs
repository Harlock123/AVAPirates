using Avalonia;
using Avalonia.Media;
using AVAPirates.Game;
using AVAPirates.Rules;
using AVAPirates.Sea;

namespace AVAPirates.Views;

public sealed partial class GameCanvas
{
    private void DrawDuel(DrawingContext dc)
    {
        var d = Engine.Duel;
        if (d is null) return;
        bool town = Engine.Storming is not null;
        Box(dc, View.Inflate(4), 0xFF000000);
        double deckY = View.Y + 540;
        double Sx(double x) => View.X + 50 + x / 100 * (View.Width - 100);
        using (dc.PushClip(View))
        {
            // Sky and sea (or, storming a town, its walls), then the deck or the street.
            int bands = town ? 45 : 30;
            for (int i = 0; i < bands; i++)
                dc.FillRectangle(Brush(Sprites.Mix(town ? 0xFF2A2A44 : 0xFF6AA8E0, town ? 0xFF8A7A7A : 0xFFE8D8B0, i / (double)bands)), new Rect(View.X, View.Y + i * 12, View.Width, 13));
            if (town)
            {
                for (int i = 0; i < 6; i++)
                {
                    double hx = View.X + 20 + i * 118, h = 120 + (i * 37 % 60);
                    dc.FillRectangle(Brush(0xFFC8B898), new Rect(hx, deckY - 80 - h, 100, h + 80));
                    Poly(dc, 0xFF9A4A30, null, new Point(hx - 6, deckY - 80 - h), new Point(hx + 50, deckY - 130 - h), new Point(hx + 106, deckY - 80 - h));
                    dc.FillRectangle(Brush(0xFF3A2A1A), new Rect(hx + 40, deckY - 50, 20, 50));
                }
                dc.FillRectangle(Brush(0xFF8A8478), new Rect(View.X, deckY, View.Width, View.Bottom - deckY));
            }
            else
            {
                dc.FillRectangle(Brush(0xFF2A6A9A), new Rect(View.X, View.Y + 300, View.Width, 120));
                // The ship's side, between the sea and the deck.
                dc.FillRectangle(Brush(0xFF4A2A14), new Rect(View.X, View.Y + 420, View.Width, deckY - View.Y - 420));
                for (double y = View.Y + 432; y < deckY; y += 14) dc.DrawLine(Pen(0xFF3A2010, 2), new Point(View.X, y), new Point(View.Right, y));
                for (int i = 0; i < 14; i++)
                    dc.DrawLine(Pen(0x80FFFFFF, 2), new Point(View.X + (i * 61 + Engine.Clock * 20) % View.Width, View.Y + 320 + i % 4 * 24), new Point(View.X + (i * 61 + Engine.Clock * 20) % View.Width + 20, View.Y + 320 + i % 4 * 24));
                // Rigging and a mast behind the fighters.
                dc.FillRectangle(Brush(0xFF5A3A1C), new Rect(View.X + View.Width / 2 - 9, View.Y, 18, deckY - View.Y));
                for (int i = 0; i < 7; i++)
                    dc.DrawLine(Pen(0xFF3A2A1A, 1.5), new Point(View.X + View.Width / 2, View.Y + 40 + i * 30), new Point(View.X + 40 + i * 100, deckY - 40));
                for (double x = View.X; x < View.Right; x += 48) Image(dc, Sprites.GroundBitmap("planks"), x, deckY, 48);
                for (double y = deckY + 48; y < View.Bottom; y += 48)
                for (double x = View.X; x < View.Right; x += 48) Image(dc, Sprites.GroundBitmap("planks"), x, y, 48);
                // The rails, at either end of the deck.
                foreach (double rx in new[] { Sx(0), Sx(100) })
                {
                    dc.FillRectangle(Brush(0xFF4A2A10), new Rect(rx - 6, deckY - 70, 12, 80));
                    dc.FillRectangle(Brush(0xFF6A4020), new Rect(rx - 30, deckY - 72, 60, 8));
                }
            }
            var home = Nations.Get(Engine.World.Homeland);
            uint theirCoat = Engine.Foe is { } f ? (f.Nation == Nation.Outlaw ? 0xFF3A3A3A : Nations.Get(f.Nation).Colour) : town ? 0xFFB02020 : 0xFF8A2020;
            DrawFencer(dc, Sx(d.Player.X), deckY, 1, d.Player, 0xFF2A4A8A, home.Colour);
            DrawFencer(dc, Sx(d.Enemy.X), deckY, -1, d.Enemy, theirCoat, 0xFFE0E0E0);
            // Hearts.
            HeartBar(dc, View.X + 20, View.Y + 16, $"Captain {Engine.World.Captain}", d.Player.Morale, 0xFF60A0FF);
            HeartBar(dc, View.Right - 300, View.Y + 16, GameEngine.Cap(d.EnemyName), d.Enemy.Morale, 0xFFFF6050);
            if (d.End != DuelEnd.None)
            {
                bool won = d.End == DuelEnd.Won;
                string why = (won ? d.Enemy.X > 98 : d.Player.X < 2) ? "over the rail!" : won ? "yields!" : "you yield!";
                dc.FillRectangle(Brush(0xA0000000), new Rect(View.X, View.Y + 200, View.Width, 110));
                Shadowed(dc, won ? "VICTORY" : "DEFEATED", View.X + View.Width / 2, View.Y + 206, 72, won ? Gold : Bad, 1);
                Shadowed(dc, why, View.X + View.Width / 2, View.Y + 272, 28, Ink, 1);
            }
        }
        Text(dc, "Hold Up: high guard   Down: low guard   Left/Right step   Space lunge", Bar.X + 4, Bar.Y, 20, Dim);
        DrawBooks(dc);
        var r = WindBox;
        Box(dc, r);
        Text(dc, "The swordplay", r.X + 14, r.Y + 8, 24, Gold);
        foreach (var (line, i) in Wrap("A lunge at the height they guard is parried. Find the height they aren't guarding, and drive them to the rail.", r.Width - 28, 19).Select((l, i) => (l, i)).Take(5))
            Text(dc, line, r.X + 14, r.Y + 36 + i * 19, 19, Ink);
        DrawMessages(dc, Engine.Log);
    }

    private static void HeartBar(DrawingContext dc, double x, double y, string who, double morale, uint colour)
    {
        dc.FillRectangle(Brush(0xA0000000), new Rect(x - 6, y - 4, 292, 50));
        Text(dc, who, x, y, 22, Ink);
        var r = new Rect(x, y + 26, 280, 12);
        dc.FillRectangle(Brush(0xFF2A2018), r);
        dc.FillRectangle(Brush(colour), new Rect(r.X, r.Y, r.Width * Math.Clamp(morale / 100, 0, 1), r.Height));
    }

    /// <summary>
    /// A swordsman from the side, standing on a line: legs, coat, head and hat, and the sword arm held
    /// at the guard's height - thrust out on a lunge, flung back when struck.
    /// </summary>
    private void DrawFencer(DrawingContext dc, double x, double ground, int facing, Fencer f, uint coat, uint sash)
    {
        double lean = f.Stance switch
        {
            Stance.Lunging => Math.Clamp(1 - f.Timer / Math.Max(0.01, f.Windup), 0, 1) * 16,
            Stance.Staggered => -14,
            _ => 0,
        };
        double hipX = x, hipY = ground - 62;
        double shX = x + facing * lean, shY = ground - 122;
        // Legs, in a fencer's stance.
        dc.DrawLine(Pen(0xFF2A2018, 7), new Point(hipX, hipY), new Point(hipX + facing * 22, ground));
        dc.DrawLine(Pen(0xFF2A2018, 7), new Point(hipX, hipY), new Point(hipX - facing * 18, ground));
        dc.FillRectangle(Brush(0xFF1A1208), new Rect(hipX + facing * 22 - 8, ground - 6, 16, 7));
        dc.FillRectangle(Brush(0xFF1A1208), new Rect(hipX - facing * 18 - 8, ground - 6, 16, 7));
        // Coat and sash.
        Poly(dc, coat, 0xFF000000, new Point(hipX - 16, hipY + 10), new Point(hipX + 16, hipY + 10), new Point(shX + 13, shY), new Point(shX - 13, shY));
        dc.DrawLine(Pen(sash, 5), new Point((hipX + shX) / 2 - 14, hipY - 18), new Point((hipX + shX) / 2 + 14, hipY - 10));
        // Head and hat.
        var head = new Point(shX + facing * 3, shY - 16);
        dc.DrawEllipse(Brush(0xFFE0B090), Pen(0xFF6A4A30), head, 12, 13);
        Poly(dc, 0xFF1A1A1A, null, new Point(head.X - 20, head.Y - 8), new Point(head.X + 20, head.Y - 8), new Point(head.X + 8, head.Y - 24), new Point(head.X - 8, head.Y - 24));
        dc.FillRectangle(Brush(0xFF000000), new Rect(head.X + facing * 4 - 1.5, head.Y - 3, 3, 3));
        // The sword arm, at the guard's height (or the lunge's), and the blade.
        var height = f.Stance == Stance.Lunging ? f.Aim : f.Guard;
        double ang = height switch { Sea.Height.High => -38, Sea.Height.Low => 34, _ => -4 };
        if (f.Stance == Stance.Staggered) ang -= 40;
        double reach = 30 + (f.Stance == Stance.Lunging ? lean * 1.4 : 0);
        double rad = ang * Math.PI / 180;
        var shoulder = new Point(shX, shY + 10);
        var hand = new Point(shoulder.X + facing * Math.Cos(rad) * reach, shoulder.Y + Math.Sin(rad) * reach);
        dc.DrawLine(Pen(coat, 7), shoulder, hand);
        var tip = new Point(hand.X + facing * Math.Cos(rad) * 74, hand.Y + Math.Sin(rad) * 74);
        dc.DrawLine(Pen(0xFFE8E8F0, 3), hand, tip);
        dc.DrawLine(Pen(0xFFD0A030, 4), new Point(hand.X - Math.Sin(rad) * 8, hand.Y - Math.Cos(rad) * 8 * facing), new Point(hand.X + Math.Sin(rad) * 8, hand.Y + Math.Cos(rad) * 8 * facing));
        dc.DrawLine(Pen(coat, 6), shoulder, new Point(shoulder.X - facing * 18, shoulder.Y + 22));
    }
}

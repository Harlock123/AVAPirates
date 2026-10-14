using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AVAPirates.Views;

/// <summary>
/// The ground and water of the isles, painted as small squares of pixels (made once, then kept as
/// bitmaps and drawn scaled up): sea whose wave crests drift with the frame, shallows, beaches,
/// grass, jungle, hills and peaks. All original.
/// </summary>
public static class Sprites
{
    public static uint Shade(uint argb, double f)
    {
        uint r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        uint S(uint v) => (uint)Math.Clamp(f >= 1 ? v + (255 - v) * (f - 1) : v * f, 0, 255);
        return (argb & 0xFF000000) | (S(r) << 16) | (S(g) << 8) | S(b);
    }

    public static uint Mix(uint a, uint b, double t)
    {
        uint L(int shift) => (uint)Math.Clamp(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t, 0, 255) << shift;
        return 0xFF000000 | L(16) | L(8) | L(0);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        return h ^ (h >> 15);
    }

    private static void Disc(uint[] px, double cx, double cy, double r, uint col, uint light)
    {
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
            if (dx * dx + dy * dy > r * r) continue;
            px[y * 16 + x] = dx + dy < -r * 0.5 ? light : col;
        }
    }

    /// <summary>A square of the isles. For water the frame moves the waves; for land it picks a variant.</summary>
    public static uint[] Ground(string kind, int frame)
    {
        var px = new uint[256];
        int v = frame % 3;
        void Base(uint a, uint b, uint c, int salt)
        {
            for (int i = 0; i < 256; i++)
            {
                uint h = Hash(i % 16, i / 16, salt) % 100;
                px[i] = h < 10 ? b : h > 92 ? c : a;
            }
        }
        switch (kind)
        {
            case "deep" or "shallow":
            {
                (uint a, uint b, uint wave) = kind == "deep" ? (0xFF1E5A9Au, 0xFF1A5290u, 0xFF5E9AD8u) : (0xFF3A9AC0u, 0xFF3490B6u, 0xFF9AD8F0u);
                Base(a, b, a, 8);
                foreach (var (x, y) in new[] { (2, 3), (9, 2), (5, 8), (12, 7), (1, 12), (9, 13) })
                    for (int i = 0; i < 3; i++)
                        px[y * 16 + (x + i + frame * 2) % 16] = wave;
                break;
            }
            case "sand":
                Base(0xFFE8D498, 0xFFD8C080, 0xFFF4E4B0, 7 + v * 31);
                break;
            case "grass":
                Base(0xFF5AA040, 0xFF4A8A34, 0xFF72B850, 1 + v * 31);
                break;
            case "jungle":
                Base(0xFF2E6A2A, 0xFF22561E, 0xFF3A7A32, 3 + v * 31);
                foreach (var (cx, cy, r) in v switch
                {
                    0 => new[] { (4.5, 4.5, 4.0), (12.0, 4.0, 3.6), (8.5, 11.0, 4.4) },
                    1 => new[] { (3.5, 10.5, 3.8), (11.0, 9.5, 4.2), (7.0, 3.0, 3.2) },
                    _ => new[] { (5.0, 7.0, 4.4), (12.5, 12.5, 3.4), (12.0, 3.0, 3.0) },
                })
                    Disc(px, cx, cy, r, 0xFF2E8A34, 0xFF52B04A);
                break;
            case "hills":
                Base(0xFF5AA040, 0xFF4A8A34, 0xFF72B850, 4 + v * 31);
                foreach (var (cx, cy, r) in v switch { 0 => new[] { (5.0, 9.0, 5.0), (11.5, 11.0, 4.5) }, 1 => new[] { (8.0, 8.0, 6.0) }, _ => new[] { (4.0, 12.0, 4.0), (11.0, 7.0, 5.0) } })
                    for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                        if (dy > 1.5 || dx * dx / (r * r) + dy * dy / (r * r * 0.45) > 1) continue;
                        px[y * 16 + x] = dx < -r * 0.3 ? 0xFF9ABA68 : dx > r * 0.35 ? 0xFF5A8A3C : 0xFF7AA650;
                    }
                break;
            case "mountain":
                Base(0xFF6A6058, 0xFF5A524A, 0xFF7A7068, 5 + v * 31);
                foreach (var (peak, w, h0) in v switch { 0 => new[] { (5.0, 6.0, 1.0), (11.0, 6.5, 3.0) }, 1 => new[] { (8.0, 8.5, 0.0) }, _ => new[] { (11.0, 6.0, 1.0), (4.0, 5.5, 4.0) } })
                    for (int y = (int)h0; y < 16; y++)
                    {
                        double half = (y - h0) / (15 - h0) * w;
                        for (int x = 0; x < 16; x++)
                        {
                            double dx = x + 0.5 - peak;
                            if (Math.Abs(dx) > half) continue;
                            bool top = y < h0 + 3 && Math.Abs(dx) < half * 0.9;
                            px[y * 16 + x] = top ? (dx < 0 ? 0xFFE8E4DC : 0xFFC0BCB4) : dx < 0 ? 0xFFA0968A : 0xFF706658;
                        }
                    }
                break;
            case "planks":
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool seam = y % 4 == 3 || (x + (y / 4) * 6) % 16 == 0;
                    px[y * 16 + x] = seam ? 0xFF5A3A1C : Hash(x, y, 10) % 9 == 0 ? 0xFF8E6436 : 0xFF9A6E3E;
                }
                break;
        }
        return px;
    }

    private static readonly Dictionary<(string, int), Bitmap> Cache = [];

    public static Bitmap Bitmap(uint[] px)
    {
        var wb = new WriteableBitmap(new PixelSize(16, 16), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = wb.Lock())
        {
            unsafe
            {
                for (int y = 0; y < 16; y++)
                    px.AsSpan(y * 16, 16).CopyTo(new Span<uint>((void*)(fb.Address + y * fb.RowBytes), 16));
            }
        }
        return wb;
    }

    public static Bitmap GroundBitmap(string kind, int frame = 0)
    {
        if (!Cache.TryGetValue((kind, frame), out var b)) Cache[(kind, frame)] = b = Bitmap(Ground(kind, frame));
        return b;
    }
}

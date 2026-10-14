using AVAPirates.Rules;

namespace AVAPirates.World;

public enum Terrain : byte { Deep, Shallow, Sand, Grass, Jungle, Hills, Mountain }

/// <summary>
/// A port of the isles as the map first has it: who holds it, how big and rich it is, and what it
/// grows and wants. (What happens to it in a career - changing hands, being sacked - is kept in the
/// career's own state.)
/// </summary>
public sealed class Port
{
    public int Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The town itself, on the shore.</summary>
    public int X { get; init; }
    public int Y { get; init; }
    /// <summary>The water by the quay, where ships moor.</summary>
    public int DockX { get; init; }
    public int DockY { get; init; }
    public Nation Nation { get; set; }
    /// <summary>1 a settlement, 2 a town, 3 a city.</summary>
    public int Size { get; set; }
    public int Wealth { get; set; }
    public int Soldiers { get; set; }
    public Good Grows { get; init; }
    public Good Wants { get; init; }
}

/// <summary>
/// The Saltwind Isles: a sea of islands, large and small, grown from a fixed seed so that they are
/// the same for everyone. Every port's quay can be sailed to from every other.
/// </summary>
public sealed class SeaMap
{
    public const int Width = 160, Height = 110;

    private readonly Terrain[,] _cells = new Terrain[Width, Height];
    public List<Port> Ports { get; } = [];
    public int Seed { get; private set; }

    private static readonly Lazy<SeaMap> Shared = new(Build);
    public static SeaMap Instance => Shared.Value;

    public Terrain this[int x, int y] => x < 0 || y < 0 || x >= Width || y >= Height ? Terrain.Deep : _cells[x, y];

    public static bool IsWater(Terrain t) => t is Terrain.Deep or Terrain.Shallow;
    public bool Water(int x, int y) => IsWater(this[x, y]);
    /// <summary>Open water at a point; the edge of the chart counts as shore, so nobody sails off it.</summary>
    public bool Water(double x, double y) => x >= 1 && y >= 1 && x < Width - 1 && y < Height - 1 && Water((int)Math.Floor(x), (int)Math.Floor(y));

    public Port? PortAt(int x, int y) => Ports.FirstOrDefault(p => p.X == x && p.Y == y);

    private static readonly string[] Names =
    [
        "Port Saltire", "Calderra", "Velasquine", "San Orvino", "Mirabel", "Fort Halloway", "Greyhaven", "Corval", "Tamarind",
        "Puerto Dolce", "Santa Lumen", "Bellmere", "Isla Brava", "Kettlebay", "Arenhoek", "Vries Landing", "Marisol", "Old Cassock",
        "Port Anselm", "Sabrecove", "Rook's Rest", "Dunmarrow", "Velloran", "Cayo Fresco", "Hallow Key", "Gullstone",
    ];

    public static SeaMap Build()
    {
        for (int seed = 3; ; seed++)
        {
            var m = new SeaMap();
            if (m.TryGenerate(seed)) return m;
        }
    }

    private static double Lattice(int seed, int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519u);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (double)0xFFFFFF;
    }

    private static double Value(int seed, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double a = Lattice(seed, x0, y0), b = Lattice(seed, x0 + 1, y0), c = Lattice(seed, x0, y0 + 1), d = Lattice(seed, x0 + 1, y0 + 1);
        return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
    }

    private static double Fbm(int seed, double x, double y, int octaves)
    {
        double sum = 0, amp = 1, norm = 0, f = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Value(seed + i * 101, x * f, y * f) * amp;
            norm += amp;
            amp *= 0.5;
            f *= 2;
        }
        return sum / norm;
    }

    private bool TryGenerate(int seed)
    {
        Seed = seed;
        Ports.Clear();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double nx = x / (double)Width - 0.5, ny = y / (double)Height - 0.5;
            double edge = Math.Max(Math.Abs(nx) * 2, Math.Abs(ny) * 2);
            double e = Fbm(seed, x / 18.0, y / 18.0, 5) - Math.Max(0, edge - 0.8) * 1.5;
            double m = Fbm(seed + 555, x / 12.0, y / 12.0, 3);
            _cells[x, y] = e switch
            {
                < 0.52 => Terrain.Deep,
                < 0.575 => Terrain.Shallow,
                < 0.60 => Terrain.Sand,
                > 0.76 => Terrain.Mountain,
                > 0.70 => Terrain.Hills,
                _ => m > 0.52 ? Terrain.Jungle : Terrain.Grass,
            };
            if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1) _cells[x, y] = Terrain.Deep;
        }
        int land = 0;
        foreach (var t in _cells) if (!IsWater(t)) land++;
        if (land < Width * Height / 6 || land > Width * Height / 3) return false;

        // The open sea: water joined to the edge of the map.
        var sea = Region((x, y) => Water(x, y), 0, 0);
        // Ports: on the shore, beside the open sea, spread out over the isles.
        var rng = new Rng((ulong)seed * 7919 + 1);
        var spots = new List<(int X, int Y, int DX, int DY)>();
        for (int y = 2; y < Height - 2; y++)
        for (int x = 2; x < Width - 2; x++)
        {
            if (Water(x, y) || this[x, y] == Terrain.Mountain) continue;
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
                if (sea.Contains((x + dx, y + dy)))
                {
                    spots.Add((x, y, x + dx, y + dy));
                    break;
                }
        }
        var names = Names.ToList();
        for (int tries = 0; tries < 4000 && Ports.Count < 24; tries++)
        {
            var s = spots[rng.Next(spots.Count)];
            if (Ports.Any(p => Math.Abs(p.X - s.X) + Math.Abs(p.Y - s.Y) < 16)) continue;
            int at = rng.Next(names.Count);
            var grows = (Good)rng.Next(4);
            var wants = (Good)(((int)grows + 1 + rng.Next(3)) % 4);
            Ports.Add(new Port { Id = Ports.Count, Name = names[at], X = s.X, Y = s.Y, DockX = s.DX, DockY = s.DY, Grows = grows, Wants = wants });
            names.RemoveAt(at);
        }
        if (Ports.Count < 22) return false;
        AssignNations(rng);
        return true;
    }

    /// <summary>
    /// Shares the ports out among the crowns: Valdoro holds the south and west, Fennick the north,
    /// Lusande the east, Brannmark a handful in between, and the Brotherhood two out-of-the-way havens.
    /// </summary>
    private void AssignNations(Rng rng)
    {
        // The two most remote ports become the Brotherhood's havens.
        var havens = Ports.OrderByDescending(p => Ports.Where(o => o != p).Min(o => Math.Abs(o.X - p.X) + Math.Abs(o.Y - p.Y))).Take(2).ToHashSet();
        foreach (var p in havens) p.Nation = Nation.Outlaw;
        // Then each smaller crown takes the ports nearest its home waters, and Valdoro all the rest.
        var left = Ports.Where(p => !havens.Contains(p)).ToList();
        foreach (var (n, fx, fy, quota) in new[] { (Nation.Lusande, 0.85, 0.45, 5), (Nation.Fennick, 0.3, 0.12, 5), (Nation.Brannmark, 0.55, 0.45, 3) })
            foreach (var p in left.OrderBy(p => Math.Pow(p.X / (double)Width - fx, 2) + Math.Pow(p.Y / (double)Height - fy, 2)).Take(quota).ToList())
            {
                p.Nation = n;
                left.Remove(p);
            }
        foreach (var p in left) p.Nation = Nation.Valdoro;
        foreach (var p in Ports)
        {
            p.Size = p.Nation == Nation.Outlaw ? 1 : 1 + rng.Next(3);
            if (p.Nation == Nation.Valdoro && rng.Chance(40)) p.Size = Math.Min(3, p.Size + 1);
            p.Wealth = p.Size * (800 + rng.Next(800)) * (p.Nation == Nation.Valdoro ? 2 : 1);
            p.Soldiers = p.Nation == Nation.Outlaw ? 40 : p.Size * 60 + rng.Next(60);
        }
    }

    /// <summary>Every square joined to a start square through squares that pass a test.</summary>
    public HashSet<(int X, int Y)> Region(Func<int, int, bool> pass, int sx, int sy)
    {
        var seen = new HashSet<(int, int)>();
        if (!pass(sx, sy)) return seen;
        var q = new Queue<(int, int)>();
        q.Enqueue((sx, sy));
        seen.Add((sx, sy));
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height || seen.Contains((nx, ny)) || !pass(nx, ny)) continue;
                seen.Add((nx, ny));
                q.Enqueue((nx, ny));
            }
        }
        return seen;
    }

    /// <summary>
    /// A way across the water from one square to another, as squares (eight ways, keeping off the
    /// shore where it can); empty if there is none.
    /// </summary>
    public List<(int X, int Y)> Route((int X, int Y) from, (int X, int Y) to)
    {
        var dist = new Dictionary<(int, int), double> { [from] = 0 };
        var prev = new Dictionary<(int, int), (int, int)>();
        var open = new PriorityQueue<(int X, int Y), double>();
        open.Enqueue(from, 0);
        while (open.TryDequeue(out var cur, out _))
        {
            if (cur == to) break;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if ((dx | dy) == 0) continue;
                var n = (X: cur.X + dx, Y: cur.Y + dy);
                if (!Water(n.X, n.Y) || n.X <= 0 || n.Y <= 0 || n.X >= Width - 1 || n.Y >= Height - 1) continue;
                if (dx != 0 && dy != 0 && (!Water(cur.X + dx, cur.Y) || !Water(cur.X, cur.Y + dy))) continue;
                double step = (dx != 0 && dy != 0 ? 1.414 : 1) + (this[n.X, n.Y] == Terrain.Shallow ? 0.6 : 0);
                double d = dist[cur] + step;
                if (dist.TryGetValue(n, out var old) && old <= d) continue;
                dist[n] = d;
                prev[n] = cur;
                open.Enqueue(n, d + Math.Max(Math.Abs(to.X - n.X), Math.Abs(to.Y - n.Y)));
            }
        }
        if (!prev.ContainsKey(to)) return [];
        var path = new List<(int, int)> { to };
        var at = to;
        while (at != from)
        {
            at = prev[at];
            path.Add(at);
        }
        path.Reverse();
        return path;
    }

    public string Dump()
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char c = this[x, y] switch
                {
                    Terrain.Deep => ' ', Terrain.Shallow => '~', Terrain.Sand => ':', Terrain.Grass => '.', Terrain.Jungle => 'j',
                    Terrain.Hills => 'h', _ => 'M',
                };
                if (PortAt(x, y) is { } p) c = p.Nation switch { Nation.Valdoro => 'V', Nation.Fennick => 'F', Nation.Lusande => 'L', Nation.Brannmark => 'B', _ => 'X' };
                sb.Append(c);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

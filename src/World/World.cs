using System.IO.Compression;
using System.Numerics;
namespace UntitledGame;

public sealed class Pool { public int X0, X1, Row, Bottom, Id; public float Surf; public Water Kind; public Col Shallow, Deep; public int Flooded; }
public sealed class Lamp { public float X, Y, R, Flick; public Col C; public bool Off; }
public sealed class Prop { public string S; public float X, Y; public int Layer, Kind, Idx, GX = -1, GY; public bool Flip, Gone; public float Phase, Bend, Fall = -1, FallDir; public Lamp Lamp; public float Scale = 1; }
public sealed class Seg { public int X0, X1; public Biome B; }

/// A world grown from a seed: twenty biomes, caves, lakes, waterfalls and floating islands, bordered by sea.
/// What the player digs and builds is kept as a list of changes on top of the grown world.
public sealed class World
{
    public const int W = 2400, H = 256, TS = 24, SeaRow = 104, GenVer = 1; public const float SeaY = SeaRow * TS + 6;
    public static World Cur;
    public readonly int Seed; readonly int S; readonly Rng rng;
    public readonly T[] Tile = new T[W * H], Wall = new T[W * H]; public readonly byte[] Climb = new byte[W * H], Wet = new byte[W * H]; public readonly bool[] Turf = new bool[W * H]; public readonly float[] Light = new float[W * H];
    public readonly int[] Top = new int[W], Top0 = new int[W], Sky = new int[W]; readonly byte[] under = new byte[(W / 4) * (H / 4)]; readonly bool[] lake = new bool[W];
    public readonly List<Seg> Segs = new(); public readonly List<Pool> Pools = new(); public readonly List<Prop> Props = new(); public readonly List<Lamp> Lamps = new(); public readonly List<(int x, int y0, int y1)> Falls = new();
    public Vector2 Spawn; public int SpawnTile; readonly Dictionary<int, List<Prop>> rooted = new();
    public readonly Dictionary<int, byte> Edits = new(); public readonly HashSet<int> GoneProps = new();
    readonly Col[] grassCol = new Col[W];

    public World(int seed, string edits = null, IEnumerable<int> gone = null) { Seed = seed; rng = new Rng(seed); S = (int)(new Rng(seed ^ 0x5bd1e995).Next() % 1000000); Gen(edits, gone); }

    // ---------------------------------------------------------------- reading
    public T At(int x, int y) => x < 0 || x >= W ? T.Heartstone : y < 0 ? T.Air : y >= H ? T.Heartstone : Tile[y * W + x];
    public bool Solid(int x, int y) => At(x, y) != T.Air;
    public byte ClimbAt(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? Climb[y * W + x] : (byte)0;
    public T WallAt(int x, int y) => x < 0 || x >= W || y < 0 ? T.Air : y >= H ? T.Stone : Wall[y * W + x];
    public bool TurfAt(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Turf[y * W + x];
    public Pool PoolAt(float x, float y) { int tx = (int)MathF.Floor(x / TS), ty = (int)MathF.Floor(y / TS); if (tx < 0 || ty < 0 || tx >= W || ty >= H) return null; int id = Wet[ty * W + tx]; if (id == 0) return null; var p = Pools[id - 1]; return y > p.Surf ? p : null; }
    public bool WetTile(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Wet[y * W + x] != 0;
    public Seg SegAt(float tx) { foreach (var s in Segs) if (tx < s.X1) return s; return Segs[^1]; }
    public Biome BiomeAt(float tx) => SegAt(tx).B;
    public Biome UnderAt(int x, int y) { if (x < 0 || y < 0 || x >= W || y >= H) return Biome.Meadow; int b = under[(y / 4) * (W / 4) + x / 4]; return b == 0 ? BiomeAt(x) : (Biome)b; }
    /// The biome you are really in: underground ones only count when you are inside the earth.
    public Biome Here(int x, int y, bool cave) => cave && UnderAt(x, y) is var u && Biomes.Of(u).Under ? u : BiomeAt(x);
    public (Biome a, Biome b, float t) Mix(float tx)
    {
        const float R = 22; for (int i = 0; i < Segs.Count; i++) { var s = Segs[i]; if (tx >= s.X1) continue; if (i + 1 < Segs.Count && s.X1 - tx < R) return (s.B, Segs[i + 1].B, .5f * M.Smooth(R, 0, s.X1 - tx)); if (i > 0 && tx - s.X0 < R) return (Segs[i - 1].B, s.B, .5f + .5f * M.Smooth(0, R, tx - s.X0)); return (s.B, s.B, 0); }
        return (Segs[^1].B, Segs[^1].B, 0);
    }
    public static Col GrassCol(float tx) { var w = Cur; if (w == null) return 0x86b050; return w.grassCol[Math.Clamp((int)tx, 0, W - 1)]; }
    public float FloorAt(int x, int y) { while (y < H - 1 && !Solid(x, y)) y++; return y * TS; }
    /// Nearest standing height around y: up out of solid ground, or down to it.
    public float GroundNear(float px, float py) { int x = Math.Clamp((int)MathF.Floor(px / TS), 0, W - 1), y = Math.Clamp((int)MathF.Floor((py - 6) / TS), 1, H - 2); int k = 0; if (Solid(x, y)) { while (y > 1 && Solid(x, y) && k++ < 40) y--; return (y + 1) * TS; } while (y < H - 1 && !Solid(x, y)) y++; return y * TS; }
    float N(float x, float y, int s) => M.Noise(x, y, S + s); float F(float x, float y, int o, int s) => M.Fbm(x, y, o, S + s);

    // ---------------------------------------------------------------- growing
    void Gen(string edits, IEnumerable<int> gone)
    {
        Layout(); Heights(); Lakes(); Fill(); Caves(); Islands(); Waters(); CavePools(); Grass(); Spot(); Decorate();
        Array.Copy(Top, Top0, W); if (gone != null) foreach (int i in gone) if (i >= 0 && i < Props.Count) Drop(Props[i]); LoadEdits(edits);
        foreach (int i in Enumerable.Range(0, W)) { int y = 0; while (y < H && Tile[y * W + i] == T.Air) y++; Top[i] = y; } for (int x = 0; x < W; x++) Sky[x] = SkyOf(x);
        LightAll(); for (int i = 0; i < 4000 && flow.Count > 0; i++) FlowStep(999); flow.Clear();
    }
    void Layout()
    {
        var order = Enumerable.Range(0, Biomes.Surface).Select(i => (Biome)i).Where(b => b != Biome.Coast).ToArray(); Biome[] best;
        // neighbours should make sense together: start from a random order and keep the swaps that make the climate gentler
        float Cost(Biome[] o) { float s = MathF.Abs(Array.IndexOf(o, Biome.Meadow) - 7) * .06f, cst = Biomes.Of(Biome.Coast).Heat; for (int i = 0; i <= o.Length; i++) { float a = i == 0 ? cst : Biomes.Of(o[i - 1]).Heat, b = i == o.Length ? cst : Biomes.Of(o[i]).Heat, d = MathF.Abs(a - b); s += d * d * (i == 0 || i == o.Length ? .35f : 2); } return s; }
        for (int i = order.Length - 1; i > 0; i--) { int j = rng.I(i + 1); (order[i], order[j]) = (order[j], order[i]); } float cost = Cost(order); for (int k = 0; k < 130; k++) { int a = rng.I(order.Length), b = rng.I(order.Length); (order[a], order[b]) = (order[b], order[a]); float c = Cost(order); if (c <= cost) cost = c; else (order[a], order[b]) = (order[b], order[a]); } best = order;
        Segs.Add(new Seg { X0 = 0, X1 = 190, B = Biome.Coast }); var wt = best.Select(_ => rng.R(.8f, 1.25f)).ToArray(); float sum = wt.Sum(), x = 190; for (int i = 0; i < best.Length; i++) { float w = wt[i] / sum * 2020; Segs.Add(new Seg { X0 = (int)x, X1 = i == best.Length - 1 ? 2210 : (int)(x + w), B = best[i] }); x = Segs[^1].X1; }
        Segs.Add(new Seg { X0 = 2210, X1 = W, B = Biome.Coast });
        for (int i = 0; i < W; i++) { var (a, b, t) = Mix(i); grassCol[i] = Col.Lerp(Biomes.Of(a).Grass, Biomes.Of(b).Grass, t).Shade(M.Noise(i * .07f, 0, S) * .1f); }
    }
    static float Terrace(float v, float st, float ramp) { float t = v / st, f = MathF.Floor(t), r = t - f; return (f + M.Smooth(1 - ramp, 1, r)) * st; }
    float Shape(Seg s, float x)
    {
        switch (s.B)
        {
            case Biome.Meadow: return 92 + F(x * .03f, 0, 4, 11) * 9;
            case Biome.Amber: return 89 + F(x * .036f, 3, 4, 12) * 15;
            case Biome.Highlands: { float r = 1 - MathF.Abs(N(x * .017f, 7, 13)); return 88 - MathF.Pow(r, 2.2f) * 44 + F(x * .06f, 0, 3, 14) * 8; }
            case Biome.Frost: { float r = 1 - MathF.Abs(N(x * .02f, 2, 15)); return 88 - r * r * r * 30 + F(x * .05f, 0, 3, 16) * 4; }
            case Biome.Blossom: return Terrace(90 + N(x * .022f, 4, 17) * 13, 3, .18f);
            case Biome.Dunes: { float q = x / 42 + N(x * .012f, 1, 18) * .7f, d = q - MathF.Floor(q), h = d < .68f ? M.Smooth(0, .68f, d) : 1 - M.Smooth(.68f, 1, d); return 95 - h * (8 + N(MathF.Floor(q) * 1.7f, 0, 19) * 4) + F(x * .05f, 0, 2, 20); }
            case Biome.Mesa: { float n = N(x * .026f, 9, 21) + N(x * .08f, 3, 22) * .2f; return 97 - M.Smooth(-.05f, .05f, n) * 12 - M.Smooth(.22f, .30f, n) * 13 - M.Smooth(.42f, .48f, n) * 8 + F(x * .08f, 0, 2, 23) * 1.4f; }
            case Biome.Jungle: return 90 + F(x * .042f, 5, 4, 24) * 17;
            case Biome.Marsh: return 100.6f + N(x * .09f, 2, 25) * 1.7f;
            case Biome.Bamboo: return Terrace(84 + N(x * .019f, 6, 26) * 22, 3, .12f);
            case Biome.Lavender: return 88 + M.Sin(x * .071f + S) * 4.6f + M.Sin(x * .033f + S * .7f) * 6;
            case Biome.Toadstool: return 96 - MathF.Abs(M.Sin(x * .056f + S)) * 6.5f + F(x * .05f, 0, 3, 27) * 5;
            case Biome.Cinder: { float c = (s.X0 + s.X1) * .5f, hw = (s.X1 - s.X0) * .42f, d = MathF.Abs(x - c) / hw, cone = MathF.Pow(MathF.Max(0, 1 - d), 1.35f) * 52; float cr = MathF.Abs(x - c); if (cr < 13) cone -= (13 - cr) * 1.5f; return 96 - cone + F(x * .07f, 0, 3, 28) * 5 * M.Clamp(d + .2f); }
            case Biome.Crystal: { float j = MathF.Abs(N(x * .06f, 2, 29)); return Terrace(92 - j * 26 + F(x * .02f, 0, 2, 30) * 8, 2, .5f); }
            case Biome.Moonpetal: return 92 + F(x * .03f, 8, 4, 31) * 11;
            default: { float e = MathF.Min(x, W - 1 - x); return e < 96 ? M.Lerp(128, 105.5f, M.Smooth(0, 1, e / 96)) + F(x * .06f, 0, 3, 32) * 3 : M.Lerp(105.5f, 96, M.Smooth(96, 178, e)) + F(x * .05f, 0, 3, 33) * 2.4f; }
        }
    }
    int MaxStep(int x) => BiomeAt(x) switch { Biome.Mesa or Biome.Bamboo or Biome.Blossom => 3, Biome.Crystal => 2, _ => 1 };
    void Heights()
    {
        const float R = 20; var h = new float[W]; int si = 0; for (int x = 0; x < W; x++) { while (x >= Segs[si].X1) si++; var s = Segs[si]; float v = Shape(s, x); if (si + 1 < Segs.Count && s.X1 - x < R) v = M.Lerp(v, Shape(Segs[si + 1], x), .5f * M.Smooth(R, 0, s.X1 - x)); else if (si > 0 && x - s.X0 < R) v = M.Lerp(Shape(Segs[si - 1], x), v, .5f + .5f * M.Smooth(0, R, x - s.X0)); h[x] = M.Clamp(v, 30, 150); }
        for (int x = 0; x < W; x++) Top[x] = (int)MathF.Round(h[x]);
        for (int it = 0; it < 3; it++) { for (int x = 1; x < W; x++) { int m = Math.Max(MaxStep(x), MaxStep(x - 1)); Top[x] = Math.Clamp(Top[x], Top[x - 1] - m, Top[x - 1] + m); } for (int x = W - 2; x >= 0; x--) { int m = Math.Max(MaxStep(x), MaxStep(x + 1)); Top[x] = Math.Clamp(Top[x], Top[x + 1] - m, Top[x + 1] + m); } }
        for (int it = 0; it < 2; it++) for (int x = 2; x < W - 2; x++) { if (Top[x] < Top[x - 1] && Top[x] < Top[x + 1]) Top[x] = Math.Min(Top[x - 1], Top[x + 1]); if (Top[x] > Top[x - 1] && Top[x] > Top[x + 1]) Top[x] = Math.Max(Top[x - 1], Top[x + 1]); }
    }
    readonly List<(int a, int b, int row, Biome bi)> basins = new();
    void Lakes()
    {
        foreach (var s in Segs)
        {
            if (s.B == Biome.Coast) continue; var d = Biomes.Of(s.B); int n = d.LakeP >= 1 ? (int)d.LakeP : rng.P(d.LakeP) ? 1 : 0; if (n == 1 && s.X1 - s.X0 > 140 && rng.P(.4f)) n++;
            for (int k = 0; k < n; k++)
            {
                (int lo, int hi, float dep) = s.B switch { Biome.Marsh => (9, 18, 3.4f), Biome.Blossom => (14, 26, 4), Biome.Frost => (26, 44, 8), Biome.Dunes => (16, 22, 4), Biome.Cinder => (10, 16, 4), Biome.Bamboo => (10, 18, 4), Biome.Toadstool => (14, 24, 5), _ => (26, 54, 11) };
                int w = rng.I(lo, hi), bestA = -1; float bs = 99; for (int q = 0; q < 70; q++) { int a = rng.I(s.X0 + 14, Math.Max(s.X0 + 15, s.X1 - w - 14)), b = a + w; if (b > s.X1 - 10) continue; bool bad = false; for (int x = a - 14; x <= b + 14 && !bad; x++) bad = lake[x]; if (bad) continue; float mn = 999; for (int x = a; x <= b; x++) mn = MathF.Min(mn, Top[x]); float sc = MathF.Abs(Top[a] - Top[b]) + MathF.Max(0, Math.Max(Top[a], Top[b]) - mn) * .6f; if (sc < bs) { bs = sc; bestA = a; } }
                if (bestA < 0 || bs > 14) continue; int A = bestA, Bb = A + w, row = Math.Max(Top[A], Top[Bb]); void Bank(int x0, int dir, int y) { for (int i = 0; i < 80; i++) { int x = x0 + dir * i; if (x < 4 || x > W - 5 || (i > 0 && lake[x])) break; int t = Math.Clamp(Top[x], y - i, y + i); if (t == Top[x] && i > 3) break; Top[x] = t; } } Bank(A, -1, row); Bank(Bb, 1, row);
                for (int x = A + 1; x < Bb; x++) { float t = (x - A) / (float)w; Top[x] = row + 1 + (int)MathF.Round(MathF.Max(0, MathF.Pow(M.Sin(t * M.PI), .75f) * dep + N(x * .2f, 4, 40) * 1.1f)); }
                for (int x = A - 8; x <= Bb + 8; x++) lake[x] = true; basins.Add((A, Bb, row, s.B));
                if (s.B is Biome.Jungle or Biome.Highlands && k == 0)
                {
                    // a cliff with a waterfall on one side, and an easy slope up behind it
                    int hgt = 14 + rng.I(6), y0 = row - hgt; for (int x = A - 9; x <= A; x++) { Top[x] = Math.Min(Top[x], y0 + (x == A ? 0 : 0)); lake[x] = true; } for (int i = 1; i < 70; i++) { int x = A - 9 - i; if (x < 4 || lake[x]) break; int t = Math.Clamp(Top[x], y0 - i, y0 + i); if (t == Top[x] && i > 3) break; Top[x] = t; }
                    for (int x = A - 8; x <= A; x++) Top[x] = y0 + (x < A - 1 ? 2 : 1); Top[A - 9] = Math.Min(Top[A - 9], y0); Top[A - 10] = Math.Min(Top[A - 10], y0); basins.Add((A - 9, A, y0, s.B)); Falls.Add((A, y0, row)); // a stream along the cliff top that runs right up to the lip
                }
            }
        }
    }
    void Fill()
    {
        // underground regions: every cell has a seed point, and the nearest one decides
        int cw = 118, ch = 44; var kinds = new Dictionary<(int, int), Biome>(); Biome[] ub = [Biome.Glowcap, Biome.Amethyst, Biome.Rootways]; int dealt = rng.I(3);
        Biome Cell(int cx, int cy) { if (kinds.TryGetValue((cx, cy), out var v)) return v; float q = M.Hash(cx, cy, S + 60); v = q < .74f ? ub[(cx + cy * 2 + dealt + (q < .2f ? 1 : 0) + 30) % 3] : 0; return kinds[(cx, cy)] = v; }
        for (int gy = 0; gy < H / 4; gy++) for (int gx = 0; gx < W / 4; gx++)
            {
                float x = gx * 4 + N(gx * .12f, gy * .12f, 61) * 16, y = gy * 4 + N(gx * .12f, gy * .12f, 62) * 10; int top = Top[Math.Min(W - 1, gx * 4)]; if (gy * 4 < top + 22) continue; if (gx * 4 < 150 || gx * 4 > W - 150) { if (y > 212) under[gy * (W / 4) + gx] = (byte)Biome.EmberDeep; continue; }
                if (y > 206) { under[gy * (W / 4) + gx] = (byte)Biome.EmberDeep; continue; } int cx = (int)MathF.Floor(x / cw), cy = (int)MathF.Floor(y / ch); float bd = 1e9f; Biome best = 0; for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) { float px = (cx + i + .2f + .6f * M.Hash(cx + i, cy + j, S + 63)) * cw, py = (cy + j + .2f + .6f * M.Hash(cx + i, cy + j, S + 64)) * ch; float dx = (px - x) / cw, dy = (py - y) / ch, dd = dx * dx + dy * dy; if (dd < bd) { bd = dd; best = Cell(cx + i, cy + j); } }
                under[gy * (W / 4) + gx] = (byte)best;
            }
        Parallel.For(0, W, x =>
        {
            for (int y = Top[x]; y < H; y++)
            {
                int d = y - Top[x]; var def = Biomes.Of(BiomeAt(Math.Clamp(x + N(y * .17f, x * .05f, 41) * 7 * M.Clamp(d / 6f), 0, W - 1))); float sub = def.SubDepth + N(x * .13f, 3, 42) * 2; T t = d == 0 ? def.Top : d < sub ? def.Sub : d < 46 + N(x * .05f, y * .05f, 43) * 9 ? def.Rock : F(x * .03f, y * .05f, 2, 44) > .06f ? T.Slate : T.Stone;
                if (d >= sub)
                {
                    if (F(x * .09f, y * .11f, 3, 45) > .40f) t = d < 40 ? (def.Sub == T.Sand ? T.Sandstone : def.Grass != 0 && Blocks.Grows(def.Sub) ? def.Sub : T.Dirt) : T.Dirt; if (F(x * .07f, y * .09f, 2, 46) > .46f) t = T.Clay; if (F(x * .08f, y * .10f, 2, 47) > .50f) t = T.Gravel;
                    int ux = Math.Clamp((int)(x + N(x * .11f, y * .11f, 53) * 9), 0, W - 1), uy = Math.Clamp((int)(y + N(x * .11f, y * .11f, 54) * 7), 0, H - 1); byte ub2 = under[(uy / 4) * (W / 4) + ux / 4]; if (ub2 != 0) { var u = Biomes.Of((Biome)ub2); float v = F(x * .12f, y * .13f, 3, 48); if ((Biome)ub2 == Biome.EmberDeep) t = v > .40f ? T.Ember : F(x * .05f, y * .06f, 2, 49) > .34f ? T.Ash : T.Basalt; else if (v > .33f) t = u.Sub; else if (N(x * .05f, y * .06f, 50) > -.55f) t = u.Rock; }
                }
                else if (def.Sub == T.Snow && d >= 2 && F(x * .1f, y * .1f, 2, 51) > .34f) t = T.Ice;
                if (y >= H - 3 - (N(x * .3f, 0, 52) > 0 ? 1 : 0)) t = T.Heartstone; int i = y * W + x; Tile[i] = t; if (d > 0) Wall[i] = t == T.Heartstone || t == T.Ember ? T.Basalt : t == T.Glowrock ? T.Moss : t == T.Geode ? T.Slate : t;
            }
        });
        foreach (var (a, b, row, bi) in basins) for (int x = a; x <= b; x++) { if (bi == Biome.Frost) { if (Top[x] > row) { Tile[row * W + x] = T.Ice; } } else if (bi is not (Biome.Cinder or Biome.Crystal or Biome.Marsh) && Top[x] >= row) for (int k = 0; k < 2; k++) if (Tile[(Top[x] + k) * W + x] != T.Air) Tile[(Top[x] + k) * W + x] = bi is Biome.Highlands ? T.Gravel : T.Sand; }
    }
    void Caves()
    {
        var keep = new bool[W]; var ms = Segs.First(s => s.B == Biome.Meadow); for (int x = (ms.X0 + ms.X1) / 2 - 24; x < (ms.X0 + ms.X1) / 2 + 24; x++) keep[x] = true; var air = new bool[W * H];
        Parallel.For(2, W - 2, x =>
        {
            float door = N(x * .021f, 99, 70); bool sea = x < 150 || x > W - 150;
            for (int y = Top[x]; y < H - 4; y++)
            {
                int d = y - Top[x]; if (sea && d < 20) continue; if (lake[x] && d < 12) continue; if (keep[x] && d < 14) continue; float open = door > .34f ? M.Smooth(0, 3, d + 1) : M.Smooth(9, 17, d); if (open <= 0) continue; var u = (Biome)under[(y / 4) * (W / 4) + x / 4];
                float n1 = N(x * .026f, y * .050f, 71) + N(x * .075f, y * .11f, 72) * .22f, n2 = N(x * .031f + 40, y * .062f, 73) + N(x * .09f, y * .12f, 74) * .2f; float wd = (.052f + .050f * M.Smooth(8, 80, d)) * open; bool c = MathF.Abs(n1) < wd || (d > 26 && MathF.Abs(n2) < wd * .72f);
                if (!c && d > 20) { float big = F(x * .021f, y * .039f, 3, 75) + (u == Biome.Glowcap ? .10f : u == Biome.Amethyst ? .06f : u == Biome.EmberDeep ? .09f : 0); c = big > .42f - .05f * M.Smooth(20, 110, d); }
                if (c) air[y * W + x] = true;
            }
        });
        for (int i = 0; i < air.Length; i++) if (air[i]) Tile[i] = T.Air;
        // tidy: no floating crumbs, no sealed pinholes
        for (int it = 0; it < 2; it++) for (int y = 4; y < H - 4; y++) for (int x = 3; x < W - 3; x++) { int i = y * W + x; int n = (Tile[i - 1] != T.Air ? 1 : 0) + (Tile[i + 1] != T.Air ? 1 : 0) + (Tile[i - W] != T.Air ? 1 : 0) + (Tile[i + W] != T.Air ? 1 : 0); if (Tile[i] != T.Air && n <= 1 && Wall[i] != T.Air) Tile[i] = T.Air; else if (Tile[i] == T.Air && n == 4 && Wall[i] != T.Air) Tile[i] = Wall[i]; }
        // moss and glowing rock prefer to show on cave faces
        for (int y = 2; y < H - 4; y++) for (int x = 2; x < W - 2; x++) { int i = y * W + x; if (Tile[i] == T.Air || under[(y / 4) * (W / 4) + x / 4] == 0) continue; bool face = Tile[i - 1] == T.Air || Tile[i + 1] == T.Air || Tile[i - W] == T.Air || Tile[i + W] == T.Air; if (!face) continue; var u = (Biome)under[(y / 4) * (W / 4) + x / 4]; float q = N(x * .3f, y * .3f, 76); if (q > .42f && Tile[i] != T.Heartstone) Tile[i] = Biomes.Of(u).Sub; }
        for (int x = 0; x < W; x++) { int y = Top[x]; while (y < H && Tile[y * W + x] == T.Air) y++; Top[x] = y; }
    }
    void Islands()
    {
        foreach (var s in Segs)
        {
            int n = s.B switch { Biome.Moonpetal => 5, Biome.Crystal => 4, Biome.Blossom => 3, Biome.Meadow or Biome.Bamboo or Biome.Jungle => 2, _ => 0 }; var d = Biomes.Of(s.B);
            for (int k = 0; k < n; k++)
            {
                int w = rng.I(5, 13), px = rng.I(s.X0 + 16, s.X1 - w - 16), lo = 999; for (int q = -3; q < w + 4; q++) lo = Math.Min(lo, Math.Min(H, Top[px + q])); int y = lo - rng.I(8, 22); if (y < 12) continue; bool clash = false; for (int q = -4; q < w + 5; q++) for (int j = -5; j < 10; j++) clash |= y + j > 0 && Solid(px + q, y + j); if (clash) continue;
                for (int x = 0; x < w; x++) { float t = (x + .5f) / w; int dep = 1 + (int)(M.Sin(t * M.PI) * (w * .42f) + N((px + x) * .5f, y, 80) * 1.2f); for (int j = 0; j < Math.Max(1, dep); j++) Tile[(y + j) * W + px + x] = j < 2 ? (j == 0 ? d.Top : d.Sub) : d.Rock; }
                int end = y; while (end < H - 2 && !Solid(px + w, end)) end++; if (end - y < 34 && !lake[px + w]) for (int j = y; j < end - 1; j++) Climb[j * W + px + w] = 2; islands.Add((px, w, y));
            }
        }
        // behind every waterfall, a passage through the cliff
        foreach (var (x, _, row) in Falls) for (int k = 0; k < 70; k++) { int tx = x - k; if (tx < 4) break; int f = row - (int)MathF.Max(0, N(k * .12f, row, 84) * 2 * M.Clamp(k / 6f)); if (k > 8 && Top[tx] >= f - 1) break; for (int y = f - 4 - (N(k * .3f, row, 85) > .2f ? 1 : 0); y < f; y++) if (y > Top[tx] + 1) Tile[y * W + tx] = T.Air; }
        // trailing vines in the warm, wet places
        for (int x = 200; x < W - 200; x++) { var b = BiomeAt(x); if (b is not (Biome.Jungle or Biome.Marsh or Biome.Moonpetal)) continue; for (int y = 30; y < Top[x] + 60 && y < H - 8; y++) if (Solid(x, y) && !Solid(x, y + 1) && M.Hash(x, y, S + 81) < .10f) { int L = 3 + (int)(M.Hash(x, y, S + 82) * 7); for (int j = 1; j <= L && !Solid(x, y + j) && !Solid(x, y + j + 1); j++) Climb[(y + j) * W + x] = 2; } }
    }
    readonly List<(int x, int w, int y)> islands = new();
    Pool NewPool(int row, Water k, Biome b) { var d = Biomes.Of(b); var p = new Pool { Row = row, Surf = row * TS + 6, Kind = k, X0 = W, X1 = 0, Bottom = row, Id = Pools.Count + 1, Shallow = d.Shallow, Deep = d.Deep }; Pools.Add(p); return p; }
    void Put(Pool p, int x, int y) { Wet[y * W + x] = (byte)p.Id; p.X0 = Math.Min(p.X0, x); p.X1 = Math.Max(p.X1, x); p.Bottom = Math.Max(p.Bottom, y); }
    void Waters()
    {
        for (int side = 0; side < 2; side++) { var p = NewPool(SeaRow, Water.Sea, Biome.Coast); for (int k = 0; k < 200; k++) { int x = side == 0 ? k : W - 1 - k; if (Top[x] <= SeaRow) break; for (int y = SeaRow; y < Top[x]; y++) Put(p, x, y); } }
        foreach (var (a, b, row, bi) in basins) { if (Pools.Count > 180) break; var p = NewPool(row, bi == Biome.Cinder ? Water.Spring : b - a < 14 ? Water.River : Water.Lake, bi); for (int x = a; x <= b; x++) for (int y = row; y < H - 2; y++) { if (Tile[y * W + x] != T.Air) { if (y > row) break; continue; } Put(p, x, y); } if (p.X0 > p.X1) Pools.RemoveAt(Pools.Count - 1); }
    }
    void CavePools()
    {
        int made = 0; for (int k = 0; k < 2600 && made < 64; k++)
        {
            int x = rng.I(160, W - 160), y = rng.I(Math.Min(H - 12, Top[x] + 16), H - 8); if (Solid(x, y) || WallAt(x, y) == T.Air || Wet[y * W + x] != 0) continue; while (y < H - 6 && !Solid(x, y + 1)) y++; int row = y - rng.I(0, 4); var seen = new HashSet<int>(); var st = new Stack<(int, int)>(); st.Push((x, y)); bool ok = true;
            while (st.Count > 0 && ok) { var (cx, cy) = st.Pop(); if (cy < row || Solid(cx, cy) || !seen.Add(cy * W + cx)) continue; if (seen.Count > 190 || WallAt(cx, cy) == T.Air || Wet[cy * W + cx] != 0 || Climb[cy * W + cx] != 0) { ok = false; break; } st.Push((cx - 1, cy)); st.Push((cx + 1, cy)); st.Push((cx, cy + 1)); st.Push((cx, cy - 1)); }
            if (!ok || seen.Count < 16) continue; var u = UnderAt(x, y); var p = NewPool(row, u == Biome.EmberDeep ? Water.Spring : Water.Cave, Biomes.Of(u).Under ? u : Biome.Glowcap); foreach (int i in seen) Put(p, i % W, i / W); made++;
        }
    }
    void Grass()
    {
        for (int x = 0; x < W; x++) { var d = Biomes.Of(BiomeAt(x)); if (d.Turf == null) continue; int y = Top[x]; if (y < H && Blocks.Grows(Tile[y * W + x]) && !WetTile(x, y - 1)) Turf[y * W + x] = true; }
        foreach (var (px, w, y) in islands) for (int x = px; x < px + w; x++) if (Biomes.Of(BiomeAt(x)).Turf != null && Blocks.Grows(At(x, y)) && !Solid(x, y - 1)) Turf[y * W + x] = true;
        // the ledges inside open ground keep their grass too
        for (int x = 1; x < W - 1; x++) for (int y = Math.Min(H - 2, Top[x] + 1); y < Math.Min(H - 2, Top[x] + 6); y++) if (Blocks.Grows(Tile[y * W + x]) && Tile[(y - 1) * W + x] == T.Air && Wet[(y - 1) * W + x] == 0 && Biomes.Of(BiomeAt(x)).Turf != null) Turf[y * W + x] = true;
    }
    void Spot()
    {
        var m = Segs.First(s => s.B == Biome.Meadow); int c = (m.X0 + m.X1) / 2, best = c; for (int k = 0; k < (m.X1 - m.X0) / 2 - 6; k++) { foreach (int x in new[] { c + k, c - k }) if (Flat(x, 3) && !lake[x]) { best = x; goto done; } }
    done: SpawnTile = best; Spawn = new(best * TS + 12, Top[best] * TS);
    }

    // ---------------------------------------------------------------- dressing
    Prop Add(string s, float x, float y, int layer, bool flip = false, int kind = 0, bool hang = false, bool rootless = false)
    {
        var p = new Prop { S = s, X = x, Y = y, Layer = layer, Flip = flip, Phase = rng.R(0, 6.28f), Kind = kind, Idx = Props.Count }; Props.Add(p); if (!rootless) { p.GX = (int)MathF.Floor(x / TS); p.GY = hang ? (int)MathF.Floor((y - 4) / TS) : (int)MathF.Floor((y + 2) / TS); int k = p.GY * W + p.GX; if (!rooted.TryGetValue(k, out var l)) rooted[k] = l = new(); l.Add(p); } return p;
    }
    void Glow(Prop p, float dy, float r, int c, float fl = .1f) { p.Lamp = new Lamp { X = p.X, Y = p.Y - dy, R = r, C = c, Flick = fl }; Lamps.Add(p.Lamp); }
    bool Flat(int x, int w) { for (int i = -w; i <= w; i++) if (x + i < 1 || x + i >= W - 1 || Top[x + i] != Top[x] || Top[x] >= H - 1) return false; return true; }
    bool FlatAt(int x, int y, int w) { for (int i = -w; i <= w; i++) if (!Solid(x + i, y) || Solid(x + i, y - 1)) return false; return true; }
    void Decorate()
    {
        var used = new HashSet<int>(); void Use(int x, int y, int r) { for (int i = -r; i <= r; i++) used.Add(y * W + x + i); for (int j = -3; j <= 3; j++) for (int i = -r + 1; i < r; i++) used.Add((y + j) * W + x + i); }
        bool Near(int x) => Math.Abs(x - SpawnTile) < 3;
        void Dress(int x, int y, bool isl)
        {
            var b = BiomeAt(x); var d = Biomes.Of(b); if (WetTile(x, y - 1) || Solid(x, y - 1) || Wall[(y - 1) * W + x] != T.Air) return; float gx = x * TS, gy = y * TS + 2; T t = At(x, y); bool soil = t == d.Top || t == d.Sub || Turf[y * W + x];
            if (d.Trees.Length > 0 && !used.Contains(y * W + x) && soil && FlatAt(x, y, 1) && rng.P(d.TreeP) && !Near(x) && !ClimbNear(x, y))
            {
                float tot = d.Trees.Sum(q => q.w), q2 = rng.F() * tot; string k = d.Trees[0].s; foreach (var (s, w) in d.Trees) { if ((q2 -= w) <= 0) { k = s; break; } } var p = Add(k + rng.I(3), gx + 12, gy, rng.P(.4f) ? 0 : 1, rng.P(.5f), 1); Use(x, y, d.TreeGap + rng.I(2));
                if (k == "moontree") Glow(p, 84, 120, 0x6ac8ff); else if (k == "toadstool") Glow(p, 60, 84, rng.P(.5f) ? 0xffb0d0 : 0xa8e8d8); else if (k == "charred") Glow(p, 26, 76, 0xff8438, .16f); else if (k == "spire") Glow(p, 50, 90, rng.P(.5f) ? 0x9ad0ff : 0xc8a8ff, .05f);
            }
            if (!soil && t is not (T.Sand or T.Snow or T.Ash or T.RedRock or T.Crystal or T.Gravel)) return; if (t is T.Sand && d.Top != T.Sand) { if (rng.P(.06f)) Add("rock" + rng.I(4), gx + 12, gy, 2, rng.P(.5f)); if (rng.P(.16f)) Add("reed" + rng.I(3), gx + 12, gy, 2, rng.P(.5f), 2); return; }
            foreach (var (s, pr, kind) in d.Plants)
            {
                if (!rng.P(pr)) continue; bool big = kind == 3 || s.Contains("rock") || s is "log" or "stump" or "icespike"; if (big && !FlatAt(x, y, 1)) continue; if (s == "lavender") { for (int i = 0; i < 2; i++) Add(s + rng.I(3), gx + 6 + i * 12 + rng.R(-3, 3), gy, rng.P(.7f) ? 2 : 3, rng.P(.5f), 2); continue; }
                string nm = s is "stump" or "log" or "starfish" ? s : s + rng.I(s is "rock" or "flower" or "flowera" or "flowerh" ? 4 : s is "beachgrass" ? 2 : 3); var p = Add(nm, gx + (big ? 12 : rng.R(4, 20)), gy + (kind == 3 ? 1 : 0), kind == 3 ? (rng.P(.6f) ? 1 : 3) : rng.P(.75f) ? 2 : kind == 2 ? 3 : 2, rng.P(.5f), kind);
                if (s == "emberbloom") Glow(p, 12, 80, 0xff8438, .16f); else if (s == "moonflower") Glow(p, 14, 92, rng.P(.6f) ? 0x62d8ff : 0xb0a0ff); else if (s == "crystal") Glow(p, 10, 70, 0x9ad0ff, .04f); else if (s == "sporepuff" && rng.P(.4f)) Glow(p, 8, 50, 0xf0c8ff); if (big) break;
            }
        }
        bool ClimbNear(int x, int y) { for (int i = -1; i <= 1; i++) for (int j = 1; j < 5; j++) if (ClimbAt(x + i, y - j) != 0) return true; return false; }
        for (int x = 98; x < W - 98; x++) if (Top[x] < H - 4) Dress(x, Top[x], false); foreach (var (px, w, y) in islands) for (int x = px; x < px + w; x++) Dress(x, y, true);
        // plants in and around water
        foreach (var pl in Pools)
        {
            bool sea = pl.Kind == Water.Sea, cave = pl.Kind == Water.Cave; var b = BiomeAt((pl.X0 + pl.X1) / 2); if (pl.Kind == Water.Spring) continue;
            for (int x = Math.Max(2, pl.X0); x <= Math.Min(W - 3, pl.X1); x++)
            {
                int y = pl.Row; if (Wet[y * W + x] != pl.Id && Wet[(y + 1) * W + x] != pl.Id) continue; if (Wet[y * W + x] != pl.Id) y++; int bot = y; while (bot < H - 2 && Wet[bot * W + x] == pl.Id) bot++; if (!Solid(x, bot)) continue; float dep = bot * TS - pl.Surf, gy = bot * TS + 2, gx = x * TS;
                if (!sea && !cave && dep < 70 && rng.P(.5f) && b is not (Biome.Frost or Biome.Crystal or Biome.Dunes)) Add("reed" + rng.I(3), gx + rng.R(4, 20), gy, rng.P(.5f) ? 1 : 3, rng.P(.5f), 2);
                if (!sea && !cave && dep > 26 && dep < 210 && rng.P(b == Biome.Marsh ? .3f : .13f) && b is not (Biome.Frost or Biome.Crystal)) Add("lily" + rng.I(3), gx + 12, pl.Surf, 3, rng.P(.5f), 4, false, true);
                if (rng.P(sea ? .34f : .2f) && dep > 56) Add((sea ? "kelp" : cave || b is Biome.Moonpetal or Biome.Toadstool ? "glowkelp" : "weed") + rng.I(3), gx + 12, gy, 1, rng.P(.5f), 2); if (sea && dep > 60 && rng.P(.24f)) Add("coral" + rng.I(3), gx + 12, gy, 1, rng.P(.5f)); if (rng.P(.05f)) Add("rock" + rng.I(4), gx + 12, gy + 1, 1);
            }
        }
        // caves
        for (int x = 4; x < W - 4; x++) for (int y = Top[x] + 3; y < H - 5; y++)
            {
                if (Solid(x, y) || WallAt(x, y) == T.Air || Wet[y * W + x] != 0) continue; bool floor = Solid(x, y + 1), ceil = Solid(x, y - 1); if (!floor && !ceil) continue; var u = Biomes.Of(UnderAt(x, y)); float px = x * TS + 12;
                if (floor && Solid(x - 1, y + 1) && Solid(x + 1, y + 1) && !Solid(x - 1, y) && !Solid(x + 1, y))
                {
                    if (u.Under) { float q = rng.F(); foreach (var (s, p, lamp, r) in u.CaveFloor) { if ((q -= p) > 0) continue; var pr = Add(s == "log" ? s : s + rng.I(s is "rock" ? 4 : 3), px, (y + 1) * TS + 2, 2, rng.P(.5f), s is "fern" ? 3 : 5); if (lamp != 0) Glow(pr, 14, r, s == "glow" && pr.S.EndsWith('1') ? 0x9a8aff : lamp, s == "emberbloom" ? .16f : .1f); break; } }
                    else { float q = rng.F(); if (q < .04f) Add("rock" + rng.I(4), px, (y + 1) * TS + 2, 2, rng.P(.5f)); else if (q < .075f) Add("shroom" + rng.I(3), px, (y + 1) * TS + 2, 2); else if (q < .10f) { var pr = Add("glow" + rng.I(3), px, (y + 1) * TS + 2, 2, rng.P(.5f), 5); Glow(pr, 14, 100, 0x58e0d0); } }
                }
                if (ceil && Climb[y * W + x] == 0 && !Solid(x, y + 1) && rng.P(u.Under ? .2f : .10f)) { string hs = u.Under ? u.Hang[rng.I(u.Hang.Length)] : "stal"; Add(hs + rng.I(hs is "hang" ? 2 : 3), px, y * TS - 2, 2, rng.P(.5f), 7, true); }
            }
        for (int x = 200; x < W - 200; x++) { var b = BiomeAt(x); if (b is Biome.Dunes or Biome.Mesa or Biome.Cinder or Biome.Crystal or Biome.Frost) continue; for (int y = 8; y < Math.Min(H - 6, Top[x] + 30); y++) if (Solid(x, y) && !Solid(x, y + 1) && WallAt(x, y + 1) == T.Air && Climb[(y + 1) * W + x] == 0 && !Solid(x, y + 2) && rng.P(.2f)) Add("hang" + rng.I(2), x * TS + 12, (y + 1) * TS - 2, 2, rng.P(.5f), 7, true); }
    }
    /// Roots and gone-ness. A prop leaves the world for good once its ground is taken away.
    void Drop(Prop p) { p.Gone = true; GoneProps.Add(p.Idx); if (p.Lamp != null) p.Lamp.Off = true; }
    public IEnumerable<Prop> RootedAt(int x, int y) => rooted.TryGetValue(y * W + x, out var l) ? l.Where(p => !p.Gone) : [];
    public void Remove(Prop p) => Drop(p);

    // ---------------------------------------------------------------- light
    int SkyOf(int x) { for (int y = 0; y < H; y++) if (Tile[y * W + x] != T.Air || (Wall[y * W + x] != T.Air && y > Top0[x] + 1)) return y; return H; }
    static float Fade(T t) => t == T.Air ? .925f : t is T.Ice or T.Crystal ? .84f : .70f;
    void LightAll()
    {
        Array.Clear(Light); var q = new Queue<int>(); for (int x = 0; x < W; x++) for (int y = 0; y < Sky[x]; y++) Light[y * W + x] = 1;
        for (int x = 0; x < W; x++) { int lo = Sky[x]; if (x > 0) lo = Math.Min(lo, Sky[x - 1]); if (x < W - 1) lo = Math.Min(lo, Sky[x + 1]); for (int y = Math.Max(0, Math.Min(lo - 1, Sky[x] - 1)); y < Sky[x]; y++) q.Enqueue(y * W + x); }
        Spread(q, 0, 0, W - 1, H - 1);
    }
    void Spread(Queue<int> q, int x0, int y0, int x1, int y1)
    {
        while (q.Count > 0) { int i = q.Dequeue(), x = i % W, y = i / W; float l = Light[i]; void Go(int nx, int ny) { if (nx < x0 || ny < y0 || nx > x1 || ny > y1) return; int j = ny * W + nx; float f = l * Fade(Tile[j]); if (f > Light[j] + .01f && f > .02f) { Light[j] = f; q.Enqueue(j); } } Go(x - 1, y); Go(x + 1, y); Go(x, y - 1); Go(x, y + 1); }
    }
    /// Re-lights the neighbourhood of a change and reports the box that changed.
    (int x0, int y0, int x1, int y1) Relight(int cx, int cy, int up, int down)
    {
        const int R = 34; int x0 = Math.Max(0, cx - R), x1 = Math.Min(W - 1, cx + R), y0 = Math.Max(0, up - R), y1 = Math.Min(H - 1, down + R); var q = new Queue<int>();
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) { int i = y * W + x; bool edge = x == x0 || x == x1 || y == y0 || y == y1; float b = y < Sky[x] ? 1 : 0; if (!edge) Light[i] = b; if (Light[i] > .02f && (edge || b > 0)) q.Enqueue(i); }
        Spread(q, x0, y0, x1, y1); return (x0, y0, x1, y1);
    }

    // ---------------------------------------------------------------- changing
    /// Fired after a block changes: the tile, then the box whose light changed.
    public Action<int, int> TileChanged; public Action<int, int, int, int> LightChanged; public Action<Prop> Uprooted; public Action<int, int> VineGone;
    void Note(int i) => Edits[i] = (byte)((byte)Tile[i] | (Turf[i] ? 0x80 : 0));
    public void Set(int x, int y, T t)
    {
        if (x < 1 || y < 1 || x >= W - 1 || y >= H - 1) return; int i = y * W + x; Tile[i] = t; Turf[i] = false; if (t != T.Air) { Wet[i] = 0; Climb[i] = 0; } Note(i); int up = y, down = y;
        if (t != T.Air) { if (y + 1 < H && Turf[i + W]) { Turf[i + W] = false; Note(i + W); } if (y < Top[x]) Top[x] = y; } else if (y == Top[x]) { int k = y; while (k < H && Tile[k * W + x] == T.Air) k++; Top[x] = k; }
        int os = Sky[x]; Sky[x] = SkyOf(x); up = Math.Min(up, Math.Min(os, Sky[x])); down = Math.Max(down, Math.Min(y + 60, Math.Max(os, Sky[x])));
        if (t == T.Air)
        {
            foreach (var p in RootedAt(x, y).ToList()) { Drop(p); Uprooted?.Invoke(p); } for (int k = y + 1; k < H && Climb[k * W + x] == 2; k++) { if (k == y + 1 || Climb[(k - 1) * W + x] == 0) { Climb[k * W + x] = 0; VineGone?.Invoke(x, k); } }
            for (int f = Falls.Count - 1; f >= 0; f--) { var (fx, fy0, _) = Falls[f]; if (x == fx && y == fy0 + 1) Falls.RemoveAt(f); }
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1) }) { int j = (y + dy) * W + x + dx; if (Wet[j] != 0) { var p = Pools[Wet[j] - 1]; if (y >= p.Row) { Flow(x, y, p); break; } } }
        }
        else foreach (var p in RootedAt(x, y + 1).Where(p => p.Kind != 1 && p.Kind != 7).ToList()) { Drop(p); Uprooted?.Invoke(p); }
        if (t != T.Air) foreach (var p in Props.Count > 0 ? RootedAt(x, y - 1).Where(q => q.Kind == 7).ToList() : []) { Drop(p); Uprooted?.Invoke(p); }
        TileChanged?.Invoke(x, y); var b = Relight(x, y, up, down); LightChanged?.Invoke(b.x0, b.y0, b.x1, b.y1);
    }
    readonly Queue<(int x, int y, Pool p)> flow = new(); float flowT;
    void Flow(int x, int y, Pool p) { if (p.Flooded > 1600 || Tile[y * W + x] != T.Air || Wet[y * W + x] != 0 || y < p.Row || (Wall[y * W + x] == T.Air && y < Top0[x])) return; flow.Enqueue((x, y, p)); }
    /// Water creeps into any hole that opens below its surface, one block at a time.
    void FlowStep(int n)
    {
        for (int k = 0; k < n && flow.Count > 0; k++) { var (x, y, p) = flow.Dequeue(); if (x < 2 || x >= W - 2 || y >= H - 2 || Tile[y * W + x] != T.Air || Wet[y * W + x] != 0 || p.Flooded > 1600) continue; Put(p, x, y); p.Flooded++; Climb[y * W + x] = 0; LightChanged?.Invoke(x, y, x, y); Flow(x, y + 1, p); Flow(x - 1, y, p); Flow(x + 1, y, p); Flow(x, y - 1, p); }
    }
    public void Tick(float dt, float px)
    {
        flowT -= dt; if (flowT <= 0) { flowT = .07f; FlowStep(1 + flow.Count / 3); }
        // grass slowly creeps onto bare earth beside it
        turfT -= dt; if (turfT <= 0) { turfT = .25f; int x = (int)(px / TS) + rng.I(-34, 35); if (x > 2 && x < W - 2) { int y = Top[x]; for (int k = 0; k < 4 && y < H - 2; k++, y++) { int i = y * W + x; if (!Blocks.Grows(Tile[i]) || Turf[i] || Tile[i - W] != T.Air || Wet[i - W] != 0 || Light[i - W] < .6f || !rng.P(.35f)) continue; bool near = false; for (int dy = -1; dy <= 1 && !near; dy++) near = Turf[i - 1 + dy * W] || Turf[i + 1 + dy * W]; if (!near) continue; Turf[i] = true; Note(i); TileChanged?.Invoke(x, y); break; } } }
    }
    float turfT;
    /// A safe place to stand at the world's starting point, even if someone has built there.
    public Vector2 SafeSpawn() { int x = SpawnTile; int y = Math.Min(H - 4, Top0[x]); while (y > 4 && (Solid(x, y - 1) || Solid(x, y - 2) || Solid(x, y - 3))) y--; return new(x * TS + 12, y * TS); }
    public bool FitsPlayer(Vector2 p) { int x0 = (int)MathF.Floor((p.X - 8) / TS), x1 = (int)MathF.Floor((p.X + 7.9f) / TS); int y0 = (int)MathF.Floor((p.Y - 60) / TS), y1 = (int)MathF.Floor((p.Y - 1) / TS); for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (Solid(x, y)) return false; return p.X > 40 && p.X < W * TS - 40 && p.Y > 40 && p.Y < H * TS - 40; }
    public string SaveEdits()
    {
        if (Edits.Count == 0) return ""; using var ms = new MemoryStream(); using (var df = new DeflateStream(ms, CompressionLevel.Optimal, true)) using (var bw = new BinaryWriter(df)) { bw.Write(Edits.Count); int last = 0; foreach (var (i, b) in Edits.OrderBy(e => e.Key)) { bw.Write7BitEncodedInt(i - last); bw.Write(b); last = i; } } return Convert.ToBase64String(ms.ToArray());
    }
    void LoadEdits(string s)
    {
        if (string.IsNullOrEmpty(s)) return; try
        {
            using var df = new DeflateStream(new MemoryStream(Convert.FromBase64String(s)), CompressionMode.Decompress); using var br = new BinaryReader(df); int n = br.ReadInt32(), i = 0; var opened = new List<int>();
            for (int k = 0; k < n; k++) { i += br.Read7BitEncodedInt(); byte b = br.ReadByte(); if (i <= 0 || i >= W * H) break; var t = (T)(b & 0x7f); if ((int)t >= Blocks.All.Length) t = T.Air; Edits[i] = b; Tile[i] = t; Turf[i] = (b & 0x80) != 0; if (t != T.Air) { Wet[i] = 0; Climb[i] = 0; } else opened.Add(i); }
            for (int c = 0; c < W * (H - 1); c++) if (Climb[c + W] == 2 && Climb[c] == 0 && Tile[c] == T.Air && Edits.ContainsKey(c)) Climb[c + W] = 0;
            foreach (int o in opened) foreach (int j in new[] { o - 1, o + 1, o - W }) if (j > 0 && Wet[j] != 0) { var p = Pools[Wet[j] - 1]; if (o / W >= p.Row) Flow(o % W, o / W, p); }
            for (int f = Falls.Count - 1; f >= 0; f--) if (!Solid(Falls[f].x, Falls[f].y0 + 1)) Falls.RemoveAt(f);
        }
        catch { }
    }
}

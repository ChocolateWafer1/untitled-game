using System.Numerics;
namespace UnnamedGame;

public enum T : byte { Air, Dirt, Stone, Sand, Cobble, Plank, Moss, Clay }
public enum Biome { SeaW, BeachW, Autumn, Lake, Village, Meadow, Highlands, BeachE, SeaE }
public sealed class Pool { public int X0, X1; public float Surf; public int Bottom; public Water Kind; public string Name; public bool Contains(float x, float y) => x >= X0 * World.TS && x < (X1 + 1) * World.TS && y > Surf && y < (Bottom + 1) * World.TS; }
public sealed class Prop { public string S; public float X, Y; public int Layer; public bool Flip; public float Phase; public int Kind; public int Data; public float Scale = 1, Bend; }
public sealed class Seller { public string Name, Place; public float X, Y; public AvatarData D; public AvatarAnim An = new(); public Raylib_cs.Texture2D Tex; public int Idx; public float Talk, Wave; public bool Greeted; }

/// One fixed, seeded world shared by everybody. 1500 x 170 blocks, bordered by sea.
public sealed class World
{
    public const int W = 1500, H = 170, TS = 24, Seed = 70417, SpawnTile = 760; public const float SeaY = 98 * TS + 6, LakeY = 95 * TS + 6, RiverY = 83 * TS + 6;
    public readonly T[] Tile = new T[W * H]; public readonly T[] Wall = new T[W * H]; public readonly bool[] Plat = new bool[W * H]; public readonly byte[] Climb = new byte[W * H]; // 1 ladder 2 vine
    public readonly int[] Top = new int[W]; public readonly List<Pool> Pools = new(); public readonly List<Prop> Props = new(); public readonly List<Seller> Sellers = new(); public float[] Light = new float[W * H];
    public Vector2 Spawn; public readonly List<(float x, float y, float r, Col c, float flick)> Lamps = new(); readonly Rng rng = new(Seed);
    public (int x, int y0, int y1) Falls;
    static readonly (float x, float y, float a)[] Ctl = [(0, 114, 1.5f), (60, 111, 1.5f), (92, 104, .4f), (118, 97.4f, .3f), (150, 94, .8f), (200, 88, 2.2f), (260, 82, 1.8f), (294, 80, .4f), (299, 87, .3f), (307, 87, .3f), (312, 80, .4f), (360, 74, 2.2f), (420, 66, 1.6f), (452, 65, .4f), (468, 66, .2f), (470, 98, .3f), (482, 103, .8f), (530, 107, 1), (572, 103, .8f), (592, 95, .4f), (620, 91, 1.4f), (700, 90, 1), (728, 89, 0), (792, 89, 0), (830, 90, 1), (900, 83, 2.2f), (960, 88, 2.2f), (1010, 78, 2), (1060, 80, 1.6f), (1120, 66, 2.2f), (1170, 55, 2), (1215, 46, 1.6f), (1260, 50, 2), (1300, 60, 2), (1340, 74, 1.6f), (1375, 90, 1), (1400, 95, .6f), (1424, 97.4f, .3f), (1445, 104, .6f), (1470, 111, 1.4f), (1500, 114, 1)];

    public T At(int x, int y) => x < 0 || x >= W ? T.Stone : y < 0 ? T.Air : y >= H ? T.Stone : Tile[y * W + x];
    public bool Solid(int x, int y) => At(x, y) != T.Air;
    public bool PlatAt(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Plat[y * W + x];
    public byte ClimbAt(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? Climb[y * W + x] : (byte)0;
    public T WallAt(int x, int y) => x < 0 || x >= W || y < 0 ? T.Air : y >= H ? T.Stone : Wall[y * W + x];
    public Pool PoolAt(float x, float y) { foreach (var p in Pools) if (p.Contains(x, y)) return p; return null; }
    public static Biome BiomeAt(float tx) => tx < 92 ? Biome.SeaW : tx < 152 ? Biome.BeachW : tx < 462 ? Biome.Autumn : tx < 600 ? Biome.Lake : tx < 700 ? Biome.Meadow : tx < 812 ? Biome.Village : tx < 1066 ? Biome.Meadow : tx < 1370 ? Biome.Highlands : tx < 1436 ? Biome.BeachE : Biome.SeaE;
    public static string AreaName(float tx, bool cave) => cave ? (tx < 600 ? "Behind the Falls" : "Glowcap Caverns") : tx < 92 ? "The West Sea" : tx < 152 ? "Driftwood Beach" : tx < 292 ? "Birchwood" : tx < 316 ? "Maple River" : tx < 462 ? "Amber Forest" : tx < 600 ? "Lake Lantern" : tx < 700 ? "Clover Meadow" : tx < 812 ? "Lantern Village" : tx < 930 ? "Clover Meadow" : tx < 1066 ? "Rolling Hills" : tx < 1370 ? "Pinecrest Highlands" : tx < 1436 ? "Sunrise Shore" : "The East Sea";
    /// 0 meadow green, 1 autumn gold, 2 highland teal; blended so colours drift naturally.
    public static Col GrassCol(float tx)
    {
        Col m = 0x86b050, a = 0xc9a347, h = 0x5f9a6a, s = 0xa8b45e; float au = M.Smooth(140, 215, tx) * (1 - M.Smooth(430, 500, tx)), hi = M.Smooth(1030, 1120, tx) * (1 - M.Smooth(1345, 1390, tx)), be = (1 - M.Smooth(120, 170, tx)) + M.Smooth(1370, 1410, tx);
        var c = Col.Lerp(m, a, au); c = Col.Lerp(c, h, hi); c = Col.Lerp(c, s, M.Clamp(be)); return c.Shade(M.Noise(tx * .07f, 0, 6) * .1f);
    }
    public float Height(float x)
    {
        int i = 0; while (i < Ctl.Length - 2 && Ctl[i + 1].x < x) i++; var a = Ctl[i]; var b = Ctl[i + 1]; float t = M.Clamp((x - a.x) / (b.x - a.x)), s = t * t * (3 - 2 * t); float amp = M.Lerp(a.a, b.a, s);
        return M.Lerp(a.y, b.y, s) + M.Fbm(x * .035f, 0, 4, 11) * amp * 2.2f + M.Noise(x * .012f, 5, 3) * amp;
    }

    public World() { Gen(); }
    void Gen()
    {
        for (int x = 0; x < W; x++) Top[x] = (int)MathF.Round(Height(x));
        bool Cliff(int x) => x >= 466 && x <= 472;
        // keep the land walkable: never more than one block of step outside the waterfall cliff
        for (int it = 0; it < 3; it++)
        {
            for (int x = 1; x < W; x++) if (!Cliff(x) && !Cliff(x - 1)) Top[x] = Math.Clamp(Top[x], Top[x - 1] - 1, Top[x - 1] + 1);
            for (int x = W - 2; x >= 0; x--) if (!Cliff(x) && !Cliff(x + 1)) Top[x] = Math.Clamp(Top[x], Top[x + 1] - 1, Top[x + 1] + 1);
        }
        for (int x = 728; x <= 792; x++) Top[x] = 89;
        // widen single block spikes and pits so the ground reads as calm ledges
        for (int it = 0; it < 2; it++) for (int x = 2; x < W - 2; x++) if (!Cliff(x)) { if (Top[x] < Top[x - 1] && Top[x] < Top[x + 1]) Top[x] = Math.Min(Top[x - 1], Top[x + 1]); if (Top[x] > Top[x - 1] && Top[x] > Top[x + 1]) Top[x] = Math.Max(Top[x - 1], Top[x + 1]); }
        for (int x = 0; x < W; x++)
        {
            var b = BiomeAt(x); bool sandy = x < 158 || x > 1392; int sandD = sandy ? 5 + (int)(M.Noise(x * .2f) * 2) : 0;
            if (x >= 472 && x <= 600) sandD = Math.Max(sandD, Top[x] * TS > LakeY - 30 ? 3 : 0); if (x >= 297 && x <= 309) sandD = 2;
            int dirt = 5 + (int)(M.Noise(x * .11f, 3) * 2.4f) - (b == Biome.Highlands ? 2 : 0);
            for (int y = Top[x]; y < H; y++)
            {
                int d = y - Top[x]; T t = d < sandD ? T.Sand : d < dirt + sandD * .4f ? T.Dirt : T.Stone; if (t == T.Dirt && d > 1 && M.Fbm(x * .11f, y * .13f, 2, 40) > .42f) t = T.Stone; if (t == T.Stone && d < 26 && M.Fbm(x * .09f, y * .1f, 3, 71) > .42f) t = T.Dirt; if (t == T.Stone && M.Fbm(x * .07f, y * .09f, 2, 88) > .5f) t = T.Clay; Tile[y * W + x] = t; if (d > 0) Wall[y * W + x] = t == T.Sand ? T.Sand : d < dirt + 2 ? T.Dirt : T.Stone;
            }
            if (x >= 735 && x <= 786) for (int d = 0; d < 2; d++) Tile[(Top[x] + d) * W + x] = T.Cobble;
        }
        Caves(); Structures(); Waters(); LightBake(); Decorate(); Villagers();
        Spawn = new(SpawnTile * TS + 60, Top[SpawnTile] * TS);
    }
    void Carve(float cx, float cy, float rx, float ry, bool mossy = true)
    {
        for (int y = (int)(cy - ry) - 1; y <= cy + ry + 1; y++) for (int x = (int)(cx - rx) - 1; x <= cx + rx + 1; x++)
            {
                if (x < 2 || x >= W - 2 || y < 2 || y >= H - 3) continue; float dx = (x - cx) / rx, dy = (y - cy) / ry, n = M.Noise(x * .23f, y * .23f, 55) * .22f; if (dx * dx + dy * dy < 1 + n) { if (Tile[y * W + x] != T.Air) { Tile[y * W + x] = T.Air; if (Wall[y * W + x] == T.Air) Wall[y * W + x] = T.Stone; } }
            }
    }
    void Tunnel(params (float x, float y, float r)[] p) { for (int i = 0; i + 1 < p.Length; i++) { float len = MathF.Max(MathF.Abs(p[i + 1].x - p[i].x), MathF.Abs(p[i + 1].y - p[i].y)); for (float t = 0; t <= 1; t += 1 / len) { float r = M.Lerp(p[i].r, p[i + 1].r, t); Carve(M.Lerp(p[i].x, p[i + 1].x, t), M.Lerp(p[i].y, p[i + 1].y, t) + M.Noise(t * len * .1f, i) * 1.4f, r * 1.1f, r); } } }
    void Caves()
    {
        // Glowcap Caverns: a calm winding cave through the highlands with a pool chamber and two exits
        float e1 = Top[1092] - 2, e2 = Top[1338] - 2;
        Tunnel((1088, e1 - 1, 3.2f), (1104, e1 + 3, 3.2f), (1126, e1 + 12, 3.4f), (1150, 92, 3.6f), (1172, 100, 4)); Carve(1206, 101, 30, 11); Tunnel((1230, 102, 4.2f), (1258, 96, 3.6f), (1284, 90, 3.6f), (1312, 84, 3.4f), (1328, e2 + 4, 3.2f), (1342, e2, 3.2f));
        Carve(1150, 87, 8, 5); Carve(1270, 90, 10, 6);
        // smooth the floors so the cave is easy to walk
        Floor(1090, 1172); Floor(1172, 1182); Floor(1232, 1342);
        for (int x = 1182; x <= 1232; x++) { int f = 109 + (int)(4.5f * MathF.Sin((x - 1182) / 50f * MathF.PI)); for (int y = 100; y < 118; y++) Tile[y * W + x] = y >= f ? (y - f < 2 ? T.Moss : T.Stone) : Tile[y * W + x] == T.Air || y > 104 ? T.Air : Tile[y * W + x]; }
        // small secret room behind the waterfall
        Tunnel((475, 91, 2.6f), (466, 91, 2.6f), (458, 92, 3)); Carve(446, 91, 11, 5); Floor(436, 473);
        for (int y = 2; y < H - 1; y++) for (int x = 2; x < W - 2; x++) if (Tile[y * W + x] == T.Stone && Wall[y * W + x] != T.Air && ((x > 1080 && x < 1350) || (x > 430 && x < 476)) && (At(x, y - 1) == T.Air || At(x, y + 1) == T.Air || At(x - 1, y) == T.Air || At(x + 1, y) == T.Air) && M.Noise(x * .15f, y * .15f, 12) > -.15f) Tile[y * W + x] = T.Moss;
    }
    void Floor(int x0, int x1)
    {
        int prev = -1; for (int x = x0; x <= x1; x++)
        {
            int c = -1; for (int y = H - 6; y > Top[x] - 4; y--) if (Tile[y * W + x] == T.Air && Tile[(y + 1) * W + x] != T.Air && y < 112) { c = y + 1; if (prev < 0 || Math.Abs(c - prev) < 6) break; }
            if (c < 0) continue; if (prev >= 0 && c > prev + 1) { for (int y = prev + 1; y < c; y++) Tile[y * W + x] = T.Stone; c = prev + 1; } else if (prev >= 0 && c < prev - 1) { for (int y = c; y < prev - 1; y++) Tile[y * W + x] = T.Air; c = prev - 1; }
            for (int y = c - 4; y < c; y++) if (y > 0) { Tile[y * W + x] = T.Air; if (y > Top[x]) { if (Wall[y * W + x] == T.Air) Wall[y * W + x] = T.Stone; } } prev = c;
        }
    }
    void Platform(int x0, int x1, int y) { for (int x = x0; x <= x1; x++) if (Tile[y * W + x] == T.Air) Plat[y * W + x] = true; }
    void Ladder(int x, int y0, int y1) { for (int y = y0; y <= y1; y++) if (Tile[y * W + x] == T.Air) Climb[y * W + x] = 1; }
    void Vine(int x, int y0, int y1) { for (int y = y0; y <= y1 && Tile[y * W + x] == T.Air; y++) Climb[y * W + x] = 2; }
    void Structures()
    {
        Platform(297, 310, 80); // river bridge
        int by = Math.Min(Top[296], Top[311]); Array.Clear(Plat, 80 * W + 290, 30); Platform(295, 312, by);
        Platform(578, 592, 94); Platform(98, 118, 96); Platform(1424, 1444, 96); // lake dock and sea piers
        // the cliff beside the waterfall: ladder down to the lake, with resting ledges
        int cx = 473; while (cx > 464 && !Solid(cx - 1, Top[462] + 6)) cx--;
        for (int x = cx - 5; x <= cx + 10; x++) for (int y = 56; y < 66; y++) { Tile[y * W + x] = T.Air; Wall[y * W + x] = T.Air; }
        for (int x = cx - 5; x <= cx + 7; x++) { Top[x] = 66; for (int y = 66; y < 68 + (x < cx + 5 ? 1 : 0); y++) { Tile[y * W + x] = y == 66 ? T.Dirt : T.Stone; } }
        for (int y = 65; y < 94; y++) { Tile[y * W + cx] = T.Air; Wall[y * W + cx] = T.Air; Climb[y * W + cx] = 1; }
        for (int x = cx; x <= cx + 10; x++) for (int y = 66; y < 104; y++) if (Tile[y * W + x] == T.Air && (x < cx + 9 || M.Hash(x, y / 3, 4) > .5f || y < 70 + (x - cx) * 2) && x < cx + 9 + M.Noise(y * .31f, 7) * 2.4f) Wall[y * W + x] = T.Stone;
        Plat[66 * W + cx] = true; Platform(cx + 1, cx + 4, 93); Platform(cx + 1, cx + 3, 80);
        Falls = (cx + 6, 66, 95);
        // cave shortcuts and pleasant ledges
        int v1 = 1150; int y1 = 0; for (int y = 60; y < 100; y++) if (!Solid(v1, y) && y > Top[v1] + 4) { y1 = y; break; } if (y1 > 0) { for (int y = Top[v1] - 1; y < y1 + 5; y++) { Tile[y * W + v1] = T.Air; if (Wall[y * W + v1] == T.Air && y > Top[v1]) Wall[y * W + v1] = T.Stone; Climb[y * W + v1] = 1; } for (int y = y1 + 5; y < 112 && !Solid(v1, y); y++) Climb[y * W + v1] = 1; }
        foreach (var (px, w, up) in new[] { (236, 5, 8), (246, 4, 10), (380, 5, 8), (392, 6, 10), (404, 4, 13), (888, 5, 8), (1000, 5, 8), (1012, 6, 10), (1180, 5, 8), (1232, 6, 8), (1244, 4, 11) }) { int lo = 999; for (int q = -2; q < w + 3; q++) lo = Math.Min(lo, Top[px + q]); int y = lo - up; Ledge(px, w, y); }
    }
    void Ledge(int px, int w, int y)
    {
        // small floating grassy rocks: playful, optional platforming
        for (int x = 0; x < w; x++) { Tile[y * W + px + x] = T.Dirt; if (x > 0 && x < w - 1) { Tile[(y + 1) * W + px + x] = T.Stone; } if (x > 1 && x < w - 2) Tile[(y + 2) * W + px + x] = T.Stone; }
        Vine(px + w, y, Math.Min(Top[px + w], Top[px + w - 1]) - 2);
    }
    void Waters()
    {
        int Bot(int x0, int x1) { int b = 0; for (int x = x0; x <= x1; x++) { int y = Top[x]; while (y < H - 2 && !Solid(x, y)) y++; b = Math.Max(b, y); } return b; }
        Pools.Add(new Pool { X0 = 0, X1 = 122, Surf = SeaY, Bottom = Bot(0, 122), Kind = Water.Sea, Name = "West Sea" }); Pools.Add(new Pool { X0 = 1420, X1 = W - 1, Surf = SeaY, Bottom = Bot(1420, W - 1), Kind = Water.Sea, Name = "East Sea" });
        Pools.Add(new Pool { X0 = 468, X1 = 596, Surf = LakeY, Bottom = Bot(475, 590), Kind = Water.Lake, Name = "Lake Lantern" }); Pools.Add(new Pool { X0 = 293, X1 = 314, Surf = RiverY, Bottom = Bot(296, 311), Kind = Water.River, Name = "Maple River" });
        Pools.Add(new Pool { X0 = 1180, X1 = 1234, Surf = 108 * TS + 8, Bottom = 116, Kind = Water.Cave, Name = "Dreamer Pool" });
    }
    void LightBake()
    {
        // sky light: full above ground, then a soft flood into caves and earth
        var q = new Queue<int>(); for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) { if (Tile[y * W + x] != T.Air || (Wall[y * W + x] != T.Air && y > Top[x] + 1)) { if (x > 464 && x < 486 && y < 104) { for (int yy = y; yy < 100; yy++) if (Tile[yy * W + x] == T.Air) Light[yy * W + x] = .92f; } break; } Light[y * W + x] = 1; }
        for (int i = 0; i < Light.Length; i++) if (Light[i] > 0) q.Enqueue(i);
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % W, y = i / W; float l = Light[i]; void Go(int nx, int ny) { if (nx < 0 || ny < 0 || nx >= W || ny >= H) return; int j = ny * W + nx; float f = l * (Tile[j] != T.Air ? .70f : .925f); if (f > Light[j] + .01f && f > .02f) { Light[j] = f; q.Enqueue(j); } }
            Go(x - 1, y); Go(x + 1, y); Go(x, y - 1); Go(x, y + 1);
        }
    }

    // ---------------------------------------------------------------- dressing
    Prop Add(string s, float x, float y, int layer, bool flip = false, int kind = 0, int data = 0) { var p = new Prop { S = s, X = x, Y = y, Layer = layer, Flip = flip, Phase = rng.R(0, 6.28f), Kind = kind, Data = data }; Props.Add(p); return p; }
    void Lamp(float x, float y, float r, Col c, float fl = .06f) => Lamps.Add((x, y, r, c, fl));
    float Gy(int tx) => Top[Math.Clamp(tx, 0, W - 1)] * TS;
    bool Flat(int x, int w) { for (int i = -w; i <= w; i++) if (x + i < 1 || x + i >= W - 1 || Top[x + i] != Top[x] || !Solid(x + i, Top[x + i]) || Solid(x + i, Top[x + i] - 1)) return false; return true; }
    void Decorate()
    {
        bool Wet(int x) => PoolAt(x * TS + 12, Gy(x) - 2) != null; var used = new bool[W]; void Use(int x, int r) { for (int i = -r; i <= r; i++) if (x + i >= 0 && x + i < W) used[x + i] = true; }
        for (int x = 724; x <= 796; x++) used[x] = true; foreach (var (a, b) in new[] { (290, 318), (458, 486), (576, 596) }) for (int x = a; x <= b; x++) used[x] = true;
        for (int x = 1086; x < 1100; x++) used[x] = true; for (int x = 1332; x < 1346; x++) used[x] = true;
        void Trees(int x0, int x1, int gap, string[] kinds, float p)
        {
            for (int x = x0; x < x1; x++) { if (used[x] || Wet(x) || !Flat(x, 1) || !rng.P(p)) continue; Add(kinds[rng.I(kinds.Length)] + rng.I(3), x * TS + 12, Gy(x) + 2, rng.P(.4f) ? 0 : 1, rng.P(.5f), 1); Use(x, gap + rng.I(2)); }
        }
        Trees(160, 292, 3, ["birch", "birch", "maple"], .5f); Trees(316, 458, 3, ["maple", "maple", "birch", "oakgold"], .5f); Trees(600, 724, 7, ["oak", "oak", "willow"], .24f); Trees(798, 1060, 7, ["oak", "oak", "blossom"], .22f); Trees(1062, 1366, 3, ["pine", "pine", "pine", "fir"], .44f); Trees(1368, 1416, 6, ["palm"], .35f); Trees(122, 156, 6, ["palm"], .35f);
        // a willow weeping over the lake
        foreach (int x in new[] { 596, 604 }) Add("willow" + (x & 1), x * TS, Gy(x) + 2, 1, x == 604, 1);
        var ub = new bool[W];
        for (int x = 122; x < 1420; x++)
        {
            if (Wet(x) || !Solid(x, Top[x]) || Solid(x, Top[x] - 1)) continue; var b = BiomeAt(x); T t = At(x, Top[x]); bool plaza = x > 732 && x < 790; float y = Gy(x), cx = x * TS; if (t is T.Stone or T.Moss) continue;
            if (t == T.Sand) { if (rng.P(.10f)) Add("shell" + rng.I(3), cx + rng.R(4, 20), y + 1, 2); if (rng.P(.05f)) Add("rock" + rng.I(4), cx + 12, y + 2, 2); if (rng.P(.06f)) Add("beachgrass" + rng.I(2), cx + 12, y + 2, 2, false, 2); if (rng.P(.025f)) Add("starfish", cx + 12, y + 1, 2); continue; }
            if (plaza) continue;
            if (!ub[x] && Flat(x, 1) && rng.P(b is Biome.Autumn ? .16f : b == Biome.Highlands ? .12f : .13f)) { string k = b == Biome.Autumn ? "bushgold" : b == Biome.Highlands ? (rng.P(.5f) ? "fern" : "bushpine") : "bush"; Add(k + rng.I(3), cx + 12, y + 3, rng.P(.6f) ? 1 : 3, rng.P(.5f), 3); ub[x] = ub[x + 1] = true; }
            else if (rng.P(.05f) && Flat(x, 1)) Add("rock" + rng.I(4), cx + 12, y + 3, 2, rng.P(.5f));
            else if (b == Biome.Autumn && rng.P(.05f)) Add(rng.P(.5f) ? "stump" : "log", cx + 12, y + 2, 2);
            else if (rng.P(b == Biome.Highlands ? .12f : .34f)) Add((b == Biome.Autumn ? "flowera" : b == Biome.Highlands ? "flowerh" : "flower") + rng.I(4), cx + rng.R(4, 20), y + 2, rng.P(.75f) ? 2 : 3, rng.P(.5f), 2);
            if (b == Biome.Autumn && rng.P(.12f)) Add("shroom" + rng.I(3), cx + rng.R(4, 20), y + 2, 2);
            if (b == Biome.Highlands && rng.P(.06f)) Add("shroom" + rng.I(3), cx + rng.R(4, 20), y + 2, 2);
        }
        // reeds, lilies and kelp by the water
        foreach (var pl in Pools)
        {
            for (int x = Math.Max(1, pl.X0); x < Math.Min(W - 1, pl.X1); x++)
            {
                int gy = Top[x]; while (gy < H - 2 && !Solid(x, gy)) gy++; float depth = gy * TS - pl.Surf; if (Solid(x, (int)(pl.Surf / TS))) { if (pl.Kind is Water.Lake or Water.River && gy * TS > pl.Surf - 60 && rng.P(.5f) && x < 600 && x > 486) Add("reed" + rng.I(3), x * TS + 12, gy * TS + 2, 2, rng.P(.5f), 2); continue; }
                if (depth < 4) continue;
                if (pl.Kind is Water.Lake or Water.River && depth < 70 && rng.P(.6f)) Add("reed" + rng.I(3), x * TS + rng.R(4, 20), gy * TS + 2, rng.P(.5f) ? 1 : 3, rng.P(.5f), 2);
                if (pl.Kind == Water.Lake && depth > 30 && depth < 200 && rng.P(.16f) && (x < 575 || x > 593)) Add("lily" + rng.I(3), x * TS + 12, pl.Surf, 3, rng.P(.5f), 4);
                if (rng.P(pl.Kind == Water.Sea ? .34f : .2f) && depth > 60) Add((pl.Kind == Water.Sea ? "kelp" : pl.Kind == Water.Cave ? "glowkelp" : "weed") + rng.I(3), x * TS + 12, gy * TS + 2, 1, rng.P(.5f), 2); if (pl.Kind == Water.Sea && depth > 90 && rng.P(.18f)) Add("coral" + rng.I(3), x * TS + 12, gy * TS + 2, 1, rng.P(.5f));
                if (rng.P(.06f)) Add("rock" + rng.I(4), x * TS + 12, gy * TS + 3, 1);
            }
        }
        // caves
        for (int x = 432; x < 1345; x++)
        {
            if (x > 476 && x < 1088) continue; for (int y = 60; y < 118; y++)
            {
                if (Solid(x, y) || WallAt(x, y) == T.Air) continue; bool floor = Solid(x, y + 1), ceil = Solid(x, y - 1); float px = x * TS + 12; if (PoolAt(px, y * TS + 20) != null) continue;
                if (floor) { float q = rng.F(); if (q < .15f) { int k = rng.I(4); Add("glow" + k, px, (y + 1) * TS + 2, 2, rng.P(.5f), 5, 6); Lamp(px, y * TS + 6, 118, k % 2 == 0 ? 0x58e0d0 : 0x9a8aff, .12f); } else if (q < .19f) Add("crystal" + rng.I(3), px, (y + 1) * TS + 2, 2, rng.P(.5f), 6); else if (q < .25f) Add("rock" + rng.I(4), px, (y + 1) * TS + 3, 2); else if (q < .3f) Add("shroom" + rng.I(3), px, (y + 1) * TS + 2, 2); if (q >= .15f && q < .19f) Lamp(px, y * TS + 4, 96, 0x8ab8ff, .05f); }
                if (ceil && rng.P(.2f)) Add(rng.P(.6f) ? "stal" + rng.I(3) : "hang" + rng.I(2), px, y * TS - 2, 2, rng.P(.5f), 7);
            }
        }
        foreach (var lx in new[] { 1092, 1336, 440, 1152 }) { int y = lx == 440 ? 90 : Top[lx] - 1; Lamp(lx * TS, y * TS - 20, 120, 0xffb060); }
        for (int x = 0; x < W; x++) for (int y = 1; y < H; y++) if (Solid(x, y) && !Solid(x, y + 1) && At(x, y) != T.Plank && WallAt(x, y + 1) == T.Air && y > 40 && rng.P(.16f) && x > 160 && x < 1380 && !PlatAt(x, y + 1)) Add("hang" + rng.I(2), x * TS + 12, (y + 1) * TS - 2, 2, rng.P(.5f), 7);
        Village();
        // travellers' lanterns and signs through the world
        foreach (int x in new[] { 170, 214, 262, 290, 317, 350, 400, 446, 602, 640, 688, 830, 880, 930, 985, 1040, 1075, 1130, 1190, 1250, 1300, 1376, 1398, 138, 154 }) { int k = x; while (!Flat(k, 1) && k < x + 10) k++; if (Wet(k)) continue; Add("lantern", k * TS + 12, Gy(k) + 1, 2, false, 8); Lamp(k * TS + 12, Gy(k) - 46, 130, 0xffb258); }
        foreach (int x in new[] { 597, 560 }) { }
        Add("boat", 100 * TS, SeaY + 4, 3, false, 4); Add("boat", 1452 * TS, SeaY + 4, 3, true, 4); Add("boat", 566 * TS, LakeY + 4, 3, true, 4);
        Add("campfire", 1218 * TS, Gy(1218) + 2, 2, false, 9); Lamp(1218 * TS, Gy(1218) - 12, 190, 0xff9a48, .16f); Add("tent", 1223 * TS, Gy(1223) + 2, 1); Add("logseat", 1215 * TS, Gy(1215) + 2, 2, false, 10);
        Add("campfire", 250 * TS, Gy(250) + 2, 2, false, 9); Lamp(250 * TS, Gy(250) - 12, 190, 0xff9a48, .16f); Add("logseat", 253 * TS, Gy(253) + 2, 2, false, 10);
        foreach (int x in new[] { 612, 660, 872, 1005, 420, 206 }) { int k = x; while (!Flat(k, 1)) k++; Add("bench", k * TS + 12, Gy(k) + 2, 2, false, 10); }
        Add("windmill", 852 * TS, Gy(852) + 4, 0, false, 11); Add("haystack", 862 * TS, Gy(862) + 2, 1); Add("haystack", 845 * TS, Gy(845) + 2, 2);
        foreach (var f in new[] { (664, 8), (834, 7), (905, 9) }) for (int i = 0; i < f.Item2; i++) { int k = f.Item1 + i; if (Top[k] == Top[k + 1]) Add("fence", k * TS + 12, Gy(k) + 2, 2); }
        // hidden statue cove behind the falls
        Add("statue", 444 * TS, FloorAt(444, 91) + 2, 1);
        Forage();
    }
    public float FloorAt(int x, int y) { while (y < H - 1 && !Solid(x, y)) y++; return y * TS; }
    void Village()
    {
        float g = 89 * TS; void L(float tx) { Add("lantern", tx * TS, g + 1, 2, false, 8); Lamp(tx * TS, g - 46, 150, 0xffb258); }
        Add("cottage0", 731 * TS, g + 2, 0, false, 12); Add("cottage1", 743 * TS, g + 2, 0, false, 12); Add("cottage2", 777 * TS, g + 2, 0, false, 12); Add("cottage0", 790 * TS, g + 2, 0, true, 12);
        foreach (var (x, w) in new[] { (731f, 1), (743f, 1), (777f, 1), (790f, 1) }) { Lamp(x * TS - 44, g - 46, 84, 0xffa850); Lamp(x * TS + 44, g - 46, 84, 0xffa850); }
        Add("fountain", 760 * TS, g + 2, 1, false, 13); Lamp(760 * TS, g - 40, 110, 0x9ad8ff, .05f);
        Add("aquarium", 768.5f * TS, g + 2, 1, false, 14); Lamp(768.5f * TS, g - 40, 130, 0x7ad0f0, .04f);
        Add("board", 753 * TS, g + 2, 2, false, 15); Add("well", 736.5f * TS, g + 2, 1); Add("bench", 756.5f * TS, g + 2, 2, false, 10); Add("bench", 764 * TS, g + 2, 2, false, 10); Add("barrel", 773 * TS, g + 2, 2); Add("crate", 772 * TS, g + 2, 1); Add("barrel", 747.6f * TS, g + 2, 2); Add("cart", 784.6f * TS, g + 2, 1);
        foreach (float x in new[] { 734.6f, 739.6f, 750, 759.9f - 6.4f, 766.4f, 775, 785, 794 }) L(x);
        for (int i = 0; i < 5; i++) Add("bunting", (739.6f + i * 10.5f) * TS + 120, g - 78, 1, false, 16);
        foreach (float x in new[] { 733.6f, 740.8f, 746, 780, 786.8f, 792.6f }) Add("flowerbox" + ((int)x % 2), x * TS, g + 2, 2, false, 2);
        Add("oak1", 726 * TS, g + 2, 1, false, 1); Add("blossom0", 748.8f * TS, g + 2, 0, false, 1); Add("blossom1", 772.4f * TS, g + 2, 0, true, 1); Add("oak0", 797 * TS, Gy(797) + 2, 1, true, 1);
        for (float x = 725; x < 734; x += 1) Add("fence", x * TS + 12, g + 2, 2); for (float x = 791; x < 797; x += 1) Add("fence", x * TS + 12, Gy((int)x) + 2, 2);
        Add("signpost", 789 * TS, g + 2, 2); Add("signpost", 732.4f * TS, g + 2, 2, true);
    }
    void Forage()
    {
        // Kind 20: forageable. Data is the bait id it gives. They grow back.
        void F(string s, int bait, int x0, int x1, int n) { for (int i = 0; i < n; i++) { for (int k = 0; k < 30; k++) { int x = rng.I(x0, x1); if (!Solid(x, Top[x]) || Solid(x, Top[x] - 1) || PoolAt(x * TS + 12, Gy(x) - 2) != null || PlatAt(x, Top[x] - 1)) continue; if (Props.Any(q => q.Kind == 20 && MathF.Abs(q.X - x * TS) < 60)) continue; Add(s, x * TS + 12, Gy(x) + 2, 2, rng.P(.5f), 20, bait); break; } } }
        F("f_berry", 3, 604, 724, 7); F("f_berry", 3, 800, 1060, 14); F("f_acorn", 4, 160, 456, 18); F("f_snail", 6, 122, 156, 6); F("f_snail", 6, 1392, 1420, 6); F("f_grub", 7, 1064, 1366, 14); F("f_worm", 1, 600, 1060, 8); F("f_worm", 1, 160, 456, 7); F("f_cricket", 2, 600, 1060, 9); F("f_firefly", 10, 604, 1060, 10);
        for (int i = 0; i < 12; i++) for (int k = 0; k < 60; k++) { int x = rng.I(1100, 1335); int y = Top[x] + 6; while (y < 112 && Solid(x, y)) y++; while (y < 114 && !Solid(x, y + 1)) y++; if (y >= 112 || Solid(x, y) || PoolAt(x * TS + 12, y * TS + 20) != null || Props.Any(q => q.Kind == 20 && MathF.Abs(q.X - x * TS) < 60)) continue; Add("f_glow", x * TS + 12, (y + 1) * TS + 2, 2, rng.P(.5f), 20, 5); Lamp(x * TS + 12, y * TS + 10, 50, 0x58e0d0, .1f); break; }
    }
    void Villagers()
    {
        void S(string n, string place, int tx, AvatarData d)
        {
            int k = tx; if (place != "Lantern Village") while (!Flat(k, 2) && k < tx + 30) k++; float y = Gy(k); d.Name = n; d.Validate(); Props.Add(new Prop { S = "stall" + (Sellers.Count % 3), X = k * TS + 12, Y = y + 2, Layer = 1, Kind = 17 }); Lamp(k * TS - 30, y - 50, 110, 0xffb258); Lamp(k * TS + 54, y - 50, 110, 0xffb258);
            Sellers.Add(new Seller { Name = n, Place = place, X = k * TS + 12, Y = y, D = d, Idx = Sellers.Count }); Props.RemoveAll(q => q.Kind is 1 or 2 or 3 or 0 && q.Layer > 0 && MathF.Abs(q.X - (k * TS + 12)) < 62 && q.S != "stall0" && q.S != "stall1" && q.S != "stall2" && place != "Lantern Village");
        }
        AvatarData A(int hair, int hcol, int skin, int top, int ta, int tb, int bot, int ba, int jk, int ja, int hat, int hta, int age, int beard = 0, int eye = 0, int mouth = 0, float wt = .45f) { var d = new AvatarData { Skin = skin, Age = age, Eye = eye, Mouth = mouth, Weight = wt }; d.Hair.Id = hair; d.Hair.Base = hcol; d[Slot.Top].Id = top; d[Slot.Top].A = ta; d[Slot.Top].B = tb; d[Slot.Bottom].Id = bot; d[Slot.Bottom].A = ba; d[Slot.Jacket].Id = jk; d[Slot.Jacket].A = ja; d[Slot.Hat].Id = hat; d[Slot.Hat].A = hta; d[Slot.Beard].Id = beard; d[Slot.Beard].A = hcol; d[Slot.Shoes].Id = 6; d[Slot.Shoes].A = 0x6b4a33; return d; }
        S("Marlow", "Driftwood Beach", 144, A(11, 0xd8d6d2, 0xc68a5e, 2, 0xf3ebdc, 0x35496b, 26, 0x4f6b52, 6, 0xe0b650, 5, 0xe0b650, 3, 9, 4, 0, .62f));
        S("Hazel", "Amber Forest", 345, A(38, 0x8a3324, 0xf0c4a0, 11, 0xc99a4b, 0x8c2f35, 67, 0x6b4a33, 12, 0x8c2f35, 9, 0x8c2f35, 1, 0, 2, 0));
        S("Wren", "Lake Lantern", 606, A(36, 0x1c1618, 0xa56a42, 8, 0xf3ebdc, 0x4a6285, 10, 0x4a6285, 19, 0xd8c49a, 17, 0xe0b872, 1, 0, 5, 1));
        S("Barnaby", "Lantern Village", 782, A(8, 0x5a3a26, 0xe8b48e, 5, 0xb8473f, 0x2b2a30, 14, 0x35496b, 0, 0, 24, 0x6b4a33, 2, 8, 2, 0, .7f));
        S("Isolde", "Pinecrest Highlands", 1236, A(30, 0x432a1c, 0x7e4b30, 9, 0xe9dcc3, 0xb8473f, 7, 0x4e4c52, 20, 0x35496b, 2, 0xb8473f, 2, 0, 1, 0));
        S("Coral", "Sunrise Shore", 1404, A(23, 0xe06a8a, 0xd49a70, 6, 0x7fa8c9, 0xf3ebdc, 37, 0xe9dcc3, 0, 0, 17, 0xf0dca8, 1, 0, 9, 4));
    }
}

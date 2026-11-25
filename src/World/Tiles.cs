using Raylib_cs;
namespace UntitledGame;

/// Paints the block world in chunks: rounded outer corners, ink outlines, hand shaded block faces and grassy caps.
public sealed class Tiles
{
    public const int C = 16, PX = C * World.TS; readonly World w; readonly Dictionary<int, (Texture2D t, int used)> cache = new(); int tick;
    readonly Dictionary<int, Color[]> ready = new(); readonly HashSet<int> pending = new(); readonly object mu = new();
    public Tiles(World w) { this.w = w; }
    static Col Mat(T t) => t switch { T.Dirt => 0x9a6a48, T.Stone => 0x857f88, T.Sand => 0xe3c98e, T.Cobble => 0xa8998c, T.Plank => 0xa8693c, T.Moss => 0x76806e, T.Clay => 0xa87058, _ => 0x000000 };
    Color[] Paint(int cx, int cy)
    {
        const int TS = World.TS, MG = 4, N = PX + MG * 2; var m = new byte[N * N]; int ox = cx * PX, oy = cy * PX;
        // solidity mask with rounded exposed corners
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                int wx = ox + x - MG, wy = oy + y - MG, tx = FD(wx), ty = FD(wy); if (!w.Solid(tx, ty)) continue; int fx = wx - tx * TS, fy = wy - ty * TS; const int R = 6; bool s = true;
                bool u = w.Solid(tx, ty - 1), d = w.Solid(tx, ty + 1), l = w.Solid(tx - 1, ty), r = w.Solid(tx + 1, ty);
                if (!u && !l && fx < R && fy < R && (R - fx) * (R - fx) + (R - fy) * (R - fy) > R * R + 4) s = false; if (!u && !r && fx >= TS - R && fy < R && (fx - TS + R + 1) * (fx - TS + R + 1) + (R - fy) * (R - fy) > R * R + 4) s = false;
                if (!d && !l && fx < R && fy >= TS - R && (R - fx) * (R - fx) + (fy - TS + R + 1) * (fy - TS + R + 1) > R * R + 4) s = false; if (!d && !r && fx >= TS - R && fy >= TS - R && (fx - TS + R + 1) * (fx - TS + R + 1) + (fy - TS + R + 1) * (fy - TS + R + 1) > R * R + 4) s = false;
                if (s) m[y * N + x] = 1;
            }
        var o = new Color[PX * PX];
        for (int y = 0; y < PX; y++) for (int x = 0; x < PX; x++)
            {
                int wx = ox + x, wy = oy + y, tx = wx / TS, ty = wy / TS, fx = wx % TS, fy = wy % TS, mi = (y + MG) * N + x + MG;
                if (m[mi] == 0)
                {
                    T wt = w.WallAt(tx, ty); if (wt == T.Air) continue; var wc = Face(wt, tx, ty, fx, fy, true); // back wall
                    int near = 9; for (int k = 1; k <= 3 && near == 9; k++) if (m[mi - k] != 0 || m[mi + k] != 0 || m[mi - k * N] != 0 || m[mi + k * N] != 0) near = k; if (near < 9) wc = wc.Shade(-.34f + near * .07f);
                    bool edge = (fx < 2 && w.WallAt(tx - 1, ty) == T.Air && !w.Solid(tx - 1, ty)) || (fx >= TS - 2 && w.WallAt(tx + 1, ty) == T.Air && !w.Solid(tx + 1, ty)) || (fy < 2 && w.WallAt(tx, ty - 1) == T.Air && !w.Solid(tx, ty - 1)); if (edge) wc = wc.Ink(); o[y * PX + x] = wc.Rl(); continue;
                }
                T t = w.At(tx, ty); int ed = 9; for (int k = 1; k <= 3 && ed == 9; k++) if (m[mi - k] == 0 || m[mi + k] == 0 || m[mi - k * N] == 0 || m[mi + k * N] == 0) ed = k;
                var c = Face(t, tx, ty, fx, fy, false);
                // grass cap
                bool up = w.Solid(tx, ty - 1); bool grassy = t == T.Dirt && !up;
                if (t is T.Dirt)
                {
                    // look up to two tiles for the surface so the cap can drip over neighbouring edges
                    if (grassy)
                    {
                        float cap = 7 + M.Noise(wx * .21f, ty, 4) * 2.2f + (M.Hash(wx / 3, ty, 8) > .6f ? 2 : 0); bool le = !w.Solid(tx - 1, ty), re = !w.Solid(tx + 1, ty); if (le && fx < 5) cap += 5 - fx; if (re && fx > TS - 6) cap += fx - (TS - 6);
                        Col g = World.GrassCol(wx / (float)TS); if (fy < cap - 1) { float hl = fy < 2 ? .42f : fy < 4 ? .18f : fy > cap - 4 ? -.26f : 0; hl += M.Noise(wx * .4f, wy * .5f, 9) * .12f + (M.Hash(wx / 2, wy / 3, 2) > .86f ? .16f : 0); c = g.Shade(MathF.Round(hl * 8) / 8); } else if (fy < cap + 1) c = g.Shade(-.62f); else if (fy < cap + 4) c = c.Shade(-.28f);
                    }
                }
                if (t == T.Moss && !up && fy < 5 + M.Noise(wx * .3f, ty, 4) * 2) c = ((Col)0x6f9a52).Shade(fy < 2 ? .3f : -.05f);
                if (t == T.Moss && !w.Solid(tx, ty + 1) && fy > TS - 4 + M.Noise(wx * .4f, ty, 7) * 3) c = ((Col)0x5a8a58).Shade(-.1f);
                if (t == T.Sand && !up) { if (fy < 2) c = c.Shade(.45f); else if (fy < 5) c = c.Shade(.16f); }
                if (t is T.Stone or T.Cobble or T.Clay && !up && fy < 3) c = c.Shade(.28f);
                if (ed <= 2) c = ed == 1 ? c.Ink() : Col.Lerp(c, c.Ink(), .42f); else if (ed == 3 && m[mi - 3 * N] == 0 | m[mi - 3] == 0) c = c.Shade(.16f);
                o[y * PX + x] = c.Rl();
            }
        return o;
    }
    static int FD(int v) => v >= 0 ? v / World.TS : -((-v + World.TS - 1) / World.TS);
    static Col Face(T t, int tx, int ty, int fx, int fy, bool wall)
    {
        const int TS = World.TS; var c = Mat(t); float h = M.Hash(tx, ty, wall ? 91 : 17), s = (h - .5f) * .22f + M.Noise(tx * .19f, ty * .23f, 6) * .16f; int wx = tx * TS + fx, wy = ty * TS + fy;
        if (t is T.Cobble) { int row = fy / 8, off = (row & 1) * 6 + (ty & 1) * 3, bx = (fx + off) / 12; float hh = M.Hash(tx * 3 + bx, ty * 3 + row, 5); s = (hh - .5f) * .4f; int lx = (fx + off) % 12, ly = fy % 8; if (lx == 0 || ly == 0) s = -.5f; else if (lx == 1 || ly == 1) s += .18f; else if (lx == 11 || ly == 7) s -= .16f; return c.Shade(s); }
        if (t == T.Plank) { int row = fy / 6; s = (M.Hash(tx / 2, ty * 4 + row, 5) - .5f) * .3f + M.Noise(wx * .08f, wy * .9f, 3) * .12f; if (fy % 6 == 0) s = -.45f; return c.Shade(s); }
        // each block has its own soft bevel so the voxels stay readable
        float bev = 0; if (fx == 0 || fy == TS - 1) bev = -.20f; else if (fy == 0 || fx == TS - 1) bev = -.10f; else if (fx <= 2 || fy <= 2) bev = .13f; else if (fx >= TS - 3 || fy >= TS - 3) bev = -.10f; s += bev;
        switch (t)
        {
            case T.Dirt: { for (int k = 0; k < 3; k++) { int px = 4 + (int)(M.Hash(tx, ty, 30 + k) * 15), py = 5 + (int)(M.Hash(tx, ty, 40 + k) * 14), r = 1 + (int)(M.Hash(tx, ty, 50 + k) * 2.4f); int dx = fx - px, dy = fy - py, d = dx * dx + dy * dy; if (d <= r * r) { s += dy < 0 || dx < 0 ? .2f : -.02f; if (k == 0) c = Col.Lerp(c, 0x8a8088, .6f); } else if (d <= (r + 1) * (r + 1)) s -= .22f; } s += M.Noise(wx * .22f, wy * .3f, 8) * .09f; if (M.Hash(wx / 2, wy / 2, 3) > .95f) s -= .16f; break; }
            case T.Stone: case T.Moss: { float cr = MathF.Abs(M.Noise(wx * .085f + 9, wy * .11f, 33)); if (cr < .045f) s -= .34f; else if (cr < .085f) s += .10f; int q = (int)(M.Hash(tx, ty, 61) * 4); if (q == 0 && Math.Abs(fx - fy) < 2 && fx > 5 && fx < 18) s -= .24f; if (q == 1 && fy is > 8 and < 11 && fx > 6) s -= .18f; s += M.Noise(wx * .2f, wy * .2f, 12) * .10f; if (t == T.Moss && M.Noise(wx * .11f, wy * .13f, 44) > .18f) c = Col.Lerp(c, 0x6f9a52, .55f); break; }
            case T.Sand: s = s * .5f + M.Sin(wy * .55f + M.Noise(wx * .05f, wy * .02f, 5) * 6) * .05f; if (M.Hash(wx, wy, 2) > .96f) s -= .16f; if (M.Hash(wx, wy, 7) > .985f) s += .3f; break;
            case T.Clay: s += ((wy + (int)(M.Noise(wx * .06f, ty) * 6)) / 5 & 1) == 0 ? .06f : -.06f; break;
        }
        if (wall) { c = Col.Lerp(c, 0x4a4058, .44f); s = s * .75f - .08f; }
        return c.Shade(MathF.Round(M.Clamp(s, -.8f, .8f) * 10) / 10);
    }
    public Texture2D? Get(int cx, int cy, bool sync)
    {
        if (cx < 0 || cy < 0 || cx * C >= World.W || cy * C >= World.H) return null; int k = cy * 1000 + cx; if (cache.TryGetValue(k, out var e)) { cache[k] = (e.t, tick); return e.t; }
        Color[] px = null; lock (mu) { if (ready.Remove(k, out px)) pending.Remove(k); else if (!sync && pending.Add(k)) { Task.Run(() => { var d = Paint(cx, cy); lock (mu) ready[k] = d; }); } }
        if (px == null) { if (!sync) return null; px = Paint(cx, cy); }
        unsafe { fixed (Color* p = px) { var img = new Image { Data = p, Width = PX, Height = PX, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 }; var t = Raylib.LoadTextureFromImage(img); Raylib.SetTextureFilter(t, TextureFilter.Point); cache[k] = (t, tick); return t; } }
    }
    public void Draw(float camX, float camY, float vw, float vh)
    {
        tick++; int x0 = (int)MathF.Floor(camX / PX), x1 = (int)MathF.Floor((camX + vw) / PX), y0 = (int)MathF.Floor(camY / PX), y1 = (int)MathF.Floor((camY + vh) / PX);
        for (int y = y0 - 1; y <= y1 + 1; y++) for (int x = x0 - 1; x <= x1 + 1; x++) { bool vis = x >= x0 && x <= x1 && y >= y0 && y <= y1; var t = Get(x, y, vis); if (t is Texture2D q && vis) Raylib.DrawTexture(q, x * PX - (int)camX, y * PX - (int)camY, Color.White); }
        if (cache.Count > 70) foreach (var k in cache.Where(q => q.Value.used < tick - 300).Select(q => q.Key).Take(4).ToList()) { Raylib.UnloadTexture(cache[k].t); cache.Remove(k); }
    }
}

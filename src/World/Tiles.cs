using Raylib_cs;
namespace UntitledGame;

/// Paints the block world in chunks: rounded outer corners, ink outlines, hand shaded block faces and grassy caps.
/// When a block changes, only the pixels around it are painted again.
public sealed class Tiles
{
    public const int C = 16, PX = C * World.TS; readonly World w; readonly Dictionary<int, (Texture2D t, int used)> cache = new(); int tick;
    readonly Dictionary<int, (Color[] px, int ver)> ready = new(); readonly HashSet<int> pending = new(); readonly Dictionary<int, int> ver = new(); readonly object mu = new();
    public Tiles(World w) { this.w = w; }
    public void Unload() { foreach (var q in cache.Values) Raylib.UnloadTexture(q.t); cache.Clear(); }
    static Col Mat(T t) => Blocks.Of(t).Color;
    /// Paint any rectangle of the world, in world pixels.
    public Color[] Paint(int ox, int oy, int PW, int PH)
    {
        const int TS = World.TS, MG = 4; int N = PW + MG * 2, NH = PH + MG * 2; var m = new byte[N * NH];
        // solidity mask with rounded exposed corners
        for (int y = 0; y < NH; y++) for (int x = 0; x < N; x++)
            {
                int wx = ox + x - MG, wy = oy + y - MG, tx = FD(wx), ty = FD(wy); if (!w.Solid(tx, ty)) continue; int fx = wx - tx * TS, fy = wy - ty * TS; const int R = 6; bool s = true;
                if ((fx < R || fx >= TS - R) && (fy < R || fy >= TS - R))
                {
                    bool u = w.Solid(tx, ty - 1), d = w.Solid(tx, ty + 1), l = w.Solid(tx - 1, ty), r = w.Solid(tx + 1, ty);
                    if (!u && !l && fx < R && fy < R && (R - fx) * (R - fx) + (R - fy) * (R - fy) > R * R + 4) s = false; if (!u && !r && fx >= TS - R && fy < R && (fx - TS + R + 1) * (fx - TS + R + 1) + (R - fy) * (R - fy) > R * R + 4) s = false;
                    if (!d && !l && fx < R && fy >= TS - R && (R - fx) * (R - fx) + (fy - TS + R + 1) * (fy - TS + R + 1) > R * R + 4) s = false; if (!d && !r && fx >= TS - R && fy >= TS - R && (fx - TS + R + 1) * (fx - TS + R + 1) + (fy - TS + R + 1) * (fy - TS + R + 1) > R * R + 4) s = false;
                }
                if (s) m[y * N + x] = 1;
            }
        var o = new Color[PW * PH];
        for (int y = 0; y < PH; y++) for (int x = 0; x < PW; x++)
            {
                int wx = ox + x, wy = oy + y, tx = FD(wx), ty = FD(wy), fx = wx - tx * TS, fy = wy - ty * TS, mi = (y + MG) * N + x + MG;
                if (m[mi] == 0)
                {
                    T wt = w.WallAt(tx, ty); if (wt == T.Air) continue; var wc = Face(wt, tx, ty, fx, fy, true); // back wall
                    int near = 9; for (int k = 1; k <= 3 && near == 9; k++) if (m[mi - k] != 0 || m[mi + k] != 0 || m[mi - k * N] != 0 || m[mi + k * N] != 0) near = k; if (near < 9) wc = wc.Shade(-.34f + near * .07f);
                    bool edge = (fx < 2 && w.WallAt(tx - 1, ty) == T.Air && !w.Solid(tx - 1, ty)) || (fx >= TS - 2 && w.WallAt(tx + 1, ty) == T.Air && !w.Solid(tx + 1, ty)) || (fy < 2 && w.WallAt(tx, ty - 1) == T.Air && !w.Solid(tx, ty - 1)); if (edge) wc = wc.Ink(); o[y * PW + x] = wc.Rl(); continue;
                }
                T t = w.At(tx, ty); int ed = 9; for (int k = 1; k <= 3 && ed == 9; k++) if (m[mi - k] == 0 || m[mi + k] == 0 || m[mi - k * N] == 0 || m[mi + k * N] == 0) ed = k;
                var c = Face(t, tx, ty, fx, fy, false); bool up = w.Solid(tx, ty - 1);
                if (!up && w.TurfAt(tx, ty))
                {
                    float cap = 7 + M.Noise(wx * .21f, ty, 4) * 2.2f + (M.Hash(wx / 3, ty, 8) > .6f ? 2 : 0); bool le = !w.Solid(tx - 1, ty), re = !w.Solid(tx + 1, ty); if (le && fx < 5) cap += 5 - fx; if (re && fx > TS - 6) cap += fx - (TS - 6);
                    Col g = World.GrassCol(wx / (float)TS); if (fy < cap - 1) { float hl = fy < 2 ? .42f : fy < 4 ? .18f : fy > cap - 4 ? -.26f : 0; hl += M.Noise(wx * .4f, wy * .5f, 9) * .12f + (M.Hash(wx / 2, wy / 3, 2) > .86f ? .16f : 0); c = g.Shade(MathF.Round(hl * 8) / 8); } else if (fy < cap + 1) c = g.Shade(-.62f); else if (fy < cap + 4) c = c.Shade(-.28f);
                }
                else if (!up) switch (t)
                    {
                        case T.Moss: if (fy < 5 + M.Noise(wx * .3f, ty, 4) * 2) c = ((Col)0x6f9a52).Shade(fy < 2 ? .3f : -.05f); break;
                        case T.Sand: case T.Ash: case T.Gravel: if (fy < 2) c = c.Shade(.45f); else if (fy < 5) c = c.Shade(.16f); break;
                        case T.Snow: if (fy < 3) c = 0xffffff; else if (fy < 6) c = c.Shade(.3f); break;
                        case T.Ice: if (fy < 2) c = 0xf4fcff; else if (fy < 4) c = c.Shade(.4f); break;
                        case T.Crystal: case T.Geode: if (fy < 2) c = c.Shade(.8f); else if (fy < 4) c = c.Shade(.35f); break;
                        default: if (fy < 3) c = c.Shade(.28f); break;
                    }
                if (t == T.Moss && !w.Solid(tx, ty + 1) && fy > TS - 4 + M.Noise(wx * .4f, ty, 7) * 3) c = ((Col)0x5a8a58).Shade(-.1f);
                if (t == T.Snow && !w.Solid(tx, ty + 1) && fy > TS - 5 + M.Noise(wx * .5f, ty, 7) * 3) c = 0xb4d4ec;
                if (ed <= 2) c = ed == 1 ? c.Ink() : Col.Lerp(c, c.Ink(), .42f); else if (ed == 3 && m[mi - 3 * N] == 0 | m[mi - 3] == 0) c = c.Shade(.16f);
                o[y * PW + x] = c.Rl();
            }
        return o;
    }
    static int FD(int v) => v >= 0 ? v / World.TS : -((-v + World.TS - 1) / World.TS);
    static int FDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    static Col Face(T t, int tx, int ty, int fx, int fy, bool wall)
    {
        const int TS = World.TS; var c = Mat(t); float h = M.Hash(tx, ty, wall ? 91 : 17), s = (h - .5f) * .22f + M.Noise(tx * .19f, ty * .23f, 6) * .16f; int wx = tx * TS + fx, wy = ty * TS + fy;
        // each block has its own soft bevel so the voxels stay readable
        float bev = 0; if (fx == 0 || fy == TS - 1) bev = -.20f; else if (fy == 0 || fx == TS - 1) bev = -.10f; else if (fx <= 2 || fy <= 2) bev = .13f; else if (fx >= TS - 3 || fy >= TS - 3) bev = -.10f; s += bev;
        float Pebble(int k, float max, out bool rim) { int px = 4 + (int)(M.Hash(tx, ty, 30 + k) * 15), py = 5 + (int)(M.Hash(tx, ty, 40 + k) * 14); float r = 1 + M.Hash(tx, ty, 50 + k) * max; float dx = fx - px, dy = fy - py, d = MathF.Sqrt(dx * dx + dy * dy); rim = d > r && d <= r + 1; return d <= r ? (dy < 0 || dx < 0 ? .22f : -.04f) : 0; }
        switch (t)
        {
            case T.Dirt: case T.Loam: case T.Mud: case T.RoseEarth: case T.VioletEarth: case T.Goldsoil: case T.MoonEarth:
                for (int k = 0; k < 3; k++) { float q = Pebble(k, 2.2f, out bool rim); if (q != 0) { s += q; if (k == 0) c = t switch { T.Dirt => Col.Lerp(c, 0x8a8088, .6f), T.Loam => Col.Lerp(c, 0x8a6a44, .6f), T.Mud => Col.Lerp(c, 0x8a8a70, .5f), T.RoseEarth => Col.Lerp(c, 0xf2c8cc, .6f), T.VioletEarth => Col.Lerp(c, 0xc8b0dc, .55f), T.MoonEarth => Col.Lerp(c, 0x9ad0e8, .6f), _ => Col.Lerp(c, 0xe8b860, .6f) }; } else if (rim) s -= .22f; }
                s += M.Noise(wx * .22f, wy * .3f, 8) * .09f; if (M.Hash(wx / 2, wy / 2, 3) > .95f) s -= .16f; if (t == T.Mud) s += M.Sin(wy * .4f + M.Noise(wx * .06f, wy * .05f, 5) * 5) * .05f; if (t == T.MoonEarth && M.Hash(wx, wy, 23) > .992f) c = 0xc8f0ff; break;
            case T.Stone: case T.Moss: case T.Slate:
                {
                    if (t == T.Slate) { int lv = wy + (int)(M.Noise(wx * .05f, ty * .5f, 9) * 5), lay = lv / 5; s += (M.Hash(lay, wx / 30, 5) - .5f) * .22f; if (lv % 5 == 0) s -= .2f; break; }
                    float cr = MathF.Abs(M.Noise(wx * .085f + 9, wy * .11f, 33)); if (cr < .045f) s -= .34f; else if (cr < .085f) s += .10f; int q = (int)(M.Hash(tx, ty, 61) * 4); if (q == 0 && Math.Abs(fx - fy) < 2 && fx > 5 && fx < 18) s -= .24f; if (q == 1 && fy is > 8 and < 11 && fx > 6) s -= .18f; s += M.Noise(wx * .2f, wy * .2f, 12) * .10f; if (t == T.Moss && M.Noise(wx * .11f, wy * .13f, 44) > .18f) c = Col.Lerp(c, 0x6f9a52, .55f); break;
                }
            case T.Sand: case T.Ash: s = s * .5f + M.Sin(wy * .55f + M.Noise(wx * .05f, wy * .02f, 5) * 6) * .05f; if (M.Hash(wx, wy, 2) > .96f) s -= .16f; if (M.Hash(wx, wy, 7) > .985f) s += .3f; if (t == T.Ash && M.Hash(wx / 2, wy / 2, 9) > .988f) c = 0xd0663a; break;
            case T.Sandstone: { int lv = wy + (int)(M.Noise(wx * .04f, ty * .3f, 4) * 4), lay = lv / 6; s = s * .8f + (M.Hash(lay, 3) - .5f) * .26f; if (lv % 6 == 0) s -= .18f; if (M.Hash(wx, wy, 2) > .96f) s -= .14f; break; }
            case T.RedRock: { float b = wy / 9f + M.Noise(wx * .02f, 3, 4) * 1.4f; int lay = (int)MathF.Floor(b); Col[] bands = [0xb8603f, 0xc87a4c, 0xa44e3a, 0xd89a68, 0xb0563e, 0xe0b48a, 0x9a4a44]; c = bands[(int)(M.Hash(lay, 7) * 7)]; if (b - lay < .1f) s -= .2f; s += M.Noise(wx * .2f, wy * .2f, 12) * .10f; if (M.Hash(wx, wy, 2) > .97f) s -= .14f; break; }
            case T.Clay: s += ((wy + (int)(M.Noise(wx * .06f, ty) * 6)) / 5 & 1) == 0 ? .06f : -.06f; break;
            case T.Snow: s = s * .75f + M.Noise(wx * .08f, wy * .1f, 4) * .10f; if (s < 0) c = Col.Lerp(c, 0xa8c4e0, -s * 1.8f); s = MathF.Max(s, -.06f); if (M.Hash(wx, wy, 5) > .985f) c = 0xffffff; break;
            case T.Ice: { float q = MathF.Abs(M.Noise(wx * .05f + wy * .06f, wy * .05f - wx * .03f, 8)); if (q < .04f) s += .5f; else if (q < .07f) s += .2f; if ((wx + wy + 4000) % 22 < 3) s += .14f; s += M.Noise(wx * .1f, wy * .1f, 12) * .08f; break; }
            case T.Sporesoil: for (int k = 0; k < 4; k++) { float q = Pebble(k, 1.6f, out bool rim); if (q != 0) { c = k % 2 == 0 ? 0xc8a8d8 : 0x8ad0c0; s += q; } else if (rim) s -= .2f; } s += M.Noise(wx * .2f, wy * .24f, 8) * .12f; break;
            case T.Basalt: { int bc = FDiv(wx, 8); float off = M.Hash(bc, ty / 2, 6) * 12; s += (M.Hash(bc, (int)((wy + off) / 20), 9) - .5f) * .26f; int lx = wx - bc * 8; if (lx == 0) s -= .34f; else if (lx == 1) s += .14f; if ((wy + (int)off) % 20 == 0) s -= .3f; break; }
            case T.Chalk: { float q = M.Noise(wx * .07f, wy * .16f, 3); s = s * .7f + q * .10f; if (M.Hash(wx / 3, wy / 2, 4) > .965f) c = Col.Lerp(c, 0x9a9488, .7f); if (MathF.Abs(M.Noise(wx * .06f, wy * .09f, 31)) < .03f) s -= .2f; break; }
            case T.Crystal: case T.Geode:
                {
                    // faceted: each block is cut into a few flat planes that catch the light differently
                    float ox = 12 + (M.Hash(tx, ty, 71) - .5f) * 9, oy = 12 + (M.Hash(tx, ty, 72) - .5f) * 9; float a = MathF.Atan2(fy - oy, fx - ox) + M.Hash(tx, ty, 73) * 6; int n = 5 + (int)(M.Hash(tx, ty, 74) * 3); float an = M.Mod2(a, M.TAU) / M.TAU * n; int f = (int)an; float fr = an - f; float r = MathF.Sqrt((fx - ox) * (fx - ox) + (fy - oy) * (fy - oy));
                    if (t == T.Geode) { float ring = r + M.Noise(wx * .2f, wy * .2f, 5) * 2.4f; if (ring > 9.4f) { c = 0x6a6070; s += M.Noise(wx * .2f, wy * .2f, 12) * .12f; break; } if (ring > 8) { c = 0xe8d8f0; s = 0; break; } }
                    s = bev * .6f + (M.Hash(tx * 7 + f, ty, 75) - .42f) * .7f; if (fr * r < .9f) s += .34f; if (M.Hash(wx, wy, 76) > .988f) s += .6f; break;
                }
            case T.Amberwood: { float g = M.Noise(wx * .05f, wy * .42f, 14) + M.Noise(wx * .1f, wy * .9f, 15) * .5f; s = s * .7f + g * .26f; if (M.Noise(wx * .09f, wy * .13f, 16) > .34f) c = Col.Lerp(c, 0xf4b84a, .75f); if (M.Hash(wx, wy, 18) > .99f) s += .5f; break; }
            case T.Glowrock: for (int k = 0; k < 5; k++) { float q = Pebble(k, 2, out bool rim); if (q != 0) { c = 0x8af8e8; s = .2f + q; } else if (rim) { c = 0x3fc0b4; s = 0; } } s += M.Noise(wx * .2f, wy * .2f, 12) * .10f; break;
            case T.Ember: { float cr = MathF.Abs(M.Noise(wx * .075f + 9, wy * .085f, 35) + M.Noise(wx * .2f, wy * .2f, 36) * .3f); if (cr < .07f) { c = 0xffd070; s = 0; } else if (cr < .13f) { c = 0xff8a34; s = 0; } else if (cr < .2f) { c = 0xb0482c; s *= .5f; } else { c = 0x4e3438; s += M.Noise(wx * .2f, wy * .2f, 12) * .14f; } break; }
            case T.Gravel: { float best = 99; int bk = 0; for (int k = 0; k < 9; k++) { float px = (k % 3) * 8 + 4 + (M.Hash(tx, ty, 80 + k) - .5f) * 6, py = (k / 3) * 8 + 4 + (M.Hash(tx, ty, 90 + k) - .5f) * 6; float d = (fx - px) * (fx - px) + (fy - py) * (fy - py); if (d < best) { best = d; bk = k; } } s = bev * .4f + (M.Hash(tx * 9 + bk, ty, 99) - .5f) * .5f + (best < 5 ? .14f : best > 17 ? -.4f : 0); break; }
            case T.Coral: { float q = M.Noise(wx * .24f, wy * .24f, 8); if (q > .34f) s -= .34f; else if (q > .26f) s += .14f; c = Col.Lerp(c, 0xf0c8a0, M.Clamp(M.Noise(wx * .04f, wy * .05f, 9) + .2f)); break; }
            case T.Heartstone: { float q = MathF.Abs(M.Noise(wx * .05f, wy * .06f, 8)); if (q < .03f) c = 0x8a70c8; else s += M.Noise(wx * .16f, wy * .16f, 12) * .16f; break; }
        }
        if (wall) { c = Col.Lerp(c, 0x4a4058, t is T.Ember or T.Glowrock ? .7f : .44f); s = s * .75f - .08f; }
        return c.Shade(MathF.Round(M.Clamp(s, -.8f, .8f) * 10) / 10);
    }
    /// A little chunk of the block for inventories, hands and the ground: rounded, bevelled and outlined.
    public static Painter Chunk(T t, int size)
    {
        var p = new Painter(size, size); float R = size * .30f, q = (World.TS - size) / 2f; int v = (int)t * 7 + 3;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = MathF.Max(0, MathF.Max(R - x - .5f, x + .5f - (size - R))), dy = MathF.Max(0, MathF.Max(R - y - .5f, y + .5f - (size - R))); float d = R - MathF.Sqrt(dx * dx + dy * dy); if (dx == 0 || dy == 0) d = MathF.Min(MathF.Min(x + .5f, size - x - .5f), MathF.Min(y + .5f, size - y - .5f)); if (d < 0) continue;
                var c = Face(t, v, v + 2, Math.Clamp((int)(x + q), 3, 20), Math.Clamp((int)(y + q), 3, 20), false); float e = 0; if (d < 2.6f) e = x + y < size ? .34f : -.26f; if (y < 3.2f && d > 1) e += .12f; c = c.Shade(e); if (d < 1.05f) c = c.Ink(); p.Px[y * size + x] = c.Rl();
            }
        return p;
    }
    public Texture2D? Get(int cx, int cy, bool sync)
    {
        if (cx < 0 || cy < 0 || cx * C >= World.W || cy * C >= World.H) return null; int k = cy * 1000 + cx; if (cache.TryGetValue(k, out var e)) { cache[k] = (e.t, tick); return e.t; }
        Color[] px = null; int vr = ver.GetValueOrDefault(k); lock (mu) { if (ready.Remove(k, out var r)) { pending.Remove(k); if (r.ver == vr) px = r.px; } else if (!sync && pending.Add(k)) { Task.Run(() => { var d = Paint(cx * PX, cy * PX, PX, PX); lock (mu) ready[k] = (d, vr); }); } }
        if (px == null) { if (!sync) return null; px = Paint(cx * PX, cy * PX, PX, PX); }
        unsafe { fixed (Color* p = px) { var img = new Image { Data = p, Width = PX, Height = PX, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 }; var t = Raylib.LoadTextureFromImage(img); Raylib.SetTextureFilter(t, TextureFilter.Point); cache[k] = (t, tick); return t; } }
    }
    /// Repaint the 3x3 blocks around a change, straight into the chunk textures they belong to.
    public unsafe void Refresh(int tx, int ty)
    {
        int x0 = (tx - 1) * World.TS, y0 = (ty - 1) * World.TS, sz = World.TS * 3; var px = Paint(x0, y0, sz, sz);
        for (int cy = FDiv(y0, PX); cy <= FDiv(y0 + sz - 1, PX); cy++) for (int cx = FDiv(x0, PX); cx <= FDiv(x0 + sz - 1, PX); cx++)
            {
                int k = cy * 1000 + cx; ver[k] = ver.GetValueOrDefault(k) + 1; if (!cache.TryGetValue(k, out var e)) continue; int ax = Math.Max(x0, cx * PX), ay = Math.Max(y0, cy * PX), bx = Math.Min(x0 + sz, (cx + 1) * PX), by = Math.Min(y0 + sz, (cy + 1) * PX), ww = bx - ax, hh = by - ay; if (ww <= 0 || hh <= 0) continue;
                var part = new Color[ww * hh]; for (int y = 0; y < hh; y++) Array.Copy(px, (ay - y0 + y) * sz + ax - x0, part, y * ww, ww); fixed (Color* p = part) Raylib.UpdateTextureRec(e.t, new Rectangle(ax - cx * PX, ay - cy * PX, ww, hh), p);
            }
    }
    public void Draw(float camX, float camY, float vw, float vh)
    {
        tick++; int x0 = (int)MathF.Floor(camX / PX), x1 = (int)MathF.Floor((camX + vw) / PX), y0 = (int)MathF.Floor(camY / PX), y1 = (int)MathF.Floor((camY + vh) / PX);
        for (int y = y0 - 1; y <= y1 + 1; y++) for (int x = x0 - 1; x <= x1 + 1; x++) { bool vis = x >= x0 && x <= x1 && y >= y0 && y <= y1; var t = Get(x, y, vis); if (t is Texture2D q && vis) Raylib.DrawTexture(q, x * PX - (int)camX, y * PX - (int)camY, Color.White); }
        if (cache.Count > 70) foreach (var k in cache.Where(q => q.Value.used < tick - 300).Select(q => q.Key).Take(4).ToList()) { Raylib.UnloadTexture(cache[k].t); cache.Remove(k); }
    }
}

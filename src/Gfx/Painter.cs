using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// Per-shape shading options for the painter.
public struct Ink
{
    public Func<float, float, Col> Fn;   // colour by unit position (x,y)
    public Col C;
    public float Round;                  // bevel radius in units (volume)
    public float Light;                  // strength of form light
    public float Line;                   // inner outline strength 0..1
    public float Ao;                     // drop occlusion onto what lies beneath (units)
    public float Grad;                   // top-light to bottom-dark gradient
    public float Grain;                  // painterly mottling
    public float Alpha;
    public Func<float, float, float> Tone; // extra tone offset
    public float LinePx;
    public Func<float, float, float> Merge; // 0..1: where 1, the shape melts into what is below it (no line, no edge light)
    public Ink(Col c, float round = 3, float light = .55f, float line = 1) { C = c; Fn = null; Round = round; Light = light; Line = line; Ao = 1.2f; Grad = .12f; Grain = 0; Alpha = 1; Tone = null; LinePx = 0; Merge = null; }
    public Ink(Func<float, float, Col> f, float round = 3, float light = .55f, float line = 1) : this(default(Col), round, light, line) { Fn = f; }
}

/// Software rasteriser that produces outlined, hand-shaded pixel art from vector shapes.
/// Everything in the game (avatars, clothes, trees, tiles, UI) is drawn through this.
public sealed class Painter
{
    public int W, H;
    public Color[] Px;
    public float S = 1, OX, OY;
    byte[] mask; float[] dist;
    int bx0, by0, bx1, by1; // dirty mask box (inclusive-exclusive)

    public Painter(int w, int h) { Resize(w, h); }
    public void Resize(int w, int h) { W = w; H = h; Px = new Color[w * h]; mask = new byte[w * h]; dist = new float[w * h]; stampBuf = null; ResetBox(); }
    void ResetBox() { bx0 = W; by0 = H; bx1 = 0; by1 = 0; }
    public void Clear() { Array.Clear(Px); ClearMask(); }
    void ClearMask()
    {
        for (int y = by0; y < by1; y++) Array.Clear(mask, y * W + bx0, Math.Max(0, bx1 - bx0));
        ResetBox();
    }
    public Painter View(float s, float ox, float oy) { S = s; OX = ox; OY = oy; return this; }
    Vector2 T(Vector2 p) => new(OX + p.X * S, OY + p.Y * S);
    public Painter Begin() { ClearMask(); return this; }

    void Span(int y, float x0, float x1, byte v)
    {
        if (y < 0 || y >= H) return;
        int a = (int)MathF.Ceiling(x0 - .5f), b = (int)MathF.Ceiling(x1 - .5f);
        if (a < 0) a = 0; if (b > W) b = W; if (b <= a) return;
        if (v != 0) { if (a < bx0) bx0 = a; if (b > bx1) bx1 = b; if (y < by0) by0 = y; if (y + 1 > by1) by1 = y + 1; }
        for (int i = a, o = y * W; i < b; i++) mask[o + i] = v;
    }

    [ThreadStatic] static float[] xs;
    public Painter Poly(ReadOnlySpan<Vector2> pts, byte v = 1)
    {
        xs ??= new float[128];
        Span<Vector2> q = pts.Length < 256 ? stackalloc Vector2[pts.Length] : new Vector2[pts.Length];
        float mn = 1e9f, mx = -1e9f;
        for (int i = 0; i < pts.Length; i++) { q[i] = T(pts[i]); mn = MathF.Min(mn, q[i].Y); mx = MathF.Max(mx, q[i].Y); }
        int y0 = Math.Max(0, (int)mn - 1), y1 = Math.Min(H - 1, (int)mx + 1);
        for (int y = y0; y <= y1; y++)
        {
            float cy = y + .5f; int n = 0;
            for (int i = 0, j = q.Length - 1; i < q.Length; j = i++)
                if ((q[i].Y <= cy) != (q[j].Y <= cy) && n < 127)
                    xs[n++] = q[i].X + (cy - q[i].Y) / (q[j].Y - q[i].Y) * (q[j].X - q[i].X);
            Array.Sort(xs, 0, n);
            for (int i = 0; i + 1 < n; i += 2) Span(y, xs[i], xs[i + 1], v);
        }
        return this;
    }
    public Painter Poly(params Vector2[] p) => Poly((ReadOnlySpan<Vector2>)p, 1);
    public Painter Cut(params Vector2[] p) => Poly((ReadOnlySpan<Vector2>)p, 0);

    /// Closed Catmull-Rom blob through points.
    public Painter Blob(ReadOnlySpan<Vector2> p, byte v = 1, int seg = 5)
    {
        int n = p.Length; var o = new Vector2[n * seg]; int k = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = p[(i + n - 1) % n], b = p[i], c = p[(i + 1) % n], d = p[(i + 2) % n];
            for (int j = 0; j < seg; j++) o[k++] = M.Catmull(a, b, c, d, j / (float)seg);
        }
        return Poly((ReadOnlySpan<Vector2>)o, v);
    }
    public Painter Blob(params Vector2[] p) => Blob((ReadOnlySpan<Vector2>)p, 1);
    public Painter CutBlob(params Vector2[] p) => Blob((ReadOnlySpan<Vector2>)p, 0);

    public Painter Ellipse(float cx, float cy, float rx, float ry, byte v = 1)
    {
        var c = T(new(cx, cy)); rx = MathF.Max(rx * S, .5f); ry = MathF.Max(ry * S, .5f);
        int y0 = (int)(c.Y - ry) - 1, y1 = (int)(c.Y + ry) + 1;
        for (int y = y0; y <= y1; y++)
        {
            float d = (y + .5f - c.Y) / ry; if (d * d >= 1) continue;
            float w = rx * MathF.Sqrt(1 - d * d); Span(y, c.X - w, c.X + w, v);
        }
        return this;
    }
    public Painter CutEllipse(float cx, float cy, float rx, float ry) => Ellipse(cx, cy, rx, ry, 0);
    public Painter Rect(float x, float y, float w, float h, byte v = 1) { ReadOnlySpan<Vector2> p = [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)]; return Poly(p, v); }

    public Painter Capsule(Vector2 a, float ra, Vector2 b, float rb, byte v = 1)
    {
        Ellipse(a.X, a.Y, ra, ra, v); Ellipse(b.X, b.Y, rb, rb, v);
        var d = b - a; float l = d.Length(); if (l < .001f) return this;
        var n = new Vector2(-d.Y, d.X) / l;
        ReadOnlySpan<Vector2> p = [a + n * ra, b + n * rb, b - n * rb, a - n * ra];
        return Poly(p, v);
    }
    /// Tapered tube along a point list.
    public Painter Tube(ReadOnlySpan<Vector2> p, ReadOnlySpan<float> r, byte v = 1)
    {
        for (int i = 0; i + 1 < p.Length; i++) Capsule(p[i], r[i], p[i + 1], r[i + 1], v);
        return this;
    }
    public byte[] CopyMask() => (byte[])mask.Clone();
    public Painter IntersectMask(byte[] m)
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++) if (m[y * W + x] == 0) mask[y * W + x] = 0;
        return this;
    }
    public Painter IntersectAlpha()
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++) if (Px[y * W + x].A == 0) mask[y * W + x] = 0;
        return this;
    }
    public Painter SubtractMask(byte[] m)
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++) if (m[y * W + x] != 0) mask[y * W + x] = 0;
        return this;
    }
    public bool MaskAt(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && mask[y * W + x] != 0;

    static readonly Vector2 L = Vector2.Normalize(new(-.55f, -.83f));

    public void Shade(in Ink k)
    {
        if (bx1 <= bx0) return;
        int x0 = Math.Max(0, bx0 - 1), y0 = Math.Max(0, by0 - 1), x1 = Math.Min(W, bx1 + 1), y1 = Math.Min(H, by1 + 1);
        // chamfer distance to edge (pixels)
        for (int y = y0; y < y1; y++)
            for (int x = x0, o = y * W + x0; x < x1; x++, o++)
            {
                if (mask[o] == 0) { dist[o] = 0; continue; }
                float d = 1e6f;
                if (x > x0) d = MathF.Min(d, dist[o - 1] + 1); else d = 1;
                if (y > y0) { d = MathF.Min(d, dist[o - W] + 1); if (x > x0) d = MathF.Min(d, dist[o - W - 1] + 1.41f); if (x + 1 < x1) d = MathF.Min(d, dist[o - W + 1] + 1.41f); } else d = 1;
                dist[o] = d;
            }
        for (int y = y1 - 1; y >= y0; y--)
            for (int x = x1 - 1, o = y * W + x1 - 1; x >= x0; x--, o--)
            {
                if (mask[o] == 0) continue;
                float d = dist[o];
                if (x + 1 < x1) d = MathF.Min(d, dist[o + 1] + 1); else d = MathF.Min(d, 1);
                if (y + 1 < y1) { d = MathF.Min(d, dist[o + W] + 1); if (x + 1 < x1) d = MathF.Min(d, dist[o + W + 1] + 1.41f); if (x > x0) d = MathF.Min(d, dist[o + W - 1] + 1.41f); } else d = MathF.Min(d, 1);
                dist[o] = d;
            }
        // occlusion
        if (k.Ao > 0)
        {
            int ao = Math.Max(1, (int)MathF.Round(k.Ao * S));
            for (int y = by0; y < Math.Min(H, by1 + ao); y++)
                for (int x = bx0; x < bx1; x++)
                {
                    int o = y * W + x; if (mask[o] != 0 || Px[o].A == 0) continue;
                    if (k.Merge != null && k.Merge((x + .5f - OX) / S, (y + .5f - OY) / S) > .4f) continue;
                    for (int j = 1; j <= ao; j++)
                        if (y - j >= 0 && mask[o - j * W] != 0) { Mul(o, .70f + .2f * (j - 1f) / ao); break; }
                }
        }
        float rp = MathF.Max(1.5f, k.Round * S);
        float h = MathF.Max(1, (by1 - by0));
        float linePx = k.LinePx > 0 ? k.LinePx : MathF.Max(1, S * .42f);
        for (int y = by0; y < by1; y++)
            for (int x = bx0; x < bx1; x++)
            {
                int o = y * W + x; if (mask[o] == 0) continue;
                float ux = (x + .5f - OX) / S, uy = (y + .5f - OY) / S;
                Col c = k.Fn != null ? k.Fn(ux, uy) : k.C;
                float d = dist[o], t = 0, mg = k.Merge != null ? k.Merge(ux, uy) : 0;
                if (mg > .99f && Px[o].A > 0 && d <= linePx + .01f) continue;
                if (d < rp)
                {
                    int g = rp > 4 ? 3 : rp > 2.5f ? 2 : 1;
                    float gx = 0, gy = 0;
                    for (int q = -1; q <= 1; q++)
                    {
                        gx += D(x + g, y + q * (g - 1)) - D(x - g, y + q * (g - 1)); gy += D(x + q * (g - 1), y + g) - D(x + q * (g - 1), y - g);
                    }
                    gx /= 3 * g; gy /= 3 * g;
                    t = (gx * -L.X + gy * -L.Y) * .5f * (1 - d / rp) * k.Light; // gradient points inward; lit if edge faces light
                }
                t *= 1 - mg;
                t -= ((y - by0) / h - .4f) * k.Grad * 1.3f * (1 - mg);
                if (k.Tone != null) t += k.Tone(ux, uy);
                if (k.Grain > 0) t += (M.Noise(x * .35f, y * .35f, 5) * .6f + (M.Hash(x, y, 3) - .5f) * .6f) * k.Grain;
                t = MathF.Round(t * 9) / 9;
                c = c.Shade(M.Clamp(t * 1.15f, -.9f, .9f));
                if (k.Line > 0 && d <= linePx + .01f) c = Col.Lerp(c, c.Ink(), k.Line * .86f * (1 - mg));
                Put(o, c, k.Alpha);
            }
        float D(int x, int y) => x < x0 || y < y0 || x >= x1 || y >= y1 ? 0 : dist[y * W + x];
    }
    public void Flat(Col c, float a = 1)
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++) if (mask[y * W + x] != 0) Put(y * W + x, c, a);
    }
    /// Multiplies what lies underneath (shadows, folds); only touches opaque pixels.
    public void Mult(float f)
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++) if (mask[y * W + x] != 0 && Px[y * W + x].A > 0) Mul(y * W + x, f);
    }
    public void Tint(Col c, float a)
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++)
            { int o = y * W + x; if (mask[o] != 0 && Px[o].A > 0) { var al = Px[o].A; Put(o, c, a); Px[o].A = al; } }
    }
    public void Erase()
    {
        for (int y = by0; y < by1; y++) for (int x = bx0; x < bx1; x++) if (mask[y * W + x] != 0) Px[y * W + x] = default;
    }
    void Mul(int o, float f)
    {
        ref var p = ref Px[o];
        p.R = (byte)M.Clamp(p.R * (f * 1.02f), 0, 255); p.G = (byte)M.Clamp(p.G * (f * .97f), 0, 255); p.B = (byte)M.Clamp(p.B * (f * 1.04f), 0, 255);
    }
    void Put(int o, Col c, float a)
    {
        ref var p = ref Px[o];
        if (a >= .999f || p.A == 0) p = c.Rl(a >= .999f ? 1 : a);
        else { float ia = 1 - a; p = new Color((byte)M.Clamp(p.R * ia + c.R * 255 * a, 0, 255), (byte)M.Clamp(p.G * ia + c.G * 255 * a, 0, 255), (byte)M.Clamp(p.B * ia + c.B * 255 * a, 0, 255), (byte)Math.Max((int)p.A, (int)(a * 255))); }
    }
    public void Dot(float ux, float uy, Col c, float a = 1) { var p = T(new(ux, uy)); int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y); if (x >= 0 && y >= 0 && x < W && y < H) Put(y * W + x, c, a); }

    /// Hard stroke, optionally clipped to the current mask. mult != 0 multiplies underlying colour.
    public void Stroke(ReadOnlySpan<Vector2> pts, float widthU, Col c, float a = 1, bool clip = true, float mult = 0)
    {
        float r = MathF.Max(.5f, widthU * S * .5f);
        stampId++;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            Vector2 p = T(pts[i]), q = T(pts[i + 1]); float len = MathF.Max(1, Vector2.Distance(p, q)); int n = (int)MathF.Ceiling(len * 1.5f);
            int lx = int.MinValue, ly = 0;
            for (int j = 0; j <= n; j++)
            {
                var m = Vector2.Lerp(p, q, j / (float)n);
                int cx = (int)MathF.Floor(m.X), cy = (int)MathF.Floor(m.Y);
                if (r <= .8f)
                {
                    if (cx == lx && cy == ly) continue; lx = cx; ly = cy;
                    Plot(cx, cy);
                }
                else for (int y = (int)(m.Y - r); y <= m.Y + r; y++) for (int x = (int)(m.X - r); x <= m.X + r; x++)
                            if ((x + .5f - m.X) * (x + .5f - m.X) + (y + .5f - m.Y) * (y + .5f - m.Y) <= r * r) Plot(x, y);
            }
        }
        void Plot(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return; int o = y * W + x;
            if (clip && mask[o] == 0) return;
            if (!Stamp(o)) return;
            if (mult != 0) { if (Px[o].A > 0) Mul(o, mult); } else Put(o, c, a);
        }
    }
    int stampId; int[] stampBuf;
    bool Stamp(int o) { stampBuf ??= new int[W * H]; if (stampBuf[o] == stampId) return false; stampBuf[o] = stampId; return true; }
    public void Crease(Vector2 a, Vector2 b, float w = .6f, float m = .78f) => Stroke([a, b], w, default, 1, true, m);
    public void Line(Vector2 a, Vector2 b, float w, Col c, bool clip = true, float al = 1) => Stroke([a, b], w, c, al, clip);
    public void Curve(Vector2 a, Vector2 m, Vector2 b, float w, Col c, bool clip = true, float al = 1, float mult = 0)
    {
        Span<Vector2> p = stackalloc Vector2[9];
        for (int i = 0; i < 9; i++) { float t = i / 8f; p[i] = (1 - t) * (1 - t) * a + 2 * t * (1 - t) * m + t * t * b; }
        Stroke(p, w, c, al, clip, mult);
    }

    /// Adds a silhouette outline around everything drawn so far.
    public void Outline(Col c, int px, bool diag = false)
    {
        var src = new byte[W * H]; var rc = c.Rl();
        for (int it = 0; it < px; it++)
        {
            for (int i = 0; i < src.Length; i++) src[i] = Px[i].A > 60 ? (byte)1 : (byte)0;
            bool dg = diag || (it & 1) == 1;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    int o = y * W + x; if (src[o] != 0) continue;
                    bool n = (x > 0 && src[o - 1] != 0) || (x + 1 < W && src[o + 1] != 0) || (y > 0 && src[o - W] != 0) || (y + 1 < H && src[o + W] != 0);
                    if (!n && dg) n = (x > 0 && y > 0 && src[o - W - 1] != 0) || (x + 1 < W && y > 0 && src[o - W + 1] != 0) || (x > 0 && y + 1 < H && src[o + W - 1] != 0) || (x + 1 < W && y + 1 < H && src[o + W + 1] != 0);
                    if (n) Px[o] = rc;
                }
        }
    }
    public unsafe Texture2D ToTexture()
    {
        fixed (Color* c = Px)
        {
            var img = new Image { Data = c, Width = W, Height = H, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
            var t = Raylib.LoadTextureFromImage(img); Raylib.SetTextureFilter(t, TextureFilter.Point); return t;
        }
    }
    public unsafe void Upload(Texture2D t) { fixed (Color* c = Px) Raylib.UpdateTexture(t, c); }
    public unsafe void Export(string path)
    {
        fixed (Color* c = Px)
        {
            var img = new Image { Data = c, Width = W, Height = H, Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8 };
            Raylib.ExportImage(img, path);
        }
    }
}

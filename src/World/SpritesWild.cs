using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// Trees, plants and stones that belong to particular biomes.
public static partial class Sprites
{
    static Sprite Leafy(float h, float w0, float w1, float bend, Col bark, Col[] leaf, float spread, int n, int seed, float flat = .5f, float size = 22, bool birch = false)
    {
        var s = new Sprite { H = h }; var r = new Rng(seed); s.Parts.Add(Trunk(h * .86f, w0, w1, bend, bark, seed, 0, birch));
        for (int i = 0; i < n; i++) { float a = i * M.TAU / n + r.R(-.2f, .2f), q = i == 0 ? 0 : r.R(.6f, 1), x = M.Sin(a) * spread * q + bend * .6f, y = -h + M.Cos(a) * spread * q * flat; float R = size * r.R(.85f, 1.15f); s.Parts.Add(Clump(R * 1.14f, R * .88f, leaf[r.I(leaf.Length)], seed * 10 + i, 1.4f + (-y - h * .6f) * .03f, i * .9f, x, y)); }
        s.Parts.Add(Clump(size * 1.2f, size * .95f, leaf[0].Shade(.08f), seed * 10 + 55, 2, 3, bend * .6f + r.R(-5, 5), -h - 4)); return s;
    }
    static Sprite Rock(int v, Col c, Col? cap, int seed)
    {
        var r = new Rng(seed); return One(26, 30, 26, 3, p =>
        {
            float h = 12 + v * 4f, w = 12 + v * 3f, j = r.R(-3, 3); Vector2 bl = V(-w, 2), ml = V(-w * .95f, -h * .4f), tl = V(-w * .4f + j, -h), tr = V(w * .36f + j, -h * .86f), mr = V(w * .9f, -h * .36f), br = V(w, 2), ci = V(-w * .14f + j, -h * .54f), cr = V(w * .42f, -h * .4f), cb = V(w * .3f, 2);
            Ink F(Col k, float s) => new Ink((x, y) => k.Shade(s + M.Noise(x * .3f, y * .3f, v + seed) * .12f), 2, .25f, 0) { Ao = 0, Grad = .12f }; p.Begin().Poly(bl, ml, tl, tr, mr, br); p.Shade(F(c, -.06f)); p.Begin().Poly(ml, tl, tr, cr, ci); p.Shade(F(c, .28f)); p.Begin().Poly(bl, ml, ci, V(-w * .34f, 2)); p.Shade(F(c, .06f)); p.Begin().Poly(cr, mr, br, cb); p.Shade(F(c, -.42f)); p.Begin().Poly(cr, tr, mr); p.Shade(F(c, -.2f));
            p.Begin(); p.Line(ml, ci, 1, c.Shade(.5f), false); p.Line(ci, cr, 1, c.Shade(.4f), false); p.Line(cr, cb, 1, c.Shade(-.55f), false); p.Line(tl, ci, 1, c.Shade(.12f), false);
            if (cap is Col k2) { p.Begin().Blob(tl + V(-4, 3), tl + V(1, -2.4f), V(0, -h * 1.04f), tr + V(2, -1), tr + V(0, 4), V(j, -h * .68f), tl + V(2, 5)); p.Shade(new Ink(k2, 2, .6f, 0) { Ao = 0 }); }
            p.Begin().Rect(-26, 1.4f, 52, 4); p.Erase();
        });
    }
    static Sprite Fir(int v, Col c, bool snow)
    {
        var s = new Sprite(); var r = new Rng(v * 31 + 7); float h = r.R(104, 150); s.H = h; s.Parts.Add(Trunk(h * .8f, 6, 2, r.R(-3, 3), 0x5e4a46, v + 3)); int tiers = 5;
        for (int i = 0; i < tiers; i++)
        {
            float t = i / (float)(tiers - 1), y = -M.Lerp(h * .28f, h * .98f, t), w = M.Lerp(44, 14, t), hh = M.Lerp(40, 30, t); int seed = v * 10 + i; Col cc = c.Shade(-.1f + t * .16f); int mw = (int)w + 6;
            var part = Paint(mw, 36, mw, 22, p =>
            {
                var q = new List<Vector2> { V(0, -hh * .75f) }; for (int k = 1; k <= 4; k++) { float u = k / 4f; q.Add(V(w * u, M.Lerp(-hh * .5f, 8, u))); if (k < 4) q.Add(V(w * u - 5, M.Lerp(-hh * .5f, 8, u) + 2.6f)); } for (int k = 0; k <= 6; k++) q.Add(V(M.Lerp(w, -w, k / 6f), 8 + (k % 2 == 0 ? 4 : -1) + M.Sin(k / 6f * M.PI) * 5)); for (int k = 4; k >= 1; k--) { float u = k / 4f; if (k < 4) q.Add(V(-w * u + 5, M.Lerp(-hh * .5f, 8, u) + 2.6f)); q.Add(V(-w * u, M.Lerp(-hh * .5f, 8, u))); }
                p.Begin().Poly(q.ToArray()); p.Shade(new Ink((x, y2) => cc.Shade(-x / w * .16f + M.Noise(x * .2f, y2 * .25f, seed) * .14f), 6, .5f, .7f) { Ao = 0, Grad = .3f });
                if (snow) { var m = new List<Vector2> { V(0, -hh * .75f) }; for (int k = 1; k <= 4; k++) { float u = k / 4f; m.Add(V(w * u, M.Lerp(-hh * .5f, 8, u))); } for (int k = 0; k <= 8; k++) { float u = k / 8f * 2 - 1; m.Add(V(-u * w * .86f, M.Lerp(-hh * .5f, 6, MathF.Abs(u)) + 4 + (k % 2) * 5)); } for (int k = 4; k >= 1; k--) { float u = k / 4f; m.Add(V(-w * u, M.Lerp(-hh * .5f, 8, u))); } p.Begin().Poly(m.ToArray()); p.IntersectAlpha(); p.Shade(new Ink(0xf2f8ff, 4, .55f, 0) { Ao = 1.4f, Grad = .14f }); }
            }, .4f + t * 1.6f, i * .35f); part.OY += y; s.Parts.Add(part);
        }
        return s;
    }
    /// A long crystal shard from base to tip with a lit and a shaded face.
    static void Shard(Painter p, float x, float w, float h, float lean, Col c)
    {
        Vector2 t = V(x + lean * h, -h), l = V(x - w, 2), r = V(x + w, 2), lm = V(x - w + lean * h * .72f, -h * .76f), rm = V(x + w + lean * h * .72f, -h * .72f), mid = V(x + w * .15f, 2), mm = V(x + w * .15f + lean * h * .72f, -h * .66f);
        p.Begin().Poly(l, lm, t, mm, mid); p.Shade(new Ink((a, b) => c.Shade(.16f - b / h * .3f + M.Noise(a * .2f, b * .12f, 3) * .14f), 2, .5f, .3f) { Ao = 0, Grad = 0 }); p.Begin().Poly(mid, mm, t, rm, r); p.Shade(new Ink((a, b) => c.Shade(-.26f - b / h * .2f), 2, .3f, .3f) { Ao = 0, Grad = 0 }); p.Line(mid, mm, 1, c.Shade(.75f), false); p.Line(mm, t, 1, c.Shade(.85f), false); p.Line(lm, t, 1, c.Shade(.6f), false); p.Line(V(x - w * .5f, -h * .2f), V(x - w * .5f + lean * h * .3f, -h * .5f), 1, c.Shade(.9f), false);
    }
    static Sprite Wild(string k, int v, Rng r)
    {
        switch (k)
        {
            // -------------------------------------------------------- trees
            case "cherry": return Leafy(r.R(86, 112), 8.5f, 3.4f, r.R(-16, 16), 0x5e4040, [0xf2b4c6, 0xf8d2dc, 0xe898b0], 46, 10, v * 19 + 5, .42f, 21);
            case "snowfir": return Fir(v, 0x4a7470, true);
            case "olive": return Leafy(r.R(62, 78), 7, 3, r.R(-9, 9), 0x8a7a68, [0x8f9c72, 0xa2ae84, 0x7c8a64], 30, 7, v * 23 + 2, .4f, 16);
            case "cactus":
                return One(40, 100, 40, 3, p =>
                {
                    Col c = v == 1 ? 0x6a9a58 : 0x5a8c5a; float h = 66 + v * 13; Ink K2() => new Ink((x, y) => c.Shade(M.Mod((int)MathF.Floor(x + 60), 5) == 0 ? -.28f : M.Mod((int)MathF.Floor(x + 60), 5) == 2 ? .14f : 0), 6, .75f, .8f) { Ao = 1 };
                    void Arm(float s, float y, float L, float up) { p.Begin().Tube([V(0, y), V(s * L * .7f, y + 1), V(s * L, y - 6), V(s * L, y - up)], [5.4f, 5.4f, 5.6f, 5]); p.Shade(K2()); } Arm(-1, -h * .42f, 21, 30); if (v != 2) Arm(1, -h * .55f, 19, 24); else Arm(1, -h * .34f, 23, 38); p.Begin().Capsule(V(0, 2), 9.5f, V(0, -h), 8.4f); p.Shade(K2()); for (int i = 0; i < 26; i++) { float x = r.R(-9, 9), y = -r.R(4, h); p.Line(V(x, y), V(x + (x < 0 ? -2 : 2), y - 1.6f), .9f, 0xf3ead0, false); }
                    p.Begin(); for (int q = 0; q < 6; q++) { float a = q * M.TAU / 6; p.Ellipse(M.Sin(a) * 3.6f, -h - 9 + M.Cos(a) * 2, 2.6f, 2.4f); } p.Shade(K(v == 0 ? 0xe86a90 : 0xf0b84a, 2, .6f, 0)); p.Begin().Rect(-14, 2, 28, 8); p.Erase();
                });
            case "juniper":
                {
                    var s = new Sprite { H = 70 }; float b = v % 2 == 0 ? 16 : -14; s.Parts.Add(Paint(46, 78, 46, 3, p => { p.Begin().Tube([V(0, 2), V(-b * .35f, -18), V(b * .5f, -40), V(b, -58)], [8, 6.2f, 5, 2.6f]).Tube([V(b * .2f, -30), V(-b * .9f, -46), V(-b * 1.5f, -52)], [4, 3, 1.6f]); p.Shade(new Ink((x, y) => ((Col)0x8a6250).Shade(M.Noise(x * .5f + y * .16f, y * .06f, v) * .34f), 5, .7f, .85f) { Ao = 0 }); }));
                    foreach (var (x, y, w) in new[] { (b, -62f, 25f), (-b * 1.5f, -55f, 20f), (b * .3f, -50f, 15f) }) s.Parts.Add(Clump(w, w * .46f, (Col)(v == 1 ? 0x6a8a68 : 0x5f7f5a), v * 9 + (int)x + 90, 1, x * .1f, x, y)); return s;
                }
            case "jungle":
                {
                    float h = r.R(158, 206); var s = new Sprite { H = h }; float bend = r.R(-10, 10); s.Parts.Add(Trunk(h * .9f, 11, 5.4f, bend, 0x6a5844, v + 40)); s.Parts.Add(Paint(34, 40, 34, 4, p => { foreach (var sd in new[] { -1f, 1f }) { p.Begin().Poly(V(sd * 4, -36), V(sd * 9, -14), V(sd * 28, 2), V(sd * 2, 3)); p.Shade(Wood(0x66543f, 4)); } p.Begin().Ellipse(-12, -2, 8, 3).Ellipse(14, -3, 6, 3); p.IntersectAlpha(); p.Tint(0x5f9a44, .7f); }));
                    Col[] lf = [0x3f7a38, 0x4f8f40, 0x2f6836]; for (int i = 0; i < 10; i++) { float a = i * M.TAU / 10, q = i == 0 ? 0 : r.R(.7f, 1); float x = M.Sin(a) * 58 * q + bend, y = -h + M.Cos(a) * 20 * q; s.Parts.Add(Clump(r.R(24, 32), r.R(17, 22), lf[r.I(3)], v * 50 + i, 1.6f, i * .8f, x, y)); }
                    for (int i = 0; i < 6; i++) { float x = r.R(-60, 60), L = r.R(30, 84); int sd = i + v * 9; var part = Paint(8, 2, 8, (int)L + 6, p => { var rr = new Rng(sd); p.Curve(V(0, 0), V(rr.R(-4, 4), L * .5f), V(rr.R(-2, 2), L), 1.8f, 0x4f7a3a, false); for (float y = 4; y < L; y += 6) { p.Begin().Ellipse((int)y % 12 < 6 ? 2.4f : -2.4f, y, 2.6f, 1.8f); p.Flat((int)y % 9 < 5 ? 0x78b050 : 0x5a9044); } }, -3, i); part.Pivot = -1; part.OX += x + bend; part.OY += -h + 12; s.Parts.Add(part); }
                    return s;
                }
            case "mangrove":
                {
                    float h = r.R(92, 118); var s = new Sprite { H = h }; float b = r.R(-10, 10); s.Parts.Add(Paint(44, (int)h + 4, 44, 4, p => { for (int i = 0; i < 7; i++) { float u = (i - 3) / 3f; p.Begin().Tube([V(u * 34, 4), V(u * 26, -14), V(u * 9, -32 - (3 - MathF.Abs(i - 3)) * 3), V(b * .2f, -46)], [2.6f, 3, 3.4f, 4]); p.Shade(Wood(0x6a5c4c, 2)); } p.Begin().Tube([V(0, -38), V(b * .4f, -h * .6f), V(b, -h * .86f)], [7, 5.4f, 3.2f]).Tube([V(b * .4f, -h * .6f), V(-18, -h * .8f), V(-28, -h * .86f)], [4, 3, 1.6f]).Tube([V(b * .4f, -h * .64f), V(22, -h * .82f), V(30, -h * .9f)], [4, 3, 1.6f]); p.Shade(Wood(0x6a5c4c, 4)); }));
                    Col[] lf = [0x66804a, 0x7a9256, 0x556f44]; foreach (var (x, y, w) in new[] { (b, -h, 28f), (-30f, -h * .9f, 21f), (32f, -h * .93f, 22f), (-8f, -h * 1.08f, 20f), (16f, -h * 1.06f, 20f) }) s.Parts.Add(Clump(w, w * .7f, lf[r.I(3)], v * 13 + (int)x + 60, 1.4f, x * .1f, x, y));
                    for (int i = 0; i < 5; i++) { float x = -34 + i * 17 + r.R(-5, 5), L = r.R(16, 40); int sd = i * 3 + v; var part = Paint(8, 2, 8, (int)L + 4, p => { var rr = new Rng(sd); for (int q = 0; q < 3; q++) p.Curve(V(q * 2 - 2, 0), V(rr.R(-4, 4), L * .5f), V(rr.R(-3, 3), L * rr.R(.6f, 1)), 1.2f, 0x9aa682, false); }, -3, i); part.Pivot = -1; part.OX += x; part.OY += -h * .86f; s.Parts.Add(part); }
                    return s;
                }
            case "bamboo":
                {
                    float h = r.R(120, 172); var s = new Sprite { H = h }; int n = 2 + r.I(2);
                    for (int i = 0; i < n; i++)
                    {
                        float x = (i - (n - 1) / 2f) * 9 + r.R(-2, 2), hh = h * r.R(.74f, 1), lean = r.R(-12, 12); Col c = i % 2 == 0 ? 0x86b04a : 0x9cbc58; int sd = v * 7 + i;
                        var part = Paint(34, (int)hh + 14, 34, 4, p => { int seg = (int)(hh / 17); for (int q = 0; q < seg; q++) { float t0 = q / (float)seg, t1 = (q + 1) / (float)seg; var a = V(x + lean * t0 * t0, -hh * t0 + 2); var b2 = V(x + lean * t1 * t1, -hh * t1 + 2); p.Begin().Capsule(a, M.Lerp(3.3f, 2, t0), b2, M.Lerp(3.3f, 2, t1)); p.Shade(new Ink(c.Shade(q % 2 == 0 ? .05f : -.08f), 2, .9f, .7f) { Ao = 0 }); p.Line(b2 + V(-3, 0), b2 + V(3, 0), 1.2f, c.Shade(-.45f), false); p.Line(b2 + V(-3, 1.4f), b2 + V(2, 1.4f), 1, c.Shade(.45f), false); } var rr = new Rng(sd); for (int q = 0; q < 17; q++) { float t = rr.R(.42f, 1); var o = V(x + lean * t * t, -hh * t); float sd2 = rr.P(.5f) ? 1 : -1; var e = o + V(sd2 * rr.R(9, 19), rr.R(-7, 8)); var nn = Vector2.Normalize(V(e.Y - o.Y, o.X - e.X)) * 2.2f; p.Begin().Poly(o, (o + e) / 2 + nn, e, (o + e) / 2 - nn); p.Shade(new Ink(((Col)0x8fc04e).Shade(rr.R(-.25f, .25f)), 1, .5f, .4f) { Ao = 0 }); } }, 2.2f, i * 1.2f); s.Parts.Add(part);
                    }
                    return s;
                }
            case "cypress": { float h = r.R(104, 148); var s = new Sprite { H = h }; s.Parts.Add(Trunk(h * .3f, 4.6f, 3.4f, 0, 0x6a5244, v + 8)); for (int i = 0; i < 7; i++) { float t = i / 6f, w = (M.Sin(M.Lerp(.35f, 3.0f, t)) * .72f + .32f) * 15; s.Parts.Add(Clump(w, 15, ((Col)0x4f7452).Shade(-.1f + t * .16f), v * 9 + i, .3f + t * 1.9f, t * 1.4f, 0, -M.Lerp(h * .2f, h * .94f, t))); } return s; }
            case "toadstool":
                {
                    float h = r.R(70, 118); var s = new Sprite { H = h }; Col c = v == 0 ? 0xd0506a : v == 1 ? 0x4fa8a0 : 0x9a68c0; float b = r.R(-10, 10), R = r.R(40, 54); s.Parts.Add(Paint(30, (int)h + 4, 30, 4, p => { p.Begin().Tube([V(0, 3), V(b * .2f, -h * .4f), V(b, -h * .92f)], [11, 7.6f, 8.6f]); p.Shade(new Ink((x, y) => ((Col)0xf0e2d0).Shade(M.Noise(x * .6f, y * .05f, v) * .16f), 6, .7f, .85f) { Ao = 0 }); p.Begin().Blob(V(b - 16, -h * .70f), V(b, -h * .74f), V(b + 16, -h * .70f), V(b + 11, -h * .62f), V(b, -h * .65f), V(b - 11, -h * .62f)); p.Shade(K(0xe0ccc0, 3, .5f)); }));
                    var cap = Paint((int)R + 6, 50, (int)R + 6, 22, p => { p.Begin().Blob(V(-R, 8), V(-R * .84f, -14), V(-R * .4f, -34), V(0, -40), V(R * .4f, -34), V(R * .84f, -14), V(R, 8), V(R * .5f, 13), V(0, 15), V(-R * .5f, 13)); p.Shade(new Ink((x, y) => c.Shade(M.Noise(x * .06f, y * .08f, v) * .14f), 12, .6f, .9f) { Ao = 0, Grad = .35f }); p.Begin().Blob(V(-R * .93f, 8), V(-R * .4f, 5), V(0, 6), V(R * .4f, 5), V(R * .93f, 8), V(R * .4f, 13), V(0, 15), V(-R * .4f, 13)); p.Shade(new Ink((x, y) => ((Col)0xf6e6d4).Shade(M.Mod((int)MathF.Floor(x + 80), 5) == 0 ? -.3f : 0), 3, .4f, .5f) { Ao = 0 }); foreach (var (x, y, q) in new[] { (-.5f, -14f, 6f), (.1f, -27f, 7f), (.55f, -11f, 5.4f), (-.14f, -8f, 4.4f), (-.74f, 0f, 3.6f), (.3f, -4f, 3) }) { p.Begin().Ellipse(x * R, y, q, q * .8f); p.IntersectAlpha(); p.Shade(new Ink(0xfdf4e4, 2, .5f, .4f) { Ao = 0 }); } }, .5f, 1); cap.OX += b; cap.OY += -h * .95f; s.Parts.Add(cap); return s;
                }
            case "charred":
                {
                    float h = r.R(76, 118); return One(52, (int)h + 14, 52, 4, p => { float b = r.R(-12, 12); p.Begin().Tube([V(0, 3), V(b * .3f, -h * .4f), V(b, -h * .8f), V(b * 1.4f, -h)], [8, 5.4f, 3.6f, 1.4f]); for (int i = 0; i < 6; i++) { float t = r.R(.38f, .88f), sd = i % 2 == 0 ? 1 : -1; var a = V(b * t, -h * t); var m = a + V(sd * r.R(12, 20), -r.R(2, 10)); p.Tube([a, m, m + V(sd * r.R(5, 14), -r.R(10, 20))], [3, 2.2f, 1]); } p.Shade(new Ink((x, y) => { float q = MathF.Abs(M.Noise(x * .16f, y * .07f, v + 3)); return q < .05f && y > -h * .7f ? (Col)0xff9a40 : q < .09f && y > -h * .7f ? 0xb04a2c : ((Col)0x42383e).Shade(M.Noise(x * .5f, y * .08f, v) * .3f); }, 4, .6f, .8f) { Ao = 0 }); });
                }
            case "spire": return One(40, 128, 40, 3, p => { Col[] cs = [0xa4c8f0, 0xc4b0f0, 0x98e0e4]; Col c = cs[v]; float H = r.R(78, 120); Shard(p, -14, 7, H * .62f, -.16f, c.Shade(-.08f)); Shard(p, 15, 6.4f, H * .5f, .2f, c.Shade(-.04f)); Shard(p, 1, 9.5f, H, r.R(-.06f, .06f), c); Shard(p, -23, 4, H * .3f, -.34f, c); Shard(p, 24, 4, H * .26f, .36f, c); p.Begin().Rect(-40, 2, 80, 5); p.Erase(); });
            case "moontree":
                {
                    float h = r.R(94, 128); var s = new Sprite { H = h }; float b = r.R(-14, 14); s.Parts.Add(Trunk(h * .86f, 8, 3.2f, b, 0xb0b8d0, v + 70)); Col[] lf = v == 1 ? [0x7a70c8, 0x9488dc, 0x645cb0] : [0x3f90a8, 0x58b0c0, 0x347a98]; for (int i = 0; i < 9; i++) { float a = i * M.TAU / 9, q = i == 0 ? 0 : r.R(.6f, 1), x = M.Sin(a) * 44 * q + b * .6f, y = -h + M.Cos(a) * 24 * q; s.Parts.Add(Clump(r.R(20, 26), r.R(16, 21), lf[r.I(3)], v * 60 + i, 1.8f, i, x, y)); }
                    for (int i = 0; i < 7; i++) { float x = -46 + i * 15 + r.R(-4, 4), L = r.R(12, 34); var part = Paint(6, 2, 6, (int)L + 10, p => { p.Line(V(0, 0), V(0, L), 1, 0x8ab8d0, false); p.Begin().Blob(V(-3, L), V(0, L - 2), V(3, L), V(2, L + 5), V(0, L + 6.5f), V(-2, L + 5)); p.Shade(K(0xd0fcff, 2, .6f, .3f)); }, -4, i * .8f); part.Pivot = -1; part.OX += x + b * .6f; part.OY += -h + 8; s.Parts.Add(part); }
                    return s;
                }
            // -------------------------------------------------------- bushes
            case "bushrose": case "bushjungle": case "bushmarsh": case "bushmoon":
                { var s = new Sprite { W = 40, H = 24 }; Col c = k switch { "bushrose" => v == 1 ? 0xd88aa0 : 0x9aa86a, "bushjungle" => v == 1 ? 0x3f8a48 : 0x4f8a3a, "bushmarsh" => 0x66784c, _ => v == 1 ? 0x6a64b8 : 0x3f7e98 }; int m = 3 + v % 2; for (int i = 0; i < m; i++) { float u = i / (m - 1f) * 2 - 1; s.Parts.Add(Clump(r.R(11, 15), r.R(9, 13), c.Shade(r.R(-.1f, .12f)), v * 7 + i + 500 + k.Length, 1, i, u * 13, -10 - (1 - u * u) * 4)); } if (k is "bushrose" or "bushjungle") s.Parts.Add(Paint(20, 28, 20, 2, p => { for (int i = 0; i < 6; i++) { float x = r.R(-13, 13), y = -r.R(7, 20); p.Begin(); for (int q = 0; q < 4; q++) p.Ellipse(x + M.Sin(q * 1.57f) * 1.6f, y + M.Cos(q * 1.57f) * 1.6f, 1.4f, 1.4f); p.Flat(k == "bushrose" ? 0xfce4ea : v == 0 ? 0xf06a4a : 0xf0c040); p.Dot(x, y, 0xf6d35a); } }, 1)); return s; }
            case "bigleaf": { var s = new Sprite { W = 44, H = 34 }; for (int i = 0; i < 5; i++) { float a = M.Lerp(-1.25f, 1.25f, i / 4f), L = r.R(24, 36); Col c = i % 2 == 0 ? 0x4f9a48 : 0x3f8440; s.Parts.Add(Paint(40, 40, 40, 4, p => { var e = V(M.Sin(a) * L, -M.Cos(a) * L * .84f); var d2 = Vector2.Normalize(e); var nn = V(-d2.Y, d2.X); p.Begin().Blob(V(0, 0), e * .4f + nn * 8, e * .8f + nn * 5.4f, e, e * .8f - nn * 5.4f, e * .4f - nn * 8); p.Shade(new Ink(c, 4, .7f, .8f) { Ao = 1 }); p.Line(V(0, 0), e, 1.2f, c.Shade(.45f), false); for (int q = 1; q < 5; q++) { var o = e * (q / 5.4f); p.Line(o, o + nn * 4.6f + d2 * 3, .9f, c.Shade(-.35f), true); p.Line(o, o - nn * 4.6f + d2 * 3, .9f, c.Shade(-.35f), true); } }, 1.4f, i * .7f)); } return s; }
            case "drybrush": return One(18, 24, 18, 2, p => { for (int i = 0; i < 8; i++) { float a = M.Lerp(-1.2f, 1.2f, i / 7f), L = r.R(11, 21); var e = V(M.Sin(a) * L, -M.Cos(a) * L); p.Curve(V(0, 0), e * .5f + V(r.R(-3, 3), 0), e, 1.1f, i % 2 == 0 ? 0xa8845a : 0x8a6a4a, false); p.Line(e * .7f, e * .7f + V(r.R(-5, 5), -4), 1, 0xb8946a, false); } }, 1.2f);
            case "frostfern": return One(18, 22, 18, 2, p => { for (int i = 0; i < 6; i++) { float a = M.Lerp(-1.1f, 1.1f, i / 5f), L = r.R(12, 19); var e = V(M.Sin(a) * L, -M.Cos(a) * L); p.Line(V(0, 0), e, 1.2f, 0x9ac0d8, false); for (int q = 2; q < 6; q++) { var c = e * (q / 6f); p.Line(c, c + V(-3, -2), 1, 0xe4f4fc, false); p.Line(c, c + V(3, -2), 1, 0xc8e4f4, false); } } }, .8f);
            case "glassgrass": return One(14, 22, 14, 2, p => { for (int i = 0; i < 5; i++) { float x = r.R(-9, 9), h = r.R(8, 19), l = r.R(-4, 4); Col c = i % 2 == 0 ? 0xb8dcf8 : 0xd8c4f8; p.Begin().Poly(V(x - 1.6f, 1), V(x + l, -h), V(x + 1.6f, 1)); p.Flat(c); p.Line(V(x, 0), V(x + l, -h), 1, 0xffffff, false); } }, .5f);
            case "lavender": return One(8, 24, 8, 2, p => { int n = 2 + v % 2; for (int i = 0; i < n; i++) { float x = (i - (n - 1) / 2f) * 5 + r.R(-1, 1), h = r.R(13, 21), l = r.R(-2, 2); p.Curve(V(x * .4f, 0), V(x * .7f, -h * .5f), V(x + l, -h), 1.1f, 0x6f8a5a, false); for (int q = 0; q < 5; q++) { float y = -h + q * 2; p.Begin().Ellipse(x + l + (q % 2 == 0 ? -.8f : .8f), y, 1.5f, 1.3f); p.Flat(q % 2 == 0 ? 0xb49ae4 : 0x8a6cc8); } } }, 2.2f);
            case "flowerb": case "flowerj": case "flowerm":
                { Col[] pc = k == "flowerb" ? [0xfbe0e8, 0xf2a8bc, 0xffffff] : k == "flowerj" ? [0xf06a3a, 0xf0b83a, 0xe04a7a] : [0x9ae8f8, 0xc8b4ff, 0x78b8f8]; return One(13, 24, 13, 2, p => { for (int i = 0; i < 3; i++) { float x = r.R(-8, 8), h = r.R(9, 19); p.Curve(V(x * .4f, 0), V(x * .5f, -h * .5f), V(x, -h), 1.2f, k == "flowerm" ? 0x4f8a98 : 0x5f8a46, false); Col c = pc[(v + i) % 3]; if (k == "flowerj") { p.Begin().Blob(V(x - 4, -h - 4), V(x, -h - 1), V(x + 4, -h - 4), V(x + 2, -h + 2), V(x - 2, -h + 2)); p.Shade(K(c, 1.5f, .7f, .4f, 0)); p.Dot(x, -h - 3, 0xf6e07a); } else { p.Begin(); for (int q = 0; q < 5; q++) { float a = q * M.TAU / 5; p.Ellipse(x + M.Sin(a) * 2.2f, -h + M.Cos(a) * 2.2f, 1.7f, 1.7f); } p.Shade(K(c, 1.5f, .5f, 0, 0)); p.Begin().Ellipse(x, -h, 1.1f, 1.1f); p.Flat(0xf6e07a); } } }, 1.8f); }
            case "cactusball": return One(12, 18, 12, 2, p => { float R = 7 + v; p.Begin().Ellipse(0, -R + 1, R, R); p.Shade(new Ink((x, y) => ((Col)0x6a9a5a).Shade(M.Mod((int)MathF.Floor(x + 30), 4) == 0 ? -.3f : .05f), 5, .7f, .8f)); for (int i = 0; i < 10; i++) { float a = r.R(0, M.TAU); p.Dot(M.Sin(a) * R * .8f, -R + M.Cos(a) * R * .8f, 0xf6ecd0); } p.Begin().Ellipse(0, -R * 2 + 1, 3, 2.2f); p.Shade(K(v == 1 ? 0xf0b84a : 0xe86a90, 1.5f, .6f, 0)); });
            case "agave": return One(20, 20, 20, 2, p => { for (int i = 0; i < 7; i++) { float a = M.Lerp(-1.4f, 1.4f, i / 6f), L = r.R(13, 19); var e = V(M.Sin(a) * L, -M.Cos(a) * L); var n = Vector2.Normalize(V(-e.Y, e.X)) * 3; p.Begin().Poly(n, e, -n); p.Shade(K(i % 2 == 0 ? 0x7a9a86 : 0x688a78, 2, .6f, .6f)); p.Dot(e.X, e.Y, 0x8a4a3a); } });
            case "sporepuff": return One(14, 18, 14, 2, p => { for (int i = 0; i < 3; i++) { float x = (i - 1) * 7, R = i == 1 ? 6 : 4; p.Begin().Ellipse(x, -R + 1, R, R); p.Shade(K(v == 0 ? 0xe8cce0 : v == 1 ? 0xc8e0d4 : 0xf0dcb0, 4, .7f, .8f)); p.Dot(x - 1, -R - 1, 0xfffaf0); p.Dot(x + 2, -R + 1, 0xb090a8); p.Dot(x - 2, -R + 2, 0xb090a8); } });
            case "emberbloom": return One(12, 24, 12, 2, p => { for (int i = 0; i < 3; i++) { float x = (i - 1) * 6, h = 9 + (i == 1 ? 8 : 0) + v; p.Curve(V(x * .3f, 0), V(x * .6f, -h * .5f), V(x, -h), 1.4f, 0x4a3438, false); p.Begin().Blob(V(x - 3.6f, -h + 1), V(x - 2, -h - 5), V(x, -h - 2.4f), V(x + 2, -h - 5), V(x + 3.6f, -h + 1), V(x, -h + 3)); p.Shade(K(0xff8a3a, 2, .8f, .4f)); p.Begin().Ellipse(x, -h, 1.4f, 1.6f); p.Flat(0xffe890); } }, 1);
            case "moonflower": return One(14, 26, 14, 2, p => { for (int i = 0; i < 2 + v % 2; i++) { float x = i * 8 - 6, h = 12 + i * 5; p.Curve(V(x * .4f, 0), V(x * .4f - 2, -h * .5f), V(x, -h), 1.3f, 0x3f7a86, false); p.Begin(); for (int q = 0; q < 6; q++) { float a = q * M.TAU / 6; p.Ellipse(x + M.Sin(a) * 3.4f, -h + M.Cos(a) * 3.4f, 2.3f, 2.3f); } p.Shade(K(v == 1 ? 0xc8b8ff : 0xa8f0ff, 2, .5f, .2f)); p.Begin().Ellipse(x, -h, 1.7f, 1.7f); p.Flat(0xffffff); } }, 1.6f);
            case "icespike": return One(22, 34, 22, 2, p => { Shard(p, -8, 4, 16, -.2f, 0xb4def4); Shard(p, 8, 3.6f, 13, .24f, 0xa4d2f0); Shard(p, 0, 5, 24 + v * 3, 0, 0xc4e8fa); p.Begin().Rect(-22, 2, 44, 4); p.Erase(); });
            case "amethyst": return One(20, 34, 20, 2, p => { Col c = v == 0 ? 0xa878dc : v == 1 ? 0xc490ec : 0x8a68d0; Shard(p, -8, 4, 15, -.24f, c.Shade(-.1f)); Shard(p, 8, 3.6f, 12, .28f, c); Shard(p, 0, 5, 22 + v * 3, r.R(-.08f, .08f), c.Shade(.06f)); Shard(p, 12, 2.4f, 7, .5f, c); p.Begin().Rect(-20, 2, 40, 4); p.Erase(); });
            case "cryhang": return One(14, 2, 14, 34, p => { Col c = v == 1 ? 0xc490ec : 0xa878dc; foreach (var (x, w, h, l) in new[] { (-6f, 3.6f, 14f, -.2f), (6f, 3.2f, 12f, .22f), (0f, 4.6f, 20f + v * 4, 0f) }) { var t = V(x + l * h, h); p.Begin().Poly(V(x - w, -2), V(x - w + l * h * .7f, h * .72f), t, V(x + .6f + l * h * .7f, h * .6f), V(x + .6f, -2)); p.Shade(new Ink(c.Shade(.14f), 2, .5f, .4f) { Ao = 0 }); p.Begin().Poly(V(x + .6f, -2), V(x + .6f + l * h * .7f, h * .6f), t, V(x + w + l * h * .7f, h * .68f), V(x + w, -2)); p.Shade(new Ink(c.Shade(-.24f), 2, .3f, .4f) { Ao = 0 }); } });
            case "roots": return One(14, 2, 14, 40, p => { for (int i = 0; i < 4; i++) { float x = i * 6 - 9, L = r.R(12, 36); p.Begin().Tube([V(x, -2), V(x + r.R(-4, 4), L * .5f), V(x + r.R(-6, 6), L)], [2.4f, 1.7f, .7f]); p.Shade(Wood(0x8a6a48, 2)); } }, -1);
            case "amberbud": return One(12, 20, 12, 2, p => { for (int i = 0; i < 3; i++) { float x = (i - 1) * 6.4f, h = 5 + (i == 1 ? 7 : 1) + v; p.Curve(V(x * .4f, 1), V(x * .8f, -h * .5f), V(x, -h), 1.8f, 0x8a6a44, false); p.Begin().Blob(V(x - 3, -h + 1), V(x - 1.6f, -h - 4), V(x, -h - 6), V(x + 1.6f, -h - 4), V(x + 3, -h + 1), V(x, -h + 3)); p.Shade(K(0xf6b040, 2, .8f, .4f)); p.Dot(x - 1, -h - 2, 0xfff2c0); } }, .6f);
            case "basaltstal": return One(12, 2, 12, 40, p => { foreach (var (x, h) in new[] { (-6f, 14f), (0f, 24f + v * 5), (6f, 11f) }) { p.Begin().Poly(V(x - 3.4f, -2), V(x - 3.4f, h - 4), V(x, h), V(x + 3.4f, h - 4), V(x + 3.4f, -2)); p.Shade(new Ink((a, b) => ((Col)0x4a4452).Shade(a > x ? -.24f : .12f), 2, .6f, .7f) { Ao = 0 }); } });
            // -------------------------------------------------------- stones
            case "snowrock": return Rock(v, 0x8a94a8, 0xf4faff, 41 + v); case "sandrock": return Rock(v, 0xd0a068, null, 51 + v); case "redrock": return Rock(v, 0xb0604a, 0xc87a5a, 61 + v); case "chalkrock": return Rock(v, 0xd8d4c8, v == 1 ? 0x9ac060 : null, 71 + v); case "basaltrock": return Rock(v, 0x544e5a, null, 81 + v);
        }
        return null;
    }
}

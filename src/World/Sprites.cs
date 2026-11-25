using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// A sprite is one or more painted parts; parts with sway move in the wind independently (tree canopies, flowers...).
public sealed class Sprite { public sealed class Part { public Texture2D T; public float OX, OY, Sway, Phase, Pivot; } public List<Part> Parts = new(); public float W, H; public float Grass; }

/// Every tree, bush, flower and stone. Painted once as outlined pixel-art sprites (not blocks).
public static partial class Sprites
{
    static readonly Dictionary<string, Sprite> all = new(); static Vector2 V(float x, float y) => new(x, y);
    public static bool Has(string n) => all.ContainsKey(n);
    public static Sprite Get(string n) { if (!all.TryGetValue(n, out var s)) { s = Make(n); all[n] = s; } return s; }
    /// Paint on a canvas whose origin (0,0) is the ground point; returns part.
    static Sprite.Part Paint(int l, int t, int r, int b, Action<Painter> f, float sway = 0, float phase = 0, int outline = 1, Col? oc = null)
    {
        var p = new Painter(l + r + 2, t + b + 2); p.View(1, l + 1, t + 1); f(p); if (outline > 0) p.Outline(oc ?? 0x33201f, outline); return new Sprite.Part { T = p.ToTexture(), OX = -l - 1, OY = -t - 1, Sway = sway, Phase = phase, Pivot = b };
    }
    static Sprite One(int l, int t, int r, int b, Action<Painter> f, float sway = 0, int outline = 1) { var s = new Sprite { W = l + r, H = t }; s.Parts.Add(Paint(l, t, r, b, f, sway, 0, outline)); return s; }
    static Ink K(Col c, float round = 3, float light = .55f, float ao = 1, float line = .8f) => new Ink(c, round, light, line) { Ao = ao };
    static Ink Wood(Col c, float rd = 4) => new Ink((x, y) => c.Shade(M.Noise(x * .55f, y * .07f, 3) * .22f), rd, .65f, .8f) { Ao = 1 };

    /// A leafy clump: scalloped silhouette, big form light, then little leaf dabs for a drawn look.
    static Sprite.Part Clump(float rx, float ry, Col c, int seed, float sway, float phase, float ox, float oy, bool needle = false)
    {
        int mx = (int)rx + 7, my = (int)ry + 7; var r = new Rng(seed); var part = Paint(mx, my, mx, my, p =>
        {
            p.Begin().Ellipse(0, 0, rx * .88f, ry * .86f); int n = (int)((rx + ry) * .55f); for (int i = 0; i < n; i++) { float a = i * M.TAU / n + r.R(-.1f, .1f), q = r.R(3.2f, 5.6f); p.Ellipse(M.Sin(a) * rx * .86f, M.Cos(a) * ry * .84f, q, q * .9f); }
            p.Shade(new Ink((x, y) => c.Shade(M.Clamp((-x / rx * .35f - y / ry * .65f) * .42f, -.5f, .4f) + M.Noise(x * .16f, y * .16f, seed) * .12f), 6, .45f, .55f) { Ao = 0, Grad = .18f });
            int d = (int)(rx * ry * .085f); for (int i = 0; i < d; i++)
            {
                float a = r.R(0, M.TAU), q = MathF.Sqrt(r.F()) * .88f, x = M.Sin(a) * rx * q, y = M.Cos(a) * ry * q; float lit = -x / rx * .4f - y / ry * .7f + r.R(-.25f, .25f); Col k = lit > .1f ? c.Shade(.18f + lit * .42f) : c.Shade(-.2f + lit * .4f); float an = r.R(-.5f, .5f);
                var v = V(x, y); var e = V(M.Cos(an) * 2.6f, M.Sin(an) * 1.6f + 1); p.Begin().Poly(v - e * .9f, v + V(e.Y, -e.X) * .55f, v + e, v - V(e.Y, -e.X) * .55f); p.IntersectAlpha(); p.Flat(k);
            }
        }, sway, phase); part.OX += ox; part.OY += oy; return part;
    }
    static Sprite.Part Trunk(float h, float w0, float w1, float bend, Col c, int seed, float oy = 0, bool birch = false)
    {
        var r = new Rng(seed); return Paint((int)w0 + 34, (int)h + 4, (int)w0 + 34, 4, p =>
        {
            var pts = new List<Vector2>(); int N = 10; for (int i = 0; i <= N; i++) { float t = i / (float)N, w = M.Lerp(w0, w1, MathF.Pow(t, .8f)) * (t < .12f ? 1 + (.12f - t) * 7 : 1); pts.Add(V(M.Sin(t * 2.4f) * bend * t - w, -t * h)); }
            for (int i = N; i >= 0; i--) { float t = i / (float)N, w = M.Lerp(w0, w1, MathF.Pow(t, .8f)) * (t < .12f ? 1 + (.12f - t) * 7 : 1); pts.Add(V(M.Sin(t * 2.4f) * bend * t + w, -t * h)); }
            p.Begin(); for (int b = 0; b < 4; b++) { float t = .55f + b * .1f, s = b % 2 == 0 ? -1 : 1; var a = V(M.Sin(t * 2.4f) * bend * t, -t * h); p.Capsule(a, w1 * .9f, a + V(s * r.R(14, 26), -r.R(10, 20)), w1 * .4f); }
            p.Poly(pts.ToArray()); p.Shade(new Ink((x, y) => { float n = M.Noise(x * .6f, y * .06f, seed) * .25f + (M.Noise(x * .9f, y * .2f, seed + 4) > .35f ? -.2f : 0); var k = c.Shade(n); if (birch) { float bn = M.Noise(x * .22f + 7, y * .3f, seed); k = bn > .38f ? (Col)0x4a3c3a : c.Shade(n * .4f); } return k; }, 5, .7f, .85f) { Ao = 0 });
            if (!birch) for (int i = 0; i < 2; i++) { float yy = -h * r.R(.2f, .5f); p.Begin().Ellipse(M.Sin(yy) * 1.5f, yy, 2.4f, 3.2f); p.IntersectAlpha(); p.Flat(c.Shade(-.6f)); }
            // moss and roots
            p.Begin().Ellipse(-w0 * .8f, -2, 5, 3); p.IntersectAlpha(); p.Tint(0x7a9a50, .6f);
        });
    }
    static Sprite Tree(string kind, int v)
    {
        var s = new Sprite(); var r = new Rng(kind.GetHashCode2() + v * 77); Col[] leaf = kind switch { "oak" => [0x6f9a46, 0x84ad52, 0x5f8a48], "maple" => [0xd8743a, 0xe09a40, 0xc4523a], "oakgold" => [0xe0a83e, 0xd08a38, 0xe8bd52], "birch" => [0xe3b848, 0xd8a03c, 0xc8b454], "blossom" => [0xeaa0b4, 0xf2bcc8, 0xe08aa4], "willow" => [0x86a854, 0x98b860, 0x769a4c], _ => [0x4f8058, 0x5c8e62, 0x457450] };
        float h = kind switch { "birch" => r.R(118, 150), "maple" => r.R(104, 128), "blossom" => r.R(84, 100), "willow" => 116, "palm" => r.R(112, 140), "pine" or "fir" => r.R(128, 176), _ => r.R(100, 122) };
        if (kind is "pine" or "fir")
        {
            s.Parts.Add(Trunk(h * .9f, 6.5f, 2, r.R(-4, 4), 0x6e4a38, v + 3)); int tiers = kind == "pine" ? 6 : 5;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)(tiers - 1), y = -M.Lerp(h * .30f, h * 1.02f, t), w = M.Lerp(kind == "pine" ? 42 : 50, 13, t), hh = M.Lerp(40, 28, t); Col c = leaf[0].Shade(-.12f + t * .2f); int seed = v * 10 + i; var rr = new Rng(seed); int mw = (int)w + 6;
                var part = Paint(mw, 34, mw, 24, p => { var q = new List<Vector2> { V(0, -hh * .7f) }; int n = 5; for (int k = 0; k <= n; k++) { float u = k / (float)n; q.Add(V(M.Lerp(2, w, MathF.Pow(u, .85f)), M.Lerp(-hh * .55f, 10, u) + (k == n ? 0 : 0))); if (k < n && k > 0) q.Add(V(M.Lerp(2, w, u) - 4.5f, M.Lerp(-hh * .55f, 10, u) + 3)); } int teeth = 7; for (int k = 0; k <= teeth; k++) { float u = k / (float)teeth; q.Add(V(M.Lerp(w, -w, u), 10 + (k % 2 == 0 ? 4 : -2) + MathF.Sin(u * M.PI) * 6)); } for (int k = n; k >= 0; k--) { float u = k / (float)n; if (k < n && k > 0) q.Add(V(-M.Lerp(2, w, u) + 4.5f, M.Lerp(-hh * .55f, 10, u) + 3)); q.Add(V(-M.Lerp(2, w, MathF.Pow(u, .85f)), M.Lerp(-hh * .55f, 10, u))); } p.Begin().Poly(q.ToArray()); p.Shade(new Ink((x, y2) => c.Shade(-x / w * .16f + M.Noise(x * .2f, y2 * .25f, seed) * .14f), 6, .55f, .7f) { Ao = 0, Grad = .3f }); for (int k = 0; k < w * .9f; k++) { float x = rr.R(-w * .8f, w * .8f), yy = rr.R(-hh * .4f, 10); p.Begin(); var a = V(x, yy); var b = a + V(x / w * 2.5f, 4.5f); p.Begin().Capsule(a, .8f, b, .5f); p.IntersectAlpha(); p.Flat(c.Shade(x < 0 ? .3f : -.25f)); } }, .5f + t * 2.2f, i * .35f);
                part.OY += y; part.OX += M.Sin(t * 2.4f) * 2; s.Parts.Add(part);
            }
            s.H = h; return s;
        }
        if (kind == "palm")
        {
            float bend = v % 2 == 0 ? 22 : -20; s.Parts.Add(Paint(60, (int)h + 10, 60, 4, p => { var pts = new Vector2[12]; var w = new float[12]; for (int i = 0; i < 12; i++) { float t = i / 11f; pts[i] = V(bend * t * t, -h * t); w[i] = M.Lerp(6.5f, 3.6f, t); } p.Begin().Tube(pts, w); p.Shade(new Ink((x, y) => ((Col)0xa87c52).Shade(M.Mod((int)(y * .18f + 400), 2) == 0 ? .08f : -.16f), 4, .7f) { Ao = 0 }); for (int i = 1; i < 12; i++) p.Line(pts[i] + V(-w[i], 1), pts[i] + V(w[i], -1), 1, 0x6e4a38); }));
            for (int i = 0; i < 8; i++) { float a = M.Lerp(-1.9f, 1.9f, i / 7f); int seed = i; float L = r.R(46, 58); var part = Paint(66, 54, 66, 60, p => { var m2 = V(M.Sin(a) * L * .55f, -M.Cos(a) * L * .5f - 8); var e = V(M.Sin(a) * L, -M.Cos(a) * L * .45f + 14); Vector2 At(float t) => (1 - t) * (1 - t) * Vector2.Zero + 2 * t * (1 - t) * m2 + t * t * e; var up = new List<Vector2>(); var dn = new List<Vector2>(); for (int k = 0; k <= 12; k++) { float t = k / 12f; var c = At(t); float ww = M.Sin(MathF.Min(1, t * 1.15f + .06f) * M.PI) * 6 + 1; up.Add(c + V(0, -ww * .5f)); dn.Add(c + V(0, ww + (k % 2 == 0 ? 4 : 0))); } dn.Reverse(); up.AddRange(dn); Col cc = i % 2 == 0 ? 0x6f9e48 : 0x5c8c46; p.Begin().Poly(up.ToArray()); p.Shade(new Ink(cc, 4, .7f, .8f) { Ao = 0 }); p.Curve(V(0, 0), m2, e, 1.2f, cc.Shade(.4f)); }, 2.4f, i * .8f); part.OX += bend; part.OY -= h; s.Parts.Add(part); }
            s.Parts.Add(Paint(40, (int)h + 10, 40, 2, p => { for (int i = 0; i < 3; i++) { p.Begin().Ellipse(bend - 4 + i * 4.5f, -h + 5 + (i == 1 ? 3 : 0), 4, 4); p.Shade(K(0x7a4a30, 3, .8f)); } })); s.H = h; return s;
        }
        Col bark = kind switch { "birch" => 0xe6ddd0, "maple" => 0x7a4e3a, "blossom" => 0x6e4844, "willow" => 0x7a6046, _ => 0x80583e }; s.Parts.Add(Trunk(h * .86f, kind == "birch" ? 5 : 8.5f, kind == "birch" ? 2.6f : 3.6f, r.R(-8, 8), bark, v + 9, 0, kind == "birch"));
        float spread = kind switch { "birch" => 30, "blossom" => 40, "willow" => 46, _ => 46 }; int nC = kind == "birch" ? 8 : 9;
        if (kind == "willow") for (int i = 0; i < 21; i++) { float u = i / 20f * 2 - 1; float L = r.R(46, 88) * (1 - u * u * .25f); int seed = i + v * 20; Col c = leaf[i % 3]; var part = Paint(14, 4, 14, (int)L + 8, p => { var rr = new Rng(seed); var pts = new Vector2[8]; var w = new float[8]; for (int k = 0; k < 8; k++) { float t = k / 7f; pts[k] = V(M.Sin(t * 2 + seed) * 2.4f * t, t * L); w[k] = M.Lerp(3.4f, 1.1f, t); } p.Begin().Tube(pts, w); p.Shade(new Ink(c, 3, .6f, .7f) { Ao = 0, Grad = .2f }); for (int k = 0; k < L * .5f; k++) { float t = rr.F(); p.Line(pts[(int)(t * 7)] + V(rr.R(-3, 3), 0), pts[(int)(t * 7)] + V(rr.R(-3, 3), 4), .9f, c.Shade(rr.R(-.3f, .4f)), false); } }, 5, i * .5f); part.Pivot = -L; part.OX += u * 52; part.OY += -h * .82f + u * u * 16; s.Parts.Add(part); }
        for (int i = 0; i < nC; i++)
        {
            float a = i * M.TAU / nC + r.R(-.2f, .2f), q = i == 0 ? 0 : r.R(.55f, 1), x = M.Sin(a) * spread * q * .8f, y = -h + M.Cos(a) * spread * q * .5f + (kind == "birch" ? i * 4 - 14 : 4); float R = r.R(18, 25) * (kind == "birch" ? .86f : 1); s.Parts.Add(Clump(R * 1.12f, R * .92f, leaf[r.I(3)], v * 100 + i + kind.Length * 7, 1.6f + (-y - h * .6f) * .03f, i * .9f, x, y));
        }
        s.Parts.Add(Clump(26, 21, leaf[0].Shade(.06f), v * 100 + 55, 2, 3, r.R(-5, 5), -h - 2)); s.H = h; return s;
    }
    static Sprite Make(string n)
    {
        int v = char.IsDigit(n[^1]) ? n[^1] - '0' : 0; string k = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'); var r = new Rng(k.GetHashCode2() + v * 31);
        switch (k)
        {
            case "oak": case "maple": case "birch": case "oakgold": case "blossom": case "willow": case "pine": case "fir": case "palm": return Tree(k, v);
            case "bush": case "bushgold": case "bushpine":
                { var s = new Sprite { W = 40, H = 24 }; Col c = k == "bush" ? (v == 1 ? 0x7aa04c : 0x6a9448) : k == "bushgold" ? (v == 0 ? 0xc9803c : v == 1 ? 0xb85a3a : 0xd0a040) : 0x4f7c58; int m = 3 + v % 2; for (int i = 0; i < m; i++) { float u = m == 1 ? 0 : i / (m - 1f) * 2 - 1; s.Parts.Add(Clump(r.R(11, 15), r.R(9, 12), c.Shade(r.R(-.1f, .12f)), v * 7 + i + 300, 1, i, u * 13, -10 - (1 - u * u) * 4)); } if (v == 2 && k == "bush") s.Parts.Add(Paint(20, 24, 20, 2, p => { for (int i = 0; i < 6; i++) { p.Begin().Ellipse(r.R(-13, 13), -r.R(6, 18), 1.6f, 1.6f); p.Flat(0xd8506a); } }, 1)); return s; }
            case "fern": return One(20, 24, 20, 2, p => { for (int i = 0; i < 7; i++) { float a = M.Lerp(-1.3f, 1.3f, i / 6f), L = r.R(16, 22); var e = V(M.Sin(a) * L, -M.Cos(a) * L * .8f); for (int q = 2; q < 8; q++) { var c = e * (q / 8f); float w = (1 - q / 9f) * 5; p.Line(c + V(-w, -1.5f), c, 1.2f, 0x5f9058, false); p.Line(c + V(w, -1.5f), c, 1.2f, 0x4c7a4e, false); } p.Line(V(0, 0), e, 1.2f, 0x7aa860, false); } }, 1.6f);
            case "flower": case "flowera": case "flowerh":
                { Col[] pc = k == "flower" ? [0xf6f0e0, 0xf0bd4a, 0xd86f86, 0x9a8ac8] : k == "flowera" ? [0xe07a3a, 0xf0c050, 0xb8473f, 0xd8a0b0] : [0x7fa8c9, 0x9a8ac8, 0xf0ead8, 0xc87a9a]; return One(12, 22, 12, 2, p => { int m = 2 + r.I(3); for (int i = 0; i < m; i++) { float x = r.R(-7, 7), h = r.R(9, 18); p.Curve(V(x * .4f, 0), V(x * .5f, -h * .5f), V(x, -h), 1.2f, 0x5f8a46, false); p.Begin().Blob(V(x * .5f, -h * .4f), V(x * .5f + 4, -h * .55f), V(x * .5f + 1, -h * .3f)); p.Flat(0x6f9a50); Col c = pc[(v + i / 2) % 4]; if ((v & 1) == 0) { p.Begin(); for (int q = 0; q < 5; q++) { float a = q * M.TAU / 5; p.Ellipse(x + M.Sin(a) * 2.2f, -h + M.Cos(a) * 2.2f, 1.6f, 1.6f); } p.Shade(K(c, 1.5f, .5f, 0, 0)); p.Begin().Ellipse(x, -h, 1.1f, 1.1f); p.Flat(0xf6d35a); } else { p.Begin().Blob(V(x - 2.6f, -h - 2.5f), V(x, -h - 1.5f), V(x + 2.6f, -h - 2.5f), V(x + 2, -h + 1.5f), V(x - 2, -h + 1.5f)); p.Shade(K(c, 1.5f, .6f, 0, 0)); } } }, 1.8f); }
            case "beachgrass": case "reed": case "kelp": case "weed": case "glowkelp":
                { bool tall = k != "beachgrass"; float H0 = k == "reed" ? r.R(40, 58) : k == "beachgrass" ? 16 : r.R(34, 70); Col c = k == "reed" ? 0x86a052 : k == "beachgrass" ? 0xb0b060 : k == "kelp" ? 0x5a8a5a : k == "glowkelp" ? 0x3fa08a : 0x4f8a62; return One(16, (int)H0 + 6, 16, 2, p => { int m = k == "reed" ? 5 : 4; for (int i = 0; i < m; i++) { float x = r.R(-8, 8), h = H0 * r.R(.6f, 1); if (k is "kelp" or "weed" or "glowkelp") { var pts = new Vector2[8]; var w = new float[8]; for (int q = 0; q < 8; q++) { float t = q / 7f; pts[q] = V(x + M.Sin(t * 7 + i) * 3.4f, -t * h); w[q] = M.Lerp(2.6f, 1, t); } p.Begin().Tube(pts, w); p.Shade(K(c.Shade(r.R(-.15f, .2f)), 2, .6f, 0)); } else { p.Curve(V(x * .5f, 0), V(x * .6f, -h * .6f), V(x + r.R(-4, 4), -h), 1.6f, c.Shade(r.R(-.25f, .25f)), false); if (k == "reed" && i < 2) { p.Begin().Capsule(V(x, -h + 4), 2.2f, V(x, -h + 13), 2.2f); p.Shade(K(0x8a5a38, 2, .7f, 0)); } } } }, k is "kelp" or "weed" or "glowkelp" ? 5 : 2.6f); }
            case "lily": return One(18, 8, 18, 4, p => { p.Begin().Ellipse(0, 1, 13, 3.6f).Cut(V(0, 1), V(14, -3), V(14, 1)); p.Shade(K(0x6fa050, 3, .6f, 0)); p.Begin().Ellipse(-9, 0, 5, 2); p.Shade(K(0x86b05a, 2, .5f, 0)); if (v == 1) { p.Begin(); for (int q = 0; q < 6; q++) { float a = q * M.TAU / 6; p.Ellipse(-1 + M.Sin(a) * 3, -3 + M.Cos(a) * 1.8f, 2.4f, 2); } p.Shade(K(0xf2bcc8, 2, .6f, 0)); p.Begin().Ellipse(-1, -4, 1.4f, 1.2f); p.Flat(0xf6d35a); } });
            case "coral": return One(16, 26, 16, 2, p => { Col c = v == 0 ? 0xe58a6a : v == 1 ? 0xd86f9a : 0xe0b650; for (int i = 0; i < 5; i++) { float a = (i - 2) * .42f, L = r.R(12, 20); var m2 = V(M.Sin(a) * L * .6f, -L * .6f); var e = V(M.Sin(a) * L, -M.Cos(a) * L); p.Begin().Capsule(V(0, 0), 3, m2, 2.6f).Capsule(m2, 2.6f, e, 2).Capsule(m2, 2, m2 + V(a > 0 ? 5 : -5, -5), 1.8f); p.Shade(K(c, 2, .7f, 0)); } });
            case "shell": return One(7, 8, 7, 1, p => { p.Begin().Blob(V(-5, 0), V(-4, -5), V(0, -6.5f), V(4, -5), V(5, 0)); p.Shade(K(v == 0 ? 0xf2c8b4 : v == 1 ? 0xf6ead8 : 0xd8a0b4, 2, .6f, 0)); for (int i = -1; i <= 1; i++) p.Line(V(0, 0), V(i * 3, -5), .6f, 0xb07a70); });
            case "starfish": return One(8, 12, 8, 2, p => { var q = new Vector2[10]; for (int i = 0; i < 10; i++) { float a = i * M.PI / 5, R = (i & 1) == 0 ? 6.5f : 2.8f; q[i] = V(M.Sin(a) * R, -4 - M.Cos(a) * R); } p.Begin().Blob(q); p.Shade(K(0xe07a4a, 2, .7f, 0)); for (int i = 0; i < 5; i++) { float a = i * M.TAU / 5; p.Dot(M.Sin(a) * 3, -4 - M.Cos(a) * 3, 0xf6c890); } });
            case "rock":
                return One(26, 26, 26, 3, p =>
                {
                    // faceted boulder: flat base, lit top plane, dark side plane, then cracks, chips and a little moss
                    float h = 11 + v * 3.2f, w = 12 + v * 2.6f, j = r.R(-2, 2); Col c = v % 2 == 0 ? 0x9a949e : 0xa39a92; Vector2 bl = V(-w, 2), ml = V(-w * .92f, -h * .46f), tl = V(-w * .42f + j, -h * .98f), tr = V(w * .34f + j, -h * .90f), mr = V(w * .88f, -h * .40f), br = V(w, 2), ci = V(-w * .16f + j, -h * .56f), cr = V(w * .44f, -h * .44f), cb = V(w * .30f, 2);
                    Ink F(Col k, float s) => new Ink((x, y) => k.Shade(s + M.Noise(x * .3f, y * .3f, v + 3) * .10f), 2, .25f, 0) { Ao = 0, Grad = .12f };
                    p.Begin().Poly(bl, ml, tl, tr, mr, br); p.Shade(F(c, -.06f)); p.Begin().Poly(ml, tl, tr, cr, ci); p.Shade(F(c, .30f)); p.Begin().Poly(bl, ml, ci, V(-w * .34f, 2)); p.Shade(F(c, .08f)); p.Begin().Poly(cr, mr, br, cb); p.Shade(F(c, -.42f)); p.Begin().Poly(cr, tr, mr); p.Shade(F(c, -.18f));
                    Col ln = c.Shade(-.55f); p.Begin(); p.Line(ml, ci, 1, c.Shade(.5f), false); p.Line(ci, cr, 1, c.Shade(.42f), false); p.Line(cr, cb, 1, ln, false); p.Line(ci, V(-w * .34f, 2), 1, c.Shade(-.28f), false); p.Line(tl, ci, 1, c.Shade(.12f), false); p.Line(cr, tr, 1, c.Shade(-.1f), false);
                    p.Line(V(-w * .62f, -h * .2f), V(-w * .5f, -h * .02f), 1, ln, false); p.Line(V(w * .05f, -h * .36f), V(w * .14f, -h * .12f), 1, ln, false); p.Dot(-w * .1f, -h * .75f, c.Shade(.6f)); p.Dot(-w * .7f, -h * .5f, c.Shade(.6f)); p.Dot(w * .2f, -h * .2f, ln);
                    p.Begin().Blob(tl + V(-3, 2), tl + V(2, -1.4f), V(0, -h * .99f), tr + V(1, 0), tr + V(-2, 3.4f), V(j, -h * .74f), tl + V(2, 4)); p.IntersectAlpha(); if (v != 1) { p.Shade(new Ink(0x7fa052, 2, .6f, 0) { Ao = 0 }); }
                    p.Begin().Poly(V(w * .72f, 2), V(w * .78f, -2.4f), V(w * 1.06f, -3), V(w * 1.22f, 2)); p.Shade(new Ink(c.Shade(-.1f), 2, .7f, .6f)); p.Begin().Rect(-26, 1.4f, 52, 4); p.Erase();
                });
            case "stump": return One(18, 18, 18, 2, p => { p.Begin().Poly(V(-13, 1), V(-9, -4), V(-9, -12), V(9, -12), V(9, -4), V(13, 1)); p.Shade(Wood(0x80583e)); p.Begin().Ellipse(0, -12, 9, 3.4f); p.Shade(K(0xd8b080, 2, .4f, 0)); p.Begin().Ellipse(0, -12, 5, 1.8f); p.Flat(0xc0935f); p.Begin().Ellipse(0, -12, 2.4f, .9f); p.Flat(0xd8b080); });
            case "log": return One(26, 16, 26, 2, p => { p.Begin().Capsule(V(-18, -6), 6.5f, V(18, -6), 6.5f); p.Shade(Wood(0x80583e, 6)); p.Begin().Ellipse(20, -6, 4, 6.4f); p.Shade(K(0xd8b080, 2, .4f, 0)); p.Begin().Ellipse(20, -6, 2, 3.4f); p.Flat(0xb88a58); p.Begin().Ellipse(-8, -12, 8, 2); p.IntersectAlpha(); p.Tint(0x86a050, .8f); });
            case "shroom": return One(10, 16, 10, 1, p => { for (int i = 0; i < 2 + v % 2; i++) { float x = i * 6 - 4, h = 6 + i * 3; p.Begin().Capsule(V(x, 0), 1.8f, V(x, -h), 1.5f); p.Shade(K(0xf3e2c8, 1.5f, .5f, 0)); p.Begin().Ellipse(x, -h, 4.4f, 3.6f).Cut(V(x - 6, -h + .6f), V(x + 6, -h + .6f), V(x + 6, -h + 6), V(x - 6, -h + 6)); p.Shade(K(v == 0 ? 0xc6483e : v == 1 ? 0xb07a48 : 0xd8a048, 2, .7f, .5f)); p.Dot(x - 1, -h - 2, 0xfaf0e0); p.Dot(x + 1.6f, -h - 1, 0xfaf0e0); } });
            case "glow": return One(14, 26, 14, 1, p => { Col c = (v & 1) == 0 ? 0x62e8d4 : 0xa898ff; for (int i = 0; i < 3; i++) { float x = i * 7 - 7, h = 8 + (i == 1 ? 8 : 0) + v; p.Begin().Capsule(V(x, 0), 2, V(x, -h), 1.6f); p.Shade(K(0xd8f0ea, 1.5f, .4f, 0)); p.Begin().Ellipse(x, -h, 5.4f, 4.4f).Cut(V(x - 7, -h + 1), V(x + 7, -h + 1), V(x + 7, -h + 7), V(x - 7, -h + 7)); p.Shade(K(c, 2.5f, .5f, .5f, .5f)); p.Dot(x - 2, -h - 1, 0xf0ffff); p.Dot(x + 2, -h - 2, 0xf0ffff); } });
            case "crystal": return One(14, 26, 14, 1, p => { Col c = v == 0 ? 0x9ac0f0 : v == 1 ? 0xc8a8f0 : 0x9ae8d8; for (int i = 0; i < 4; i++) { float x = (i - 1.5f) * 5, h = r.R(10, 22), a = (i - 1.5f) * .22f; var t = V(x + M.Sin(a) * h, -h); p.Begin().Poly(V(x - 3, 1), V(x - 3 + a * 8, -h * .7f), t, V(x + 3 + a * 8, -h * .7f), V(x + 3, 1)); p.Shade(K(c, 2, .9f, .5f)); p.Line(V(x - 1, 0), t + V(-1, 3), 1, 0xf6faff); } });
            case "stal": return One(10, 2, 10, 30, p => { p.Begin().Poly(V(-7, -1), V(7, -1), V(2, 14 + v * 5), V(0, 18 + v * 5), V(-3, 10)); p.Shade(K(0x7a7480, 3, .7f, 0)); });
            case "hang": return One(12, 1, 12, 36, p => { for (int i = 0; i < 4; i++) { float x = i * 5 - 8, L = r.R(12, 32); p.Curve(V(x, 0), V(x + 2, L * .5f), V(x, L), 1.4f, 0x5f8a52, false); for (float y = 4; y < L; y += 5) { p.Begin().Ellipse(x + ((int)y % 2 == 0 ? 2 : -2), y, 2.2f, 1.6f); p.Flat(((int)y % 3 == 0) ? 0x7aa860 : 0x638f50); } if (v == 1 && i == 2) { p.Begin().Ellipse(x, L, 2.4f, 2.4f); p.Flat(0xf2bcc8); } } }, -2);
            case "grass": return One(9, 14, 9, 2, p => { for (int i = 0; i < 5 + v % 3; i++) { float x = r.R(-6, 6), h = r.R(5, 11); p.Stroke([V(x * .7f, 1), V(x, -h * .5f), V(x * 1.3f + r.R(-2, 2), -h)], 1.5f, Col.Lerp(0x9a9a9a, 0xf0f0f0, r.F()), 1, false); } }, 0, 0);
        }
        return Wild(k, v, r) ?? One(4, 4, 4, 0, p => { p.Begin().Ellipse(0, -2, 2, 2); p.Flat(0xff00ff); });
    }
    public static int GetHashCode2(this string s) { int h = 17; foreach (char c in s) h = h * 31 + c; return h & 0xffffff; }
}

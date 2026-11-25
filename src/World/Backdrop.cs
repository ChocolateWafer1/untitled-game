using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
namespace UntitledGame;

/// The far distance. Every biome has its own hand painted horizon in four parallax layers, and they dissolve into each other.
public sealed class Backdrop
{
    const int TW = 1024, TH = 300; readonly Dictionary<Biome, Texture2D[]> done = new(); readonly Dictionary<Biome, Task<Painter[]>> busy = new(); readonly Shader sh; readonly int uTint, uFog, uAmt, uLit, uFade;
    public Backdrop(Shader hill) { sh = hill; uTint = Raylib.GetShaderLocation(sh, "tint"); uFog = Raylib.GetShaderLocation(sh, "fog"); uAmt = Raylib.GetShaderLocation(sh, "amt"); uLit = Raylib.GetShaderLocation(sh, "lit"); uFade = Raylib.GetShaderLocation(sh, "fade"); }
    /// Painting happens on another thread; asking early means it is ready on arrival.
    public Texture2D[] Get(Biome b, bool wait = false)
    {
        if (done.TryGetValue(b, out var t)) return t; if (!busy.TryGetValue(b, out var k)) busy[b] = k = Task.Run(() => { var o = new Painter[4]; for (int i = 0; i < 4; i++) o[i] = Layer(b, i); return o; }); if (!wait && !k.IsCompleted) return null;
        var p = k.Result; t = new Texture2D[4]; for (int i = 0; i < 4; i++) { t[i] = p[i].ToTexture(); Raylib.SetTextureWrap(t[i], TextureWrap.Repeat); } busy.Remove(b); return done[b] = t;
    }
    static float Per(float x, int seed, float amp, int f) { float r = 0; for (int i = 1; i <= 5; i++) { int k = f * i + (seed * i) % 3; r += M.Sin(x / TW * M.TAU * k + M.Hash(i, seed) * 6.28f) * amp / (i * 1.1f); } return r; }
    static float Frac(float v) => v - MathF.Floor(v);
    static float Height(string k, int L, float x, int sd)
    {
        float[] by = [96, 112, 118, 128]; float b = by[L]; float Ridge(int f, float amp, int s) { float r = 0; for (int i = 0; i < 4; i++) { float q = MathF.Abs(M.Sin(x / TW * M.PI * (f << i) + M.Hash(i, s) * M.PI * 2)); r += (1 - q) * amp / (1 << i); } return r; }
        // tall shapes standing side by side, each with its own width and height
        float Row(int n, float lo, float hi, Func<float, float> prof, float wid, int s) { float best = 0; for (int i = -1; i <= n; i++) { int j = M.Mod(i, n); float c = (i + .5f + (M.Hash(j, s) - .5f) * .6f) / n * TW, w = wid * (.6f + .8f * M.Hash(j, s + 1)), hh = M.Lerp(lo, hi, M.Hash(j, s + 2)); foreach (int sh in new[] { -TW, 0, TW }) { float u = MathF.Abs(x - c - sh) / w; if (u < 1) best = MathF.Max(best, hh * prof(u)); } } return best; }
        switch (k)
        {
            case "peaks": return L == 0 ? b + 34 - Ridge(3, 52, sd) : L == 1 ? b + 22 - Ridge(5, 34, sd + 3) : L == 2 ? b + 10 - Ridge(6, 14, sd + 5) + Per(x, sd + 2, 8, 2) : b + Per(x, sd + 4, 10, 3);
            case "dunes": { float q = x / TW * (3 + L * 2) + M.Hash(L, sd), u = Frac(q); float a = M.Lerp(.6f, 1, M.Hash(M.Mod((int)MathF.Floor(q), 3 + L * 2), sd + L)); return b + 26 - (u < .66f ? M.Smooth(0, .66f, u) : 1 - M.Smooth(.66f, 1, u)) * (L == 0 ? 30 : 38 - L * 4) * a + Per(x, sd + L, 4, 2); }
            case "mesa": { float f(float u) => u < .74f ? 1 : u < .80f ? .62f : u < .9f ? .58f : .2f; return L <= 1 ? b + 34 - Row(L == 0 ? 7 : 5, 24, 78, f, L == 0 ? 74 : 96, sd + L * 9) + Per(x, sd, 2.4f, 9) : b + 14 - Row(5, 8, 34, f, 120, sd + L * 9) + Per(x, sd + L, 5, 3); }
            case "canopy": { float s = MathF.Abs(M.Sin(x / TW * M.PI * (16 + L * 4 + sd % 3))) * (9 + L * 2) + MathF.Abs(M.Sin(x / TW * M.PI * (37 + L * 6))) * 5; return b + 8 + Per(x, sd + L, 24 - L * 4, 1 + L) - s - (L == 0 ? 24 : 12); }
            case "marsh": return b + 26 + Per(x, sd + L, L == 0 ? 12 : 4, 2 + L) - (L == 0 ? 18 : 0);
            case "karst": { float f(float u) => MathF.Pow(1 - u * u * u, .55f); return L == 0 ? b + 30 - Row(10, 40, 120, f, 46, sd) + Per(x, sd, 2, 12) : L == 1 ? b + 22 - Row(7, 24, 96, f, 56, sd + 7) + Per(x, sd, 2, 14) : b + MathF.Floor(Per(x, sd + L, 18, 1 + L) / 6) * 6; }
            case "volcano": { float f(float u) => u < .12f ? .93f : MathF.Pow(1 - u, 1.25f) * 1.1f; return L == 0 ? b + 40 - Row(3, 70, 130, f, 190, sd) - Ridge(9, 5, sd) : L == 1 ? b + 26 - Row(4, 30, 66, f, 150, sd + 5) - Ridge(9, 7, sd + 2) : b + 14 - Ridge(8, 14 - L * 2, sd + L) + Per(x, sd + L, 6, 2); }
            case "crystal": { float f(float u) => 1 - u; float f2(float u) => u < .16f ? 1 - u * 1.6f : .74f - (u - .16f) * .88f; return b + 34 - MathF.Max(Row(11 - L, 20, L == 0 ? 120 : 96 - L * 20, f, 34, sd + L), Row(8, 10, 70 - L * 10, f2, 40, sd + L + 4)) + Per(x, sd + L, 5, 2); }
            case "islands": { float f(float u) => MathF.Sqrt(MathF.Max(0, 1 - u * u)); return L == 0 ? 150 - Row(6, 8, 46, f, 60, sd) : L == 1 ? 154 - Row(4, 12, 50, f, 74, sd + 6) : b + 26 + Per(x, sd + L, 7, 2 + L); }
            case "shrooms": return b + 6 - MathF.Abs(M.Sin(x / TW * M.PI * (4 + L * 2) + sd)) * 26 + Per(x, sd + L, 8, 3);
            default: return L == 0 ? b + MathF.Abs(Per(x, sd + 3, 30, 3)) * -1.5f + 26 + Per(x, 7, 5, 9) : b + Per(x, L * 5 + 1 + sd, 20 - L * 2.5f, 1 + L);
        }
    }
    static void Stamp(Painter p, string k, float x, float y, float s, Col c, int seed)
    {
        Vector2 V(float a, float b) => new(x + a * s, y + b * s); Ink I(Col q, float r = .6f) => new Ink(q, s * r, .6f, .6f) { Ao = 0 }; var h = M.Hash(seed, 5);
        switch (k)
        {
            case "pine": case "snowpine": p.Begin().Poly(V(-.7f, 0), V(0, -3), V(.7f, 0)); p.Shade(I(k == "pine" ? c : Col.Lerp(0x5f8a92, c, .35f), .5f)); if (k == "snowpine") { p.Begin().Poly(V(-.42f, -1.2f), V(0, -3), V(.42f, -1.2f), V(.1f, -1.7f), V(-.1f, -1.3f)); p.Shade(I(0xf8fcff)); } break;
            case "palm": p.Stroke([V(0, 0), V(.2f, -1.2f), V(.5f, -2.3f)], s * .22f, 0x7a5a44, 1, false); for (int i = 0; i < 6; i++) { float a = M.Lerp(-1.8f, 1.8f, i / 5f); p.Stroke([V(.5f, -2.3f), V(.5f + M.Sin(a) * .7f, -2.3f - M.Cos(a) * .6f), V(.5f + M.Sin(a) * 1.2f, -2.1f - M.Cos(a) * .4f)], s * .24f, c.Shade(i % 2 == 0 ? .1f : -.1f), 1, false); } break;
            case "shroom": { Col cap = h < .33f ? 0xc05a74 : h < .66f ? 0x5aa8a0 : 0x9a70c0; p.Begin().Poly(V(-.28f, 0), V(-.18f, -1.7f), V(.18f, -1.7f), V(.28f, 0)); p.Shade(I(0xe8d8cc)); p.Begin().Blob(V(-1.15f, -1.5f), V(-.8f, -2.4f), V(0, -2.8f), V(.8f, -2.4f), V(1.15f, -1.5f), V(0, -1.4f)); p.Shade(I(Col.Lerp(cap, c, .3f))); p.Begin().Ellipse(x - s * .3f, y - s * 2.2f, s * .18f, s * .14f).Ellipse(x + s * .4f, y - s * 1.9f, s * .14f, s * .11f); p.Flat(0xf6ecdc); break; }
            case "bamboo": for (int i = 0; i < 4; i++) { float o = (i - 1.5f) * .36f, l = (M.Hash(seed, i) - .5f) * .5f; p.Stroke([V(o, 0), V(o + l, -3 - M.Hash(i, seed))], s * .16f, c.Shade(i % 2 == 0 ? .16f : -.1f), 1, false); p.Begin().Ellipse(x + (o + l) * s, y - s * 3, s * .42f, s * .7f); p.Shade(I(c.Shade(.06f), .4f)); } break;
            case "cypress": p.Begin().Blob(V(-.34f, 0), V(-.42f, -1.3f), V(0, -3), V(.42f, -1.3f), V(.34f, 0)); p.Shade(I(Col.Lerp(0x4f7258, c, .3f))); break;
            case "dead": p.Stroke([V(0, 0), V(.06f, -1.4f), V(-.1f, -2.4f)], s * .2f, c.Shade(-.4f), 1, false); p.Stroke([V(0, -1.1f), V(.6f, -1.9f), V(.7f, -2.4f)], s * .12f, c.Shade(-.4f), 1, false); p.Stroke([V(0, -1.4f), V(-.6f, -2)], s * .12f, c.Shade(-.4f), 1, false); break;
            case "jungle": p.Begin().Rect(x - s * .12f, y - s * 2.2f, s * .24f, s * 2.4f); p.Flat(0x5f5244); p.Begin().Ellipse(x, y - s * 2.4f, s * 1.25f, s * .6f).Ellipse(x - s * .7f, y - s * 2.1f, s * .6f, s * .4f).Ellipse(x + s * .7f, y - s * 2.15f, s * .6f, s * .4f); p.Shade(I(c)); break;
            default: { Col q = k switch { "autumn" => h < .34f ? 0xd08a44 : h < .67f ? 0xc46a40 : 0xd0a850, "blossom" => h < .5f ? 0xe8b0c0 : 0xd898ac, "moon" => h < .5f ? 0x4f8ab0 : 0x6a70b8, _ => c }; q = Col.Lerp(q, c, k == "tree" ? 1 : .35f); p.Begin().Rect(x - s * .12f, y - s, s * .24f, s * 1.2f); p.Flat(0x6a5348); p.Begin().Ellipse(x, y - s * 1.5f, s * .95f, s).Ellipse(x - s * .6f, y - s * 1.1f, s * .6f, s * .6f).Ellipse(x + s * .6f, y - s * 1.15f, s * .65f, s * .6f); p.Shade(I(q)); break; }
        }
    }
    static Painter Layer(Biome bi, int L)
    {
        var d = Biomes.Of(bi); string k = d.Back; int sd = (int)bi * 5 + 1; var p = new Painter(TW + 96, TH); p.View(1, 48, 0); var r = new Rng(L * 13 + 2 + (int)bi * 71); Col far = d.Far, mid = d.Mid, near = d.Near; Col cc = L == 0 ? far : L == 1 ? Col.Lerp(far, mid, .75f) : L == 2 ? Col.Lerp(mid, near, .4f) : near;
        var hs = new float[TW + 130]; for (int x = -62; x < TW + 66; x++) hs[x + 62] = Height(k, L, x, sd); float H(float x) => hs[Math.Clamp((int)MathF.Round(x) + 62, 0, hs.Length - 1)];
        var pts = new List<Vector2>(); int step = k is "mesa" or "karst" or "crystal" ? 2 : 4; for (int x = -60; x <= TW + 60; x += step) pts.Add(new(x, H(x))); pts.Add(new(TW + 60, 340)); pts.Add(new(-60, 340)); bool ridged = (k is "peaks" or "volcano" && L <= 1) || k == "crystal" || (k == "hills" && L == 0 && bi is Biome.Meadow or Biome.Amber); int mx = Math.Max(1, TW);
        Col Tone(float x, float y)
        {
            float dd = y - H(x); var c = cc; float wx = M.Mod2(x, TW);
            if (k == "mesa" || (k == "volcano" && L <= 1 && false)) { float b = (y + M.Sin(x / TW * M.TAU * 3) * 5) / 7; int lay = (int)MathF.Floor(b); c = c.Shade((M.Hash(lay, sd + L) - .5f) * .42f); if (b - lay < .16f) c = c.Shade(-.14f); float cl = M.Sin(x / TW * M.TAU * 60 + M.Sin(y * .1f)); if (dd < 60) c = c.Shade(cl * .05f); }
            else if (k == "karst" && L <= 1) { float cl = M.Sin(x / TW * M.TAU * 70 + M.Sin(y * .06f) * 3); c = c.Shade(cl * .05f); float green = M.Noise(wx * .05f, y * .05f, sd); if (dd < 12 || green > .25f) c = Col.Lerp(c, 0x7aa05a, .5f); }
            else if (k == "crystal") { float f = M.Sin(x / TW * M.TAU * 22 + y * .045f) > 0 ? .16f : -.12f; f += M.Sin(x / TW * M.TAU * 47 - y * .07f) > .6f ? .18f : 0; c = c.Shade(f); }
            else if (ridged)
            {
                float nx = x + y * .55f, nz = x - y * .5f; float ridge = M.Sin(nx * M.TAU / TW * 21 + M.Sin(y * .07f) * 2) * .5f + M.Sin(nz * M.TAU / TW * 34 + M.Sin(y * .11f + 1) * 2) * .35f; c = c.Shade(ridge * .13f);
                if (k == "volcano") { float lava = MathF.Abs(M.Sin(x / TW * M.TAU * 17 + M.Sin(y * .09f) * 1.4f)); if (L == 0 && dd < 46 && lava < .10f * (1 - dd / 46)) c = new Col(1, .84f, .55f); if (L == 0 && dd < 5 && H(x) < 60) c = new Col(1, .82f, .5f); }
                else { float sl = (bi == Biome.Frost ? 130 : 78) + M.Sin(x * M.TAU / TW * 46) * 4 + M.Sin(x * M.TAU / TW * 83 + 1) * 3 + ridge * 5; if (y < sl) c = Col.Lerp(0xf6f2f4, c, .12f).Shade(ridge * .10f); }
            }
            else if (k == "dunes") { float rip = M.Sin((dd + M.Sin(x / TW * M.TAU * 5) * 8) * .5f), sx = x - dd * .85f; float lee = M.Smooth(.25f, 1.0f, (H(sx + 4) - H(sx - 4)) / 2.2f) * M.Clamp(1 - dd / 52); c = c.Shade(rip * .035f - lee * .30f); }
            else if (k == "marsh" || (k == "islands" && L <= 1 && y > 150)) { if (k == "islands") { c = M.Lerp(0, 1, .5f) > 0 ? Col.Lerp(0x7ac0d0, 0x4f9ab4, M.Clamp((y - 150) / 90)) : c; if (M.Sin(y * 1.1f + M.Sin(x / TW * M.TAU * 9) * 2) > .86f) c = c.Shade(.35f); } else { float water = M.Noise(wx * .012f, y * .13f, sd + L); if (dd > 12 && water > .05f) { c = Col.Lerp(c, 0xb4c8c0, .6f); if (M.Sin(y * .9f + x * .02f) > .8f) c = c.Shade(.2f); } } }
            else if (k == "canopy") { float q = M.Sin(x / TW * M.TAU * (34 + L * 6) + M.Sin(y * .13f) * 1.7f) * M.Sin(y * .21f + M.Sin(x / TW * M.TAU * 19) * 2); c = c.Shade(q * .10f + (q > .55f ? .08f : q < -.6f ? -.10f : 0)); }
            else if (bi == Biome.Lavender && L >= 1) { float row = M.Sin((dd * (1.1f + L * .1f) + M.Sin(x / TW * M.TAU * (2 + L)) * 26) * (.36f - L * .05f)); c = row > .2f ? c.Shade(.12f) : row < -.72f ? Col.Lerp(c, 0x7a8a78, .45f) : c; }
            else { float band = M.Sin((dd + M.Sin(x / TW * M.TAU * (3 + L)) * 12) * .12f); c = c.Shade(band * .05f + M.Noise(wx * .03f, y * .06f, L) * .13f); if (bi == Biome.Blossom && L >= 2 && M.Noise(wx * .04f, y * .08f, 9) > .3f) c = Col.Lerp(c, 0xa8b878, .5f); }
            if (!ridged && dd < 5) c = c.Shade(.3f); else if (!ridged && dd < 9) c = c.Shade(.14f); return c.Shade(-M.Clamp(dd / 170) * .25f);
        }
        p.Begin().Poly(CollectionsMarshal.AsSpan(pts), 1); p.Shade(new Ink(Tone, 2, 0, .75f) { Ao = 0, Grad = 0, LinePx = L == 0 ? 1 : 1.5f });
        if (d.Stamp != "none" && (L > 0 || k is "islands"))
        {
            void Wrap(float x, Action<float> f) { f(x); if (x < 90) f(x + TW); if (x > TW - 90) f(x - TW); }
            int n = (L == 0 ? 10 : L == 1 ? 56 : L == 2 ? 44 : 32) * (d.Stamp is "dead" ? 2 : 4) / 4; if (d.Stamp is "bamboo" or "jungle") n = n * 3 / 2; if (bi == Biome.Lavender) n /= 3; if (k is "marsh" or "volcano") n = n * 2 / 3;
            for (int i = 0; i < n; i++)
            {
                float x = r.F() * TW, off = r.F() * r.F() * (L == 1 ? 46 : 34), s = (L == 0 ? 2.4f : L == 1 ? 3.6f : L == 2 ? 6.5f : 11) * r.R(.7f, 1.3f) * (d.Stamp is "shroom" ? 1.25f : 1); Col tc = d.Stamp is "pine" or "jungle" or "palm" ? Col.Lerp(cc, 0x4f7c50, L >= 2 ? .55f : .3f).Shade(r.R(-.3f, 0)) : d.Stamp == "bamboo" ? Col.Lerp(cc, 0x86b04a, .6f).Shade(r.R(-.2f, .1f)) : cc.Shade(r.R(-.42f, -.16f)); int seed = r.I(999);
                if (k == "islands" && L <= 1 && H(x) > 146) continue; if (k == "karst" && L <= 1) { Wrap(x, xx => { p.Begin().Ellipse(xx, H(xx) + off * .3f, s * 1.1f, s * .7f); p.Shade(new Ink(((Col)0x6a945a).Shade(r.R(-.2f, .1f)), s * .5f, .6f, .5f) { Ao = 0 }); }); continue; } Wrap(x, xx => Stamp(p, d.Stamp, xx, H(xx) + off, s, tc, seed));
            }
            if (k == "hills" && L >= 2 && bi != Biome.Lavender) for (int i = 0; i < 8; i++) { float x = r.F() * TW, off = r.R(26, 70); Wrap(x, xx => { for (float q = 0; q < 40; q += 3) { float yy = H(xx + q) + off; p.Begin().Ellipse(xx + q, yy, 3.4f, 2.4f); p.Shade(new Ink(cc.Shade(-.3f), 2, .5f, .4f) { Ao = 0 }); } }); }
        }
        // crop the margin so the edge of the canvas never becomes a drawn outline
        var o = new Painter(TW, TH); for (int y = 0; y < TH; y++) Array.Copy(p.Px, y * p.W + 48, o.Px, y * TW, TW); return o;
    }
    public void Draw(Env e, World wd, float camX, float camY, int w, int h, float groundY, float tx)
    {
        var (a, b, t) = wd.Mix(tx); Get(wd.BiomeAt(tx - 60)); Get(wd.BiomeAt(tx + 60)); var ta = Get(a); var tb = a == b ? null : Get(b); if (ta == null && tb == null) ta = done.Values.FirstOrDefault(); if (ta == null) { ta = tb; tb = null; t = 0; } if (ta == null) return; if (tb == null) t = 0;
        Vector3 V(Col c) => new(c.R, c.G, c.B); float und = M.Clamp((camY + h * .5f - groundY - 60) / 400); float fa = M.Clamp((1 - t) * 2), fb = tb == null ? 0 : M.Clamp(t * 2);
        Raylib.BeginShaderMode(sh); float[] px = [.04f, .09f, .17f, .3f], py = [.02f, .045f, .08f, .14f], by = [.36f, .44f, .54f, .64f], fog = [.44f, .28f, .14f, .04f]; Col light = e.Amb * .82f + e.Sun * (.28f + .10f * e.Golden);
        Raylib.SetShaderValue(sh, uTint, V(light), ShaderUniformDataType.Vec3); Raylib.SetShaderValue(sh, uFog, V(Col.Lerp(e.Hor, e.Fog, .5f)), ShaderUniformDataType.Vec3); Raylib.SetShaderValue(sh, uLit, e.Night, ShaderUniformDataType.Float);
        for (int i = 0; i < 4; i++)
        {
            float am = M.Clamp(fog[i] + e.Mist * .35f + e.Rain * .16f); Raylib.SetShaderValue(sh, uAmt, am, ShaderUniformDataType.Float);
            float oy = h * by[i] - (camY + h * .5f - 92 * 24) * py[i] - 60; float ox = M.Mod((int)(camX * px[i] + i * 211), TW);
            for (int k = 0; k < 2; k++) { var set = k == 0 ? ta : tb; float f = k == 0 ? fa : fb; if (set == null || f <= .01f) continue; Raylib.SetShaderValue(sh, uFade, f, ShaderUniformDataType.Float); Rlgl.SetTexture(0); Raylib.DrawTexturePro(set[i], new Rectangle(ox, 0, w, TH), new Rectangle(0, (int)oy, w, TH), Vector2.Zero, 0, Color.White); if (oy + TH - 1 < h) Raylib.DrawTexturePro(set[i], new Rectangle(ox, TH - 2, w, 1), new Rectangle(0, (int)oy + TH - 1, w, h - oy), Vector2.Zero, 0, Color.White); Rlgl.DrawRenderBatchActive(); }
        }
        Raylib.EndShaderMode(); if (und > 0) Raylib.DrawRectangle(0, 0, w, h, new Color(26, 20, 34, (int)(und * 255)));
    }
}

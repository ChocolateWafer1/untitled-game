using System.Numerics;
using Raylib_cs;
namespace UnnamedGame;

/// Everything that moves on its own: grass, particles, weather, animals.
public sealed class Life
{
    struct Pt { public Vector2 P, V; public float T, L, S, G, D, R; public Color C; public int K; } // K 0 square 1 leaf 2 soft puff 3 drop 4 bubble 5 spark(glow) 6 smoke
    readonly List<Pt> pts = new(1200); readonly World w; readonly GameScreen g; readonly Rng r = new(321); readonly float[] bend = new float[World.W * 3], bv = new float[World.W * 3]; Texture2D[] grass; readonly Dictionary<string, Texture2D> an = new(); float leafT, moteT, rainT;
    enum Kind { Bird, Butterfly, Bunny, Duck, Frog, Firefly, Gull, Moth, Fish, Squirrel }
    sealed class Animal { public Kind K; public Vector2 P, V, Home; public float T, A, B; public int S, Dir = 1, Var; public Pool Pool; public float Seen; }
    readonly List<Animal> animals = new();
    public Life(World w, GameScreen g) { this.w = w; this.g = g; grass = new Texture2D[4]; for (int i = 0; i < 4; i++) grass[i] = Sprites.Get("grass" + i).Parts[0].T; Art(); Populate(); }
    // ---------------------------------------------------------------- particles
    void Add(Vector2 p, Vector2 v, float life, float s, Color c, int k = 0, float grav = 0, float drag = 1) { if (pts.Count < 1500) pts.Add(new Pt { P = p, V = v, L = life, S = s, C = c, K = k, G = grav, D = drag, R = r.R(0, 6) }); }
    public void Dust(Vector2 p, string surf, int n, int dir) { Col c = surf switch { "grass" => World.GrassCol(p.X / 24).Shade(-.05f), "leaves" => r.P(.5f) ? 0xd8843a : 0xc9a347, "sand" => 0xe3c98e, "wood" => 0xb08a60, "stone" => 0xa8a4a8, _ => 0xa88060 }; for (int i = 0; i < n; i++) Add(p + new Vector2(r.R(-5, 5), -1), new(dir * r.R(5, 30) + r.R(-24, 24), -r.R(6, 36)), r.R(.25f, .55f), r.R(1, 2.2f), c.Rl(.8f), surf == "leaves" ? 1 : surf == "stone" ? 0 : 2, 60, 3); }
    public void Drip(Vector2 p) => Add(p + new Vector2(r.R(-6, 6), -r.R(8, 40)), new(r.R(-8, 8), 0), .5f, 1, new Color(170, 210, 230, 200), 3, 300, 0);
    public void Splash(Vector2 p, float a) { int n = (int)(6 + a * 22); for (int i = 0; i < n; i++) Add(p + new Vector2(r.R(-8, 8) * (1 + a), 0), new(r.R(-70, 70) * (.5f + a), -r.R(50, 190) * (.35f + a * .75f)), r.R(.4f, .9f), r.R(1, 2.6f), new Color(206, 234, 244, 230), 3, 420, .4f); }
    public void Bubble(Vector2 p) => Add(p + new Vector2(r.R(-3, 3), 0), new(r.R(-6, 6), -r.R(26, 50)), r.R(.8f, 1.6f), r.R(1, 2.2f), new Color(214, 240, 248, 170), 4, 0, .5f);
    public void Sparkle(Vector2 p, int n, Color c) { for (int i = 0; i < n; i++) Add(p + new Vector2(r.R(-10, 10), r.R(-10, 10)), new(r.R(-40, 40), r.R(-60, 10)), r.R(.4f, 1), r.R(1, 2.4f), c, 5, 20, 2); }
    public void Leaves(Vector2 p, int n, Col c) { for (int i = 0; i < n; i++) Add(p + new Vector2(r.R(-16, 16), r.R(-12, 8)), new(r.R(-30, 30), r.R(-30, 10)), r.R(1, 2.2f), 2, c.Shade(r.R(-.2f, .2f)).Rl(), 1, 26, 1.2f); }
    public void Update(float dt, Player pl, float cx, float cy, int vw, int vh)
    {
        var e = g.Env; float wind = e.Wind;
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var p = pts[i]; p.T += dt; if (p.T > p.L || p.P.X < cx - 200 || p.P.X > cx + vw + 200 || p.P.Y > cy + vh + 160) { pts[i] = pts[^1]; pts.RemoveAt(pts.Count - 1); continue; }
            p.V.Y += p.G * dt; p.V *= MathF.Exp(-p.D * dt); if (p.K == 1) { p.V.X += (wind * 46 + M.Sin(p.T * 3 + p.R) * 26) * dt * 2; p.V.Y = MathF.Min(p.V.Y, 26 + M.Sin(p.T * 4 + p.R) * 12); } if (p.K == 6) p.V.X += wind * 14 * dt; p.P += p.V * dt;
            int tx = (int)(p.P.X / 24), ty = (int)(p.P.Y / 24); if (p.K is 1 or 3 && w.Solid(tx, ty)) { if (p.K == 3) { p.T = p.L; } else { p.V = default; p.G = 0; p.P.Y = ty * 24 - .5f; p.K = 7; p.L = p.T + 3; } } if (p.K == 3 && p.V.Y > 40 && w.PoolAt(p.P.X, p.P.Y) != null) p.T = p.L; if (p.K == 4 && w.PoolAt(p.P.X, p.P.Y - 3) == null) p.T = p.L; pts[i] = p;
        }
        // ambient leaves, petals, pollen
        leafT -= dt; float ctx = (cx + vw / 2f) / 24; var b = World.BiomeAt(ctx); bool under = g.Under > .5f; if (leafT < 0 && !under) { leafT = (b == Biome.Autumn ? .16f : .9f) / (1 + MathF.Abs(wind) * 1.4f); float x = cx + r.R(-60, vw + 60) - wind * 60, y = cy + r.R(-20, vh * .5f); if (w.Light[Math.Clamp((int)(y / 24), 0, World.H - 1) * World.W + Math.Clamp((int)(x / 24), 0, World.W - 1)] > .9f) { Col c = b == Biome.Autumn ? (r.P(.4f) ? 0xd8743a : r.P(.5f) ? 0xe0a83e : 0xc4523a) : b == Biome.Highlands ? 0x5f8a60 : r.P(.4f) ? 0xf2bcc8 : 0x8fb050; Add(new(x, y), new(wind * 30, 14), 9, 2, c.Rl(), 1, 14, .4f); } }
        moteT -= dt; if (moteT < 0 && !under && e.Rain < .2f) { moteT = .12f; float a = e.Golden * .8f + .2f; Add(new(cx + r.R(0, vw), cy + r.R(vh * .2f, vh)), new(wind * 8 + r.R(-4, 4), r.R(-7, 2)), r.R(3, 6), 1, Col.Lerp(e.Sun, new Col(1, .95f, .8f), .5f).Rl(a * .5f), 8, 0, .1f); } else if (moteT < 0 && under) { moteT = .25f; Add(new(cx + r.R(0, vw), cy + r.R(0, vh)), new(r.R(-4, 4), r.R(-6, 1)), r.R(3, 6), 1, new Color(130, 240, 225, 130), 8, 0, .1f); }
        if (e.Snow > .05f && !under) { int n = 0; foreach (var q in pts) if (q.K == 10) n++; for (int k = 0; k < 3 && n < 260 * e.Snow; k++, n++) Add(new(cx + r.R(-80, vw + 60) - wind * 60, cy - r.R(4, 40)), new(wind * 34 + r.R(-8, 8), r.R(26, 54)), 14, r.P(.3f) ? 2 : 1, new Color(250, 252, 255, 220), 10, 0, 0); for (int i = 0; i < pts.Count; i++) if (pts[i].K == 10) { var q = pts[i]; q.P.X += M.Sin(q.T * 1.6f + q.R) * 9 * dt; int sx = (int)(q.P.X / 24), sy = (int)(q.P.Y / 24); if (w.Solid(sx, sy) || w.PoolAt(q.P.X, q.P.Y) != null) q.T = q.L + 1; pts[i] = q; } }
        // rain
        if (e.Rain > .05f && !under) { rainT += dt * e.Rain * vw * .55f; while (rainT > 1) { rainT -= 1; float x = cx + r.R(-80, vw + 80) - wind * 50; Add(new(x, cy - 14), new(wind * 110 - 14, r.R(430, 540)), 1.4f, r.R(6, 11), new Color(190, 212, 230, 120), 9, 0, 0); } for (int i = pts.Count - 1; i >= 0; i--) { if (pts[i].K != 9) continue; var p = pts[i].P; int tx = (int)(p.X / 24), ty = (int)(p.Y / 24); if (w.Solid(tx, ty) || w.PoolAt(p.X, p.Y) != null || (w.PlatAt(tx, ty) && r.P(.5f))) { if (r.P(.55f)) { Add(new(p.X, w.Solid(tx, ty) ? ty * 24 - 1 : p.Y - 2), new(r.R(-22, 22), -r.R(16, 46)), .28f, 1, new Color(206, 226, 240, 170), 3, 220, 0); } pts[i] = pts[^1]; pts.RemoveAt(pts.Count - 1); } } }
        // grass springs
        int g0 = Math.Max(0, (int)(cx / 24) - 2) * 3, g1 = Math.Min(World.W - 1, (int)((cx + vw) / 24) + 2) * 3; for (int i = g0; i < g1; i++) { float gx = i * 8 + 4, d = gx - pl.P.X, gy = w.Top[i / 3] * 24; float push = 0; if (MathF.Abs(d) < 15 && MathF.Abs(gy - pl.P.Y) < 30) { push = MathF.Sign(d) * (1 - MathF.Abs(d) / 15) * 9 + pl.V.X * .03f; if (MathF.Abs(pl.V.X) > 40 && r.P(dt * 1.2f)) { Snd.Play("rustle", .10f, 1.2f + r.F() * .4f); Add(new(gx, gy - 5), new(pl.V.X * .2f + r.R(-20, 20), -r.R(10, 40)), .6f, 1, World.GrassCol(gx / 24).Shade(.1f).Rl(), 1, 60, 1); } } bv[i] += ((push - bend[i]) * 130 - bv[i] * 9) * dt; bend[i] += bv[i] * dt; }
        Animals(dt, pl, cx, cy, vw, vh);
    }
    public void DrawGrass(int cx, int cy, int vw, int vh, bool front)
    {
        float t = g.Env.T, wind = g.Env.Wind; int x0 = Math.Max(0, cx / 24 - 1), x1 = Math.Min(World.W - 1, (cx + vw) / 24 + 1);
        for (int x = x0; x <= x1; x++)
        {
            int ty = w.Top[x]; if (!w.Solid(x, ty) || w.Tile[ty * World.W + x] != T.Dirt || w.Solid(x, ty - 1)) continue; float y = ty * 24 - cy; if (y < -20 || y > vh + 20) continue; if (w.PoolAt(x * 24 + 12, ty * 24 - 6) != null) continue;
            for (int k = 0; k < 3; k++) { float h = M.Hash(x, k, 3); if ((h > .62f) != front) continue; if (M.Hash(x, k, 9) < .12f) continue; float gx = x * 24 + 4 + k * 8 + h * 3; Col c = World.GrassCol(gx / 24).Shade((M.Hash(x, k, 5) - .5f) * .3f + (front ? -.1f : .06f)); float sw = M.Sin(t * (1.6f + MathF.Abs(wind)) + gx * .035f) * (1 + MathF.Abs(wind) * 2.2f) + wind * 2.6f + bend[x * 3 + k]; sw = MathF.Round(sw * 2) / 2; Shear(grass[(int)(M.Hash(x, k, 7) * 4)], MathF.Round(gx - cx) - 10, MathF.Round(y + 3 - 16 + (h * 4)), sw, c.Rl(), h > .5f); }
        }
    }
    /// Textured quad whose top edge is pushed sideways (wind / collision bending).
    public static void Shear(Texture2D t, float x, float y, float dx, Color c, bool flip = false, bool hang = false)
    {
        float w = t.Width, h = t.Height, u0 = flip ? 1 : 0, u1 = flip ? 0 : 1, a = hang ? 0 : dx, b = hang ? dx : 0; Rlgl.SetTexture(t.Id); Rlgl.Begin(7); Rlgl.Color4ub(c.R, c.G, c.B, c.A); Rlgl.Normal3f(0, 0, 1); Rlgl.TexCoord2f(u0, 0); Rlgl.Vertex2f(x + a, y); Rlgl.TexCoord2f(u0, 1); Rlgl.Vertex2f(x + b, y + h); Rlgl.TexCoord2f(u1, 1); Rlgl.Vertex2f(x + w + b, y + h); Rlgl.TexCoord2f(u1, 0); Rlgl.Vertex2f(x + w + a, y); Rlgl.End(); Rlgl.SetTexture(0);
    }
    public void DrawPts(int cx, int cy, bool glow)
    {
        foreach (var p in pts)
        {
            float k = p.T / p.L; float x = MathF.Round(p.P.X - cx), y = MathF.Round(p.P.Y - cy); bool gl = p.K is 5 or 8; if (gl != glow) continue; var c = p.C; float fa = k > .7f ? (1 - k) / .3f : 1; if (p.K == 8) fa = M.Sin(M.Clamp(k) * M.PI); c.A = (byte)(c.A * fa);
            switch (p.K) { case 1: case 7: { float a = p.K == 7 ? 0 : M.Sin(p.T * 5 + p.R); Raylib.DrawRectangle((int)x - 1, (int)y, a > 0 ? 3 : 2, a > .3f ? 1 : 2, c); break; } case 2: { float s = p.S * (1 + k * 1.4f); c.A = (byte)(c.A * (1 - k) * .7f); Raylib.DrawRectangle((int)(x - s), (int)(y - s), (int)(s * 2), (int)(s * 2), c); break; } case 4: Raylib.DrawRectangleLines((int)x - 1, (int)y - 1, (int)p.S + 2, (int)p.S + 2, c); break; case 6: { float s = p.S * (1 + k * 2); c.A = (byte)(c.A * (1 - k)); Raylib.DrawCircle((int)x, (int)y, s, c); break; } case 9: { var d = Vector2.Normalize(p.V) * p.S; Raylib.DrawLine((int)x, (int)y, (int)(x - d.X), (int)(y - d.Y), c); break; } default: Raylib.DrawRectangle((int)x, (int)y, Math.Max(1, (int)p.S), Math.Max(1, (int)p.S), c); break; }
        }
    }
    public void Smoke(Vector2 p, float s, Color c) => Add(p, new(r.R(-4, 4), -r.R(10, 18)), r.R(2, 3.4f), s, c, 6, 0, .3f);
    public void Flame(Vector2 p) { Add(p + new Vector2(r.R(-5, 5), 0), new(r.R(-6, 6), -r.R(18, 46)), r.R(.3f, .6f), r.R(1.5f, 3.4f), r.P(.5f) ? new Color(255, 190, 80, 255) : new Color(255, 120, 50, 255), 5, -30, 1); if (r.P(.12f)) Add(p + new Vector2(r.R(-5, 5), -8), new(r.R(-14, 14), -r.R(30, 70)), r.R(.8f, 1.6f), 1, new Color(255, 200, 110, 255), 5, -10, .6f); }
    public void Drop(Vector2 p, Vector2 v, float life) => Add(p, v, life, r.R(1, 2), new Color(190, 226, 240, 220), 3, 260, 0);
    public void Foam(Vector2 p) => Add(p, new(r.R(-34, 34), -r.R(10, 60)), r.R(.4f, .9f), r.R(1, 2), new Color(236, 246, 250, 190), 3, 160, 1);
    public int CrittersAhead(Vector2 p, int dir, float range) { int n = 0; foreach (var a in animals) if (a.K is Kind.Bunny or Kind.Squirrel && a.S != 2 && (a.P.X - p.X) * dir is > -20 and var q && q < range) n++; return n; }
    public void Scatter(float cx, float cy, int vw) { foreach (var a in animals) if (a.K == Kind.Bird && a.P.X > cx - 200 && a.P.X < cx + vw + 200 && a.S == 0) { a.S = 1; a.Dir = r.P(.5f) ? 1 : -1; a.V = new(a.Dir * r.R(60, 120), -r.R(90, 150)); } Snd.Play("flap", .7f); }
    public void Flock(float cx, float cy, int vw) { int n = 0; foreach (var a in animals) if (a.K == Kind.Bird && n++ < 26) { a.Home = a.P; a.P = new(cx - r.R(20, 260), cy + r.R(20, 110)); a.S = 1; a.Dir = 1; a.V = new(r.R(90, 130), r.R(-8, 6)); } }
    public void Petals(float cx, float cy, int vw, int kind) { for (int i = 0; i < 160; i++) Add(new(cx + r.R(-260, vw), cy - r.R(0, 160)), new(r.R(20, 60), r.R(10, 30)), 12, 2, (kind == 0 ? (r.P(.5f) ? (Col)0xf2bcc8 : 0xf8d8de) : r.P(.4f) ? (Col)0xd8743a : r.P(.5f) ? 0xe0a83e : 0xc4523a).Rl(), 1, 16, .4f); }
    public void Swarm(Vector2 c) { for (int i = 0; i < 60; i++) Add(c + new Vector2(r.R(-240, 240), r.R(-110, 10)), new(r.R(-12, 12), r.R(-14, 6)), r.R(8, 16), 2, new Color(230, 255, 120, 220), 8, 0, .1f); }

    // ---------------------------------------------------------------- animals
    void Art()
    {
        void A(string n, int W, int H, Action<Painter> f, Col? o = null) { var p = new Painter(W, H); p.View(1, W / 2f, H / 2f); f(p); p.Outline(o ?? 0x33201f, 1); an[n] = p.ToTexture(); }
        Vector2 V(float x, float y) => new(x, y); Ink K(Col c, float q = 2) => new Ink(c, q, .6f, 0) { Ao = 0 }; Col[] bc = [0xc65a4a, 0x5f8ac0, 0xd8a848, 0x8a6a50];
        for (int v = 0; v < 4; v++) { Col c = bc[v]; A("bird0" + v, 16, 14, p => { p.Begin().Ellipse(-.5f, 1, 4.4f, 3.6f); p.Shade(K(c)); p.Begin().Ellipse(-.4f, 2.2f, 2.6f, 2.2f); p.Flat(0xf3e6cc); p.Begin().Ellipse(3, -2, 2.6f, 2.5f); p.Shade(K(c)); p.Begin().Poly(V(5, -2.4f), V(7.4f, -1.4f), V(5, -.8f)); p.Flat(0xe8a040); p.Begin().Poly(V(-4, 0), V(-7.6f, -1.6f), V(-6, 2)); p.Flat(c.Shade(-.3f)); p.Dot(3, -3, 0x1a1014); p.Line(V(-1, 4), V(-1, 6), 1, 0xb8803c, false); p.Line(V(1, 4), V(1, 6), 1, 0xb8803c, false); }); for (int f = 0; f < 3; f++) { float a = f == 0 ? -5 : f == 1 ? 0 : 4; A("bird" + (f + 1) + v, 18, 16, p => { p.Begin().Ellipse(0, 1, 4.6f, 2.8f); p.Shade(K(c)); p.Begin().Ellipse(4, -.4f, 2.4f, 2.3f); p.Shade(K(c)); p.Begin().Poly(V(6, -.6f), V(8, .2f), V(6, .8f)); p.Flat(0xe8a040); p.Begin().Poly(V(-4, 0), V(-8, -1), V(-7, 2)); p.Flat(c.Shade(-.3f)); p.Begin().Poly(V(2.4f, 0), V(-1, a - 1), V(-4.5f, a), V(-2.4f, 1)); p.Flat(c.Shade(-.25f)); p.Dot(4, -1, 0x1a1014); }); } }
        Col[] fl = [0xf0bd4a, 0xe58ab0, 0x8ab8e8, 0xf6f0e0]; for (int v = 0; v < 4; v++) for (int f = 0; f < 2; f++) { float ww = f == 0 ? 4 : 1.6f; Col c = fl[v]; A("fly" + f + v, 12, 10, p => { p.Begin().Ellipse(-ww * .7f, -1.4f, ww * .75f, 2.2f).Ellipse(ww * .7f, -1.4f, ww * .75f, 2.2f).Ellipse(-ww * .5f, 1.4f, ww * .55f, 1.5f).Ellipse(ww * .5f, 1.4f, ww * .55f, 1.5f); p.Shade(K(c, 1.4f)); p.Begin().Rect(-.5f, -2.4f, 1, 5); p.Flat(0x3a2a28); }); }
        for (int f = 0; f < 2; f++) { float s = f == 0 ? 0 : 1; A("bunny" + f, 24, 22, p => { p.Begin().Ellipse(-2, 3 - s, 6 + s, 4.6f - s * .6f); p.Shade(K(0xb89878, 3)); p.Begin().Ellipse(-8.4f - s, 2 - s, 2, 2); p.Flat(0xf6ead8); p.Begin().Ellipse(5.6f + s, 0 - s, 3.4f, 3.2f); p.Shade(K(0xb89878)); p.Begin().Capsule(V(4, -2 - s), 1.3f, V(2.6f - s * 2, -8 - s), 1.1f).Capsule(V(6, -2 - s), 1.3f, V(6 - s * 3, -8.4f - s), 1.1f); p.Shade(K(0xc4a484, 1)); p.Dot(6.6f + s, -1 - s, 0x1a1014); p.Dot(8.6f + s, 0 - s, 0xd88a8a); p.Line(V(-5, 7 - s), V(-1 - s * 4, 7.6f - s), 1.6f, 0xa08468, false); p.Line(V(3, 6 - s), V(4 + s * 3, 7.6f - s), 1.4f, 0xa08468, false); }); }
        for (int v = 0; v < 2; v++) A("duck" + v, 26, 18, p => { p.Begin().Blob(V(-9, 0), V(-4, -2), V(4, -1), V(8, 2), V(4, 6), V(-6, 6)); p.Shade(K(v == 0 ? 0x9a8a78 : 0xb89a70, 3)); p.Begin().Ellipse(6, -4, 3.6f, 3.4f); p.Shade(K(v == 0 ? 0x3f7a52 : 0xb89a70)); p.Begin().Poly(V(8.6f, -4.4f), V(12, -3), V(8.6f, -2)); p.Flat(0xe8a040); if (v == 0) { p.Begin().Rect(4, -1, 4, 1); p.Flat(0xf6f0e0); } p.Begin().Blob(V(-6, 1), V(0, 0), V(3, 2), V(-4, 4)); p.Shade(K(v == 0 ? 0x7a6a5c : 0x9c8058, 1.4f)); p.Dot(7, -5, 0x1a1014); });
        for (int f = 0; f < 2; f++) A("frog" + f, 20, 16, p => { float q = f; p.Begin().Ellipse(0, 2 - q, 5.4f, 3.6f); p.Shade(K(0x7aa848, 2)); p.Begin().Ellipse(1.6f, -1.4f - q, 1.8f, 1.8f).Ellipse(4.2f, -1 - q, 1.6f, 1.6f); p.Shade(K(0x8aba54, 1)); p.Dot(2, -2 - q, 0x1a1014); p.Dot(4.6f, -2 - q, 0x1a1014); if (f == 0) { p.Begin().Ellipse(-4, 4, 3, 1.8f).Ellipse(4, 5, 2, 1.2f); p.Shade(K(0x6a9840, 1)); } else { p.Line(V(-4, 3), V(-8, 6), 1.8f, 0x6a9840, false); p.Line(V(3, 3), V(6, 4), 1.6f, 0x6a9840, false); } });
        for (int f = 0; f < 3; f++) { float a = f == 0 ? -5 : f == 1 ? -1 : 3; A("gull" + f, 30, 18, p => { p.Begin().Ellipse(0, 1, 5.4f, 2.6f); p.Shade(K(0xf6f2ea)); p.Begin().Ellipse(5, -.6f, 2.5f, 2.3f); p.Shade(K(0xf6f2ea)); p.Begin().Poly(V(7, -.6f), V(9.6f, .4f), V(7, 1)); p.Flat(0xf0b040); p.Begin().Poly(V(2, 0), V(-2, a - 1), V(-12, a + 1), V(-4, 1.4f)); p.Shade(K(0xc4ccd4, 1)); p.Begin().Poly(V(3, 0), V(6, a), V(13, a + 1.6f), V(5, 1.6f)); p.Shade(K(0xd8dee4, 1)); p.Dot(5, -1, 0x1a1014); }); }
        for (int f = 0; f < 2; f++) A("squirrel" + f, 24, 20, p => { p.Begin().Blob(V(-5, 4), V(-9, 0), V(-8, -6), V(-4, -7), V(-5, -2), V(-3, 3)); p.Shade(K(0xc87a48, 2)); p.Begin().Ellipse(0, 2 - f, 4.4f + f, 3.6f); p.Shade(K(0xb86a3c, 2)); p.Begin().Ellipse(4.4f + f, -1 - f, 2.8f, 2.6f); p.Shade(K(0xb86a3c)); p.Begin().Poly(V(3, -3 - f), V(3.6f, -5.6f - f), V(5, -3.4f - f)); p.Flat(0xb86a3c); p.Dot(5.4f + f, -2 - f, 0x1a1014); p.Line(V(-2, 5), V(-3 - f * 2, 6.6f), 1.4f, 0x9c5a34, false); p.Line(V(2, 5), V(3 + f * 2, 6.6f), 1.4f, 0x9c5a34, false); });
        for (int v = 0; v < 3; v++) { int vv = v; var p = new Painter(34, 16); FishDex.Draw(p, FishDex.All[v == 0 ? 0 : v == 1 ? 12 : 27], .2f + v * .03f, 17, 8, 0, true); an["fish" + v] = p.ToTexture(); }
    }
    void Populate()
    {
        void A(Kind k, float x, float y, int v = 0, Pool p = null) => animals.Add(new Animal { K = k, P = new(x, y), Home = new(x, y), Var = v, Pool = p, T = r.R(0, 5), Dir = r.P(.5f) ? 1 : -1 });
        for (int i = 0; i < 90; i++) { int x = r.I(160, 1400); if (w.PoolAt(x * 24, w.Top[x] * 24 - 4) == null && w.Solid(x, w.Top[x])) for (int k = 0; k < 1 + r.I(3); k++) A(Kind.Bird, x * 24 + k * 22, w.Top[Math.Min(World.W - 1, x + k)] * 24, r.I(4)); }
        for (int i = 0; i < 60; i++) { int x = r.P(.8f) ? r.I(600, 1066) : r.I(170, 460); A(Kind.Butterfly, x * 24, w.Top[x] * 24 - 30, r.I(4)); }
        for (int i = 0; i < 26; i++) { int x = r.P(.6f) ? r.I(600, 720) + (r.P(.5f) ? 220 : 0) : r.I(320, 450); A(Kind.Bunny, x * 24, w.Top[x] * 24); }
        for (int i = 0; i < 14; i++) { int x = r.I(170, 290) + (r.P(.5f) ? 160 : 0); A(Kind.Squirrel, x * 24, w.Top[x] * 24); }
        foreach (var p in w.Pools) { int n = p.Kind == Water.Sea ? 22 : p.Kind == Water.Lake ? 26 : 7; for (int i = 0; i < n; i++) A(Kind.Fish, r.R(p.X0 + 3, p.X1 - 3) * 24, p.Surf + r.R(26, 150), r.I(3), p); if (p.Kind == Water.Lake) for (int i = 0; i < 9; i++) A(Kind.Duck, r.R(p.X0 + 14, p.X1 - 24) * 24, p.Surf, r.I(2), p); }
        foreach (int x in new[] { 486, 494, 589, 598, 293, 314, 600, 316 }) A(Kind.Frog, x * 24, w.Top[x] * 24);
        for (int i = 0; i < 70; i++) { int x = r.P(.75f) ? r.I(600, 1060) : r.I(170, 460); A(Kind.Firefly, x * 24, w.Top[x] * 24 - r.R(10, 60)); }
        for (int i = 0; i < 9; i++) { int x = r.P(.5f) ? r.I(60, 150) : r.I(1400, 1460); A(Kind.Gull, x * 24, World.SeaY - r.R(130, 260)); }
        foreach (var l in w.Lamps.Where((_, i) => i % 4 == 0)) if (l.c.B > l.c.R) A(Kind.Moth, l.x, l.y - 20);
    }
    float GroundY(float x) => w.Top[Math.Clamp((int)(x / 24), 0, World.W - 1)] * 24;
    void Animals(float dt, Player pl, float cx, float cy, int vw, int vh)
    {
        var e = g.Env;
        foreach (var a in animals)
        {
            if (a.P.X < cx - 420 || a.P.X > cx + vw + 420) { if (a.S != 0 && a.K is Kind.Bird) { a.S = 0; a.P = a.Home; a.V = default; } if (a.K is Kind.Bunny or Kind.Squirrel && MathF.Abs(a.P.X - a.Home.X) > 8 && (a.Home.X < cx - 60 || a.Home.X > cx + vw + 60)) { a.P = a.Home; a.S = 0; a.A = a.B = a.Seen = 0; } continue; }
            a.T += dt; float dx = a.P.X - pl.P.X, dy = a.P.Y - pl.P.Y, dist = MathF.Sqrt(dx * dx + dy * dy);
            switch (a.K)
            {
                case Kind.Bird:
                    if (a.S == 0) { if (a.A > 0) { a.A -= dt; a.P.X += a.Dir * 34 * dt; a.P.Y = GroundY(a.P.X) - MathF.Abs(M.Sin(a.A * 16)) * 4; } else { a.P.Y = GroundY(a.P.X); if (r.P(dt * .5f)) { a.Dir = r.P(.5f) ? 1 : -1; a.A = r.R(.2f, .6f); } a.B = r.P(dt * .8f) ? .3f : a.B - dt; } if (r.P(dt * .035f) && e.Night < .4f && e.Rain < .4f) Snd.At("bird", a.P.X - cx - vw / 2, a.P.Y - cy - vh / 2, .5f, .9f + a.Var * .12f + r.F() * .1f); if (dist < (MathF.Abs(pl.V.X) > 90 ? 100 : 58)) { a.S = 1; a.Dir = dx > 0 ? 1 : -1; a.V = new(a.Dir * r.R(70, 110), -r.R(90, 130)); Snd.At("flap", dx, dy, .6f, .9f + r.F() * .3f); } }
                    else { a.V.Y += (a.T % 1f < .5f ? -30 : 10) * dt; a.V.X += a.Dir * 20 * dt; a.P += a.V * dt; if (a.P.Y < cy - 160) { a.P = a.Home; a.S = 0; a.V = default; a.P.X += 5000; a.P.X = a.Home.X; } }
                    break;
                case Kind.Butterfly: if (e.Night > .5f || e.Rain > .3f) break; a.V = M.Damp(a.V, new Vector2(M.Sin(a.T * .7f + a.Home.X) * 26 + e.Wind * 12, M.Sin(a.T * 1.9f) * 20), 3, dt) + (dist < 30 ? new Vector2(MathF.Sign(dx) * 60, -50) * dt : default); a.P += a.V * dt; a.P += (a.Home - a.P) * dt * .12f; float gy = GroundY(a.P.X) - 10; if (a.P.Y > gy) a.P.Y = gy; break;
                case Kind.Bunny: case Kind.Squirrel:
                    // S: 0 calm, 1 bolting, 2 hidden in the undergrowth
                    if (a.S == 2) { a.Seen -= dt; bool homeSafe = MathF.Abs(a.Home.X - pl.P.X) > 300 && (a.Home.X < cx - 40 || a.Home.X > cx + vw + 40); if (a.Seen < 0 && homeSafe) { a.P = a.Home; a.S = 0; a.A = a.B = a.Seen = 0; } break; }
                    if (a.A > 0)
                    {
                        a.A -= dt; float sp = a.S == 1 ? 205 : 46; a.P.X += a.Dir * sp * dt; a.B = MathF.Abs(M.Sin(a.A * (a.S == 1 ? 15 : 9))) * (a.S == 1 ? 11 : 6); if (w.PoolAt(a.P.X + a.Dir * 20, GroundY(a.P.X + a.Dir * 20) - 4) != null) { a.Dir *= -1; if (a.S == 1) a.A = 0; }
                        // a short sprint, then a dive into the grass, so nobody is herded along in front of you
                        if (a.S == 1 && a.A <= 0) { a.S = 2; a.Seen = r.R(8, 16); a.B = 0; Leaves(a.P + new Vector2(0, -6), 7, World.GrassCol(a.P.X / 24)); Snd.At("rustle", a.P.X - pl.P.X, 0, .5f, 1.1f); break; }
                    }
                    else { a.B = 0; a.S = 0; if (r.P(dt * .45f)) { a.Dir = MathF.Abs(a.P.X - a.Home.X) > 110 ? (a.P.X > a.Home.X ? -1 : 1) : r.P(.5f) ? 1 : -1; a.A = r.R(.3f, 1); } }
                    if (dist < 84 && a.S == 0) { a.S = 1; a.Dir = dx > 0 ? 1 : -1; if (MathF.Abs(pl.V.X) > 90 && MathF.Abs(dx) < 46 && r.P(.4f)) a.Dir *= -1; a.A = r.R(.45f, .85f); Snd.At("hop", dx, dy, .4f); }
                    a.P.Y = M.Damp(a.P.Y, GroundY(a.P.X), 16, dt); break;
                case Kind.Duck: a.V.X = M.Damp(a.V.X, dist < 90 ? MathF.Sign(dx) * 46 : a.Dir * 11, 2, dt); if (r.P(dt * .12f)) a.Dir *= -1; a.P.X += a.V.X * dt; if (a.P.X < (a.Pool.X0 + 8) * 24) a.Dir = 1; if (a.P.X > (a.Pool.X1 - 20) * 24) a.Dir = -1; a.P.X = M.Clamp(a.P.X, (a.Pool.X0 + 6) * 24, (a.Pool.X1 - 12) * 24); a.P.Y = a.Pool.Surf + M.Sin(a.P.X * .045f + e.T * 1.5f) * .6f + M.Sin(a.T * 2) * .6f; if (a.V.X != 0) a.Dir = a.V.X > 0 ? 1 : -1; if (r.P(dt * .05f)) Snd.At("duck", dx, dy, .4f, .9f + r.F() * .2f); if (r.P(dt * .4f)) g.R.Ripple(a.P.X, .7f); break;
                case Kind.Frog: if (a.A > 0) { a.A -= dt; a.P.X += a.Dir * 60 * dt; a.B = M.Sin(M.Clamp(a.A / .5f) * M.PI) * 16; if (a.A <= 0) a.B = 0; } else { if (dist < 60) { a.Dir = dx > 0 ? 1 : -1; a.A = .5f; Snd.At("hop", dx, dy, .5f, .8f); } else if (MathF.Abs(a.P.X - a.Home.X) > 70 && r.P(dt)) { a.Dir = a.P.X > a.Home.X ? -1 : 1; a.A = .5f; } } a.P.Y = GroundY(a.P.X); if (w.PoolAt(a.P.X, a.P.Y - 4) is Pool q) a.P.Y = q.Surf; if (r.P(dt * (.05f + e.Night * .12f + e.Rain * .12f))) Snd.At("frog", dx, dy, .5f, .9f + r.F() * .3f); break;
                case Kind.Firefly: case Kind.Moth: a.V = M.Damp(a.V, new(M.Sin(a.T * .8f + a.Home.X) * 14, M.Sin(a.T * 1.3f + a.Home.Y) * 12), 2, dt); a.P += a.V * dt + (a.Home - a.P) * dt * .2f; break;
                case Kind.Gull: a.P.X += a.Dir * 46 * dt; a.P.Y = a.Home.Y + M.Sin(a.T * .6f) * 16; if (MathF.Abs(a.P.X - a.Home.X) > 420) a.Dir = a.P.X > a.Home.X ? -1 : 1; if (r.P(dt * .06f) && e.Night < .4f) Snd.At("gull", dx, dy * .5f, .4f, .9f + r.F() * .2f); break;
                case Kind.Fish: { float ty = a.Home.Y + M.Sin(a.T * .5f + a.Home.X) * 12; a.V.X = M.Damp(a.V.X, dist < 60 ? MathF.Sign(dx) * 120 : a.Dir * (14 + a.Var * 6), 3, dt); a.P.X += a.V.X * dt; a.P.Y = M.Damp(a.P.Y, ty, 2, dt); int tx = (int)((a.P.X + a.Dir * 22) / 24), tyy = (int)(a.P.Y / 24); if (w.Solid(tx, tyy) || tx < a.Pool.X0 + 1 || tx > a.Pool.X1 - 1) a.Dir *= -1; if (r.P(dt * .08f)) a.Dir *= -1; if (r.P(dt * .03f)) Bubble(a.P); if (w.Solid((int)(a.P.X / 24), tyy)) { a.P = a.Home; if (w.Solid((int)(a.P.X / 24), (int)(a.P.Y / 24))) { a.Home.Y = a.Pool.Surf + 26; a.Home.X = (a.Pool.X0 + a.Pool.X1) * 12; } } break; }
            }
        }
    }
    public void DrawAnimals(int cx, int cy, int vw, bool water)
    {
        var e = g.Env; foreach (var a in animals)
        {
            float x = a.P.X - cx, y = a.P.Y - cy; if (x < -40 || x > vw + 40) continue; bool isW = a.K is Kind.Fish; if (isW != water) continue; string n; float oy = 0;
            switch (a.K)
            {
                case Kind.Bird: if (a.S == 0 && (e.Night > .75f || e.Rain > .6f)) continue; n = "bird" + (a.S == 0 ? 0 : 1 + (int)(a.T * 14) % 3) + a.Var; oy = a.S == 0 ? -6 + (a.B > 0 ? 1 : 0) : 0; break;
                case Kind.Butterfly: if (e.Night > .5f || e.Rain > .3f) continue; n = "fly" + (int)(a.T * 9) % 2 + a.Var; break;
                case Kind.Bunny: if (a.S == 2) continue; n = "bunny" + (a.B > 3 ? 1 : 0); oy = -9 - a.B; break;
                case Kind.Squirrel: if (a.S == 2) continue; n = "squirrel" + (a.B > 3 ? 1 : 0); oy = -8 - a.B; break;
                case Kind.Duck: n = "duck" + a.Var; oy = -3; break;
                case Kind.Frog: n = "frog" + (a.A > 0 ? 1 : 0); oy = -6 - a.B; break;
                case Kind.Gull: if (e.Night > .6f) continue; n = "gull" + ((int)(a.T * 5) % 4 is var q && q == 3 ? 1 : q); break;
                case Kind.Fish: { n = "fish" + a.Var; var t0 = an[n]; Ui.Tex(t0, MathF.Round(x - 17), MathF.Round(y - 8), 1, new Color(255, 255, 255, 120), a.V.X < 0); continue; }
                default: continue;
            }
            var t = an[n]; Ui.Tex(t, MathF.Round(x - t.Width / 2f), MathF.Round(y - t.Height / 2f + oy), 1, Color.White, a.Dir < 0);
        }
    }
    public void DrawGlows(int cx, int cy, int vw, Render R)
    {
        var e = g.Env; foreach (var a in animals)
        {
            if (a.K is not (Kind.Firefly or Kind.Moth)) continue; float x = a.P.X - cx, y = a.P.Y - cy; if (x < -20 || x > vw + 20) continue; float on = a.K == Kind.Moth ? .7f : e.Night * (1 - e.Rain); if (on < .05f) continue; float b = MathF.Pow(MathF.Max(0, M.Sin(a.T * 1.7f + a.Home.X)), 2) * on; if (b < .03f) continue; var c = a.K == Kind.Moth ? new Color(140, 245, 230, (int)(180 * b)) : new Color(230, 255, 120, (int)(200 * b)); Ui.GlowAt(x, y, 13, Raylib.Fade(c, b * .5f)); Raylib.DrawRectangle((int)x, (int)y, 2, 2, c);
        }
    }
    public void AddLights(Render R, float cx, float vw) { int n = 0; if (g.Env.Night > .4f) foreach (var a in animals) if (a.K == Kind.Firefly && a.P.X > cx && a.P.X < cx + vw && n++ < 5) R.Lights.Add((a.P.X, a.P.Y, 46, .5f * MathF.Pow(MathF.Max(0, M.Sin(a.T * 1.7f + a.Home.X)), 2), 0xd8f080)); }
}

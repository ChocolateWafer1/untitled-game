using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// Everything that moves on its own: grass, particles, weather, animals.
public sealed class Life
{
    struct Pt { public Vector2 P, V; public float T, L, S, G, D, R; public Color C; public int K; } // K 0 square 1 leaf 2 soft puff 3 drop 4 bubble 5 spark(glow) 6 smoke
    readonly List<Pt> pts = new(1200); readonly World w; readonly GameScreen g; readonly Rng r = new(321); readonly float[] bend = new float[World.W * 3], bv = new float[World.W * 3]; static Texture2D[] grass; static readonly Dictionary<string, Texture2D> an = new(); float leafT, moteT, rainT;
    enum Kind { Bird, Butterfly, Bunny, Duck, Frog, Firefly, Gull, Moth, Fish, Squirrel }
    sealed class Animal { public Kind K; public Vector2 P, V, Home; public float T, A, B; public int S, Dir = 1, Var; public Pool Pool; public float Seen; }
    readonly List<Animal> animals = new();
    public Life(World w, GameScreen g) { this.w = w; this.g = g; if (grass == null) { grass = new Texture2D[4]; for (int i = 0; i < 4; i++) grass[i] = Sprites.Get("grass" + i).Parts[0].T; Art(); } Populate(); }
    // ---------------------------------------------------------------- particles
    void Add(Vector2 p, Vector2 v, float life, float s, Color c, int k = 0, float grav = 0, float drag = 1) { if (pts.Count < 1500) pts.Add(new Pt { P = p, V = v, L = life, S = s, C = c, K = k, G = grav, D = drag, R = r.R(0, 6) }); }
    public void Dust(Vector2 p, string surf, int n, int dir) { Col c = surf switch { "grass" => World.GrassCol(p.X / 24).Shade(-.05f), "leaves" => r.P(.5f) ? 0xd8843a : 0xc9a347, "sand" => w.At((int)(p.X / 24), (int)((p.Y + 6) / 24)) == T.Ash ? 0x6a6468 : 0xe3c98e, "snow" => 0xf2f8ff, "wood" => 0xb08a60, "stone" => 0xa8a4a8, _ => 0xa88060 }; for (int i = 0; i < n; i++) Add(p + new Vector2(r.R(-5, 5), -1), new(dir * r.R(5, 30) + r.R(-24, 24), -r.R(6, 36)), r.R(.25f, .55f), r.R(1, 2.2f), c.Rl(.8f), surf == "leaves" ? 1 : surf == "stone" ? 0 : 2, 60, 3); }
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
            p.V.Y += p.G * dt; p.V *= MathF.Exp(-p.D * dt); if (p.K == 11) { p.V.X = M.Damp(p.V.X, 130 + MathF.Abs(wind) * 160, 2, dt); p.V.Y = M.Sin(p.T * 3 + p.R) * 9; } if (p.K == 1) { p.V.X += (wind * 46 + M.Sin(p.T * 3 + p.R) * 26) * dt * 2; p.V.Y = MathF.Min(p.V.Y, 26 + M.Sin(p.T * 4 + p.R) * 12); } if (p.K == 6) p.V.X += wind * 14 * dt; p.P += p.V * dt;
            int tx = (int)(p.P.X / 24), ty = (int)(p.P.Y / 24); if (p.K is 10 or 11 or 13 && w.Solid(tx, ty)) p.T = p.L; if (p.K is 12 or 14 && p.V.Y > 0 && w.Solid(tx, ty)) { p.P.Y = ty * 24 - .5f; p.V.Y *= -.34f; p.V.X *= .5f; } if (p.K is 1 or 3 && w.Solid(tx, ty)) { if (p.K == 3) { p.T = p.L; } else { p.V = default; p.G = 0; p.P.Y = ty * 24 - .5f; p.K = 7; p.L = p.T + 3; } } if (p.K == 3 && p.V.Y > 40 && w.PoolAt(p.P.X, p.P.Y) != null) p.T = p.L; if (p.K == 4 && w.PoolAt(p.P.X, p.P.Y - 3) == null) p.T = p.L; pts[i] = p;
        }
        for (int i = flashes.Count - 1; i >= 0; i--) { var f = flashes[i]; f.t += dt; if (f.t > .32f) flashes.RemoveAt(i); else flashes[i] = f; }
        // ambient leaves, petals, pollen
        float ctx = (cx + vw / 2f) / 24, cty = (cy + vh / 2f) / 24; bool under = g.Under > .5f; var bd = Biomes.Of(w.Here((int)ctx, (int)(pl.P.Y / 24) - 1, under)); Drifting(dt, bd, under, cx, cy, vw, vh);
        moteT -= dt; if (moteT < 0 && !under && e.Rain < .2f) { moteT = .12f; float a = e.Golden * .8f + .2f; Add(new(cx + r.R(0, vw), cy + r.R(vh * .2f, vh)), new(wind * 8 + r.R(-4, 4), r.R(-7, 2)), r.R(3, 6), 1, Col.Lerp(e.Sun, new Col(1, .95f, .8f), .5f).Rl(a * .5f), 8, 0, .1f); } else if (moteT < 0 && under) { moteT = .25f; Add(new(cx + r.R(0, vw), cy + r.R(0, vh)), new(r.R(-4, 4), r.R(-6, 1)), r.R(3, 6), 1, new Color(130, 240, 225, 130), 8, 0, .1f); }
        if (e.Snow > .05f && !under) { int n = 0; foreach (var q in pts) if (q.K == 10) n++; for (int k = 0; k < 3 && n < 260 * e.Snow; k++, n++) Add(new(cx + r.R(-80, vw + 60) - wind * 60, cy - r.R(4, 40)), new(wind * 34 + r.R(-8, 8), r.R(26, 54)), 14, r.P(.3f) ? 2 : 1, new Color(250, 252, 255, 220), 10, 0, 0); for (int i = 0; i < pts.Count; i++) if (pts[i].K == 10) { var q = pts[i]; q.P.X += M.Sin(q.T * 1.6f + q.R) * 9 * dt; int sx = (int)(q.P.X / 24), sy = (int)(q.P.Y / 24); if (w.Solid(sx, sy) || w.PoolAt(q.P.X, q.P.Y) != null) q.T = q.L + 1; pts[i] = q; } }
        // rain
        if (e.Rain > .05f && !under) { rainT += dt * e.Rain * vw * .55f; while (rainT > 1) { rainT -= 1; float x = cx + r.R(-80, vw + 80) - wind * 50; Add(new(x, cy - 14), new(wind * 110 - 14, r.R(430, 540)), 1.4f, r.R(6, 11), new Color(190, 212, 230, 120), 9, 0, 0); } for (int i = pts.Count - 1; i >= 0; i--) { if (pts[i].K != 9) continue; var p = pts[i].P; int tx = (int)(p.X / 24), ty = (int)(p.Y / 24); if (w.Solid(tx, ty) || w.PoolAt(p.X, p.Y) != null) { if (r.P(.55f)) { Add(new(p.X, w.Solid(tx, ty) ? ty * 24 - 1 : p.Y - 2), new(r.R(-22, 22), -r.R(16, 46)), .28f, 1, new Color(206, 226, 240, 170), 3, 220, 0); } pts[i] = pts[^1]; pts.RemoveAt(pts.Count - 1); } } }
        // grass springs
        int g0 = Math.Max(0, (int)(cx / 24) - 2) * 3, g1 = Math.Min(World.W - 1, (int)((cx + vw) / 24) + 2) * 3; for (int i = g0; i < g1; i++) { float gx = i * 8 + 4, d = gx - pl.P.X; float push = 0; if (MathF.Abs(d) < 15 && w.TurfAt((int)(gx / 24), (int)((pl.P.Y + 4) / 24))) { float gy = pl.P.Y; push = MathF.Sign(d) * (1 - MathF.Abs(d) / 15) * 9 + pl.V.X * .03f; if (MathF.Abs(pl.V.X) > 40 && r.P(dt * 1.2f)) { Snd.Play("rustle", .10f, 1.2f + r.F() * .4f); Add(new(gx, gy - 5), new(pl.V.X * .2f + r.R(-20, 20), -r.R(10, 40)), .6f, 1, World.GrassCol(gx / 24).Shade(.1f).Rl(), 1, 60, 1); } } bv[i] += ((push - bend[i]) * 130 - bv[i] * 9) * dt; bend[i] += bv[i] * dt; }
        Animals(dt, pl, cx, cy, vw, vh); Extras(dt, cx, cy, vw);
    }
    public void DrawGrass(int cx, int cy, int vw, int vh, bool front)
    {
        float t = g.Env.T, wind = g.Env.Wind; int x0 = Math.Max(0, cx / 24 - 1), x1 = Math.Min(World.W - 1, (cx + vw) / 24 + 1), y0 = Math.Max(1, cy / 24 - 1), y1 = Math.Min(World.H - 1, (cy + vh) / 24 + 1);
        for (int x = x0; x <= x1; x++)
        {
            for (int ty = y0; ty <= y1; ty++) if (w.Turf[ty * World.W + x] && !w.Solid(x, ty - 1)) { float y = ty * 24 - cy; if (w.Wet[(ty - 1) * World.W + x] != 0) continue;
            for (int k = 0; k < 3; k++) { float h = M.Hash(x, k + ty * 3, 3); if ((h > .62f) != front) continue; if (M.Hash(x, k, 9) < .12f) continue; float gx = x * 24 + 4 + k * 8 + h * 3; Col c = World.GrassCol(gx / 24).Shade((M.Hash(x, k, 5) - .5f) * .3f + (front ? -.1f : .06f)); float sw = M.Sin(g.Env.GrassT + gx * .035f) * (1 + MathF.Abs(wind) * 2.2f) + wind * 2.6f + (MathF.Abs(ty * 24 - g.P.P.Y) < 40 ? bend[x * 3 + k] : 0); sw = MathF.Round(sw * 2) / 2; Shear(grass[(int)(M.Hash(x, k, 7) * 4)], MathF.Round(gx - cx) - 10, MathF.Round(y + 3 - 16 + (h * 4)), sw, c.Rl(), h > .5f); } }
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
            float k = p.T / p.L; float x = MathF.Round(p.P.X - cx), y = MathF.Round(p.P.Y - cy); bool gl = p.K is 5 or 8 or 13; if (gl != glow) continue; var c = p.C; float fa = k > .7f ? (1 - k) / .3f : 1; if (p.K == 8) fa = M.Sin(M.Clamp(k) * M.PI); c.A = (byte)(c.A * fa);
            switch (p.K) { case 1: case 7: { float a = p.K == 7 ? 0 : M.Sin(p.T * 5 + p.R); Raylib.DrawRectangle((int)x - 1, (int)y, a > 0 ? 3 : 2, a > .3f ? 1 : 2, c); break; } case 2: { float s = p.S * (1 + k * 1.4f); c.A = (byte)(c.A * (1 - k) * .7f); Raylib.DrawRectangle((int)(x - s), (int)(y - s), (int)(s * 2), (int)(s * 2), c); break; } case 4: Raylib.DrawRectangleLines((int)x - 1, (int)y - 1, (int)p.S + 2, (int)p.S + 2, c); break; case 6: { float s = p.S * (1 + k * 2); c.A = (byte)(c.A * (1 - k)); Raylib.DrawCircle((int)x, (int)y, s, c); break; } case 11: Raylib.DrawRectangle((int)x, (int)y, (int)(3 + p.S * 4), 1, c); break; case 9: { var d = Vector2.Normalize(p.V) * p.S; Raylib.DrawLine((int)x, (int)y, (int)(x - d.X), (int)(y - d.Y), c); break; } case 13: { var d = p.V.LengthSquared() > 1 ? Vector2.Normalize(p.V) * p.S : default; Raylib.DrawLineV(new(x, y), new(x - d.X, y - d.Y), c); Raylib.DrawRectangle((int)x, (int)y, 1, 1, new Color(255, 255, 240, (int)c.A)); break; } case 14: { float a = p.R + MathF.Min(p.T, p.V.LengthSquared() > 400 ? p.T : .4f) * 14; var d = new Vector2(MathF.Cos(a), MathF.Sin(a)) * p.S * .5f; Raylib.DrawLineV(new(x - d.X, y - d.Y), new(x + d.X, y + d.Y), c); break; } default: Raylib.DrawRectangle((int)x, (int)y, Math.Max(1, (int)p.S), Math.Max(1, (int)p.S), c); break; }
        }
    }
    public void Smoke(Vector2 p, float s, Color c) => Add(p, new(r.R(-4, 4), -r.R(10, 18)), r.R(2, 3.4f), s, c, 6, 0, .3f);
    public void Flame(Vector2 p) { Add(p + new Vector2(r.R(-5, 5), 0), new(r.R(-6, 6), -r.R(18, 46)), r.R(.3f, .6f), r.R(1.5f, 3.4f), r.P(.5f) ? new Color(255, 190, 80, 255) : new Color(255, 120, 50, 255), 5, -30, 1); if (r.P(.12f)) Add(p + new Vector2(r.R(-5, 5), -8), new(r.R(-14, 14), -r.R(30, 70)), r.R(.8f, 1.6f), 1, new Color(255, 200, 110, 255), 5, -10, .6f); }
    public void Drop(Vector2 p, Vector2 v, float life) => Add(p, v, life, r.R(1, 2), new Color(190, 226, 240, 220), 3, 260, 0);
    public void Foam(Vector2 p) => Add(p, new(r.R(-34, 34), -r.R(10, 60)), r.R(.4f, .9f), r.R(1, 2), new Color(236, 246, 250, 190), 3, 160, 1);
    public int CrittersAhead(Vector2 p, int dir, float range) { int n = 0; foreach (var a in animals) if (a.K is Kind.Bunny or Kind.Squirrel && a.S != 2 && (a.P.X - p.X) * dir is > -20 and var q && q < range) n++; return n; }
    public void Scatter(float cx, float cy, int vw) { foreach (var a in animals) if (a.K == Kind.Bird && a.P.X > cx - 200 && a.P.X < cx + vw + 200 && a.S == 0) { a.S = 1; a.Dir = r.P(.5f) ? 1 : -1; a.V = new(a.Dir * r.R(60, 120), -r.R(90, 150)); } Snd.Play("flap", .7f); }
    float showerT, showerLen, showerAcc; int showerKind; readonly List<(Vector2 p, float t, float ph, float life)> glowers = new(); sealed class Flier { public Vector2 P, V; public float Ph, Off; public int Var; } readonly List<Flier> fliers = new();
    public void Petals(int kind, float seconds) { showerKind = kind; showerT = 0; showerLen = seconds; }
    public void Flock(float cx, float cy, int vw) { int n = 13 + r.I(6); float y = cy + r.R(50, 110); for (int i = 0; i < n; i++) { int row = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1; fliers.Add(new Flier { P = new(cx - 50 - row * 17 - r.R(0, 6), y + row * side * 8 + r.R(-3, 3)), V = new(r.R(84, 92), 0), Ph = r.F() * 6, Off = row * side * 8, Var = 0 }); } Snd.Play("flap", .4f); }
    public void Swarm(Vector2 c) { for (int i = 0; i < 54; i++) glowers.Add((c + new Vector2(r.R(-260, 260), r.R(-120, 20)), -r.R(0, 5), r.R(0, 6.28f), r.R(18, 34))); }
    void Extras(float dt, float cx, float cy, int vw)
    {
        if (showerT < showerLen) { showerT += dt; float k = M.Smooth(0, 3, showerT) * (1 - M.Smooth(showerLen - 4, showerLen, showerT)); showerAcc += dt * 26 * k; while (showerAcc > 1) { showerAcc -= 1; Col c = showerKind == 0 ? (r.P(.5f) ? 0xf2bcc8 : 0xf8d8de) : r.P(.4f) ? 0xd8743a : r.P(.5f) ? 0xe0a83e : 0xc4523a; Add(new(cx + r.R(-160, vw + 20), cy - r.R(4, 30)), new(r.R(10, 40), r.R(12, 30)), r.R(9, 14), 2, c.Rl(), 1, 16, .4f); } }
        for (int i = fliers.Count - 1; i >= 0; i--) { var f = fliers[i]; f.Ph += dt; f.P += new Vector2(f.V.X + M.Sin(f.Ph * .7f) * 5, M.Sin(f.Ph * .9f + f.Off) * 6) * dt; if (f.P.X > cx + vw + 140) fliers.RemoveAt(i); }
        for (int i = glowers.Count - 1; i >= 0; i--) { var q = glowers[i]; q.t += dt; q.p += new Vector2(M.Sin(q.t * .8f + q.ph * 3) * 13, M.Sin(q.t * 1.2f + q.ph) * 10) * dt; if (q.t > q.life) glowers.RemoveAt(i); else glowers[i] = q; }
    }

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
        for (int v = 0; v < 3; v++) { var p = new Painter(34, 16); p.View(1, 17, 8); float L = 9 + v * 1.6f, hh = 3 + v * .6f; p.Begin().Blob(V(L, 0), V(L * .4f, -hh), V(-L * .5f, -hh * .6f), V(-L, 0), V(-L * .5f, hh * .6f), V(L * .4f, hh)).Poly(V(-L * .8f, 0), V(-L * 1.4f, -hh), V(-L * 1.15f, 0), V(-L * 1.4f, hh)); p.Flat(0x15141c); an["fish" + v] = p.ToTexture(); }
    }
    void Populate()
    {
        void A(Kind k, float x, float y, int v = 0, Pool p = null) => animals.Add(new Animal { K = k, P = new(x, y), Home = new(x, y), Var = v, Pool = p, T = r.R(0, 5), Dir = r.P(.5f) ? 1 : -1 }); float G(int x) => w.Top[Math.Clamp(x, 0, World.W - 1)] * 24; bool Dry(int x) => !w.WetTile(x, w.Top[x] - 1) && w.Top[x] < World.H - 4;
        foreach (var s in w.Segs)
        {
            int len = s.X1 - s.X0; int X() => r.I(s.X0 + 6, s.X1 - 6); void Many(Kind k, float per, float up = 0) { int n = (int)(len * per / 100 + r.F()); for (int i = 0; i < n; i++) { int x = X(); if (Dry(x)) A(k, x * 24, G(x) - up * r.R(.4f, 1.4f), r.I(4)); } } void Birds(float per) { int n = (int)(len * per / 100 + r.F()); for (int i = 0; i < n; i++) { int x = X(); if (Dry(x)) for (int k = 0; k < 1 + r.I(3); k++) A(Kind.Bird, x * 24 + k * 22, G(x + k), r.I(4)); } }
            switch (s.B)
            {
                case Biome.Meadow: Birds(6); Many(Kind.Butterfly, 9, 30); Many(Kind.Bunny, 5); Many(Kind.Firefly, 9, 40); break;
                case Biome.Amber: Birds(7); Many(Kind.Squirrel, 5); Many(Kind.Bunny, 2); Many(Kind.Firefly, 4, 40); break;
                case Biome.Highlands: Birds(5); Many(Kind.Squirrel, 4); Many(Kind.Bunny, 2); break;
                case Biome.Frost: Many(Kind.Bunny, 5); Birds(2); break;
                case Biome.Blossom: Birds(6); Many(Kind.Butterfly, 10, 30); Many(Kind.Bunny, 3); Many(Kind.Firefly, 5, 40); break;
                case Biome.Jungle: Birds(8); Many(Kind.Butterfly, 12, 60); Many(Kind.Frog, 4); Many(Kind.Firefly, 6, 60); break;
                case Biome.Marsh: Birds(2); Many(Kind.Frog, 9); Many(Kind.Firefly, 16, 40); break;
                case Biome.Bamboo: Birds(6); Many(Kind.Butterfly, 5, 40); Many(Kind.Squirrel, 2); break;
                case Biome.Lavender: Birds(4); Many(Kind.Butterfly, 14, 24); Many(Kind.Bunny, 5); Many(Kind.Firefly, 7, 40); break;
                case Biome.Toadstool: Many(Kind.Moth, 10, 50); Many(Kind.Frog, 3); Many(Kind.Butterfly, 5, 40); break;
                case Biome.Moonpetal: Many(Kind.Moth, 14, 60); Many(Kind.Firefly, 8, 50); Many(Kind.Bunny, 3); break;
                case Biome.Crystal: Many(Kind.Moth, 6, 50); break;
                case Biome.Mesa: case Biome.Dunes: Birds(2.4f); break;
                case Biome.Coast: for (int i = 0; i < 6; i++) A(Kind.Gull, r.I(s.X0 + 50, s.X1 - 50) * 24, World.SeaY - r.R(130, 260)); break;
            }
        }
        foreach (var p in w.Pools)
        {
            int wide = p.X1 - p.X0; if (p.Kind == Water.Spring || wide < 5) continue; int n = p.Kind == Water.Sea ? 22 : Math.Min(22, wide / 3); for (int i = 0; i < n; i++) { float fx = r.R(p.X0 + 1, p.X1 - 1) * 24, fy = p.Surf + r.R(24, MathF.Max(30, (p.Bottom - p.Row) * 20)); if (w.PoolAt(fx, fy) == p) A(Kind.Fish, fx, fy, r.I(3), p); }
            var b = w.BiomeAt((p.X0 + p.X1) / 2); if (p.Kind == Water.Lake && wide > 24 && b is not (Biome.Frost or Biome.Crystal or Biome.Dunes)) for (int i = 0; i < 4; i++) { float dx = r.R(p.X0 + 8, p.X1 - 8) * 24; if (w.PoolAt(dx, p.Surf + 4) == p) A(Kind.Duck, dx, p.Surf, r.I(2), p); }
        }
        foreach (var l in w.Lamps.Where((q, i) => i % 5 == 0 && q.Y > w.Top0[Math.Clamp((int)(q.X / 24), 0, World.W - 1)] * 24 + 60)) if (l.C.B > l.C.R) A(Kind.Moth, l.X, l.Y - 20);
    }
    float GroundY(float x, float y) => w.GroundNear(x, y);
    float drift;
    /// What hangs in the air here: leaves, snow, ash, spores, blown sand, petals, sparks of light.
    void Drifting(float dt, BiomeDef b, bool under, float cx, float cy, int vw, int vh)
    {
        var e = g.Env; float wind = e.Wind; drift += dt * b.AirRate * (under ? .7f : 1) * 2.4f; int n = 0; while (drift > 1 && n++ < 6)
        {
            drift -= 1; Col c = b.AirCols[r.I(b.AirCols.Length)]; float x = cx + r.R(-70, vw + 60) - wind * 40, y = cy + r.R(-20, vh * .55f); int li = Math.Clamp((int)(y / 24), 0, World.H - 1) * World.W + Math.Clamp((int)(x / 24), 0, World.W - 1); bool open = w.Light[li] > .9f; if (!under && !open && b.Air is Drift.Leaves or Drift.Petals or Drift.Snow or Drift.Ash or Drift.Sand) continue;
            switch (b.Air)
            {
                case Drift.Leaves: case Drift.Petals: if (r.P(.55f)) Add(new(x, y), new(wind * 30, 14), 9, 2, c.Rl(), 1, 14, .4f); break;
                case Drift.Snow: Add(new(x, cy - r.R(0, 30)), new(wind * 30 + r.R(-8, 8), r.R(22, 44)), 14, r.P(.25f) ? 2 : 1, new Color(250, 252, 255, 210), 10); break;
                case Drift.Ash: if (c.R > .9f) Add(new(x, cy + r.R(vh * .4f, vh)), new(wind * 16 + r.R(-8, 8), -r.R(10, 30)), r.R(2, 4.5f), 1, c.Rl(), 5, -4, .1f); else Add(new(x, cy - r.R(0, 30)), new(wind * 18 + r.R(-6, 6), r.R(14, 30)), 14, r.P(.3f) ? 2 : 1, c.Rl(.75f), 10); break;
                case Drift.Sand: Add(new(cx - r.R(0, 120), cy + r.R(vh * .25f, vh)), new(r.R(90, 160), 0), r.R(3, 6), r.R(.4f, 1.6f), c.Rl(.5f), 11); break;
                case Drift.Spores: case Drift.Glow: case Drift.Shards: Add(new(cx + r.R(0, vw), cy + r.R(0, vh)), new(wind * 6 + r.R(-5, 5), r.R(-9, 3)), r.R(3, 6), r.P(.3f) ? 2 : 1, c.Rl(.85f), 8, 0, .1f); break;
                case Drift.Mist: if (r.P(.5f)) Add(new(cx + r.R(0, vw), cy + r.R(vh * .4f, vh * .8f)), new(r.R(-4, 4), r.R(-5, 2)), r.R(3, 5), 1, c.Rl(.7f), 8, 0, .1f); break;
                default: if (r.P(.4f)) Add(new(x, cy + r.R(0, vh)), new(wind * 14 + r.R(-4, 4), r.R(-5, 6)), r.R(4, 7), 1, c.Rl(.7f), 8, 0, .1f); break;
            }
        }
    }
    /// What flies off a block when it is hit or breaks, by what it is made of.
    public void Debris(T b, Vector2 p, Vector2 dir, int n, bool burst)
    {
        Col c = Blocks.Of(b).Color; Vector2 RV(float k) => new(r.R(-k, k), r.R(-k, k));
        switch (Blocks.Mat(b))
        {
            case "dirt": for (int i = 0; i < n; i++) Add(p + RV(5), dir * r.R(15, 55) + new Vector2(r.R(-40, 40), -r.R(20, 90)), r.R(.5f, 1), r.R(2, 3.6f), c.Shade(r.R(-.25f, .15f)).Rl(), 12, 460, .4f); Puff(p, burst ? 6 : 2, c.Shade(.15f), burst ? 12 : 6); break;
            case "sand": for (int i = 0; i < n * 3; i++) Add(p + new Vector2(r.R(-7, 7), r.R(-2, 8)), new Vector2(dir.X * r.R(5, 30) + r.R(-12, 12), r.R(-30, 20)), r.R(.6f, 1.3f), 1, c.Shade(r.R(-.2f, .2f)).Rl(), 3, 520, .3f); Puff(p, burst ? 5 : 1, c.Shade(.25f), 8); break;
            case "wood": for (int i = 0; i < n; i++) Add(p + RV(4), dir * r.R(30, 90) + new Vector2(r.R(-40, 40), -r.R(30, 100)), r.R(.5f, 1), r.R(3, 5), c.Shade(r.R(-.2f, .3f)).Rl(), 14, 420, .6f); Puff(p, burst ? 4 : 1, c.Shade(.3f), 8); break;
            case "snow": for (int i = 0; i < n * 2; i++) Add(p + RV(6), new Vector2(r.R(-50, 50), -r.R(10, 70)) + dir * 20, r.R(.6f, 1.4f), r.R(1, 2.2f), new Color(248, 252, 255, 230), 0, 120, 1.6f); Puff(p, burst ? 8 : 3, 0xf4f8ff, 10); break;
            case "ice" or "crystal": for (int i = 0; i < n; i++) Add(p + RV(5), dir * r.R(30, 90) + new Vector2(r.R(-60, 60), -r.R(30, 110)), r.R(.4f, .9f), r.R(1, 2.6f), c.Shade(r.R(0, .5f)).Rl(.85f), 12, 420, .5f); Sparkle(p, burst ? 8 : 2, new Color(230, 246, 255, 255)); break;
            default: Chips(p, n, c, dir); Puff(p, burst ? 6 : 2, c.Shade(.2f), burst ? 14 : 5); if (b == T.Moss && burst) Leaves(p, 4, 0x6f8a4a); break;
        }
    }
    /// Hot little streaks off stone and ore; each burst lights the dark around it for a moment.
    readonly List<(Vector2 p, float t, float a, Col c)> flashes = new();
    public void Sparks(Vector2 p, Vector2 dir, int n, Col c, float light)
    {
        for (int i = 0; i < n; i++) Add(p, dir * r.R(60, 170) + new Vector2(r.R(-90, 90), -r.R(40, 140)), r.R(.22f, .5f), r.R(2, 4), c.Shade(r.R(0, .4f)).Rl(), 13, 380, 1.2f);
        flashes.Add((p, 0, light, c)); if (flashes.Count > 6) flashes.RemoveAt(0);
    }
    /// Little square crumbs thrown off by a hit.
    public void Chips(Vector2 p, int n, Col c, Vector2 dir, float speed = 70) { for (int i = 0; i < n; i++) Add(p + new Vector2(r.R(-5, 5), r.R(-5, 5)), dir * speed * r.R(.3f, 1) + new Vector2(r.R(-50, 50), r.R(-80, 10)), r.R(.35f, .8f), r.R(1, 3.2f), c.Shade(r.R(-.3f, .35f)).Rl(), 12, 380, .6f); }
    public void Puff(Vector2 p, int n, Col c, float spread = 10) { for (int i = 0; i < n; i++) { float a = r.R(0, M.TAU); Add(p + new Vector2(M.Sin(a), M.Cos(a)) * spread * .6f, new Vector2(M.Sin(a), M.Cos(a)) * r.R(6, 34), r.R(.3f, .7f), r.R(1.4f, 3), c.Rl(.6f), 2, 0, 3); } }
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
                    if (a.S == 0) { if (a.A > 0) { a.A -= dt; a.P.X += a.Dir * 34 * dt; a.P.Y = GroundY(a.P.X, a.P.Y) - MathF.Abs(M.Sin(a.A * 16)) * 4; } else { a.P.Y = GroundY(a.P.X, a.P.Y); if (r.P(dt * .5f)) { a.Dir = r.P(.5f) ? 1 : -1; a.A = r.R(.2f, .6f); } a.B = r.P(dt * .8f) ? .3f : a.B - dt; } if (r.P(dt * .035f) && e.Night < .4f && e.Rain < .4f) Snd.At("bird", a.P.X - cx - vw / 2, a.P.Y - cy - vh / 2, .5f, .9f + a.Var * .12f + r.F() * .1f); if (dist < (MathF.Abs(pl.V.X) > 90 ? 100 : 58)) { a.S = 1; a.Dir = dx > 0 ? 1 : -1; a.V = new(a.Dir * r.R(70, 110), -r.R(90, 130)); Snd.At("flap", dx, dy, .6f, .9f + r.F() * .3f); } }
                    else { a.V.Y += (a.T % 1f < .5f ? -30 : 10) * dt; a.V.X += a.Dir * 20 * dt; a.P += a.V * dt; if (a.P.Y < cy - 160) { a.P = a.Home; a.S = 0; a.V = default; a.P.X += 5000; a.P.X = a.Home.X; } }
                    break;
                case Kind.Butterfly: if (e.Night > .5f || e.Rain > .3f) break; a.V = M.Damp(a.V, new Vector2(M.Sin(a.T * .7f + a.Home.X) * 26 + e.Wind * 12, M.Sin(a.T * 1.9f) * 20), 3, dt) + (dist < 30 ? new Vector2(MathF.Sign(dx) * 60, -50) * dt : default); a.P += a.V * dt; a.P += (a.Home - a.P) * dt * .12f; float gy = GroundY(a.P.X, a.P.Y) - 10; if (a.P.Y > gy) a.P.Y = gy; break;
                case Kind.Bunny: case Kind.Squirrel:
                    // S: 0 calm, 1 bolting, 2 hidden in the undergrowth
                    if (a.S == 2) { a.Seen -= dt; bool homeSafe = MathF.Abs(a.Home.X - pl.P.X) > 300 && (a.Home.X < cx - 40 || a.Home.X > cx + vw + 40); if (a.Seen < 0 && homeSafe) { a.P = a.Home; a.S = 0; a.A = a.B = a.Seen = 0; } break; }
                    if (a.A > 0)
                    {
                        a.A -= dt; float sp = a.S == 1 ? 205 : 46; a.P.X += a.Dir * sp * dt; a.B = MathF.Abs(M.Sin(a.A * (a.S == 1 ? 15 : 9))) * (a.S == 1 ? 11 : 6); if ((w.PoolAt(a.P.X + a.Dir * 20, GroundY(a.P.X + a.Dir * 20, a.P.Y) - 4) != null || MathF.Abs(GroundY(a.P.X + a.Dir * 20, a.P.Y) - a.P.Y) > 60)) { a.Dir *= -1; if (a.S == 1) a.A = 0; }
                        // a short sprint, then a dive into the grass, so nobody is herded along in front of you
                        if (a.S == 1 && a.A <= 0) { a.S = 2; a.Seen = r.R(8, 16); a.B = 0; Leaves(a.P + new Vector2(0, -6), 7, World.GrassCol(a.P.X / 24)); Snd.At("rustle", a.P.X - pl.P.X, 0, .5f, 1.1f); break; }
                    }
                    else { a.B = 0; a.S = 0; if (r.P(dt * .45f)) { a.Dir = MathF.Abs(a.P.X - a.Home.X) > 110 ? (a.P.X > a.Home.X ? -1 : 1) : r.P(.5f) ? 1 : -1; a.A = r.R(.3f, 1); } }
                    if (dist < 84 && a.S == 0) { a.S = 1; a.Dir = dx > 0 ? 1 : -1; if (MathF.Abs(pl.V.X) > 90 && MathF.Abs(dx) < 46 && r.P(.4f)) a.Dir *= -1; a.A = r.R(.45f, .85f); Snd.At("hop", dx, dy, .4f); }
                    a.P.Y = M.Damp(a.P.Y, GroundY(a.P.X, a.P.Y - 2), 16, dt); break;
                case Kind.Duck: a.V.X = M.Damp(a.V.X, dist < 90 ? MathF.Sign(dx) * 46 : a.Dir * 11, 2, dt); if (r.P(dt * .12f)) a.Dir *= -1; a.P.X += a.V.X * dt; if (a.P.X < (a.Pool.X0 + 4) * 24) a.Dir = 1; if (a.P.X > (a.Pool.X1 - 4) * 24) a.Dir = -1; if (w.PoolAt(a.P.X + a.Dir * 16, a.Pool.Surf + 4) != a.Pool) { a.Dir *= -1; a.P.X += a.Dir * 2; a.V.X = a.Dir * 11; } a.P.Y = a.Pool.Surf + M.Sin(a.P.X * .045f + e.T * 1.5f) * .6f + M.Sin(a.T * 2) * .6f; if (a.V.X != 0) a.Dir = a.V.X > 0 ? 1 : -1; if (r.P(dt * .05f)) Snd.At("duck", dx, dy, .4f, .9f + r.F() * .2f); if (r.P(dt * .4f)) g.R.Ripple(a.P.X, .7f); break;
                case Kind.Frog: if (a.A > 0) { a.A -= dt; a.P.X += a.Dir * 60 * dt; a.B = M.Sin(M.Clamp(a.A / .5f) * M.PI) * 16; if (a.A <= 0) a.B = 0; } else { if (dist < 60) { a.Dir = dx > 0 ? 1 : -1; a.A = .5f; Snd.At("hop", dx, dy, .5f, .8f); } else if (MathF.Abs(a.P.X - a.Home.X) > 70 && r.P(dt)) { a.Dir = a.P.X > a.Home.X ? -1 : 1; a.A = .5f; } } a.P.Y = GroundY(a.P.X, a.P.Y); if (w.PoolAt(a.P.X, a.P.Y - 4) is Pool q) a.P.Y = q.Surf; if (r.P(dt * (.05f + e.Night * .12f + e.Rain * .12f))) Snd.At("frog", dx, dy, .5f, .9f + r.F() * .3f); break;
                case Kind.Firefly: case Kind.Moth: a.V = M.Damp(a.V, new(M.Sin(a.T * .8f + a.Home.X) * 14, M.Sin(a.T * 1.3f + a.Home.Y) * 12), 2, dt); a.P += a.V * dt + (a.Home - a.P) * dt * .2f; break;
                case Kind.Gull: a.P.X += a.Dir * 46 * dt; a.P.Y = a.Home.Y + M.Sin(a.T * .6f) * 16; if (MathF.Abs(a.P.X - a.Home.X) > 420) a.Dir = a.P.X > a.Home.X ? -1 : 1; if (r.P(dt * .06f) && e.Night < .4f) Snd.At("gull", dx, dy * .5f, .4f, .9f + r.F() * .2f); break;
                case Kind.Fish: { float ty = a.Home.Y + M.Sin(a.T * .5f + a.Home.X) * 12; a.V.X = M.Damp(a.V.X, dist < 60 ? MathF.Sign(dx) * 120 : a.Dir * (14 + a.Var * 6), 3, dt); a.P.X += a.V.X * dt; a.P.Y = M.Damp(a.P.Y, ty, 2, dt); int tx = (int)((a.P.X + a.Dir * 22) / 24), tyy = (int)(a.P.Y / 24); if (!w.WetTile(tx, tyy)) { a.Dir *= -1; a.V.X = a.Dir * 14; } if (r.P(dt * .08f)) a.Dir *= -1; if (r.P(dt * .03f)) Bubble(a.P); if (!w.WetTile((int)(a.P.X / 24), tyy)) { a.P = a.Home; a.V = default; if (w.PoolAt(a.P.X, a.P.Y) == null) a.S = 9; } break; }
            }
        }
    }
    public void DrawAnimals(int cx, int cy, int vw, bool water)
    {
        var e = g.Env; if (!water) foreach (var f in fliers) { var ft = an["gull" + ((int)(f.Ph * 6) % 4 is var fq && fq == 3 ? 1 : fq)]; Ui.Tex(ft, MathF.Round(f.P.X - cx - 11), MathF.Round(f.P.Y - cy - 7), .75f, new Color(236, 234, 240, 255)); } foreach (var a in animals)
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
                case Kind.Fish: { if (a.S == 9) continue; n = "fish" + a.Var; var t0 = an[n]; Ui.Tex(t0, MathF.Round(x - 17), MathF.Round(y - 8), 1, new Color(255, 255, 255, 120), a.V.X < 0); continue; }
                default: continue;
            }
            var t = an[n]; Ui.Tex(t, MathF.Round(x - t.Width / 2f), MathF.Round(y - t.Height / 2f + oy), 1, Color.White, a.Dir < 0);
        }
    }
    public void DrawGlows(int cx, int cy, int vw, Render R)
    {
        var e = g.Env; foreach (var q in glowers) { if (q.t < 0) continue; float b = M.Smooth(0, 2.5f, q.t) * (1 - M.Smooth(q.life - 4, q.life, q.t)) * (.25f + .75f * MathF.Pow(MathF.Max(0, M.Sin(q.t * 1.9f + q.ph)), 2)); float x = q.p.X - cx, y = q.p.Y - cy; Ui.GlowAt(x, y, 13, new Color(230, 255, 120, (int)(95 * b))); Raylib.DrawRectangle((int)x, (int)y, 2, 2, new Color(240, 255, 160, (int)(235 * b))); } foreach (var a in animals)
        {
            if (a.K is not (Kind.Firefly or Kind.Moth)) continue; float x = a.P.X - cx, y = a.P.Y - cy; if (x < -20 || x > vw + 20) continue; float on = a.K == Kind.Moth ? .7f : e.Night * (1 - e.Rain); if (on < .05f) continue; float b = MathF.Pow(MathF.Max(0, M.Sin(a.T * 1.7f + a.Home.X)), 2) * on; if (b < .03f) continue; var c = a.K == Kind.Moth ? new Color(140, 245, 230, (int)(180 * b)) : new Color(230, 255, 120, (int)(200 * b)); Ui.GlowAt(x, y, 13, Raylib.Fade(c, b * .5f)); Raylib.DrawRectangle((int)x, (int)y, 2, 2, c);
        }
    }
    public void AddLights(Render R, float cx, float vw) { foreach (var f in flashes) { float k = 1 - f.t / .32f; R.Lights.Add((f.p.X, f.p.Y, 72, f.a * k * k, f.c)); } int n = 0; if (g.Env.Night > .4f) foreach (var a in animals) if (a.K == Kind.Firefly && a.P.X > cx && a.P.X < cx + vw && n++ < 5) R.Lights.Add((a.P.X, a.P.Y, 46, .5f * MathF.Pow(MathF.Max(0, M.Sin(a.T * 1.7f + a.Home.X)), 2), 0xd8f080)); }
}

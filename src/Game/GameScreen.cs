using System.Numerics;
using Raylib_cs;
namespace UnnamedGame;

public sealed partial class GameScreen
{
    public readonly World W; public readonly Render R; public readonly Env Env = new(); public readonly Life Life; public readonly Player P; public readonly Fishing Fish; readonly Tiles tiles; readonly Backdrop back; public float CamX, CamY; float lookX, lookY, shake; public float Under; readonly Rng rng = new(55); string area = "", shownArea = ""; float areaT = 9; float time; readonly AvatarPainter sap = new(); readonly Painter spt = new(130, 130); readonly Prop[] byX; float hintT; public bool Title; float titleT; float autosave = 30;
    public GameScreen()
    {
        W = new World(); R = new Render(W); tiles = new Tiles(W); back = new Backdrop(R.Hill); Life = new Life(W, this); P = new Player(W, this); Fish = new Fishing(this); byX = W.Props.OrderBy(p => p.X).ToArray(); foreach (var s in W.Sellers) { s.Tex = spt.ToTexture(); s.An.IdleStyle = s.Idx % 3 == 1 ? 1 : 0; } Env.Thunder = v => Snd.Play("thunder", v * .9f, .85f + rng.F() * .3f); BuildMap();
    }
    public void Begin(bool fresh)
    {
        var d = Store.Data; P.SetAvatar(d.Avatar); if (fresh) { P.P = W.Spawn; if (d.X < 0) { Env.Clock = .30f; Env.Day = 1; Env.SetWeather(Weather.Clear, true); } }
        P.V = default; Fish.Cancel(); panel = Panel.None; lookX = P.Dir * 34; lookY = 0; CamX = P.P.X + lookX - Ui.VW / 2; CamY = P.P.Y - 34 - Ui.VH * .66f; Env.Colors();
    }
    public void LoadClock() { Env.Clock = Store.Data.Clock; Env.Day = Math.Max(1, Store.Data.Day); Env.SetWeather(Weather.Clear, true); Env.WeatherT = 120; Env.Colors(); }
    public void Shake(float v) => shake = MathF.Max(shake, v * Store.Cfg.Shake * (Store.Cfg.ReduceMotion ? .2f : 1));
    public Vector2 ToScreen(Vector2 w) => new(w.X - CamX, w.Y - CamY);
    public bool MenuOpen => panel != Panel.None;

    public void Update(float dt)
    {
        float vw = Ui.VW, vh = Ui.VH; if (Title || panel is not (Panel.Menu or Panel.Settings)) time += dt; bool paused = panel is Panel.Menu or Panel.Settings, wasOpen = panel != Panel.None; if (Title) { UpdateTitle(dt); return; }
        Panels(dt); if (paused) { Ambience(); return; }
        bool control = panel == Panel.None && !emoteOpen && !wasOpen; Env.Update(dt); Fish.Update(dt, control); P.Frozen = Fish.Locks; P.Update(dt, control && !Fish.Locks);
        if (control && !Fish.Busy && !wasOpen) Interact(); else prompt = null;
        Camera(dt, vw, vh); Life.Update(dt, P, CamX, CamY, (int)vw, (int)vh); Sellers(dt); Emit(dt);
        // areas
        int tx = (int)(P.P.X / 24), ty = (int)((P.P.Y - 30) / 24); bool cave = W.WallAt(tx, ty) != T.Air && ty > W.Top[Math.Clamp(tx, 0, World.W - 1)] + 2; var a = World.AreaName(P.P.X / 24, cave); if (a != area) { area = a; areaHold = 0; } else areaHold += dt; if (areaHold > .8f && shownArea != area) { shownArea = area; areaT = 0; newArea = Store.Data.Areas.Add(area); Snd.Play("area", newArea ? .7f : .35f); } areaT += dt;
        Under = M.Damp(Under, cave ? 1 : 0, 2.2f, dt); Ambience(); Snd.Duck = M.Damp(Snd.Duck, Fish.S == FS.Fight ? .75f : 1, 2, dt); var sd = Store.Data; sd.Clock = Env.Clock; sd.Day = Env.Day; sd.X = P.P.X; sd.Y = P.P.Y; autosave -= dt; if (autosave < 0) { autosave = 45; Store.SaveGame(); }
    }
    float areaHold; bool newArea;
    void Camera(float dt, float vw, float vh)
    {
        lookX = M.Damp(lookX, P.Dir * 34 + P.V.X * .22f, 1.8f, dt); lookY = M.Damp(lookY, P.V.Y > 300 ? 60 : In.Down(Act.Down) && P.Crouch ? 40 : 0, 2, dt); float zx = P.P.X + lookX, zy = P.P.Y - 34 + lookY; if (Fish.S is FS.Wait or FS.Bite or FS.Fight) zx = (P.P.X + Fish.Bob.X) / 2; float gx = zx - vw / 2, gy = zy - vh * .66f; CamX = M.Damp(CamX, gx, 5, dt); CamY = M.Damp(CamY, gy, P.Ground ? 5 : 3.4f, dt); CamX = M.Clamp(CamX, 0, World.W * 24 - vw - 2); CamY = M.Clamp(CamY, 0, World.H * 24 - vh - 2); shake = M.Damp(shake, 0, 7, dt);
    }
    void Ambience()
    {
        if (Title) { Snd.Loop("birds", .4f); Snd.Loop("wind", .25f); return; }
        float tx = P.P.X / 24; var b = World.BiomeAt(tx); float outd = 1 - Under; float sea = MathF.Max(1 - M.Clamp((tx - 110) / 50), 1 - M.Clamp((1430 - tx) / 50)); Snd.Loop("waves", sea * outd * .9f); Snd.Loop("wind", (.16f + MathF.Abs(Env.Wind) * .4f + (b == Biome.Highlands ? .2f : 0)) * outd); Snd.Loop("birds", (1 - Env.Night) * (1 - Env.Rain) * outd * (1 - sea * .7f) * .6f); Snd.Loop("crickets", Env.Night * (1 - Env.Rain) * outd * .5f * (1 - sea)); Snd.Loop("rain", Env.Rain * (.25f + outd * .75f)); Snd.Loop("cave", Under * .7f);
        float fx = MathF.Abs(P.P.X - W.Falls.x * 24), fy = MathF.Abs(P.P.Y - World.LakeY) * .6f; Snd.Loop("waterfall", M.Clamp(1 - MathF.Sqrt(fx * fx + fy * fy) / 620)); float st = MathF.Abs(P.P.X - 304 * 24); Snd.Loop("stream", M.Clamp(1 - st / 420) * .7f * outd); Snd.Loop("stream", M.Clamp(1 - MathF.Abs(P.P.X - 760 * 24) / 230) * .4f); float fire = MathF.Min(MathF.Abs(P.P.X - 1218 * 24), MathF.Abs(P.P.X - 250 * 24)); Snd.Loop("fire", M.Clamp(1 - fire / 200) * .8f); if (P.Swim && P.Air > .2f) Snd.Loop("underwater", .7f);
        string m = Under > .6f ? "cave" : Env.Night > .6f ? "night" : Env.Rain > .5f ? "rainy" : b switch { Biome.Autumn => "autumn", Biome.Lake => "lake", Biome.Highlands => "highlands", Biome.SeaW or Biome.SeaE or Biome.BeachW or Biome.BeachE => "beach", _ => "village" }; if (!Snd.HasMusic(m)) m = "village"; Snd.Music(m);
    }
    void Emit(float dt)
    {
        foreach (var p in Visible(300))
        {
            if (p.Kind == 9) { if (rng.P(dt * 40)) Life.Flame(new(p.X, p.Y - 8)); if (rng.P(dt * 2)) Life.Smoke(new(p.X, p.Y - 26), 2, new Color(120, 110, 120, 60)); }
            else if (p.Kind == 12 && rng.P(dt * 1.6f)) Life.Smoke(new(p.X + (p.Flip ? -42 : 42), p.Y - (p.S == "cottage1" ? 136 : 126)), 2.4f, new Color(210, 204, 210, 70));
            else if (p.Kind == 13 && rng.P(dt * 30)) { Life.Drop(new(p.X + rng.R(-2, 2), p.Y - 60), new(rng.R(-46, 46), -rng.R(40, 90)), .55f); if (rng.P(.5f)) Life.Drop(new(p.X + rng.R(-22, 22), p.Y - 42), new(rng.R(-8, 8), 10), .42f); }
            else if (p.Kind == 1 && Env.Wind != 0 && rng.P(dt * (.05f + MathF.Abs(Env.Wind) * .14f)) && !p.S.StartsWith("pine") && !p.S.StartsWith("palm") && !p.S.StartsWith("fir")) { Col c = p.S.StartsWith("maple") ? 0xd8743a : p.S.StartsWith("birch") || p.S.StartsWith("oakg") ? 0xe0b048 : p.S.StartsWith("bloss") ? 0xf2bcc8 : 0x86b050; Life.Leaves(new(p.X, p.Y - 96), 1, c); }
        }
        float fx = W.Falls.x * 24 + 24; if (MathF.Abs(fx - P.P.X) < 700) for (int i = 0; i < 2; i++) if (rng.P(.7f)) Life.Foam(new(fx + rng.R(-30, 30), World.LakeY - 2)); if (MathF.Abs(fx - P.P.X) < 700 && rng.P(dt * 6)) R.Ripple(fx + rng.R(-30, 30), 1.2f);
        // mist of breath from a hat-chilled highland night is left to the imagination
    }
    IEnumerable<Prop> Visible(float pad) { float x0 = CamX - pad, x1 = CamX + Ui.VW + pad; int lo = 0, hi = byX.Length; while (lo < hi) { int m = (lo + hi) / 2; if (byX[m].X < x0) lo = m + 1; else hi = m; } for (int i = lo; i < byX.Length && byX[i].X < x1; i++) yield return byX[i]; }
    public static bool Grown(Prop p) { var k = p.S + (int)p.X; return !Store.Data.ForageTimes.TryGetValue(k, out float t) || Store.Data.Day + Store.Data.Clock - t > .55f; }
    void Sellers(float dt)
    {
        foreach (var s in W.Sellers)
        {
            if (MathF.Abs(s.X - P.P.X) > 700) continue; float dx = P.P.X - s.X; bool near = MathF.Abs(dx) < 110 && MathF.Abs(P.P.Y - s.Y) < 60; s.An.Look = near ? new(M.Clamp(dx / 40, -1, 1), .1f) : default; s.Talk = MathF.Max(0, s.Talk - dt); s.An.Talk = s.Talk > 0 ? 1 : 0; s.An.Wind = Env.Wind * .4f; if (near && !s.Greeted) { s.Greeted = true; s.An.Set(Anim.Wave); s.An.StateT = 0; s.Wave = 1.6f; } if (MathF.Abs(dx) > 400) s.Greeted = false; s.Wave -= dt; if (s.Wave <= 0) s.An.Set(Anim.Idle); if (s.An.Update(dt)) { spt.Clear(); sap.Draw(spt, s.D, s.An.Pose, AvatarAnim.PxPerUnit, 65, 120, outlinePx: 1); spt.Upload(s.Tex); }
        }
    }

    // ---------------------------------------------------------------- interaction
    Prop target; Seller seller; string prompt;
    void Interact()
    {
        target = null; seller = null; prompt = null; float best = 46; if (P.Swim || P.Climb) { } else
        {
            foreach (var s in W.Sellers) { float d = MathF.Abs(s.X - P.P.X); if (d < 64 && MathF.Abs(s.Y - P.P.Y) < 40) { seller = s; prompt = "Talk to " + s.Name; best = 0; } }
            if (seller == null) foreach (var p in Visible(0)) { float d = MathF.Abs(p.X - P.P.X) + MathF.Abs(p.Y - P.P.Y) * .6f; if (d > best) continue; string q = p.Kind switch { 20 => Grown(p) && !(p.S == "f_firefly" && Env.Night < .5f) ? "Pick up " + FishDex.BaitNames[p.Data] : null, 10 => P.Sit ? null : "Sit", 14 => "Aquarium", 15 => "Read notices", 9 => null, _ => null }; if (q != null) { best = d; target = p; prompt = q; } }
        }
        if (In.Press(Act.Interact) && (seller != null || target != null))
        {
            if (seller != null) OpenShop(seller);
            else switch (target.Kind)
                {
                    case 20: { var d = Store.Data; d.Baits[target.Data] = Math.Min(99, d.Baits[target.Data] + (target.Data == 1 ? 2 : 1)); d.ForageTimes[target.S + (int)target.X] = d.Day + Env.Clock; Snd.Play("forage", .8f, .95f + rng.F() * .15f); Life.Leaves(new(target.X, target.Y - 8), 6, World.GrassCol(target.X / 24)); Life.Sparkle(new(target.X, target.Y - 8), 5, new Color(255, 236, 150, 255)); Toast("+" + (target.Data == 1 ? 2 : 1) + " " + FishDex.BaitNames[target.Data]); P.An.Punch(3); if (d.Bait == 0) d.Bait = target.Data; break; }
                    case 10: P.SitOn(target); break;
                    case 14: Open(Panel.Tank); break;
                    case 15: Open(Panel.Board); break;
                }
        }
        if (In.Press(Act.Bag)) Open(Panel.Bag); if (In.Press(Act.Journal)) Open(Panel.Journal); if (In.Press(Act.Map)) Open(Panel.Map); if (In.Press(Act.Menu)) Open(Panel.Menu);
    }
    readonly List<(string s, float t)> toasts = new(); public void Toast(string s) => toasts.Add((s, 0));

    // ---------------------------------------------------------------- world drawing
    public void RenderWorld()
    {
        float vw = Ui.VW, vh = Ui.VH; R.Ensure(vw, vh); int w = R.W, h = R.H; int cx = (int)MathF.Floor(CamX), cy = (int)MathF.Floor(CamY); R.Lights.Clear(); float dark = MathF.Max(MathF.Max(Env.Night, Under), M.Clamp(Env.Storm * .6f)); float lampA = .12f + .95f * M.Smooth(.1f, .8f, MathF.Max(dark, Env.Golden * .42f));
        foreach (var l in W.Lamps.Where(l => l.x > cx - l.r && l.x < cx + w + l.r && l.y > cy - l.r && l.y < cy + h + l.r).OrderBy(l => MathF.Abs(l.x - cx - w / 2f)).Take(24)) { float fl = 1 + l.flick * (M.Sin(time * 7 + l.x) * .6f + M.Sin(time * 17.3f + l.y) * .4f); bool cold = l.c.B > l.c.R; R.Lights.Add((l.x, l.y, l.r * (.9f + .1f * fl), (cold ? MathF.Max(.75f, lampA) * 1.15f : lampA * 1.0f) * fl, l.c)); }
        Life.AddLights(R, cx, w); if (Under > .3f) R.Lights.Add((P.P.X, P.P.Y - 36, 190, .5f * Under, 0xffd8a8)); if (Env.Night > .5f) R.Lights.Add((P.P.X, P.P.Y - 40, 90, .22f * Env.Night, 0xffd0a0)); if (R.Lights.Count > 28) R.Lights.RemoveRange(28, R.Lights.Count - 28);
        float tx = (cx + w / 2f) / 24; float groundY = W.Top[Math.Clamp((int)tx, 0, World.W - 1)] * 24;
        R.Frame(Env, W, CamX, CamY, () => back.Draw(Env, cx, cy, w, h, groundY, tx), () => Albedo(cx, cy, w, h), c => Shades(cx, cy, w), () => Glows(cx, cy, w, h, lampA), () => { Life.DrawPts(cx, cy, false); Fish.DrawWorld(cx, cy); }, Under);
    }
    public void DrawWorld()
    {
        float sx = shake > .05f ? M.Sin(time * 61) * shake : 0, sy = shake > .05f ? M.Sin(time * 53 + 2) * shake : 0; R.Present(CamX, CamY, sx, sy);
    }
    void Albedo(int cx, int cy, int w, int h)
    {
        foreach (var p in Visible(170)) if (p.Layer == 0) DrawProp(p, cx, cy);
        // climbables sit against the back wall
        int x0 = Math.Max(0, cx / 24 - 1), x1 = Math.Min(World.W - 1, (cx + w) / 24 + 1), y0 = Math.Max(0, cy / 24 - 1), y1 = Math.Min(World.H - 1, (cy + h) / 24 + 1);
        tiles.Draw(cx, cy, w, h);
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                int i = y * World.W + x; if (W.Climb[i] == 1) Ladder(x * 24 - cx, y * 24 - cy); else if (W.Climb[i] == 2) VineDraw(x, y, cx, cy);
                if (W.Plat[i] && !W.Solid(x, y)) Plank(x, y, x * 24 - cx, y * 24 - cy);
            }
        foreach (var p in Visible(170)) if (p.Layer == 1) DrawProp(p, cx, cy);
        Life.DrawAnimals(cx, cy, w, true); Fish.DrawUnder(cx, cy); foreach (var s in W.Sellers) { float x = s.X - cx; if (x > -90 && x < w + 90) { Shadow(x, s.Y - cy, 13); Ui.Tex(s.Tex, MathF.Round(x - 65), MathF.Round(s.Y - cy - 120 + 1), 1, Color.White); } }
        foreach (var p in Visible(170)) if (p.Layer == 2 && p.Kind != 20) DrawProp(p, cx, cy);
        foreach (var p in Visible(20)) if (p.Kind == 20 && Grown(p) && !(p.S == "f_firefly")) DrawProp(p, cx, cy);
        Life.DrawGrass(cx, cy, w, h, false); Life.DrawAnimals(cx, cy, w, false); if (!Title) { if (P.Ground) Shadow(P.P.X - cx, P.P.Y + P.StepY - cy, 13); P.Draw(cx, cy); } Life.DrawGrass(cx, cy, w, h, true); foreach (var p in Visible(170)) if (p.Layer == 3) DrawProp(p, cx, cy);
    }
    static void Shadow(float x, float y, float r) { Raylib.DrawEllipse((int)x, (int)y + 1, r, 2.4f, new Color(30, 18, 34, 80)); }
    void Ladder(int x, int y) { var d = new Color(62, 38, 32, 255); var c = new Color(156, 106, 66, 255); var l = new Color(190, 138, 90, 255); foreach (int ox in new[] { 4, 17 }) { Raylib.DrawRectangle(x + ox - 1, y, 5, 24, d); Raylib.DrawRectangle(x + ox, y, 3, 24, c); Raylib.DrawRectangle(x + ox, y, 1, 24, l); } for (int k = 0; k < 3; k++) { Raylib.DrawRectangle(x + 5, y + 2 + k * 8, 14, 4, d); Raylib.DrawRectangle(x + 6, y + 3 + k * 8, 12, 2, c); } }
    void VineDraw(int tx, int ty, int cx, int cy) { int x = tx * 24 - cx, y = ty * 24 - cy; float s = M.Sin(time * 1.4f + ty * .5f) * 1.5f + Env.Wind; for (int k = 0; k < 3; k++) { int ox = 6 + k * 5 + (int)(s * (k - 1) * .4f + s * .5f); Raylib.DrawRectangle(x + ox, y, 2, 24, new Color(72, 104, 62, 255)); for (int q = 0; q < 3; q++) { int yy = y + q * 8 + k * 3; Raylib.DrawRectangle(x + ox - 3 + (q + k) % 2 * 4, yy, 4, 3, new Color(122, 168, 84, 255)); Raylib.DrawRectangle(x + ox - 3 + (q + k) % 2 * 4, yy + 2, 4, 1, new Color(74, 110, 60, 255)); } } }
    void Plank(int tx, int ty, int x, int y)
    {
        bool l = W.PlatAt(tx - 1, ty), r = W.PlatAt(tx + 1, ty); Raylib.DrawRectangle(x, y, 24, 9, new Color(58, 34, 30, 255)); Col c = Col.Lerp(0xb07a4a, 0x9a6640, M.Hash(tx, ty, 3)); Raylib.DrawRectangle(x + (l ? 0 : 1), y + 1, 24 - (l ? 1 : 2), 6, c.Rl()); Raylib.DrawRectangle(x + (l ? 0 : 1), y + 1, 24 - (l ? 1 : 2), 1, c.Shade(.35f).Rl()); Raylib.DrawRectangle(x + 12, y + 1, 1, 6, c.Shade(-.5f).Rl()); Raylib.DrawRectangle(x + 3, y + 3, 1, 1, c.Shade(-.6f).Rl()); Raylib.DrawRectangle(x + 20, y + 3, 1, 1, c.Shade(-.6f).Rl());
        if (tx % 4 == 0 && W.PoolAt(tx * 24 + 12, (ty + 2) * 24 + 20) != null) { int hgt = 0; while (hgt < 14 && !W.Solid(tx, ty + 1 + hgt)) hgt++; Raylib.DrawRectangle(x + 8, y + 8, 8, hgt * 24 + 16, new Color(58, 34, 30, 255)); Raylib.DrawRectangle(x + 9, y + 8, 6, hgt * 24 + 16, new Color(122, 82, 58, 255)); Raylib.DrawRectangle(x + 9, y + 8, 2, hgt * 24 + 16, new Color(150, 104, 72, 255)); }
    }
    void DrawProp(Prop p, int cx, int cy)
    {
        var s = Sprites.Get(p.S); float bx = MathF.Round(p.X - cx), by = MathF.Round(p.Y - cy); if (by < -190 || by > Ui.VH + 260) return; float wind = Env.Wind; float q = MathF.Floor(time * 12) / 12;
        if (p.Kind == 4) { var pl = W.PoolAt(p.X, p.Y + 6); if (pl != null) by += MathF.Round(M.Sin(p.X * .045f + Env.T * 1.5f) * .8f + M.Sin(time * 1.1f + p.Phase) * .8f); }
        if (p.Kind is 1 or 3 && p.Layer < 3) Shadow(bx, by - 1, p.Kind == 1 ? 16 : 12);
        float push = 0; if (p.Kind is 2 or 3 && !Title) { float d = p.X - P.P.X; if (MathF.Abs(d) < 22 && MathF.Abs(p.Y - P.P.Y) < 40) { push = MathF.Sign(d) * (1 - MathF.Abs(d) / 22) * 5 + P.V.X * .02f; if (p.Kind == 3 && MathF.Abs(P.V.X) > 30 && rng.P(Ui.DT * 5)) { Snd.Play("rustle", .25f, .9f + rng.F() * .3f); Life.Leaves(new(p.X, p.Y - 12), 1, World.GrassCol(p.X / 24)); } } p.Bend = M.Damp(p.Bend, push, 9, Ui.DT); push = p.Bend; }
        foreach (var part in s.Parts)
        {
            float ox = p.Flip ? -part.OX - part.T.Width : part.OX; float x = bx + ox, y = by + part.OY; if (part.Sway == 0 && push == 0) { Ui.Tex(part.T, x, y, 1, Color.White, p.Flip); continue; }
            float sw = (M.Sin(q * (1.1f + MathF.Abs(wind) * .7f) + p.Phase + part.Phase + p.X * .01f) * (.35f + MathF.Abs(wind) * .55f) + wind * .7f) * MathF.Abs(part.Sway);
            if (p.Kind is 1 or 3) { float wy = M.Cos(q * 1.3f + part.Phase * 2 + p.Phase) * MathF.Abs(wind) * .5f; if (part.Pivot < 0) Life.Shear(part.T, x, y, MathF.Round(sw * 1.4f), Color.White, p.Flip, true); else Ui.Tex(part.T, x + MathF.Round(sw + push), y + MathF.Round(wy), 1, Color.White, p.Flip); }
            else if (part.Sway < 0) Life.Shear(part.T, x, y, MathF.Round(sw * 1.2f), Color.White, p.Flip, true);
            else { bool water = p.S.StartsWith("kelp") || p.S.StartsWith("weed") || p.S.StartsWith("glowk") || p.S.StartsWith("lily"); float a = water ? M.Sin(time * 1.2f + p.Phase) * part.Sway * .8f : sw; Life.Shear(part.T, x, y, MathF.Round((a + push) * 2) / 2, Color.White, p.Flip); }
        }
        if (p.Kind == 11) { var m = Sprites.Get("mill").Parts[0]; Raylib.DrawTexturePro(m.T, new Rectangle(0, 0, m.T.Width, m.T.Height), new Rectangle(bx, by - 124, m.T.Width, m.T.Height), new(m.T.Width / 2f, m.T.Height / 2f), MathF.Floor(time * 12) / 12 * (14 + wind * 26) % 360, Color.White); }
        if (p.Kind == 14) TankFish(bx, by);
    }
    readonly Dictionary<int, Texture2D> mini = new();
    public Texture2D MiniFish(int id, float sc = .3f) { if (!mini.TryGetValue(id, out var t)) { var p = new Painter(48, 30); FishDex.Draw(p, FishDex.All[id], .34f, 23, 15); p.Outline(0x2a1a1c, 1); mini[id] = t = p.ToTexture(); } return t; }
    void TankFish(float bx, float by) { var l = Store.Data.Tank; for (int i = 0; i < l.Count && i < 8; i++) { float sp = .25f + M.Hash(i, 3) * .3f, ph = time * sp + i * 1.7f; float x = M.Sin(ph) * 26, y = -44 + M.Hash(i, 5) * 22 + M.Sin(time * .8f + i) * 2; bool fl = M.Cos(ph) < 0; var t = MiniFish(l[i].Id); Raylib.DrawTexturePro(t, new Rectangle(0, 0, fl ? -t.Width : t.Width, t.Height), new Rectangle(MathF.Round(bx + x - 12), MathF.Round(by + y - 7), 24, 15), Vector2.Zero, 0, new Color(220, 240, 255, 235)); } if (rng.P(Ui.DT * 2)) Life.Bubble(new(bx + CamX - 20 + rng.R(0, 40), by + CamY - 16)); }
    Texture2D shade;
    void Shades(int cx, int cy, int w)
    {
        // soft pools of shade under canopies; drawn multiplied into the light map
        if (shade.Id == 0) { var p = new Painter(64, 64); for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = MathF.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32; float v = M.Clamp(1 - d); v = 1 - v * v * .62f; p.Px[y * 64 + x] = new Color((int)(v * 255), (int)(M.Lerp(v, 1, .05f) * 255), (int)(M.Lerp(v, 1, .16f) * 255), 255); } shade = p.ToTexture(); Raylib.SetTextureFilter(shade, TextureFilter.Bilinear); }
        float sunX = -Env.SunDir.X * 26; foreach (var p in Visible(140)) if (p.Kind == 1) { float sc = Sprites.Get(p.S).H / 110; float x = p.X - cx + sunX, y = p.Y - cy; Raylib.DrawTexturePro(shade, new Rectangle(0, 0, 64, 64), new Rectangle(x - 58 * sc, y - 46, 116 * sc, 84), Vector2.Zero, 0, Color.White); }
    }
    void Glows(int cx, int cy, int w, int h, float lampA)
    {
        foreach (var l in R.Lights) { float x = l.x - cx, y = l.y - cy; if (l.r < 130 && l.c.B > l.c.R) { Ui.GlowAt(x, y, 24, l.c.Rl(.2f * l.i)); continue; } if (l.r == 190 || l.r == 120 || l.r == 90) continue; if (l.r < 80) continue; float b = M.Clamp(lampA); if (l.c.R > .9f) { Ui.GlowAt(x, y, l.r * .34f, l.c.Rl(.18f * b)); Ui.GlowAt(x, y, 11, l.c.Rl(.4f * b)); } }
        Life.DrawPts(cx, cy, true); Life.DrawGlows(cx, cy, w, R);
        foreach (var p in Visible(20)) if (p.Kind == 20 && Grown(p)) { if (p.S == "f_firefly") { if (Env.Night > .5f) { float b = .5f + .5f * M.Sin(time * 3 + p.Phase); float x = p.X - cx + M.Sin(time + p.Phase) * 10, y = p.Y - cy - 16 + M.Cos(time * 1.4f + p.Phase) * 6; Ui.GlowAt(x, y, 16, new Color(230, 255, 110, (int)(130 * b))); Raylib.DrawRectangle((int)x, (int)y, 2, 2, new Color(250, 255, 190, 255)); } continue; } float g = MathF.Max(0, M.Sin(time * 1.6f + p.Phase * 3)); if (g > .8f) { float x = p.X - cx - 4, y = p.Y - cy - 9; Raylib.DrawRectangle((int)x - 2, (int)y, 5, 1, new Color(255, 250, 220, 230)); Raylib.DrawRectangle((int)x, (int)y - 2, 1, 5, new Color(255, 250, 220, 230)); } }
        foreach (var p in Visible(60)) if (p.Kind == 9) { float f = .8f + .2f * M.Sin(time * 13) * M.Sin(time * 7.7f); Ui.GlowAt(p.X - cx, p.Y - cy - 12, 60 * f, new Color(255, 140, 60, 70)); Ui.GlowAt(p.X - cx, p.Y - cy - 9, 18, new Color(255, 200, 110, 150)); }
    }
}

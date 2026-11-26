using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public sealed partial class GameScreen
{
    public World W; public readonly Render R; public readonly Env Env = new(); public Life Life; public readonly Player P; Tiles tiles; readonly Backdrop back; public float CamX, CamY; float lookX, lookY, shake; public float Under; readonly Rng rng = new(Environment.TickCount); string area = "", shownArea = ""; float areaT = 9, areaHold; bool newArea; float time, playT; Prop[] byX; Lamp[] lampX; public bool Title; float titleT; float autosave = 30; bool playing;
    public GameScreen(World w)
    {
        World.Cur = w; W = w; R = new Render(w); back = new Backdrop(R.Hill); P = new Player(w, this); Env.Thunder = v => { Snd.Play("thunder", v * .9f, .85f + rng.F() * .3f); Rumble.Kick(.5f * v, .08f, .7f); }; SetWorld(w);
    }
    public void SetWorld(World w)
    {
        ClearDig(); World.Cur = w; W = w; R.SetWorld(w); tiles?.Unload(); tiles = new Tiles(w); P.SetWorld(w); Life = new Life(w, this); byX = w.Props.OrderBy(p => p.X).ToArray(); lampX = w.Lamps.OrderBy(l => l.X).ToArray(); mapReady = false; warm = 0;
        w.TileChanged = (x, y) => tiles.Refresh(x, y); w.LightChanged = (x0, y0, x1, y1) => R.UpdateTiles(x0, y0, x1, y1); w.Uprooted = Uproot; w.VineGone = (x, y) => { Life.Leaves(new(x * 24 + 12, y * 24 + 12), 3, 0x7aa854); };
    }
    /// Enter the world as the character that owns the current save.
    public void Begin()
    {
        var d = Store.Data; P.SetAvatar(d.Avatar); var at = new Vector2(d.X, d.Y); P.P = d.X > 0 && W.FitsPlayer(at) ? at : W.SafeSpawn(); P.V = default; P.Climb = false; P.EmoteT = 0; P.Dir = 1; panel = Panel.None; held = default; heldFrom = -1; toasts.Clear(); gains.Clear(); admin = false; hideHud = false; playT = 0; area = shownArea = ""; areaT = 9; areaHold = 0; Under = 0; autosave = 30; playing = true; barSel = 0; barHand = -9; nameT = 9; BuildMap();
        foreach (var q in d.Drops) loot.Add(new Loot { Id = q.Id, N = q.N, P = new(q.X, q.Y), T = 2, HopT = rng.R(1, 4) });
        Env.Clock = d.Clock; Env.Day = d.Day; Env.SkipTo = -1; Env.TimeScale = 1; Env.WindForce = -1; Env.SetWeather(Weather.Clear, true); Env.WeatherT = 150; Env.SnowCover = 0; Env.Wet = 0; Env.Rainbow = 0; Env.RainbowGoal = 0; Env.ForceAurora = false; Env.EclipseT = 0; Env.MeteorT = 0; Env.Meteors.Clear(); lookX = P.Dir * 34; lookY = 0; CamX = P.P.X + lookX - Ui.VW / 2; CamY = P.P.Y - 34 - Ui.VH * .66f; Local(P.P.X / 24, 99); Env.Colors(); back.Get(W.BiomeAt(P.P.X / 24), true);
    }
    /// Copy what has happened in the world into the save.
    public void Flush()
    {
        if (!playing) return; var d = Store.Data; d.Edits = W.SaveEdits(); d.Gone = W.GoneProps.ToList(); d.Gen = World.GenVer; d.X = P.P.X; d.Y = P.P.Y; d.Drops = loot.Take(400).Select(l => new DropData { Id = l.Id, N = l.N, X = l.P.X, Y = l.P.Y }).ToList(); if (!held.Empty) d.Drops.Add(new DropData { Id = held.Id, N = held.N, X = P.P.X, Y = P.P.Y - 20 });
    }
    public void Leave() { playing = false; ClearDig(); }
    public void Shake(float v) => shake = MathF.Max(shake, v * Store.Cfg.Shake * (Store.Cfg.ReduceMotion ? .2f : 1));
    public Vector2 ToScreen(Vector2 w) => new(w.X - CamX, w.Y - CamY);
    public bool MenuOpen => panel != Panel.None;

    public void Update(float dt)
    {
        float vw = Ui.VW, vh = Ui.VH; if (Title || panel is not (Panel.Menu or Panel.Settings)) time += dt; bool paused = panel is Panel.Menu or Panel.Settings, wasOpen = panel != Panel.None; if (Title) { UpdateTitle(dt); return; }
        Panels(dt); if (paused) { P.Mining = false; Ambience(); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) { admin = !admin; Snd.Play(admin ? "open" : "close"); }
        bool control = panel == Panel.None && !emoteOpen && !wasOpen && !Program.Busy; Ui.InGame = panel == Panel.None && !Program.Busy; AdminTick(dt); Local(P.P.X / 24, dt); Env.Update(dt); BarUpdate(dt, control); Digging(dt, control); P.Update(dt, control); Falling(dt); W.Tick(dt, P.P.X);
        if (control) { if (In.Press(Act.Bag)) Open(Panel.Bag); if (In.Press(Act.Map)) Open(Panel.Map); if (In.Press(Act.Menu)) Open(Panel.Menu); }
        Camera(dt, vw, vh); Life.Update(dt, P, CamX, CamY, (int)vw, (int)vh); Emit(dt); Warm();
        // areas
        int tx = (int)(P.P.X / 24), ty = (int)((P.P.Y - 30) / 24); bool cave = W.WallAt(tx, ty) != T.Air && ty > W.Top0[Math.Clamp(tx, 0, World.W - 1)] + 3; string a = !cave && tx < 92 ? "The West Sea" : !cave && tx > World.W - 92 ? "The East Sea" : Biomes.Of(W.Here(tx, ty, cave)).Name; if (a != area) { area = a; areaHold = 0; } else areaHold += dt; if (areaHold > .8f && shownArea != area) { shownArea = area; areaT = 0; newArea = Store.Data.Areas.Add(area); Snd.Play("area", newArea ? .7f : .3f); } areaT += dt;
        Under = M.Damp(Under, cave ? 1 : 0, 2.2f, dt); Ambience(); playT += dt; var sd = Store.Data; sd.Clock = Env.Clock; sd.Day = Env.Day; sd.Seconds += dt; autosave -= dt; if (autosave < 0) { autosave = 45; Store.SaveGame(); }
    }
    /// The sky, mist and weather lean towards the biome you are in.
    void Local(float tx, float dt)
    {
        var (ba, bb, t) = W.Mix(tx); var a = Biomes.Of(ba); var b = Biomes.Of(bb); float k = 1 - MathF.Exp(-1.6f * dt); Env.LTop = Col.Lerp(Env.LTop, Col.Lerp(a.SkyTop, b.SkyTop, t), k); Env.LHor = Col.Lerp(Env.LHor, Col.Lerp(a.SkyHor, b.SkyHor, t), k); Env.LSun = Col.Lerp(Env.LSun, Col.Lerp(a.Sun, b.Sun, t), k); Env.LAmb = Col.Lerp(Env.LAmb, Col.Lerp(a.Amb, b.Amb, t), k); Env.LMist = M.Lerp(Env.LMist, M.Lerp(a.Mist, b.Mist, t), k); Env.Cold = M.Lerp(Env.Cold, M.Lerp(a.Cold, b.Cold, t), k); Env.Dry = M.Lerp(Env.Dry, M.Lerp(a.Dry, b.Dry, t), k);
    }
    int warm;
    /// Paint the sprites just outside the view a few at a time, so walking into new country never stutters.
    void Warm() { int n = 0; foreach (var p in Visible(900)) { if (!Sprites.Has(p.S)) { Sprites.Get(p.S); if (++n >= 1) return; } } }
    void Camera(float dt, float vw, float vh)
    {
        lookX = M.Damp(lookX, P.Mining ? P.MineAim.X * 20 : P.Dir * 34 + P.V.X * .22f, 1.8f, dt); lookY = M.Damp(lookY, P.V.Y > 300 ? 60 : In.Down(Act.Down) && P.Crouch ? 40 : 0, 2, dt); float zx = P.P.X + lookX, zy = P.P.Y - 34 + lookY; float gx = zx - vw / 2, gy = zy - vh * M.Lerp(.66f, .56f, Under); CamX = M.Damp(CamX, gx, 5, dt); CamY = M.Damp(CamY, gy, P.Ground ? 5 : 3.4f, dt); CamX = M.Clamp(CamX, 0, World.W * 24 - vw - 2); CamY = M.Clamp(CamY, 0, World.H * 24 - vh - 2); shake = M.Damp(shake, 0, 7, dt);
    }
    void Ambience()
    {
        if (Title) { Snd.Loop("birds", .4f); Snd.Loop("wind", .25f); return; }
        float tx = P.P.X / 24; var b = W.BiomeAt(tx); float outd = 1 - Under; float sea = MathF.Max(1 - M.Clamp((tx - 140) / 50), 1 - M.Clamp((World.W - 140 - tx) / 50)); bool lively = b is not (Biome.Cinder or Biome.Dunes or Biome.Mesa or Biome.Crystal or Biome.Frost); Snd.Loop("waves", sea * outd * .9f); Snd.Loop("wind", (.16f + MathF.Abs(Env.Wind) * .4f + (b is Biome.Highlands or Biome.Frost or Biome.Dunes or Biome.Mesa ? .26f : 0)) * outd); if (lively) Snd.Loop("birds", (1 - Env.Night) * (1 - Env.Rain) * outd * (1 - sea * .7f) * (b == Biome.Jungle ? .9f : .6f)); if (lively) Snd.Loop("crickets", MathF.Max(Env.Night, b is Biome.Moonpetal or Biome.Marsh ? .5f : 0) * (1 - Env.Rain) * outd * .5f * (1 - sea)); Snd.Loop("rain", Env.Rain * (.25f + outd * .75f)); Snd.Loop("cave", Under * .7f);
        foreach (var f in W.Falls) { float fx = MathF.Abs(P.P.X - f.x * 24), fy = MathF.Abs(P.P.Y - f.y1 * 24) * .6f; if (fx < 700) Snd.Loop("waterfall", M.Clamp(1 - MathF.Sqrt(fx * fx + fy * fy) / 620)); } if (P.Swim && P.Air > .2f) Snd.Loop("underwater", .7f); if (Under > .5f && W.Here((int)tx, (int)(P.P.Y / 24) - 1, true) == Biome.EmberDeep) Snd.Loop("fire", .35f); if (b == Biome.Cinder) Snd.Loop("fire", .18f * outd);
        string m = Under > .6f ? "cave" : Env.Night > .6f ? "night" : Env.Rain > .5f ? "rainy" : Biomes.Of(b).Music; if (!Snd.HasMusic(m)) m = "night"; Snd.Music(m);
    }
    void Emit(float dt)
    {
        foreach (var p in Visible(300))
        {
            if (p.Kind == 1 && Env.Wind != 0 && rng.P(dt * (.05f + MathF.Abs(Env.Wind) * .14f)) && !(p.S.StartsWith("pine") || p.S.StartsWith("palm") || p.S.StartsWith("fir") || p.S.StartsWith("snow") || p.S.StartsWith("cactus") || p.S.StartsWith("spire") || p.S.StartsWith("toad") || p.S.StartsWith("cypress"))) { if (p.S.StartsWith("charred")) { Life.Flame(new(p.X + rng.R(-8, 8), p.Y - rng.R(20, 60))); continue; } Col c = p.S.StartsWith("maple") ? 0xd8743a : p.S.StartsWith("birch") || p.S.StartsWith("oakg") ? 0xe0b048 : p.S.StartsWith("bloss") || p.S.StartsWith("cherry") ? 0xf2bcc8 : p.S.StartsWith("moon") ? 0x7ad0e0 : World.GrassCol(p.X / 24); Life.Leaves(new(p.X, p.Y - 96), 1, c); }
            else if (p.Lamp != null && p.S.StartsWith("ember") && rng.P(dt * .8f)) Life.Flame(new(p.X, p.Y - 14));
        }
        foreach (var pl in W.Pools) if (pl.Kind == Water.Spring && pl.X1 * 24 > CamX - 40 && pl.X0 * 24 < CamX + Ui.VW + 40 && pl.Surf > CamY - 60 && pl.Surf < CamY + Ui.VH + 60 && rng.P(dt * 5)) { float sx = rng.R(pl.X0, pl.X1 + 1) * 24; if (W.PoolAt(sx, pl.Surf + 4) == pl) Life.Smoke(new(sx, pl.Surf - 4), 2.4f, new Color(240, 236, 240, 60)); }
        foreach (var f in W.Falls) { float fx = f.x * 24 + 44; if (MathF.Abs(fx - CamX - Ui.VW / 2) > 700) continue; if (rng.P(dt * 14)) Life.Drop(new(f.x * 24 + 24 + rng.R(0, 10), f.y0 * 24 + 8), new(rng.R(14, 46), rng.R(-12, 10)), .6f); for (int i = 0; i < 2; i++) if (rng.P(.7f)) Life.Foam(new(fx + rng.R(-28, 28), f.y1 * 24 + 4)); if (rng.P(dt * 6)) R.Ripple(fx + rng.R(-30, 30), 1.2f); }
    }
    IEnumerable<Prop> Visible(float pad) { float x0 = CamX - pad, x1 = CamX + Ui.VW + pad; int lo = 0, hi = byX.Length; while (lo < hi) { int m = (lo + hi) / 2; if (byX[m].X < x0) lo = m + 1; else hi = m; } float y0 = CamY - 60 - pad * .4f, y1 = CamY + Ui.VH + 240; for (int i = lo; i < byX.Length && byX[i].X < x1; i++) { var p = byX[i]; if ((!p.Gone || (p.Fall >= 0 && p.Fall < 1.15f)) && p.Y > y0 && p.Y < y1) yield return p; } }

    // ---------------------------------------------------------------- world drawing
    int lampN;
    public void RenderWorld()
    {
        float vw = Ui.VW, vh = Ui.VH; R.Ensure(vw, vh); int w = R.W, h = R.H; int cx = (int)MathF.Floor(CamX), cy = (int)MathF.Floor(CamY); R.Lights.Clear(); float dark = MathF.Max(MathF.Max(Env.Night, Under), M.Clamp(Env.Storm * .6f)); dark = MathF.Max(dark, W.BiomeAt((cx + w / 2f) / 24) == Biome.Moonpetal ? .62f : 0); float lampA = .12f + .95f * M.Smooth(.1f, .8f, MathF.Max(dark, Env.Golden * .42f));
        { int lo = 0, hi = lampX.Length; while (lo < hi) { int m = (lo + hi) / 2; if (lampX[m].X < cx - 150) lo = m + 1; else hi = m; } var near = new List<Lamp>(); for (int i = lo; i < lampX.Length && lampX[i].X < cx + w + 150; i++) { var l = lampX[i]; if (!l.Off && l.Y > cy - l.R && l.Y < cy + h + l.R) near.Add(l); } if (near.Count > 19) { near.Sort((a, b) => MathF.Abs(a.X - cx - w / 2f).CompareTo(MathF.Abs(b.X - cx - w / 2f))); near.RemoveRange(19, near.Count - 19); } foreach (var l in near) { float fl = 1 + l.Flick * (M.Sin(time * 7 + l.X) * .6f + M.Sin(time * 17.3f + l.Y) * .4f); bool cold = l.C.B > l.C.R; R.Lights.Add((l.X, l.Y, l.R * (.9f + .1f * fl), (cold ? MathF.Max(.4f, lampA) * 1.15f : lampA) * fl, l.C)); } }
        lampN = R.Lights.Count; GlowTiles(cx, cy, w, h, lampA); Life.AddLights(R, cx, w); if (Under > .3f) R.Lights.Add((P.P.X, P.P.Y - 36, 190, .5f * Under, 0xffd8a8)); if (Env.Night > .5f) R.Lights.Add((P.P.X, P.P.Y - 40, 90, .22f * Env.Night, 0xffd0a0)); if (R.Lights.Count > 28) R.Lights.RemoveRange(28, R.Lights.Count - 28); lampN = Math.Min(lampN, R.Lights.Count);
        float tx = (cx + w / 2f) / 24; float groundY = W.Top0[Math.Clamp((int)tx, 0, World.W - 1)] * 24; R.Frame(Env, W, CamX, CamY, () => back.Draw(Env, W, cx, cy, w, h, groundY, tx), () => Albedo(cx, cy, w, h), c => Shades(cx, cy, w), () => Glows(cx, cy, w, h, lampA), () => { Life.DrawPts(cx, cy, false); if (!Title) DrawAim(cx, cy); }, Under);
    }
    /// Shining blocks light their surroundings, whether the world grew them or you placed them.
    void GlowTiles(int cx, int cy, int w, int h, float lampA)
    {
        int n = 0, x0 = Math.Max(1, cx / 24 - 2), x1 = Math.Min(World.W - 2, (cx + w) / 24 + 2), y0 = Math.Max(1, cy / 24 - 2), y1 = Math.Min(World.H - 2, (cy + h) / 24 + 2); var taken = new List<Vector2>();
        for (int y = y0; y <= y1 && n < 6; y++) for (int x = x0; x <= x1 && n < 6; x++) { var t = W.Tile[y * World.W + x]; if (t is not (T.Glowrock or T.Ember or T.Geode or T.Crystal)) continue; if (!Exposed(x, y)) continue; if (t is T.Crystal or T.Geode && M.Hash(x, y, 3) > .3f) continue; var c = new Vector2(x * 24 + 12, y * 24 + 12); bool near = false; foreach (var q in taken) near |= Vector2.DistanceSquared(q, c) < 110 * 110; if (near) continue; taken.Add(c); n++; var d = Blocks.Of(t); float fl = t == T.Ember ? 1 + .14f * M.Sin(time * 3 + x) : 1; R.Lights.Add((c.X, c.Y, d.GlowR, MathF.Max(.35f, lampA) * .9f * fl, d.Glow)); }
    }
    public void DrawWorld()
    {
        float sx = shake > .05f ? M.Sin(time * 61) * shake : 0, sy = shake > .05f ? M.Sin(time * 53 + 2) * shake : 0; R.Present(CamX, CamY, sx, sy);
    }
    void Albedo(int cx, int cy, int w, int h)
    {
        foreach (var p in Visible(170)) if (p.Layer == 0) DrawProp(p, cx, cy);
        int x0 = Math.Max(0, cx / 24 - 1), x1 = Math.Min(World.W - 1, (cx + w) / 24 + 1), y0 = Math.Max(0, cy / 24 - 1), y1 = Math.Min(World.H - 1, (cy + h) / 24 + 1);
        tiles.Draw(cx, cy, w, h); if (!Title) DrawDigs(cx, cy); for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (W.Climb[y * World.W + x] == 2) VineDraw(x, y, cx, cy);
        foreach (var p in Visible(170)) if (p.Layer == 1) DrawProp(p, cx, cy);
        Life.DrawAnimals(cx, cy, w, true); foreach (var p in Visible(170)) if (p.Layer == 2) DrawProp(p, cx, cy);
        Life.DrawGrass(cx, cy, w, h, false); Life.DrawAnimals(cx, cy, w, false); if (!Title) { DrawLoot(cx, cy); if (P.Ground) Shadow(P.P.X - cx, P.P.Y + P.StepY - cy, 13); P.Draw(cx, cy); DrawHeld(cx, cy); } Life.DrawGrass(cx, cy, w, h, true); foreach (var p in Visible(170)) if (p.Layer == 3) DrawProp(p, cx, cy);
    }
    static void Shadow(float x, float y, float r) { Raylib.DrawEllipse((int)x, (int)y + 1, r, 2.4f, new Color(30, 18, 34, 80)); }
    void VineDraw(int tx, int ty, int cx, int cy) { int x = tx * 24 - cx, y = ty * 24 - cy; float s = M.Sin(time * 1.4f + ty * .5f) * 1.5f + Env.Wind; for (int k = 0; k < 3; k++) { int ox = 6 + k * 5 + (int)(s * (k - 1) * .4f + s * .5f); Raylib.DrawRectangle(x + ox, y, 2, 24, new Color(72, 104, 62, 255)); for (int q = 0; q < 3; q++) { int yy = y + q * 8 + k * 3; Raylib.DrawRectangle(x + ox - 3 + (q + k) % 2 * 4, yy, 4, 3, new Color(122, 168, 84, 255)); Raylib.DrawRectangle(x + ox - 3 + (q + k) % 2 * 4, yy + 2, 4, 1, new Color(74, 110, 60, 255)); } } }
    void DrawProp(Prop p, int cx, int cy)
    {
        var s = Sprites.Get(p.S); float bx = MathF.Round(p.X - cx), by = MathF.Round(p.Y - cy); if (by < -60 || by > Ui.VH + 260) return; float wind = Env.Wind; float q = MathF.Floor(Env.SwayT * 12) / 12;
        if (p.Fall >= 0) { float f = MathF.Pow(M.Clamp(p.Fall / 1.15f), 2.4f); Ui.PushScale(bx, by - 2, 1, 1, p.FallDir * (f * 86 + M.Sin(p.Fall * 30) * (1 - f) * 1.2f)); foreach (var part in s.Parts) Ui.Tex(part.T, bx + (p.Flip ? -part.OX - part.T.Width : part.OX), by + part.OY, 1, Color.White, p.Flip); Ui.PopScale(); return; }
        if (p.Kind == 4) { var pl = W.PoolAt(p.X, p.Y + 6); if (pl == null) return; by += MathF.Round(M.Sin(p.X * .045f + Env.T * 1.5f) * .8f + M.Sin(time * 1.1f + p.Phase) * .8f); }
        if (p.Kind is 1 or 3 && p.Layer < 3) Shadow(bx, by - 1, p.Kind == 1 ? 16 : 12);
        float push = 0; if (p.Kind is 2 or 3 && !Title) { float d = p.X - P.P.X; if (MathF.Abs(d) < 22 && MathF.Abs(p.Y - P.P.Y) < 40) { push = MathF.Sign(d) * (1 - MathF.Abs(d) / 22) * 5 + P.V.X * .02f; if (p.Kind == 3 && MathF.Abs(P.V.X) > 30 && rng.P(Ui.DT * 5)) { Snd.Play("rustle", .25f, .9f + rng.F() * .3f); Life.Leaves(new(p.X, p.Y - 12), 1, World.GrassCol(p.X / 24)); } } p.Bend = M.Damp(p.Bend, push, 9, Ui.DT); push = p.Bend; }
        foreach (var part in s.Parts)
        {
            float ox = p.Flip ? -part.OX - part.T.Width : part.OX; float x = bx + ox, y = by + part.OY; if (part.Sway == 0 && push == 0) { Ui.Tex(part.T, x, y, 1, Color.White, p.Flip); continue; }
            float sw = (M.Sin(q + p.Phase + part.Phase + p.X * .01f) * (.35f + MathF.Abs(wind) * .55f) + wind * .7f) * MathF.Abs(part.Sway);
            if (p.Kind is 1 or 3) { float wy = M.Cos(q * 1.3f + part.Phase * 2 + p.Phase) * MathF.Abs(wind) * .5f; if (part.Pivot < 0) Life.Shear(part.T, x, y, MathF.Round(sw * 1.4f), Color.White, p.Flip, true); else Ui.Tex(part.T, x + MathF.Round(sw + push), y + MathF.Round(wy), 1, Color.White, p.Flip); }
            else if (part.Sway < 0) Life.Shear(part.T, x, y, MathF.Round(sw * 1.2f), Color.White, p.Flip, true);
            else { bool water = p.S.StartsWith("kelp") || p.S.StartsWith("weed") || p.S.StartsWith("glowk") || p.S.StartsWith("lily"); float a = water ? M.Sin(time * 1.2f + p.Phase) * part.Sway * .8f : sw; Life.Shear(part.T, x, y, MathF.Round((a + push) * 2) / 2, Color.White, p.Flip); }
        }
    }
    Texture2D shade;
    void Shades(int cx, int cy, int w)
    {
        // soft pools of shade under canopies; drawn multiplied into the light map
        if (shade.Id == 0) { var p = new Painter(64, 64); for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = MathF.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32; float v = M.Clamp(1 - d); v = 1 - v * v * .62f; p.Px[y * 64 + x] = new Color((int)(v * 255), (int)(M.Lerp(v, 1, .05f) * 255), (int)(M.Lerp(v, 1, .16f) * 255), 255); } shade = p.ToTexture(); Raylib.SetTextureFilter(shade, TextureFilter.Bilinear); }
        float sunX = -Env.SunDir.X * 26; foreach (var p in Visible(140)) if (p.Kind == 1 && p.Fall < 0) { float sc = Sprites.Get(p.S).H / 110; if (p.S.StartsWith("bamboo") || p.S.StartsWith("cactus") || p.S.StartsWith("charred")) sc *= .5f; float x = p.X - cx + sunX, y = p.Y - cy; Raylib.DrawTexturePro(shade, new Rectangle(0, 0, 64, 64), new Rectangle(x - 58 * sc, y - 46, 116 * sc, 84), Vector2.Zero, 0, Color.White); }
    }
    void Glows(int cx, int cy, int w, int h, float lampA)
    {
        for (int i = 0; i < R.Lights.Count; i++) { var l = R.Lights[i]; float x = l.x - cx, y = l.y - cy; if (i < lampN) { bool cold = l.c.B > l.c.R; Ui.GlowAt(x, y, cold ? 26 : l.r * .3f, l.c.Rl(.2f * M.Clamp(l.i))); Ui.GlowAt(x, y, 11, l.c.Rl(.34f * M.Clamp(lampA))); } else if (l.r is 120 or 70 or 64 or 135) Ui.GlowAt(x, y, 24, l.c.Rl(.22f * M.Clamp(l.i))); }
        if (!Title) foreach (var l in loot) if (l.T > l.Born && Blocks.Glows((T)l.Id)) Ui.GlowAt(l.P.X - cx, l.P.Y - 6 - cy, 16, ((Col)Blocks.All[l.Id].Glow).Rl(.35f)); Life.DrawPts(cx, cy, true); Life.DrawGlows(cx, cy, w, R);
    }
}

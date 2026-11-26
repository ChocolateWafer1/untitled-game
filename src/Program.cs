using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public enum Scr { Title, Select, Editor, World }

public static class Program
{
    public static Scr Screen = Scr.Title; public static EditorScreen Editor; public static GameScreen Game; public static readonly SelectScreen Select = new(); public static bool Quit; static string shot; static string shotDir; static Task<World> growing; static Action<World> grown; static float growT; public static bool Busy => irisT < 1 || hold > 0 || growing != null;
    // cartoon iris
    static float iris = 1, irisFrom, irisTo, irisT = 1, irisDur = .8f; static Vector2 irisC; static Action irisMid; static float hold;

    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].StartsWith("--") && Dev.Cpu(args)) return;
        bool dev = args.Length > 1 && args[0] == "--shot"; if (dev) { shot = args[1]; shotDir = args[2]; Store.ReadOnly = true; Store.Dir = Path.Combine(shotDir, "nosave"); }
        try { Run(dev); }
        catch (Exception e) when (!dev) { try { Directory.CreateDirectory(Store.Dir); File.WriteAllText(Path.Combine(Store.Dir, "crash.txt"), e.ToString()); } catch { } throw; }
    }
    static void Run(bool dev)
    {
        Store.LoadAll(); Raylib.SetTraceLogLevel(dev && shot == "passes" ? TraceLogLevel.Info : TraceLogLevel.Warning);
        Raylib.SetConfigFlags((dev ? ConfigFlags.HiddenWindow : ConfigFlags.ResizableWindow) | (Store.Cfg.Vsync && !dev ? ConfigFlags.VSyncHint : 0)); Raylib.InitWindow(1280, 720, "Untitled Game"); Raylib.SetExitKey(KeyboardKey.Null); if (!dev) Raylib.SetTargetFPS(Store.Cfg.Vsync ? 0 : 144);
        if (!dev) ApplyWindow();
        Snd.Load(dev); Ui.Load(); Editor = new EditorScreen { Done = EditorDone, Back = EditorBack }; Game = new GameScreen(new World(dev ? 4242 : Saves.All.OrderByDescending(q => q.Data.Played).FirstOrDefault()?.Data.Seed ?? Random.Shared.Next(1, int.MaxValue)));
        Dev.Shots = shotDir;
        if (dev) { Dev.RunShot(shot); Raylib.CloseWindow(); return; }
        Start();
        while (!Raylib.WindowShouldClose() && !Quit) Frame(MathF.Min(Raylib.GetFrameTime(), .05f));
        Store.SaveCfg(); Store.SaveGame(); Snd.Close(); Raylib.CloseWindow();
    }
    public static void StartForTest() => Start();
    static void Start() { Screen = Scr.Title; Game.OpenTitle(); iris = 0; IrisOpen(new(Ui.VW * .5f, 110)); }
    static AvatarData Fresh() => new() { Name = "" };
    static void OpenEditor() { Screen = Scr.Editor; Game.Title = true; Editor.Open(Fresh(), true); IrisOpen(new(Ui.VW * .27f, 190)); }
    /// Main menu -> save files -> a new character (the creator) or an existing one.
    public static void PlayFromTitle() { Screen = Scr.Select; Select.Open(); Snd.Play("page"); }
    public static void BackToTitle() { Screen = Scr.Title; Snd.Play("close", .8f); }
    public static void NewCharacter() { if (!Saves.Full) IrisClose(Ui.Mouse, OpenEditor); }
    static bool editInGame;
    /// Change a character that already exists, from the save files or from the pause menu.
    public static void EditAvatar(Saves.Slot s, Vector2 c, bool inGame)
    {
        if (Busy || s == null) return; if (inGame) Store.SaveGame(); IrisClose(c, () => { editInGame = inGame; Screen = Scr.Editor; Editor.Open(s.Data.Avatar, true, s); IrisOpen(new(Ui.VW * .27f, 190)); });
    }
    static void BackToGame(float dur = 1.25f) => IrisClose(Editor.AvatarCenter, () => { Screen = Scr.World; Game.P.SetAvatar(Store.Data.Avatar); IrisOpen(Game.ToScreen(Game.P.P + new Vector2(0, -36)), dur); });
    static void EditorBack() { if (Busy) return; if (Editor.Editing != null && editInGame) { BackToGame(); return; } ToSelect(); }
    static void ToSelect() => IrisClose(Editor.AvatarCenter, () => { Screen = Scr.Select; Game.OpenTitle(); Select.Open(); IrisOpen(new(Ui.VW / 2, Ui.VH / 2)); });
    /// The world grows on another thread while the iris closes; if it is not done yet, a little sprout keeps you company.
    static void Enter(Saves.Slot s, Vector2 c)
    {
        var d = s.Data; growing = null; var t = Task.Run(() => new World(d.Seed, d.Gen == World.GenVer ? d.Edits : "", d.Gen == World.GenVer ? d.Gone : null)); IrisClose(c, () => { growing = t; growT = 0; grown = w => { Store.Current = s; Store.Data = d; Game.SetWorld(w); Screen = Scr.World; Game.Title = false; Game.Begin(); IrisOpen(Game.ToScreen(Game.P.P + new Vector2(0, -36)), 1.35f); }; });
    }
    public static void PlaySave(Saves.Slot s, Vector2 c) { if (!Busy) Enter(s, c); }
    public static void ToTitle() => IrisClose(new(Ui.VW / 2, Ui.VH / 2), () => { Store.SaveGame(); Store.Current = null; Game.Leave(); Screen = Scr.Title; Game.OpenTitle(); IrisOpen(new(Ui.VW * .5f, 110)); });
    public static void Warp(Action mid) => IrisClose(Game.ToScreen(Game.P.P + new Vector2(0, -36)), () => { mid(); IrisOpen(Game.ToScreen(Game.P.P + new Vector2(0, -36))); });
    static void EditorDone()
    {
        if (Editor.Editing is Saves.Slot e) { if (!Saves.Edit(e, Editor.D)) Snd.Play("error"); if (editInGame) BackToGame(); else ToSelect(); return; }
        var s = Saves.Create(Editor.D); if (s == null) { Snd.Play("error"); EditorBack(); return; } Enter(s, Editor.AvatarCenter);
    }
    public static void ApplyWindow()
    {
        bool bl = Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);
        if (Store.Cfg.Window == 0) { if (!bl) Raylib.ToggleBorderlessWindowed(); }
        else { if (bl) Raylib.ToggleBorderlessWindowed(); Raylib.MaximizeWindow(); }
    }
    public static void Frame(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F11)) { Store.Cfg.Window = 1 - Store.Cfg.Window; ApplyWindow(); Store.SaveCfg(); }
        Ui.Begin(dt); Rumble.Update(dt); bool busy = Busy; if (busy) { Ui.Click = Ui.Held = Ui.RightClick = Ui.RightHeld = Ui.Release = false; Ui.Wheel = 0; } In.Locked = busy;
        Snd.Update(dt); Voice.Update(dt);
        if (Screen == Scr.Editor) { Snd.Music("editor"); Editor.Update(dt); } else { if (Screen == Scr.Select) Select.Update(dt); Game.Update(dt); }
        Raylib.BeginDrawing(); Raylib.ClearBackground(Color.Black); if (Screen != Scr.Editor) Game.RenderWorld();
        if (Screen != Scr.Editor) { Ui.Push(); Game.DrawWorld(); Ui.Pop(); } Ui.Push(); if (Screen == Scr.Editor) Editor.Draw(); else { Game.DrawUi(); if (Screen == Scr.Select) Select.Draw(); } Ui.End(); DrawIris(dt); Ui.Pop(); Raylib.EndDrawing();
    }
    public static void IrisClose(Vector2 c, Action mid, float dur = .85f) { irisC = c; irisFrom = 1; irisTo = 0; irisT = 0; irisDur = dur; irisMid = mid; Snd.Play("iris_in"); }
    public static void IrisOpen(Vector2 c, float dur = 1.25f) { irisC = c; irisFrom = 0; irisTo = 1; irisT = 0; irisDur = dur; irisMid = null; Snd.Play("iris_out"); }
    static void DrawIris(float dt)
    {
        if (growing != null)
        {
            growT += dt; Raylib.DrawRectangle(-2, -2, (int)Ui.VW + 4, (int)Ui.VH + 4, Color.Black); if (growT > .35f) { float a = M.Clamp((growT - .35f) * 3), cx = Ui.VW / 2, cy = Ui.VH / 2 + 6, g = .5f + .5f * M.Sin(growT * 3); Font.Draw("Growing the world", cx, cy + 22, Raylib.Fade(Ui.Cream, a), 1.5f, .5f); Raylib.DrawEllipse((int)cx, (int)cy + 10, 16, 4, Raylib.Fade(new Color(122, 82, 58, 255), a)); float h = 10 + g * 8, l = M.Sin(growT * 2) * 2; Raylib.DrawLineEx(new(cx, cy + 9), new(cx + l, cy + 9 - h), 2, Raylib.Fade(new Color(122, 168, 84, 255), a)); Raylib.DrawEllipse((int)(cx + l - 5), (int)(cy + 8 - h), 5, 2.6f, Raylib.Fade(new Color(142, 190, 98, 255), a)); Raylib.DrawEllipse((int)(cx + l + 5), (int)(cy + 7 - h), 5, 2.6f, Raylib.Fade(new Color(122, 168, 84, 255), a)); }
            if (growing.IsCompleted) { var w = growing.Result; growing = null; var f = grown; grown = null; f(w); } return;
        }
        if (hold > 0) { hold -= dt; Raylib.DrawRectangle(-2, -2, (int)Ui.VW + 4, (int)Ui.VH + 4, Color.Black); if (hold <= 0) { var m = irisMid; irisMid = null; m?.Invoke(); } return; }
        if (irisT < 1)
        {
            irisT = MathF.Min(1, irisT + MathF.Min(dt, 1 / 50f) / irisDur); /* a slow first frame after loading must not skip the start */ float max = MathF.Sqrt(Ui.VW * Ui.VW + Ui.VH * Ui.VH); float r;
            if (irisTo < irisFrom) { float t = irisT; r = t < .62f ? M.Lerp(max, 62, Ease.OutCubic(t / .62f)) : t < .78f ? 62 + M.Sin((t - .62f) / .16f * M.PI) * 7 : M.Lerp(62, 0, Ease.InCubic((t - .78f) / .22f)); }
            else { float t = M.Clamp((irisT - .2f) / .8f); r = t <= 0 ? 0 : max * t * t * (.35f + .65f * t); } // fully black first, then one smooth growth from nothing
            iris = r; Ring(r); if (irisT >= 1 && irisTo == 0) hold = .28f;
        }
    }
    static void Ring(float r)
    {
        r = MathF.Max(0, r); float max = MathF.Max(Ui.VW, Ui.VH) * 2.2f; int seg = 64; if (r < .5f) { Raylib.DrawRectangle(-2, -2, (int)Ui.VW + 4, (int)Ui.VH + 4, Color.Black); return; }
        Raylib.DrawRing(irisC, r, max, 0, 360, seg, Color.Black); Raylib.DrawRing(irisC, r, r + 2.5f, 0, 360, seg, new Color(58, 32, 30, 255));
    }
}

/// Developer tooling: contact sheets and scripted screenshots.
public static class Dev
{
    public static string Shots;
    public static bool Cpu(string[] a)
    {
        switch (a[0])
        {
            case "--sheet": Sheet(a[1]); return true; case "--walk": WalkSheet(a[1]); return true; case "--migrate": Store.Migrate(a[1], a[2]); return true; case "--slide": SlideSheet(a[1]); return true; case "--poses": PoseSheet(a[1]); return true; case "--wind": WindSheet(a[1]); return true; case "--wave": WaveSheet(a[1]); return true; case "--cat": Cat(a[1], a[2]); return true; case "--mine": MineSheet(a[1]); return true; case "--blocks": BlockSheet(a[1]); return true; case "--worlds": Worlds(a[1], int.Parse(a[2])); return true; case "--icon": IconArt(a[1]); return true; case "--hand": { var big = new Painter(1000, 340); int k = 0; foreach (Col sk in new Col[] { 0xf0c4a0, 0xc68a5e, 0x7e4b30 }) { Blit(GameScreen.HandArt(34, sk, k == 0 ? 0x2f9a94 : 0xc65a4a), big, 10 + k * 320, 10, 8); k++; } Finish(big, a[1]); return true; } case "--weight": { int[] lbs = [100, 110, 120, 130, 140, 150, 170, 190, 220, 260, 300]; var big = new Painter(lbs.Length * 130, 300); var ap = new AvatarPainter(); var one = new Painter(130, 300); for (int i = 0; i < lbs.Length; i++) { float lo = 0, hi = 1; for (int k = 0; k < 24; k++) { float m = (lo + hi) / 2; if (Rig.Pounds(new AvatarData { Weight = m }) < lbs[i]) lo = m; else hi = m; } one.Clear(); var d = new AvatarData { Weight = lo }; var rg = new Rig(d); ap.Draw(one, d, Pose.Stand, 2.5f, 65, 290, outlinePx: 2); Blit(one, big, i * 130, 0); Console.WriteLine($"{lbs[i]} lb at average height: slider {lo:0.00}, waist {rg.Waist * 67 / 95.2f * 5.8f:0.0} in"); } Finish(big, a[1]); return true; } case "--steps": Tall(); return true; case "--backs": Backs(a[1]); return true;
        }
        return false;
    }
    static void IconArt(string path) { var p = new Painter(64, 64); p.View(1, 0, 0); p.Begin().Ellipse(32, 32, 30, 30); p.Shade(new Ink((x, y) => Col.Lerp(0x7ab8e0, 0xffc878, M.Clamp(y / 46)), 6, .5f, 1)); p.Begin().Ellipse(46, 22, 7, 7); p.IntersectAlpha(); p.Flat(0xfff2c0); p.Begin().Blob(new(0, 46), new(14, 36), new(30, 42), new(46, 35), new(64, 44), new(60, 64), new(4, 64)); p.IntersectAlpha(); p.Shade(new Ink(0x8ab46a, 3, .5f, .7f)); p.Begin().Blob(new(6, 54), new(18, 44), new(32, 40), new(46, 44), new(58, 54), new(48, 66), new(16, 66)); p.IntersectAlpha(); p.Shade(new Ink(0x6f9a4c, 5, .6f, .9f)); p.Curve(new(32, 44), new(30, 30), new(33, 17), 3.2f, 0x5f8a46, false); p.Begin().Blob(new(33, 18), new(42, 10), new(44, 18), new(37, 22)); p.Shade(new Ink(0x9ac862, 3, .8f, .8f)); p.Begin().Blob(new(32, 22), new(22, 16), new(21, 24), new(28, 27)); p.Shade(new Ink(0x7ab050, 3, .8f, .8f)); p.Outline(0x33201f, 1); p.Export(path); }
    /// Every step in the land too tall to jump.
    static void Tall() { foreach (int sd in new[] { 4242, 12161, 20080, 991, 77 }) { var w = new World(sd); Console.WriteLine($"{sd}: spawn {w.SpawnTile} tops " + string.Join(" ", Enumerable.Range(w.SpawnTile - 10, 24).Select(q => w.Top0[q]))); for (int x = 100; x < World.W - 100; x++) if (Math.Abs(w.Top0[x] - w.Top0[x - 1]) > 3) Console.WriteLine($"   {x} {w.BiomeAt(x)} {w.Top0[x - 1]} > {w.Top0[x]}"); Console.WriteLine("   falls " + string.Join(" ", w.Falls) + " pools " + string.Join(" ", w.Pools.Take(14).Select(q => q.X0 + "-" + q.X1 + "@" + q.Row))); } }
    /// Whole worlds, one pixel per block.
    static void Worlds(string path, int count)
    {
        var big = new Painter(World.W, count * (World.H + 8)); for (int k = 0; k < count; k++) { var sw = Stopwatch.StartNew(); var w = new World(k * 7919 + 4242); World.Cur = w; long ms = sw.ElapsedMilliseconds; int steep = 0; for (int x = 100; x < World.W - 100; x++) if (Math.Abs(w.Top0[x] - w.Top0[x - 1]) > 3) steep++; var seen = new HashSet<Biome>(); for (int x = 0; x < World.W; x += 4) for (int y = 0; y < World.H; y += 4) { if (Biomes.Of(w.UnderAt(x, y)).Under) seen.Add(w.UnderAt(x, y)); } Console.WriteLine($"seed {w.Seed}: {ms} ms, props {w.Props.Count}, lamps {w.Lamps.Count}, pools {w.Pools.Count}, falls {w.Falls.Count}, tall steps {steep}, under {string.Join("/", seen)}, order {string.Join(" ", w.Segs.Select(q => q.B))}, spawn {w.SpawnTile}"); for (int x = 0; x < World.W; x++) for (int y = 0; y < World.H; y++) { int i = y * World.W + x; Col c = w.Wet[i] != 0 ? w.Pools[w.Wet[i] - 1].Shallow : w.Tile[i] != T.Air ? (w.Turf[i] ? World.GrassCol(x) : (Col)Blocks.Of(w.Tile[i]).Color) : w.Climb[i] != 0 ? 0x40c040 : w.Wall[i] != T.Air ? Col.Lerp(Blocks.Of(w.Wall[i]).Color, 0x201828, .72f) : 0xbfd8ea; big.Px[(k * (World.H + 8) + y) * World.W + x] = c.Rl(); } foreach (var p in w.Props) { int px = (int)(p.X / 24), py = (int)((p.Y - 6) / 24); if (px >= 0 && py >= 0 && px < World.W && py < World.H && p.Kind == 1) for (int q = 0; q < 4 && py - q >= 0; q++) big.Px[(k * (World.H + 8) + py - q) * World.W + px] = new Color(60, 110, 60, 255); } }
        big.Export(path);
    }
    static void BlockSheet(string path) { var big = new Painter(14 * 60, 3 * 100); for (int i = 1; i < Blocks.All.Length; i++) { int x = (i - 1) % 14 * 60 + 8, y = (i - 1) / 14 * 100 + 8; Blit(Tiles.Chunk((T)i, 20), big, x, y, 2); Blit(Tiles.Chunk((T)i, 12), big, x + 8, y + 50, 2); } Finish(big, path); }
    static void MineSheet(string path)
    {
        var big = new Painter(1500, 4 * 250); var ap = new AvatarPainter(); var one = new Painter(250, 250); int row = 0; foreach (var aim in new[] { new Vector2(1, .1f), new Vector2(-1, .2f), new Vector2(.1f, 1), new Vector2(.15f, -1) }) { var an = new AvatarAnim { Dir = aim.X > 0 ? 1 : -1, Aim = Vector2.Normalize(aim), MineAmt = 1, Fps = 18 }; an.Set(Anim.Mine); for (int k = 0; k < 40; k++) an.Update(1 / 60f); for (int i = 0; i < 6; i++) { int g = 0; while (!an.Update(1 / 60f) && g++ < 20) { } one.Clear(); ap.Draw(one, new AvatarData(), an.Pose, 2.0f, 125, 235, outlinePx: 2); Blit(one, big, i * 250, row * 250); } row++; } Finish(big, path);
    }
    static void Backs(string path) { var m = typeof(Backdrop).GetMethod("Layer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); var big = new Painter(1024 * 2, 8 * 200); for (int b = 0; b < 16; b++) { for (int L = 0; L < 4; L++) { var p = (Painter)m.Invoke(null, [(Biome)b, L]); int ox = b % 2 * 1024, oy = b / 2 * 200; for (int y = 0; y < 200; y++) for (int x = 0; x < 1024; x++) { int sy = y - L * 14 + 30; if (sy < 0 || sy >= 300) continue; var c = p.Px[sy * 1024 + x]; if (c.A > 80) big.Px[(oy + y) * big.W + ox + x] = c; } } } Finish(big, path); }
    public static void Steps(int n, float dt = 1 / 60f) { for (int i = 0; i < n; i++) Program.Frame(dt); }
    public static void Save(string n) { var img = Raylib.LoadImageFromScreen(); if (img.Width != 1280) Raylib.ImageCrop(ref img, new Rectangle(0, img.Height - 720, 1280, 720)); Raylib.ExportImage(img, Path.Combine(Shots, n + ".png")); Raylib.UnloadImage(img); }
    public static void ClickAt(float x, float y) { Ui.FakeMouse = new(x, y); Steps(2); Ui.FakeMouse = new(x, y); Ui.FakeClick = true; Steps(1); Steps(2); }
    static void Key(Act a, string nm = null, int wait = 50) { In.ForcePress.Add(a); Steps(1); In.ForcePress.Clear(); Steps(wait); if (nm != null) Save(nm); }
    static void NewSave(string name, int seed) { var r = new Rng(12 + name.Length); var a = AvatarData.Random(r); a.Name = name; var d = new SaveData { Avatar = a, Seed = seed, Clock = .42f }; d.Validate(); Store.Data = d; Store.Current = new Saves.Slot { Data = d, File = "x" }; }
    static void Wait(int max = 900) { for (int i = 0; i < max && Program.Busy; i++) { Steps(1); if (i > 120) Thread.Sleep(2); } }
    public static void RunShot(string n)
    {
        if (n == "audio") { Snd.Load(false); Program.Screen = Scr.Editor; Program.Editor.Open(new AvatarData(), false); foreach (var q in new[] { "click", "step_grass", "step_stone", "step_snow", "splash", "voicea", "thunder", "bird", "dice" }) { Snd.Play(q, .05f); Steps(10); Console.WriteLine(q + " " + Snd.Has(q)); } Snd.Close(); return; }
        if (n == "flow") { Flow(); return; }
        if (n == "creator")
        {
            var r = new Rng(31); Program.Screen = Scr.Editor; var d = AvatarData.Random(r); d.Name = "Robin"; d[Slot.Top].Id = 0; d.Tattoos[0] = 3; d.Tattoos[1] = 12; d.Validate(); d.Liner = 2; d.Lipstick = .5f; d.Piercings = 1 | 64; Program.Editor.Open(d, true); Ui.FakeMouse = new(5, 5); Steps(60); var E = Program.Editor;
            foreach (var (t, sb, nm) in new[] { (1, 0, "shape"), (1, 5, "skin"), (1, 6, "makeup"), (1, 7, "tattoos"), (1, 8, "piercings"), (1, 9, "beard"), (2, 1, "hairshape"), (0, 0, "body") }) { E.ShowForTest(t, sb); for (int i = 0; i < 40; i++) Steps(1); Save("cr_" + nm); }
            // place a mole by clicking the face, then type a height and a weight
            E.ShowForTest(1, 5); Steps(10); var (hc, hrx, hry) = E.HeadForTest; float avX = M.Clamp((Ui.VW - 336 - 34) / 2, 110, 260); var at = new Vector2(avX - 125 + hc.X + hrx * .5f, 318 - 336 + hc.Y + hry * .4f); int before = E.D.Moles.Count; ClickAt(at.X, at.Y); Steps(10); Console.WriteLine($"moles {before} -> {E.D.Moles.Count}"); Save("cr_mole");
            E.ShowForTest(0, 0); Steps(5); float px = Ui.VW - 336 + 14, w = 336 - 28; ClickAt(px + w - 20, 6 + 34 + 2 + 3); Steps(20); Save("cr_typing"); foreach (var (i, txt) in new[] { (0, "5'4\""), (0, "6 1"), (0, "178cm"), (1, "120"), (1, "80kg"), (1, "190"), (3, "30") }) Console.WriteLine($"typed {txt,-7} -> {E.TypeForTest(i, txt)}"); Steps(20); Save("cr_typed"); return;
        }
        if (n == "studio")
        {
            var r = new Rng(31); Program.Screen = Scr.Editor; var d = AvatarData.Random(r); d.Name = "Robin"; d[Slot.Top].Id = TankTop(); d[Slot.Jacket].Id = 0; d.Validate(); Program.Editor.Open(d, true); Ui.FakeMouse = new(5, 5); Steps(30); var E = Program.Editor;
            E.ShowForTest(1, 7); Steps(30); Save("st_page"); float px = Ui.VW - 336 + 14, w = 336 - 28; ClickAt(px + (w - 104) / 2, 340 - 18); Steps(20); Console.WriteLine("studio open: " + E.StudioOpen); Save("st_open");
            float X0 = MathF.Floor((Ui.VW - 640) / 2); void Tool(int i) => ClickAt(X0 + 16 + 29, 44 + i * 21 + 9);
            void Drag(params (int x, int y)[] cells) { var p0 = E.CellPosForTest(cells[0].x, cells[0].y); Ui.FakeMouse = p0; Steps(2); Ui.FakeMouse = p0; Ui.FakeClick = true; Steps(1); Ui.FakeHold = true; for (int i = 1; i < cells.Length; i++) { var a = E.CellPosForTest(cells[i - 1].x, cells[i - 1].y); var b = E.CellPosForTest(cells[i].x, cells[i].y); for (int k = 1; k <= 6; k++) { Ui.FakeMouse = Vector2.Lerp(a, b, k / 6f); Steps(1); } } Ui.FakeHold = false; Ui.FakeRelease = true; Steps(1); Steps(4); }
            void Click(int x, int y) { var p0 = E.CellPosForTest(x, y); ClickAt(p0.X, p0.Y); Steps(4); }
            ClickAt(X0 + 286 + 45, 282 + 9); // mirror across on
            Tool(0); Drag((6, 4), (30, 12), (60, 3), (90, 13), (120, 5)); Console.WriteLine("brush, mirrored: " + E.StudioInk(0)); Save("st_brush");
            Tool(4); Drag((20, 9), (44, 22)); int boxed = E.StudioInk(0); ClickAt(X0 + 80 + 10 * 18 + 9, 258 + 9); Tool(2); Click(32, 15); Console.WriteLine($"box {boxed}, filled red inside: " + E.StudioInk(0)); Save("st_fill");
            Tool(6); Drag((18, 16), (46, 16)); Console.WriteLine("knife slice: " + E.StudioInk(0));
            ClickAt(X0 + 380 + 40, 282 + 9); ClickAt(X0 + 380 + 40, 282 + 9); ClickAt(X0 + 464 + 46, 282 + 9); ClickAt(X0 + 80 + 4 * 18 + 9, 258 + 9); Tool(5); Drag((70, 12), (78, 20)); Console.WriteLine("circles x3 repeated: " + E.StudioInk(0)); Save("st_repeat");
            Tool(7); Drag((19, 8), (45, 23)); Drag((30, 14), (52, 6)); Console.WriteLine("moved selection: " + E.StudioInk(0)); Save("st_move");
            var zp = E.CellPosForTest(40, 16); Ui.FakeMouse = zp; Steps(2); for (int i = 0; i < 6; i++) { Ui.FakeWheel = 1; Steps(2); } Steps(10); Save("st_zoom");
            ClickAt(X0 + 496 + 63, 306 + 9); Console.WriteLine("copied to other arm: " + E.StudioInk(1)); ClickAt(X0 + 552 + 35, 13 + 10); Steps(60); Console.WriteLine("studio closed: " + !E.StudioOpen + ", tattoos " + string.Join(",", E.D.Tattoos.Take(2))); E.ShowForTest(0, 0); Steps(60); Save("st_done"); { var json = System.Text.Json.JsonSerializer.Serialize(E.D); var back = System.Text.Json.JsonSerializer.Deserialize<AvatarData>(json); back.Validate(); Console.WriteLine($"saved and loaded: {back.Art[0]?.Count(b => b != 0)} / {back.Art[1]?.Count(b => b != 0)}, same {back.Art[0].SequenceEqual(E.D.Art[0]) && back.Art[1].SequenceEqual(E.D.Art[1])}, file {json.Length / 1024} KB"); }
            return;
        }
        if (n == "editor")
        {
            var r = new Rng(12); Program.Screen = Scr.Editor; var d = AvatarData.Random(r); d.Name = "Juniper"; Program.Editor.Open(d, true); Ui.FakeMouse = new(5, 5); Steps(90); Save("ed_body"); float px = Ui.VW - 336; for (int i = 1; i < 10; i++) { ClickAt(px - 10, 6 + 8 + i * 33 + 15); Steps(70); Save("ed_" + i); } return;
        }
        RunWorld(n);
    }
    /// First launch, making characters, the six-save limit, duplicate names, switching, deleting and world persistence, on real files in a test folder.
    static void Flow()
    {
        void Check(string what, bool ok) => Console.WriteLine((ok ? "ok    " : "FAIL  ") + what); Store.TestSaves = true; if (Directory.Exists(Store.Dir)) Directory.Delete(Store.Dir, true); Saves.Refresh(); var G = Program.Game; Ui.FakeMouse = new(5, 300); float pxE = 0;
        void Create(string nm) { Program.Editor.SetNameForTest(nm); Steps(2); pxE = Ui.VW - 336; ClickAt(pxE + 270, 6 + 348 - 24); Steps(90); }
        Program.StartForTest(); Steps(150); Check("first launch opens the main menu", Program.Screen == Scr.Title && Saves.All.Count == 0); Save("f_menu"); ClickAt(Ui.VW / 2, 180); Steps(60); Check("play opens the save files, all empty", Program.Screen == Scr.Select && Saves.All.Count == 0); Save("f_empty"); In.ForcePress.Add(Act.Menu); Steps(1); In.ForcePress.Clear(); Steps(10); Check("escape goes back to the main menu", Program.Screen == Scr.Title); ClickAt(Ui.VW / 2, 180); Steps(60); float gx0 = (Ui.VW - 188 * 3 - 24) / 2; ClickAt(gx0 + 94, 52 + 44); Steps(60); Wait(); Steps(60); Check("new character opens the creator", Program.Screen == Scr.Editor); ClickAt(25, 24); Steps(60); Wait(); Steps(90); Check("back from the creator returns to the save files", Program.Screen == Scr.Select && Saves.All.Count == 0); ClickAt(gx0 + 94, 52 + 44); Steps(60); Wait(); Steps(60); Save("f_creator"); Create("Pip"); Steps(60); Wait(); Steps(60); Check("finishing makes one save and enters the world", Saves.All.Count == 1 && Program.Screen == Scr.World && File.Exists(Path.Combine(Saves.Dir, "Pip.json"))); int seedA = G.W.Seed; Check("world uses the save's seed", seedA == Saves.All[0].Data.Seed); Save("f_world");
        // change the world
        int tx = (int)(G.P.P.X / 24) + 3, ty = G.W.Top[tx]; var was = G.W.At(tx, ty); G.W.Set(tx, ty, T.Air); G.W.Set(tx + 4, G.W.Top[tx + 4] - 1, T.Chalk); int py = G.W.Top[tx + 4]; Bag.Add(Store.Data, (int)T.Clay, 130); Steps(5); Key(Act.Menu, null, 30); Save("f_pause"); ClickAt(Ui.VW / 2, 180 - 107.5f + 8 + 16 + 4 * 31 + 13); Steps(60); Wait(); Steps(150); Check("save and exit reaches the title", Program.Screen == Scr.Title && Store.Current == null); Save("f_title");
        ClickAt(Ui.VW / 2, 180); Steps(60); Check("play opens character select", Program.Screen == Scr.Select); Save("f_select1"); float gx = (Ui.VW - 188 * 3 - 24) / 2; void ClickCard(int i, float ox, float oy) => ClickAt(gx + i % 3 * 200 + ox, 52 + i / 3 * 94 + oy);
        foreach (var (nm, k) in new[] { ("Juniper", 1), ("Moss", 2), ("Wren", 3), ("Olive", 4), ("con", 5), ("Basil", 6), ("Fern", 7), ("Hazel", 8) }) { ClickCard(k, 94, 44); Steps(60); Wait(); Steps(30); Check("creator opens for " + nm, Program.Screen == Scr.Editor); if (k == 1) { Create("pip"); Steps(20); Check("a taken name is refused", Program.Screen == Scr.Editor && Saves.All.Count == 1); Save("f_taken"); } Create(nm); Steps(60); Wait(); Steps(30); Check(nm + " saved", Saves.All.Count == k + 1 && Program.Screen == Scr.World); Program.ToTitle(); Steps(60); Wait(); Steps(120); Program.PlayFromTitle(); Steps(50); }
        Save("f_select9"); Check("nine saves is full", Saves.Full && Saves.Create(new AvatarData { Name = "Tenth" }) == null); Check("every world has its own seed", Saves.All.Select(q => q.Data.Seed).Distinct().Count() == 9); Check("reserved Windows name got a safe file", File.Exists(Path.Combine(Saves.Dir, "con_.json")));
        ClickCard(0, 188 - 23, 88 - 21); Steps(60); Save("f_delete"); ClickAt(Ui.VW / 2 - 130 + 74, 180 - 56 + 112 - 28); Steps(20); Check("keep keeps", Saves.All.Count == 9); ClickCard(8, 188 - 23, 88 - 21); Steps(60); ClickAt(Ui.VW / 2 + 130 - 74, 180 - 56 + 112 - 28); Steps(20); Check("delete removes one", Saves.All.Count == 8 && Directory.GetFiles(Path.Combine(Store.Dir, "deleted")).Length == 1);
        ClickCard(0, 104, 88 - 21); Steps(60); Wait(); Steps(60); Check("pip loads again", Store.Data.Avatar.Name == "Pip" && G.W.Seed == seedA); Check("dug hole was remembered", G.W.At(tx, ty) == T.Air && was != T.Air); Check("placed block was remembered", G.W.At(tx + 4, py) == T.Chalk); Check("items were remembered", Bag.Count(Store.Data, (int)T.Clay) == 130); Check("position was remembered", MathF.Abs(G.P.P.X - Store.Data.X) < 40);
        // editing an existing character, from the pause menu and from the save files
        { float px2 = Ui.VW - 336; var cur = Store.Current; int skin0 = cur.Data.Avatar.Skin; Key(Act.Menu, null, 30); Save("f_pause2"); ClickAt(Ui.VW / 2, 180 - 107.5f + 8 + 16 + 31 + 13); Steps(60); Wait(); Steps(60); Check("pause menu opens the editor", Program.Screen == Scr.Editor && Program.Editor.Editing == cur); Program.Editor.D.Skin = 0x4a2c20; Program.Editor.D.Hair.Id = Catalog.BaldHair + 5; Steps(20); Save("f_editing"); ClickAt(px2 + 270, 6 + 348 - 24); Steps(150); Wait(); Steps(60); Check("saving returns to the same world", Program.Screen == Scr.World && G.W.Seed == seedA && G.W.At(tx, ty) == T.Air); Check("the player wears the change", G.P.D.Skin == 0x4a2c20 && skin0 != 0x4a2c20); Save("f_edited");
          Key(Act.Menu, null, 30); ClickAt(Ui.VW / 2, 180 - 107.5f + 8 + 16 + 31 + 13); Steps(60); Wait(); Steps(60); Program.Editor.D.Skin = 0xffffff; ClickAt(25, 24); Steps(60); Wait(); Steps(60); Check("back discards the edit", Program.Screen == Scr.World && G.P.D.Skin == 0x4a2c20); Program.ToTitle(); Steps(60); Wait(); Steps(120); Program.PlayFromTitle(); Steps(50); Save("f_select_edit"); ClickCard(0, 140, 88 - 21); Steps(60); Wait(); Steps(60); Check("the save file card opens the editor", Program.Screen == Scr.Editor && Program.Editor.Editing?.Data.Avatar.Name == "Pip"); Program.Editor.SetNameForTest("Juniper"); Steps(2); ClickAt(px2 + 270, 6 + 348 - 24); Steps(20); Check("another character's name is refused", Program.Screen == Scr.Editor); Program.Editor.SetNameForTest("Pippa"); Steps(2); ClickAt(px2 + 270, 6 + 348 - 24); Steps(150); Wait(); Steps(60); Check("renaming renames the file and keeps everything", Program.Screen == Scr.Select && File.Exists(Path.Combine(Saves.Dir, "Pippa.json")) && !File.Exists(Path.Combine(Saves.Dir, "Pip.json")) && Saves.All.Count == 8 && Saves.All[0].Data.Avatar.Name == "Pippa" && Saves.All[0].Data.Seed == seedA && Bag.Count(Saves.All[0].Data, (int)T.Clay) == 130); }
        // an old save from before seeds
        var legacy = Path.Combine(Shots, "legacy"); if (Directory.Exists(legacy)) Directory.Delete(legacy, true); Directory.CreateDirectory(legacy); Store.Dir = legacy; File.WriteAllText(Path.Combine(legacy, "save.json"), System.Text.Json.JsonSerializer.Serialize(new { Created = true, Avatar = new AvatarData { Name = "Oldie" }, Day = 7, Clock = .4f, Coins = 99, Baits = new[] { 0, 1, 2 }, Fish = new object[] { new { Id = 3 } } })); Saves.Refresh(); Check("old save imported", Saves.All.Count == 1 && Saves.All[0].Data.Avatar.Name == "Oldie" && Saves.All[0].Data.Day == 7 && File.Exists(Path.Combine(legacy, "save.json"))); File.Copy(Path.Combine(Saves.Dir, "Oldie.json"), Path.Combine(Saves.Dir, "Copy.json")); Saves.Refresh(); Check("a copied file is not a second character", Saves.All.Count == 1); File.WriteAllText(Path.Combine(Saves.Dir, "broken.json"), "{oops"); Saves.Refresh(); Check("a broken file is skipped", Saves.All.Count == 1);
    }
    public static void RunWorld(string n)
    {
        var G = Program.Game; NewSave("Juniper", G.W.Seed); Ui.FakeMouse = new(320, 8); Steps(2); Program.Screen = Scr.World; G.Title = false; G.Begin(); Steps(2);
        void At(Vector2 p, float clock, Weather w = Weather.Clear) { G.Travel(p); G.Env.Clock = clock; G.Env.SetWeather(w, true); G.Env.WeatherT = 999; }
        void Shot(string nm, Vector2? p, float clock, Weather w = Weather.Clear, int warm = 110) { if (p == null) { Console.WriteLine("no spot for " + nm); return; } At(p.Value, clock, w); Steps(warm); G.Env.Clock = clock; Steps(2); Save(nm); }
        Vector2 Ground(int x) => new(x * 24 + 12, G.W.Top[x] * 24);
        if (n == "biomes") { foreach (var b in Enum.GetValues<Biome>()) Shot("b_" + (int)b + "_" + b, G.FindBiome(b), b is Biome.Moonpetal ? .5f : .44f); return; }
        if (n == "dusk") { foreach (var b in new[] { Biome.Dunes, Biome.Crystal, Biome.Moonpetal, Biome.Cinder, Biome.Toadstool, Biome.Frost }) { Shot("d_" + b, G.FindBiome(b), b is Biome.Moonpetal or Biome.Toadstool ? .93f : .72f, b == Biome.Frost ? Weather.Rain : Weather.Clear); } return; }
        if (n == "mine")
        {
            At(G.W.SafeSpawn(), .45f); Steps(60); int px = (int)(G.P.P.X / 24), py = (int)(G.P.P.Y / 24); Vector2 Scr2(int x, int y) => new(x * 24 + 12 - G.CamX, y * 24 + 12 - G.CamY); void Hold(int x, int y, int frames, string nm = null, int every = 6) { Ui.FakeMouse = Scr2(x, y); Ui.FakeClick = true; Ui.FakeHold = true; for (int i = 0; i < frames; i++) { Ui.FakeMouse = Scr2(x, y); Steps(1); if (nm != null && i % every == every - 1) Save(nm + (i / every).ToString("00")); } Ui.FakeHold = false; Steps(1); }
            G.W.Set(px + 1, py - 1, T.Stone); G.W.Set(px + 1, py - 2, T.Geode); G.W.Set(px + 2, py - 1, T.Dirt); Steps(5); int before = Store.Data.Mined; Hold(px + 1, py - 1, 150, "m_", 10); Console.WriteLine("stone mined: " + (Store.Data.Mined - before) + " tile now " + G.W.At(px + 1, py - 1)); for (int i = 0; i < 6; i++) { Steps(5); Save("n_" + i); } Steps(120); Console.WriteLine("picked up stone: " + Bag.Count(Store.Data, (int)T.Stone)); Hold(px + 1, py - 2, 110); Save("m_geode"); Hold(px, py, 40); Save("m_down"); Hold(px, py, 60); Steps(60); Save("m_hole"); Console.WriteLine("dirt " + Bag.Count(Store.Data, (int)T.Dirt));
            { At(G.W.SafeSpawn(), .45f); Steps(30); int x = (int)(G.P.P.X / 24) + 1, y = (int)(G.P.P.Y / 24) - 1; G.W.Set(x, y, T.Dirt); Steps(2); int m0 = Store.Data.Mined, s0 = G.P.An.StrikeN; float f0 = 0;
              void Tap() { Ui.FakeMouse = Scr2(x, y); Steps(1); Ui.FakeMouse = Scr2(x, y); Ui.FakeClick = true; Steps(1); for (int i = 0; i < 50; i++) { Ui.FakeMouse = Scr2(x, y); Steps(1); } }
              Tap(); f0 = G.DigProgressForTest(x, y); Console.WriteLine($"one tap: dirt {f0:0.00} done, {G.P.An.StrikeN - s0} swing(s), still mining after: {G.P.Mining}"); Tap(); Console.WriteLine($"second tap breaks it: {Store.Data.Mined - m0 == 1}"); Steps(90); Console.WriteLine("dirt in bag after pickup: " + Bag.Count(Store.Data, (int)T.Dirt));
              Bag.Add(Store.Data, (int)T.Sand, 250); Console.WriteLine("250 sand stacks as: " + string.Join(",", Store.Data.Slots.Where(q => q.Id == (int)T.Sand).Select(q => q.N))); }
            { At(G.W.SafeSpawn(), .97f); Steps(30); int x = (int)(G.P.P.X / 24) + 1, y = (int)(G.P.P.Y / 24) - 1; G.W.Set(x, y, T.Glowrock); G.W.Set(x, y - 1, T.Stone); Steps(2); Hold(x, y, 130, "night_", 5); Steps(40); Save("night_after"); }
            foreach (T t in Enum.GetValues<T>()) if (Blocks.Minable(t)) { At(G.W.SafeSpawn(), .45f); Steps(8); int x = (int)(G.P.P.X / 24) + 2, y = (int)(G.P.P.Y / 24) - 1; G.W.Set(x, y, t); Steps(2); Ui.FakeClick = true; Ui.FakeHold = true; int f = 0; while (G.W.At(x, y) != T.Air && f++ < 900) { Ui.FakeMouse = Scr2(x, y); Steps(1); } Ui.FakeHold = false; Steps(2); Console.WriteLine($"{Blocks.Of(t).Name,-16} {f / 60f:0.0}s"); G.W.Set(x, y, T.Air); }
            return;
        }
        if (n == "pad")
        {
            In.FakePad = true; At(G.W.SafeSpawn(), .45f); Steps(40); int px = (int)(G.P.P.X / 24), py = (int)((G.P.P.Y - 1) / 24), dir = G.P.Dir; Console.WriteLine("controller in use: " + In.UsingPad + ", xinput pad found: " + Rumble.Connected);
            for (int k = 0; k < 3; k++) G.W.Set(px + dir, py - k, T.Dirt); G.W.Set(px, py - 3, T.Dirt); G.W.Set(px - dir, py - 1, T.Sand); Steps(3);
            void Rt(int frames) { In.FakeRT = true; Steps(frames); In.FakeRT = false; Steps(50); }
            Rt(1); Rt(1); Console.WriteLine($"RT tapped twice, stick at rest: chest block {G.W.At(px + dir, py - 1)}, knee block {G.W.At(px + dir, py)}"); Save("p_front");
            Rt(70); Console.WriteLine($"RT held: knee block {G.W.At(px + dir, py)}");
            In.Force.Add(Act.Up); Rt(70); In.Force.Remove(Act.Up); Console.WriteLine($"up + RT: block over head {G.W.At(px, py - 3)}");
            In.FakeR = new(-dir, 0); Console.WriteLine($"  aiming behind: player tile {(int)(G.P.P.X / 24)},{(int)((G.P.P.Y - 1) / 24)} dir {G.P.Dir}, targets {G.PadForTest()}, sand at {px - dir},{py - 1}"); Rt(70); In.FakeR = default; Console.WriteLine($"right stick behind + RT: sand {G.W.At(px - dir, py - 1)}");
            Bag.Add(Store.Data, (int)T.Stone, 10); Store.Data.Hand = Array.FindIndex(Store.Data.Slots, q => q.Id == (int)T.Stone); Steps(5); int st0 = Bag.Count(Store.Data, (int)T.Stone); Console.WriteLine($"  build targets {G.PadForTest()}"); In.FakeLT = true; Steps(1); In.FakeLT = false; Steps(20); Console.WriteLine($"LT builds in front: {G.W.At(px + dir, py)} / {G.W.At(px + dir, py - 1)}, stone used {st0 - Bag.Count(Store.Data, (int)T.Stone)}"); Save("p_build");
            In.FakeR = new(0, 1); Rt(80); In.FakeR = default; Console.WriteLine($"stick down + RT: under feet {G.W.At(px, py + 1)}"); Save("p_down"); In.FakePad = false; return;
        }
        if (n == "osk")
        {
            var r = new Rng(5); Program.Screen = Scr.Editor; var d = AvatarData.Random(r); d.Name = "Sky"; Program.Editor.Open(d, true); Ui.FakeMouse = new(5, 5); Steps(30); In.FakePad = true; float nx = (Ui.VW - 336) / 2; ClickAt(M.Clamp((Ui.VW - 336 - 34) / 2, 110, 260), 22); Steps(20); Console.WriteLine("keyboard showing: " + Ui.OskOn); Save("osk"); In.FakePad = false; Steps(3); return;
        }
        if (n == "build")
        {
            At(G.W.SafeSpawn(), .45f); Steps(40); var d = Store.Data; T[] give = [T.Chalk, T.RedRock, T.Glowrock, T.Dirt, T.Stone, T.Sand, T.Crystal, T.Amberwood, T.Snow, T.Basalt]; foreach (var t in give) Bag.Add(d, (int)t, 40); Steps(5); int px = (int)(G.P.P.X / 24), py = (int)(G.P.P.Y / 24); Vector2 Scr2(int x, int y) => new(x * 24 + 12 - G.CamX, y * 24 + 12 - G.CamY); void Put(int x, int y) { Ui.FakeMouse = Scr2(x, y); Steps(1); Ui.FakeMouse = Scr2(x, y); Ui.FakeRight = true; Steps(1); Steps(8); } void Wheel(int k) { for (int i = 0; i < Math.Abs(k); i++) { Ui.FakeWheel = -Math.Sign(k); Steps(1); Steps(7); } } Wheel(1); Console.WriteLine("hand after one scroll: " + Items.Name(d.Slots[d.Hand].Id)); Save("s_bar1"); for (int y = 1; y <= 4; y++) { Put(px + 3, py - y); } Wheel(1); for (int x = 3; x >= -1; x--) Put(px + x, py - 5); Console.WriteLine($"placed {d.Placed}, chalk left {Bag.Count(d, (int)T.Chalk)}, red left {Bag.Count(d, (int)T.RedRock)}"); Wheel(1); Put(px + 2, py - 1); Steps(20); Ui.FakeMouse = Scr2(px - 2, py - 1); Steps(20); Save("s_build"); Put(px, py - 1); Console.WriteLine("cannot place inside yourself: " + (G.W.At(px, py - 1) == T.Air)); Put(px + 30, py - 1); G.Env.Clock = .93f; Steps(60); Save("s_night"); G.Env.Clock = .45f; Ui.FakeWheel = -1; Steps(3); Save("s_scroll0"); Steps(3); Save("s_scroll1"); Steps(14); Save("s_scroll2"); Wheel(-9); Save("s_wrap"); return;
        }
        if (n == "bar")
        {
            // every list size: scroll through and back, checking nothing jumps, and that it is perfectly still afterwards
            At(G.W.SafeSpawn(), .45f); Steps(20); foreach (int count in new[] { 0, 1, 2, 3, 5, 7, 8, 9, 14 }) { var d = Store.Data; Array.Clear(d.Slots); d.Hand = -1; for (int i = 0; i < count; i++) d.Slots[i] = new(1 + i, 20); Steps(60); float worst = 0, still = 0; var last = G.BarForTest(); void Track(ref float m) { var now = G.BarForTest(); foreach (var (off, slot) in now) foreach (var (o2, s2) in last) if (s2 == slot && MathF.Abs(o2) < 3 && MathF.Abs(off) < 3) m = MathF.Max(m, MathF.Abs(off - o2)); last = now; } foreach (int dir in new[] { -1, 1 }) for (int k = 0; k < count + 3; k++) { bool wraps = count + 1 < 9 && false; Ui.FakeWheel = dir; for (int f = 0; f < 14; f++) { Steps(1); Track(ref worst); } } Steps(60); last = G.BarForTest(); for (int f = 0; f < 120; f++) { Steps(1); Track(ref still); } var h = d.Hand; if (count is 2 or 5) { d.Hand = count - 1; Steps(60); last = G.BarForTest(); d.Slots[0] = default; float gone = 0; for (int f = 0; f < 60; f++) { Steps(1); Track(ref gone); } Console.WriteLine($"   losing an item above the selection moves the selection by {gone:0.00}"); } Console.WriteLine($"{count} items: largest move in one frame {worst:0.00} rows, drift at rest {still:0.0000}"); if (count == 2) Save("bar_2"); if (count == 7) Save("bar_7"); }
            return;
        }
        if (n == "bag")
        {
            At(G.FindBiome(Biome.Amber).Value, .66f); var d = Store.Data; for (int i = 1; i < Blocks.All.Length; i++) if (Items.Valid(i)) Bag.Add(d, i, (i * 37) % 120 + 1); Steps(60); Key(Act.Bag, "i_open"); float gw = Bag.Cols * 31 - 2, gh = Bag.Rows * 31 - 2, W2 = 104 + gw + 62, H2 = gh + 92, x = MathF.Round((Ui.VW - W2) / 2), y = MathF.Round((Ui.VH - H2) / 2), gx = x + 21 + 104 + 18, gy = y + 40; Vector2 Sl(int i) => new(gx + i % 12 * 31 + 14, gy + i / 12 * 31 + 14); Ui.FakeMouse = Sl(5); Steps(30); Save("i_hover"); var a0 = d.Slots[0]; ClickAt(Sl(0).X, Sl(0).Y); Steps(30); Console.WriteLine("picked up: " + d.Slots[0].Empty); Ui.FakeMouse = Sl(60); Steps(20); Save("i_held"); ClickAt(Sl(60).X, Sl(60).Y); Console.WriteLine("moved: " + (d.Slots[60].Id == a0.Id && d.Slots[60].N == a0.N)); int n2 = d.Slots[2].N; Ui.FakeMouse = Sl(2); Ui.FakeRight = true; Steps(1); Steps(1); ClickAt(Sl(61).X, Sl(61).Y); Console.WriteLine($"split {n2} into {d.Slots[2].N} + {d.Slots[61].N}"); Steps(30); ClickAt(Sl(4).X, Sl(4).Y); ClickAt(Sl(4).X, Sl(4).Y); Steps(10); Console.WriteLine("double-click holds: " + (d.Hand == 4)); Steps(30); Save("i_hand"); int tot = d.Slots.Sum(s => s.N); ClickAt(Sl(7).X, Sl(7).Y); ClickAt(8, 8); Steps(10); Console.WriteLine("thrown out: " + (d.Slots.Sum(s => s.N) < tot) + " panel open " + G.MenuOpen); Steps(40); ClickAt(gx + gw - 64, gy + gh + 17); Steps(20); Save("i_sorted"); Ui.FakeMouse = Sl(0); Ui.FakeClick = true; Steps(1); Ui.FakeHold = true; Ui.FakeMouse = Sl(70); Steps(5); Ui.FakeHold = false; Ui.FakeRelease = true; Steps(2); Console.WriteLine("drag and drop: " + (!d.Slots[70].Empty)); Key(Act.Bag); Console.WriteLine("closed: " + !G.MenuOpen); Key(Act.Map, "i_map"); Key(Act.Menu); Key(Act.Menu, "i_menu"); Key(Act.Menu); In.Force.Add(Act.Emote); Steps(30); Save("i_emote"); In.Force.Clear(); G.ToggleAdminForTest(); ClickAt(70 + 12 + 17 + 3 * 33, 16 + 35); Steps(30); Save("i_admin"); ClickAt(70 + 12 + 17 + 4 * 33, 16 + 35); Steps(30); Save("i_admin2"); return;
        }
        if (n == "water")
        {
            var pl = G.W.Pools.First(p => p.Kind == Water.Lake); int x = pl.X0 - 1; At(new(x * 24 - 30, G.W.Top[x - 2] * 24), .45f); Steps(60); Save("wa_0"); for (int k = 0; k < 6; k++) for (int q = 0; q < 5; q++) G.W.Set(x - q, pl.Row + k, T.Air); Steps(30); Save("wa_1"); Steps(200); Save("wa_2"); Console.WriteLine("flooded " + pl.Flooded); if (G.W.Falls.Count > 0) { var f = G.W.Falls[0]; Shot("wa_top", new(f.x * 24 - 240, G.W.Top0[f.x - 10] * 24), .42f); Shot("wa_fall", new(f.x * 24 + 200, f.y1 * 24 - 4), .4f); Shot("wa_behind", new(f.x * 24 - 40, f.y1 * 24), .42f); } return;
        }
        if (n == "tree") { var p = G.W.Props.First(q => q.Kind == 1 && q.S.StartsWith("oak")); At(new(p.X - 70, G.W.Top[(int)((p.X - 70) / 24)] * 24), .45f); Steps(60); G.W.Set(p.GX, p.GY, T.Air); for (int i = 0; i < 6; i++) { Steps(14); Save("t_" + i); } return; }
        if (n == "gpu")
        {
            unsafe void Sync() { byte* px = Rlgl.ReadScreenPixels(1, 1); Raylib.MemFree(px); } // waits until the graphics card has really finished
            foreach (var b in Enum.GetValues<Biome>()) { var p = G.FindBiome(b); if (p == null) continue; At(p.Value, .5f); for (int i = 0; i < 140; i++) { Steps(1); Sync(); } double worst = 0; var all = Stopwatch.StartNew(); int slow = 0; In.Force.Add(Act.Right); for (int i = 0; i < 180; i++) { var sw = Stopwatch.StartNew(); Steps(1); Sync(); double f = sw.Elapsed.TotalMilliseconds; worst = Math.Max(worst, f); if (f > 16.6) slow++; } In.Force.Clear(); Console.WriteLine($"{b,-11} {all.Elapsed.TotalMilliseconds / 180:0.0} ms (worst {worst:0}, over budget {slow})"); } return;
        }
        if (n == "dig") { At(G.W.SafeSpawn(), .5f); Steps(30); var sw = Stopwatch.StartNew(); int x = (int)(G.P.P.X / 24), y = G.W.Top[x]; for (int i = 0; i < 100; i++) G.W.Set(x + i % 10, y + i / 10, T.Air); Console.WriteLine($"one block change: {sw.Elapsed.TotalMilliseconds / 100:0.00} ms"); sw.Restart(); G.Flush(); Console.WriteLine($"flush {sw.Elapsed.TotalMilliseconds:0.0} ms, {Store.Data.Edits.Length} chars"); return; }
        if (n == "gait")
        {
            foreach (var (nm, rn) in new[] { ("walk", false), ("run", true) }) { At(G.W.SafeSpawn(), .5f); In.Force.Clear(); Steps(40); In.Force.Add(Act.Right); if (rn) In.Force.Add(Act.Run); Steps(60); float x0 = G.P.P.X, mn = 999, mx = 0; for (int i = 0; i < 120; i++) { Steps(1); mn = MathF.Min(mn, G.P.V.X); mx = MathF.Max(mx, G.P.V.X); } Console.WriteLine($"{nm}: average {(G.P.P.X - x0) / 2:0}, {mn:0} to {mx:0}"); } In.Force.Clear(); return;
        }
        if (n == "move" || n == "back")
        {
            int dir = n == "move" ? 1 : -1; At(Ground(dir > 0 ? 180 : World.W - 180), .4f); Steps(20); float last = 0; int stuck = 0, skips = 0; In.Force.Add(dir > 0 ? Act.Right : Act.Left); In.Force.Add(Act.Run); for (int i = 0; i < 60 * 700 && (dir > 0 ? G.P.P.X < (World.W - 180) * 24 : G.P.P.X > 180 * 24); i++) { Steps(1); if (i % 20 == 0) { if (MathF.Abs(G.P.P.X - last) < 6) { stuck++; In.ForcePress.Add(Act.Jump); In.Force.Add(Act.Jump); if (stuck > 5) In.Force.Add(Act.Up); if (stuck == 4) Console.WriteLine($"slow near tile {G.P.P.X / 24:0} ({G.W.BiomeAt(G.P.P.X / 24)}) y {G.P.P.Y / 24:0} swim {G.P.Swim} climb {G.P.Climb}"); if (stuck > 34) { skips++; Console.WriteLine($"SKIP at {G.P.P.X / 24:0} {G.W.BiomeAt(G.P.P.X / 24)}"); G.P.P = Ground((int)(G.P.P.X / 24) + dir * 4) + new Vector2(0, -2); stuck = 0; } } else { stuck = 0; In.Force.Remove(Act.Jump); In.Force.Remove(Act.Up); } last = G.P.P.X; } else In.ForcePress.Clear(); } Console.WriteLine($"finished at {G.P.P.X / 24:0}, skips {skips}"); In.Force.Clear(); return;
        }
        if (n == "sky") { At(G.W.SafeSpawn(), .95f); G.Env.TimeScale = 0; Steps(60); G.Env.MeteorT = 40; Steps(220); for (int i = 0; i < 3; i++) { Steps(12); Save("m_" + i); } G.Env.MeteorT = 0; G.Env.BoltT = 0; G.Env.BoltX = .4f; G.Env.SetWeather(Weather.Storm, true); Steps(3); Save("m_bolt"); return; }
        if (n == "title") { G.OpenTitle(); Program.Screen = Scr.Title; Steps(200); Save("ti_0"); return; }
    }
    static void Finish(Painter big, string path) { for (int i = 0; i < big.Px.Length; i++) if (big.Px[i].A == 0) big.Px[i] = new Color(168, 150, 128, 255); big.Export(path); }
    static void Sheet(string path)
    {
        var big = new Painter(1500, 760); var ap = new AvatarPainter(); var r = new Rng(5); var one = new Painter(230, 360);
        for (int i = 0; i < 6; i++) { one.Clear(); var d = AvatarData.Random(r); ap.Draw(one, d, Pose.Stand, 2.8f, 115, 340, outlinePx: 2); Blit(one, big, 10 + i * 245, 10); }
        var sm = new Painter(80, 110); for (int i = 0; i < 16; i++) { sm.Clear(); var d = AvatarData.Random(r); ap.Draw(sm, d, Pose.Stand, .76f, 40, 100, outlinePx: 1); Blit(sm, big, 10 + (i % 8) * 185, 380 + (i / 8) * 185, 2); }
        Finish(big, path);
    }
    static void WalkSheet(string path)
    {
        var big = new Painter(1440, 4 * 180); var ap = new AvatarPainter(); var sm = new Painter(90, 90); var r = new Rng(21);
        for (int row = 0; row < 4; row++)
        {
            var d = AvatarData.Random(r); var an = new AvatarAnim { Dir = row % 2 == 0 ? 1 : -1 }; bool run = row >= 2; an.Stride = AvatarPainter.StrideOf(d); an.Vel = new(an.Dir * (run ? 138 : 64), 0); an.Set(run ? Anim.Run : Anim.Walk); for (int k = 0; k < 60; k++) an.Update(1 / 60f);
            for (int i = 0; i < 8; i++) { int g = 0; while (!an.Update(1 / 60f) && g++ < 20) { } sm.Clear(); ap.Draw(sm, d, an.Pose, AvatarAnim.PxPerUnit, 45, 86, outlinePx: 1); Blit(sm, big, i * 180, row * 180, 2); }
        }
        Finish(big, path);
    }
    static void SlideSheet(string path)
    {
        var big = new Painter(1500, 3 * 250); var ap = new AvatarPainter(); var one = new Painter(375, 250); var r = new Rng(8);
        for (int i = 0; i < 12; i++) { var d = i == 0 || i == 4 ? new AvatarData() : AvatarData.Random(r); if (i == 2) d[Slot.Bottom].Id = 66; if (i == 5) d[Slot.Bottom].Id = 61; if (i == 3) d[Slot.Jacket].Id = 7; if (i == 6) { d.Weight = .95f; d.Height = .1f; } if (i == 7) d.Height = 1; var an = new AvatarAnim { Dir = i % 2 == 0 ? 1 : -1 }; if (i == 4) an.Dir = -1; an.Set(i >= 8 ? Anim.Run : Anim.Slide); an.Vel = new(an.Dir * 200, 0); for (int k = 0; k < 40; k++) an.Update(1 / 60f); if (i >= 8) { an.Set(Anim.Slide); int q = 0; for (int k = 0; k < (i - 7) * 1; k++) { q = 0; while (!an.Update(1 / 60f) && q++ < 20) { } } } one.Clear(); ap.Draw(one, d, an.Pose, 2.6f, 187, 215, outlinePx: 2); for (int x = 0; x < 375; x++) one.Px[217 * 375 + x] = new Color(90, 130, 70, 255); Blit(one, big, (i % 4) * 375, (i / 4) * 250); }
        Finish(big, path);
    }
    static void PoseSheet(string path)
    {
        (string n, Anim a, Vector2 v, int dir, float wait)[] L = [("crouch", Anim.Crouch, default, 1, .6f), ("cwR", Anim.CrouchWalk, new(38, 0), 1, .5f), ("cwL", Anim.CrouchWalk, new(-38, 0), -1, .7f), ("sit", Anim.Sit, default, 1, .6f), ("sitg", Anim.SitGround, default, 1, .6f), ("nap", Anim.Sleep, default, 1, .6f), ("bow", Anim.Bow, default, 1, .9f), ("land", Anim.Land, default, 1, .4f), ("jU", Anim.Jump, new(0, -320), 1, .4f), ("jA", Anim.Jump, new(0, 0), 1, .4f), ("jF", Anim.Fall, new(0, 300), 1, .4f), ("jrU", Anim.Jump, new(130, -320), 1, .4f), ("jrA", Anim.Jump, new(130, 0), 1, .4f), ("jrF", Anim.Fall, new(130, 300), 1, .4f), ("jlU", Anim.Jump, new(-130, -320), -1, .4f), ("jlF", Anim.Fall, new(-130, 300), -1, .4f), ("c1", Anim.Climb, new(0, -92), 1, .35f), ("c2", Anim.Climb, new(0, -92), 1, .6f), ("c3", Anim.Climb, new(0, -92), 1, .85f), ("ledge", Anim.Ledge, default, 1, .3f), ("wave", Anim.Wave, default, 1, .8f), ("slide", Anim.Slide, new(150, 0), 1, .3f)];
        var big = new Painter(8 * 190, 3 * 330); var ap = new AvatarPainter(); var one = new Painter(190, 330); var r = new Rng(4);
        for (int i = 0; i < L.Length; i++) { var d = i >= 16 && i < 20 ? AvatarData.Random(r) : new AvatarData(); if (i == 16) { d = new AvatarData(); d[Slot.Top].Id = 13; } if (i == 17) { d[Slot.Hat].Id = 0; d.Hair.Id = 19; d[Slot.Jacket].Id = 17; } if (i == 5) d[Slot.Bottom].Id = 30; var an = new AvatarAnim { Dir = L[i].dir }; an.Set(L[i].a); an.Vel = L[i].v; for (int k = 0; k < (int)(L[i].wait * 60); k++) an.Update(1 / 60f); one.Clear(); ap.Draw(one, d, an.Pose, 2.5f, 95, 318 - (L[i].a is Anim.Jump or Anim.Fall ? 30 : 0), outlinePx: 2); Blit(one, big, (i % 8) * 190, (i / 8) * 330); }
        Finish(big, path);
    }
    static void WindSheet(string path)
    {
        int[] hair = [2, 19, 16, 9, 26, 34, 21, 3]; int[] jk = [0, 0, 1, 7, 23, 0, 4, 17]; int[] bt = [0, 2, 4, 12, 18, 65, 24, 39]; var big = new Painter(1520, 3 * 330); var ap = new AvatarPainter(); var one = new Painter(190, 330);
        for (int c = 0; c < 8; c++)
        {
            var d = new AvatarData(); d.Hair.Id = hair[c]; d[Slot.Jacket].Id = jk[c]; d[Slot.Bottom].Id = bt[c]; d[Slot.Top].Id = c == 1 ? 10 : c == 5 ? 2 : c; d.Hair.Base = AvatarData.HairCols[c + 2]; var an = new AvatarAnim { Wind = 2.3f }; an.Set(Anim.Idle); for (int k = 0; k < 90; k++) an.Update(1 / 60f);
            for (int row = 0; row < 3; row++) { if (row == 0) { var calm = new AvatarAnim(); for (int k = 0; k < 60; k++) calm.Update(1 / 60f); one.Clear(); ap.Draw(one, d, calm.Pose, 2.5f, 95, 318, outlinePx: 2); } else { for (int k = 0; k < 31; k++) an.Update(1 / 60f); one.Clear(); ap.Draw(one, d, an.Pose, 2.5f, 95, 318, outlinePx: 2); } Blit(one, big, c * 190, row * 330); }
        }
        Finish(big, path);
    }
    static void WaveSheet(string path)
    {
        var big = new Painter(1500, 2 * 340); var ap = new AvatarPainter(); var one = new Painter(250, 340); var an = new AvatarAnim(); var d = new AvatarData(); an.Set(Anim.Wave);
        for (int i = 0; i < 6; i++) { for (int g = 0; g < 3; g++) { int q = 0; while (!an.Update(1 / 60f) && q++ < 20) { } } one.Clear(); ap.Draw(one, d, an.Pose, 2.62f, 110, 326, outlinePx: 2); Blit(one, big, i * 250, 0); }
        for (int i = 0; i < 6; i++) { d.Mouth = new[] { 0, 1, 4, 9, 5, 8 }[i]; d.Skin = AvatarData.SkinTones[i * 2 + 1]; one.Clear(); ap.Draw(one, d, Pose.Stand with { Mouth = i == 5 ? .8f : 0 }, 7.5f, 125, 800, outlinePx: 2); Blit(one, big, i * 250, 340); }
        Finish(big, path);
    }
    static void HairBig(string path, int mode) { int first = Catalog.BaldHair + 1, from = mode % 2 * 25; var big = new Painter(5 * 290, 5 * 290); var ap = new AvatarPainter(); var one = new Painter(290, 290); for (int i = 0; i < 25; i++) { int k = first + from + i; if (k >= Catalog.Hair.Length) break; one.Clear(); var d = new AvatarData(); d.Hair.Id = k; d.Hair.Base = new[] { 0x432a1c, 0xc08d4f, 0xb5502a, 0x1c1618, 0xd8d6d2 }[i % 5]; if (i % 4 == 2) d.Skin = 0x9a6038; var po = Pose.Stand; if (mode / 2 == 1) d[Slot.Hat].Id = 1 + i % 6; if (mode / 2 == 2) po.Back = true; ap.Draw(one, d, po, 5.6f, 145, 630, outlinePx: 2); Blit(one, big, i % 5 * 290, i / 5 * 290); } Finish(big, path); }
    /// Only the newest cuts, large, with their length showing.
    static void HairSheet(string path) { int first = Catalog.BaldHair + 1, n = Catalog.Hair.Length - first; var big = new Painter(10 * 190, (n + 9) / 10 * 250); var ap = new AvatarPainter(); var one = new Painter(190, 250); for (int i = 0; i < n; i++) { one.Clear(); var d = new AvatarData(); d.Hair.Id = first + i; d.Hair.Base = AvatarData.HairCols[(i * 5 + i / 10) % 14]; if (i % 4 == 2) d.Skin = 0x9a6038; float L = Catalog.Hair[first + i].BackLen; ap.Draw(one, d, Pose.Stand, L > 2.8f ? 2.4f : 3.2f, 95, L > 2.8f ? 290 : 360, outlinePx: 2); Blit(one, big, i % 10 * 190, i / 10 * 250); } Finish(big, path); Console.WriteLine(n + " new cuts: " + string.Join(", ", Catalog.Hair.Skip(first).Select((h, i) => i + " " + h.Name))); }
    /// Close-ups of the face options: 0 sculpting, 1 skin, 2 makeup and piercings, 3 beards and hairlines, 4 tattoos.
    static void DetailSheet(string path, int page)
    {
        var list = new List<AvatarData>(); AvatarData B() { var d = new AvatarData(); d.Hair.Id = 1; d[Slot.Top].Id = 0; return d; }
        switch (page)
        {
            case 0: foreach (var f in new Action<AvatarData, float>[] { (d, v) => d.FaceLength = v, (d, v) => d.Jaw = v, (d, v) => d.Chin = v, (d, v) => d.Cheeks = v, (d, v) => d.EyeSize = v, (d, v) => d.EyeGap = v, (d, v) => d.EyeHeight = v, (d, v) => d.BrowHeight = v, (d, v) => d.NoseSize = v, (d, v) => d.NoseWidth = v, (d, v) => d.MouthWidth = v, (d, v) => d.LipSize = v, (d, v) => d.EarSize = v }) foreach (float v in new[] { 0f, 1f }) { var d = B(); f(d, v); list.Add(d); } break;
            case 1: { var a = B(); a.Freckles = 1; list.Add(a); a = B(); a.Wrinkles = 1; a.LaughLines = 1; list.Add(a); a = B(); a.UnderEyes = 1; list.Add(a); a = B(); a.Vitiligo = .6f; a.Skin = 0x7e4b30; list.Add(a); for (int i = 1; i < Catalog.Scars.Length; i++) { a = B(); a.Scar = i; list.Add(a); } for (int i = 1; i < Catalog.Birthmarks.Length; i++) { a = B(); a.Birthmark = i; list.Add(a); } a = B(); a.Moles = [.5f, .3f, -.3f, .6f, .2f, -.5f]; list.Add(a); break; }
            case 2: for (int i = 1; i < Catalog.Liners.Length; i++) { var a = B(); a.Liner = i; a.Shadow = i * .18f; a.ShadowCol = 0x9c4a7a; a.Lipstick = i * .2f; a.Blush = .5f; list.Add(a); } for (int i = 0; i < Catalog.Piercings.Length; i++) { var a = B(); a.Piercings = 1 << i; a.Metal = i % 2 == 0 ? 0xc8ccd2 : 0xe0b650; list.Add(a); } break;
            case 3: for (int i = 1; i < Catalog.Beards.Length; i++) { var a = B(); a[Slot.Beard].Id = i; a[Slot.Beard].A = 0x432a1c; if (i % 4 == 0) a.BeardGrey = .6f; if (i % 3 == 0) a.BeardLen = 1; list.Add(a); } for (int i = 0; i < Catalog.Hairlines.Length; i++) { var a = B(); a.Hair.Id = 7; a.Hair.Hairline = i; list.Add(a); } { var a = B(); a.Hair.Id = 22; a.Hair.Curl = 1; list.Add(a); a = B(); a.Hair.Id = 22; a.Hair.Length = 0; list.Add(a); a = B(); a.Hair.Id = 13; a.Hair.Flip = true; list.Add(a); a = B(); a.BrowCol = 0xd8d6d2; list.Add(a); } break;
            default: for (int i = 1; i < Catalog.TattooDesigns.Length; i++) { var a = B(); a[Slot.Top].Id = 32 < Catalog.Tops.Length ? 0 : 0; a.Tattoos = [i, i, 0, 0, 0, 0, 0]; a.Validate(); a.TatColor = i % 2 == 0; list.Add(a); } break;
        }
        bool full = page == 4; int cw = full ? 170 : 160, ch = full ? 330 : 170, cols = 9; var big = new Painter(cols * cw, (list.Count + cols - 1) / cols * ch); var ap = new AvatarPainter(); var one = new Painter(cw, ch);
        for (int i = 0; i < list.Count; i++) { one.Clear(); if (full) ap.Draw(one, list[i], Pose.Stand with { ArmL0 = -.5f, ArmL1 = -1.2f, ArmR0 = .5f, ArmR1 = 1.2f }, 2.6f, cw / 2, 318, outlinePx: 2); else ap.Draw(one, list[i], Pose.Stand, 6.2f, cw / 2, 630, outlinePx: 2); Blit(one, big, i % cols * cw, i / cols * ch); }
        Finish(big, path);
    }
    static void TatBig(string path)
    {
        int n = Catalog.TattooDesigns.Length - 1, cw = 200, ch = 180; var big = new Painter(9 * cw, 4 * ch); var one = new Painter(96, 88);
        for (int i = 0; i < n * 2; i++) { int id = i % n + 1; bool col = i >= n; one.Clear(); EditorScreen.TatThumb(one, id, 0xe6b48e, 0x2a2c38, col); Blit(one, big, i % 9 * cw + 4, i / 9 * ch + 2, 2); }
        Finish(big, path);
    }
    static int TankTop() { for (int i = 0; i < Catalog.Tops.Length; i++) if (Catalog.Tops[i].Name.Contains("Tank")) return i; return 0; }
    static void TatPeople(string path)
    {
        var big = new Painter(2 * 520, 760); var ap = new AvatarPainter(); var one = new Painter(520, 760);
        for (int i = 0; i < 2; i++)
        {
            var d = new AvatarData(); d.Hair.Id = i == 0 ? 1 : 19; d[Slot.Top].Id = 0; d.Skin = i == 0 ? 0xe8b896 : 0xb07a52; d.TatColor = i == 1;
            d.Tattoos = i == 0 ? [1, 10, 0, 0, 0, 0, 0] : [11, 16, 0, 0, 0, 0, 0]; d[Slot.Top].Id = i == 0 ? 0 : TankTop(); d.Validate();
            one.Clear(); ap.Draw(one, d, Pose.Stand with { ArmL0 = -.5f, ArmL1 = -1.2f, ArmR0 = .5f, ArmR1 = 1.2f }, 4.6f, 260, 750, outlinePx: 2); Blit(one, big, i * 520, 0); var sm = new Painter(110, 120); ap.Draw(sm, d, Pose.Stand with { ArmL0 = -.5f, ArmL1 = -1.2f, ArmR0 = .5f, ArmR1 = 1.2f }, AvatarAnim.PxPerUnit, 55, 115, outlinePx: 1); Blit(sm, big, i * 520 + 10, 10, 3);
        }
        Finish(big, path);
    }
    static void GlassSheet(string path) { int n = Catalog.GlassDefs.Length - 1; var big = new Painter(7 * 200, (n + 6) / 7 * 170); var ap = new AvatarPainter(); var one = new Painter(200, 170); int[] fr = [0x2b2a30, 0x8a5a38, 0xc99a4b, 0xb9b2a8, 0x8c2f35]; for (int i = 0; i < n; i++) { one.Clear(); var d = new AvatarData(); d[Slot.Glasses].Id = i + 1; d[Slot.Glasses].A = fr[i % 5]; d[Slot.Glasses].B = 0x9ec3cf; if (Catalog.GlassDefs[i + 1].Shade) d[Slot.Glasses].B = 0x3a3440; ap.Draw(one, d, Pose.Stand, 7.5f, 100, 760, outlinePx: 2); Blit(one, big, i % 7 * 200, i / 7 * 170); } Finish(big, path); }
    static void WristSheet(string path) { var big = new Painter(8 * 170, 2 * 330); var ap = new AvatarPainter(); var one = new Painter(170, 330); int k = 0; foreach (int top in new[] { 0, 9 }) foreach (int g in new[] { 4, 5, 6, 8 }) foreach (int jk in new[] { 0, 2 }) { if (jk == 2 && top == 9) continue; one.Clear(); var d = new AvatarData(); d[Slot.Top].Id = top; d[Slot.Jacket].Id = jk; d[Slot.Gloves].Id = g; d[Slot.Gloves].A = 0xc65a3a; d[Slot.Gloves].B = 0xe0b650; d[Slot.Gloves].C = 0x4a6285; d[Slot.Earrings].Id = 3; d.Hair.Id = 19; ap.Draw(one, d, Pose.Stand with { ArmR0 = .5f, ArmR1 = 1.3f }, 2.6f, 85, 318, outlinePx: 2); Blit(one, big, k % 8 * 170, k / 8 * 330); k++; } Finish(big, path); }
    static void Cat(string path, string what)
    {
        if (what == "hairnew") { HairSheet(path); return; } if (what == "glasses") { GlassSheet(path); return; } if (what.StartsWith("detail")) { DetailSheet(path, int.Parse(what[6..])); return; } if (what == "wrists") { WristSheet(path); return; } if (what == "tatbig") { TatBig(path); return; } if (what == "tatppl") { TatPeople(path); return; } if (what.StartsWith("hairbig")) { HairBig(path, int.Parse(what[7..])); return; } int n = what == "hair" ? Catalog.Hair.Length : what == "face" ? 24 : Catalog.Count(Enum.Parse<Slot>(what)); int cols = 10, cw = 150, ch = what is "Bottom" ? 190 : 170; var big = new Painter(cols * cw, ((n + cols - 1) / cols) * ch); var ap = new AvatarPainter(); var one = new Painter(cw, ch);
        for (int i = 0; i < n; i++)
        {
            one.Clear(); var d = new AvatarData(); float sc = 2.6f, gy = 300; if (what == "hair") { d.Hair.Id = i; d.Hair.Base = AvatarData.HairCols[i % 12]; sc = 3.2f; gy = 350; } else if (what == "face") { d.Eye = i % 12; d.Brow = i % 10; d.Nose = i % 8; d.Mouth = i % 10; d.Marks = i % 8; d.Skin = AvatarData.SkinTones[i % 14]; d[Slot.Beard].Id = i % 14; sc = 4.6f; gy = 470; }
            else { var s = Enum.Parse<Slot>(what); d[s].Id = i; if (s is Slot.Bottom or Slot.Shoes) { gy = ch - 10; if (s == Slot.Shoes) sc = 4; } if (s == Slot.Hat) { sc = 4f; gy = 430; } }
            ap.Draw(one, d, Pose.Stand, sc, cw / 2, gy, outlinePx: 2); Blit(one, big, (i % cols) * cw, (i / cols) * ch);
        }
        Finish(big, path);
    }
    public static void Blit(Painter a, Painter b, int ox, int oy, int z = 1) { for (int y = 0; y < a.H * z; y++) for (int x = 0; x < a.W * z; x++) { var c = a.Px[y / z * a.W + x / z]; int bx = ox + x, by = oy + y; if (c.A > 0 && bx < b.W && by < b.H && bx >= 0 && by >= 0) b.Px[by * b.W + bx] = c; } }
}

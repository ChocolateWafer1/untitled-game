using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
namespace UnnamedGame;

public enum Scr { Title, Editor, World }

public static class Program
{
    public static Scr Screen = Scr.Title; public static EditorScreen Editor; public static GameScreen Game; public static bool Quit; static bool editFromGame; static string shot; static string shotDir;
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
        Store.LoadAll(); Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags((dev ? ConfigFlags.HiddenWindow : ConfigFlags.ResizableWindow) | (Store.Cfg.Vsync && !dev ? ConfigFlags.VSyncHint : 0)); Raylib.InitWindow(1280, 720, "UNNAMED GAME"); Raylib.SetExitKey(KeyboardKey.Null); if (!dev) Raylib.SetTargetFPS(Store.Cfg.Vsync ? 0 : 144);
        if (!dev) ApplyWindow();
        Snd.Load(dev); Ui.Load(); Editor = new EditorScreen { Done = EditorDone }; Game = new GameScreen();
        Dev.Shots = shotDir;
        if (dev) { Dev.RunShot(shot); Raylib.CloseWindow(); return; }
        Start();
        while (!Raylib.WindowShouldClose() && !Quit) Frame(MathF.Min(Raylib.GetFrameTime(), .05f));
        Store.SaveCfg(); if (Store.Data.Created) Store.SaveGame(); Snd.Close(); Raylib.CloseWindow();
    }
    public static void StartForTest() => Start();
    static void Start() { if (Store.Data.Created) { Screen = Scr.Title; Game.OpenTitle(); iris = 0; IrisOpen(new(Ui.VW * .5f, 110)); } else OpenEditor(false); }
    static void OpenEditor(bool fromGame) { editFromGame = fromGame; Screen = Scr.Editor; Game.Title = false; Editor.Open(Store.Data.Avatar, fromGame); IrisOpen(new(Ui.VW * .27f, 190)); }
    public static void PlayFromTitle() => IrisClose(new(Ui.VW * .5f, 180), () => OpenEditor(false));
    public static void EditAvatar() => IrisClose(Game.ToScreen(Game.P.P + new Vector2(0, -36)), () => OpenEditor(true));
    public static void ToTitle() => IrisClose(new(Ui.VW / 2, Ui.VH / 2), () => { Screen = Scr.Title; Game.OpenTitle(); IrisOpen(new(Ui.VW * .5f, 110)); });
    public static void Warp(Action mid) => IrisClose(Game.ToScreen(Game.P.P + new Vector2(0, -36)), () => { mid(); IrisOpen(Game.ToScreen(Game.P.P + new Vector2(0, -36))); });
    static void EditorDone()
    {
        Store.Data.Avatar = Editor.D; Store.Data.Created = true; Store.SaveGame();
        IrisClose(Editor.AvatarCenter, () => { Screen = Scr.World; Game.Title = false; Game.Begin(!editFromGame || Game.P.P.X < 0); if (!editFromGame && Store.Data.X > 0) Game.LoadClock(); IrisOpen(Game.ToScreen(Game.P.P + new Vector2(0, -36)), 1.35f); });
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
        Ui.Begin(dt); bool busy = irisT < 1 || hold > 0; if (busy) { Ui.Click = Ui.Held = false; } In.Locked = busy;
        Snd.Update(dt); Voice.Update(dt);
        if (Screen == Scr.Editor) { Snd.Music("editor"); Editor.Update(dt); } else Game.Update(dt);
        Raylib.BeginDrawing(); Raylib.ClearBackground(Color.Black); if (Screen != Scr.Editor) Game.RenderWorld();
        if (Screen != Scr.Editor) { Ui.Push(); Game.DrawWorld(); Ui.Pop(); } Ui.Push(); if (Screen == Scr.Editor) Editor.Draw(); else Game.DrawUi(); Ui.End(); DrawIris(dt); Ui.Pop(); Raylib.EndDrawing();
    }
    public static void IrisClose(Vector2 c, Action mid, float dur = .85f) { irisC = c; irisFrom = 1; irisTo = 0; irisT = 0; irisDur = dur; irisMid = mid; Snd.Play("iris_in"); }
    public static void IrisOpen(Vector2 c, float dur = 1.25f) { irisC = c; irisFrom = 0; irisTo = 1; irisT = 0; irisDur = dur; irisMid = null; Snd.Play("iris_out"); }
    static void DrawIris(float dt)
    {
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
        switch (a[0]) { case "--sheet": Sheet(a[1]); return true; case "--walk": WalkSheet(a[1]); return true; case "--icon": { var pi = new Painter(64, 64); pi.View(1, 0, 0); pi.Begin().Ellipse(32, 32, 30, 30); pi.Shade(new Ink((x, y) => Col.Lerp(0xffd890, 0xe8804a, y / 64), 6, .5f, 1)); pi.Begin().Blob(new(2, 46), new(16, 40), new(32, 45), new(48, 39), new(62, 46), new(50, 60), new(32, 63), new(14, 60)); pi.IntersectAlpha(); pi.Shade(new Ink(0x4f9fb4, 4, .5f, .8f)); FishDex.Draw(pi, FishDex.All[3], .44f, 31, 31); pi.View(1, 0, 0); pi.Outline(0x33201f, 1); pi.Export(a[1]); return true; }
            case "--poses": PoseSheet(a[1]); return true;
            case "--wind": WindSheet(a[1]); return true;
            case "--wave": WaveSheet(a[1]); return true; case "--fish": FishSheet(a[1]); return true; case "--cat": Cat(a[1], a[2]); return true; }
        return false;
    }
    public static void Steps(int n, float dt = 1 / 60f) { for (int i = 0; i < n; i++) Program.Frame(dt); }
    public static void Save(string n) { var img = Raylib.LoadImageFromScreen(); if (img.Width != 1280) Raylib.ImageCrop(ref img, new Rectangle(0, img.Height - 720, 1280, 720)); Raylib.ExportImage(img, Path.Combine(Shots, n + ".png")); Raylib.UnloadImage(img); }
    public static void ClickAt(float x, float y) { Ui.FakeMouse = new(x, y); Steps(2); Ui.FakeMouse = new(x, y); Ui.FakeClick = true; Steps(1); Steps(2); }
    public static void RunShot(string n)
    {
        if (n == "audio") { Snd.Load(false); Program.Screen = Scr.Editor; Program.Editor.Open(new AvatarData(), false); foreach (var q in new[] { "click", "step_grass", "splash", "voicea", "catch", "thunder", "bird" }) { Snd.Play(q, .05f); Steps(10); Console.WriteLine(q + " " + Snd.Has(q)); } Snd.Close(); return; }
        if (n == "flow")
        {
            Program.StartForTest(); Ui.FakeMouse = new(5, 300); Steps(100); Save("l_editor0"); Program.Editor.SetNameForTest("Pip"); float px0 = Ui.VW - 336; Steps(1); ClickAt(px0 + 270, 6 + 348 - 24); Steps(40); Save("l_finish"); Steps(55); for (int i = 0; i < 7; i++) { Steps(9); Save("l_iris" + i); } for (int i = 0; i < 9; i++) { Steps(9); Save("l_open" + i); } Console.WriteLine("screen " + Program.Screen + " name " + Store.Data.Avatar.Name); In.ForcePress.Add(Act.Menu); Steps(1); In.ForcePress.Clear(); Steps(30); ClickAt(Ui.VW / 2, 180 - 107 + 8 + 16 + 31 + 13); Steps(150); Console.WriteLine("after edit avatar: " + Program.Screen); Save("l_edit"); return;
        }
        if (n != "editor") { RunWorld(n); return; }
        if (n == "editor")
        {
            var r = new Rng(12); Program.Screen = Scr.Editor; var d = AvatarData.Random(r); d.Name = "Juniper"; Program.Editor.Open(d, false); Ui.FakeMouse = new(5, 5); Steps(90); Save("ed_body");
            float px = Ui.VW - 336; for (int i = 1; i < 10; i++) { ClickAt(px - 10, 6 + 8 + i * 33 + 15); Steps(70); Save("ed_" + i); }
            ClickAt(px - 10, 6 + 8 + 5 * 33 + 15); Steps(10); ClickAt(px + 260, 50); Steps(60); Save("ed_skirts");
        }
    }
    public static void RunWorld(string n) { var G = Program.Game; Program.Screen = Scr.World; var r = new Rng(12); Store.Data.Avatar = AvatarData.Random(r); Store.Data.Avatar.Name = "Juniper"; Ui.FakeMouse = new(5, 300); Steps(2); Program.Screen = Scr.World; G.Title = false; G.Begin(true);
        void Shot(string nm, float tx, float clock, Weather w = Weather.Clear, float? y = null, int dir = 1, int warm = 70) { G.P.P = new(tx * 24, y ?? G.W.Top[(int)tx] * 24); G.P.V = default; G.P.Dir = dir; G.Env.Clock = clock; G.Env.SetWeather(w, true); G.CamX = G.P.P.X - Ui.VW / 2; G.CamY = G.P.P.Y - Ui.VH * .62f; Steps(warm); G.Env.Clock = clock; Steps(2); Save(nm); }
        if (n == "fish") { G.P.P = new(591 * 24, 94 * 24); Store.Data.Bait = 1; Store.Data.Baits[1] = 20; G.P.Dir = -1; G.Env.Clock = .66f; G.Env.SetWeather(Weather.Clear, true); G.CamX = G.P.P.X - 320; G.CamY = G.P.P.Y - 230; Steps(60); In.Force.Add(Act.Fish); In.ForcePress.Add(Act.Fish); Steps(1); In.ForcePress.Clear(); Steps(44); Save("f_aim"); In.Force.Clear(); Steps(90); Save("f_wait"); int guard = 0; while (G.Fish.S != FS.Bite && guard++ < 1500) Steps(1); Steps(12); Save("f_bite"); In.ForcePress.Add(Act.Fish); Steps(1); In.ForcePress.Clear(); Steps(30); guard = 0; bool saved = false; while (G.Fish.S == FS.Fight && guard++ < 3000) { In.Force.Clear(); if (G.Fish.Tension < .6f && G.Fish.Phase != 1) In.Force.Add(Act.Fish); if (G.Fish.FishP > G.Fish.NetP + .03f) In.Force.Add(Act.Right); else if (G.Fish.FishP < G.Fish.NetP - .03f) In.Force.Add(Act.Left); Steps(1); if (guard == 70) { Save("f_fight"); saved = true; } } In.Force.Clear(); In.ForcePress.Clear(); Console.WriteLine("fight result " + G.Fish.S + " after " + guard + " " + G.Fish.F?.Name + " saved " + saved); Steps(70); Save("f_caught"); return; }
        if (n == "admin") { G.P.P = new(1000 * 24, G.W.Top[1000] * 24); Steps(60); G.ToggleAdminForTest(); for (int i = 0; i < 5; i++) { ClickAt(8 + 12 + 17 + i * 33, 46 + 35); Steps(20); Save("a_" + i); } return; }
        if (n == "fishes")
        {
            var rb = new Rng(5); foreach (var (id, rod) in new[] { (5, 0), (6, 0), (8, 2), (9, 2), (9, 4) })
            {
                int wins = 0, tries = 4; float time = 0; for (int q = 0; q < tries; q++)
                {
                    G.Fish.Cancel(); G.P.P = new(591 * 24, 94 * 24); Store.Data.Rod = rod; G.P.Dir = -1; Store.Data.Fish.Clear(); G.Env.Clock = .6f; In.Force.Clear(); Steps(30); In.Force.Add(Act.Fish); In.ForcePress.Add(Act.Fish); Steps(1); In.ForcePress.Clear(); Steps(44); In.Force.Clear(); int gd = 0; G.Fish.NextFish = id; while (G.Fish.S != FS.Wait && gd++ < 200) Steps(1); G.Fish.ForceBite(id); Steps(10); In.ForcePress.Add(Act.Fish); Steps(1); In.ForcePress.Clear(); gd = 0; float seen = .5f; bool hold = true;
                    // reacts late (about a quarter second) and imprecisely, like a person
                    var lag = new Queue<(float f, float n, bool rel)>(); while (G.Fish.S == FS.Fight && gd++ < 60 * 90) { lag.Enqueue((G.Fish.FishP + rb.R(-.03f, .03f), G.Fish.NetP, G.Fish.NeedRelease)); var o = lag.Count > 15 ? lag.Dequeue() : lag.Peek(); In.Force.Clear(); seen = o.f; if (seen > o.n + .06f) In.Force.Add(Act.Right); else if (seen < o.n - .06f) In.Force.Add(Act.Left); hold = o.rel ? false : G.Fish.Tension < .3f ? true : hold; if (hold) In.Force.Add(Act.Fish); Steps(1); }
                    if (G.Fish.S == FS.Caught) wins++; time += gd / 60f;
                }
                Console.WriteLine($"{FishDex.All[id].Name} rod {rod}: {wins}/{tries} caught, {time / tries:0.0}s");
            }
            return;
        }
        if (n == "perf") { foreach (var tx in new[] { 760f, 380f, 500f, 1200f }) { G.P.P = new(tx * 24, G.W.Top[(int)tx] * 24); Steps(80); var sw = Stopwatch.StartNew(); Steps(120); Console.WriteLine($"{tx}: {sw.ElapsedMilliseconds / 120f:0.0} ms/frame"); } return; }
        if (n == "ui") { var d = Store.Data; for (int i = 0; i < 26; i++) { var f = FishDex.All[(i * 7) % 40]; var (kg, cm) = FishDex.Roll(f, r, 0); d.Fish.Add(new FishItem { Id = (i * 7) % 40, Kg = kg, Cm = cm }); d.Journal[(i * 7) % 40] = new FishRecord { Count = i + 1, BestKg = kg, BestCm = cm }; if (i % 3 == 0) d.Tank.Add(d.Fish[^1]); } d.Coins = 1234; G.P.P = new(820 * 24 + 30, 89 * 24); G.Env.Clock = .70f; Steps(60); void Key(Act a, string nm) { In.ForcePress.Add(a); Steps(1); In.ForcePress.Clear(); Steps(50); Save(nm); } Key(Act.Interact, "u_shop"); ClickAt(Ui.VW / 2 - 244 + 90, 22 + 38 + 30); Steps(30); Save("u_buy"); Key(Act.Menu, "x"); Key(Act.Bag, "u_bag"); Key(Act.Menu, "x"); Key(Act.Journal, "u_journal"); Key(Act.Menu, "x"); Key(Act.Map, "u_map"); Key(Act.Menu, "x"); Key(Act.Menu, "u_menu"); ClickAt(Ui.VW / 2, 180 - 107 + 8 + 16 + 62 + 13); Steps(40); Save("u_settings"); Key(Act.Menu, "x"); Key(Act.Menu, "x"); In.Force.Add(Act.Emote); Steps(30); Ui.FakeMouse = new(Ui.VW / 2 + 70, 110); Steps(20); Save("u_emote"); In.Force.Clear(); G.P.P = new(785 * 24, 89 * 24); Ui.FakeMouse = new(5, 300); Steps(20); Key(Act.Interact, "u_tank"); Key(Act.Menu, "x"); G.OpenTitle(); Program.Screen = Scr.Title; Steps(200); Save("u_title"); return; }
        if (n == "move") { G.P.P = new(150 * 24, G.W.Top[150] * 24); G.Env.Clock = .4f; float last = 0; int stuck = 0; In.Force.Add(Act.Right); In.Force.Add(Act.Run); for (int i = 0; i < 60 * 420 && G.P.P.X < 1430 * 24; i++) { Steps(1); if (i % 20 == 0) { if (MathF.Abs(G.P.P.X - last) < 6) { stuck++; In.ForcePress.Add(Act.Jump); In.Force.Add(Act.Jump); if (stuck == 3) Console.WriteLine($"stuck near tile {G.P.P.X / 24:0} y {G.P.P.Y / 24:0} swim {G.P.Swim} climb {G.P.Climb}"); if (stuck > 25) { Console.WriteLine("SKIP"); G.P.P.X += 72; G.P.P.Y = G.W.Top[(int)(G.P.P.X / 24)] * 24 - 4; stuck = 0; } } else { stuck = 0; In.Force.Remove(Act.Jump); } last = G.P.P.X; } else In.ForcePress.Clear(); } Console.WriteLine($"finished at {G.P.P.X / 24:0}"); In.Force.Clear(); return; }
        if (n == "back") { G.P.P = new(1400 * 24, G.W.Top[1400] * 24); float last = 0; int stuck = 0; In.Force.Add(Act.Left); In.Force.Add(Act.Run); for (int i = 0; i < 60 * 420 && G.P.P.X > 130 * 24; i++) { Steps(1); if (i % 20 == 0) { if (MathF.Abs(G.P.P.X - last) < 6) { stuck++; In.ForcePress.Add(Act.Jump); In.Force.Add(Act.Jump); if (stuck > 8 || (G.P.P.X < 480 * 24 && G.P.P.X > 462 * 24)) { In.Force.Add(Act.Up); In.Force.Remove(Act.Left); } if (stuck == 3) Console.WriteLine($"stuck near tile {G.P.P.X / 24:0} y {G.P.P.Y / 24:0} swim {G.P.Swim} climb {G.P.Climb} {G.P.An.State}"); if (stuck > 40) { Console.WriteLine("SKIP"); G.P.P.X -= 72; G.P.P.Y = G.W.Top[(int)(G.P.P.X / 24)] * 24 - 4; stuck = 0; } } else { if (!G.P.Climb) { In.Force.Remove(Act.Up); In.Force.Add(Act.Left); } stuck = 0; In.Force.Remove(Act.Jump); } last = G.P.P.X; } else In.ForcePress.Clear(); } Console.WriteLine($"finished at {G.P.P.X / 24:0}"); In.Force.Clear(); return; }
        if (n == "world") { Shot("w_village", 762, .5f); Shot("w_village2", 806, .66f); Shot("w_snow", 300, .42f, Weather.Snow, null, 1, 700); Shot("w_swim", 520, .45f, Weather.Clear, World.LakeY + 40); In.Force.Add(Act.Right); Steps(40); Save("w_swim2"); Steps(16); Save("w_swim3"); In.Force.Clear(); Shot("w_noon", 1000, .42f); Shot("w_cloudy", 1000, .4f, Weather.Cloudy); Shot("w_village_night", 752, .93f); Shot("w_autumn", 372, .68f, Weather.Breezy); Shot("w_river", 302, .31f); Shot("w_lake", 484, .5f, Weather.Clear, 93 * 24); Shot("w_lake2", 590, .74f); Shot("w_meadow", 880, .30f, Weather.Mist); Shot("w_high", 1200, .45f); Shot("w_cave", 1178, .5f, Weather.Clear, 105 * 24); Shot("w_beach", 1418, .27f); Shot("w_rain", 820, .55f, Weather.Rain); Shot("w_storm", 300, .6f, Weather.Storm); } }
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
    static void FishSheet(string path) { var big = new Painter(1440, 6 * 130); var one = new Painter(200, 130); for (int i = 0; i < FishDex.All.Length; i++) { one.Clear(); FishDex.Draw(one, FishDex.All[i], 1.3f, 100, 65); one.Outline(0x2a1a1c, 2); Blit(one, big, (i % 7) * 205, (i / 7) * 130); } Finish(big, path); }
    static void Cat(string path, string what)
    {
        int n = what == "hair" ? Catalog.Hair.Length : what == "face" ? 24 : Catalog.Count(Enum.Parse<Slot>(what)); int cols = 10, cw = 150, ch = what is "Bottom" ? 190 : 170; var big = new Painter(cols * cw, ((n + cols - 1) / cols) * ch); var ap = new AvatarPainter(); var one = new Painter(cw, ch);
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

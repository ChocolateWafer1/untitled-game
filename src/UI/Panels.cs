using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public enum Panel { None, Menu, Settings, Bag, Map }

public sealed partial class GameScreen
{
    Panel panel = Panel.None, settingsFrom = Panel.None; float openT; int setTab; bool emoteOpen; float emoteA; Act? rebinding; Texture2D map, mapBig; bool mapReady;
    void Open(Panel p) { panel = p; openT = 0; Snd.Play(p is Panel.Map ? "page" : "open"); if (p == Panel.Map && !mapReady) BuildMap(); }
    void ClosePanel()
    {
        if (panel == Panel.Settings) { Store.SaveCfg(); panel = settingsFrom; openT = settingsFrom == Panel.Menu ? .3f : 0; rebinding = null; Snd.Play("close", .7f); return; }
        if (panel is Panel.Bag) ReturnHeld(); panel = Panel.None; Snd.Play("close", .8f);
    }
    void Panels(float dt)
    {
        openT += dt; if (panel == Panel.None || rebinding != null) return;
        if (In.Press(Act.Menu) || In.PadBack || (panel == Panel.Bag && In.Press(Act.Bag)) || (panel == Panel.Map && In.Press(Act.Map))) ClosePanel();
    }
    float Pop => Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp(openT * 4.2f), 1.2f);
    readonly List<(string s, float t)> toasts = new(); public void Toast(string s) { if (toasts.Count > 3) toasts.RemoveAt(0); toasts.Add((s, 0)); }

    public void DrawUi()
    {
        float W = Ui.VW, H = Ui.VH; if (Title) { if (Program.Screen != Scr.Select) DrawTitle(); return; }
        if (panel == Panel.None) { if (!hideHud) Hud(); EmoteWheel(); if (admin) Admin(); return; }
        float dim = M.Clamp(openT * 6); Raylib.DrawRectangle(-2, -2, (int)W + 4, (int)H + 4, new Color(36, 18, 22, (int)(dim * 120)));
        float ps = MathF.Max(.01f, .82f + Pop * .18f); Ui.PushScale(W / 2, H / 2, ps, ps); switch (panel) { case Panel.Menu: Menu(); break; case Panel.Settings: SettingsUi(); break; case Panel.Bag: BagUi(); break; case Panel.Map: MapUi(); break; } Ui.PopScale(); Toasts();
    }
    void Toasts()
    {
        float W = Ui.VW; for (int i = toasts.Count - 1; i >= 0; i--) { var q = toasts[i]; q.t += Ui.DT; toasts[i] = q; if (q.t > 2.4f) { toasts.RemoveAt(i); continue; } float a = M.Clamp(q.t * 5) * M.Clamp((2.4f - q.t) * 2); float y = Ui.VH - 30 - (toasts.Count - 1 - i) * 18 - Ease.OutBack(M.Clamp(q.t * 4)) * 8; float w = Font.Width(q.s) + 16; Ui.Nine("tip", W / 2 - w / 2, y, w, 17, Raylib.Fade(Color.White, a)); Font.Draw(q.s, W / 2, y + 5, Raylib.Fade(Ui.Cream, a), 1, .5f); }
    }
    void Hud()
    {
        if (P.D == null) return;
        float W = Ui.VW, H = Ui.VH; float x = W - 112, y = 8; Ui.Nine("paper", x, y, 104, 28, Color.White); Ui.Icon(Env.WeatherIcon, x + 8, y + 6 + M.Sin(Ui.T * 1.5f) * 1, 1); Font.Draw(Env.TimeText, x + 29, y + 9, Ui.Ink, 1.25f); Font.Draw("Day " + Env.Day, x + 96, y + 11, Ui.Soft, .85f, 1);
        if (mapReady) { float mw = 104, mx = x, my = y + 30; Ui.Nine("dark", mx, my, mw, 30, Color.White); float span = 420, c = M.Clamp(P.P.X / 24 - span / 2, 0, World.W - span); Raylib.DrawTexturePro(map, new Rectangle(c, 0, span, map.Height), new Rectangle(mx + 4, my + 4, mw - 8, 22), Vector2.Zero, 0, Color.White); float px = mx + 4 + (P.P.X / 24 - c) / span * (mw - 8), py = my + 4 + M.Clamp((P.P.Y / 24 - MapY0) / MapYH) * 22; Raylib.DrawCircleV(new(px, py - 1), 2.6f, Ui.Ink); Raylib.DrawCircleV(new(px, py - 1), 1.8f, new Color(255, 110, 90, 255)); }
        Bar();
        if (Store.Data.Seconds < 60 && playT < 34) { float a = M.Clamp(playT - 2) * M.Clamp(34 - playT); string q = In.UsingPad ? "Stick walk   A jump   B run   RT dig   LT build   Right stick aim   LB/RB choose   Y satchel   X emotes" : In.Name(Act.Left) + "/" + In.Name(Act.Right) + " walk   " + In.Name(Act.Run) + " run   " + In.Name(Act.Jump) + " jump   Hold left click to dig   Right click to build   Scroll to choose   " + In.Name(Act.Bag) + " or E satchel"; float w = Font.Width(q) + 24; Ui.Nine("tip", W / 2 - w / 2, H - 34, w, 18, Raylib.Fade(Color.White, a)); Font.Draw(q, W / 2, H - 29, Raylib.Fade(Ui.Cream, a), 1, .5f); }
        if (areaT < 3.2f) { float k = M.Clamp(areaT * 3), a = k * M.Clamp((3.2f - areaT) * 1.4f), ay = 14 - (1 - Ease.OutCubic(k)) * 12; if (newArea) { float w = Font.Width(shownArea, 1.5f) + 36; Ui.Nine("paper", W / 2 - w / 2, ay, w, 28, Raylib.Fade(Color.White, a)); Font.Draw(shownArea, W / 2, ay + 7, Raylib.Fade(Ui.Ink, a), 1.5f, .5f); } else Font.Draw(shownArea, W / 2, 16, Raylib.Fade(Ui.Cream, a * .8f), 1, .5f, new Color(40, 24, 26, 160)); }
        Toasts(); var ns = ToScreen(P.P + new Vector2(0, -92)); if (!P.Mining && cursorA < .1f && Ui.Over(ns.X - 18, ns.Y, 36, 92)) Font.Draw(P.D.Name, ns.X, ns.Y - 6, Ui.Cream, 1, .5f, Ui.Ink);
    }
    static readonly (string n, string i, Anim a)[] Emotes = [("Wave", "wave", Anim.Wave), ("Dance", "dance", Anim.Dance), ("Laugh", "laugh", Anim.Laugh), ("Point", "point", Anim.Point), ("Cheer", "cheer", Anim.Cheer), ("Think", "think", Anim.Think), ("Sit", "sit", Anim.SitGround), ("Nap", "sleep", Anim.Sleep), ("Shrug", "shrug", Anim.Shrug), ("Bow", "bow", Anim.Bow)];
    void EmoteWheel()
    {
        bool hold = In.Down(Act.Emote) && P.Ground && !P.Swim; bool was = emoteOpen; emoteOpen = hold; emoteA = M.Damp(emoteA, hold ? 1 : 0, 16, Ui.DT); if (hold && !was) Snd.Play("open", .6f); if (emoteA < .02f) return; var c = new Vector2(Ui.VW / 2, Ui.VH / 2 - 10); var m = In.UsingPad ? (In.LStick != default ? In.LStick : In.RStick) * 60 : Ui.Mouse - c; int n = Emotes.Length, hot = -1; if (m.Length() > 22) { float an = MathF.Atan2(m.X, -m.Y); if (an < 0) an += M.TAU; hot = (int)MathF.Round(an / M.TAU * n) % n; }
        float R0 = 84 * Ease.OutBack(M.Clamp(emoteA)); Raylib.DrawCircleV(c, R0 + 26, new Color(40, 22, 26, (int)(130 * emoteA))); Raylib.DrawRing(c, R0 + 24, R0 + 27, 0, 360, 60, new Color(224, 166, 92, (int)(220 * emoteA)));
        for (int i = 0; i < n; i++) { float an = i * M.TAU / n; var p = c + new Vector2(M.Sin(an), -M.Cos(an)) * R0; var a = Ui.A("em_" + i); a.Hover.Update(i == hot ? 1 : 0, 400, 18, Ui.DT); if (i == hot && !a.Was) Snd.Play("hover", .5f, 1 + i * .05f); a.Was = i == hot; float s = 1 + a.Hover.X * .35f; Raylib.DrawCircleV(p, 17 * s, Ui.Ink); Raylib.DrawCircleV(p, 15.5f * s, i == hot ? Ui.Gold : Ui.Cream); Ui.Icon(Emotes[i].i, p.X - 9 * s, p.Y - 9 * s, 1.12f * s, emoteA); }
        Font.Draw(hot >= 0 ? Emotes[hot].n : "Emotes", c.X, c.Y - 8, Raylib.Fade(Ui.Cream, emoteA), 2, .5f, Ui.Ink); if ((was && !hold && hot >= 0) || (hold && Ui.Click && hot >= 0)) { P.DoEmote(Emotes[hot].a); if (Emotes[hot].a is Anim.Laugh or Anim.Cheer or Anim.Wave) Voice.Say(P.D.Voice, 4); Snd.Play("confirm", .4f); if (hold) { emoteOpen = false; emoteA = 0; } }
    }
    (float x, float y, float w, float h) Frame(float w, float h, string title, string icon = null)
    {
        float x = (Ui.VW - w) / 2, y = (Ui.VH - h) / 2; Ui.Nine("panel", x, y, w, h, Color.White); float tx = x + 16; if (icon != null) { Ui.Icon(icon, tx, y + 13); tx += 22; } Font.Draw(title, tx, y + 14, Ui.Ink, 2); if (Ui.Button("close", null, x + w - 36, y + 11, 22, 20, "btnR", "x")) ClosePanel(); return (x + 16, y + 38, w - 32, h - 52);
    }
    void Menu()
    {
        float w = 180, h = 215; float x = (Ui.VW - w) / 2, y = (Ui.VH - h) / 2 + 8; Ui.Nine("panel", x, y, w, h, Color.White); Font.Draw("Paused", Ui.VW / 2, y - 22 + M.Sin(Ui.T * 2) * 1.5f, Ui.Cream, 3, .5f, Ui.Ink, 1); Font.Draw("World seed " + W.Seed, Ui.VW / 2, y + h + 8, new Color(236, 214, 182, 255), 1, .5f, Ui.Ink);
        string[] l = ["Resume", "Edit Avatar", "Settings", "Back to Start", "Save and Exit", "Quit Game"]; string[] ic = ["check", "top", "gear", "home", "map", "x"]; string[] st = ["btnG", "btnB", "btnB", "btn", "btn", "btnR"];
        for (int i = 0; i < l.Length; i++) { float q = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp(openT * 4 - i * .22f)); Rlgl.PushMatrix(); Rlgl.Translatef((1 - q) * -60, 0, 0); if (Ui.Button("pm" + i, l[i], x + 18, y + 16 + i * 31, w - 36, 26, st[i], ic[i], text: 1.25f)) switch (i) { case 0: ClosePanel(); break; case 1: ClosePanel(); Program.EditAvatar(Store.Current, ToScreen(P.P + new Vector2(0, -36)), true); break; case 2: settingsFrom = Panel.Menu; panel = Panel.Settings; openT = 0; break; case 3: ClosePanel(); Program.Warp(() => { P.P = W.SafeSpawn(); P.V = default; P.Climb = false; CamX = P.P.X - Ui.VW / 2; CamY = P.P.Y - 34 - Ui.VH * .66f; }); break; case 4: Program.ToTitle(); break; case 5: Store.SaveGame(); Program.Quit = true; break; } Rlgl.PopMatrix(); }
    }
    void Row(string n, float x, float y) => Font.Draw(n, x, y, Ui.Ink);
    void SettingsUi()
    {
        var (x, y, w, h) = Frame(380, 290, "Settings", "gear"); var c = Store.Cfg; string[] t = ["Audio", "Video", "Controls", "Access"]; string[] ic = ["sound", "video", "keys", "eye"]; for (int i = 0; i < 4; i++) if (Ui.Button("st" + i, t[i], x + i * 87.5f, y, 84, 21, "btnP", ic[i], on: setTab == i)) { setTab = i; rebinding = null; }
        float yy = y + 34; void Sl(string n, float v, Action<float> set) { Row(n, x + 4, yy); float q = v; if (Ui.Slider("s_" + n, ref q, x + 116, yy, w - 160)) set(q); Font.Draw(((int)MathF.Round(v * 100)).ToString(), x + w - 6, yy, Ui.Soft, 1, 1); yy += 26; }
        void Tg(string n, bool v, Action<bool> set, string hint = null) { Row(n, x + 4, yy); bool q = v; if (Ui.Toggle("t_" + n, ref q, x + w - 34, yy - 3)) set(q); if (hint != null) Font.Draw(hint, x + 4, yy + 10, Ui.Soft, .85f); yy += hint != null ? 30 : 26; }
        void Ch(string n, int v, string[] o, Action<int> set) { Row(n, x + 4, yy); float bw = 70; for (int i = 0; i < o.Length; i++) if (Ui.Button("c_" + n + i, o[i], x + w - (o.Length - i) * (bw + 3), yy - 6, bw, 19, "btnP", on: v == i)) set(i); yy += 28; }
        switch (setTab)
        {
            case 0: Sl("Master volume", c.Master, v => c.Master = v); Sl("Music", c.Music, v => c.Music = v); Sl("Sound effects", c.Sfx, v => c.Sfx = v); Sl("Ambience", c.Ambience, v => c.Ambience = v); Sl("Interface", c.Ui, v => c.Ui = v); break;
            case 1: Ch("Window (F11)", c.Window, ["Borderless", "Windowed"], v => { c.Window = v; Program.ApplyWindow(); }); Ch("Lighting", c.Light, ["Low", "Medium", "High"], v => c.Light = v); Tg("Pixel-perfect scrolling", c.PixelPerfect, v => c.PixelPerfect = v); Tg("VSync", c.Vsync, v => { c.Vsync = v; if (v) { Raylib.SetWindowState(ConfigFlags.VSyncHint); Raylib.SetTargetFPS(0); } else { Raylib.ClearWindowState(ConfigFlags.VSyncHint); Raylib.SetTargetFPS(144); } }); Sl("Screen shake", c.Shake, v => c.Shake = v); break;
            case 2:
                {
                    int i = 0; foreach (Act a in Enum.GetValues<Act>()) { if (a == Act.Menu) continue; float bx = x + (i % 2) * 178, by = yy - 4 + (i / 2) * 26; Row(a switch { Act.Bag => "Inventory", Act.Emote => "Emotes", _ => a.ToString() }, bx + 4, by + 6); bool on = rebinding == a; if (Ui.Button("rb" + a, on ? "Press..." : In.Name(a), bx + 84, by, 82, 21, "btnP", on: on)) { rebinding = a; while (Raylib.GetKeyPressed() != 0) { } } i++; }
                    if (rebinding is Act ra) { int k = Raylib.GetKeyPressed(); if (k != 0) { if (k != (int)KeyboardKey.Escape) { c.Keys[ra.ToString()] = k; Snd.Play("confirm", .6f); } rebinding = null; } }
                    yy += 5 * 26 + 2; Sl("Controller rumble", c.Rumble, v => { c.Rumble = v; Rumble.Kick(.4f, .4f, .1f); });
                    if (Ui.Button("rbres", "Reset keys", x, y + 204, 90, 21, "btn")) { c.Keys.Clear(); Snd.Play("undo"); } Font.Draw(In.UsingPad ? "RT digs, LT builds, LB/RB choose, X emotes." : "Left mouse digs, right mouse builds.", x + 98, y + 206, Ui.Soft); Font.Draw(In.UsingPad ? "Right stick aims; B runs, Y opens the satchel." : "E also opens the inventory.", x + 98, y + 217, Ui.Soft); break;
                }
            case 3: Tg("Reduce motion", c.ReduceMotion, v => c.ReduceMotion = v, "Calms bounces, wobbles and screen shake"); Tg("Larger text", c.BigText, v => c.BigText = v, "Makes interface text bigger"); Tg("Bold player outline", c.Contrast, v => { c.Contrast = v; if (P.D != null) P.Redraw(); }, "Thicker outline so you are easy to spot"); break;
        }
    }

    // ---------------------------------------------------------------- map
    const int MW = 588, MH = 124; const float MapY0 = 14, MapYH = 242;
    Col MapCol(World w, float x, float ty)
    {
        int xi = Math.Clamp((int)x, 0, World.W - 1), iy = Math.Clamp((int)ty, 0, World.H - 1), i = iy * World.W + xi; if (w.Wet[i] != 0) return Col.Lerp(w.Pools[w.Wet[i] - 1].Shallow, 0xe8dcc0, .25f); var t = w.Tile[i]; if (t == T.Air) return w.Wall[i] != T.Air && ty > w.Top0[xi] + 1 ? Col.Lerp(Blocks.Of(w.Wall[i]).Color, 0x3a3040, .6f) : Col.Lerp(0xf0e2bc, 0xe4cfa0, (ty - MapY0) / MapYH);
        if (ty - w.Top0[xi] < 3 && Biomes.Of(w.BiomeAt(xi)).Turf != null) return World.GrassCol(x).Shade(-.1f); return Col.Lerp(Blocks.Of(t).Color, 0xc8a884, .22f).Shade(M.Noise(x * .06f, ty * .16f) * .08f);
    }
    void BuildMap()
    {
        mapReady = true; if (map.Id != 0) { Raylib.UnloadTexture(map); Raylib.UnloadTexture(mapBig); } var w = W;
        var p = new Painter(World.W, 64); Parallel.For(0, World.W, x => { for (int y = 0; y < 64; y++) { float ty = MapY0 + y * MapYH / 64; p.Px[y * World.W + x] = (MathF.Abs(ty - w.Top0[x]) < 1.1f ? (Col)0x4a3428 : MapCol(w, x, ty)).Rl(); } }); map = p.ToTexture();
        // the big map is painted at the size it is shown, so nothing on it is ever stretched
        var b = new Painter(MW, MH); float TX(float px) => px * World.W / MW; float Y(float ty) => (ty - MapY0) * MH / MapYH; for (int y = 0; y < MH; y++) for (int x = 0; x < MW; x++) { float tx = TX(x), ty = MapY0 + y * MapYH / MH; float d = Y(w.Top0[(int)tx]) - y; b.Px[y * MW + x] = (d is >= -.5f and < .6f ? (Col)0x4a3428 : MapCol(w, tx, ty)).Rl(); }
        b.View(1, 0, 0); var r = new Rng(3); Vector2 V(float x, float y) => new(x, y); Ink I(Col c, float q = 2) => new Ink(c, q, .7f, 1) { Ao = 0, LinePx = 1 };
        for (float px = 34; px < MW - 34; px += r.R(5, 10))
        {
            int tx = (int)TX(px); if (w.WetTile(tx, w.Top0[tx] - 1)) continue; var bi = w.BiomeAt(tx); float y = Y(w.Top0[tx]) + 1, s = r.R(.8f, 1.2f); void Trunk(float h) { b.Begin().Rect(px - .8f, y - h * s, 1.6f, h * s); b.Flat(0x5a4034); } void Round(Col c) { Trunk(4); b.Begin().Ellipse(px, y - 7 * s, 4.4f * s, 4 * s).Ellipse(px - 2.4f * s, y - 5.4f * s, 2.6f * s, 2.4f * s).Ellipse(px + 2.4f * s, y - 5.6f * s, 2.6f * s, 2.4f * s); b.Shade(I(c, 3)); }
            switch (bi)
            {
                case Biome.Highlands: case Biome.Frost: Trunk(3); for (int k = 0; k < 2; k++) { b.Begin().Poly(V(px - (4 - k) * s, y - (2 + k * 4) * s), V(px, y - (9 + k * 4) * s), V(px + (4 - k) * s, y - (2 + k * 4) * s)); b.Shade(I(bi == Biome.Frost ? (k == 1 ? 0xf4faff : 0x8ab0b0) : 0x4f8058)); } break;
                case Biome.Dunes: if (!r.P(.3f)) break; b.Begin().Capsule(V(px, y), 1.6f, V(px, y - 8 * s), 1.6f).Capsule(V(px, y - 4), 1, V(px + 3, y - 7), 1); b.Shade(I(0x5a8c5a)); break;
                case Biome.Mesa: if (!r.P(.25f)) break; Round(0x6a8a58); break;
                case Biome.Amber: Round(r.P(.5f) ? 0xd8843a : r.P(.5f) ? 0xc9a347 : 0xc4523a); break; case Biome.Blossom: if (r.P(.6f)) Round(0xf0b0c4); break; case Biome.Jungle: Trunk(8); b.Begin().Ellipse(px, y - 10 * s, 6 * s, 3 * s); b.Shade(I(0x3f8a44)); break; case Biome.Marsh: if (r.P(.5f)) Round(0x66804a); break; case Biome.Bamboo: b.Line(V(px, y), V(px + 1, y - 11 * s), 1.2f, 0x6a9a3a, false); b.Line(V(px + 2, y), V(px + 2, y - 8 * s), 1.2f, 0x86b04a, false); break;
                case Biome.Lavender: if (r.P(.3f)) { b.Begin().Blob(V(px - 1.6f, y), V(px, y - 11 * s), V(px + 1.6f, y)); b.Shade(I(0x4f7452)); } else { b.Begin().Ellipse(px, y - 1.4f, 2.4f, 1.6f); b.Shade(I(0x9a80c8, 1)); } break;
                case Biome.Toadstool: b.Begin().Rect(px - 1, y - 6 * s, 2, 6 * s); b.Flat(0xe8d8cc); b.Begin().Blob(V(px - 4.6f * s, y - 5 * s), V(px, y - 11 * s), V(px + 4.6f * s, y - 5 * s)); b.Shade(I(r.P(.5f) ? 0xd0506a : 0x5aa8a0)); break; case Biome.Cinder: if (r.P(.4f)) { b.Line(V(px, y), V(px, y - 8 * s), 1.2f, 0x42383e, false); b.Line(V(px, y - 4), V(px + 3, y - 7), 1, 0x42383e, false); } break; case Biome.Crystal: b.Begin().Poly(V(px - 2 * s, y), V(px - 1, y - 9 * s), V(px + 2 * s, y)); b.Shade(I(r.P(.5f) ? 0xa4c8f0 : 0xc4b0f0)); break; case Biome.Moonpetal: if (r.P(.6f)) Round(r.P(.5f) ? 0x58b0c0 : 0x8a80d0); break; case Biome.Coast: if (tx is > 110 and < World.W - 110 && r.P(.4f)) { b.Line(V(px, y), V(px + 2, y - 8 * s), 1.2f, 0x7a5a44, false); b.Begin().Ellipse(px + 2, y - 8 * s, 4, 1.8f); b.Shade(I(0x6f9e48)); } break; default: if (r.P(.45f)) Round(r.P(.15f) ? 0xe8a0b4 : 0x6f9a46); break;
            }
        }
        mapBig = b.ToTexture();
    }
    void MapUi()
    {
        float fit = MathF.Min(1, (Ui.VW - 56) / MW); var (x, y, w, h) = Frame(MW * fit + 32, 236, "World Map", "map"); float mh = MH * fit; Ui.Nine("slot", x - 4, y - 2, w + 8, mh + 8, Color.White); Raylib.DrawTexturePro(mapBig, new Rectangle(0, 0, MW, MH), new Rectangle(x, y + 2, MW * fit, mh), Vector2.Zero, 0, Color.White); float X(float tx) => x + tx / World.W * w; float Y(float ty) => y + 2 + M.Clamp((ty - MapY0) / MapYH) * mh; var d = Store.Data; float[] end = [x - 20, x - 20, x - 20, x - 20];
        // places you have not been are still blank parchment
        foreach (var s in W.Segs) if (!d.Areas.Contains(Biomes.Of(s.B).Name)) { float x0 = X(s.X0), x1 = X(s.X1); Raylib.DrawRectangleV(new(MathF.Round(x0), y + 2), new(MathF.Round(x1) - MathF.Round(x0), mh), new Color(238, 220, 182, 225)); Font.Draw("?", (x0 + x1) / 2, y + mh / 2 - 8, new Color(176, 140, 104, 255), 2, .5f); }
        foreach (var s in W.Segs) { float mid = s.B == Biome.Coast ? (s.X0 == 0 ? 130 : World.W - 130) : (s.X0 + s.X1) / 2f; if (!d.Areas.Contains(Biomes.Of(s.B).Name)) continue; string n = Biomes.Of(s.B).Name; float tw = Font.Width(n, .85f), px = M.Clamp(X(mid), x + tw / 2, x + w - tw / 2); int row = 0; while (row < 3 && px - tw / 2 < end[row] + 6) row++; end[row] = px + tw / 2; float py = y + mh + 12 + row * 13; Raylib.DrawLineEx(new(X(mid), Y(W.Top0[(int)mid]) + 4), new(px, py), 1, new Color(120, 84, 60, 110)); Font.Draw(n, px, py, Ui.Ink, .85f, .5f); }
        int under = 0; for (int i = Biomes.Surface; i < Biomes.All.Length; i++) if (d.Areas.Contains(Biomes.All[i].Name)) under++; Font.Draw(d.Areas.Count(q => Biomes.All.Any(b => b.Name == q)) + " of " + Biomes.All.Length + " biomes found, " + under + " of 4 underground", x + w, y - 22, Ui.Soft, 1, 1.22f);
        float bob = MathF.Abs(M.Sin(Ui.T * 3.4f)) * 4, qx = X(P.P.X / 24), qy = Y(P.P.Y / 24) - 8 - bob; Raylib.DrawTriangle(new(qx - 5, qy - 5), new(qx, qy + 4), new(qx + 5, qy - 5), Ui.Ink); Raylib.DrawCircleV(new(qx, qy - 7), 6, Ui.Ink); Raylib.DrawCircleV(new(qx, qy - 7), 4.6f, new Color(255, 110, 90, 255)); Raylib.DrawCircleV(new(qx - 1, qy - 8), 1.6f, Ui.Cream);
    }

    // ---------------------------------------------------------------- title
    float titlePan = 700 * 24;
    void UpdateTitle(float dt)
    {
        titleT += dt; Env.Update(dt * .6f); if (panel == Panel.Settings && (In.Press(Act.Menu) || In.PadBack) && rebinding == null) ClosePanel(); openT += dt; titlePan += dt * 15; if (titlePan > 2160 * 24) titlePan = 240 * 24; CamX = titlePan; float gy = 0; for (int i = 0; i < 26; i++) gy += W.Top0[Math.Clamp((int)(titlePan / 24) + i, 0, World.W - 1)]; gy = gy / 26 * 24 - Ui.VH * .72f; CamY = titleT < .1f ? gy : M.Damp(CamY, gy, .6f, dt); Local(titlePan / 24 + 13, dt); Life.Update(dt, P, CamX, CamY, (int)Ui.VW, (int)Ui.VH); Emit(dt); Ambience(); Snd.Music("title"); P.P = new(-9999, -9999); Under = 0;
    }
    public void OpenTitle() { Title = true; titleT = 0; panel = Panel.None; titlePan = W.SpawnTile * 24 - 400; Env.Clock = .70f; Env.SkipTo = -1; Env.SetWeather(Weather.Breezy, true); Env.WindForce = -1; Env.TimeScale = 1; P.Mining = false; Local(titlePan / 24 + 13, 99); Env.Colors(); }
    void DrawTitle()
    {
        float W2 = Ui.VW, H = Ui.VH, t = titleT; Raylib.DrawRectangleGradientV(0, 0, (int)W2 + 1, 170, new Color(30, 14, 34, 120), new Color(30, 14, 34, 0)); Raylib.DrawRectangleGradientV(0, (int)H - 120, (int)W2 + 1, 121, new Color(30, 14, 24, 0), new Color(30, 14, 24, 120)); string s = "Untitled Game"; float size = 6, tw = Font.Width(s, size); float x = W2 / 2 - tw / 2;
        for (int i = 0; i < s.Length; i++) { string c = s[i].ToString(); float k = Store.Cfg.ReduceMotion ? 1 : Ease.OutElastic(M.Clamp((t - .3f - i * .07f) / 1.1f)); float cw = Font.Width(c, size) + size * Font.Zoom; if (s[i] != ' ' && k > .01f) { float y = 60 + M.Sin(t * 1.8f + i * .55f) * 4 + (1 - k) * -80; float sc = MathF.Max(.02f, k); Ui.PushScale(x + cw / 2, y + 22, sc, sc, M.Sin(t * 1.3f + i) * 2.4f); Col cc = Col.Lerp(0xffe2a0, 0xf08a4a, .5f + .5f * M.Sin(i * .6f + t * .8f)); Font.Draw(c, x + 2, y + 5, new Color(58, 26, 30, 170), size, 0); Font.Draw(c, x, y, cc.Rl(), size, 0, new Color(62, 30, 30, 255)); Ui.PopScale(); } x += cw; }
        if (panel == Panel.Settings) { float ps = .82f + Pop * .18f; Ui.PushScale(W2 / 2, H / 2, ps, ps); SettingsUi(); Ui.PopScale(); return; }
        Font.Draw("Version " + Saves.GameVer, W2 - 6, H - 13, Raylib.Fade(Ui.Cream, .65f * M.Clamp(t - 1.4f)), 1, 1, new Color(40, 22, 26, 120));
        Font.Draw("a cozy little world", W2 / 2, 124, Raylib.Fade(Ui.Cream, M.Clamp(t - 1.4f)), 1.5f, .5f, Ui.Ink); string[] l = ["Play", "Settings", "Quit"]; string[] ic = ["right", "gear", "x"]; string[] st = ["btnG", "btnB", "btnR"]; for (int i = 0; i < 3; i++) { float k = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp((t - 1 - i * .12f) * 2.4f)); if (k <= 0) continue; Ui.PushScale(W2 / 2, 180 + i * 36, k, k); if (Ui.Button("ti" + i, l[i], W2 / 2 - 66, 166 + i * 36, 132, 29, st[i], ic[i], text: 1.5f)) switch (i) { case 0: Program.PlayFromTitle(); break; case 1: settingsFrom = Panel.None; Open(Panel.Settings); break; case 2: Program.Quit = true; break; } Ui.PopScale(); }
    }
}

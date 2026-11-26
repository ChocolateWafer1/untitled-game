using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public static class Font
{
    static Texture2D tex; static readonly Dictionary<char, (int x, int w)> g = new();
    public static void Load()
    {
        int total = 2; foreach (var kv in FontData.Glyphs) total += kv.Value.IndexOf('/') + 2;
        var p = new Painter(total, 12); int x = 1;
        foreach (var kv in FontData.Glyphs)
        {
            var rows = kv.Value.Split('/'); int w = rows[0].Length; g[kv.Key] = (x, w);
            for (int r = 0; r < rows.Length; r++) for (int c = 0; c < w && c < rows[r].Length; c++) if (rows[r][c] == '#') p.Px[(r + 1) * p.W + x + c] = Color.White;
            x += w + 2;
        }
        tex = p.ToTexture();
    }
    public static float Zoom => Store.Cfg.BigText ? 1.25f : 1;
    public static float Width(string s, float size = 1) { float w = 0; foreach (char c in s) w += (g.TryGetValue(c, out var q) ? q.w : 3) + 1; return MathF.Max(0, w - 1) * size * Zoom; }
    public static void Draw(string s, float x, float y, Color c, float size = 1, float align = 0, Color? outline = null, float shadow = 0)
    {
        size *= Zoom; float w = 0; foreach (char ch in s) w += (g.TryGetValue(ch, out var q) ? q.w : 3) + 1;
        x -= (w - 1) * size * align;
        if (shadow > 0) Pass(s, x, y + size * shadow, Raylib.Fade(Color.Black, .35f * c.A / 255f), size);
        if (outline is Color o) { o.A = (byte)(o.A * c.A / 255); for (int i = 0; i < 8; i++) { int dx = i is 0 or 3 or 5 ? -1 : i is 2 or 4 or 7 ? 1 : 0, dy = i < 3 ? -1 : i > 4 ? 1 : 0; Pass(s, x + dx * size, y + dy * size, o, size); } }
        Pass(s, x, y, c, size);
    }
    static void Pass(string s, float x, float y, Color c, float size)
    {
        foreach (char ch in s)
        {
            if (!g.TryGetValue(ch, out var q)) { x += 4 * size; continue; }
            Raylib.DrawTexturePro(tex, new Rectangle(q.x, 1, q.w, 9), new Rectangle(x, y, q.w * size, 9 * size), Vector2.Zero, 0, c); x += (q.w + 1) * size;
        }
    }
    public static List<string> Wrap(string s, float w, float size = 1)
    {
        var r = new List<string>(); string line = "";
        foreach (var word in s.Split(' ')) { var t = line.Length == 0 ? word : line + " " + word; if (Width(t, size) > w && line.Length > 0) { r.Add(line); line = word; } else line = t; }
        if (line.Length > 0) r.Add(line); return r;
    }
}

/// Immediate mode UI with springy, per-widget animation state. Works in a 360-tall virtual canvas.
public static class Ui
{
    public static float VW = 640, VH = 360, Scale = 2, T, DT; public static Vector2 Mouse; public static bool Click, Held, Release, RightClick, RightHeld; public static float Wheel; public static bool Enabled = true;
    public sealed class An { public Spring Hover = new(), Press = new(), Sel = new(); public bool Was, SlotWas; public float Born = -1; public int Seen; }
    static readonly Dictionary<string, An> an = new(); static int frame; public static string Hot, Active; public static string Tip; static float tipT; static Vector2 pad;
    static Vector2 lastMouse; public static bool PadMode; public static Vector2? FakeMouse; public static bool FakeClick, FakeRight, FakeHold, FakeRelease, FakeRightHold; public static float FakeWheel;
    public static readonly Color Ink = new(62, 38, 34, 255), Cream = new(250, 240, 218, 255), Soft = new(140, 104, 82, 255), Gold = new(240, 182, 78, 255), Orange = new(226, 124, 66, 255), Green = new(120, 158, 88, 255), Red = new(196, 82, 70, 255);

    public static void Begin(float dt)
    {
        frame++; DT = dt; T += dt; int w = Raylib.GetRenderWidth(), h = Raylib.GetRenderHeight(); Scale = h / 360f; VH = 360; VW = w / Scale; if (VW < 480) { Scale = w / 480f; VW = 480; VH = h / Scale; }
        var m = Raylib.GetMousePosition() / Scale; Click = Raylib.IsMouseButtonPressed(MouseButton.Left); Held = Raylib.IsMouseButtonDown(MouseButton.Left); Release = Raylib.IsMouseButtonReleased(MouseButton.Left); RightClick = Raylib.IsMouseButtonPressed(MouseButton.Right); RightHeld = Raylib.IsMouseButtonDown(MouseButton.Right); Wheel = Raylib.GetMouseWheelMove();
        if (m != lastMouse) PadMode = false; lastMouse = m;
        In.Track(); (prevRects, rects) = (rects, prevRects); rects.Clear(); bool cursor = !InGame; InGame = false;
        if (In.Pad0 && cursor)
        {
            var st = In.LStick; if (st != default) { if (!PadMode) pad = m; PadMode = true; pad += st * dt * (90 + 430 * st.LengthSquared()); pad = Vector2.Clamp(pad, Vector2.Zero, new(VW, VH)); }
            (GamepadButton b, Vector2 d)[] hops = [(GamepadButton.LeftFaceUp, new(0, -1)), (GamepadButton.LeftFaceDown, new(0, 1)), (GamepadButton.LeftFaceLeft, new(-1, 0)), (GamepadButton.LeftFaceRight, new(1, 0))];
            foreach (var (b, d) in hops) if (Raylib.IsGamepadButtonPressed(0, b)) { if (!PadMode) pad = m; PadMode = true; Hop(d); }
            if (PadMode) { m = pad; Click |= Raylib.IsGamepadButtonPressed(0, GamepadButton.RightFaceDown); Held |= Raylib.IsGamepadButtonDown(0, GamepadButton.RightFaceDown); Release |= Raylib.IsGamepadButtonReleased(0, GamepadButton.RightFaceDown); RightClick |= Raylib.IsGamepadButtonPressed(0, GamepadButton.RightFaceLeft); RightHeld |= Raylib.IsGamepadButtonDown(0, GamepadButton.RightFaceLeft); Wheel -= In.RStick.Y * dt * 10; }
        }
        else if (!cursor) PadMode = false;
        if (FakeMouse is Vector2 f) { m = f; Click = FakeClick; Held = FakeClick || FakeHold; Release = FakeRelease; RightClick = FakeRight; RightHeld = FakeRight || FakeRightHold; Wheel = FakeWheel; FakeWheel = 0; FakeClick = FakeRight = FakeRelease = false; }
        Mouse = m; Tip = null; if (!Held) Active = null; Enabled = true; Hot = null;
    }
    public static void Push() { Rlgl.PushMatrix(); Rlgl.Scalef(Scale, Scale, 1); }
    public static void Pop() => Rlgl.PopMatrix();
    public static void End()
    {
        tipT = Tip != null ? MathF.Min(1, tipT + DT * 8) : 0;
        if (Tip != null && tipT > .2f) { float w = Font.Width(Tip) + 12, x = M.Clamp(Mouse.X + 8, 4, VW - w - 4), y = M.Clamp(Mouse.Y - 16, 4, VH - 18); Nine("tip", x, y - (1 - Ease.OutBack(tipT)) * -4, w, 15, Raylib.Fade(Color.White, tipT)); Font.Draw(Tip, x + 6, y + 3, Raylib.Fade(Cream, tipT)); }
        if (WantKeys != null) Keyboard(WantKeys); else OskOn = false; WantKeys = null;
        if (PadMode) { var c = Mouse; Raylib.DrawCircleV(c, 5, Ink); Raylib.DrawCircleV(c, 3.8f, Gold); Raylib.DrawCircleV(c - new Vector2(1, 1), 1.2f, Cream); }
    }
    // ---------------------------------------------------------------- on-screen keyboard for controllers
    /// A text field that is being typed into sets this each frame (extra keys for the number row); the keyboard shows only when a controller is in use.
    public static string WantKeys; public static bool OskOn; public static Rectangle OskRect; static bool inOsk, oskLower, oskBack, oskDone; static readonly Queue<int> oskQ = new();
    public static int NextChar() => oskQ.Count > 0 ? oskQ.Dequeue() : Raylib.GetCharPressed();
    public static bool BackKey() { bool b = oskBack; oskBack = false; return b || Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace); }
    public static bool EnterKey() { bool b = oskDone; oskDone = false; return b || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter); }
    static void Keyboard(string extra)
    {
        if (!In.UsingPad) { OskOn = false; return; }
        string[] rows = ["1234567890" + extra, "QWERTYUIOP", "ASDFGHJKL-", "ZXCVBNM_"]; float kw = 22, kh = 19, g = 3, pw = 13 * (kw + g) + 14, ph = 5 * (kh + g) + 16, px = MathF.Round(VW / 2 - pw / 2), py = VH - ph - 6;
        bool was = OskOn; OskOn = true; OskRect = new Rectangle(px, py, pw, ph); if (!was) { pad = new(px + pw / 2, py + ph - 18); PadMode = true; }
        inOsk = true; Nine("panel", px, py, pw, ph, Color.White);
        for (int r = 0; r < rows.Length; r++) { float rx = px + 7 + (pw - 14 - rows[r].Length * (kw + g) + g) / 2; for (int i = 0; i < rows[r].Length; i++) { char c = rows[r][i]; if (oskLower && char.IsLetter(c)) c = char.ToLowerInvariant(c); if (Button("osk" + r + "_" + i, c.ToString(), rx + i * (kw + g), py + 8 + r * (kh + g), kw, kh, "btnP", text: .95f)) { oskQ.Enqueue(c); Snd.Play("type", .7f); } } }
        float by = py + 8 + 4 * (kh + g), bx = px + 7;
        if (Button("oskAa", oskLower ? "ABC" : "abc", bx, by, 44, kh, "btnP", text: .9f)) oskLower = !oskLower;
        if (Button("oskSp", "Space", bx + 48, by, 130, kh, "btnP", text: .9f)) oskQ.Enqueue(' ');
        if (Button("oskBk", "Delete", bx + 182, by, 64, kh, "btnR", text: .9f, tip: "B")) oskBack = true;
        if (Button("oskOk", "Done", bx + 250, by, pw - 14 - 250, kh, "btnG", "check", text: .9f, tip: "Start")) oskDone = true;
        if (In.PadPress(GamepadButton.RightFaceRight)) oskBack = true; if (In.PadPress(GamepadButton.MiddleRight)) oskDone = true; if (In.PadPress(GamepadButton.RightFaceLeft)) oskQ.Enqueue(' ');
        inOsk = false;
    }
    public static An A(string id) { if (!an.TryGetValue(id, out var a)) an[id] = a = new An(); if (a.Seen < frame - 1) { a.Born = T; a.Hover = new(); a.Press = new(); a.Was = false; } a.Seen = frame; return a; }
    /// Set by the game every frame the character has control, so the left stick walks instead of moving a cursor.
    public static bool InGame;
    static List<Vector4> rects = new(), prevRects = new();
    /// Jumps the controller cursor to the nearest button in a direction.
    static void Hop(Vector2 d)
    {
        Vector2 best = default; float bs = 1e9f;
        foreach (var r in prevRects) { if (r.Z < 4 || r.W < 4) continue; var c = new Vector2(r.X + r.Z / 2, r.Y + r.W / 2); if (c.X < 0 || c.Y < 0 || c.X > VW || c.Y > VH) continue; var v = c - pad; float along = Vector2.Dot(v, d); if (along < 4) continue; float perp = MathF.Abs(v.X * d.Y - v.Y * d.X), sc = along + perp * 2.2f; if (sc < bs) { bs = sc; best = c; } }
        if (bs < 1e8f) { pad = best; Snd.Play("hover", .35f); }
    }
    static bool Blocked => OskOn && !inOsk && Mouse.X >= OskRect.X && Mouse.Y >= OskRect.Y && Mouse.X < OskRect.X + OskRect.Width && Mouse.Y < OskRect.Y + OskRect.Height;
    public static bool InOsk => OskOn && Mouse.X >= OskRect.X && Mouse.Y >= OskRect.Y && Mouse.X < OskRect.X + OskRect.Width && Mouse.Y < OskRect.Y + OskRect.Height;
    public static bool Over(float x, float y, float w, float h) => Enabled && !Blocked && Mouse.X >= x && Mouse.Y >= y && Mouse.X < x + w && Mouse.Y < y + h;
    /// Runs interaction + springs for a widget. Returns (animation, clicked).
    public static (An a, bool hit, bool over) Widget(string id, float x, float y, float w, float h, string sfx = "click", bool silent = false)
    {
        rects.Add(new Vector4(x, y, w, h));
        var a = A(id); bool o = Over(x, y, w, h) && (Active == null || Active == id); bool hit = false;
        if (o) { Hot = id; if (!a.Was && !silent) Snd.Play("hover", .35f, 1 + M.Hash(frame, 3) * .12f); if (Click) { Active = id; hit = true; a.Press.V += 9; if (sfx != null) Snd.Play(sfx, .8f, .96f + M.Hash(frame, 7) * .1f); } }
        a.Was = o; a.Hover.Update(o ? 1 : 0, 420, 22, DT); a.Press.Update(0, 480, 16, DT); return (a, hit, o);
    }
    public static void PushScale(float cx, float cy, float sx, float sy, float rot = 0) { Rlgl.PushMatrix(); Rlgl.Translatef(cx, cy, 0); Rlgl.Rotatef(rot, 0, 0, 1); Rlgl.Scalef(sx, sy, 1); Rlgl.Translatef(-cx, -cy, 0); }
    public static void PopScale() => Rlgl.PopMatrix();
    static void Juice(An a, float x, float y, float w, float h, float grow = .05f, float rot = 0)
    {
        float k = Store.Cfg.ReduceMotion ? .3f : 1; float s = 1 + a.Hover.X * grow * k, p = M.Clamp(a.Press.X, -.5f, .5f) * .5f * k; PushScale(x + w / 2, y + h / 2, s + p * .5f, s - p, rot * k);
    }
    public static bool Button(string id, string label, float x, float y, float w, float h, string style = "btn", string icon = null, string tip = null, bool on = false, float text = 1, bool disabled = false)
    {
        bool en = Enabled; if (disabled) Enabled = false; var (a, hit, o) = Widget(id, x, y, w, h); Enabled = en; Juice(a, x, y, w, h, .05f, label == null ? M.Sin(T * 14) * a.Hover.X * 2 : 0);
        Nine(on ? style + "On" : style, x, y, w, h, disabled ? new Color(190, 180, 175, 255) : Raylib.ColorLerp(Color.White, new Color(255, 248, 225, 255), M.Clamp(a.Hover.X)));
        float cx = x + w / 2; if (icon != null) { float iw = 16; float ix = label == null ? cx - iw / 2 : x + 5; Icon(icon, ix, y + h / 2 - iw / 2 - .5f, 1, disabled ? .5f : 1); if (label != null) cx += 6; }
        if (label != null) Font.Draw(label, cx, y + h / 2 - 4.5f * text * Font.Zoom, style == "btnP" ? Ink : new Color(255, 248, 232, disabled ? 130 : 255), text, .5f, style == "btnP" ? null : new Color(70, 38, 28, 255));
        PopScale(); if (o && tip != null) Tip = tip; return hit && !disabled;
    }
    public static bool Slider(string id, ref float v, float x, float y, float w, Action<float, float, float> track = null)
    {
        var (a, _, o) = Widget(id, x - 4, y - 3, w + 8, 14, null); bool ch = false;
        if (Active == id && Held) { float n = M.Clamp((Mouse.X - x) / w); if (MathF.Abs(n - v) > .0005f) { if ((int)(n * 20) != (int)(v * 20)) Snd.Play("tick", .35f, .8f + n * .6f); v = n; ch = true; } }
        if (o && Wheel != 0 && Active == null) { }
        a.Sel.Update(v, 600, 34, DT); float k = M.Clamp(a.Sel.X, -.02f, 1.02f);
        if (track != null) track(x, y, w); else { Nine("track", x - 2, y + 1, w + 4, 7, Color.White); if (k > .01f) Nine("fill", x - 1, y + 2, MathF.Max(5, w * k + 2), 5, Color.White); }
        float kx = x + w * k; Juice(a, kx - 5, y - 1, 10, 10, .22f); Nine("knob", kx - 5, y - 1, 10, 11, Color.White); PopScale(); return ch;
    }
    public static bool Toggle(string id, ref bool v, float x, float y)
    {
        var (a, hit, _) = Widget(id, x, y, 26, 13); if (hit) v = !v; a.Sel.Update(v ? 1 : 0, 520, 24, DT); Juice(a, x, y, 26, 13, .06f);
        Nine(v ? "fillBig" : "track", x, y + 1, 26, 11, Color.White); Nine("knob", x + M.Lerp(0, 15, a.Sel.X), y, 11, 13, Color.White); PopScale(); return hit;
    }
    public static (An a, bool hit, bool over) Card(string id, float x, float y, float w, float h, bool sel)
    {
        var r = Widget(id, x, y, w, h, "pick"); r.a.Sel.Update(sel ? 1 : 0, 380, 15, DT); float born = M.Clamp((T - r.a.Born) * 5); float s = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(born);
        float k = Store.Cfg.ReduceMotion ? .3f : 1; float sc = s * (1 + r.a.Hover.X * .07f * k + r.a.Sel.X * .04f), pr = M.Clamp(r.a.Press.X, -.5f, .5f) * .5f * k; PushScale(x + w / 2, y + h / 2, MathF.Max(.01f, sc + pr * .5f), MathF.Max(.01f, sc - pr), r.over ? M.Sin(T * 10) * 1.6f * k : 0);
        Nine(sel ? "cardOn" : "card", x, y, w, h, Color.White); return r;
    }

    // ---------------------------------------------------------------- art
    static readonly Dictionary<string, (Texture2D t, int b)> nine = new(); static readonly Dictionary<string, Texture2D> icons = new();
    public static void Nine(string n, float x, float y, float w, float h, Color tint)
    {
        if (!nine.TryGetValue(n, out var q)) { if (n.EndsWith("On") && nine.ContainsKey(n[..^2])) q = nine[n[..^2]]; else return; }
        var np = new NPatchInfo { Source = new Rectangle(0, 0, q.t.Width, q.t.Height), Left = q.b, Right = q.b, Top = q.b, Bottom = q.b, Layout = NPatchLayout.NinePatch };
        Raylib.DrawTextureNPatch(q.t, np, new Rectangle(x, y, w, h), Vector2.Zero, 0, tint);
    }
    public static Texture2D IconTex(string n) => icons.TryGetValue(n, out var t) ? t : icons["dot"];
    public static void Icon(string n, float x, float y, float s = 1, float al = 1, Color? tint = null)
    {
        if (!icons.TryGetValue(n, out var t)) return; var c = tint ?? Color.White; c.A = (byte)(255 * al); Raylib.DrawTextureEx(t, new(x, y), 0, s, c);
    }
    static void RR(Painter p, float x, float y, float w, float h, float r)
    { p.Rect(x + r, y, w - r * 2, h).Rect(x, y + r, w, h - r * 2).Ellipse(x + r, y + r, r, r).Ellipse(x + w - r, y + r, r, r).Ellipse(x + r, y + h - r, r, r).Ellipse(x + w - r, y + h - r, r, r); }
    static void Mk(string n, int size, int border, Action<Painter> f) { var p = new Painter(size, size); p.View(1, 0, 0); f(p); nine[n] = (p.ToTexture(), border); }
    public static void Load()
    {
        Font.Load();
        void Box(string n, int size, int b, float r, Col line, Col rim, Col body, float bevel = 2)
            => Mk(n, size, b, p => { p.Begin(); RR(p, 0, 0, size, size, r); p.Flat(line); p.Begin(); RR(p, 1, 1, size - 2, size - 2, r - 1); p.Shade(new Ink(rim, bevel, .8f, 0) { Ao = 0, Grad = .15f }); p.Begin(); RR(p, 1 + bevel, 1 + bevel, size - 2 - bevel * 2, size - 2 - bevel * 2, MathF.Max(1, r - 1 - bevel)); p.Flat(body); });
        // wooden frame around warm paper
        Mk("panel", 64, 20, p =>
        {
            p.Begin(); RR(p, 0, 0, 64, 64, 8); p.Flat(0x3a2220); p.Begin(); RR(p, 1, 1, 62, 62, 7); Func<float, float, Col> wood = (x, y) => Col.Lerp(0xa8693c, 0x8a5230, M.Clamp(.5f + M.Noise(MathF.Min(x, 64 - x) * .9f, MathF.Min(y, 64 - y) * .9f, 2) * .6f)); p.Shade(new Ink(wood, 3, .9f, 0) { Ao = 0, Grad = 0 });
            p.Begin(); RR(p, 6, 6, 52, 52, 4); p.Flat(0x4e2e22); p.Begin(); RR(p, 7, 7, 50, 50, 3.5f); p.Flat(0xf3e2bd); p.Begin(); RR(p, 8, 8, 48, 48, 3); p.Flat(0xfaefd3);
            p.Begin(); RR(p, 7, 7, 50, 50, 3.5f); for (int i = 0; i < 48; i++) { p.Dot(8 + i, 8, 0xe2c898, .8f); p.Dot(8 + i, 9, 0xead6ac, .5f); p.Dot(8, 8 + i, 0xe2c898, .6f); }
            foreach (var (x, y) in new[] { (4, 4), (60, 4), (4, 60), (60, 60) }) { p.Begin().Ellipse(x, y, 2.2f, 2.2f); p.Shade(new Ink(0xe8bd62, 1.5f, .9f) { Ao = 0 }); }
        });
        Mk("paper", 48, 14, p => { p.Begin(); RR(p, 0, 0, 48, 48, 6); p.Flat(0x5a3a2a); p.Begin(); RR(p, 1, 1, 46, 46, 5); p.Flat(0xead3a4); p.Begin(); RR(p, 2, 2, 44, 44, 4); p.Flat(0xfaefd3); for (int i = 0; i < 12; i++) { p.Dot(5 + i * 3.3f, 4, 0xc9a06a); p.Dot(6 + i * 3.3f, 4, 0xc9a06a); p.Dot(5 + i * 3.3f, 43, 0xc9a06a); p.Dot(6 + i * 3.3f, 43, 0xc9a06a); p.Dot(4, 5 + i * 3.3f, 0xc9a06a); p.Dot(4, 6 + i * 3.3f, 0xc9a06a); p.Dot(43, 5 + i * 3.3f, 0xc9a06a); p.Dot(43, 6 + i * 3.3f, 0xc9a06a); } });
        void Btn(string n, Col c) => Mk(n, 24, 9, p => { p.Begin(); RR(p, 0, 0, 24, 24, 6); p.Flat(0x3e2420); p.Begin(); RR(p, 1, 1, 22, 22, 5); p.Flat(c.Shade(-.45f)); p.Begin(); RR(p, 1, 1, 22, 19, 5); p.Flat(c); p.Begin(); RR(p, 2, 2, 20, 8, 4); p.Flat(c.Shade(.22f)); p.Begin(); RR(p, 2, 4, 20, 14, 4); p.Flat(c.Shade(.06f)); p.Begin().Rect(5, 2, 8, 1); p.Flat(c.Shade(.8f)); });
        Btn("btn", 0xc98a4e); Btn("btnOn", 0xe8a94a); Btn("btnG", 0x7fa05a); Btn("btnR", 0xc45a48); Btn("btnB", 0x5f8aa8); Btn("btnP", 0xf3e2bd); Btn("btnPOn", 0xf0c060); Btn("tab", 0xb0764a); Btn("tabOn", 0xf0bd5a);
        Box("card", 24, 8, 5, 0x8a6a4e, 0xf6e6c2, 0xfbf3de, 1); Box("cardOn", 24, 8, 5, 0x7a3a20, 0xf0a848, 0xfff3cf, 2);
        Box("slot", 24, 8, 5, 0x6a4a38, 0xc9ac7c, 0xdcc498, 1); Box("tip", 16, 6, 4, 0x2a1818, 0x5a3a30, 0x46302a, 1); Box("dark", 24, 8, 6, 0x1e1418, 0x3a2a30, 0x2c2026, 1);
        Box("track", 12, 5, 4, 0x6a4a38, 0xb89868, 0xc9ac7c, 1); Box("fill", 8, 3, 2.5f, 0xb8602a, 0xf0a848, 0xe8923f, 1); Box("fillBig", 12, 5, 4, 0x4a6a3a, 0x9aba6a, 0x86a858, 1);
        Mk("knob", 11, 5, p => { p.Begin().Ellipse(5.5f, 5.5f, 5.5f, 5.5f); p.Flat(0x3e2420); p.Begin().Ellipse(5.5f, 5.5f, 4.5f, 4.5f); p.Shade(new Ink(0xf6dfa8, 3, .8f, 0) { Ao = 0 }); p.Begin().Ellipse(4, 4, 1.2f, 1.2f); p.Flat(0xfffaf0); });
        Mk("glow", 64, 1, p => { for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = MathF.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32; float v = M.Clamp(1 - d); v = v * v; p.Px[y * 64 + x] = new Color(255, 255, 255, (int)(v * 255)); } });
        Glow = nine["glow"].t; Raylib.SetTextureFilter(Glow, TextureFilter.Bilinear);
        Icons.Make(icons);
    }
    public static Texture2D Glow;
    public static void GlowAt(float x, float y, float r, Color c) => Raylib.DrawTexturePro(Glow, new Rectangle(0, 0, 64, 64), new Rectangle(x - r, y - r, r * 2, r * 2), Vector2.Zero, 0, c);
    public static void Tex(Texture2D t, float x, float y, float s, Color c, bool flip = false) => Raylib.DrawTexturePro(t, new Rectangle(0, 0, flip ? -t.Width : t.Width, t.Height), new Rectangle(x, y, t.Width * s, t.Height * s), Vector2.Zero, 0, c);
    public static void Clip(float x, float y, float w, float h) { Rlgl.DrawRenderBatchActive(); Raylib.BeginScissorMode((int)(x * Scale), (int)(y * Scale), (int)(w * Scale), (int)(h * Scale)); }
    public static void Unclip() => Raylib.EndScissorMode();
}

static class Icons
{
    static Vector2 V(float x, float y) => new(x, y);
    public static void Make(Dictionary<string, Texture2D> o)
    {
        void I(string n, Action<Painter> f) { var p = new Painter(16, 16); p.View(1, 0, 0); f(p); p.Outline(0x3e2420, 1); o[n] = p.ToTexture(); }
        Ink K(Col c, float r = 2) => new Ink(c, r, .7f, 0) { Ao = 0 };
        I("dot", p => { p.Begin().Ellipse(8, 8, 3, 3); p.Shade(K(0xf0d070)); });
        I("body", p => { p.Begin().Ellipse(8, 4, 2.6f, 2.8f); p.Shade(K(0xe8b48e)); p.Begin().Poly(V(3, 14), V(4, 8.5f), V(12, 8.5f), V(13, 14)); p.Shade(K(0x5f8aa8)); });
        I("face", p => { p.Begin().Ellipse(8, 8, 6, 6); p.Shade(K(0xf0c4a0, 3)); p.Dot(5, 7, 0x3e2420); p.Dot(5, 8, 0x3e2420); p.Dot(10, 7, 0x3e2420); p.Dot(10, 8, 0x3e2420); p.Curve(V(5.5f, 10), V(8, 12.4f), V(10.5f, 10), 1, 0x8c2f35, false); });
        I("hair", p => { p.Begin().Blob(V(2, 12), V(3, 4), V(8, 2), V(13, 4), V(14, 12), V(11.5f, 13), V(11, 7), V(8, 6), V(5, 7), V(4.5f, 13)); p.Shade(K(0x9a5a34, 3)); });
        I("top", p => { p.Begin().Poly(V(2, 5), V(5, 2.5f), V(7, 4), V(9, 4), V(11, 2.5f), V(14, 5), V(12.4f, 7.6f), V(11.4f, 7), V(11.4f, 14), V(4.6f, 14), V(4.6f, 7), V(3.6f, 7.6f)); p.Shade(K(0xe9dcc3, 3)); });
        I("jacket", p => { p.Begin().Poly(V(1.6f, 6), V(5, 2.4f), V(11, 2.4f), V(14.4f, 6), V(14, 12), V(12, 12), V(11.6f, 14), V(4.4f, 14), V(4, 12), V(2, 12)); p.Shade(K(0x4f6b52, 3)); p.Line(V(8, 5), V(8, 14), 1, 0xd8c49a); p.Begin().Poly(V(5.4f, 2.4f), V(8, 5.6f), V(10.6f, 2.4f)); p.Flat(0xd8c49a); });
        I("pants", p => { p.Begin().Poly(V(4, 2), V(12, 2), V(13, 14), V(9.4f, 14), V(8, 6.6f), V(6.6f, 14), V(3, 14)); p.Shade(K(0x4a6285, 2.4f)); p.Line(V(4, 4), V(12, 4), 1, 0x35496b); });
        I("shoes", p => { p.Begin().Poly(V(2, 5), V(6, 5), V(8, 8.4f), V(13, 9.6f), V(14, 12), V(2, 12)); p.Shade(K(0xb8473f, 2.4f)); p.Begin().Rect(2, 11.6f, 12.2f, 2); p.Flat(0xf6eee0); });
        I("hat", p => { p.Begin().Blob(V(4.4f, 10), V(4.6f, 4), V(8, 3), V(11.4f, 4), V(11.6f, 10)); p.Shade(K(0xc99a4b, 2.4f)); p.Begin().Rect(4.4f, 7.8f, 7.2f, 2); p.Flat(0x8c2f35); p.Begin().Ellipse(8, 10.8f, 7, 1.7f); p.Shade(K(0xc99a4b, 1)); });
        I("extras", p => { p.Begin().Ellipse(4.8f, 8, 3, 3).CutEllipse(4.8f, 8, 1.6f, 1.6f).Ellipse(11.2f, 8, 3, 3).CutEllipse(11.2f, 8, 1.6f, 1.6f).Rect(7, 7.4f, 2, 1.2f); p.Flat(0x4e4c52); });
        I("style", p => { p.Begin().Poly(V(8, 1.6f), V(9.8f, 6), V(14.4f, 6.2f), V(10.8f, 9), V(12, 13.6f), V(8, 11), V(4, 13.6f), V(5.2f, 9), V(1.6f, 6.2f), V(6.2f, 6)); p.Shade(K(0xf0bd5a, 3)); });
        I("dice", p => { p.Begin().Rect(3, 3, 10, 10); p.Shade(K(0xf6eee0, 2)); foreach (var (x, y) in new[] { (5, 5), (10, 5), (7, 7), (5, 10), (10, 10) }) { p.Begin().Rect(x, y, 1.6f, 1.6f); p.Flat(0xb8473f); } });
        I("undo", p => { p.Curve(V(4, 7), V(14, 3), V(11, 13), 2, 0xf6eee0, false); p.Begin().Poly(V(1.6f, 7.4f), V(6.4f, 3), V(6.8f, 10)); p.Flat(0xf6eee0); });
        I("redo", p => { p.Curve(V(12, 7), V(2, 3), V(5, 13), 2, 0xf6eee0, false); p.Begin().Poly(V(14.4f, 7.4f), V(9.6f, 3), V(9.2f, 10)); p.Flat(0xf6eee0); });
        I("flip", p => { p.Begin().Poly(V(1.6f, 8), V(6, 4), V(6, 12)).Poly(V(14.4f, 8), V(10, 4), V(10, 12)).Rect(5, 7, 6, 2); p.Flat(0xf6eee0); });
        I("fan", p => { for (int i = 0; i < 3; i++) { float a = i * M.TAU / 3 + .4f; p.Begin().Ellipse(8 + M.Sin(a) * 3.4f, 8 + M.Cos(a) * 3.4f, 2.7f, 2.7f); p.Shade(K(0x9ec3cf, 2)); } p.Begin().Ellipse(8, 8, 1.6f, 1.6f); p.Flat(0x35496b); });
        I("save", p => { p.Begin().Poly(V(3, 2), V(13, 2), V(13, 14), V(8, 10.6f), V(3, 14)); p.Shade(K(0xc45a48, 2)); });
        I("lock", p => { p.Begin().Ellipse(8, 6, 3.6f, 4).CutEllipse(8, 6, 2, 2.6f); p.Flat(0xb9b2a8); p.Begin().Rect(3.6f, 6.6f, 8.8f, 7); p.Shade(K(0xe0b650)); p.Begin().Rect(7.4f, 9, 1.4f, 2.6f); p.Flat(0x5a3a26); });
        I("unlock", p => { p.Begin().Ellipse(10.4f, 5, 3.4f, 3.6f).CutEllipse(10.4f, 5, 1.8f, 2.2f); p.Flat(0xb9b2a8); p.Begin().Rect(2.6f, 6.6f, 8.8f, 7); p.Shade(K(0xc9ac7c)); });
        I("check", p => { p.Stroke([V(3, 8.4f), V(6.6f, 12), V(13, 4)], 2.6f, 0xf6eee0, 1, false); });
        I("x", p => { p.Line(V(4, 4), V(12, 12), 2.4f, 0xf6eee0, false); p.Line(V(12, 4), V(4, 12), 2.4f, 0xf6eee0, false); });
        I("gear", p => { for (int i = 0; i < 8; i++) { float a = i * M.TAU / 8; p.Begin().Ellipse(8 + M.Sin(a) * 4.6f, 8 + M.Cos(a) * 4.6f, 1.6f, 1.6f); p.Flat(0xb9b2a8); } p.Begin().Ellipse(8, 8, 4.6f, 4.6f).CutEllipse(8, 8, 1.8f, 1.8f); p.Shade(K(0xc9c2b8, 2)); });
        I("map", p => { p.Begin().Poly(V(1.6f, 4), V(6, 2.4f), V(10, 4), V(14.4f, 2.4f), V(14.4f, 12), V(10, 13.6f), V(6, 12), V(1.6f, 13.6f)); p.Shade(K(0xf3e2bd, 1)); p.Line(V(6, 3), V(6, 12), 1, 0xc9a06a, false); p.Line(V(10, 4), V(10, 13), 1, 0xc9a06a, false); p.Dot(12, 6, 0xb8473f); p.Dot(11, 7, 0xb8473f); p.Dot(12, 8, 0xb8473f); });
        I("bag", p => { p.Begin().Ellipse(8, 4.6f, 3, 2.8f).CutEllipse(8, 4.8f, 1.7f, 1.6f); p.Flat(0x6b4a33); p.Begin().Blob(V(2.6f, 12), V(3.6f, 6), V(12.4f, 6), V(13.4f, 12), V(8, 14)); p.Shade(K(0xb0764a, 3)); p.Begin().Rect(6.4f, 8, 3.2f, 2.6f); p.Flat(0xe0b650); });
        I("left", p => { p.Begin().Poly(V(4, 8), V(11, 3), V(11, 13)); p.Flat(0xf6eee0); });
        I("right", p => { p.Begin().Poly(V(12, 8), V(5, 3), V(5, 13)); p.Flat(0xf6eee0); });
        I("home", p => { p.Begin().Poly(V(1.6f, 8), V(8, 2), V(14.4f, 8)); p.Flat(0xb8473f); p.Begin().Rect(3.6f, 7.6f, 8.8f, 6); p.Shade(K(0xf3e2bd)); p.Begin().Rect(6.8f, 9.6f, 2.6f, 4); p.Flat(0x6b4a33); });
        I("sound", p => { p.Begin().Poly(V(2, 6), V(5, 6), V(9, 2.4f), V(9, 13.6f), V(5, 10), V(2, 10)); p.Shade(K(0xc9ac7c)); p.Curve(V(11, 5), V(13.6f, 8), V(11, 11), 1.2f, 0xf6eee0, false); });
        I("video", p => { p.Begin().Rect(1.6f, 3, 12.8f, 8.4f); p.Shade(K(0x5f8aa8)); p.Begin().Rect(6, 11.4f, 4, 1.4f).Rect(4, 12.8f, 8, 1.2f); p.Flat(0x6b4a33); });
        I("keys", p => { p.Begin().Rect(1.4f, 4.4f, 13.2f, 7.4f); p.Shade(K(0xc9c2b8)); for (int i = 0; i < 5; i++) { p.Begin().Rect(2.8f + i * 2.2f, 5.8f, 1.2f, 1.2f); p.Flat(0x4e4c52); } p.Begin().Rect(4.6f, 9, 7, 1.2f); p.Flat(0x4e4c52); });
        I("eye", p => { p.Begin().Blob(V(1.4f, 8), V(8, 3.6f), V(14.6f, 8), V(8, 12.4f)); p.Flat(0xfaf4ea); p.Begin().Ellipse(8, 8, 2.8f, 2.8f); p.Flat(0x4a82b0); p.Begin().Ellipse(8, 8, 1.2f, 1.2f); p.Flat(0x1a1014); });
        I("sun", p => { for (int i = 0; i < 8; i++) { float a = i * M.TAU / 8; p.Line(V(8, 8), V(8 + M.Sin(a) * 6.4f, 8 + M.Cos(a) * 6.4f), 1.2f, 0xf0bd4a, false); } p.Begin().Ellipse(8, 8, 3.8f, 3.8f); p.Shade(K(0xffd970)); });
        I("moon", p => { p.Begin().Ellipse(7.4f, 8, 5.4f, 5.4f).CutEllipse(10.2f, 6.4f, 4.6f, 4.6f); p.Shade(K(0xf0ead0)); });
        I("rain", p => { p.Begin().Ellipse(6, 6, 3.4f, 3).Ellipse(10, 6, 3.6f, 3.4f).Rect(4, 6, 8, 3); p.Shade(K(0xb0bcc8)); for (int i = 0; i < 3; i++) p.Line(V(5 + i * 3, 11), V(4 + i * 3, 14), 1, 0x6fa8d8, false); });
        I("storm", p => { p.Begin().Ellipse(6, 6, 3.4f, 3).Ellipse(10, 6, 3.6f, 3.4f).Rect(4, 6, 8, 3); p.Shade(K(0x6a6a82)); p.Begin().Poly(V(9, 8), V(6, 12), V(8, 12), V(7, 15), V(11, 10.6f), V(9, 10.6f)); p.Flat(0xffd970); });
        I("mist", p => { for (int i = 0; i < 3; i++) p.Stroke([V(2, 5 + i * 3.4f), V(6, 4 + i * 3.4f), V(10, 6 + i * 3.4f), V(14, 5 + i * 3.4f)], 1.4f, 0xd8e0e4, 1, false); });
        I("cloud", p => { p.Begin().Ellipse(5.6f, 9, 3.4f, 3).Ellipse(10, 7.6f, 4, 3.8f).Rect(4, 9, 8, 3); p.Shade(K(0xe8ecee)); });
        string[] em = ["wave", "dance", "laugh", "point", "cheer", "think", "sit", "sleep"];
        void Face(string n, Action<Painter> f) => I(n, p => { p.Begin().Ellipse(8, 8, 6, 6); p.Shade(K(0xf0c878, 3)); f(p); });
        I("wave", p => { p.Begin().Ellipse(8, 10, 3.6f, 3.6f); p.Shade(K(0xf0c4a0)); for (int i = 0; i < 4; i++) { p.Begin().Capsule(V(5.4f + i * 1.8f, 9), 1, V(4.4f + i * 2.3f, 3.6f + (i == 0 || i == 3 ? 1.4f : 0)), .9f); p.Shade(K(0xf0c4a0, 1)); } p.Begin().Capsule(V(5, 11), 1, V(2.4f, 8.6f), .9f); p.Shade(K(0xf0c4a0, 1)); });
        I("dance", p => { p.Begin().Ellipse(5, 12, 2.2f, 1.8f).Ellipse(12, 10.6f, 2.2f, 1.8f).Rect(6, 3.4f, 1.2f, 9).Rect(13, 2.4f, 1.2f, 8.6f); p.Flat(0x9a8ac8); p.Begin().Poly(V(6, 3.4f), V(14.2f, 2), V(14.2f, 4.4f), V(6, 5.8f)); p.Flat(0x9a8ac8); });
        Face("laugh", p => { p.Line(V(4, 6), V(6.4f, 7), 1, 0x3e2420, false); p.Line(V(12, 6), V(9.6f, 7), 1, 0x3e2420, false); p.Begin().Blob(V(4.4f, 9), V(11.6f, 9), V(8, 13)); p.Flat(0x8c2f35); });
        I("point", p => { p.Begin().Capsule(V(3, 10), 2.4f, V(7, 10), 2.4f).Capsule(V(6, 8.4f), 1.1f, V(13.6f, 8), 1); p.Shade(K(0xf0c4a0)); });
        I("cheer", p => { for (int i = 0; i < 6; i++) { float a = i * M.TAU / 6; p.Line(V(8 + M.Sin(a) * 3, 8 + M.Cos(a) * 3), V(8 + M.Sin(a) * 6.6f, 8 + M.Cos(a) * 6.6f), 1.4f, i % 2 == 0 ? 0xf0bd4a : 0xd86f86, false); } p.Begin().Ellipse(8, 8, 2, 2); p.Flat(0xffe9a8); });
        Face("think", p => { p.Dot(6, 6, 0x3e2420); p.Dot(10, 5, 0x3e2420); p.Line(V(6, 11), V(10, 10.4f), 1, 0x8c2f35, false); p.Begin().Ellipse(13.4f, 2.6f, 1.6f, 1.6f); p.Flat(0xfaf4ea); });
        I("sit", p => { p.Begin().Rect(2, 8, 12, 2).Rect(3, 10, 2, 4).Rect(11, 10, 2, 4).Rect(2, 3.4f, 12, 2); p.Shade(K(0xa8693c, 1)); });
        I("sleep", p => { void Z(float x, float y, float s) { p.Line(V(x, y), V(x + s, y), 1.2f, 0x9ec3cf, false); p.Line(V(x + s, y), V(x, y + s), 1.2f, 0x9ec3cf, false); p.Line(V(x, y + s), V(x + s, y + s), 1.2f, 0x9ec3cf, false); } Z(2, 8, 5); Z(9, 2.4f, 4); });
        I("bow", p => { p.Begin().Ellipse(11, 6, 2.2f, 2.2f); p.Shade(K(0xe8b48e)); p.Begin().Capsule(V(9, 8), 1.8f, V(5, 9), 1.8f); p.Shade(K(0x5f8aa8)); p.Begin().Capsule(V(5, 10), 1.4f, V(5, 14), 1.2f); p.Shade(K(0x4a6285)); });
        I("shrug", p => { p.Begin().Ellipse(8, 4.6f, 2.2f, 2.2f); p.Shade(K(0xe8b48e)); p.Begin().Capsule(V(8, 9), 2.2f, V(8, 12), 2); p.Shade(K(0xb8473f)); p.Line(V(5, 8), V(2.4f, 10), 1.6f, 0xe8b48e, false); p.Line(V(2.4f, 10), V(1.6f, 7), 1.4f, 0xe8b48e, false); p.Line(V(11, 8), V(13.6f, 10), 1.6f, 0xe8b48e, false); p.Line(V(13.6f, 10), V(14.4f, 7), 1.4f, 0xe8b48e, false); });
        // tattoo studio tools
        I("tBrush", p => { p.Begin().Capsule(V(13, 2.5f), 1.5f, V(8.2f, 7.6f), 1.3f); p.Shade(K(0xa8693c, 1)); p.Begin().Capsule(V(8.6f, 7.2f), 1.7f, V(6.6f, 9.2f), 1.7f); p.Shade(K(0xc9ccd2, 1)); p.Begin().Poly(V(5.4f, 8.2f), V(7.6f, 10.4f), V(2.2f, 14.2f)); p.Flat(0x2a2c38); });
        I("tEraser", p => { p.Begin().Poly(V(2.5f, 10), V(8.5f, 4), V(13.5f, 9), V(7.5f, 15)); p.Shade(K(0xe88a9a, 1)); p.Begin().Poly(V(5.8f, 6.7f), V(8.5f, 4), V(13.5f, 9), V(10.8f, 11.7f)); p.Shade(K(0x6f9ac0, 1)); });
        I("tFill", p => { p.Begin().Poly(V(2.5f, 7), V(9.5f, 3.2f), V(13, 9.6f), V(6, 13.4f)); p.Shade(K(0xc9ccd2, 1)); p.Begin().Poly(V(2.5f, 7), V(9.5f, 3.2f), V(10.4f, 4.9f), V(3.4f, 8.7f)); p.Flat(0x2a2c38); p.Begin().Ellipse(13.4f, 13, 1.9f, 1.9f).Poly(V(11.8f, 12.2f), V(13.4f, 8.6f), V(15, 12.2f)); p.Flat(0x3a72b8); });
        I("tLine", p => { p.Line(V(3.5f, 12.5f), V(12.5f, 3.5f), 1.7f, 0xf6eee0, false); p.Begin().Ellipse(3.2f, 12.8f, 1.8f, 1.8f).Ellipse(12.8f, 3.2f, 1.8f, 1.8f); p.Flat(0xf0b64e); });
        I("tBox", p => { p.Begin().Rect(2.5f, 3.5f, 11, 9).Rect(4.3f, 5.3f, 7.4f, 5.4f, 0); p.Flat(0xf6eee0); });
        I("tCircle", p => { p.Begin().Ellipse(8, 8, 5.8f, 5.8f).CutEllipse(8, 8, 4, 4); p.Flat(0xf6eee0); });
        I("tKnife", p => { p.Begin().Poly(V(1.8f, 14.4f), V(9.6f, 5.6f), V(11.6f, 7.6f)); p.Shade(K(0xdfe3e8, 1)); p.Begin().Capsule(V(10.6f, 5.4f), 1.6f, V(13.8f, 2.2f), 1.6f); p.Shade(K(0xa8693c, 1)); });
        I("tSelect", p => { p.Begin(); for (int i = 0; i < 4; i++) { float o = 2.5f + i * 3; p.Rect(o, 2.5f, 1.8f, 1.5f).Rect(o, 12, 1.8f, 1.5f).Rect(2.5f, o, 1.5f, 1.8f).Rect(12, o, 1.5f, 1.8f); } p.Flat(0xf6eee0); });
        I("tPicker", p => { p.Begin().Capsule(V(3, 13), .8f, V(9.6f, 6.4f), 1.4f); p.Shade(K(0xc9d6e0, 1)); p.Begin().Ellipse(11.8f, 4.2f, 2.6f, 2.6f); p.Shade(K(0xc8384a, 1)); });
        I("tHand", p => { p.Begin().Ellipse(8.4f, 10.6f, 4.2f, 3.8f).Capsule(V(5.2f, 9), 1.1f, V(5.2f, 4), 1.1f).Capsule(V(7.6f, 8), 1.1f, V(7.6f, 2.4f), 1.1f).Capsule(V(10, 8), 1.1f, V(10, 3), 1.1f).Capsule(V(12.2f, 9), 1, V(12.3f, 5), 1).Capsule(V(5, 12), 1.1f, V(2.2f, 8.6f), 1.1f); p.Shade(K(0xf0c4a0, 1.5f)); });
        I("tMirA", p => { p.Begin().Poly(V(1.5f, 8), V(6.5f, 4), V(6.5f, 12)).Poly(V(14.5f, 8), V(9.5f, 4), V(9.5f, 12)); p.Flat(0xf6eee0); p.Line(V(8, 2), V(8, 14), 1, 0xf0b64e, false); });
        I("tMirC", p => { p.Begin().Poly(V(8, 1.5f), V(4, 6.5f), V(12, 6.5f)).Poly(V(8, 14.5f), V(4, 9.5f), V(12, 9.5f)); p.Flat(0xf6eee0); p.Line(V(2, 8), V(14, 8), 1, 0xf0b64e, false); });
        I("tRepeat", p => { p.Begin().Rect(1.5f, 5.5f, 3.6f, 5).Rect(6.2f, 5.5f, 3.6f, 5).Rect(10.9f, 5.5f, 3.6f, 5); p.Flat(0xf6eee0); });
        I("zin", p => { p.Begin().Ellipse(6.8f, 6.8f, 5, 5).CutEllipse(6.8f, 6.8f, 3.4f, 3.4f).Capsule(V(10.4f, 10.4f), 1.4f, V(13.8f, 13.8f), 1.4f); p.Flat(0xf6eee0); p.Line(V(4.8f, 6.8f), V(8.8f, 6.8f), 1.2f, 0xf0b64e, false); p.Line(V(6.8f, 4.8f), V(6.8f, 8.8f), 1.2f, 0xf0b64e, false); });
        I("zout", p => { p.Begin().Ellipse(6.8f, 6.8f, 5, 5).CutEllipse(6.8f, 6.8f, 3.4f, 3.4f).Capsule(V(10.4f, 10.4f), 1.4f, V(13.8f, 13.8f), 1.4f); p.Flat(0xf6eee0); p.Line(V(4.8f, 6.8f), V(8.8f, 6.8f), 1.2f, 0xf0b64e, false); });
    }
}

using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

struct Puff { public Vector2 P, V; public float T, L, S, R; public Color C; public int K; }
sealed class Puffs
{
    readonly List<Puff> l = new(); readonly Rng r = new(3);
    public void Add(Vector2 p, Vector2 v, float life, float s, Color c, int kind = 0) { if (l.Count < 400) l.Add(new Puff { P = p, V = v, L = life, S = s, C = c, K = kind, R = r.R(0, 6) }); }
    public void Burst(Vector2 p, int n, float sp, Func<int, Color> c, int kind = 0, float g = 0) { for (int i = 0; i < n; i++) { float a = r.R(0, M.TAU); Add(p, new Vector2(M.Sin(a), M.Cos(a)) * r.R(.3f, 1) * sp, r.R(.4f, 1f), r.R(1.5f, 4), c(i), kind); } }
    public void Draw(float dt)
    {
        for (int i = l.Count - 1; i >= 0; i--)
        {
            var p = l[i]; p.T += dt; if (p.T > p.L) { l.RemoveAt(i); continue; }
            p.V *= MathF.Exp(-dt * (p.K == 2 ? 1.2f : 3.5f)); if (p.K == 2) p.V.Y += 160 * dt; p.P += p.V * dt; l[i] = p; float k = p.T / p.L; var c = Raylib.Fade(p.C, (1 - k) * p.C.A / 255f);
            if (p.K == 0) { float s = p.S * (.6f + k * 1.2f); Raylib.DrawCircleV(p.P, s + .8f, Raylib.Fade(Ui.Ink, (1 - k) * .25f)); Raylib.DrawCircleV(p.P, s, c); }
            else if (p.K == 1) { float s = p.S * (1 - k); Raylib.DrawRectangleV(p.P - new Vector2(s, .5f), new(s * 2, 1), c); Raylib.DrawRectangleV(p.P - new Vector2(.5f, s), new(1, s * 2), c); }
            else Raylib.DrawRectanglePro(new Rectangle(p.P.X, p.P.Y, p.S, p.S * .6f), new(p.S / 2, p.S * .3f), (p.R + p.T * 5) * 57, c);
        }
    }
}

/// The avatar wardrobe: room and character on the left, item drawers on the right.
public sealed class EditorScreen
{
    public AvatarData D; public Action Done; public Action Back; public bool CanBack; readonly AvatarPainter ap = new(); readonly Painter pt = new(250, 350); Texture2D tex, room; readonly AvatarAnim an = new() { Fps = 12 }; readonly Puffs fx = new(); readonly Rng rng = new(Environment.TickCount);
    const float S = 2.62f; int tab; readonly int[] sub = new int[12]; float scroll, scrollT; int chip; bool fan; float fanSpin, fanPow; int preview; float poseT; float tabSlide = 1; string name; bool typing; float shake; float finishT = -1; readonly List<AvatarData> undo = new(), redo = new(); readonly HashSet<string> locks = new(); bool outfits; float diceRot; float introT, taken; bool sliderUndo; float avX;
    static readonly string[] Tabs = ["Body", "Face", "Hair", "Tops", "Jackets", "Bottoms", "Shoes", "Hats", "Extras", "Style"]; static readonly string[] TabIcons = ["body", "face", "hair", "top", "jacket", "pants", "shoes", "hat", "extras", "style"];
    readonly Dictionary<string, (Texture2D t, int hash)> thumbs = new(); readonly Painter tp = new(96, 92); int budget;

    public EditorScreen() { tex = pt.ToTexture(); room = Room.Paint().ToTexture(); }
    public void Open(AvatarData d, bool canBack)
    {
        D = d.Clone(); CanBack = canBack; name = D.Name ?? ""; undo.Clear(); redo.Clear(); finishT = -1; introT = 0; typing = false; outfits = false; tab = 0; chip = 0; preview = 0; an.Vel = default; Redraw();
    }
    public void SetNameForTest(string s) => name = s;
    public Vector2 AvatarCenter => new(avX, 190);
    void Redraw() { pt.Clear(); ap.Draw(pt, D, an.Pose, S, 125, 336, outlinePx: 2); pt.Upload(tex); }

    void Snap() { undo.Add(D.Clone()); if (undo.Count > 60) undo.RemoveAt(0); redo.Clear(); }
    void Changed(int admire, bool pop = true)
    {
        if (pop) { an.Punch(5.5f); Snd.Play("cloth", .7f, .9f + rng.F() * .25f); an.Gust(rng.R(-1, 1)); var c = new Vector2(avX, 300); for (int i = 0; i < 10; i++) fx.Add(c + new Vector2(rng.R(-34, 34), rng.R(-4, 8)), new(rng.R(-60, 60), rng.R(-26, 0)), rng.R(.4f, .8f), rng.R(2, 5), new Color(238, 218, 186, 190)); for (int i = 0; i < 6; i++) fx.Add(new Vector2(avX + rng.R(-50, 50), rng.R(90, 260)), new(0, rng.R(-30, -6)), rng.R(.5f, 1), rng.R(3, 6), new Color(255, 226, 140, 255), 1); }
        if (admire >= 0 && preview == 0) { an.AdmireSlot = admire; an.Set(Anim.Admire); an.StateT = 0; poseT = 1.15f; }
    }
    static bool ValidName(string s) => s.Trim().Length is >= 3 and <= 10;

    public void Update(float dt)
    {
        introT += dt; budget = 3; an.IdleStyle = D.Idle; an.Stride = AvatarPainter.StrideOf(D); fanPow = M.Damp(fanPow, fan ? 1 : 0, 3, dt); an.Wind = fanPow * 2.3f + .12f; fanSpin += fanPow * dt * 26;
        poseT -= dt; if (finishT < 0) { if (preview == 0) { if (poseT <= 0) an.Set(Anim.Idle); an.Vel = M.Damp(an.Vel, default, 10, dt); } else { an.Dir = preview == 1 ? 1 : -1; an.Vel = new(an.Dir * (preview == 1 ? 64 : 64), 0); an.Set(Anim.Walk); } }
        if (an.Update(dt)) Redraw();
        if (typing)
        {
            int c; while ((c = Raylib.GetCharPressed()) > 0) if (name.Length < 10 && (char.IsAsciiLetterOrDigit((char)c) || c is ' ' or '_' or '-')) { name += (char)c; Snd.Play("type", .8f, .9f + rng.F() * .3f); } else { shake = .25f; Snd.Play("error", .4f); }
            if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && name.Length > 0) { name = name[..^1]; Snd.Play("type", .7f, .75f); }
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Escape)) typing = false;
        }
        else if (finishT < 0)
        {
            if (Raylib.IsKeyDown(KeyboardKey.LeftControl)) { if (Raylib.IsKeyPressed(KeyboardKey.Z)) Undo(); if (Raylib.IsKeyPressed(KeyboardKey.Y)) Redo(); }
            if (CanBack && Raylib.IsKeyPressed(KeyboardKey.Escape)) Back?.Invoke();
        }
        shake = MathF.Max(0, shake - dt);
        if (finishT >= 0) { finishT += dt; if (finishT > 1.25f) { finishT = -100; Done?.Invoke(); } }
        Snd.Loop("room", .35f);
    }
    void Undo() { if (undo.Count == 0) return; redo.Add(D.Clone()); D = undo[^1]; undo.RemoveAt(undo.Count - 1); Snd.Play("undo"); Changed(-1); }
    void Redo() { if (redo.Count == 0) return; undo.Add(D.Clone()); D = redo[^1]; redo.RemoveAt(redo.Count - 1); Snd.Play("undo", 1, 1.2f); Changed(-1); }
    void Finish()
    {
        if (ValidName(name) && Saves.NameTaken(name)) { shake = .45f; taken = 3; Snd.Play("error"); typing = true; return; }
        if (!ValidName(name)) { shake = .45f; Snd.Play("error"); typing = true; return; }
        D.Name = name.Trim(); typing = false; finishT = 0; an.Set(Anim.ShowOff); an.Punch(9); Snd.Play("finish"); var cols = new[] { Ui.Gold, Ui.Orange, Ui.Green, new Color(216, 111, 134, 255), new Color(126, 168, 201, 255) };
        for (int i = 0; i < 70; i++) fx.Add(new(avX + rng.R(-30, 30), 200), new(rng.R(-190, 190), rng.R(-260, -60)), rng.R(.9f, 1.6f), rng.R(3, 6), cols[i % 5], 2);
    }

    // ---------------------------------------------------------------- drawing
    public void Draw()
    {
        float W = Ui.VW, H = Ui.VH, t = Ui.T; float px = W - 336; avX = M.Clamp((px - 34) / 2, 110, 260); float rx = avX - 170, off = H - 360;
        Raylib.DrawRectangle(0, 0, (int)W + 1, (int)H + 1, new Color(58, 38, 40, 255)); Ui.Tex(room, rx - 100, 0, 1, Color.White);
        // warm light: lamp, string lights and window beams, all breathing a little
        Raylib.BeginBlendMode(BlendMode.Additive); float fl = .92f + .05f * M.Sin(t * 5.1f) + .03f * M.Sin(t * 13.7f);
        Ui.GlowAt(rx + 40, 168, 150 * fl, new Color(255, 178, 92, 90)); Ui.GlowAt(rx + 40, 166, 46, new Color(255, 220, 150, 120)); Ui.GlowAt(avX, 250, 210, new Color(255, 170, 90, 34)); Ui.GlowAt(rx + 96, 92, 90, new Color(255, 190, 130, 46));
        for (int i = 0; i < 11; i++) { float lx = rx - 20 + i * 42, ly = 16 + MathF.Abs(M.Sin(i * 1.57f + .8f)) * 9; Col lc = i % 3 == 0 ? 0xffd27a : i % 3 == 1 ? 0xffa870 : 0xffe6a8; float tw = .75f + .25f * M.Sin(t * 2 + i * 2.1f); Ui.GlowAt(lx, ly + 5, 18, lc.Rl(.5f * tw)); }
        var beam = new Color(255, 196, 130, 22); for (int i = 0; i < 3; i++) { float bx = rx + 62 + i * 26; Raylib.DrawTriangle(new(bx, 60), new(bx + 80, 320), new(bx + 112, 320), beam); Raylib.DrawTriangle(new(bx, 60), new(bx + 112, 320), new(bx + 22, 60), beam); }
        for (int i = 0; i < 26; i++) { float q = M.Hash(i, 1), xx = rx + 40 + M.Hash(i, 2) * 230 + M.Sin(t * .4f + i) * 12, yy = 60 + (M.Hash(i, 3) * 250 + t * (3 + q * 6)) % 250; float al = .2f + .5f * MathF.Abs(M.Sin(t * .7f + i * 2)); Raylib.DrawRectangleV(new(xx, yy), new(1, 1), new Color(255, 226, 170, (int)(al * 160))); }
        Raylib.EndBlendMode();
        if (fanPow > .02f) Fan(rx);
        // avatar on the rug, squash and stretch from the feet
        float sq = M.Clamp(an.Squash, -.35f, .35f), ap2 = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp(introT * 1.6f)); float sx = (1 + sq * .55f) * ap2, sy = (1 - sq * .6f) * ap2, gy = 318;
        Raylib.DrawEllipse((int)avX, (int)gy, 44 * sx, 7, new Color(40, 20, 26, 90)); Raylib.DrawTexturePro(tex, new Rectangle(0, 0, pt.W, pt.H), new Rectangle(avX - 125 * sx, gy - 336 * sy, pt.W * sx, pt.H * sy), Vector2.Zero, 0, Color.White);
        fx.Draw(Ui.DT);
        Raylib.DrawRectangleGradientV(0, 290, (int)W, 72, new Color(30, 12, 20, 0), new Color(30, 12, 20, 110)); Raylib.DrawRectangleGradientH(0, 0, 60, (int)H, new Color(30, 12, 20, 110), new Color(30, 12, 20, 0));
        bool en = finishT < 0; Ui.Enabled = en;
        Left(rx); float slide = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp((introT - .15f) * 1.8f), 1.1f); if (finishT > .5f) slide = 1 - Ease.InCubic(M.Clamp((finishT - .5f) * 2));
        Rlgl.PushMatrix(); Rlgl.Translatef((1 - slide) * 380, 0, 0); RightPanel(px); Rlgl.PopMatrix();
        if (outfits) OutfitPanel();
        if (CanBack && finishT < 0 && Ui.Button("edback", null, 10, 12, 30, 24, "btn", "left", "Back")) Back?.Invoke();
        if (finishT > 0) { float k = Ease.OutBack(M.Clamp(finishT * 2.5f)); Ui.PushScale(avX, 44, k, k); Font.Draw("Looking good, " + D.Name + "!", avX, 36, Ui.Cream, 2, .5f, Ui.Ink, 1); Ui.PopScale(); }
    }
    void Fan(float rx)
    {
        float x = rx - 2, y = 262; Raylib.DrawRectangleV(new(x - 2, y), new(5, 50), Ui.Ink); Raylib.DrawRectangleV(new(x - 1, y), new(3, 50), new Color(140, 150, 160, 255)); Raylib.DrawEllipse((int)x, 313, 15, 4, Ui.Ink); Raylib.DrawEllipse((int)x, 312, 13, 3, new Color(150, 160, 170, 255));
        Raylib.DrawEllipse((int)x, (int)y, 15, 23, Ui.Ink); Raylib.DrawEllipse((int)x, (int)y, 13.5f, 21.5f, new Color(74, 92, 112, 255)); for (int i = 0; i < 3; i++) { float a = fanSpin + i * M.TAU / 3; Raylib.DrawEllipse((int)(x + M.Sin(a) * 6), (int)(y + M.Cos(a) * 11), 5.5f, 8, new Color(158, 195, 207, 200)); } Raylib.DrawCircleV(new(x, y), 3, Ui.Gold);
        if (rng.P(fanPow * .6f)) fx.Add(new(x + 14, y + rng.R(-130, 30)), new(rng.R(180, 320), rng.R(-8, 8)), .7f, 1.2f, new Color(255, 250, 235, 90));
    }
    void Left(float rx)
    {
        float W = Ui.VW; float sh = shake > 0 ? M.Sin(shake * 70) * shake * 14 : 0; float nx = avX - 70 + sh; float k = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp((introT - .3f) * 2));
        Rlgl.PushMatrix(); Rlgl.Translatef(0, (1 - k) * -60, 0);
        var (a, hit, o) = Ui.Widget("name", nx, 10, 140, 24); if (hit) { typing = true; while (Raylib.GetCharPressed() > 0) { } } else if (Ui.Click && !o) typing = false;
        a.Sel.Update(typing ? 1 : 0, 300, 16, Ui.DT); Ui.PushScale(nx + 70, 22, 1 + a.Hover.X * .03f + a.Sel.X * .05f, 1 + a.Hover.X * .03f + a.Sel.X * .05f); Ui.Nine("paper", nx, 8, 140, 28, Color.White);
        string shown = name.Length == 0 && !typing ? "Your name..." : name; float tw = Font.Width(shown, 1.5f); Font.Draw(shown, nx + 70, 15, name.Length == 0 && !typing ? Ui.Soft : Ui.Ink, 1.5f, .5f); if (typing && (Ui.T % .9f) < .5f) Raylib.DrawRectangleV(new(nx + 71 + tw / 2 + (name.Length > 0 ? 1 : -tw / 2), 15), new(1.5f, 12), Ui.Orange);
        Font.Draw(name.Length + "/10", nx + 132, 18, ValidName(name) ? Ui.Green : Ui.Orange, 1, 1); taken -= Ui.DT; if (taken > 0) Font.Draw("You already have a " + name.Trim(), nx + 70, 38, new Color(255, 170, 140, 255), 1, .5f, Ui.Ink); else if (!ValidName(name)) Font.Draw("3 to 10 letters", nx + 70, 38, new Color(255, 214, 170, 255), 1, .5f, Ui.Ink); Ui.PopScale(); Rlgl.PopMatrix();
        float by = 326, x = avX - 108; Rlgl.PushMatrix(); Rlgl.Translatef(0, (1 - k) * 60, 0);
        if (Ui.Button("rand", null, x, by, 34, 28, "btnP", null, "Randomize (unlocked tabs)")) { Snap(); D = AvatarData.Random(rng, D, s => locks.Contains(s)); D.Name = name; diceRot = 1; Snd.Play("dice"); Changed(4); for (int i = 0; i < 14; i++) fx.Add(new(x + 17, by), new(rng.R(-90, 90), rng.R(-120, -20)), .8f, 4, Ui.Gold, 1); }
        diceRot = M.Damp(diceRot, 0, 3, Ui.DT); Ui.PushScale(x + 17, by + 13, 1 + diceRot * .5f, 1 + diceRot * .5f, diceRot * 720); Ui.Icon("dice", x + 9, by + 5); Ui.PopScale();
        if (Ui.Button("undo", null, x + 37, by, 28, 28, "btn", "undo", "Undo (Ctrl+Z)", disabled: undo.Count == 0)) Undo();
        if (Ui.Button("redo", null, x + 67, by, 28, 28, "btn", "redo", "Redo (Ctrl+Y)", disabled: redo.Count == 0)) Redo();
        if (Ui.Button("flip", null, x + 99, by, 28, 28, "btnB", "flip", preview == 0 ? "Rotate: walk right" : preview == 1 ? "Rotate: walk left" : "Rotate: face front")) { preview = (preview + 1) % 3; Snd.Play("whoosh", .6f); an.Punch(4); }
        if (Ui.Button("fan", null, x + 129, by, 28, 28, "btnB", "fan", "Wind fan", fan)) { fan = !fan; }
        if (Ui.Button("outfits", null, x + 161, by, 28, 28, "btnG", "save", "Saved outfits", outfits)) { outfits = !outfits; Snd.Play(outfits ? "open" : "close"); }
        if (Ui.Button("voice", null, x + 191, by, 28, 28, "btnP", "sound", "Say hello")) { Hello(); }
        Rlgl.PopMatrix();
    }
    void Hello() { an.Set(Anim.Wave); an.StateT = 0; poseT = 1.3f; preview = 0; Voice.Say(D.Voice, 5); }

    void RightPanel(float px)
    {
        float py = 6, pw = 330, ph = 348; tabSlide = M.Damp(tabSlide, 1, 11, Ui.DT);
        // category tabs
        for (int i = 0; i < 10; i++)
        {
            float tx = px - 27, ty = py + 8 + i * 33; var (a, hit, o) = Ui.Widget("tab" + i, tx, ty, 34, 30, "tab"); a.Sel.Update(tab == i ? 1 : 0, 320, 17, Ui.DT); if (hit && tab != i) { tab = i; scrollT = scroll = 0; chip = 0; tabSlide = 0; typing = false; }
            float ex = a.Hover.X * 4 + a.Sel.X * 7; Ui.PushScale(tx + 17, ty + 15, 1 + a.Press.X * .2f, 1 - a.Press.X * .2f); Ui.Nine(tab == i ? "tabOn" : "tab", tx - ex, ty, 38 + ex, 30, Color.White); Ui.Icon(TabIcons[i], tx + 5 - ex, ty + 6.5f - MathF.Abs(M.Sin(Ui.T * 6)) * a.Hover.X * 2, 1); Ui.PopScale(); if (o) Ui.Tip = Tabs[i];
            if (locks.Contains(Tabs[i])) Ui.Icon("lock", tx - ex - 4, ty + 15, .7f);
        }
        Ui.Nine("panel", px, py, pw, ph, Color.White); float x = px + 14, w = pw - 28; float sl = Store.Cfg.ReduceMotion ? 0 : (1 - tabSlide) * 26; float al = M.Clamp(tabSlide * 1.5f);
        Font.Draw(Tabs[tab], x + 2 + sl, py + 13, Ui.Ink, 2); bool lk = locks.Contains(Tabs[tab]);
        if (tab < 9 && Ui.Button("lockbtn", null, px + pw - 40, py + 11, 24, 20, "btnP", lk ? "lock" : "unlock", lk ? "Locked: randomize skips this" : "Lock for randomize", lk)) { if (!locks.Remove(Tabs[tab])) locks.Add(Tabs[tab]); }
        Rlgl.PushMatrix(); Rlgl.Translatef(sl, 0, 0); float y = py + 34;
        switch (tab)
        {
            case 0: BodyTab(x, y, w); break;
            case 1: FaceTab(x, y, w); break;
            case 2: HairTab(x, y, w); break;
            case 3: PieceTab(Slot.Top, x, y, w, 0, Catalog.Tops.Length, 0); break;
            case 4: PieceTab(Slot.Jacket, x, y, w, 0, Catalog.Jackets.Length, 1); break;
            case 5: { var s = Subs(["Pants", "Shorts", "Skirts"], x, y, w); PieceTab(Slot.Bottom, x, y + 22, w, s * 30, 30, 2); break; }
            case 6: PieceTab(Slot.Shoes, x, y, w, 0, Catalog.Shoes.Length, 3); break;
            case 7: PieceTab(Slot.Hat, x, y, w, 0, Catalog.Hats.Length, 4); break;
            case 8: { var s = Subs(["Glasses", "Ears", "Neck", "Back", "Hands"], x, y, w); var slot = new[] { Slot.Glasses, Slot.Earrings, Slot.Neck, Slot.Back, Slot.Gloves }[s]; PieceTab(slot, x, y + 22, w, 0, Catalog.Count(slot), s == 0 ? 5 : 1); break; }
            case 9: StyleTab(x, y, w); break;
        }
        Rlgl.PopMatrix();
        if (Ui.Button("finish", "Create", px + pw - 108, py + ph - 37, 92, 26, "btnG", "check", ValidName(name) ? null : "Pick a name first", text: 2)) Finish();
    }
    int Subs(string[] n, float x, float y, float w)
    {
        float bw = (w - (n.Length - 1) * 3) / n.Length; for (int i = 0; i < n.Length; i++) if (Ui.Button("sub" + tab + i, n[i], x + i * (bw + 3), y, bw, 18, "btnP", on: sub[tab] == i)) { if (sub[tab] != i) { sub[tab] = i; scroll = scrollT = 0; chip = 0; } } return sub[tab];
    }

    /// Scrollable grid of live preview cards. Returns clicked index or -1.
    int Grid(string id, int count, Func<int, bool> sel, Func<int, (Texture2D, bool)> thumb, Func<int, string> nm, float x, float y, float w, float h)
    {
        const int cols = 5; float cw = (w - 8 - (cols - 1) * 4) / cols, ch = 54, rowH = ch + 4; int rows = (count + cols - 1) / cols; float max = MathF.Max(0, rows * rowH - h); bool inside = Ui.Over(x, y, w, h);
        if (inside) scrollT = M.Clamp(scrollT - Ui.Wheel * 42, 0, max); scrollT = M.Clamp(scrollT, 0, max); scroll = M.Damp(scroll, scrollT, 14, Ui.DT); Ui.Nine("slot", x - 4, y - 4, w + 8, h + 8, Color.White);
        int clicked = -1; float tx = 0; Ui.Clip(x - 3 + (Store.Cfg.ReduceMotion ? 0 : (1 - tabSlide) * 26), y - 3, w + 6, h + 6); bool en = Ui.Enabled; if (!inside) Ui.Enabled = false;
        for (int i = 0; i < count; i++)
        {
            float cx = x + (i % cols) * (cw + 4), cy = y + (i / cols) * rowH - scroll; if (cy > y + h || cy + ch < y) continue; bool s = sel(i); var (a, hit, o) = Ui.Card(id + i, cx, cy, cw, ch, s); var (tex2, ready) = thumb(i);
            if (ready) Raylib.DrawTexturePro(tex2, new Rectangle(0, 0, tex2.Width, tex2.Height), new Rectangle(cx + (cw - 48) / 2, cy + 4 - a.Hover.X * 1.5f, 48, 46), Vector2.Zero, 0, Color.White); else Font.Draw("...", cx + cw / 2, cy + 20, Ui.Soft, 1, .5f);
            Ui.PopScale(); if (hit) clicked = i; if (o) Ui.Tip = nm(i);
        }
        Ui.Enabled = en; Ui.Unclip(); _ = tx;
        if (max > 0) { float bh = MathF.Max(16, h * h / (rows * rowH)), by = y + (h - bh) * (scroll / max); Raylib.DrawRectangleRounded(new Rectangle(x + w - 4, y, 4, h), 1, 4, new Color(160, 120, 84, 120)); var (a2, _, _) = Ui.Widget(id + "bar", x + w - 6, y, 9, h, null, true); if (Ui.Active == id + "bar" && Ui.Held) scrollT = M.Clamp((Ui.Mouse.Y - y - bh / 2) / (h - bh)) * max; Raylib.DrawRectangleRounded(new Rectangle(x + w - 4 - a2.Hover.X, by, 4 + a2.Hover.X * 2, bh), 1, 4, Ui.Orange); }
        return clicked;
    }
    (Texture2D, bool) Thumb(string key, int hash, Action<Painter> draw)
    {
        if (thumbs.TryGetValue(key, out var t)) { if (t.hash != hash && budget > 0) { budget--; tp.Clear(); draw(tp); tp.Upload(t.t); thumbs[key] = (t.t, hash); } return (t.t, true); }
        if (budget <= 0) return (default, false); budget--; tp.Clear(); draw(tp); var nt = tp.ToTexture(); thumbs[key] = (nt, hash); return (nt, true);
    }
    static int HashP(Piece p) => HashCode.Combine(p.A, p.B, p.C, p.Pat, p.Scale);
    (Texture2D, bool) PieceThumb(Slot s, int id)
    {
        var pc = D[s]; return Thumb(s + "_" + id, HashCode.Combine(HashP(pc), s == Slot.Beard ? D.Skin : 0), p =>
        {
            var d = new AvatarData(); foreach (var q in d.P) q.Id = 0; d.P[(int)s] = pc.Clone(); d[s].Id = id; d.OutlineCol = 0x3e2420; var po = Pose.Stand; if (s is Slot.Top or Slot.Jacket) { po.ArmL0 = -.32f; po.ArmL1 = -.2f; po.ArmR0 = .32f; po.ArmR1 = .2f; }
            (float sc, float gy) v = s switch { Slot.Top => (1.75f, 166), Slot.Jacket => (1.5f, 144), Slot.Bottom => (1.62f, 88), Slot.Shoes => (3.4f, 84), Slot.Hat => (2.8f, 300), Slot.Glasses => (3.6f, 372), Slot.Earrings => (3f, 312), Slot.Neck => (2f, 200), Slot.Back => (1.55f, 150), Slot.Gloves => (1.5f, 130), _ => (3.6f, 366) };
            if (s == Slot.Shoes) d[Slot.Bottom].Id = 30; float ox = s == Slot.Gloves ? 48 : 48; ap.Draw(p, d, po, v.sc, ox, v.gy, Show.Mannequin, s, 1);
        });
    }
    void PieceTab(Slot s, float x, float y, float w, int first, int count, int admire)
    {
        float gridH = 344 - (y - 6) - 148; int i = Grid("g" + s + first, count, k => D[s].Id == first + k, k => PieceThumb(s, first + k), k => Catalog.NameOf(s, first + k), x, y + 4, w, gridH);
        if (i >= 0 && D[s].Id != first + i) { Snap(); D[s].Id = first + i; Changed(admire); }
        ColorSection(s, x, y + gridH + 14, w, admire);
    }
    void ColorSection(Slot s, float x, float y, float w, int admire)
    {
        var p = D[s]; string nm = Catalog.NameOf(s, p.Id); Font.Draw(nm, x, y, Ui.Ink, 1.25f);
        bool none = p.Id == 0 && s is not (Slot.Top or Slot.Bottom or Slot.Shoes); if (none) { Font.Draw("Pick something to change its colors.", x, y + 18, Ui.Soft); return; }
        string[] z = ["Main", "Accent", "Trim"]; int[] cs = [p.A, p.B, p.C];
        for (int k = 0; k < 3; k++) Chip("chip" + k, z[k], cs[k], x + k * 52, y + 14, chip == k, () => chip = k, 50);
        if (s is Slot.Top or Slot.Jacket or Slot.Bottom or Slot.Hat or Slot.Neck)
        {
            float bx = x + 158; if (Ui.Button("patL", null, bx, y + 14, 18, 18, "btnP", "left")) { Snap(); p.Pat = M.Mod(p.Pat - 1, Patterns.Names.Length); Changed(admire); }
            PatSwatch(p, bx + 20, y + 14, 62); if (Ui.Button("patR", null, bx + 84, y + 14, 18, 18, "btnP", "right")) { Snap(); p.Pat = M.Mod(p.Pat + 1, Patterns.Names.Length); Changed(admire); }
            float sc = (p.Scale - .5f) / 1.5f; if (p.Pat > 0) { if (Ui.Slider("pats", ref sc, bx + 108, y + 18, 32)) { p.Scale = .5f + sc * 1.5f; } if (Ui.Over(bx + 104, y + 14, 40, 18)) Ui.Tip = "Pattern size"; }
        }
        Palette(AvatarData.Cloth, cs[Math.Min(chip, 2)], c => { if (chip == 0) p.A = c; else if (chip == 1) p.B = c; else p.C = c; }, x, y + 37, w, admire);
    }
    static readonly Painter sw = new(62, 18); Texture2D swT; int swH;
    void PatSwatch(Piece p, float x, float y, float w)
    {
        int h = HashP(p); if (swT.Id == 0 || h != swH) { swH = h; for (int j = 0; j < 18; j++) for (int i = 0; i < 62; i++) { bool e = i is 0 or 61 || j is 0 or 17; sw.Px[j * 62 + i] = e ? Ui.Ink : Patterns.Get(p.Pat, (i - 31) / 1.6f, j / 1.6f, p.A, p.B, p.C, p.Scale).Rl(); } if (swT.Id == 0) swT = sw.ToTexture(); else sw.Upload(swT); }
        Ui.Tex(swT, x, y, 1, Color.White); Font.Draw(Patterns.Names[p.Pat], x + w / 2, y + 5, Ui.Cream, 1, .5f, Ui.Ink);
    }
    void Chip(string id, string label, int col, float x, float y, bool on, Action pick, float w = 47)
    {
        var (a, hit, _) = Ui.Widget(id, x, y, w, 18, "tick"); if (hit) pick(); a.Sel.Update(on ? 1 : 0, 320, 16, Ui.DT); Ui.PushScale(x + w / 2, y + 9, 1 + a.Hover.X * .06f + a.Sel.X * .03f, 1 + a.Hover.X * .06f + a.Sel.X * .03f);
        Ui.Nine(on ? "cardOn" : "card", x, y, w, 18, Color.White); Raylib.DrawRectangleV(new(x + 4, y + 4), new(10, 10), Ui.Ink); Raylib.DrawRectangleV(new(x + 5, y + 5), new(8, 8), ((Col)col).Rl()); Font.Draw(label, x + 17, y + 5, Ui.Ink); Ui.PopScale();
    }
    void Palette(int[] pal, int cur, Action<int> set, float x, float y, float w, int admire, bool hsv = true)
    {
        int cols = 15; float cw = w / cols; for (int i = 0; i < pal.Length; i++)
        {
            float cx = x + (i % cols) * cw, cy = y + (i / cols) * cw; var (a, hit, o) = Ui.Widget("pal" + tab + pal.Length + "_" + i, cx, cy, cw, cw, "tick"); bool on = pal[i] == cur; a.Sel.Update(on ? 1 : 0, 400, 14, Ui.DT); float g = a.Hover.X * 2.4f + a.Sel.X * 1.6f + a.Press.X;
            Raylib.DrawRectangleRounded(new Rectangle(cx + 1.5f - g, cy + 1.5f - g, cw - 3 + g * 2, cw - 3 + g * 2), .5f, 4, on ? Ui.Orange : Ui.Ink); Raylib.DrawRectangleRounded(new Rectangle(cx + 2.8f - g, cy + 2.8f - g, cw - 5.6f + g * 2, cw - 5.6f + g * 2), .5f, 4, ((Col)pal[i]).Rl()); Raylib.DrawRectangleRounded(new Rectangle(cx + 4.4f - g, cy + 4.2f - g, (cw - 5.6f) * .45f, 2), 1, 3, new Color(255, 255, 255, 70));
            if (hit) { Snap(); set(pal[i]); Changed(admire); }
        }
        if (!hsv) return; float y2 = y + ((pal.Length + cols - 1) / cols) * cw + 5; var (h, s, v) = ((Col)cur).ToHsv(); if (hueMem.TryGetValue(cur, out var m)) (h, s) = m; float sw2 = (w - 30) / 3; bool ch = false; if (Ui.Click && Ui.Over(x - 4, y2 - 4, w + 8, 16)) Snap();
        ch |= Ui.Slider("hsvH", ref h, x + 4, y2, sw2, (tx, ty, tw) => { for (int i = 0; i < 24; i++) Raylib.DrawRectangleV(new(tx + tw * i / 24, ty + 2), new(tw / 24 + .5f, 6), Col.Hsv(i / 24f, .75f, .9f).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
        float hh = h, vv = v, ss = s; ch |= Ui.Slider("hsvS", ref s, x + 14 + sw2, y2, sw2, (tx, ty, tw) => { Raylib.DrawRectangleGradientH((int)tx, (int)ty + 2, (int)tw, 6, Col.Hsv(hh, 0, MathF.Max(.3f, vv)).Rl(), Col.Hsv(hh, 1, MathF.Max(.3f, vv)).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
        ch |= Ui.Slider("hsvV", ref v, x + 24 + sw2 * 2, y2, sw2, (tx, ty, tw) => { Raylib.DrawRectangleGradientH((int)tx, (int)ty + 2, (int)tw, 6, Color.Black, Col.Hsv(hh, ss, 1).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
        if (ch) { int c = Col.Hsv(h, s, v).Hex; hueMem.Clear(); hueMem[c] = (h, s); set(c); }
    }
    readonly Dictionary<int, (float, float)> hueMem = new();

    /// Real-world readouts worked out from the body the sliders produce (an average body stands 5'7").
    string Measure(int i)
    {
        var r = new Rig(D); const float inch = 67f / 95.2f; float tall = r.Total * inch, bmi = D.Weight < .42f ? M.Lerp(16.8f, 22.6f, D.Weight / .42f) : M.Lerp(22.6f, 37f, (D.Weight - .42f) / .58f); string In(float v) => MathF.Round(v * 2) / 2 + " in";
        switch (i)
        {
            case 0: { int t = (int)MathF.Round(tall); return t / 12 + "'" + t % 12 + "\""; }
            case 1: return (int)MathF.Round(bmi * tall * tall / 703) + " lb";
            case 2: return In(r.ShX * 2 * inch);
            case 3: return In(r.Waist * inch * 5.8f);
            case 4: return In(r.Hip * inch * 6.0f);
            case 5: return In((r.UArm + r.LArm + r.HandR * 2.4f) * inch);
            case 6: return In((r.Thigh + r.Shin + r.AnkleH) * inch);
            default: return In(r.HeadRx * inch * 4.4f);
        }
    }
    void BodyTab(float x, float y, float w)
    {
        (string n, Func<float> g, Action<float> s)[] sl = [("Height", () => D.Height, v => D.Height = v), ("Weight", () => D.Weight, v => D.Weight = v), ("Shoulders", () => D.Shoulder, v => D.Shoulder = v), ("Waist", () => D.Waist, v => D.Waist = v), ("Hips", () => D.Hip, v => D.Hip = v), ("Arm length", () => D.Arm, v => D.Arm = v), ("Leg length", () => D.Leg, v => D.Leg = v), ("Head size", () => D.Head, v => D.Head = v)];
        for (int i = 0; i < sl.Length; i++)
        {
            float yy = y + 2 + i * 19; Font.Draw(sl[i].n, x + 2, yy, Ui.Ink); float v = sl[i].g(); if (Ui.Click && Ui.Over(x + 70, yy - 3, w - 100, 14)) Snap();
            if (Ui.Slider("b" + i, ref v, x + 76, yy, w - 122)) { sl[i].s(v); if (rng.P(.1f)) an.Punch(1); } Font.Draw(Measure(i), x + w - 2, yy, Ui.Ink, 1, 1);
        }
        float y2 = y + 158; Font.Draw("Age", x + 2, y2 + 4, Ui.Ink); for (int i = 0; i < 4; i++) if (Ui.Button("age" + i, Catalog.Ages[i], x + 34 + i * 66, y2, 63, 18, "btnP", on: D.Age == i)) { Snap(); D.Age = i; Changed(6); }
        Font.Draw("Skin", x + 2, y2 + 26, Ui.Ink); int[] sk = AvatarData.SkinTones; float cw = w / 14; for (int i = 0; i < 14; i++)
        {
            float cx = x + i * cw, cy = y2 + 38; var (a, hit, o) = Ui.Widget("skin" + i, cx, cy, cw, 22, "tick"); bool on = D.Skin == sk[i]; a.Sel.Update(on ? 1 : 0, 400, 14, Ui.DT); float g = a.Hover.X * 2.4f + a.Sel.X * 1.8f; Raylib.DrawRectangleRounded(new Rectangle(cx + 1 - g, cy + 1 - g, cw - 2 + g * 2, 20 + g * 2), .6f, 5, on ? Ui.Orange : Ui.Ink); Raylib.DrawRectangleRounded(new Rectangle(cx + 2.4f - g, cy + 2.4f - g, cw - 4.8f + g * 2, 17.2f + g * 2), .6f, 5, ((Col)sk[i]).Rl()); if (hit) { Snap(); D.Skin = sk[i]; Changed(6); }
        }
        Font.Draw("Fine tune", x + 2, y2 + 66, Ui.Soft); var (h, s, v2) = ((Col)D.Skin).ToHsv(); float sw2 = (w - 70) / 2; if (Ui.Click && Ui.Over(x + 50, y2 + 60, w, 16)) Snap(); float hue = M.Clamp((h - .02f) / .08f), val = M.Clamp((v2 - .2f) / .8f);
        bool c = Ui.Slider("skH", ref hue, x + 56, y2 + 66, sw2, (tx, ty, tw) => { Raylib.DrawRectangleGradientH((int)tx, (int)ty + 2, (int)tw, 6, Col.Hsv(.02f, .45f, .9f).Rl(), Col.Hsv(.10f, .45f, .9f).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
        c |= Ui.Slider("skV", ref val, x + 68 + sw2, y2 + 66, sw2 - 4, (tx, ty, tw) => { Raylib.DrawRectangleGradientH((int)tx, (int)ty + 2, (int)tw, 6, Col.Hsv(.05f, .6f, .25f).Rl(), Col.Hsv(.07f, .2f, 1).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
        if (c) D.Skin = Col.Hsv(.02f + hue * .08f, M.Lerp(.62f, .2f, val), .2f + val * .8f).Hex;
    }
    (Texture2D, bool) FaceThumb(string k, int i, Action<AvatarData> set) => Thumb("f" + k + i, HashCode.Combine(D.Skin, D.Hair.Base, D.EyeCol, D.Lip, D[Slot.Beard].A, D.Age, D.Eye * 31 + D.Mouth), p => { var d = D.Clone(); d[Slot.Hat].Id = 0; d[Slot.Glasses].Id = 0; d.Hair.Id = 0; d.Height = .5f; d.Leg = .5f; d.Age = D.Age == 3 ? 3 : 1; d.Head = .5f; set(d); ap.Draw(p, d, Pose.Stand with { HeadTilt = 0 }, 4.4f, 48, 440, outlinePx: 1); });
    void FaceTab(float x, float y, float w)
    {
        int s = Subs(["Eyes", "Brows", "Nose", "Mouth", "Marks", "Beard"], x, y, w); y += 22; float gh = 344 - (y - 6) - 128; int i;
        switch (s)
        {
            case 0: i = Grid("eye", Catalog.Eyes.Length, k => D.Eye == k, k => FaceThumb("e", k, d => d.Eye = k), k => Catalog.Eyes[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Eye = i; Changed(5); } break;
            case 1: i = Grid("brow", Catalog.Brows.Length, k => D.Brow == k, k => FaceThumb("b", k, d => d.Brow = k), k => Catalog.Brows[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Brow = i; Changed(5); } break;
            case 2: i = Grid("nose", Catalog.Noses.Length, k => D.Nose == k, k => FaceThumb("n", k, d => d.Nose = k), k => Catalog.Noses[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Nose = i; Changed(5); } break;
            case 3: i = Grid("mouth", Catalog.Mouths.Length, k => D.Mouth == k, k => FaceThumb("m", k, d => d.Mouth = k), k => Catalog.Mouths[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Mouth = i; Changed(5); } break;
            case 4: i = Grid("mark", Catalog.Marks.Length, k => D.Marks == k, k => FaceThumb("k", k, d => d.Marks = k), k => Catalog.Marks[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Marks = i; Changed(5); } break;
            default: i = Grid("beard", Catalog.Beards.Length, k => D[Slot.Beard].Id == k, k => FaceThumb("d", k, d => d[Slot.Beard].Id = k), k => Catalog.Beards[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D[Slot.Beard].Id = i; Changed(5); } break;
        }
        float cy = y + gh + 14; if (s is 0 or 1 or 2 or 4) { Font.Draw("Eye color", x, cy, Ui.Ink, 1.25f); Palette(AvatarData.EyeCols, D.EyeCol, c => D.EyeCol = c, x, cy + 16, w, 5); }
        else if (s == 3) { Font.Draw("Lip color", x, cy, Ui.Ink, 1.25f); Palette([0xb5605a, 0xc87a70, 0xa04a4a, 0x8c2f35, 0xd86f86, 0xe3a1a1, 0x7a3a34, 0x5a2a28, 0x9c4a7a, 0xb8473f], D.Lip, c => D.Lip = c, x, cy + 16, w, 5); }
        else { Font.Draw("Beard color", x, cy, Ui.Ink, 1.25f); Palette(AvatarData.HairCols, D[Slot.Beard].A, c => D[Slot.Beard].A = c, x, cy + 16, w, 5); }
    }
    void HairTab(float x, float y, float w)
    {
        float gh = 344 - (y - 6) - 148; var hr = D.Hair; int hs = HashCode.Combine(hr.Base, hr.Highlight, hr.HighlightAmt, HashCode.Combine(hr.SplitOn, hr.Split, hr.Ombre, hr.OmbreCol), HashCode.Combine(hr.Roots, hr.RootCol, hr.UnderOn, hr.Under), D.Skin);
        int i = Grid("hair", Catalog.Hair.Length, k => hr.Id == k, k => Thumb("hair" + k, hs, p => { var d = new AvatarData { Hair = hr.Clone(), Skin = D.Skin, Eye = D.Eye, EyeCol = D.EyeCol, Mouth = D.Mouth }; foreach (var q in d.P) q.Id = 0; d.Hair.Id = k; d.OutlineCol = 0x3e2420; float L = Catalog.Hair[k].BackLen; ap.Draw(p, d, Pose.Stand with { HeadTilt = 0 }, L > 1.2f ? 2.5f : 3.1f, 48, L > 1.2f ? 266 : 322, outlinePx: 1); }), k => Catalog.Hair[k].Name, x, y + 4, w, gh);
        if (i >= 0) { Snap(); hr.Id = i; Changed(5); }
        float cy = y + gh + 13; Font.Draw(Catalog.Hair[hr.Id].Name, x, cy - 1, Ui.Ink, 1.25f); string[] n = ["Base", "Streak", "Split", "Tips", "Roots", "Under"]; int[] c = [hr.Base, hr.Highlight, hr.Split, hr.OmbreCol, hr.RootCol, hr.Under]; bool[] act = [true, hr.HighlightAmt > 0, hr.SplitOn, hr.Ombre > 0, hr.Roots > 0, hr.UnderOn];
        for (int k = 0; k < 6; k++) Chip("hc" + k, n[k], c[k], x + k * 50.4f, cy + 12, chip == k, () => chip = k, 49);
        for (int k = 1; k < 6; k++) if (act[k]) Raylib.DrawCircleV(new(x + k * 50.4f + 46, cy + 14), 2.4f, Ui.Green);
        Rlgl.PushMatrix(); if (chip == 1) { float v = hr.HighlightAmt; Font.Draw("Amount", x + 130, cy, Ui.Soft); if (Ui.Slider("ha", ref v, x + 172, cy - 1, 110)) hr.HighlightAmt = v < .03f ? 0 : v; }
        else if (chip == 3) { float v = hr.Ombre; Font.Draw("Length", x + 130, cy, Ui.Soft); if (Ui.Slider("ho", ref v, x + 172, cy - 1, 110)) hr.Ombre = v < .03f ? 0 : v; }
        else if (chip == 4) { float v = hr.Roots; Font.Draw("Length", x + 130, cy, Ui.Soft); if (Ui.Slider("hr", ref v, x + 172, cy - 1, 110)) hr.Roots = v < .03f ? 0 : v; }
        else if (chip == 2) { bool v = hr.SplitOn; Font.Draw("Split dye", x + 196, cy, Ui.Soft); if (Ui.Toggle("hs", ref v, x + 250, cy - 3)) { Snap(); hr.SplitOn = v; Changed(5); } }
        else if (chip == 5) { bool v = hr.UnderOn; Font.Draw("Underlayer", x + 190, cy, Ui.Soft); if (Ui.Toggle("hu", ref v, x + 250, cy - 3)) { Snap(); hr.UnderOn = v; Changed(5); } }
        Rlgl.PopMatrix();
        Palette(AvatarData.HairCols, c[Math.Min(chip, 5)], v => { switch (chip) { case 0: if (D[Slot.Beard].A == hr.Base) D[Slot.Beard].A = v; hr.Base = v; break; case 1: hr.Highlight = v; if (hr.HighlightAmt < .05f) hr.HighlightAmt = .5f; break; case 2: hr.Split = v; hr.SplitOn = true; break; case 3: hr.OmbreCol = v; if (hr.Ombre < .05f) hr.Ombre = .5f; break; case 4: hr.RootCol = v; if (hr.Roots < .05f) hr.Roots = .5f; break; default: hr.Under = v; hr.UnderOn = true; break; } }, x, cy + 35, w, 5);
    }
    void StyleTab(float x, float y, float w)
    {
        Font.Draw("Idle pose", x + 2, y, Ui.Ink, 1.25f); for (int i = 0; i < 6; i++) if (Ui.Button("idle" + i, Catalog.Idles[i], x + (i % 3) * 102, y + 14 + (i / 3) * 24, 98, 21, "btnP", on: D.Idle == i)) { Snap(); D.Idle = i; an.Punch(4); poseT = 0; }
        Font.Draw("Voice", x + 2, y + 68, Ui.Ink, 1.25f); for (int i = 0; i < 6; i++) if (Ui.Button("vo" + i, Catalog.Voices[i], x + (i % 3) * 102, y + 82 + (i / 3) * 24, 98, 21, "btnP", on: D.Voice == i)) { Snap(); D.Voice = i; Hello(); }
        Font.Draw("Outline color", x + 2, y + 138, Ui.Ink, 1.25f); int[] oc = [0x2a1a1c, 0x000000, 0x1c2440, 0x402018, 0x1f3a2a, 0x3a1f48, 0x5a1c24, 0x6b4a33, 0x83807f, 0xf6eee0, 0xf0bd5a, 0xd86f86, 0x7fa8c9, 0x8fb584, 0x9a8ac8]; Palette(oc, D.OutlineCol, c => D.OutlineCol = c, x, y + 153, w, 6);
        Font.Draw("Try an emote", x + 2, y + 208, Ui.Ink, 1.25f); (string i, Anim a)[] em = [("wave", Anim.Wave), ("dance", Anim.Dance), ("laugh", Anim.Laugh), ("point", Anim.Point), ("cheer", Anim.Cheer), ("think", Anim.Think), ("shrug", Anim.Shrug), ("bow", Anim.Bow)];
        for (int i = 0; i < em.Length; i++) if (Ui.Button("em" + i, null, x + i * 37.5f, y + 223, 34, 28, "btnB", em[i].i)) { preview = 0; an.Set(em[i].a); an.StateT = 0; poseT = 1.8f; if (em[i].a is Anim.Laugh or Anim.Cheer or Anim.Wave) Voice.Say(D.Voice, 4); }
    }
    void OutfitPanel()
    {
        float x = avX - 118, y = 218, w = 236, h = 102; Ui.Nine("panel", x, y, w, h, Color.White); Font.Draw("Saved outfits", x + 14, y + 12, Ui.Ink, 1.25f); Font.Draw("click to wear", x + w - 14, y + 13, Ui.Soft, 1, 1);
        for (int i = 0; i < 5; i++)
        {
            float cx = x + 13 + i * 42.5f, cy = y + 26; var o = i < Store.Outfits.Count ? Store.Outfits[i] : null; var (a, hit, ov) = Ui.Card("of" + i, cx, cy, 39, 46, false); if (o != null) { var (t, ok) = Thumb("outfit" + i, HashCode.Combine(o.GetHashCode(), D.Skin, D.Hair.Id), p => { var q = D.Clone(); q.P = o.Select(z => z.Clone()).ToArray(); q.Validate(); ap.Draw(p, q, Pose.Stand, .8f, 48, 88, outlinePx: 1); }); if (ok) Raylib.DrawTexturePro(t, new Rectangle(24, 0, 48, 92), new Rectangle(cx + 8, cy + 2, 22, 42), Vector2.Zero, 0, Color.White); } else Font.Draw("+", cx + 19.5f, cy + 16, Ui.Soft, 2, .5f);
            void Keep() { while (Store.Outfits.Count <= i) Store.Outfits.Add(null); Store.Outfits[i] = D.P.Select(z => z.Clone()).ToArray(); Store.SaveOutfits(); Snd.Play("confirm"); }
            Ui.PopScale(); if (hit) { if (o != null) { Snap(); var beard = D[Slot.Beard].Clone(); D.P = o.Select(z => z.Clone()).ToArray(); D.P[(int)Slot.Beard] = beard; D.Validate(); Changed(4); } else Keep(); } if (ov) Ui.Tip = o == null ? "Save these clothes" : "Wear outfit " + (i + 1);
            if (Ui.Button("ofs" + i, o == null ? "Save" : "Replace", cx, cy + 48, 39, 15, "btnG", text: .8f)) Keep();
        }
    }
}

public static class Voice
{
    static int left, voice; static float t; static readonly Rng r = new(8);
    public static void Say(int v, int n) { left = n; voice = v; t = 0; }
    public static void Update(float dt) { if (left <= 0) return; t -= dt; if (t <= 0) { t = .105f; left--; Snd.Play("voice" + (char)('a' + voice), .55f, .9f + r.F() * .3f); } }
    public static bool Talking => left > 0;
}

/// Hand painted bedroom behind the avatar.
static class Room
{
    static Vector2 V(float x, float y) => new(x, y);
    public static Painter Paint()
    {
        var p = new Painter(1000, 360); p.View(1, 100, 0); var g = new Ink(0, 6, .5f);
        Func<float, float, Col> wall = (x, y) => { float s = M.Mod((int)MathF.Floor(x / 14), 2) == 0 ? 0 : .06f; Col c = Col.Lerp(0x8a5a58, 0x74454c, M.Clamp(y / 240)); c = c.Shade(s); float mx = M.Mod((int)MathF.Floor(x), 28) - 14, my = M.Mod((int)MathF.Floor(y) + (M.Mod((int)MathF.Floor(x / 28), 2)) * 18, 36) - 18; if (MathF.Abs(mx) + MathF.Abs(my) < 4) c = Col.Lerp(c, 0xd89a78, .35f); return c; };
        p.Begin().Rect(-100, 0, 1000, 252); p.Shade(new Ink(wall, 2, 0, 0) { Ao = 0, Grad = .25f });
        // wainscot + floor
        p.Begin().Rect(-100, 206, 1000, 52); p.Shade(new Ink((x, y) => M.Mod((int)MathF.Floor(x), 22) == 0 ? (Col)0x4a2c26 : Col.Lerp(0x7e4c38, 0x6a3e32, M.Noise(x * .3f, y * .05f) * .5f + .5f), 2, .3f, 0) { Ao = 0 }); p.Begin().Rect(-100, 202, 1000, 6); p.Shade(new Ink(0x9a6244, 2, .6f)); p.Begin().Rect(-100, 250, 1000, 8); p.Shade(new Ink(0x9a6244, 2, .6f));
        p.Begin().Rect(-100, 258, 1000, 104); p.Shade(new Ink((x, y) => { float v = y - 258; int row = (int)(MathF.Sqrt(v) * 1.15f); float xx = x + row * 47; int k = (int)MathF.Floor(xx / 74); Col c = Col.Lerp(0x9c623e, 0x7c4a34, M.Hash(k, row) * .8f); if (M.Mod((int)xx, 74) == 0) c = c.Shade(-.5f); if ((int)(MathF.Sqrt(v) * 1.15f) != (int)(MathF.Sqrt(v + 1) * 1.15f)) c = c.Shade(-.5f); return c.Shade(M.Noise(x * .08f, y * .9f, 4) * .12f); }, 2, 0, 0) { Ao = 0, Grad = .1f });
        // window with dusk hills
        p.Begin().Rect(52, 36, 92, 112); p.Shade(new Ink(0x5e3830, 4, .6f)); p.Begin().Rect(59, 43, 78, 98);
        Func<float, float, Col> sky = (x, y) => { float t = (y - 43) / 98; Col c = t < .5f ? Col.Lerp(0x6f6aa0, 0xe89a80, t * 2) : Col.Lerp(0xe89a80, 0xffd890, (t - .5f) * 2); float n = M.Fbm(x * .05f, y * .12f, 3, 4); if (n > .1f && t < .6f) c = Col.Lerp(c, 0xffc8a0, .5f); float h1 = 112 + M.Sin(x * .06f) * 7 + M.Noise(x * .05f) * 5; if (y > h1) c = Col.Lerp(0x8a6a8a, 0x6a5a7e, t); float h2 = 124 + M.Sin(x * .045f + 2) * 6; if (y > h2) c = 0x4a4a62; return c; };
        p.Shade(new Ink(sky, 2, 0, 0) { Ao = 0, Grad = 0 }); p.Begin().Ellipse(112, 100, 8, 8); p.Flat(0xfff0c0); p.Begin().Rect(96, 43, 4, 98).Rect(59, 88, 78, 4); p.Shade(new Ink(0x6e4436, 1.5f, .6f)); p.Begin().Rect(46, 146, 104, 8); p.Shade(new Ink(0x8a5640, 2, .6f));
        for (int s = -1; s <= 1; s += 2) { float cx = 98 + s * 50; p.Begin().Blob(V(cx - 13, 32), V(cx + 13, 32), V(cx + 14 - s * 4, 80), V(cx + s * 7 + 9, 150), V(cx + s * 7 - 9, 152), V(cx - 14 - s * 4, 80)); p.Shade(new Ink(0xb8574a, 8, .6f)); for (int i = -2; i <= 2; i++) p.Curve(V(cx + i * 5, 34), V(cx + i * 5 - s * 3, 80), V(cx + i * 4 + s * 7, 148), 1.2f, default, true, 1, .82f); p.Begin().Rect(cx - 12 - s * 2, 104, 24, 5); p.Shade(new Ink(0xe0b650, 2, .6f)); }
        p.Begin().Capsule(V(34, 32), 2.2f, V(162, 32), 2.2f); p.Shade(new Ink(0x6e4436, 2, .7f));
        // string lights
        var wire = new List<Vector2>(); for (int i = 0; i <= 120; i++) wire.Add(V(-60 + i * 5, 12 + MathF.Abs(M.Sin(i * 5f / 42 * 1.57f)) * 9)); p.Begin(); p.Stroke(wire.ToArray(), 1.2f, 0x3a2220, 1, false); for (int i = 0; i < 12; i++) { float lx = -20 + i * 42, ly = 16 + MathF.Abs(M.Sin(i * 1.57f + .8f)) * 9; p.Begin().Ellipse(lx, ly + 5, 3, 4); p.Shade(new Ink(i % 3 == 0 ? 0xffd27a : i % 3 == 1 ? 0xffa870 : 0xffe6a8, 2, .5f)); }
        // shelf with bits and pieces
        p.Begin().Rect(176, 92, 74, 5); p.Shade(new Ink(0x9a6244, 2, .6f)); Col[] bk = [0x8c2f35, 0x4f6b52, 0x4a6285, 0xc99a4b, 0x6d4a8a]; for (int i = 0; i < 5; i++) { p.Begin().Rect(180 + i * 6, 70 + (i * 7 % 5), 5.4f, 22 - (i * 7 % 5)); p.Shade(new Ink(bk[i], 1.5f, .5f)); }
        p.Begin().Poly(V(220, 92), V(218, 80), V(234, 80), V(232, 92)); p.Shade(new Ink(0xc65a3a, 3, .6f)); for (int i = 0; i < 7; i++) { float a = (i - 3) * .42f; p.Begin().Blob(V(226, 80), V(226 + M.Sin(a) * 12 - 3, 72 - M.Cos(a) * 8), V(226 + M.Sin(a) * 18, 72 - M.Cos(a) * 12), V(226 + M.Sin(a) * 12 + 3, 74 - M.Cos(a) * 8)); p.Shade(new Ink(i % 2 == 0 ? 0x5f8a52 : 0x7ba566, 2, .5f)); }
        p.Begin().Ellipse(243, 86, 5, 6); p.Shade(new Ink(0x9ec3cf, 2, .7f));
        // framed landscape
        p.Begin().Rect(184, 34, 52, 34); p.Shade(new Ink(0xa8693c, 3, .7f)); p.Begin().Rect(189, 39, 42, 24); p.Shade(new Ink(0xf3e2bd, 1, .3f)); p.Begin().Rect(190, 40, 40, 22); p.Shade(new Ink((x, y) => y < 50 + M.Sin(x * .25f) * 2 ? Col.Lerp(0x9fc8e0, 0xffd8a0, (y - 40) / 12) : y < 55 + M.Sin(x * .18f + 1) * 2 ? (Col)0x86ab74 : 0x5f8a56, 1, 0, 0) { Ao = 0, Grad = 0 }); p.Begin().Ellipse(221, 45, 3, 3); p.Flat(0xfff0c0);
        // floor lamp
        p.Begin().Rect(38, 186, 4, 120); p.Shade(new Ink(0x5a3a2a, 2, .6f)); p.Begin().Ellipse(40, 308, 17, 5); p.Shade(new Ink(0x5a3a2a, 3, .6f)); p.Begin().Poly(V(28, 150), V(52, 150), V(62, 190), V(18, 190)); p.Shade(new Ink((x, y) => Col.Lerp(0xffe0a0, 0xf0a860, M.Clamp((y - 150) / 40)), 5, .3f)); p.Begin().Rect(18, 186, 44, 4); p.Shade(new Ink(0xc65a3a, 1.5f, .4f)); for (int i = 0; i < 11; i++) p.Line(V(20 + i * 4, 190), V(20 + i * 4, 194), 1, 0xe0b650, false);
        // rug
        p.Begin().Ellipse(170, 322, 118, 25); p.Shade(new Ink((x, y) => { float d = MathF.Sqrt((x - 170) * (x - 170) / (118f * 118) + (y - 322) * (y - 322) / (25f * 25)); if (d > .9f) return 0xe0b650; if (d > .8f) return 0x8c2f35; if (d > .62f) { float a = MathF.Atan2((y - 322) * 4.7f, x - 170); return M.Sin(a * 20) > 0 ? (Col)0xb8574a : (Col)0xe9c9a0; } if (d > .55f) return 0x35496b; return Col.Lerp(0xb8574a, 0x9c4440, d); }, 3, .4f)); for (int i = 0; i < 40; i++) { float a = i * M.TAU / 40; p.Line(V(170 + M.Cos(a) * 117, 322 + M.Sin(a) * 25), V(170 + M.Cos(a) * 123, 322 + M.Sin(a) * 28), 1.4f, 0xe0b650, false); }
        // wardrobe
        p.Begin().Rect(316, 60, 150, 246); p.Shade(new Ink((x, y) => Col.Lerp(0x8a5236, 0x734230, M.Noise(x * .5f, y * .04f, 3) * .5f + .5f), 7, .6f)); p.Begin().Rect(310, 54, 162, 12); p.Shade(new Ink(0xa8693c, 3, .7f)); for (int i = 0; i < 2; i++) { p.Begin().Rect(324 + i * 70, 76, 62, 214); p.Shade(new Ink(0x96603e, 4, .6f)); p.Begin().Rect(330 + i * 70, 84, 50, 90); p.Shade(new Ink(0x7e4c32, 3, -.5f)); p.Begin().Rect(330 + i * 70, 186, 50, 94); p.Shade(new Ink(0x7e4c32, 3, -.5f)); p.Begin().Ellipse(i == 0 ? 382 : 398, 182, 3, 3); p.Shade(new Ink(0xe0b650, 2, .8f)); }
        // plant
        p.Begin().Poly(V(-8, 310), V(-12, 278), V(18, 278), V(14, 310)); p.Shade(new Ink(0xb8674a, 5, .6f)); for (int i = 0; i < 9; i++) { float a = (i - 4) * .36f; var b = V(3 + M.Sin(a) * 34, 250 - M.Cos(a) * 44); p.Begin().Blob(V(3, 279), Vector2.Lerp(V(3, 279), b, .5f) + V(-6, -4), b, Vector2.Lerp(V(3, 279), b, .5f) + V(6, 4)); p.Shade(new Ink(i % 2 == 0 ? 0x4f7a4a : 0x6b9a58, 3, .6f)); p.Line(V(3, 279), b, 1, 0x3a5a36); }
        return p;
    }
}

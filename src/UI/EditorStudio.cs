using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// The tattoo studio: paint your own tattoo onto each arm, unrolled flat from shoulder to wrist,
/// with a live look at the character beside it.
public sealed partial class EditorScreen
{
    enum Tl { Brush, Eraser, Fill, Line, Box, Circle, Knife, Select, Picker, Hand }
    static readonly (Tl t, string name, string icon, KeyboardKey key, string hint)[] Tools =
    [
        (Tl.Brush, "Brush", "tBrush", KeyboardKey.B, "Brush (B): drag to paint ink."),
        (Tl.Eraser, "Eraser", "tEraser", KeyboardKey.E, "Eraser (E): drag to rub ink out."),
        (Tl.Fill, "Fill", "tFill", KeyboardKey.G, "Fill (G): click to fill an area with the ink color."),
        (Tl.Line, "Line", "tLine", KeyboardKey.L, "Line (L): drag a straight line. Hold Shift to snap the angle."),
        (Tl.Box, "Box", "tBox", KeyboardKey.R, "Box (R): drag a box. Shift makes a square."),
        (Tl.Circle, "Circle", "tCircle", KeyboardKey.C, "Circle (C): drag a circle or oval. Shift makes it round."),
        (Tl.Knife, "Knife", "tKnife", KeyboardKey.K, "Knife (K): drag to slice a thin cut through the ink."),
        (Tl.Select, "Select", "tSelect", KeyboardKey.S, "Select (S): drag a box, then drag it to move. Ctrl+C, X, V work."),
        (Tl.Picker, "Picker", "tPicker", KeyboardKey.I, "Picker (I): click ink to pick up its color."),
        (Tl.Hand, "Move", "tHand", KeyboardKey.H, "Move (H): drag to look around. Right-drag works with any tool."),
    ];
    const int TW = AvatarPainter.TW, TH = AvatarPainter.TH;
    const float AX = 80, AY = 44, AW = 392, AH = 186, PX = 480, PW = 144;
    static readonly int[] Repeats = [1, 2, 3, 4, 6, 8];

    bool studio; int sArm; Tl tool = Tl.Brush, paintTool = Tl.Brush; int sCol = 2, sSize = 2, repeat = 1; bool mirAlong, mirAcross, filled;
    float sz = 3, spx, spy, pvZoom = 4.2f, pvT; bool pvDirty = true;
    readonly Painter cvP = new(TW, TH), pvP = new(144, 186), tinyP = new(2, 2); Texture2D cvTex, pvTex;
    readonly List<(int arm, byte[] a)> sUndo = new(), sRedo = new();
    byte[][] openArt; int[] openTat, openPal; int openCol; readonly bool[] edited = new bool[2];
    bool drawing, panning; int lx, ly, ax0, ay0; byte[] shapeBase; Vector2 panFrom;
    bool hasSel, selecting, moving; int selX, selY, selW, selH, grabX, grabY; byte[] flt; byte[] clip; int clipW, clipH;
    Rig sRig; static int tankTop = -1;

    float SX0 => MathF.Floor((Ui.VW - 640) / 2);
    byte[] Cur => D.Art[sArm] ??= new byte[TW * TH];
    float Hf(int ix) => AvatarPainter.ArmRadOf(sRig, AvatarPainter.ArmT(Math.Clamp(ix, 0, TW - 1))) / sRig.ArmR;
    int DCol(int j) => sArm == 0 ? TW - 1 - j : j; // screen column <-> canvas column (the left arm shows wrist-first)
    float ViewX => SX0 + AX + AW / 2 - TW * sz / 2 + spx;
    float ViewCy => AY + AH / 2 + spy;
    (float top, float h) Span(int ix) { float h = TH * sz * Hf(ix); return (ViewCy - h / 2, h); }
    float CellLeft(int ix) => ViewX + DCol(ix) * sz;
    (int x, int y) CellAt(Vector2 m)
    {
        int j = (int)MathF.Floor((m.X - ViewX) / sz); int ix = sArm == 0 ? TW - 1 - j : j; var (top, h) = Span(ix);
        return (ix, (int)MathF.Floor((m.Y - top) / h * TH));
    }
    public Vector2 CellPosForTest(int ix, int iy) { var (top, h) = Span(ix); return new(CellLeft(ix) + sz / 2, top + (iy + .5f) * h / TH); }
    public bool StudioOpen => studio;
    public int StudioInk(int arm) { var a = D.Art[arm]; return a == null ? 0 : a.Count(b => b != 0); }

    public void OpenStudio(int arm)
    {
        Snap(); studio = true; sArm = arm; D.Art ??= new byte[2][]; openArt = [D.Art[0]?.ToArray(), D.Art[1]?.ToArray()]; openTat = (int[])D.Tattoos.Clone(); openPal = (int[])D.TatPal.Clone(); openCol = D.TatCol;
        sUndo.Clear(); sRedo.Clear(); edited[0] = edited[1] = false; hasSel = false; flt = null; drawing = false; sRig = new Rig(D);
        int c = Array.IndexOf(D.TatPal, D.TatCol); sCol = c >= 0 ? c + 1 : 2;
        if (cvTex.Id == 0) cvTex = cvP.ToTexture(); if (pvTex.Id == 0) pvTex = pvP.ToTexture(); Fit(); pvDirty = true; pvT = 0; Snd.Play("click");
        if (tankTop < 0) { tankTop = 0; for (int i = 0; i < Catalog.Tops.Length; i++) if (Catalog.Tops[i].Name.Contains("Tank")) { tankTop = i; break; } }
    }
    void CloseStudio(bool keep)
    {
        Commit();
        if (!keep) { D.Art = openArt; D.Tattoos = openTat; D.TatPal = openPal; D.TatCol = openCol; if (undo.Count > 0) undo.RemoveAt(undo.Count - 1); }
        else for (int a = 0; a < 2; a++) if (edited[a]) { var art = D.Art[a]; bool any = art != null && Array.Exists(art, b => b != 0); D.Art[a] = any ? art : null; D.Tattoos[a] = any ? -1 : 0; }
        studio = false; Snd.Play(keep ? "finish" : "undo", keep ? .5f : 1); Redraw(); if (keep) Changed(1);
    }
    void Fit() { sz = MathF.Min(AW / (TW + 10f), AH / (TH + 8f)); spx = spy = 0; }
    void ZoomAt(float f, Vector2 m)
    {
        float nz = M.Clamp(sz * f, 1.6f, 20); if (MathF.Abs(nz - sz) < .001f) return; float cx = (m.X - ViewX) / sz, cy = (m.Y - ViewCy) / sz;
        spx = m.X - cx * nz - (SX0 + AX + AW / 2 - TW * nz / 2); spy = m.Y - cy * nz - (AY + AH / 2); sz = nz;
    }
    void Push(int arm = -1) { if (arm < 0) arm = sArm; sUndo.Add((arm, (D.Art[arm] ?? new byte[TW * TH]).ToArray())); if (sUndo.Count > 100) sUndo.RemoveAt(0); sRedo.Clear(); edited[arm] = true; }
    void SUndo() { Commit(); if (sUndo.Count == 0) return; var (a, b) = sUndo[^1]; sUndo.RemoveAt(sUndo.Count - 1); sRedo.Add((a, (D.Art[a] ?? new byte[TW * TH]).ToArray())); D.Art[a] = b; edited[a] = true; if (a != sArm) SwitchArm(a); hasSel = false; pvDirty = true; Snd.Play("undo"); }
    void SRedo() { Commit(); if (sRedo.Count == 0) return; var (a, b) = sRedo[^1]; sRedo.RemoveAt(sRedo.Count - 1); sUndo.Add((a, (D.Art[a] ?? new byte[TW * TH]).ToArray())); D.Art[a] = b; edited[a] = true; if (a != sArm) SwitchArm(a); hasSel = false; pvDirty = true; Snd.Play("undo", 1, 1.2f); }
    void SwitchArm(int a) { Commit(); hasSel = false; sArm = a; pvDirty = true; }

    // ---------------------------------------------------------------- painting
    static void P1(byte[] b, int x, int y, byte v) { if (x >= 0 && x < TW && y >= 0 && y < TH) b[y * TW + x] = v; }
    IEnumerable<(int x, int y)> Images(int x, int y)
    {
        for (int k = 0; k < repeat; k++)
        {
            int xx = repeat == 1 ? x : ((x + (int)MathF.Round(k * TW / (float)repeat)) % TW + TW) % TW;
            yield return (xx, y); if (mirAlong) yield return (TW - 1 - xx, y);
            if (mirAcross) { yield return (xx, TH - 1 - y); if (mirAlong) yield return (TW - 1 - xx, TH - 1 - y); }
        }
    }
    void Plot(byte[] b, int x, int y, byte v) { foreach (var (px, py) in Images(x, y)) P1(b, px, py, v); }
    void Stamp(byte[] b, int x, int y, byte v, int size)
    {
        float c = (size - 1) / 2f, r2 = size * size / 4f + .3f; int o = (int)MathF.Floor(c);
        for (int i = 0; i < size; i++) for (int j = 0; j < size; j++) { float dx = i - c, dy = j - c; if (dx * dx + dy * dy <= r2) Plot(b, x + i - o, y + j - o, v); }
    }
    void LineC(byte[] b, int x0, int y0, int x1, int y1, byte v, int size)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, e = dx + dy;
        for (int g = 0; g < 4000; g++) { Stamp(b, x0, y0, v, size); if (x0 == x1 && y0 == y1) break; int e2 = 2 * e; if (e2 >= dy) { e += dy; x0 += sx; } if (e2 <= dx) { e += dx; y0 += sy; } }
    }
    void BoxC(byte[] b, int x0, int y0, int x1, int y1, byte v, int size, bool fill)
    {
        int ax = Math.Min(x0, x1), bx = Math.Max(x0, x1), ay = Math.Min(y0, y1), by = Math.Max(y0, y1);
        if (fill) { for (int y = ay; y <= by; y++) for (int x = ax; x <= bx; x++) Plot(b, x, y, v); return; }
        LineC(b, ax, ay, bx, ay, v, size); LineC(b, bx, ay, bx, by, v, size); LineC(b, bx, by, ax, by, v, size); LineC(b, ax, by, ax, ay, v, size);
    }
    void OvalC(byte[] b, int x0, int y0, int x1, int y1, byte v, int size, bool fill)
    {
        float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f, rx = MathF.Abs(x1 - x0) / 2f, ry = MathF.Abs(y1 - y0) / 2f;
        if (fill) { for (int y = (int)MathF.Floor(cy - ry); y <= (int)MathF.Ceiling(cy + ry); y++) for (int x = (int)MathF.Floor(cx - rx); x <= (int)MathF.Ceiling(cx + rx); x++) { float u = (x - cx) / (rx + .5f), w = (y - cy) / (ry + .5f); if (u * u + w * w <= 1) Plot(b, x, y, v); } return; }
        int n = Math.Max(12, (int)(M.TAU * MathF.Max(rx, ry) * 1.2f)); int px = (int)MathF.Round(cx + rx), py = (int)MathF.Round(cy);
        for (int i = 1; i <= n; i++) { float a = i * M.TAU / n; int qx = (int)MathF.Round(cx + M.Cos(a) * rx), qy = (int)MathF.Round(cy + M.Sin(a) * ry); LineC(b, px, py, qx, qy, v, size); px = qx; py = qy; }
    }
    void Flood(byte[] b, int x, int y, byte v)
    {
        foreach (var (sx, sy) in Images(x, y).ToArray())
        {
            if (sx < 0 || sx >= TW || sy < 0 || sy >= TH) continue; byte t = b[sy * TW + sx]; if (t == v) continue; var st = new Stack<(int, int)>(); st.Push((sx, sy));
            while (st.Count > 0) { var (qx, qy) = st.Pop(); if (qx < 0 || qx >= TW || qy < 0 || qy >= TH || b[qy * TW + qx] != t) continue; b[qy * TW + qx] = v; st.Push((qx + 1, qy)); st.Push((qx - 1, qy)); st.Push((qx, qy + 1)); st.Push((qx, qy - 1)); }
        }
    }
    (int, int) Snap45(int x0, int y0, int x1, int y1, bool square)
    {
        int dx = x1 - x0, dy = y1 - y0;
        if (square) { int m = Math.Max(Math.Abs(dx), Math.Abs(dy)); return (x0 + Math.Sign(dx == 0 ? 1 : dx) * m, y0 + Math.Sign(dy == 0 ? 1 : dy) * m); }
        if (Math.Abs(dx) > 2 * Math.Abs(dy)) return (x1, y0); if (Math.Abs(dy) > 2 * Math.Abs(dx)) return (x0, y1);
        int k = Math.Max(Math.Abs(dx), Math.Abs(dy)); return (x0 + Math.Sign(dx) * k, y0 + Math.Sign(dy) * k);
    }

    // ---------------------------------------------------------------- selection
    bool InSel(int x, int y) => hasSel && x >= selX && y >= selY && x < selX + selW && y < selY + selH;
    byte[] Extract() { var o = new byte[selW * selH]; for (int y = 0; y < selH; y++) for (int x = 0; x < selW; x++) { int cx = selX + x, cy = selY + y; if (cx >= 0 && cx < TW && cy >= 0 && cy < TH) o[y * selW + x] = Cur[cy * TW + cx]; } return o; }
    void ClearRect() { for (int y = 0; y < selH; y++) for (int x = 0; x < selW; x++) P1(Cur, selX + x, selY + y, 0); }
    void Lift() { if (flt != null || !hasSel) return; Push(); flt = Extract(); ClearRect(); }
    void Commit() { if (flt == null) return; for (int y = 0; y < selH; y++) for (int x = 0; x < selW; x++) { byte v = flt[y * selW + x]; if (v != 0) P1(Cur, selX + x, selY + y, v); } flt = null; pvDirty = true; }
    void SelCopy() { if (!hasSel) return; clip = flt != null ? flt.ToArray() : Extract(); clipW = selW; clipH = selH; Snd.Play("tick"); }
    void SelDelete() { if (!hasSel) return; if (flt != null) flt = null; else { Push(); ClearRect(); } hasSel = false; pvDirty = true; }
    void SelCut() { SelCopy(); SelDelete(); }
    void SelPaste()
    {
        if (clip == null) return; Commit(); Push(); flt = clip.ToArray(); selW = clipW; selH = clipH;
        if (!hasSel) { var (cx, cy) = CellAt(new(SX0 + AX + AW / 2, AY + AH / 2)); selX = Math.Clamp(cx - selW / 2, 0, Math.Max(0, TW - selW)); selY = Math.Clamp(cy - selH / 2, 0, Math.Max(0, TH - selH)); }
        hasSel = true; tool = Tl.Select; pvDirty = true;
    }
    void SelFlip(bool along)
    {
        if (!hasSel) return; Lift(); var o = new byte[flt.Length];
        for (int y = 0; y < selH; y++) for (int x = 0; x < selW; x++) o[y * selW + x] = along ? flt[y * selW + (selW - 1 - x)] : flt[(selH - 1 - y) * selW + x];
        flt = o; pvDirty = true;
    }
    void SelAll() { Commit(); hasSel = true; selX = selY = 0; selW = TW; selH = TH; tool = Tl.Select; }

    // ---------------------------------------------------------------- update and input
    void StudioUpdate(float dt)
    {
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (ctrl)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Z)) SUndo(); if (Raylib.IsKeyPressed(KeyboardKey.Y)) SRedo(); if (Raylib.IsKeyPressed(KeyboardKey.C)) SelCopy();
            if (Raylib.IsKeyPressed(KeyboardKey.X)) SelCut(); if (Raylib.IsKeyPressed(KeyboardKey.V)) SelPaste(); if (Raylib.IsKeyPressed(KeyboardKey.A)) SelAll();
        }
        else if (!drawing)
        {
            foreach (var t in Tools) if (Raylib.IsKeyPressed(t.key)) SetTool(t.t);
            if (Raylib.IsKeyPressed(KeyboardKey.Delete) || Raylib.IsKeyPressed(KeyboardKey.Backspace)) SelDelete();
            if (Raylib.IsKeyPressed(KeyboardKey.LeftBracket)) sSize = Math.Max(1, sSize - 1); if (Raylib.IsKeyPressed(KeyboardKey.RightBracket)) sSize = Math.Min(8, sSize + 1);
            var mid = new Vector2(SX0 + AX + AW / 2, AY + AH / 2);
            if (Raylib.IsKeyPressed(KeyboardKey.Equal) || Raylib.IsKeyPressed(KeyboardKey.KpAdd)) ZoomAt(1.25f, mid); if (Raylib.IsKeyPressed(KeyboardKey.Minus) || Raylib.IsKeyPressed(KeyboardKey.KpSubtract)) ZoomAt(.8f, mid); if (Raylib.IsKeyPressed(KeyboardKey.Zero)) Fit();
            if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { Commit(); hasSel = false; }
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { if (hasSel) { Commit(); hasSel = false; } else CloseStudio(true); }
        }
        pvT -= dt; if (pvDirty && pvT <= 0) { PreviewRedraw(); pvDirty = false; pvT = .05f; }
        Snd.Loop("room", .35f);
    }
    void SetTool(Tl t) { if (t != Tl.Select) { Commit(); hasSel = false; } tool = t; if (t is Tl.Brush or Tl.Eraser or Tl.Fill or Tl.Line or Tl.Box or Tl.Circle or Tl.Knife) paintTool = t; }
    void CanvasInput()
    {
        var m = Ui.Mouse; bool over = Ui.Over(SX0 + AX, AY, AW, AH); bool space = Raylib.IsKeyDown(KeyboardKey.Space), shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        // looking around: wheel zooms at the cursor, right/middle drag or the Move tool pans
        if (over && Ui.Wheel != 0) ZoomAt(Ui.Wheel > 0 ? 1.18f : 1 / 1.18f, m);
        bool mid = Raylib.IsMouseButtonDown(MouseButton.Middle);
        if (!panning && over && Ui.Active == null && (Ui.RightClick || Raylib.IsMouseButtonPressed(MouseButton.Middle) || (Ui.Click && (tool == Tl.Hand || space)))) { panning = true; panFrom = m; }
        if (panning) { if (Ui.RightHeld || mid || Ui.Held) { spx += m.X - panFrom.X; spy += m.Y - panFrom.Y; panFrom = m; float lim = TW * sz / 2 + AW / 2 - 20; spx = M.Clamp(spx, -lim, lim); spy = M.Clamp(spy, -AH / 2 - TH * sz / 2 + 20, AH / 2 + TH * sz / 2 - 20); return; } panning = false; }
        var (cx, cy) = CellAt(m); byte ink = (byte)sCol;
        if (!drawing && Ui.Click && over && Ui.Active == null)
        {
            drawing = true; lx = ax0 = cx; ly = ay0 = cy;
            switch (tool)
            {
                case Tl.Brush or Tl.Eraser: Push(); Stamp(Cur, cx, cy, tool == Tl.Brush ? ink : (byte)0, sSize); break;
                case Tl.Fill: Push(); Flood(Cur, cx, cy, ink); drawing = false; Snd.Play("tick"); break;
                case Tl.Picker: if (cx >= 0 && cx < TW && cy >= 0 && cy < TH && Cur[cy * TW + cx] != 0) { sCol = Cur[cy * TW + cx]; Snd.Play("tick"); SetTool(paintTool); } drawing = false; break;
                case Tl.Line or Tl.Box or Tl.Circle or Tl.Knife: Push(); shapeBase = Cur.ToArray(); break;
                case Tl.Select:
                    if (InSel(cx, cy)) { Lift(); moving = true; grabX = cx - selX; grabY = cy - selY; }
                    else { Commit(); hasSel = false; selecting = true; selX = cx; selY = cy; selW = selH = 1; }
                    break;
            }
            pvDirty = true;
        }
        else if (drawing && Ui.Held)
        {
            switch (tool)
            {
                case Tl.Brush or Tl.Eraser: if (cx != lx || cy != ly) { LineC(Cur, lx, ly, cx, cy, tool == Tl.Brush ? ink : (byte)0, sSize); lx = cx; ly = cy; pvDirty = true; } break;
                case Tl.Line or Tl.Knife: { Array.Copy(shapeBase, Cur, Cur.Length); var (ex, ey) = shift ? Snap45(ax0, ay0, cx, cy, false) : (cx, cy); LineC(Cur, ax0, ay0, ex, ey, tool == Tl.Knife ? (byte)0 : ink, tool == Tl.Knife ? 1 : sSize); pvDirty = true; break; }
                case Tl.Box or Tl.Circle: { Array.Copy(shapeBase, Cur, Cur.Length); var (ex, ey) = shift ? Snap45(ax0, ay0, cx, cy, true) : (cx, cy); if (tool == Tl.Box) BoxC(Cur, ax0, ay0, ex, ey, ink, sSize, filled); else OvalC(Cur, ax0, ay0, ex, ey, ink, sSize, filled); pvDirty = true; break; }
                case Tl.Select:
                    if (moving) { selX = cx - grabX; selY = cy - grabY; pvDirty = true; }
                    else if (selecting) { int qx = Math.Clamp(cx, 0, TW - 1), qy = Math.Clamp(cy, 0, TH - 1), sx = Math.Clamp(ax0, 0, TW - 1), sy = Math.Clamp(ay0, 0, TH - 1); selX = Math.Min(sx, qx); selY = Math.Min(sy, qy); selW = Math.Abs(qx - sx) + 1; selH = Math.Abs(qy - sy) + 1; hasSel = true; }
                    break;
            }
        }
        if (drawing && !Ui.Held) { drawing = false; moving = false; if (selecting) { selecting = false; hasSel = selW * selH > 1; } shapeBase = null; pvDirty = true; }
    }

    // ---------------------------------------------------------------- preview
    void PreviewRedraw()
    {
        var pd = D.Clone(); if (flt != null) { var a = Cur.ToArray(); for (int y = 0; y < selH; y++) for (int x = 0; x < selW; x++) { byte v = flt[y * selW + x]; if (v != 0) P1(a, selX + x, selY + y, v); } pd.Art[sArm] = a; }
        pd[Slot.Jacket].Id = 0; pd[Slot.Gloves].Id = 0; if (!Catalog.Tops[pd[Slot.Top].Id].Name.Contains("Tank")) pd[Slot.Top].Id = tankTop;
        var pose = Pose.Stand with { ArmL0 = -1.3f, ArmL1 = -1.5f, ArmR0 = 1.3f, ArmR1 = 1.5f };
        tinyP.Clear(); ap.Draw(tinyP, pd, pose, pvZoom, 0, 0); var (sh, _, ha) = ap.ArmJoints(sArm == 0 ? -1 : 1); var c = (sh + ha) / 2;
        pvP.Clear(); ap.Draw(pvP, pd, pose, pvZoom, pvP.W / 2f - c.X * pvZoom, pvP.H / 2f - c.Y * pvZoom, outlinePx: 2); pvP.Upload(pvTex);
    }

    // ---------------------------------------------------------------- drawing
    void Compose()
    {
        var b = Cur; var pal = D.TatPal; Col sk = D.Skin;
        for (int iy = 0; iy < TH; iy++)
        {
            float s = (iy + .5f) / TH * 2 - 1; Col baseC = sk.Shade(.10f * -s - .30f * s * s);
            for (int ix = 0; ix < TW; ix++)
            {
                int k = b[iy * TW + ix]; if (flt != null) { int fx = ix - selX, fy = iy - selY; if (fx >= 0 && fy >= 0 && fx < selW && fy < selH && flt[fy * selW + fx] != 0) k = flt[fy * selW + fx]; }
                cvP.Px[iy * TW + ix] = (k == 0 || k > pal.Length ? baseC : Col.Lerp(baseC, pal[k - 1], .9f)).Rl();
            }
        }
        cvP.Upload(cvTex);
    }
    void StudioDraw()
    {
        float W = Ui.VW, H = Ui.VH, X = SX0;
        Raylib.DrawRectangle(0, 0, (int)W + 1, (int)H + 1, new Color(46, 30, 32, 255)); Ui.Nine("panel", X + 2, 2, 636, 356, Color.White);
        // header
        Font.Draw("Tattoo Studio", X + 20, 17, Ui.Ink, 1.5f);
        if (Ui.Button("sarm0", "Left arm", X + 150, 13, 74, 20, "btnP", on: sArm == 0, text: .9f) && sArm != 0) SwitchArm(0);
        if (Ui.Button("sarm1", "Right arm", X + 228, 13, 74, 20, "btnP", on: sArm == 1, text: .9f) && sArm != 1) SwitchArm(1);
        if (Ui.Button("sundo", null, X + 420, 13, 26, 20, "btn", icon: "undo", tip: "Undo (Ctrl+Z)", disabled: sUndo.Count == 0)) SUndo();
        if (Ui.Button("sredo", null, X + 450, 13, 26, 20, "btn", icon: "redo", tip: "Redo (Ctrl+Y)", disabled: sRedo.Count == 0)) SRedo();
        if (Ui.Button("scancel", "Cancel", X + 486, 13, 62, 20, "btnR", text: .9f)) { CloseStudio(false); return; }
        if (Ui.Button("sdone", "Done", X + 552, 13, 70, 20, "btnG", icon: "check", text: .9f)) { CloseStudio(true); return; }
        // tools
        for (int i = 0; i < Tools.Length; i++) { var t = Tools[i]; if (Ui.Button("stool" + i, t.name, X + 16, AY + i * 21, 58, 19, "btnP", icon: t.icon, on: tool == t.t, text: .85f)) SetTool(t.t); }

        // the arm, unrolled: shoulder at one end, wrist at the other
        CanvasInput(); Compose();
        float ax = X + AX, ay = AY; Ui.Nine("dark", ax - 3, ay - 3, AW + 6, AH + 6, Color.White); Ui.Clip(ax, ay, AW, AH);
        for (int gx = 0; gx < AW; gx += 12) for (int gy = 0; gy < AH; gy += 12) Raylib.DrawRectangle((int)(ax + gx + (spx % 12 + 12) % 12), (int)(ay + gy + (spy % 12 + 12) % 12), 1, 1, new Color(255, 255, 255, 18));
        float vx = ViewX, half = TH * sz / 2;
        // hand stub past the wrist, so the arm reads the right way round
        { var (wt, wh) = Span(TW - 1); float wx = sArm == 0 ? vx - wh * .55f : vx + TW * sz + wh * .55f; Raylib.DrawEllipse((int)wx, (int)ViewCy, wh * .62f, wh * .52f, Ui.Ink); Raylib.DrawEllipse((int)wx, (int)ViewCy, wh * .62f - 1.5f, wh * .52f - 1.5f, ((Col)D.Skin).Shade(-.08f).Rl()); }
        for (int j = 0; j < TW; j++) { int ix = DCol(j); float cx = vx + j * sz; if (cx > ax + AW || cx + sz < ax) continue; var (top, h) = Span(ix); Raylib.DrawTexturePro(cvTex, new Rectangle(ix, 0, 1, TH), new Rectangle(cx, top, sz + .05f, h), Vector2.Zero, 0, Color.White); }
        // outline
        for (int j = 0; j < TW; j++) { int ix = DCol(j); var (top, h) = Span(ix); float cx = vx + j * sz; Raylib.DrawLineEx(new(cx, top - .5f), new(cx + sz, top - .5f), 1.2f, Ui.Ink); Raylib.DrawLineEx(new(cx, top + h + .5f), new(cx + sz, top + h + .5f), 1.2f, Ui.Ink); }
        { var (t0, h0) = Span(DCol(0)); var (t1, h1) = Span(DCol(TW - 1)); Raylib.DrawLineEx(new(vx - .5f, t0), new(vx - .5f, t0 + h0), 1.2f, Ui.Ink); Raylib.DrawLineEx(new(vx + TW * sz + .5f, t1), new(vx + TW * sz + .5f, t1 + h1), 1.2f, Ui.Ink); }
        // cell grid when zoomed in
        if (sz >= 7) for (int j = 0; j < TW; j++) { float cx = vx + j * sz; if (cx > ax + AW || cx + sz < ax) continue; var (top, h) = Span(DCol(j)); Raylib.DrawLineV(new(cx, top), new(cx, top + h), new Color(0, 0, 0, 30)); for (int r = 1; r < TH; r++) Raylib.DrawLineV(new(cx, top + h * r / TH), new(cx + sz, top + h * r / TH), new Color(0, 0, 0, 22)); }
        // symmetry guides
        var guide = new Color(240, 182, 78, 170);
        if (mirAlong) for (float yy = ViewCy - half - 6; yy < ViewCy + half + 6; yy += 6) Raylib.DrawLineEx(new(vx + TW * sz / 2, yy), new(vx + TW * sz / 2, yy + 3), 1.2f, guide);
        if (mirAcross) for (float xx = vx - 6; xx < vx + TW * sz + 6; xx += 6) Raylib.DrawLineEx(new(xx, ViewCy), new(xx + 3, ViewCy), 1.2f, guide);
        if (repeat > 1) for (int k = 1; k < repeat; k++) { int ix = (int)MathF.Round(k * TW / (float)repeat); float gx = sArm == 0 ? vx + (TW - ix) * sz : vx + ix * sz; var (top, h) = Span(ix); for (float yy = top - 4; yy < top + h + 4; yy += 5) Raylib.DrawLineEx(new(gx, yy), new(gx, yy + 2.5f), 1, new Color(126, 168, 201, 200)); }
        // selection: marching dashes along the cells' edges
        if (hasSel)
        {
            float tt = Ui.T * 8;
            for (int ix = Math.Max(0, selX); ix < Math.Min(TW, selX + selW); ix++)
            {
                var (top, h) = Span(ix); float cx = CellLeft(ix); float y0 = top + h * selY / TH, y1 = top + h * (selY + selH) / TH; var c = ((int)(ix + tt) & 1) == 0 ? Color.White : Ui.Ink;
                Raylib.DrawLineEx(new(cx, y0), new(cx + sz, y0), 1.2f, c); Raylib.DrawLineEx(new(cx, y1), new(cx + sz, y1), 1.2f, c);
                if (ix == selX || ix == selX + selW - 1) foreach (float ex in sArm == 0 ? (ix == selX ? [cx + sz] : [cx]) : (ix == selX ? [cx] : (float[])[cx + sz])) Raylib.DrawLineEx(new(ex, y0), new(ex, y1), 1.2f, c);
                if (selW == 1) Raylib.DrawLineEx(new(sArm == 0 ? cx : cx + sz, y0), new(sArm == 0 ? cx : cx + sz, y1), 1.2f, c);
            }
        }
        // what the brush will touch
        if (Ui.Over(ax, ay, AW, AH) && !panning && tool is Tl.Brush or Tl.Eraser or Tl.Knife or Tl.Fill or Tl.Picker)
        {
            var (cx, cy) = CellAt(Ui.Mouse); int size = tool is Tl.Brush or Tl.Eraser ? sSize : 1; float c = (size - 1) / 2f, rr2 = size * size / 4f + .3f; int o = (int)MathF.Floor(c);
            for (int i = 0; i < size; i++) for (int j = 0; j < size; j++) { float dx = i - c, dy = j - c; if (dx * dx + dy * dy > rr2) continue; foreach (var (qx, py) in Images(cx + i - o, cy + j - o)) { if (qx < 0 || qx >= TW || py < 0 || py >= TH) continue; var (top, h) = Span(qx); Raylib.DrawRectangleLinesEx(new Rectangle(CellLeft(qx), top + h * py / TH, sz, h / TH), sz > 5 ? 1 : .6f, new Color(255, 255, 255, 200)); } }
        }
        float ly2 = ViewCy + half + 4; Font.Draw("Shoulder", sArm == 0 ? vx + TW * sz - 2 : vx + 2, ly2, new Color(250, 240, 218, 170), .85f, sArm == 0 ? 1 : 0); Font.Draw("Wrist", sArm == 0 ? vx + 2 : vx + TW * sz - 2, ly2, new Color(250, 240, 218, 170), .85f, sArm == 0 ? 0 : 1);
        Ui.Unclip();
        // zoom row under the arm
        float zy = AY + AH + 5; var midv = new Vector2(ax + AW / 2, ay + AH / 2);
        if (Ui.Button("szout", null, ax, zy, 22, 18, "btnP", icon: "zout", tip: "Zoom out (-)")) ZoomAt(1 / 1.25f, midv);
        if (Ui.Button("szin", null, ax + 25, zy, 22, 18, "btnP", icon: "zin", tip: "Zoom in (+)")) ZoomAt(1.25f, midv);
        if (Ui.Button("szfit", "Fit", ax + 50, zy, 34, 18, "btnP", text: .85f, tip: "Show the whole arm (0)")) Fit();
        Font.Draw($"{sz / MathF.Min(AW / (TW + 10f), AH / (TH + 8f)) * 100:0}%", ax + 90, zy + 5, Ui.Soft, .9f);
        Font.Draw("Wheel zooms, right-drag moves around", ax + AW, zy + 5, Ui.Soft, .85f, 1);

        // live preview of the character, sleeves out of the way
        float px = X + PX; Ui.Nine("slot", px - 3, AY - 3, PW + 6, AH + 6, Color.White); Raylib.DrawRectangleGradientV((int)px, (int)AY, (int)PW, (int)AH, new Color(226, 200, 160, 255), new Color(196, 160, 118, 255));
        Ui.Clip(px, AY, PW, AH); Ui.Tex(pvTex, px, AY, 1, Color.White); Ui.Unclip();
        if (Ui.Over(px, AY, PW, AH) && Ui.Wheel != 0) { pvZoom = M.Clamp(pvZoom * (Ui.Wheel > 0 ? 1.15f : 1 / 1.15f), 2.6f, 9); pvDirty = true; }
        if (Ui.Button("pvout", null, px, zy, 22, 18, "btnP", icon: "zout", tip: "Zoom the preview out")) { pvZoom = MathF.Max(2.6f, pvZoom / 1.2f); pvDirty = true; }
        if (Ui.Button("pvin", null, px + 25, zy, 22, 18, "btnP", icon: "zin", tip: "Zoom the preview in")) { pvZoom = MathF.Min(9, pvZoom * 1.2f); pvDirty = true; }
        Font.Draw("Preview", px + PW, zy + 5, Ui.Soft, .85f, 1);

        // ink, size and symmetry
        float r1 = 258; Font.Draw("Ink", X + 16, r1 + 3, Ui.Ink, 1.1f);
        Swatches("sink", D.TatPal, sCol - 1, k => { sCol = k + 1; if (tool is Tl.Eraser or Tl.Knife or Tl.Select or Tl.Hand or Tl.Picker) SetTool(Tl.Brush); Snd.Play("tick", .5f); }, X + 80, r1, 288);
        { var (h, s, v) = ((Col)D.TatPal[sCol - 1]).ToHsv(); bool ch = false; float hx = X + 380, sw2 = 74;
          ch |= Ui.Slider("tsH", ref h, hx, r1 + 4, sw2, (tx, ty, tw) => { for (int i = 0; i < 24; i++) Raylib.DrawRectangleV(new(tx + tw * i / 24, ty + 2), new(tw / 24 + .5f, 6), Col.Hsv(i / 24f, .75f, .9f).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
          float hh = h, ss0 = s, vv = v; ch |= Ui.Slider("tsS", ref s, hx + sw2 + 10, r1 + 4, sw2, (tx, ty, tw) => { Raylib.DrawRectangleGradientH((int)tx, (int)ty + 2, (int)tw, 6, Col.Hsv(hh, 0, MathF.Max(.3f, vv)).Rl(), Col.Hsv(hh, 1, MathF.Max(.3f, vv)).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
          ch |= Ui.Slider("tsV", ref v, hx + sw2 * 2 + 20, r1 + 4, sw2, (tx, ty, tw) => { Raylib.DrawRectangleGradientH((int)tx, (int)ty + 2, (int)tw, 6, Color.Black, Col.Hsv(hh, ss0, 1).Rl()); Raylib.DrawRectangleLinesEx(new Rectangle(tx - 1, ty + 1, tw + 2, 8), 1, Ui.Ink); });
          if (ch) { D.TatPal[sCol - 1] = Col.Hsv(h, s, v).Hex; pvDirty = true; } }
        float r2 = 282; Font.Draw("Size", X + 16, r2 + 3, Ui.Ink, 1.1f); float sv = (sSize - 1) / 7f; if (Ui.Slider("tsize", ref sv, X + 82, r2 + 4, 84)) sSize = 1 + (int)MathF.Round(sv * 7); Font.Draw(sSize.ToString(), X + 176, r2 + 3, Ui.Ink);
        if (Ui.Button("smirA", "Mirror along", X + 196, r2, 86, 19, "btnP", icon: "tMirA", on: mirAlong, text: .85f, tip: "Mirror between the shoulder and wrist halves")) mirAlong = !mirAlong;
        if (Ui.Button("smirC", "Mirror across", X + 286, r2, 90, 19, "btnP", icon: "tMirC", on: mirAcross, text: .85f, tip: "Mirror between the top and bottom of the arm")) mirAcross = !mirAcross;
        if (Ui.Button("srep", $"Repeat {repeat}x", X + 380, r2, 80, 19, "btnP", icon: "tRepeat", on: repeat > 1, text: .85f, tip: "Repeat what you draw down the arm")) repeat = Repeats[(Array.IndexOf(Repeats, repeat) + 1) % Repeats.Length];
        if (Ui.Button("sfill", filled ? "Filled shapes" : "Outline shapes", X + 464, r2, 92, 19, "btnP", on: filled, text: .85f, tip: "Boxes and circles drawn solid or as an outline")) filled = !filled;
        // selection and whole-arm actions
        float r3 = 306; bool sel = hasSel; Font.Draw("Edit", X + 16, r3 + 3, Ui.Ink, 1.1f);
        if (Ui.Button("scut", "Cut", X + 80, r3, 40, 19, "btnP", text: .85f, disabled: !sel, tip: "Ctrl+X")) SelCut();
        if (Ui.Button("scopy", "Copy", X + 123, r3, 44, 19, "btnP", text: .85f, disabled: !sel, tip: "Ctrl+C")) SelCopy();
        if (Ui.Button("spaste", "Paste", X + 170, r3, 46, 19, "btnP", text: .85f, disabled: clip == null, tip: "Ctrl+V")) SelPaste();
        if (Ui.Button("sflipA", "Flip along", X + 219, r3, 64, 19, "btnP", text: .85f, disabled: !sel)) SelFlip(true);
        if (Ui.Button("sflipC", "Flip across", X + 286, r3, 66, 19, "btnP", text: .85f, disabled: !sel)) SelFlip(false);
        if (Ui.Button("sdel", "Delete", X + 355, r3, 48, 19, "btnP", text: .85f, disabled: !sel, tip: "Delete")) SelDelete();
        if (Ui.Button("sclear", "Clear arm", X + 430, r3, 62, 19, "btnR", text: .85f)) { Commit(); hasSel = false; Push(); Array.Clear(Cur); pvDirty = true; }
        if (Ui.Button("sother", "Copy to other arm", X + 496, r3, 126, 19, "btnB", text: .85f, tip: "Puts this design on the other arm too, mirrored")) { Commit(); Push(1 - sArm); D.Art[1 - sArm] = Cur.ToArray(); pvDirty = true; Snd.Play("tick"); }
        // help line
        string hint = Tools.First(t => t.t == tool).hint; Font.Draw(hint, X + 16, 334, Ui.Soft, .9f);
        Font.Draw("Ctrl+Z undo", X + 624, 334, Ui.Soft, .85f, 1);
    }
}

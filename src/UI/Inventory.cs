using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// The inventory satchel: one page, many pockets.
public sealed partial class GameScreen
{
    ItemStack held; int heldFrom = -1; bool dragging; Vector2 dragAt; Vector2 heldPos, heldVel; float trashOpen, lastClick; int lastSlot = -1, tipSlot, tipShown; float tipA; readonly AvatarAnim bagAn = new(); readonly Painter bagPt = new(120, 190); Texture2D bagTex; readonly AvatarPainter bagAp = new(); bool bagReady; Texture2D satchel, pocket, pocketHot;
    const float SW = 29, SG = 2;
    void ReturnHeld()
    {
        if (held.Empty) { held = default; return; } var d = Store.Data; if (heldFrom >= 0 && d.Slots[heldFrom].Empty) d.Slots[heldFrom] = held; else { int left = Bag.Add(d, held.Id, held.N); if (left > 0) loot.Add(new Loot { Id = held.Id, N = left, P = P.P + new Vector2(0, -20), V = new(P.Dir * 60, -80) }); }
        held = default; heldFrom = -1; dragging = false;
    }
    void Throw(bool one) { if (held.Empty) return; int n = one ? 1 : held.N; loot.Add(new Loot { Id = held.Id, N = n, P = P.P + new Vector2(P.Dir * 10, -34), V = new(P.Dir * 150, -120), T = -.9f }); held.N -= n; if (held.N <= 0) { held = default; heldFrom = -1; } dragging = false; Snd.Play("cloth", .7f); }
    bool Dropped => Ui.Click || (Ui.Release && dragging && Vector2.Distance(dragAt, Ui.Mouse) > 7);
    void SlotUi(int i, float x, float y)
    {
        var d = Store.Data; var (a, _, over) = Ui.Widget("slot" + i, x, y, SW, SW, null, true); over = Ui.Over(x, y, SW, SW); if (over && !a.SlotWas) Snd.Play("hover", .2f, 1.1f + i % Bag.Cols * .03f); a.SlotWas = over; var it = d.Slots[i]; float born = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp(openT * 4.4f - (i % Bag.Cols * .045f + i / Bag.Cols * .06f))); if (born <= 0) return; bool hand = d.Hand == i && !it.Empty;
        Ui.PushScale(x + SW / 2, y + SW / 2, born, born); Raylib.DrawTexturePro(over ? pocketHot : pocket, new Rectangle(0, 0, 58, 58), new Rectangle(x, y, SW, SW), Vector2.Zero, 0, Color.White);
        if (hand) { float p = 1 + M.Sin(Ui.T * 4) * .6f; Ui.GlowAt(x + SW / 2, y + SW / 2, 22, new Color(255, 206, 110, 90)); Raylib.DrawRectangleRoundedLinesEx(new Rectangle(x + 1.5f - p * .4f, y + 1.5f - p * .4f, SW - 3 + p * .8f, SW - 3 + p * .8f), .34f, 5, 1.6f, new Color(255, 206, 100, 255)); }
        if (!it.Empty) { float wob = over && held.Empty ? M.Sin(Ui.T * 9) * 4 : 0, sc = 1 + a.Hover.X * .14f + a.Press.X * .12f; if (Blocks.Glows((T)it.Id)) Ui.GlowAt(x + SW / 2, y + SW / 2, 15, ((Col)Blocks.All[it.Id].Glow).Rl(.45f)); Raylib.DrawEllipse((int)(x + SW / 2), (int)(y + SW - 5), 8, 2, new Color(30, 14, 14, 110)); Items.Draw(it.Id, x + SW / 2, y + SW / 2 - 1, 20, sc, wob); if (it.N > 1) Font.Draw(it.N.ToString(), x + SW - 3, y + SW - 11, Ui.Cream, 1, 1, Ui.Ink); }
        Ui.PopScale(); if (!over) return; if (!it.Empty && held.Empty) tipSlot = i;
        if (Ui.Click)
        {
            bool dbl = i == lastSlot && Ui.T - lastClick < .32f; lastSlot = i; lastClick = Ui.T;
            if (dbl && (!held.Empty || !it.Empty)) { if (!held.Empty && it.Empty) { d.Slots[i] = held; held = default; heldFrom = -1; } if (!d.Slots[i].Empty) { d.Hand = -2; Equip(i); Snd.Play("confirm", .4f, 1.2f); } dragging = false; a.Press.V += 12; return; }
            if (held.Empty) { if (!it.Empty) { held = it; heldFrom = i; d.Slots[i] = default; dragging = true; dragAt = Ui.Mouse; heldPos = new(x + SW / 2, y + SW / 2); Snd.Play("pick", .7f, 1.1f); } } else Put(i, held.N, a);
        }
        else if (Ui.Release && dragging) { if (Vector2.Distance(dragAt, Ui.Mouse) > 7 && !held.Empty) Put(i, held.N, a); dragging = false; }
        else if (Ui.RightClick) { if (held.Empty) { if (!it.Empty) { int half = (it.N + 1) / 2; held = new(it.Id, half); heldFrom = i; d.Slots[i].N -= half; if (d.Slots[i].N <= 0) d.Slots[i] = default; heldPos = Ui.Mouse; Snd.Play("pick", .6f, 1.3f); } } else Put(i, 1, a); }
    }
    void Put(int i, int n, Ui.An a)
    {
        var d = Store.Data; ref var s = ref d.Slots[i]; a.Press.V += 10; bool wasHand = heldFrom >= 0 && d.Hand == heldFrom;
        if (s.Empty) { s = new(held.Id, n); held.N -= n; Snd.Play("pick", .7f, .9f); if (wasHand && held.N <= 0) d.Hand = i; }
        else if (s.Id == held.Id) { int k = Math.Min(n, Bag.MaxStack - s.N); if (k <= 0) { Snd.Play("error", .3f); return; } s.N += k; held.N -= k; Snd.Play("pick", .7f, .95f); if (wasHand && held.N <= 0) d.Hand = i; }
        else if (n == held.N) { (s, held) = (held, s); if (wasHand) d.Hand = i; heldFrom = -1; Snd.Play("pick", .7f, 1); dragging = false; return; }
        else { Snd.Play("error", .3f); return; }
        if (held.N <= 0) { held = default; heldFrom = -1; } dragging = false;
    }
    void Satchel(int w, int h)
    {
        if (satchel.Id != 0 && satchel.Width == w && satchel.Height == h) return; if (satchel.Id != 0) Raylib.UnloadTexture(satchel);
        // canvas lining framed in stitched leather with brass corners
        var p = new Painter(w, h); p.View(1, 0, 0); void RR(float x, float y, float ww, float hh, float r) { p.Rect(x + r, y, ww - r * 2, hh).Rect(x, y + r, ww, hh - r * 2).Ellipse(x + r, y + r, r, r).Ellipse(x + ww - r, y + r, r, r).Ellipse(x + r, y + hh - r, r, r).Ellipse(x + ww - r, y + hh - r, r, r); }
        Col leather(float x, float y) => ((Col)0x7c4a30).Shade(M.Noise(x * .12f, y * .12f, 3) * .12f + M.Noise(x * .5f, y * .5f, 4) * .08f + (M.Hash((int)x / 2, (int)y / 2, 3) > .96f ? -.12f : 0)); p.Begin(); RR(0, 0, w, h, 16); p.Flat(0x3a201c); p.Begin(); RR(1, 1, w - 2, h - 2, 15); p.Shade(new Ink(leather, 5, .9f, 0) { Ao = 0, Grad = .1f });
        p.Begin(); RR(11, 11, w - 22, h - 22, 9); p.Flat(0x3a201c); p.Begin(); RR(12, 12, w - 24, h - 24, 8); p.Shade(new Ink((x, y) => ((Col)0xe6cfa2).Shade((((int)x + (int)y) % 3 == 0 ? -.05f : 0) + (((int)x - (int)y + 900) % 4 == 0 ? .05f : 0) + M.Noise(x * .02f, y * .02f, 8) * .10f - M.Clamp((14 - MathF.Min(MathF.Min(x - 12, w - 12 - x), MathF.Min(y - 12, h - 12 - y))) / 14) * .3f), 1, 0, 0) { Ao = 0, Grad = 0 });
        Col st = 0xf0d49a, sd = 0x4a2a20; for (int x = 18; x < w - 20; x += 7) foreach (int y in new[] { 6, h - 7 }) { p.Begin().Rect(x, y + 1, 4, 1); p.Flat(sd); p.Begin().Rect(x, y, 4, 1); p.Flat(st); } for (int y = 18; y < h - 20; y += 7) foreach (int x in new[] { 6, w - 7 }) { p.Begin().Rect(x + 1, y, 1, 4); p.Flat(sd); p.Begin().Rect(x, y, 1, 4); p.Flat(st); }
        foreach (int cx in new[] { 0, 1 }) foreach (int cy in new[] { 0, 1 }) { float X(float v) => cx == 0 ? v : w - v; float Y(float v) => cy == 0 ? v : h - v; p.Begin().Poly(new(X(2), Y(17)), new(X(4), Y(7)), new(X(7), Y(4)), new(X(17), Y(2)), new(X(21), Y(8)), new(X(12), Y(12)), new(X(8), Y(21))); p.Shade(new Ink(0xd8a850, 3, .9f, .9f)); p.Begin().Ellipse(X(8), Y(8), 2, 2); p.Shade(new Ink(0xf6e0a0, 1.4f, .8f, .6f)); }
        satchel = p.ToTexture();
        if (pocket.Id == 0)
        {
            Texture2D Pk(Col cloth, Col rim) { var q = new Painter(58, 58); q.View(1, 0, 0); void R2(float x, float y, float s, float r) => q.Rect(x + r, y, s - r * 2, s).Rect(x, y + r, s, s - r * 2).Ellipse(x + r, y + r, r, r).Ellipse(x + s - r, y + r, r, r).Ellipse(x + r, y + s - r, r, r).Ellipse(x + s - r, y + s - r, r, r); q.Begin(); R2(0, 0, 58, 12); q.Flat(0x4a2c24); q.Begin(); R2(2, 2, 54, 10); q.Flat(rim); q.Begin(); R2(5, 5, 48, 8); q.Shade(new Ink((x, y) => cloth.Shade((((int)x / 2 + (int)y / 2) % 2 == 0 ? .03f : -.03f) - M.Clamp((16 - y) / 16) * .34f - M.Clamp((12 - x) / 12) * .12f + M.Clamp((y - 40) / 16) * .12f), 1, 0, 0) { Ao = 0, Grad = 0 }); var t = q.ToTexture(); Raylib.SetTextureFilter(t, TextureFilter.Bilinear); return t; }
            pocket = Pk(0x8a6248, 0xa87852); pocketHot = Pk(0xb48a5c, 0xf0c470);
        }
    }
    void Portrait(float x, float y, float w, float h)
    {
        if (!bagReady) { bagTex = bagPt.ToTexture(); bagReady = true; } bagAn.IdleStyle = P.D.Idle; bagAn.Wind = .25f + MathF.Abs(Env.Wind) * .3f; bagAn.Look = new(M.Clamp((Ui.Mouse.X - x - w / 2) / 90, -1, 1), M.Clamp((Ui.Mouse.Y - y - 40) / 90, -1, 1)); if (bagAn.Update(Ui.DT) || openT < .05f) { bagPt.Clear(); bagAp.Draw(bagPt, P.D, bagAn.Pose, 1.42f, 60, 180, outlinePx: 1); bagPt.Upload(bagTex); }
        Raylib.DrawRectangleRounded(new Rectangle(x - 3, y - 3, w + 6, h + 6), .09f, 6, new Color(58, 32, 28, 255)); Raylib.DrawRectangleRounded(new Rectangle(x - 2, y - 2, w + 4, h + 4), .085f, 6, new Color(196, 138, 80, 255)); Raylib.DrawRectangleRounded(new Rectangle(x - .5f, y - .5f, w + 1, h + 1), .07f, 6, new Color(58, 32, 28, 255));
        // the window shows the place you are standing in
        Ui.Clip(x, y, w, h); Raylib.DrawRectangleGradientV((int)x, (int)y, (int)w + 1, (int)h, Env.Top.Shade(.1f).Rl(), Env.Hor.Shade(.1f).Rl()); var set = back.Get(W.BiomeAt(P.P.X / 24)); Col lit = Env.Amb * .82f + Env.Sun * .3f; if (set != null) for (int i = 1; i < 4; i++) Raylib.DrawTexturePro(set[i], new Rectangle(P.P.X * .05f + i * 60, 70, w, 150), new Rectangle(x, y + h * .22f + i * 12, w, 150), Vector2.Zero, 0, Col.Lerp(lit, Env.Hor, .5f - i * .14f).Rl()); Col g = World.GrassCol(P.P.X / 24) * lit; Raylib.DrawRectangleV(new(x, y + h - 16), new(w, 16), g.Rl()); Raylib.DrawRectangleV(new(x, y + h - 16), new(w, 2), g.Shade(.3f).Rl());
        Raylib.DrawEllipse((int)(x + w / 2), (int)(y + h - 10), 28, 4, new Color(30, 16, 22, 110)); Ui.Tex(bagTex, x + w / 2 - 60, y + h - 190, 1, Color.White); Ui.Unclip();
    }
    void BagUi()
    {
        var d = Store.Data; float gw = Bag.Cols * (SW + SG) - SG, gh = Bag.Rows * (SW + SG) - SG, pw = 104; float W = pw + gw + 62, H = gh + 92; Satchel((int)W, (int)H); float x = MathF.Round((Ui.VW - W) / 2), y = MathF.Round((Ui.VH - H) / 2); tipSlot = -1; Ui.Tex(satchel, x + 3, y + 5, 1, new Color(20, 8, 10, 120)); Ui.Tex(satchel, x, y, 1, Color.White);
        float px = x + 21, py = y + 40; Font.Draw(P.D.Name, px + pw / 2, y + 20, Ui.Ink, 1.75f, .5f); Portrait(px, py, pw, gh - 42);
        // what you are holding
        float hy = py + gh - 34; var (ha, _, _) = Ui.Widget("handslot", px, hy, pw, 34, null, true); bool hOver = Ui.Over(px, hy, pw, 34); Raylib.DrawRectangleRounded(new Rectangle(px, hy, pw, 34), .3f, 6, new Color(74, 44, 36, 255)); Raylib.DrawRectangleRounded(new Rectangle(px + 1.5f, hy + 1.5f, pw - 3, 31), .28f, 6, hOver ? new Color(240, 196, 112, 255) : new Color(196, 138, 80, 255)); Raylib.DrawRectangleRounded(new Rectangle(px + 3, hy + 3, pw - 6, 28), .26f, 6, new Color(246, 230, 196, 255));
        bool any = d.Hand >= 0 && !d.Slots[d.Hand].Empty; if (any) Items.Draw(d.Slots[d.Hand].Id, px + 20, hy + 17, 20, 1.1f + ha.Press.X * .2f, M.Sin(Ui.T * 2.6f) * 3); else Fist(px + 20, hy + 17, 1.1f); Font.Draw("In hand", px + 38, hy + 7, Ui.Soft, .85f); var hn = any ? Items.Name(d.Slots[d.Hand].Id) : "Nothing"; Font.Draw(hn, px + 38, hy + 17, Ui.Ink, Font.Width(hn) > pw - 44 ? .85f : 1); if (hOver) Ui.Tip = !held.Empty ? "Hold this" : any ? "Click to empty your hand" : "Drop something here to hold it";
        if (hOver && held.Empty && Ui.Click && any) { Equip(-1); ha.Press.V += 8; }
        else if (hOver && !held.Empty && Dropped) { int to = heldFrom >= 0 && d.Slots[heldFrom].Empty ? heldFrom : Array.FindIndex(d.Slots, s => s.Empty); if (to >= 0) { d.Slots[to] = held; held = default; heldFrom = -1; dragging = false; d.Hand = -2; Equip(to); ha.Press.V += 10; Snd.Play("confirm", .4f, 1.2f); } }
        float gx = px + pw + 18, gy = y + 40; Font.Draw("Satchel", gx, y + 20, Ui.Ink, 1.75f); int used = Bag.Used(d); float bw = 92; Raylib.DrawRectangleRounded(new Rectangle(gx + gw - bw - 48, y + 24, bw, 8), 1, 4, new Color(74, 44, 36, 255)); if (used > 0) Raylib.DrawRectangleRounded(new Rectangle(gx + gw - bw - 47, y + 25, MathF.Max(6, (bw - 2) * used / Bag.Size), 6), 1, 4, used > Bag.Size * .9f ? Ui.Red : Ui.Gold); Font.Draw(used + " / " + Bag.Size, gx + gw, y + 24, Ui.Soft, 1, 1);
        for (int i = 0; i < Bag.Size; i++) SlotUi(i, gx + i % Bag.Cols * (SW + SG), gy + i / Bag.Cols * (SW + SG));
        // foot: what you are pointing at, sort, throw away
        float fy = gy + gh + 7; tipA = M.Damp(tipA, tipSlot >= 0 ? 1 : 0, 14, Ui.DT); if (tipSlot >= 0 && !d.Slots[tipSlot].Empty) tipShown = d.Slots[tipSlot].Id;
        if (tipA > .03f && tipShown > 0) { var q = Blocks.All[tipShown]; Font.Draw(q.Name, gx, fy, Raylib.Fade(Ui.Ink, tipA), 1.25f); string how = q.Time < .5f ? "very soft" : q.Time < .8f ? "soft" : q.Time < 1.3f ? "firm" : q.Time < 2 ? "hard" : "very hard"; Font.Draw(how, gx + Font.Width(q.Name, 1.25f) + 8, fy + 2, Raylib.Fade(Ui.Orange, tipA)); Font.Draw(q.Text, gx, fy + 13, Raylib.Fade(Ui.Soft, tipA)); }
        if (tipA < .5f) Font.Draw("Double-click to hold. Right-click splits. Drop outside to throw.", gx, fy + 6, Raylib.Fade(Ui.Soft, 1 - tipA * 2), .9f);
        float bx = gx + gw; if (Ui.Button("sort", "Sort", bx - 92, fy - 1, 56, 22, "btnB", null, "Stack and tidy")) { ReturnHeld(); Bag.Sort(d); Snd.Play("cloth"); }
        float tx = bx - 31; var (ta, _, _) = Ui.Widget("trash", tx, fy - 2, 31, 25, null, true); bool tOver = Ui.Over(tx, fy - 2, 31, 25); trashOpen = M.Damp(trashOpen, tOver && !held.Empty ? 1 : 0, 14, Ui.DT); Ui.PushScale(tx + 15, fy + 10, 1 + trashOpen * .15f, 1 + trashOpen * .15f); Ui.Nine("btnR", tx, fy - 2, 31, 25, Color.White); Raylib.DrawRectangleV(new(tx + 10, fy + 7), new(11, 10), Ui.Ink); Raylib.DrawRectangleV(new(tx + 11, fy + 8), new(9, 8), Ui.Cream); for (int k = 0; k < 3; k++) Raylib.DrawRectangleV(new(tx + 12.6f + k * 2.8f, fy + 9), new(1, 6), Ui.Soft); Rlgl.PushMatrix(); Rlgl.Translatef(tx + 10, fy + 6, 0); Rlgl.Rotatef(-trashOpen * 32, 0, 0, 1); Raylib.DrawRectangleV(new(-1, -2), new(13, 3), Ui.Ink); Raylib.DrawRectangleV(new(0, -1.4f), new(11, 1.6f), Ui.Cream); Rlgl.PopMatrix(); Ui.PopScale(); if (tOver && held.Empty) Ui.Tip = "Drop things here to destroy them";
        if (tOver && !held.Empty && Dropped) { held = default; heldFrom = -1; dragging = false; Snd.Play("cloth", .8f, .7f); ta.Press.V += 10; }
        bool outside = !Ui.Over(x, y, W, H); if (outside && !held.Empty && (Dropped || Ui.RightClick)) Throw(Ui.RightClick); else if (outside && held.Empty && Ui.Click) ClosePanel(); if (Ui.Release) dragging = false; HeldUi();
    }
    void HeldUi()
    {
        if (held.Empty) return; var v = heldPos; heldPos = Store.Cfg.ReduceMotion ? Ui.Mouse : M.Damp(heldPos, Ui.Mouse, 24, Ui.DT); heldVel = M.Damp(heldVel, (heldPos - v) / MathF.Max(.001f, Ui.DT), 12, Ui.DT); Raylib.DrawEllipse((int)heldPos.X + 2, (int)heldPos.Y + 14, 10, 3, new Color(30, 14, 14, 90)); Items.Draw(held.Id, heldPos.X, heldPos.Y, 20, 1.35f, M.Clamp(heldVel.X * .07f, -28, 28)); if (held.N > 1) Font.Draw(held.N.ToString(), heldPos.X + 16, heldPos.Y + 5, Ui.Cream, 1, 1, Ui.Ink);
    }
}

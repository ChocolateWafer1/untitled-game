using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// The hotbar: one straight column of slots at the left edge that scrolls through everything you own.
public sealed partial class GameScreen
{
    Spring barPop = new(); float barSel, nameT = 9; readonly List<(int id, int n, float t)> gains = new(); public bool OverBar; float barWheel, barHover;
    const float BarX = 28, BarGap = 34, SlotOff = 24, SlotOn = 30;
    /// Everything you own, in bag order, with bare hands first. Values are slot indices, -1 for hands.
    List<int> BarList() { var l = new List<int> { -1 }; var s = Store.Data.Slots; for (int i = 0; i < s.Length; i++) if (!s[i].Empty) l.Add(i); return l; }
    void Gained(int id, int n) { for (int i = 0; i < gains.Count; i++) if (gains[i].id == id) { gains[i] = (id, gains[i].n + n, MathF.Min(gains[i].t, .25f)); return; } gains.Add((id, n, 0)); if (gains.Count > 5) gains.RemoveAt(0); }
    void Equip(int slot) { var d = Store.Data; if (d.Hand == slot) return; d.Hand = slot; nameT = 0; Snd.Play("tick", .6f, .9f + rng.F() * .2f); barPop.V = 7; }
    void BarUpdate(float dt, bool control)
    {
        var d = Store.Data; if (d.Hand < -1 || (d.Hand >= 0 && d.Slots[d.Hand].Empty)) d.Hand = -1; nameT += dt; var list = BarList(); int n = list.Count, at = Math.Max(0, list.IndexOf(d.Hand));
        if (control && Ui.Wheel != 0 && !OverAdmin) { barWheel += Ui.Wheel; while (MathF.Abs(barWheel) >= 1) { int step = barWheel > 0 ? -1 : 1; barWheel += step; at = M.Mod(at + step, n); Equip(list[at]); } }
        // items gained or lost elsewhere in the list must not make the column move
        if (d.Hand == barHand && at != barAt) { barSel += at - barAt; } barHand = d.Hand; barAt = at;
        // long lists loop and take the short way round; short ones are a plain column with two ends
        bool loop = n >= Loop; if (loop) { float turns = MathF.Round((barSel - at) / n); barSel -= turns * n; } else barSel = M.Clamp(barSel, 0, n - 1);
        barSel = Store.Cfg.ReduceMotion ? at : M.Damp(barSel, at, 18, dt); if (MathF.Abs(barSel - at) < .004f) barSel = at; barPop.Update(0, 300, 24, dt);
        float cy = Ui.VH / 2; OverBar = control && MathF.Abs(Ui.Mouse.X - BarX) < 20 && MathF.Abs(Ui.Mouse.Y - cy) < BarGap * 3.5f; barHover = M.Damp(barHover, OverBar ? 1 : 0, 10, dt);
        if (OverBar && Ui.Click) { float my = Ui.Mouse.Y - cy; int k = (int)MathF.Round(my / BarGap); foreach (var (off, slot) in Shown(list)) if (MathF.Abs(off - k) < .4f) { Equip(slot); break; } }
        for (int i = gains.Count - 1; i >= 0; i--) { var g = gains[i]; g.t += dt; if (g.t > 2.6f) gains.RemoveAt(i); else gains[i] = g; }
    }
    const int Loop = 9; int barHand = -9, barAt;
    /// The entries near the middle and how far each one is from being chosen.
    List<(float off, int slot)> Shown(List<int> list)
    {
        int n = list.Count; var o = new List<(float, int)>(); if (n >= Loop) { int b = (int)MathF.Round(barSel); for (int k = -4; k <= 4; k++) o.Add((b + k - barSel, list[M.Mod(b + k, n)])); } else for (int q = 0; q < n; q++) o.Add((q - barSel, list[q])); return o;
    }
    /// For tests: where every shown entry is drawn, in rows from the middle.
    public List<(float off, int slot)> BarForTest() => Shown(BarList());
    /// Bare hands: an open palm in the character's own skin tone, with their sleeve at the wrist. Painted at the exact size it is shown.
    public static Painter HandArt(int size, Col skin, Col sleeve)
    {
        var p = new Painter(size, size); float k = size / 30f; p.View(k, 0, 0); Vector2 V(float x, float y) => new(x, y); Ink I(Col c, float line) => new Ink(c, 2.2f, .55f, line) { Ao = 0, Grad = .06f, LinePx = 1 };
        p.Begin().Poly(V(11.4f, 22), V(20, 22), V(20.4f, 29), V(11, 29)); p.Shade(I(skin.Shade(-.1f), 0));
        foreach (var (a, ra, b, rb, t) in new[] { (V(20.6f, 17), 2.1f, V(24, 8.6f), 1.75f, -.12f), (V(17.9f, 15.4f), 2.35f, V(19.3f, 5f), 2f, -.04f), (V(14.4f, 15), 2.4f, V(14.2f, 3.6f), 2.05f, .02f), (V(11f, 15.6f), 2.3f, V(9f, 5.8f), 1.95f, .06f) }) { p.Begin().Capsule(a, ra, b, rb); p.Shade(I(skin.Shade(t), .62f)); }
        p.Begin().Blob(V(8.6f, 17), V(10.4f, 14.6f), V(15.4f, 14f), V(20.4f, 15f), V(22.6f, 17.4f), V(21.4f, 22.2f), V(19.2f, 25.4f), V(12.4f, 25.4f), V(9.2f, 22)); p.Shade(new Ink((x, y) => skin.Shade(M.Clamp((x - 18) * .05f + (y - 22) * .03f, 0, .3f) * -1), 1, 0, 0) { Ao = 0, Grad = 0 });
        p.Begin().Capsule(V(11.4f, 22), 2.7f, V(5.2f, 15.4f), 1.9f); p.Shade(I(skin.Shade(.04f), .55f)); p.Begin().Ellipse(12.8f, 21.6f, 3.2f, 3); p.Flat(skin);
        Col cr = skin.Shade(-.26f); p.Curve(V(20.6f, 18.6f), V(17f, 19.6f), V(13.6f, 18.6f), 1 / k, cr, false); p.Curve(V(14.2f, 20.4f), V(15.2f, 22), V(14.4f, 23.6f), 1 / k, cr, false);
        p.Begin().Poly(V(9.8f, 25), V(21.6f, 25), V(22, 29), V(9.4f, 29)); p.Shade(new Ink(sleeve, 2, .7f, .5f) { Ao = 1, LinePx = 1 }); p.Outline(0x2c1a1c, 1); return p;
    }
    static readonly Dictionary<(int, int, int), Texture2D> hands = new();
    void Hand(float x, float y, int size, float scale = 1, float a = 1)
    {
        Col skin = P.D.Skin, sl = P.D[Slot.Jacket].Id > 0 ? P.D[Slot.Jacket].A : P.D[Slot.Top].A; var key = (size, skin.Hex, sl.Hex); if (!hands.TryGetValue(key, out var t)) hands[key] = t = HandArt(size, skin, sl).ToTexture(); float w = size * scale; Raylib.DrawTexturePro(t, new Rectangle(0, 0, size, size), new Rectangle(x, y, w, w), new(w / 2, w / 2), 0, new Color(255, 255, 255, (int)(255 * M.Clamp(a))));
    }
    void Bar()
    {
        var d = Store.Data; var list = BarList(); float cy = MathF.Round(Ui.VH / 2), x = BarX;
        foreach (var (off, slot) in Shown(list).OrderByDescending(q => MathF.Abs(q.off)))
        {
            float ab = MathF.Abs(off), near = M.Smooth(1, 0, ab), fade = M.Clamp(4.1f - ab); if (fade <= .02f) continue; float y = cy + off * BarGap + MathF.Sign(off) * M.Clamp(ab) * 3, size = M.Lerp(SlotOff, SlotOn, near) + barPop.X * near, h = size / 2; bool sel = near > .5f; bool hov = OverBar && MathF.Abs(Ui.Mouse.Y - y) < BarGap / 2 && !sel;
            if (slot < 0) Hand(x, y, sel ? 34 : 28, sel ? (size + 4) / 34 : (size + 4) / 28 + (hov ? .08f : 0), fade); else if (sel) Items.Draw(d.Slots[slot].Id, x, y, 30, size / 30, 0, fade); else Items.Draw(d.Slots[slot].Id, x, y, 24, size / 24 + (hov ? .08f : 0), 0, fade);
            if (slot >= 0 && d.Slots[slot].N > 1) Font.Draw(d.Slots[slot].N.ToString(), x + h + 1, y + h - 8, Raylib.Fade(Color.White, fade), 1, 1, Raylib.Fade(new Color(30, 18, 22, 255), fade));
        }
        // the name slides out beside it for a moment
        float na = MathF.Max(M.Clamp(nameT * 6) * M.Clamp((2.2f - nameT) * 2.4f), barHover); if (na > .02f) { string s = d.Hand < 0 || d.Slots[d.Hand].Empty ? "Bare hands" : Items.Name(d.Slots[d.Hand].Id); float w = Font.Width(s, 1.25f) + 20, nx = x + 18 + Ease.OutCubic(M.Clamp(na)) * 6; Ui.Nine("tip", nx, cy - 10, w, 20, Raylib.Fade(Color.White, na)); Font.Draw(s, nx + 10, cy - 5.5f, Raylib.Fade(Ui.Cream, na), 1.25f); }
        // what you just picked up
        for (int i = 0; i < gains.Count; i++) { var (id, gn, t) = gains[gains.Count - 1 - i]; float a = M.Clamp(t * 8) * M.Clamp((2.6f - t) * 2); float gy = Ui.VH - 22 - i * 16, gx = 14 - (1 - Ease.OutCubic(M.Clamp(t * 6))) * 24; Items.Draw(id, gx + 4, gy + 4, 12, 1 + MathF.Max(0, .25f - t) * 1.4f, 0, a); Font.Draw("+" + gn + " " + Items.Name(id), gx + 15, gy, Raylib.Fade(Ui.Cream, a), 1, 0, new Color(44, 24, 26, 220)); }
    }
}

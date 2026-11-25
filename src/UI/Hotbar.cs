using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// The hotbar: one straight column of slots at the left edge that scrolls through everything you own.
public sealed partial class GameScreen
{
    Spring barPos = new(), barPop = new(); float barSel, nameT = 9; static Shader grey; static int greyAmt, greyDark; static bool greyOk; readonly List<(int id, int n, float t)> gains = new(); public bool OverBar; float barWheel, barHover;
    const float BarX = 28, BarGap = 34, SlotOff = 24, SlotOn = 30;
    /// Everything you own, in bag order, with bare hands first. Values are slot indices, -1 for hands.
    List<int> BarList() { var l = new List<int> { -1 }; var s = Store.Data.Slots; for (int i = 0; i < s.Length; i++) if (!s[i].Empty) l.Add(i); return l; }
    void Gained(int id, int n) { for (int i = 0; i < gains.Count; i++) if (gains[i].id == id) { gains[i] = (id, gains[i].n + n, MathF.Min(gains[i].t, .25f)); return; } gains.Add((id, n, 0)); if (gains.Count > 5) gains.RemoveAt(0); }
    void Equip(int slot) { var d = Store.Data; if (d.Hand == slot) return; d.Hand = slot; nameT = 0; Snd.Play("tick", .6f, .9f + rng.F() * .2f); barPop.V = 7; }
    void BarUpdate(float dt, bool control)
    {
        var d = Store.Data; if (d.Hand < -1 || (d.Hand >= 0 && d.Slots[d.Hand].Empty)) d.Hand = -1; nameT += dt; var list = BarList(); int n = list.Count, at = Math.Max(0, list.IndexOf(d.Hand));
        if (control && Ui.Wheel != 0 && !OverAdmin) { barWheel += Ui.Wheel; while (MathF.Abs(barWheel) >= 1) { int step = barWheel > 0 ? -1 : 1; barWheel += step; at = M.Mod(at + step, n); Equip(list[at]); } }
        // take the short way round the loop
        float want = at; if (n > 2) { while (want - barSel > n / 2f) barSel += n; while (barSel - want > n / 2f) barSel -= n; } if (Store.Cfg.ReduceMotion || MathF.Abs(want - barSel) > n + 1) { barSel = want; barPos = new(want); } else { barPos.X = barSel; barPos.Update(want, 260, 24, dt); barSel = barPos.X; } barPop.Update(0, 300, 15, dt);
        float cy = Ui.VH / 2; OverBar = control && MathF.Abs(Ui.Mouse.X - BarX) < 20 && MathF.Abs(Ui.Mouse.Y - cy) < BarGap * 3.5f; barHover = M.Damp(barHover, OverBar ? 1 : 0, 10, dt);
        if (OverBar && Ui.Click) { float my = Ui.Mouse.Y - cy; int k = (int)MathF.Round(my / BarGap); foreach (var (off, slot) in Shown(list)) if (MathF.Abs(off - k) < .4f) { Equip(slot); break; } }
        for (int i = gains.Count - 1; i >= 0; i--) { var g = gains[i]; g.t += dt; if (g.t > 2.6f) gains.RemoveAt(i); else gains[i] = g; }
    }
    /// The entries near the middle and how far each one is from being chosen. Long lists wrap round like a loop.
    List<(float off, int slot)> Shown(List<int> list)
    {
        int n = list.Count; var o = new List<(float, int)>(); if (n >= 8) { int b = (int)MathF.Round(barSel); for (int k = -4; k <= 4; k++) o.Add((b + k - barSel, list[M.Mod(b + k, n)])); return o; }
        float s = M.Mod2(barSel, n); for (int q = 0; q < n; q++) { float f = q - s; if (n > 2) { if (f > n / 2f) f -= n; if (f < -n / 2f) f += n; } o.Add((f, list[q])); } return o;
    }
    static void Greyed(float amt, float dark, Action f)
    {
        if (!greyOk) { greyOk = true; grey = Raylib.LoadShaderFromMemory(null, "#version 330\nin vec2 fragTexCoord; in vec4 fragColor; out vec4 o; uniform sampler2D texture0; uniform float amt,dark; void main(){ vec4 c=texture(texture0,fragTexCoord)*fragColor; float l=dot(c.rgb,vec3(.3,.59,.11)); o=vec4(mix(c.rgb,vec3(l),amt)*dark,c.a); }"); greyAmt = Raylib.GetShaderLocation(grey, "amt"); greyDark = Raylib.GetShaderLocation(grey, "dark"); }
        Raylib.BeginShaderMode(grey); Raylib.SetShaderValue(grey, greyAmt, amt, ShaderUniformDataType.Float); Raylib.SetShaderValue(grey, greyDark, dark, ShaderUniformDataType.Float); f(); Raylib.EndShaderMode();
    }
    static void Fist(float x, float y, float s, float a = 1)
    {
        var ol = new Color(58, 32, 30, (int)(255 * a)); var sk = new Color(232, 186, 150, (int)(255 * a)); var hi = new Color(250, 214, 182, (int)(255 * a)); Ui.PushScale(x, y, s, s); Raylib.DrawRectangleRounded(new Rectangle(x - 8, y - 7, 16, 14), .6f, 4, ol); Raylib.DrawRectangleRounded(new Rectangle(x - 7, y - 6, 14, 12), .6f, 4, sk); for (int i = 0; i < 4; i++) { Raylib.DrawRectangleRounded(new Rectangle(x - 7 + i * 3.5f, y - 6, 3.2f, 6), .7f, 3, hi); if (i > 0) Raylib.DrawRectangleV(new(x - 7.4f + i * 3.5f, y - 5), new(1, 5), ol); }
        Raylib.DrawRectangleRounded(new Rectangle(x - 6, y + .6f, 9, 3.6f), .8f, 3, hi); Raylib.DrawRectangleV(new(x - 5, y + 4.4f), new(8, 1), ol); Ui.PopScale();
    }
    void Bar()
    {
        var d = Store.Data; var list = BarList(); float cy = MathF.Round(Ui.VH / 2), x = BarX;
        foreach (var (off, slot) in Shown(list).OrderByDescending(q => MathF.Abs(q.off)))
        {
            float ab = MathF.Abs(off), near = M.Smooth(1, 0, ab), fade = M.Clamp(4.1f - ab); if (fade <= .02f) continue; float y = cy + off * BarGap + MathF.Sign(off) * M.Clamp(ab) * 3, size = M.Lerp(SlotOff, SlotOn, near) + barPop.X * near, h = size / 2; bool sel = near > .5f; bool hov = OverBar && MathF.Abs(Ui.Mouse.Y - y) < BarGap / 2 && !sel;
            float g = 1 - near; Greyed(g * .75f, 1 - g * .14f, () => { if (slot < 0) Fist(x, y, M.Lerp(1.2f, 1.5f, near) + (hov ? .08f : 0), fade); else if (sel) Items.Draw(d.Slots[slot].Id, x, y, 30, size / 30, 0, fade); else Items.Draw(d.Slots[slot].Id, x, y, 24, size / 24 + (hov ? .08f : 0), 0, fade); });
            if (slot >= 0 && d.Slots[slot].N > 1) Font.Draw(d.Slots[slot].N.ToString(), x + h + 1, y + h - 8, Raylib.Fade(Raylib.ColorLerp(new Color(206, 200, 196, 255), Color.White, near), fade), 1, 1, Raylib.Fade(new Color(30, 18, 22, 255), fade));
        }
        // the name slides out beside it for a moment
        float na = MathF.Max(M.Clamp(nameT * 6) * M.Clamp((2.2f - nameT) * 2.4f), barHover); if (na > .02f) { string s = d.Hand < 0 || d.Slots[d.Hand].Empty ? "Bare hands" : Items.Name(d.Slots[d.Hand].Id); float w = Font.Width(s, 1.25f) + 20, nx = x + 18 + Ease.OutCubic(M.Clamp(na)) * 6; Ui.Nine("tip", nx, cy - 10, w, 20, Raylib.Fade(Color.White, na)); Font.Draw(s, nx + 10, cy - 5.5f, Raylib.Fade(Ui.Cream, na), 1.25f); }
        // what you just picked up
        for (int i = 0; i < gains.Count; i++) { var (id, gn, t) = gains[gains.Count - 1 - i]; float a = M.Clamp(t * 8) * M.Clamp((2.6f - t) * 2); float gy = Ui.VH - 22 - i * 16, gx = 14 - (1 - Ease.OutCubic(M.Clamp(t * 6))) * 24; Items.Draw(id, gx + 4, gy + 4, 12, 1 + MathF.Max(0, .25f - t) * 1.4f, 0, a); Font.Draw("+" + gn + " " + Items.Name(id), gx + 15, gy, Raylib.Fade(Ui.Cream, a), 1, 0, new Color(44, 24, 26, 220)); }
    }
}

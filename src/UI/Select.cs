using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// Character select: six save files, each showing the one character it belongs to.
public sealed class SelectScreen
{
    readonly AvatarAnim[] an = new AvatarAnim[Saves.Max]; readonly Painter[] pt = new Painter[Saves.Max]; readonly Texture2D[] tex = new Texture2D[Saves.Max]; readonly AvatarPainter ap = new(); float openT; Saves.Slot asking; float askT; readonly float[] wave = new float[Saves.Max];
    const float CW = 188, CH = 122;
    public void Open() { openT = 0; asking = null; Saves.Refresh(); for (int i = 0; i < Saves.Max; i++) { an[i] ??= new AvatarAnim(); an[i].Set(Anim.Idle); if (pt[i] == null) { pt[i] = new Painter(110, 150); tex[i] = pt[i].ToTexture(); } pt[i].Clear(); pt[i].Upload(tex[i]); } for (int i = 0; i < Saves.All.Count; i++) Paint(i); }
    void Paint(int i) { pt[i].Clear(); ap.Draw(pt[i], Saves.All[i].Data.Avatar, an[i].Pose, 1.02f, 55, 142, outlinePx: 1); pt[i].Upload(tex[i]); }
    static string Time(double s) => s < 60 ? "just started" : s < 3600 ? (int)(s / 60) + "m played" : (int)(s / 3600) + "h " + (int)(s / 60 % 60) + "m played";
    public void Update(float dt)
    {
        openT += dt; for (int i = 0; i < Saves.All.Count; i++) { var d = Saves.All[i].Data.Avatar; an[i].IdleStyle = d.Idle; an[i].Wind = .3f; wave[i] -= dt; if (wave[i] <= 0) an[i].Set(Anim.Idle); if (an[i].Update(dt)) Paint(i); }
        if (In.Press(Act.Menu)) { if (asking != null) { asking = null; Snd.Play("close", .7f); } else Program.BackToTitle(); }
    }
    public void Draw()
    {
        float W = Ui.VW, H = Ui.VH; Raylib.DrawRectangle(-2, -2, (int)W + 4, (int)H + 4, new Color(34, 18, 26, (int)(M.Clamp(openT * 3) * 130))); Font.Draw("Choose a character", W / 2, 18, Ui.Cream, 2.4f, .5f, Ui.Ink, 1); Font.Draw(Saves.All.Count + " of " + Saves.Max + " save files", W / 2, 43, new Color(240, 214, 176, 255), 1, .5f, Ui.Ink);
        float k3 = MathF.Min(1, (W - 20) / (CW * 3 + 24)); float gx = (W - CW * 3 - 24) / 2, gy = 58; bool en = Ui.Enabled; if (asking != null) Ui.Enabled = false;
        for (int i = 0; i < Saves.Max; i++)
        {
            float x = gx + i % 3 * (CW + 12), y = gy + i / 3 * (CH + 10); float born = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp(openT * 2.6f - i * .12f)); if (born <= 0) continue; Ui.PushScale(x + CW / 2, y + CH / 2, born, born);
            if (i < Saves.All.Count) Card(i, x, y); else Empty(i, x, y); Ui.PopScale();
        }
        if (Ui.Button("selback", "Back", 14, H - 33, 84, 25, "btn", "left", text: 1.25f)) Program.BackToTitle(); Ui.Enabled = en; if (asking != null) Ask();
    }
    void Card(int i, float x, float y)
    {
        var s = Saves.All[i]; var d = s.Data; var a = Ui.A("sel" + i); bool over = Ui.Over(x, y, CW, CH); a.Hover.Update(over ? 1 : 0, 300, 18, Ui.DT); if (over && !a.Was) { Snd.Play("hover", .4f); an[i].Set(Anim.Wave); an[i].StateT = 0; wave[i] = 1.5f; } a.Was = over; y += MathF.Round(-a.Hover.X * 3 * (Store.Cfg.ReduceMotion ? 0 : 1));
        Ui.Nine("panel", x, y, CW, CH, Color.White); Raylib.DrawRectangleGradientV((int)x + 12, (int)y + 12, 66, (int)CH - 24, new Color(250, 222, 170, 255), new Color(224, 176, 124, 255)); Raylib.BeginBlendMode(BlendMode.Additive); Ui.GlowAt(x + 45, y + 54, 48, new Color(255, 220, 150, (int)(50 + 40 * M.Clamp(a.Hover.X)))); Raylib.EndBlendMode(); Raylib.DrawEllipse((int)x + 45, (int)(y + CH - 19), 20, 3.4f, new Color(90, 50, 40, 100)); Ui.Clip(x + 11, y + 4, 68, CH - 14); Ui.Tex(tex[i], x + 45 - 55, y + CH - 18 - 142, 1, Color.White); Ui.Unclip();
        Font.Draw(d.Avatar.Name, x + 88, y + 14, Ui.Ink, Font.Width(d.Avatar.Name, 1.75f) > CW - 102 ? 1.25f : 1.75f); Font.Draw("Day " + d.Day, x + 88, y + 34, Ui.Soft); Font.Draw(d.Mined + " blocks dug", x + 88, y + 46, Ui.Soft); Font.Draw(Time(d.Seconds), x + 88, y + 58, Ui.Soft);
        if (Ui.Button("play" + i, "Play", x + 86, y + CH - 45, 64, 28, "btnG", "right", text: 1.25f)) { an[i].Set(Anim.Cheer); wave[i] = 3; Program.PlaySave(s, new(x + 45, y + CH / 2)); }
        if (Ui.Button("del" + i, null, x + CW - 36, y + CH - 43, 24, 24, "btnR", "x", "Delete")) { asking = s; askT = 0; Snd.Play("open", .7f); }
    }
    void Empty(int i, float x, float y)
    {
        var (a, hit, over) = Ui.Widget("new" + i, x, y, CW, CH); float p = M.Clamp(a.Press.X, -.5f, .5f) * .3f; Ui.PushScale(x + CW / 2, y + CH / 2, 1 + a.Hover.X * .03f + p, 1 + a.Hover.X * .03f - p); Ui.Nine("paper", x, y, CW, CH, new Color(255, 255, 255, (int)(150 + 80 * M.Clamp(a.Hover.X)))); float b = M.Sin(Ui.T * 2.4f + i) * 2; Font.Draw("+", x + CW / 2, y + 34 + b, over ? Ui.Orange : Ui.Soft, 4, .5f); Font.Draw("New character", x + CW / 2, y + 78, over ? Ui.Ink : Ui.Soft, 1.25f, .5f); Ui.PopScale(); if (hit) Program.NewCharacter();
    }
    void Ask()
    {
        askT += Ui.DT; float W = Ui.VW, H = Ui.VH, k = Store.Cfg.ReduceMotion ? 1 : Ease.OutBack(M.Clamp(askT * 5)); Raylib.DrawRectangle(-2, -2, (int)W + 4, (int)H + 4, new Color(24, 10, 14, (int)(130 * M.Clamp(askT * 6)))); float w = 260, h = 112, x = (W - w) / 2, y = (H - h) / 2; Ui.PushScale(W / 2, H / 2, MathF.Max(.01f, k), MathF.Max(.01f, k)); Ui.Nine("panel", x, y, w, h, Color.White); Font.Draw("Delete " + asking.Data.Avatar.Name + "?", W / 2, y + 16, Ui.Ink, 1.75f, .5f); Font.Draw("This character and their whole world", W / 2, y + 38, Ui.Soft, 1, .5f); Font.Draw("will be removed from the game.", W / 2, y + 49, Ui.Soft, 1, .5f);
        if (Ui.Button("askno", "Keep", x + 22, y + h - 42, 104, 27, "btnG", "check", text: 1.25f)) { asking = null; Snd.Play("close", .7f); } else if (Ui.Button("askyes", "Delete", x + w - 126, y + h - 42, 104, 27, "btnR", "x", text: 1.25f, disabled: askT < .7f)) { Saves.Delete(asking); asking = null; Snd.Play("undo", 1, .7f); for (int i = 0; i < Saves.All.Count; i++) Paint(i); }
        Ui.PopScale();
    }
}

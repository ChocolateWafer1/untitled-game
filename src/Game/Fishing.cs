using System.Numerics;
using Raylib_cs;
namespace UnnamedGame;

public enum FS { Off, Aim, Fly, Wait, Bite, Fight, Caught, Lost }

/// Cast, wait, hook and fight. Each species has its own movement style; big fish add surges, leaps and dives.
public sealed class Fishing
{
    readonly GameScreen g; readonly Rng r = new(Environment.TickCount); public FS S = FS.Off; float t, charge, biteIn, biteWin, castT; public Vector2 Bob, BobV; Pool pool; public FishDef F; public int FishId; float kg, cm; float nib;
    // fight state
    public float Dist, MaxDist, Tension, Stamina, dirT, slack, over, phaseT, spook; public int Dir; public int Phase; // 0 normal 1 surge 2 leap 3 dive 4 rest
    float phaseLen, leapWin, divePress; bool leapOk; int jumps; float fishX, fishVX, shakeT; public string Msg = ""; public float MsgT; public FishItem Got; public bool IsNew, IsRecord; Texture2D art; float reelSnd, tensSnd; Texture2D shadow; int phaseCount; float showT;
    float Big => F == null ? 0 : M.Clamp(F.Hard * .75f + M.Clamp((cm - F.MinCm) / (F.MaxCm - F.MinCm)) * .25f);
    public Fishing(GameScreen g) { this.g = g; }
    public bool Busy => S != FS.Off; public bool Locks => S is FS.Aim or FS.Fly or FS.Wait or FS.Bite or FS.Fight or FS.Caught or FS.Lost;
    bool Held => In.Down(Act.Fish) || (Ui.Held && !In.Locked); bool Pressed => In.Press(Act.Fish) || (Ui.Click && !In.Locked && !(Ui.Mouse.Y < 50 && S == FS.Off && (Ui.Mouse.X < 160 || Ui.Mouse.X > Ui.VW - 120)));
    public Vector2 Tip { get { var p = g.P; float lift = S == FS.Fight ? 14 + Tension * 10 : S == FS.Aim ? 30 : 22; float reach = S == FS.Aim ? -18 - charge * 8 : 34 - (S == FS.Fight ? Tension * 10 : 0); return new(p.P.X + p.Dir * reach, p.P.Y - 46 - lift - (S == FS.Aim ? 16 : 0)); } }
    public bool CanStart(out string why) { why = null; var p = g.P; if (p.Swim || p.Climb || p.Sit || !p.Ground) return false; return Near(out _); }
    /// Is there water within casting reach in the direction faced?
    public bool Near(out Pool pl) { pl = null; var p = g.P; for (float d = 10; d < 230; d += 14) foreach (var q in g.W.Pools) { float x = p.P.X + p.Dir * d; if (x > q.X0 * 24 && x < (q.X1 + 1) * 24 && p.P.Y < q.Surf + 30 && p.P.Y > q.Surf - 190 && g.W.PoolAt(x, q.Surf + 8) == q && !g.W.Solid((int)(x / 24), (int)((q.Surf + 4) / 24))) { pl = q; return true; } } return false; }
    void Say(string m) { Msg = m; MsgT = 2.2f; }
    public void Cancel() { S = FS.Off; g.P.Frozen = false; }
    public void Update(float dt, bool control)
    {
        t += dt; MsgT -= dt; var p = g.P; var D = Store.Data;
        if (S == FS.Off) { if (control && Pressed && CanStart(out _)) { S = FS.Aim; charge = 0; t = 0; p.EmoteT = 0; p.V.X = 0; } else if (control && In.Press(Act.Fish) && p.Ground && !p.Swim && !Near(out _)) { Say("Face some water to fish"); Snd.Play("error", .4f); } return; }
        p.Frozen = true; p.V.X = 0; p.An.Dir = p.Dir; p.An.Vel = default; p.An.Strain = S == FS.Fight ? Tension : 0; p.An.ReelSpeed = S == FS.Fight && Held ? 1 : 0;
        switch (S)
        {
            case FS.Aim:
                p.An.Set(Anim.Cast); charge = M.Clamp(MathF.Abs(M.Sin(t * 1.35f - M.PI / 2) + 1) / 2); if ((int)(t * 9) != (int)((t - dt) * 9)) Snd.Play("charge", .4f, .7f + charge);
                if (!Held && t > .1f) { S = FS.Fly; castT = 0; float pw = (90 + charge * 190) * FishDex.RodCast[D.Rod]; Bob = Tip; BobV = new(p.Dir * pw, -150 - charge * 110); Snd.Play("cast"); p.An.Set(Anim.RodHold); }
                if (In.Press(Act.Menu) || In.Press(Act.Interact)) Cancel(); break;
            case FS.Fly:
                castT += dt; BobV.Y += 420 * dt; Bob += BobV * dt; p.An.Set(Anim.RodHold); var pl = g.W.PoolAt(Bob.X, Bob.Y); if (pl != null) { pool = pl; Bob.Y = pl.Surf; S = FS.Wait; Snd.Play("plop", .8f); g.Life.Splash(Bob, .3f); g.R.Ripple(Bob.X, 3); NewBite(); } else if (g.W.Solid((int)(Bob.X / 24), (int)(Bob.Y / 24)) || castT > 3) { Snd.Play("land", .3f); Say("Missed the water"); Cancel(); }
                break;
            case FS.Wait:
                p.An.Set(Anim.RodHold); biteIn -= dt; Bob.Y = pool.Surf + M.Sin(g.Env.T * 2.2f) * 1.2f; nib -= dt; if (nib < 0 && biteIn < 4 && biteIn > .6f) { nib = r.R(.7f, 1.7f); Bob.Y += 3; g.R.Ripple(Bob.X, 1.4f); Snd.Play("splash_small", .2f, 1.5f); }
                if (biteIn <= 0) { S = FS.Bite; t = 0; biteWin = (.95f - Big * .28f) * (D.Lure == 3 ? 1.6f : 1); Snd.Play("bite"); Snd.Play("splash_small", .6f); g.Life.Splash(Bob, .35f); g.R.Ripple(Bob.X, 4); }
                else if (Pressed || In.Press(Act.Menu) || In.Press(Act.Left) || In.Press(Act.Right)) { if (biteIn < 3.2f && Pressed) Say("Too early!"); Snd.Play("reel", .5f); Cancel(); }
                break;
            case FS.Bite:
                Bob.Y = pool.Surf + 5 + M.Sin(g.Env.T * 30) * 2; if (Pressed) StartFight(); else if (t > biteWin) { Say("It got away..."); UseBait(); S = FS.Lost; t = 0; Snd.Play("splash_small", .4f, .8f); }
                break;
            case FS.Fight: Fight(dt); break;
            case FS.Caught: showT += dt; p.An.Set(Anim.ShowOff); if (showT > .7f && (Pressed || In.Press(Act.Interact) || In.Press(Act.Jump))) { Snd.Play("close"); Cancel(); } break;
            case FS.Lost: p.An.Set(Anim.Shrug); if (t > 1.1f) Cancel(); break;
        }
    }
    void UseBait() { var D = Store.Data; if (D.Bait > 0) { D.Baits[D.Bait] = Math.Max(0, D.Baits[D.Bait] - 1); if (D.Baits[D.Bait] == 0) { Say("Out of " + FishDex.BaitNames[D.Bait]); D.Bait = 0; } } }
    void NewBite()
    {
        var D = Store.Data; FishId = Pick(); F = FishDex.All[FishId]; (kg, cm) = FishDex.Roll(F, r, D.Lure == 2 ? .55f : D.Lure == 4 ? .25f : 0); float sp = (D.Bait == 0 ? 1.45f : 1) * (D.Lure == 1 ? .65f : 1) * (g.Env.Rain > .3f ? .8f : 1); biteIn = r.R(2.5f, 8) * sp + F.Rarity * .5f; nib = 1;
    }
    int Pick()
    {
        var D = Store.Data; var e = g.Env; float tot = 0; var ws = new float[FishDex.All.Length]; float depth = 0; for (float y = pool.Surf; y < pool.Surf + 400 && !g.W.Solid((int)(Bob.X / 24), (int)(y / 24)); y += 12) depth = y - pool.Surf;
        for (int i = 0; i < ws.Length; i++)
        {
            var f = FishDex.All[i]; if (f.W != pool.Kind || (f.T & e.Period) == 0) continue; if (f.S != Sky.Any && f.S != e.SkyKind && pool.Kind != Water.Cave) continue; if (f.Legend && (D.Bait != 11 || depth < 60)) continue;
            float w = f.Rarity switch { 1 => 10, 2 => 5.5f, 3 => 2.6f, 4 => 1.1f, _ => 2.2f }; if (f.Bait == D.Bait) w *= 3; if (D.Bait == 0 && f.Rarity >= 3) w *= .3f; if (f.Rarity >= 3 && depth > 110) w *= 1.5f; if (D.Lure == 4 && f.Rarity >= 3) w *= 1.8f; if (D.Lure == 5 && e.Night > .5f && f.Rarity >= 3) w *= 2.4f; if (f.S != Sky.Any) w *= 1.8f; if (f.Rarity <= 1 && D.Lure >= 2) w *= .7f; ws[i] = w; tot += w;
        }
        float q = r.F() * tot; for (int i = 0; i < ws.Length; i++) { q -= ws[i]; if (q <= 0 && ws[i] > 0) return i; } for (int i = 0; i < ws.Length; i++) if (ws[i] > 0) return i; return Array.FindIndex(FishDex.All, f => f.W == pool.Kind);
    }
    void StartFight()
    {
        S = FS.Fight; t = 0; MaxDist = Dist = 9 + Big * 34; Tension = .35f; Stamina = 1; slack = over = 0; Dir = r.P(.5f) ? 1 : -1; dirT = 1; Phase = 0; phaseT = 0; phaseLen = r.R(2.2f, 3.4f); jumps = 0; phaseCount = 0; fishX = Bob.X; fishVX = 0; Snd.Play("splash", .5f, 1.2f); g.Life.Splash(Bob, .6f); var pt = new Painter(150, 80); FishDex.Draw(pt, F, .9f, 72, 40, 0, true); if (shadow.Id != 0) Raylib.UnloadTexture(shadow); shadow = pt.ToTexture(); if (Big > .55f) { Say(F.Legend ? "Something enormous..." : "A big one!"); g.Shake(3); }
    }
    void NextPhase()
    {
        phaseT = 0; phaseCount++; float hard = Big; float q = r.F(); if (Phase != 0 && Phase != 4) { Phase = r.P(.25f + (1 - Stamina) * .4f) ? 4 : 0; phaseLen = Phase == 4 ? r.R(1.2f, 2.2f) : r.R(2.4f, 4) - hard * 1.4f; return; }
        if (hard < .18f) { Phase = q < .25f ? 1 : 0; phaseLen = Phase == 1 ? .7f : 2.4f; }
        else Phase = F.M switch { Move.Jumper => q < .5f ? 2 : q < .75f ? 1 : 0, Move.Diver => q < .55f ? 3 : q < .8f ? 1 : 0, Move.Thrasher => q < .6f ? 1 : q < .75f ? 2 : 0, Move.Darter => q < .65f ? 1 : 0, Move.Circler => q < .3f ? 1 : 0, _ => q < .3f ? 1 : 0 };
        if (F.Legend && Phase == 0) Phase = 1 + r.I(3);
        switch (Phase) { case 1: phaseLen = (F.M == Move.Darter ? .8f : 1.3f) + hard * .8f; g.Life.Splash(Bob, .5f); Snd.Play("splash_small", .7f); g.Shake(1 + hard * 2); break; case 2: phaseLen = 1.15f; leapOk = false; jumps++; Snd.Play("splash", .7f); g.Life.Splash(Bob, 1); g.R.Ripple(Bob.X, 6); break; case 3: phaseLen = 1.9f + hard; divePress = 0; Snd.Play("splash_small", .8f, .7f); break; default: phaseLen = r.R(2.2f, 3.6f) - hard * 1.2f; break; }
    }
    void Fight(float dt)
    {
        var p = g.P; var D = Store.Data; p.An.Set(Anim.Reel); float hard = Big, pow = FishDex.RodPower[D.Rod], line = FishDex.RodLine[D.Rod]; bool reel = Held; phaseT += dt; if (phaseT > phaseLen) { if (Phase == 2 && !leapOk) { Tension += .42f + hard * .22f; Dist += 2.5f; Say("Bow the rod when it leaps!"); g.Shake(3); } if (Phase == 3 && divePress < 3 + hard * 4) { Tension += .25f; Dist += 3; } NextPhase(); }
        // which way is it swimming?
        dirT -= dt; if (dirT <= 0) { dirT = F.M switch { Move.Thrasher => r.R(.45f, .9f), Move.Darter => r.R(.6f, 1.3f), Move.Circler => 1.5f, Move.Drifter => r.R(2, 3.4f), Move.Steady => r.R(2, 3.5f), _ => r.R(1, 2) } * M.Lerp(1.25f, .75f, hard); Dir = F.M == Move.Circler ? -Dir : r.P(.62f) ? -Dir : Dir; }
        int inp = (In.Down(Act.Right) ? 1 : 0) - (In.Down(Act.Left) ? 1 : 0); bool counter = inp == -Dir, wrong = inp == Dir; float pull = (.16f + hard * .42f) * (.45f + Stamina * .65f);
        if (Phase == 1) pull *= 1.9f; if (Phase == 4) pull *= .25f; if (Phase == 3) pull *= 1.15f; if (counter) pull *= .52f; if (wrong) pull *= 1.35f;
        float gain = 0; if (reel) { gain = pull + .20f + (Phase == 1 ? .34f : 0); Dist -= (2.9f * pow) * (Phase == 4 ? 1.75f : Phase == 1 ? .25f : 1) * (counter ? 1.25f : 1) * (1 - hard * .34f) * dt; Stamina -= dt * (.03f + (counter ? .055f : .012f)) * pow / (1 + hard * 1.5f); }
        else { gain = pull * .55f - .62f; Dist += (.5f + hard * 1.9f) * (Phase == 1 ? 2.1f : Phase == 4 ? .1f : .75f) * (counter ? .5f : 1) * dt; if (Phase == 1) Stamina -= dt * .05f; }
        Stamina = M.Clamp(Stamina, 0, 1); if (Stamina < .05f && hard > .3f) { gain *= .75f; }
        Tension = M.Clamp(Tension + gain * dt / line * 1.25f, 0, 1.25f);
        if (Phase == 2) { float k = phaseT / phaseLen; Bob.Y = pool.Surf - M.Sin(k * M.PI) * (34 + hard * 40); if (!leapOk && k > .12f && k < .86f && (In.Press(Act.Down) || In.Press(Act.Jump))) { leapOk = true; Tension = MathF.Max(.15f, Tension - .22f); Stamina -= .1f; Snd.Play("confirm", .6f); Say("Nice!"); } } else Bob.Y = pool.Surf + (Phase == 3 ? 10 : 2) + M.Sin(g.Env.T * 12) * 1.2f;
        if (Phase == 3) { Tension += dt * .12f * (1 - M.Clamp(divePress / (3 + hard * 4))); if (In.Press(Act.Up) || In.Press(Act.Jump)) { divePress++; Snd.Play("reel", .6f, 1.2f); Stamina -= .012f; } }
        // motion in the water
        fishVX = M.Damp(fishVX, Dir * (36 + hard * 46) * (Phase == 1 ? 2 : 1), 4, dt); float edge = 60 + hard * 40; float cx2 = p.P.X + p.Dir * (40 + M.Clamp(Dist / MaxDist) * 150); Bob.X += fishVX * dt; Bob.X = M.Damp(Bob.X, M.Clamp(Bob.X, cx2 - edge, cx2 + edge), 5, dt); Bob.X = M.Clamp(Bob.X, pool.X0 * 24 + 16, (pool.X1 + 1) * 24 - 16); while (g.W.Solid((int)(Bob.X / 24), (int)((pool.Surf + 4) / 24)) && MathF.Abs(Bob.X - p.P.X) < 600) Bob.X += p.Dir * 4; if (r.P(dt * (4 + hard * 8))) { g.Life.Splash(new(Bob.X, pool.Surf), .12f + (Phase == 1 ? .3f : 0)); g.R.Ripple(Bob.X, 1.4f + hard * 2); }
        reelSnd -= dt; if (reel && reelSnd < 0) { reelSnd = .075f; Snd.Play("reel", .28f, .85f + Tension * .5f); } tensSnd -= dt; if (Tension > .78f && tensSnd < 0) { tensSnd = .16f; Snd.Play("tension", .6f, .8f + Tension * .6f); shakeT = .1f; }
        over = Tension >= 1 ? over + dt : 0; slack = Tension <= .03f ? slack + dt : MathF.Max(0, slack - dt * 2);
        if (over > .32f) { Snd.Play("snap"); Say("The line snapped!"); g.Shake(4); Lose(); return; }
        if (slack > 2.2f) { Say("The hook slipped out..."); Lose(); return; }
        if (Dist > MaxDist * 1.55f + 6) { Say("It ran out all your line!"); Lose(); return; }
        if (Dist <= 0) Win();
    }
    void Lose() { UseBait(); S = FS.Lost; t = 0; g.Life.Splash(new(Bob.X, pool.Surf), .7f); Snd.Play("splash", .4f); }
    void Win()
    {
        UseBait(); S = FS.Caught; showT = 0; var D = Store.Data; Got = new FishItem { Id = FishId, Kg = kg, Cm = cm }; IsNew = !D.Journal.TryGetValue(FishId, out var rec); rec ??= new FishRecord(); IsRecord = !IsNew && kg > rec.BestKg; rec.Count++; rec.BestKg = MathF.Max(rec.BestKg, kg); rec.BestCm = MathF.Max(rec.BestCm, cm); D.Journal[FishId] = rec; if (D.Fish.Count < 60) D.Fish.Add(Got); else Say("Bag full, so you let it go");
        Snd.Play(F.Legend ? "legend" : "catch"); Snd.Play("splash", .5f); g.Life.Splash(new(Bob.X, pool.Surf), 1); g.Life.Sparkle(g.P.P + new Vector2(0, -60), F.Legend ? 50 : 18, new Color(255, 226, 140, 255)); var pt = new Painter(200, 130); FishDex.Draw(pt, F, 1.3f, 96, 66); pt.Outline(0x2a1a1c, 2); if (art.Id != 0) Raylib.UnloadTexture(art); art = pt.ToTexture(); Store.SaveGame(); D.TutorialDone = true; g.P.An.Punch(6); if (F.Legend) g.Shake(5);
    }
    // ---------------------------------------------------------------- drawing
    public void DrawWorld(int cx, int cy)
    {
        if (S is FS.Off or FS.Caught or FS.Lost) return; var p = g.P; var c = new Vector2(cx, cy); var hand = p.HandPos(p.Dir) - c; var tip = Tip - c; Col[] rc = [0x8a6a48, 0xc8a860, 0x9a5a34, 0x6ab0c0, 0xb0a0e8]; Col rod = rc[Store.Data.Rod]; var m = (hand + tip) / 2 + new Vector2(0, -4 - Tension * (S == FS.Fight ? 8 : 0)); Vector2 prev = hand - Vector2.Normalize(tip - hand) * 8;
        for (int i = 0; i <= 12; i++) { float k = i / 12f; var q = (1 - k) * (1 - k) * (hand - Vector2.Normalize(tip - hand) * 8) + 2 * k * (1 - k) * m + k * k * tip; if (i > 0) { Raylib.DrawLineEx(prev, q, 3.2f - k * 1.2f, new Color(42, 26, 28, 255)); } prev = q; }
        prev = hand - Vector2.Normalize(tip - hand) * 8; for (int i = 0; i <= 12; i++) { float k = i / 12f; var q = (1 - k) * (1 - k) * (hand - Vector2.Normalize(tip - hand) * 8) + 2 * k * (1 - k) * m + k * k * tip; if (i > 0) Raylib.DrawLineEx(prev, q, 1.4f, rod.Rl()); prev = q; }
        Raylib.DrawCircleV(hand + new Vector2(p.Dir * 3, 2), 3, new Color(42, 26, 28, 255)); Raylib.DrawCircleV(hand + new Vector2(p.Dir * 3, 2), 2, new Color(150, 150, 160, 255)); if (S == FS.Aim) return; var b = Bob - c; float sag = S == FS.Fight ? (1 - M.Clamp(Tension * 1.3f)) * 24 : S == FS.Fly ? 4 : 18; prev = tip; var lc = S == FS.Fight && Tension > .78f ? new Color(255, 150, 120, 255) : new Color(244, 240, 228, 200); for (int i = 1; i <= 14; i++) { float k = i / 14f; var q = Vector2.Lerp(tip, b, k) + new Vector2(0, M.Sin(k * M.PI) * sag); Raylib.DrawLineV(prev, q, lc); prev = q; }
        if (S == FS.Fight && Phase == 2) { float k = phaseT / phaseLen; Raylib.DrawTexturePro(shadow, new Rectangle(0, 0, Dir < 0 ? -150 : 150, 80), new Rectangle(b.X, b.Y, 46 + Big * 44, 25 + Big * 22), new(23 + Big * 22, 12 + Big * 11), (k - .5f) * 80 * Dir, new Color(255, 255, 255, 255)); }
        else if (S != FS.Fight || Phase != 3) { Raylib.DrawCircleV(b, 3, new Color(42, 26, 28, 255)); Raylib.DrawRectangleV(b - new Vector2(2, 2), new(4, 2), new Color(220, 80, 66, 255)); Raylib.DrawRectangleV(b - new Vector2(2, 0), new(4, 2), new Color(250, 242, 228, 255)); }
    }
    public void DrawUnder(int cx, int cy)
    {
        if (S is FS.Wait or FS.Bite && biteIn < 2.5f) { float k = M.Clamp(1 - biteIn / 2.5f); float s = 18 + cm * .16f; s = MathF.Min(s, 70); float x = Bob.X - cx + (1 - k) * 60 * (FishId % 2 == 0 ? 1 : -1), y = pool.Surf - cy + 14 + (1 - k) * 14; Raylib.DrawEllipse((int)x, (int)y, s * .5f, s * .18f, new Color(10, 20, 30, (int)(90 * k))); }
        if (S == FS.Fight && Phase != 2) { float s = .22f + Big * .5f; Raylib.DrawTexturePro(shadow, new Rectangle(0, 0, Dir < 0 ? -150 : 150, 80), new Rectangle(Bob.X - cx, pool.Surf - cy + 14 + (Phase == 3 ? 16 : 0), 150 * s, 80 * s), new(75 * s, 40 * s), M.Sin(g.Env.T * 9) * 6, new Color(255, 255, 255, 110)); }
    }
    float mx; readonly Spring[] sp = new Spring[4];
    public void DrawUi()
    {
        float W = Ui.VW, dt = Ui.DT; var p = g.P; Vector2 ps = g.ToScreen(p.P + new Vector2(0, -100)); float t = Ui.T;
        if (MsgT > 0) { float k = M.Clamp(MsgT * 3); Font.Draw(Msg, M.Clamp(ps.X, 80, W - 80), ps.Y - 14 + (1 - k) * -6, Raylib.Fade(Ui.Cream, k), 1.5f, .5f, Ui.Ink); }
        if (S == FS.Aim) { float x = ps.X - 30, y = ps.Y; Ui.Nine("dark", x - 4, y - 3, 68, 14, Color.White); Raylib.DrawRectangleV(new(x, y), new(60, 8), new Color(60, 44, 48, 255)); Raylib.DrawRectangleGradientH((int)x, (int)y, (int)(60 * charge), 8, new Color(130, 180, 90, 255), charge > .9f ? new Color(255, 220, 120, 255) : new Color(240, 170, 70, 255)); }
        if (S == FS.Bite) { float b = Ease.OutElastic(M.Clamp(this.t * 2)); Ui.PushScale(ps.X, ps.Y, b, b); Font.Draw("!", ps.X, ps.Y - 20, new Color(255, 220, 90, 255), 4, .5f, Ui.Ink); Ui.PopScale(); Font.Draw("Press " + In.Name(Act.Fish) + " or click!", ps.X, ps.Y + 20, Ui.Cream, 1, .5f, Ui.Ink); }
        if (S == FS.Fight)
        {
            float w = 280, x = (W - w) / 2 + (shakeT > 0 ? M.Sin(t * 90) * 2 * Store.Cfg.Shake : 0), y = 16; shakeT -= dt; Ui.Nine("panel", x, y, w, 92, Color.White); string nm = Store.Data.Journal.ContainsKey(FishId) ? F.Name : "???"; Font.Draw(nm, x + 14, y + 12, Ui.Ink, 1.25f); for (int i = 0; i < F.Rarity; i++) Font.Draw("*", x + w - 18 - i * 7, y + 13, F.Legend ? Ui.Orange : Ui.Gold, 1.25f);
            // distance track with the fish swimming along it
            float tx = x + 16, tw = w - 32, ty = y + 32; Raylib.DrawRectangleRounded(new Rectangle(tx - 2, ty - 2, tw + 4, 14), .8f, 6, Ui.Ink); Raylib.DrawRectangleRounded(new Rectangle(tx, ty, tw, 10), .8f, 6, new Color(106, 170, 186, 255)); for (int i = 0; i < 9; i++) Raylib.DrawRectangleV(new(tx + 8 + i * tw / 9 + M.Sin(t * 2 + i) * 3, ty + 2 + (i * 5 % 6)), new(8, 1), new Color(160, 210, 220, 150)); sp[0].Update(M.Clamp(Dist / (MaxDist * 1.55f + 6)), 90, 14, dt); float fx = tx + 6 + (tw - 14) * M.Clamp(sp[0].X); Raylib.DrawRectangleV(new(tx + 1, ty + 1), new(6, 8), Ui.Green); Raylib.DrawTexturePro(shadow, new Rectangle(0, 0, -150, 80), new Rectangle(fx, ty + 5, 34, 18), new(17, 9), M.Sin(t * 12) * 8, Color.White); Font.Draw(MathF.Max(0, Dist).ToString("0.0") + "m", tx + tw, ty - 11, Ui.Soft, 1, 1);
            // tension
            float gy = y + 52; Font.Draw("Line", tx, gy + 1, Ui.Ink); float bx = tx + 32, bw = tw - 32; Raylib.DrawRectangleRounded(new Rectangle(bx - 2, gy - 1, bw + 4, 12), .7f, 5, Ui.Ink); Raylib.DrawRectangleGradientH((int)bx, (int)gy + 1, (int)(bw * .5f), 8, new Color(120, 168, 200, 255), new Color(130, 176, 96, 255)); Raylib.DrawRectangleGradientH((int)(bx + bw * .5f), (int)gy + 1, (int)(bw * .5f) + 1, 8, new Color(130, 176, 96, 255), new Color(214, 76, 60, 255)); sp[1].Update(M.Clamp(Tension, 0, 1.03f), 260, 22, dt); float nx = bx + bw * M.Clamp(sp[1].X, 0, 1.02f); Raylib.DrawRectangleV(new(nx - 2.5f, gy - 4), new(5, 18), Ui.Ink); Raylib.DrawRectangleV(new(nx - 1.5f, gy - 3), new(3, 16), Tension > .78f && t % .16f < .08f ? Color.White : Ui.Gold); if (slack > .4f) Font.Draw("Too slack! Reel!", bx + bw / 2, gy + 1, Ui.Cream, 1, .5f, Ui.Ink);
            float sy = y + 70; Font.Draw("Fight", tx, sy, Ui.Ink); Raylib.DrawRectangleRounded(new Rectangle(bx - 1, sy + 1, bw + 2, 6), .8f, 4, Ui.Ink); Raylib.DrawRectangleV(new(bx, sy + 2), new(bw * Stamina, 4), Ui.Orange);
            // direction arrow + instructions
            float ax = W / 2 + Dir * (52 + M.Sin(t * 10) * 5); string pr = Phase switch { 1 => "It's running! Ease off!", 2 => leapOk ? "Nice!" : "Leap! Press " + In.Name(Act.Down) + "!", 3 => "Diving! Tap " + In.Name(Act.Up) + "!", 4 => "It's tired. Reel!", _ => "Hold " + (-Dir > 0 ? In.Name(Act.Right) : In.Name(Act.Left)) + " to steer against it" }; Color pc = Phase switch { 1 => new Color(255, 150, 120, 255), 2 => new Color(255, 226, 120, 255), 3 => new Color(150, 210, 255, 255), 4 => new Color(180, 240, 150, 255), _ => Ui.Cream }; float pulse = Phase is 1 or 2 or 3 ? 1 + MathF.Abs(M.Sin(t * 9)) * .08f : 1; Ui.PushScale(W / 2, y + 108, pulse, pulse); Font.Draw(pr, W / 2, y + 102, pc, 1.5f, .5f, Ui.Ink); Ui.PopScale(); if (Phase is 0 or 4 or 1) Ui.Icon(Dir > 0 ? "right" : "left", ax - 12 + (Dir > 0 ? 90 : -90) , y + 98, 1.4f); if (this.t < 6 && !Store.Data.TutorialDone) Font.Draw("Hold " + In.Name(Act.Fish) + " or mouse to reel. Let go before the line turns red.", W / 2, y + 122, Ui.Cream, 1, .5f, Ui.Ink);
        }
        if (S == FS.Caught)
        {
            float k = Ease.OutBack(M.Clamp(showT * 2.6f)); float w = 250, h = 150, x = (W - w) / 2, y = 52; Ui.PushScale(W / 2, y + h / 2, k, k, (1 - k) * -8); Raylib.BeginBlendMode(BlendMode.Additive); Ui.GlowAt(W / 2, y + 66, 150, new Color(255, 206, 120, F.Legend ? 130 : 70)); if (F.Legend || IsNew) for (int i = 0; i < 10; i++) { float a = t * .5f + i * M.TAU / 10; Raylib.DrawTriangle(new(W / 2, y + 66), new(W / 2 + M.Sin(a + .12f) * 170, y + 66 + M.Cos(a + .12f) * 170), new(W / 2 + M.Sin(a) * 170, y + 66 + M.Cos(a) * 170), new Color(255, 220, 140, 26)); } Raylib.EndBlendMode();
            Ui.Nine("panel", x, y, w, h, Color.White); string head = F.Legend ? "LEGENDARY CATCH!" : IsNew ? "New fish!" : IsRecord ? "New record!" : "Nice catch!"; Font.Draw(head, W / 2, y - 4, F.Legend ? Ui.Gold : Ui.Cream, 2, .5f, Ui.Ink, 1); Ui.Tex(art, W / 2 - 64, y + 12 + M.Sin(t * 2.4f) * 2, .66f, Color.White); Font.Draw(F.Name, W / 2, y + 92, Ui.Ink, 1.5f, .5f); Font.Draw(Got.Kg.ToString(Got.Kg < 1 ? "0.00" : "0.0") + " kg    " + Got.Cm.ToString("0") + " cm    " + FishDex.Value(Got) + " coins", W / 2, y + 108, Ui.Soft, 1, .5f); var ln = Font.Wrap(F.Text, w - 30); for (int i = 0; i < ln.Count; i++) Font.Draw(ln[i], W / 2, y + 120 + i * 9, Ui.Ink, 1, .5f); if (showT > 1) Font.Draw("click to continue", W / 2, y + h + 4, Raylib.Fade(Ui.Cream, .6f + .4f * M.Sin(t * 4)), 1, .5f, Ui.Ink); Ui.PopScale();
        }
    }
}

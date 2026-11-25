using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public enum FS { Off, Aim, Fly, Wait, Bite, Fight, Caught, Lost }

/// Cast, wait, hook and fight. Each species has its own movement style; big fish add surges, leaps and dives.
public sealed class Fishing
{
    readonly GameScreen g; readonly Rng r = new(Environment.TickCount); public FS S = FS.Off; float t, charge, biteIn, biteWin, castT; public Vector2 Bob, BobV; Pool pool; public FishDef F; public int FishId; float kg, cm; float nib;
    // fight state: keep the net over the fish (A/D), hold to reel, ease off when the line glows red
    public float FishP, NetP, NetV, NetW, Catch, Tension, slack, over, phaseT, grace; public int Dir; public int Phase; // 0 swim 1 run 2 leap 3 dive 4 tired
    float fishTo, fishT, fishV, wob, Stamina; public bool Inside, NeedLeft, NeedRight, NeedReel, NeedRelease; float hintA, hintD, hintF, Dist, MaxDist, dirT;
    float phaseLen, leapWin, divePress; bool leapOk; int jumps; float fishX, fishVX, shakeT; public string Msg = ""; public float MsgT; public FishItem Got; public bool IsNew, IsRecord; Texture2D art; float reelSnd, tensSnd; Texture2D shadow; int phaseCount; float showT;
    float Big => F == null ? 0 : M.Clamp(F.Hard * .75f + M.Clamp((cm - F.MinCm) / (F.MaxCm - F.MinCm)) * .25f);
    public Fishing(GameScreen g) { this.g = g; }
    public bool Busy => S != FS.Off; public bool Locks => S is FS.Aim or FS.Fly or FS.Wait or FS.Bite or FS.Fight or FS.Caught or FS.Lost;
    bool Held => In.Down(Act.Fish) || (Ui.Held && !In.Locked && !g.OverAdmin); bool Pressed => In.Press(Act.Fish) || (Ui.Click && !In.Locked && !g.OverAdmin && !(Ui.Mouse.Y < 50 && S == FS.Off && (Ui.Mouse.X < 160 || Ui.Mouse.X > Ui.VW - 120)));
    public Vector2 Tip { get { var p = g.P; float lift = S == FS.Fight ? 14 + Tension * 10 : S == FS.Aim ? 30 : 22; float reach = S == FS.Aim ? -18 - charge * 8 : 34 - (S == FS.Fight ? Tension * 10 : 0); return new(p.P.X + p.Dir * reach, p.P.Y - 46 - lift - (S == FS.Aim ? 16 : 0)); } }
    public bool CanStart(out string why) { why = null; var p = g.P; if (p.Swim || p.Climb || p.Sit || !p.Ground) return false; return Near(out _); }
    /// Is there water within casting reach in the direction faced?
    public bool Near(out Pool pl) { pl = null; var p = g.P; for (float d = 10; d < 230; d += 14) foreach (var q in g.W.Pools) { float x = p.P.X + p.Dir * d; if (x > q.X0 * 24 && x < (q.X1 + 1) * 24 && p.P.Y < q.Surf + 30 && p.P.Y > q.Surf - 190 && g.W.PoolAt(x, q.Surf + 8) == q && !g.W.Solid((int)(x / 24), (int)((q.Surf + 4) / 24))) { pl = q; return true; } } return false; }
    void Say(string m) { Msg = m; MsgT = 2.2f; }
    public void ForceBite(int id) { if (S is FS.Wait or FS.Bite && id >= 0) { FishId = id; F = FishDex.All[id]; (kg, cm) = FishDex.Roll(F, r, .4f); } if (S == FS.Wait) biteIn = .01f; }
    public int NextFish = -1; public bool Frenzy;
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
                castT += dt; BobV.Y += 420 * dt; Bob += BobV * dt; p.An.Set(Anim.RodHold); var pl = g.W.PoolAt(Bob.X, Bob.Y); if (pl != null) { pool = pl; Bob.Y = pl.Surf; S = FS.Wait; t = 0; Snd.Play("plop", .8f); g.Life.Splash(Bob, .3f); g.R.Ripple(Bob.X, 3); NewBite(); } else if (g.W.Solid((int)(Bob.X / 24), (int)(Bob.Y / 24)) || castT > 3) { Snd.Play("land", .3f); Say("Missed the water"); Cancel(); }
                break;
            case FS.Wait:
                p.An.Set(Anim.RodHold); biteIn -= dt; Bob.Y = pool.Surf + M.Sin(g.Env.T * 2.2f) * 1.2f; nib -= dt; if (nib < 0 && biteIn < 4 && biteIn > .6f) { nib = r.R(.7f, 1.7f); Bob.Y += 3; g.R.Ripple(Bob.X, 1.4f); Snd.Play("splash_small", .2f, 1.5f); }
                if (biteIn <= 0) { S = FS.Bite; t = 0; biteWin = (2.3f - Big * .5f) * (D.Lure == 3 ? 1.5f : 1); Snd.Play("bite"); Snd.Play("splash_small", .6f); g.Life.Splash(Bob, .35f); g.R.Ripple(Bob.X, 4); }
                else if ((Pressed && t > .4f) || In.Press(Act.Menu) || In.Press(Act.Interact)) { Say("Reeled in"); Snd.Play("reel", .5f); Cancel(); }
                break;
            case FS.Bite:
                Bob.Y = pool.Surf + 5 + M.Sin(g.Env.T * 30) * 2; if (Pressed) StartFight(); else if (t > biteWin) { Say("It swam off. Bait is safe!"); S = FS.Lost; t = 0; Snd.Play("splash_small", .4f, .8f); }
                break;
            case FS.Fight: Fight(dt); break;
            case FS.Caught: showT += dt; p.An.Set(Anim.ShowOff); if (showT > .7f && (Pressed || In.Press(Act.Interact) || In.Press(Act.Jump))) { Snd.Play("close"); Cancel(); } break;
            case FS.Lost: p.An.Set(Anim.Shrug); if (t > 1.1f) Cancel(); break;
        }
    }
    void UseBait() { var D = Store.Data; if (D.Bait > 0) { D.Baits[D.Bait] = Math.Max(0, D.Baits[D.Bait] - 1); if (D.Baits[D.Bait] == 0) { Say("Out of " + FishDex.BaitNames[D.Bait]); D.Bait = 0; } } }
    void NewBite()
    {
        var D = Store.Data; FishId = NextFish >= 0 ? NextFish : Pick(); NextFish = -1; F = FishDex.All[FishId]; (kg, cm) = FishDex.Roll(F, r, D.Lure == 2 ? .55f : D.Lure == 4 ? .25f : 0); float sp = (D.Bait == 0 ? 1.45f : 1) * (D.Lure == 1 ? .65f : 1) * (g.Env.Rain > .3f ? .8f : 1); biteIn = Frenzy ? r.R(.4f, 1.2f) : r.R(2.5f, 8) * sp + F.Rarity * .5f; nib = 1;
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
        S = FS.Fight; t = 0; grace = 2.2f; Catch = .30f; Tension = .12f; slack = over = 0; Phase = 0; phaseT = 0; phaseLen = r.R(3, 4.5f); FishP = .5f; NetP = .5f; NetV = 0; fishTo = .5f; fishT = 1; fishV = 0; phaseCount = 0; float line = FishDex.RodLine[Store.Data.Rod]; NetW = M.Clamp(M.Lerp(.34f, .17f, Big) * M.Lerp(1, line, .35f), .14f, .46f);
        Snd.Play("splash", .5f, 1.2f); g.Life.Splash(Bob, .6f); var pt = new Painter(150, 80); FishDex.Draw(pt, F, .9f, 72, 40, 0, true); if (shadow.Id != 0) Raylib.UnloadTexture(shadow); shadow = pt.ToTexture(); if (Big > .55f) { Say(F.Legend ? "Something enormous..." : "A big one!"); g.Shake(3); }
    }
    void NextPhase()
    {
        phaseT = 0; phaseCount++; float hard = Big, q = r.F(); leapOk = false; if (Phase != 0) { Phase = 0; phaseLen = r.R(2.8f, 4.6f) - hard * 1.6f; return; }
        Phase = hard < .12f ? 0 : F.M switch { Move.Jumper => q < .6f ? 2 : 1, Move.Diver => q < .6f ? 3 : 1, Move.Thrasher => q < .8f ? 1 : 2, Move.Darter => 1, Move.Steady or Move.Drifter => q < .4f ? 1 : 4, _ => q < .6f ? 1 : 4 };
        if (F.Legend) Phase = 1 + r.I(3); phaseLen = Phase switch { 1 => 1.0f + hard * .7f, 2 => 1.2f, 3 => 1.6f + hard * .6f, 4 => 1.8f, _ => r.R(2.4f, 4) };
        if (Phase == 1) { g.Life.Splash(Bob, .5f); Snd.Play("splash_small", .7f); g.Shake(1 + hard * 2); } if (Phase == 2) { Snd.Play("splash", .7f); g.Life.Splash(Bob, 1); g.R.Ripple(Bob.X, 6); fishTo = FishP > .5f ? r.R(.08f, .4f) : r.R(.6f, .92f); } if (Phase == 3) Snd.Play("splash_small", .8f, .7f);
    }
    void Fight(float dt)
    {
        var p = g.P; p.An.Set(Anim.Reel); float hard = Big, pow = FishDex.RodPower[Store.Data.Rod]; bool reel = Held; grace -= dt; phaseT += dt; if (phaseT > phaseLen) NextPhase();
        // the fish: each species picks targets in its own way
        fishT -= dt; float speed = M.Lerp(.5f, 1.75f, hard) * (Phase == 1 ? 1.7f : Phase == 4 ? .25f : Phase == 2 ? 1.8f : 1);
        if (fishT <= 0 && Phase != 2)
        {
            (float lo, float hi, float jump) m = F.M switch { Move.Darter => (.5f, 1.4f, .55f), Move.Thrasher => (.3f, .7f, .38f), Move.Circler => (1.1f, 1.2f, .8f), Move.Drifter => (1.6f, 2.8f, .3f), Move.Steady => (1.6f, 2.6f, .34f), Move.Diver => (1f, 2f, .42f), _ => (.9f, 1.8f, .45f) }; fishT = r.R(m.lo, m.hi) * M.Lerp(1.35f, .85f, hard); float n = F.M == Move.Circler ? (fishTo > .5f ? r.R(.10f, .28f) : r.R(.72f, .9f)) : FishP + r.R(-1, 1) * m.jump * M.Lerp(.75f, 1.2f, hard); if (n < .06f) n = .06f + r.F() * .2f; if (n > .94f) n = .94f - r.F() * .2f; fishTo = n;
        }
        float k = F.M == Move.Darter ? 9 : 5; fishV = M.Damp(fishV, M.Clamp((fishTo - FishP) * k, -speed, speed), 7, dt); FishP = M.Clamp(FishP + fishV * dt, .04f, .96f); if (MathF.Abs(fishV) > .03f) Dir = fishV > 0 ? 1 : -1;
        // the net: A and D push it directly
        int inp = (In.Down(Act.Right) ? 1 : 0) - (In.Down(Act.Left) ? 1 : 0); NetV += inp * 5.2f * dt; NetV *= MathF.Exp(-dt * (inp == 0 ? 6.5f : 2.4f)); if (inp != 0 && MathF.Sign(NetV) != inp) NetV *= MathF.Exp(-dt * 9); NetV = M.Clamp(NetV, -1.25f, 1.25f); NetP += NetV * dt; float half = NetW / 2; if (NetP < half) { NetP = half; NetV = MathF.Abs(NetV) * .25f; } if (NetP > 1 - half) { NetP = 1 - half; NetV = -MathF.Abs(NetV) * .25f; }
        float w = NetW * (Phase == 3 ? .78f : 1); float off = FishP - NetP; Inside = MathF.Abs(off) < w / 2 + .012f;
        // line tension and catch progress
        float gain = reel ? Inside ? (Phase == 1 ? .62f + hard * .4f : .10f + hard * .24f) : .52f : -.85f; if (Phase == 4 && reel) gain *= .3f; Tension = M.Clamp(Tension + gain * dt / M.Lerp(1, FishDex.RodLine[Store.Data.Rod], .6f), 0, 1.1f); if (grace > 0) Tension = MathF.Min(Tension, .7f);
        if (Inside && reel) Catch += dt * (Phase == 1 ? .03f : M.Lerp(.30f, .115f, MathF.Sqrt(hard))) * pow * (Phase == 4 ? 1.6f : 1) * (F.Legend ? .75f : 1); else if (Inside) Catch += dt * .012f; else if (grace <= 0) Catch -= dt * M.Lerp(.045f, .10f, hard);
        if (Phase == 2) { if (!leapOk && phaseT > .1f && In.Press(Act.Jump)) { leapOk = true; Catch += .10f; Snd.Play("confirm", .6f); Say("Nice!"); } float q = phaseT / phaseLen; Bob.Y = pool.Surf - M.Sin(q * M.PI) * (34 + hard * 40); } else Bob.Y = pool.Surf + (Phase == 3 ? 10 : 2) + M.Sin(g.Env.T * 12) * 1.2f;
        // what should the player be doing right now?
        NeedRelease = Tension > .72f || (Phase == 1 && reel && Tension > .35f); NeedLeft = !Inside && off < 0; NeedRight = !Inside && off > 0; NeedReel = Inside && !reel && !NeedRelease && Phase != 1;
        // the fish you see in the water mirrors the fish on the bar
        float cx2 = p.P.X + p.Dir * 110; float want = cx2 + (FishP - .5f) * (130 + hard * 70); Bob.X = M.Damp(Bob.X, want, 5, dt); Bob.X = M.Clamp(Bob.X, pool.X0 * 24 + 16, (pool.X1 + 1) * 24 - 16); int gd = 0; while (g.W.Solid((int)(Bob.X / 24), (int)((pool.Surf + 4) / 24)) && gd++ < 140) Bob.X += p.Dir * 4; if (r.P(dt * (3 + hard * 7))) { g.Life.Splash(new(Bob.X, pool.Surf), .12f + (Phase == 1 ? .3f : 0)); g.R.Ripple(Bob.X, 1.4f + hard * 2); }
        reelSnd -= dt; if (reel && reelSnd < 0) { reelSnd = .075f; Snd.Play("reel", Inside ? .28f : .12f, .85f + Tension * .5f); } tensSnd -= dt; if (Tension > .74f && tensSnd < 0) { tensSnd = .16f; Snd.Play("tension", .6f, .8f + Tension * .6f); shakeT = .1f; }
        over = Tension >= 1 ? over + dt : MathF.Max(0, over - dt * 2); if (over > .55f) { Snd.Play("snap"); Say("The line snapped!"); g.Shake(4); Lose(); return; }
        if (Catch <= 0) { Say("It slipped the net..."); Lose(); return; }
        if (Catch >= 1) Win();
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
        if (S == FS.Fight && Phase == 2) { float k = M.Clamp(phaseT / phaseLen); Raylib.DrawTexturePro(shadow, new Rectangle(0, 0, Dir < 0 ? -150 : 150, 80), new Rectangle(b.X, b.Y, 46 + Big * 44, 25 + Big * 22), new(23 + Big * 22, 12 + Big * 11), (k - .5f) * 80 * Dir, new Color(255, 255, 255, 255)); }
        else if (S != FS.Fight || Phase != 3) { Raylib.DrawCircleV(b, 3, new Color(42, 26, 28, 255)); Raylib.DrawRectangleV(b - new Vector2(2, 2), new(4, 2), new Color(220, 80, 66, 255)); Raylib.DrawRectangleV(b - new Vector2(2, 0), new(4, 2), new Color(250, 242, 228, 255)); }
    }
    public void DrawUnder(int cx, int cy)
    {
        if (S is FS.Wait or FS.Bite && biteIn < 2.5f) { float k = M.Clamp(1 - biteIn / 2.5f); float s = 18 + cm * .16f; s = MathF.Min(s, 70); float x = Bob.X - cx + (1 - k) * 60 * (FishId % 2 == 0 ? 1 : -1), y = pool.Surf - cy + 14 + (1 - k) * 14; Raylib.DrawEllipse((int)x, (int)y, s * .5f, s * .18f, new Color(10, 20, 30, (int)(90 * k))); }
        if (S == FS.Fight && Phase != 2) { float s = .22f + Big * .5f; Raylib.DrawTexturePro(shadow, new Rectangle(0, 0, Dir < 0 ? -150 : 150, 80), new Rectangle(Bob.X - cx, pool.Surf - cy + 14 + (Phase == 3 ? 16 : 0), 150 * s, 80 * s), new(75 * s, 40 * s), M.Sin(g.Env.T * 9) * 6, new Color(255, 255, 255, 110)); }
    }
    void Meter(float x, float y, float w, float v, Color c, ref Spring s, float dt) { s.Update(M.Clamp(v, 0, 1), 260, 24, dt); Raylib.DrawRectangleRounded(new Rectangle(x - 1.5f, y - 1, w + 3, 11), .7f, 5, Ui.Ink); Raylib.DrawRectangleRounded(new Rectangle(x, y + .5f, w, 8), .7f, 5, new Color(214, 192, 150, 255)); if (s.X > .012f) Raylib.DrawRectangleRounded(new Rectangle(x, y + .5f, MathF.Max(5, w * M.Clamp(s.X)), 8), .7f, 5, c); Raylib.DrawRectangleRounded(new Rectangle(x + 2, y + 1.5f, MathF.Max(0, w * M.Clamp(s.X) - 5), 2), 1, 3, new Color(255, 255, 255, 80)); }
    void Key(float x, float y, string k, float lit, bool down) { float w = MathF.Max(16, Font.Width(k) + 9), bob = lit * MathF.Abs(M.Sin(Ui.T * 8)) * -2 + (down ? 1.5f : 0); Raylib.DrawRectangleRounded(new Rectangle(x - 1, y - 1 + bob, w + 2, 18 - (down ? 2 : 0)), .4f, 5, Ui.Ink); var c = Raylib.ColorLerp(new Color(226, 208, 172, 255), new Color(255, 204, 92, 255), M.Clamp(lit)); if (down) c = Raylib.ColorLerp(c, new Color(168, 214, 130, 255), .75f); Raylib.DrawRectangleRounded(new Rectangle(x, y + bob, w, 13), .4f, 5, c); Font.Draw(k, x + w / 2, y + 2.5f + bob, Ui.Ink, 1, .5f); }
    readonly Spring[] sp = new Spring[4];
    public void DrawUi()
    {
        float W = Ui.VW, dt = Ui.DT; var p = g.P; Vector2 ps = g.ToScreen(p.P + new Vector2(0, -100)); float t = Ui.T;
        if (MsgT > 0) { float k = M.Clamp(MsgT * 3); Font.Draw(Msg, M.Clamp(ps.X, 80, W - 80), ps.Y - 14 + (1 - k) * -6, Raylib.Fade(Ui.Cream, k), 1.5f, .5f, Ui.Ink); }
        if (S == FS.Aim) { float x = ps.X - 30, y = ps.Y; Ui.Nine("dark", x - 4, y - 3, 68, 14, Color.White); Raylib.DrawRectangleV(new(x, y), new(60, 8), new Color(60, 44, 48, 255)); Raylib.DrawRectangleGradientH((int)x, (int)y, (int)(60 * charge), 8, new Color(130, 180, 90, 255), charge > .9f ? new Color(255, 220, 120, 255) : new Color(240, 170, 70, 255)); }
        if (S == FS.Bite)
        {
            float b = Ease.OutElastic(M.Clamp(this.t * 2.4f)), left = M.Clamp(1 - this.t / biteWin); Raylib.DrawCircleV(new(ps.X, ps.Y - 8), 19 * b, new Color(52, 30, 28, 220)); Raylib.DrawRing(new(ps.X, ps.Y - 8), 15 * b, 18 * b, -90, -90 + 360 * left, 40, left > .35f ? Ui.Gold : Ui.Red); Ui.PushScale(ps.X, ps.Y - 8, b, b); Font.Draw("!", ps.X, ps.Y - 19, new Color(255, 224, 110, 255), 2.6f, .5f); Ui.PopScale(); Font.Draw("Press " + In.Name(Act.Fish) + " or click", ps.X, ps.Y + 15, Ui.Cream, 1, .5f, Ui.Ink);
        }
        if (S == FS.Fight)
        {
            float w = 330, x = (W - w) / 2 + (shakeT > 0 ? M.Sin(t * 90) * 2 * Store.Cfg.Shake : 0), y = 12; shakeT -= dt; Ui.Nine("panel", x, y, w, 100, Color.White); string nm = Store.Data.Journal.ContainsKey(FishId) ? F.Name : "Mystery fish"; Font.Draw(nm, x + 14, y + 11, Ui.Ink, 1.25f); for (int i = 0; i < F.Rarity; i++) Font.Draw("*", x + w - 20 - i * 7, y + 12, F.Legend ? Ui.Orange : Ui.Gold, 1.25f);
            // the water lane: net and fish
            float lx = x + 14, lw = w - 28, ly = y + 27, lh = 34; Raylib.DrawRectangleRounded(new Rectangle(lx - 2, ly - 2, lw + 4, lh + 4), .25f, 6, Ui.Ink); Raylib.DrawRectangleGradientV((int)lx, (int)ly, (int)lw, (int)lh, new Color(122, 190, 204, 255), new Color(58, 118, 146, 255)); for (int i = 0; i < 14; i++) Raylib.DrawRectangleV(new(lx + (i * 47 + t * 9) % (lw - 12), ly + 4 + (i * 11 % 26)), new(9, 1), new Color(190, 230, 238, 120)); Raylib.DrawRectangleV(new(lx, ly), new(lw, 2), new Color(220, 244, 248, 190));
            sp[0].Update(NetP, 900, 44, dt); float nw = NetW * lw, nx = lx + sp[0].X * lw - nw / 2; var nc = NeedRelease ? new Color(230, 90, 70, 255) : Inside ? new Color(255, 214, 96, 255) : new Color(244, 236, 216, 255); Raylib.DrawRectangleRounded(new Rectangle(nx, ly + 1, nw, lh - 2), .3f, 6, Raylib.Fade(nc, Inside ? .34f : .18f)); for (float q = 5; q < nw - 2; q += 7) { Raylib.DrawLineV(new(nx + q, ly + 2), new(nx + q, ly + lh - 2), Raylib.Fade(nc, .28f)); } for (float q = 7; q < lh; q += 7) Raylib.DrawLineV(new(nx + 1, ly + q), new(nx + nw - 1, ly + q), Raylib.Fade(nc, .28f)); Raylib.DrawRectangleRoundedLinesEx(new Rectangle(nx, ly + 1, nw, lh - 2), .3f, 6, 2, Ui.Ink); Raylib.DrawRectangleRoundedLinesEx(new Rectangle(nx + 1, ly + 2, nw - 2, lh - 4), .3f, 6, 1.4f, nc);
            float fx = lx + FishP * lw, hop = Phase == 2 ? M.Sin(M.Clamp(phaseT / phaseLen) * M.PI) * 24 : 0, fs = .20f + Big * .12f; bool kn = Store.Data.Journal.ContainsKey(FishId); var ft = kn ? g.BigFishTex(FishId) : shadow; float fy = ly + lh / 2 - hop + M.Sin(t * 5) * 1.5f + (Phase == 3 ? 7 : 0); Raylib.DrawTexturePro(ft, new Rectangle(0, 0, Dir < 0 ? -ft.Width : ft.Width, ft.Height), new Rectangle(fx, fy, ft.Width * fs * (kn ? .8f : 1), ft.Height * fs * (kn ? .8f : 1)), new(ft.Width * fs * (kn ? .4f : .5f), ft.Height * fs * (kn ? .4f : .5f)), M.Sin(t * 12) * (Phase == 1 ? 14 : 5), Phase == 3 ? new Color(190, 210, 230, 130) : Phase == 1 && t % .2f < .1f ? new Color(255, 190, 170, 255) : Color.White);
            // catch and line meters
            float by = y + 67; Font.Draw("Catch", lx, by, Ui.Ink); Meter(lx + 34, by, lw - 34, Catch, new Color(130, 176, 96, 255), ref sp[1], dt); float gy = y + 81; Font.Draw("Line", lx, gy, Ui.Ink); Col tc = Col.Lerp(Col.Lerp(0x78a8c8, 0xf0bd4a, M.Clamp(Tension * 1.7f)), 0xd84a3a, M.Clamp(Tension * 2.2f - 1.2f)); Meter(lx + 34, gy, lw - 34, Tension, Tension > .74f && t % .16f < .08f ? Color.White : tc.Rl(), ref sp[2], dt);
            // key coach: the key you need right now lights up
            float cy2 = y + 106, mid = W / 2; hintA = M.Damp(hintA, NeedLeft ? 1 : 0, 14, dt); hintD = M.Damp(hintD, NeedRight ? 1 : 0, 14, dt); hintF = M.Damp(hintF, NeedReel ? 1 : 0, 14, dt); Key(mid - 92, cy2, In.Name(Act.Left), hintA, In.Down(Act.Left)); Key(mid - 72, cy2, In.Name(Act.Right), hintD, In.Down(Act.Right)); Key(mid + 64, cy2, In.Name(Act.Fish), hintF, Held);
            string msg; Color mc; if (NeedRelease) { msg = Phase == 1 ? "It's running! Let go!" : "Let go!"; mc = new Color(255, 130, 108, 255); } else if (Phase == 2 && !leapOk) { msg = "Leap! Press " + In.Name(Act.Jump); mc = new Color(255, 226, 120, 255); } else if (NeedLeft || NeedRight) { msg = "Follow the fish"; mc = Ui.Cream; } else if (NeedReel) { msg = "Hold to reel"; mc = new Color(180, 240, 150, 255); } else if (Phase == 4) { msg = "It's tired. Reel!"; mc = new Color(180, 240, 150, 255); } else if (Phase == 1) { msg = "Wait for it..."; mc = new Color(255, 200, 150, 255); } else { msg = "Good, keep going"; mc = new Color(220, 236, 200, 255); } float pu = NeedRelease ? 1 + MathF.Abs(M.Sin(t * 10)) * .08f : 1; Ui.PushScale(mid, cy2 + 7, pu, pu); Font.Draw(msg, mid, cy2 + 3, mc, 1, .5f, Ui.Ink); Ui.PopScale();
        }
        if (S == FS.Caught)
        {
            float k = Ease.OutBack(M.Clamp(showT * 2.6f)); float w = 250, h = 150, x = (W - w) / 2, y = 52; Ui.PushScale(W / 2, y + h / 2, k, k, (1 - k) * -8); Raylib.BeginBlendMode(BlendMode.Additive); Ui.GlowAt(W / 2, y + 66, 150, new Color(255, 206, 120, F.Legend ? 130 : 70)); if (F.Legend || IsNew) for (int i = 0; i < 10; i++) { float a = t * .5f + i * M.TAU / 10; Raylib.DrawTriangle(new(W / 2, y + 66), new(W / 2 + M.Sin(a + .12f) * 170, y + 66 + M.Cos(a + .12f) * 170), new(W / 2 + M.Sin(a) * 170, y + 66 + M.Cos(a) * 170), new Color(255, 220, 140, 26)); } Raylib.EndBlendMode();
            Ui.Nine("panel", x, y, w, h, Color.White); string head = F.Legend ? "LEGENDARY CATCH!" : IsNew ? "New fish!" : IsRecord ? "New record!" : "Nice catch!"; Font.Draw(head, W / 2, y - 4, F.Legend ? Ui.Gold : Ui.Cream, 2, .5f, Ui.Ink, 1); Ui.Tex(art, W / 2 - 64, y + 12 + M.Sin(t * 2.4f) * 2, .66f, Color.White); Font.Draw(F.Name, W / 2, y + 92, Ui.Ink, 1.5f, .5f); Font.Draw(Got.Kg.ToString(Got.Kg < 1 ? "0.00" : "0.0") + " kg    " + Got.Cm.ToString("0") + " cm    " + FishDex.Value(Got) + " coins", W / 2, y + 108, Ui.Soft, 1, .5f); var ln = Font.Wrap(F.Text, w - 30); for (int i = 0; i < ln.Count; i++) Font.Draw(ln[i], W / 2, y + 120 + i * 9, Ui.Ink, 1, .5f); if (showT > 1) Font.Draw("click to continue", W / 2, y + h + 4, Raylib.Fade(Ui.Cream, .6f + .4f * M.Sin(t * 4)), 1, .5f, Ui.Ink); Ui.PopScale();
        }
    }
}

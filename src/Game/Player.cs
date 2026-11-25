using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public sealed class Player
{
    public Vector2 P, V; public AvatarData D; public readonly AvatarAnim An = new(); readonly AvatarPainter ap = new(); readonly Painter pt = new(150, 150); public Texture2D Tex; readonly World w; readonly GameScreen g;
    public bool Ground, Swim, Climb, Sit, Crouch; public Prop Seat; float coyote, buffer, slideT, landT, dropT, fallPeak, ledgeT = -1; Vector2 ledgeA, ledgeB; public float StepY; public Anim Emote = Anim.Idle; public float EmoteT; public float Air; public bool Frozen; public float WetT;
    public const float HW = 8, OX = 75, OY = 128; public float HH => Crouch ? 40 : 62; public int Dir = 1; float footT;
    public Player(World w, GameScreen g) { this.w = w; this.g = g; Tex = pt.ToTexture(); An.Event = Ev; }
    public void SetAvatar(AvatarData d) { D = d; An.IdleStyle = d.Idle; An.Stride = AvatarPainter.StrideOf(d); Redraw(); }
    public Vector2 HandPos(int side) => new(P.X + (side < 0 ? ap.HandL.X : ap.HandR.X) * AvatarAnim.PxPerUnit, P.Y + StepY + (side < 0 ? ap.HandL.Y : ap.HandR.Y) * AvatarAnim.PxPerUnit);
    public void Redraw() { pt.Clear(); ap.Draw(pt, D, An.Pose, AvatarAnim.PxPerUnit, OX, OY, outlinePx: Store.Cfg.Contrast ? 2 : 1); pt.Upload(Tex); }
    public string Surface()
    {
        int tx = (int)(P.X / 24), ty = (int)((P.Y + 4) / 24); if (w.PlatAt(tx, ty) && !w.Solid(tx, ty)) return "wood"; var t = w.At(tx, ty); return t switch { T.Sand => "sand", T.Stone or T.Cobble or T.Moss or T.Clay => "stone", T.Plank => "wood", _ => w.Solid(tx, ty - 1) ? "dirt" : World.BiomeAt(tx) == Biome.Autumn ? "leaves" : w.WallAt(tx, ty - 1) != T.Air ? "dirt" : "grass" };
    }
    void Ev(string e)
    {
        switch (e)
        {
            case "step": Snd.Play("step_" + Surface(), An.State == Anim.Run ? .55f : .4f, .92f + M.Hash(Environment.TickCount, 1) * .18f); g.Life.Dust(P, Surface(), An.State == Anim.Run ? 2 : 1, -Dir); if (WetT > 0) g.Life.Drip(P); break;
            case "climb": Snd.Play("climb", .4f, .9f + M.Hash(Environment.TickCount, 2) * .2f); break;
            case "stroke": Snd.Play("stroke", .4f, .9f + M.Hash(Environment.TickCount, 2) * .2f); if (w.PoolAt(P.X, P.Y - 10) is Pool q) g.R.Ripple(P.X, 1.6f); break;
        }
    }
    bool Free(float x, float y, float hh) { int x0 = (int)MathF.Floor((x - HW) / 24), x1 = (int)MathF.Floor((x + HW - .01f) / 24), y0 = (int)MathF.Floor((y - hh) / 24), y1 = (int)MathF.Floor((y - .01f) / 24); for (int j = y0; j <= y1; j++) for (int i = x0; i <= x1; i++) if (w.Solid(i, j)) return false; return true; }
    public void DoEmote(Anim a) { Emote = a; EmoteT = a is Anim.SitGround or Anim.Sleep or Anim.Dance ? 999 : 2.2f; Sit = false; An.Set(a); An.StateT = 0; }
    public void StandUp() { if (Sit && Seat != null) { Sit = false; Seat = null; An.Set(Anim.Idle); } }
    public void SitOn(Prop b) { Sit = true; Seat = b; V = default; P = new(b.X, b.Y - 1); Snd.Play("sit"); An.StateT = 0; EmoteT = 0; }
    public void Update(float dt, bool control)
    {
        An.Wind = g.Env.Wind * (w.WallAt((int)(P.X / 24), (int)((P.Y - 30) / 24)) == T.Air ? .85f : .05f); An.Talk = Voice.Talking ? 1 : 0; WetT = MathF.Max(0, WetT - dt); StepY = M.Damp(StepY, 0, 18, dt); dropT -= dt; landT -= dt; coyote -= dt; buffer -= dt;
        if (ledgeT >= 0) { ledgeT += dt / .42f; float t = M.Clamp(ledgeT); P = t < .5f ? new(ledgeA.X, M.Lerp(ledgeA.Y, ledgeB.Y + 8, Ease.OutCubic(t * 2))) : new(M.Lerp(ledgeA.X, ledgeB.X, (t - .5f) * 2), M.Lerp(ledgeB.Y + 8, ledgeB.Y, (t - .5f) * 2)); An.Set(t < .55f ? Anim.Ledge : Anim.Crouch); if (t >= 1) { P = ledgeB; ledgeT = -1; V = default; Ground = true; } Tick(dt); return; }
        bool L = control && In.Down(Act.Left), R = control && In.Down(Act.Right), U = control && In.Down(Act.Up), Dn = control && In.Down(Act.Down), run = control && In.Down(Act.Run); float mx = (R ? 1 : 0) - (L ? 1 : 0); if (control && In.Press(Act.Jump)) buffer = .13f; if (In.Press(Act.Jump) == false && U && !Climb && !Swim && false) buffer = .1f;
        if (Sit) { if (mx != 0 || buffer > 0 || U) { StandUp(); buffer = 0; } else { An.Set(Anim.Sit); An.Vel = default; Tick(dt); return; } }
        if (EmoteT > 0) { EmoteT -= dt; if (mx != 0 || buffer > 0 || !Ground) EmoteT = 0; }
        var pool = w.PoolAt(P.X, P.Y - 4); float sub = pool != null ? P.Y - pool.Surf : 0; bool was = Swim; Swim = pool != null && sub > (Swim ? 36 : 44); if (Swim && !was) { Crouch = false; float hit = MathF.Abs(V.Y); g.Life.Splash(new(P.X, pool.Surf), M.Clamp(hit / 300, .25f, 1.4f)); g.R.Ripple(P.X, M.Clamp(hit / 70, 1.5f, 7)); Snd.Play(hit > 180 ? "splash" : "splash_small", M.Clamp(hit / 400, .35f, 1)); V.Y *= .35f; Climb = false; } if (was && !Swim) { g.Life.Splash(new(P.X, P.Y - 10), .4f); Snd.Play("splash_small", .5f); WetT = 5; }
        if (sub > 4 && !Swim && MathF.Abs(V.X) > 20 && Ground) { footT -= dt * MathF.Abs(V.X); if (footT < 0) { footT = 24; g.Life.Splash(new(P.X, pool.Surf), .22f); g.R.Ripple(P.X, 1.2f); Snd.Play("splash_small", .25f, 1.2f); } WetT = 5; }
        int tx = (int)MathF.Floor(P.X / 24); byte c = 0; int cTile = 0; for (int k = -1; k <= 1 && c == 0; k++) { if (MathF.Abs((tx + k) * 24 + 12 - P.X) > 17) continue; foreach (float oy in new[] { 6f, 30f, 2f }) { c = w.ClimbAt(tx + k, (int)((P.Y - oy) / 24)); if (c != 0) { cTile = tx + k; break; } } }
        byte below = w.ClimbAt(tx, (int)((P.Y + 6) / 24));
        if (!Climb && c != 0 && !Swim && ((U && V.Y > -60) || (Dn && !Ground))) { Climb = true; V = default; }
        if (!Climb && Ground && Dn && below != 0 && w.PlatAt(tx, (int)((P.Y + 6) / 24))) { Climb = true; cTile = tx; P.Y += 6; dropT = .3f; V = default; }
        if (Climb && c == 0 && below == 0) { Climb = false; if (U) V.Y = -150; }
        if (Climb)
        {
            An.Phase += 0; P.X = M.Damp(P.X, cTile * 24 + 12, 14, dt); float vy = (Dn ? 1 : 0) - (U ? 1 : 0); V = new(0, vy * 92); if (buffer > 0) { Climb = false; buffer = 0; V = new(mx * 110, -270); Snd.Play("jump", .5f); } else { float ny = P.Y + V.Y * dt; if (Free(P.X, ny, HH)) P.Y = ny; else if (V.Y > 0) { Climb = false; } An.Set(Anim.Climb); An.Vel = V; Ground = false; fallPeak = P.Y; if (U && !w.Solid(tx, (int)((P.Y - 8) / 24)) && w.ClimbAt(cTile, (int)((P.Y - 10) / 24)) == 0) { Climb = false; V = new(mx * 40, -150); } Tick(dt); return; }
        }
        // swimming
        if (Swim)
        {
            float buoy = sub > 46 ? -1 : 1; float tvx = mx * (run ? 118 : 82); V.X = M.Damp(V.X, tvx, 4, dt); float ty = U ? -95 : Dn ? 95 : (sub > 54 ? -38 : sub > 49 ? -10 : 14); if (U && sub < 51) ty = -12; V.Y = M.Damp(V.Y, ty, 4, dt); if (buffer > 0 && sub < 60) { V.Y = -300; buffer = 0; Snd.Play("splash_small", .7f); g.Life.Splash(new(P.X, pool.Surf), .5f); }
            Move(dt); if (mx != 0) Dir = (int)mx; An.Dir = Dir; An.Vel = V; An.Set(MathF.Abs(V.X) > 26 || MathF.Abs(V.Y) > 55 ? Anim.Swim : Anim.Tread); if (sub > 56) { Air += dt; if (M.Hash((int)(g.Env.T * 8), 2) > .72f) g.Life.Bubble(new(P.X + Dir * 6, P.Y - 48)); } else Air = 0; fallPeak = P.Y; Tick(dt); return;
        }
        // ground and air
        bool wantCrouch = Dn && Ground && below == 0 && !Frozen; if (Ground && wantCrouch && run && MathF.Abs(V.X) > 110 && slideT <= 0 && !Crouch) { slideT = .78f; V.X = Dir * 225; Snd.Play("slide", .6f); }
        if (!wantCrouch && Crouch && !Free(P.X, P.Y, 62)) wantCrouch = true; Crouch = wantCrouch || (slideT > 0 && Ground);
        if (slideT > 0) { slideT -= dt; V.X = M.Approach(V.X, 0, 230 * dt); if (!Ground || MathF.Abs(V.X) < 40) slideT = 0; if (M.Hash((int)(g.Env.T * 40), 4) > .35f) g.Life.Dust(P + new Vector2(Dir * 22, 0), Surface(), 1, -Dir); }
        else { float spd = Crouch ? 38 : run ? 138 : 66; float acc = Ground ? (mx != 0 ? 900 : 1100) : 620; V.X = M.Approach(V.X, mx * spd, acc * dt); }
        if (mx != 0 && slideT <= 0) Dir = (int)mx; V.Y = MathF.Min(V.Y + (V.Y < 0 && control && In.Down(Act.Jump) ? 760 : 1080) * dt, 620); if (Ground) coyote = .1f;
        if (buffer > 0 && coyote > 0 && Dn && w.PlatAt(tx, (int)((P.Y + 2) / 24)) && !w.Solid(tx, (int)((P.Y + 2) / 24))) { dropT = .22f; buffer = 0; P.Y += 2; Ground = false; coyote = 0; }
        else if (buffer > 0 && coyote > 0) { V.Y = -338; buffer = coyote = 0; Ground = false; slideT = 0; An.Punch(-4); Snd.Play("jump", .5f, .95f + M.Hash(Environment.TickCount, 4) * .12f); g.Life.Dust(P, Surface(), 4, 0); EmoteT = 0; }
        bool wasG = Ground; if (!Ground) fallPeak = MathF.Min(fallPeak, P.Y); else fallPeak = P.Y; float vy0 = V.Y; Move(dt);
        if (Ground && !wasG) { float h = P.Y - fallPeak; if (h > 14) { landT = h > 60 ? .2f : .1f; An.Punch(M.Clamp(h / 14, 2, 9)); Snd.Play("land", M.Clamp(h / 130, .18f, .8f)); Snd.Play("step_" + Surface(), .6f, .85f); g.Life.Dust(P, Surface(), (int)M.Clamp(h / 12, 3, 12), 0); if (h > 150) g.Shake(M.Clamp(h / 60, 0, 4)); } }
        // ledge grab
        if (!Ground && V.Y > 20 && mx != 0 && slideT <= 0) { int wx = (int)MathF.Floor((P.X + mx * (HW + 4)) / 24), wy = (int)MathF.Floor((P.Y - 56) / 24); if (w.Solid(wx, wy) && !w.Solid(wx, wy - 1) && !w.Solid(wx, wy - 2) && !w.Solid(wx, wy - 3) && !w.Solid(tx, wy - 1) && !w.Solid(tx, wy)) { ledgeA = new(P.X, wy * 24 + 58); ledgeB = new(wx * 24 + 12, wy * 24); ledgeT = 0; V = default; Snd.Play("climb", .6f, .8f); } }
        An.Dir = Dir; An.Vel = V; if (!Frozen)
        {
            if (slideT > 0) An.Set(Anim.Slide); else if (!Ground) { if (V.Y < 40 || P.Y - fallPeak < 8 && coyote > -.12f) { if (coyote < -.04f || V.Y < -60) An.Set(V.Y < 40 ? Anim.Jump : Anim.Fall); } else An.Set(Anim.Fall); } else if (landT > 0 && MathF.Abs(V.X) < 30) An.Set(Anim.Land); else if (Crouch) An.Set(MathF.Abs(V.X) > 8 ? Anim.CrouchWalk : Anim.Crouch); else if (MathF.Abs(V.X) > 92) An.Set(Anim.Run); else if (MathF.Abs(V.X) > 6) An.Set(Anim.Walk); else if (EmoteT > 0) An.Set(Emote); else An.Set(Anim.Idle);
        }
        Tick(dt);
    }
    void Tick(float dt) { if (An.Update(dt)) Redraw(); }
    void Move(float dt)
    {
        float nx = P.X + V.X * dt; nx = M.Clamp(nx, 26, World.W * 24 - 26);
        // soft current pushing back at the edge of the world
        if (Swim) { float e = 30 * 24; if (P.X < e) V.X += (e - P.X) / e * 260 * dt; if (P.X > World.W * 24 - e) V.X -= (P.X - World.W * 24 + e) / e * 260 * dt; }
        if (Free(nx, P.Y, HH)) P.X = nx;
        else if ((Ground || Swim) && Free(nx, P.Y - 24.5f, HH) && Free(P.X, P.Y - 24.5f, HH)) { float oy = P.Y; P.X = nx; P.Y = MathF.Floor((P.Y - 1) / 24) * 24; if (P.Y > oy - .5f) P.Y -= 24; StepY += oy - P.Y; StepY = MathF.Min(StepY, 26); }
        else V.X = 0;
        float ny = P.Y + V.Y * dt; Ground = false;
        if (V.Y >= 0)
        {
            int f0 = (int)MathF.Floor((P.Y - .01f) / 24), f1 = (int)MathF.Floor((ny) / 24); bool hit = false; int x0 = (int)MathF.Floor((P.X - HW) / 24), x1 = (int)MathF.Floor((P.X + HW - .01f) / 24);
            for (int y = f0 + 1; y <= f1 && !hit; y++) for (int x = x0; x <= x1; x++) if (w.Solid(x, y) || (w.PlatAt(x, y) && dropT <= 0 && !Swim)) { P.Y = y * 24; V.Y = 0; Ground = true; hit = true; break; }
            if (!hit) { P.Y = ny; if (!Swim && V.X != 0 && V.Y < 90 && coyote > .05f) { int by = (int)MathF.Floor((P.Y + 24.5f) / 24); bool land = false; for (int x = x0; x <= x1; x++) land |= w.Solid(x, by) && !w.Solid(x, by - 1); if (land && by * 24 - P.Y <= 25 && by * 24 - P.Y > 0) { float oy = P.Y; P.Y = by * 24; StepY -= P.Y - oy; Ground = true; V.Y = 0; } } }
        }
        else if (Free(P.X, ny, HH)) P.Y = ny; else { if (!Swim) { // corner nudge
                for (int k = 1; k <= 6; k++) { if (Free(P.X + k, ny, HH)) { P.X += k; P.Y = ny; return; } if (Free(P.X - k, ny, HH)) { P.X -= k; P.Y = ny; return; } } } V.Y = 0; }
    }
    public void Draw(int cx, int cy)
    {
        float sq = M.Clamp(An.Squash, -.3f, .3f) * (Store.Cfg.ReduceMotion ? .3f : 1); float sx = 1 + sq * .45f, sy = 1 - sq * .5f; float x = MathF.Round(P.X) - cx, y = MathF.Round(P.Y + StepY) - cy + 1;  Raylib.DrawTexturePro(Tex, new Rectangle(0, 0, pt.W, pt.H), new Rectangle(MathF.Round(x - OX * sx), MathF.Round(y - OY * sy), MathF.Round(pt.W * sx), MathF.Round(pt.H * sy)), Vector2.Zero, 0, Color.White);
    }
}

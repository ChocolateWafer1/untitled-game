using System.Numerics;
namespace UnnamedGame;

public enum Anim { Idle, Walk, Run, Jump, Fall, Land, Crouch, CrouchWalk, Slide, Climb, Swim, Tread, Sit, RodHold, Cast, Reel, Wave, Dance, Laugh, Point, Cheer, Think, Sleep, Admire, Ledge, ShowOff, Shrug, Bow }

/// Turns movement into poses. Poses are sampled 12 times a second ("on twos") for a hand drawn feel,
/// while position, cloth and physics keep running smoothly underneath.
public sealed class AvatarAnim
{
    public Anim State = Anim.Idle; public float StateT, Phase; public int Dir = 1; public Vector2 Vel; public float Wind, Fps = 12, Talk;
    public Vector2 Look; public float ReelSpeed, Strain; public int AdmireSlot; public int IdleStyle; public float Stride = 1;
    public Action<string> Event; // "step", "stroke", ...
    public Pose Pose = Pose.Stand;
    Spring hemX = new(), hemY = new(), hairX = new(), hairY = new(), slX = new(), slY = new(), squash = new(), turn = new();
    float t, acc, blinkT = 2, blink, lookT, lookX, lookY; Vector2 lastVel; readonly Rng rng = new(Environment.TickCount);
    public const float PxPerUnit = .76f;

    public void Set(Anim a) { if (a != State) { State = a; StateT = 0; } }
    public void Punch(float v) => squash.V += v;
    public float Squash => squash.X;
    public void Gust(float v) { hemX.V += v * 14; hairX.V += v * 18; slX.V += v * 10; }

    public bool Update(float dt, float scale = PxPerUnit)
    {
        t += dt; StateT += dt; dt = MathF.Min(dt, .05f);
        // gait phase is tied to ground speed so feet never skate
        float v = MathF.Abs(Vel.X);
        if (State is Anim.Walk or Anim.Run or Anim.CrouchWalk)
        {
            float amp = State == Anim.Run ? .74f : State == Anim.CrouchWalk ? .36f : .46f, step = 2 * MathF.Sin(amp * Stride) * 42 * scale * (State == Anim.Run ? 1.32f : 1f);
            float o = Phase; Phase += v / step * M.PI * dt;
            if ((int)((o + M.PI / 2) / M.PI) != (int)((Phase + M.PI / 2) / M.PI)) Event?.Invoke("step");
        }
        else if (State == Anim.Climb) { float o = Phase; Phase += MathF.Abs(Vel.Y) * dt * .085f; if ((int)(o / M.PI) != (int)(Phase / M.PI)) Event?.Invoke("climb"); }
        else if (State is Anim.Swim or Anim.Tread) { float o = Phase; Phase += dt * (State == Anim.Swim ? 4.6f : 2.6f); if ((int)(o / M.PI) != (int)(Phase / M.PI)) Event?.Invoke("stroke"); }
        else if (State == Anim.Reel) Phase += dt * (3 + ReelSpeed * 14);
        else if (State == Anim.Dance) Phase += dt * 7.5f;
        // secondary motion
        Vector2 accel = (Vel - lastVel) / MathF.Max(dt, .001f); lastVel = Vel;
        float gust = Wind * (1 + .6f * M.Sin(t * 1.9f) + .35f * M.Sin(t * 4.3f + 1));
        float tx = M.Clamp(-Vel.X * .016f + gust * 1.1f, -4.2f, 4.2f), ty = M.Clamp(-Vel.Y * .012f, -4, 2.4f);
        hemX.Update(tx * .8f, 120, 7.5f, dt); hemY.Update(ty, 130, 8, dt); hairX.Update(M.Clamp(-Vel.X * .018f + gust * 2.1f, -5.2f, 5.2f), 85, 6, dt); hairY.Update(ty * 1.2f, 90, 6, dt); slX.Update(tx * .65f, 150, 8, dt); slY.Update(ty * .6f, 150, 8, dt);
        hemX.V -= M.Clamp(accel.X, -2500, 2500) * .00004f; hairX.V -= M.Clamp(accel.X, -2500, 2500) * .00006f;
        squash.Update(0, 260, 13, dt);
        turn.Update(Moving ? Dir : State is Anim.RodHold or Anim.Cast or Anim.Reel or Anim.Slide || (State is Anim.Jump or Anim.Fall && v > 12) ? Dir * .9f : 0, 240, 26, dt);
        blinkT -= dt; if (blinkT < 0) { blinkT = rng.R(1.8f, 5); blink = .16f; if (rng.P(.15f)) blinkT = .28f; }
        blink = MathF.Max(0, blink - dt); lookT -= dt;
        if (lookT < 0) { lookT = rng.R(1.5f, 4.5f); if (rng.P(.5f)) { lookX = rng.R(-1, 1); lookY = rng.R(-.4f, .6f); } else { lookX = lookY = 0; } }
        acc += dt; float fr = 1 / Fps; if (acc < fr) return false; acc -= fr; if (acc > fr) acc = 0;
        var target = Build(); bool cyc = State is Anim.Walk or Anim.Run or Anim.Swim or Anim.Climb or Anim.Dance or Anim.CrouchWalk;
        Pose = Mix(Pose, target, StateT < .12f ? .6f : cyc ? 1 : .55f);
        Pose.HemX = hemX.X; Pose.HemY = hemY.X; Pose.HairX = hairX.X; Pose.HairY = hairY.X; Pose.SleeveX = slX.X; Pose.SleeveY = slY.X; Pose.T = t;
        Pose.Wind = MathF.Min(1.3f, MathF.Abs(gust) * .55f + v * .0045f + MathF.Abs(Vel.Y) * .0025f);
        Pose.Blink = blink > 0 || State is Anim.Sleep or Anim.Laugh ? 1 : 0; Pose.Turn = M.Clamp(turn.X, -1, 1); Pose.Squash = squash.X;
        return true;
    }
    bool Moving => State is Anim.Walk or Anim.Run or Anim.CrouchWalk or Anim.Swim;

    static Pose Mix(Pose a, Pose b, float k)
    {
        Pose r = b; float L(float x, float y) => x + (y - x) * k;
        r.RootX = L(a.RootX, b.RootX); r.Lift = L(a.Lift, b.Lift); r.Lean = L(a.Lean, b.Lean); r.HeadTilt = L(a.HeadTilt, b.HeadTilt); r.HeadX = L(a.HeadX, b.HeadX); r.HeadY = L(a.HeadY, b.HeadY); r.Breath = L(a.Breath, b.Breath);
        r.ArmL0 = L(a.ArmL0, b.ArmL0); r.ArmL1 = L(a.ArmL1, b.ArmL1); r.ArmR0 = L(a.ArmR0, b.ArmR0); r.ArmR1 = L(a.ArmR1, b.ArmR1); r.LegL0 = L(a.LegL0, b.LegL0); r.LegL1 = L(a.LegL1, b.LegL1); r.LegR0 = L(a.LegR0, b.LegR0); r.LegR1 = L(a.LegR1, b.LegR1); r.FootL = L(a.FootL, b.FootL); r.FootR = L(a.FootR, b.FootR);
        r.LookX = L(a.LookX, b.LookX); r.LookY = L(a.LookY, b.LookY); r.Brow = L(a.Brow, b.Brow); r.Mouth = L(a.Mouth, b.Mouth); r.RootY = L(a.RootY, b.RootY); r.WristL = L(a.WristL, b.WristL); r.WristR = L(a.WristR, b.WristR); r.OpenL = b.OpenL; r.OpenR = b.OpenR; return r;
    }

    void Gait(ref Pose p, float amp, float flex, float arm, float elbow, float lean)
    {
        float d = Dir, ph = Phase; amp *= Stride; flex *= M.Lerp(.28f, 1, M.Clamp((Stride - .4f) / .6f)); arm *= M.Lerp(.7f, 1, Stride);
        (float h, float k, float f) Leg(float q)
        {
            float s = M.Sin(q), c = M.Cos(q); float hip = amp * s + amp * .06f;
            float kn = .10f + flex * MathF.Pow(MathF.Max(0, M.Cos(q + .42f)), 1.25f) + MathF.Max(0, -c) * .12f * flex;
            float foot = s > .72f && c > -.3f ? -.34f : s < -.35f ? M.Lerp(.0f, .62f, M.Smooth(-.35f, -1, s)) : c > 0 ? .22f : 0; return (hip, hip - kn, foot);
        }
        var a = Leg(ph); var b = Leg(ph + M.PI); p.LegL0 = d * a.h; p.LegL1 = d * a.k; p.FootL = a.f; p.LegR0 = d * b.h; p.LegR1 = d * b.k; p.FootR = b.f;
        float sa = M.Sin(ph - .12f);
        p.ArmL0 = -d * arm * sa - .03f; p.ArmL1 = p.ArmL0 + d * (elbow + MathF.Max(0, -sa) * elbow * .55f);
        p.ArmR0 = d * arm * sa + .03f; p.ArmR1 = p.ArmR0 + d * (elbow + MathF.Max(0, sa) * elbow * .55f);
        p.Lean = d * lean; p.HeadTilt = d * lean * .3f; p.HeadX = d * lean * 4; p.HeadY = M.Sin(ph * 2) * .4f; p.LookX = d * .7f;
    }

    Pose Build()
    {
        var p = Pose.Stand; float s = StateT, d = Dir; float br = M.Sin(t * 2.1f); p.Breath = br; p.LookX = lookX; p.LookY = lookY;
        if (Look != default) { p.LookX = Look.X; p.LookY = Look.Y; }
        float tk = Talk > 0 ? MathF.Abs(M.Sin(t * 17)) : 0; p.Mouth = tk * .9f;
        switch (State)
        {
            case Anim.Idle: Idle(ref p, br); break;
            case Anim.Walk: Gait(ref p, .46f, .82f, .42f, .2f, .05f); break;
            case Anim.Run: Gait(ref p, .74f, 1.4f, .62f, .92f, .17f); p.Lift = MathF.Max(0, M.Cos(Phase * 2 + .5f)) * 3.4f * Stride; break;
            case Anim.CrouchWalk: Gait(ref p, .36f, .7f, .25f, .7f, .3f); p.LegL0 += .35f * d; p.LegR0 += .35f * d; p.LegL1 -= .45f * d; p.LegR1 -= .45f * d; break;
            case Anim.Crouch: p.LegL0 = -.95f; p.LegL1 = .75f; p.LegR0 = .95f; p.LegR1 = -.75f; p.Lean = .05f * d; p.ArmL0 = -.4f; p.ArmL1 = .1f; p.ArmR0 = .4f; p.ArmR1 = -.1f; p.HeadY = .8f; break;
            case Anim.Slide: p.LegL0 = d * 1.35f; p.LegL1 = d * 1.2f; p.LegR0 = d * 1.2f; p.LegR1 = d * .2f; p.Lean = -d * .75f; p.ArmL0 = -d * 1.1f; p.ArmL1 = -d * .9f; p.ArmR0 = -d * .7f; p.ArmR1 = d * .4f; p.HeadTilt = d * .3f; p.Brow = 1; p.Mouth = .5f; break;
            case Anim.Jump:
                if (MathF.Abs(Vel.X) > 12) { p.LegL0 = d * .85f; p.LegL1 = d * -.25f; p.LegR0 = -d * .3f; p.LegR1 = -d * .95f; p.FootR = .4f; } else { p.LegL0 = -.18f; p.LegL1 = -.05f; p.LegR0 = .3f; p.LegR1 = -.45f; }
                p.ArmL0 = -1.0f; p.ArmL1 = -1.45f; p.ArmR0 = 1.0f; p.ArmR1 = 1.45f; p.HeadY = -.5f; p.LookY = -.5f; p.Brow = .7f; break;
            case Anim.Fall: { p.LegL0 = -.35f; p.LegL1 = -.05f; p.LegR0 = .55f; p.LegR1 = -.2f; float f = M.Sin(t * 14) * .14f; p.ArmL0 = -1.5f + f; p.ArmL1 = -1.9f + f; p.ArmR0 = 1.5f - f; p.ArmR1 = 1.9f - f; p.LookY = .8f; p.Brow = 1; p.Mouth = Vel.Y > 420 ? .8f : .1f; break; }
            case Anim.Land: p.LegL0 = -.62f; p.LegL1 = .5f; p.LegR0 = .62f; p.LegR1 = -.5f; p.ArmL0 = -.6f; p.ArmL1 = -.2f; p.ArmR0 = .6f; p.ArmR1 = .2f; p.HeadY = 1; p.Lean = d * .04f; break;
            case Anim.Climb: { float c = M.Sin(Phase); p.ArmL0 = -2.6f + c * .35f; p.ArmL1 = -3.1f + c * .3f; p.ArmR0 = 2.6f + c * .35f; p.ArmR1 = 3.1f + c * .3f; p.LegL0 = -.5f - c * .35f; p.LegL1 = .25f + c * .3f; p.LegR0 = .5f - c * .35f; p.LegR1 = -.25f + c * .3f; p.LookY = -.6f; break; }
            case Anim.Ledge: p.ArmL0 = -2.8f; p.ArmL1 = -3.0f; p.ArmR0 = 2.8f; p.ArmR1 = 3.0f; p.LegL0 = -.1f; p.LegL1 = .1f; p.LegR0 = .5f; p.LegR1 = -.5f; p.LookY = -1; p.Brow = .6f; break;
            case Anim.Swim: { float c = M.Sin(Phase), c2 = M.Sin(Phase * 2); p.FreeRoot = true; p.RootY = 12; p.Lean = d * 1.05f; p.ArmL0 = d * (2.2f + c * 1.2f); p.ArmL1 = d * (2.5f + c * .9f); p.ArmR0 = d * (2.2f - c * 1.2f); p.ArmR1 = d * (2.5f - c * .9f); p.LegL0 = -d * (1.15f + c2 * .28f); p.LegL1 = -d * (1.35f + c2 * .35f); p.LegR0 = -d * (1.15f - c2 * .28f); p.LegR1 = -d * (1.35f - c2 * .35f); p.FootL = p.FootR = .8f; p.HeadTilt = -d * .55f; p.HeadX = d * 2; p.HeadY = -1.5f; break; }
            case Anim.Tread: { float c = M.Sin(Phase); p.FreeRoot = true; p.RootY = M.Sin(Phase * .5f) * 1.4f; p.ArmL0 = -1.15f + c * .22f; p.ArmL1 = -1.3f - c * .5f; p.ArmR0 = 1.15f - c * .22f; p.ArmR1 = 1.3f + c * .5f; p.LegL0 = -.25f + c * .4f; p.LegL1 = -.1f + c * .2f; p.LegR0 = .25f + c * .4f; p.LegR1 = .1f + c * .2f; p.Brow = .3f; break; }
            case Anim.Sit: p.LegL0 = -1.2f; p.LegL1 = -.02f; p.LegR0 = 1.2f; p.LegR1 = .02f; p.ArmL0 = -.3f; p.ArmL1 = .12f; p.ArmR0 = .3f; p.ArmR1 = -.12f; p.HeadY = .2f + br * .15f; break;
            case Anim.Sleep: p.LegL0 = -1.3f; p.LegL1 = .55f; p.LegR0 = 1.3f; p.LegR1 = -.55f; p.ArmL0 = -.2f; p.ArmL1 = .2f; p.ArmR0 = .2f; p.ArmR1 = -.2f; p.HeadTilt = .24f + br * .04f; p.HeadY = 1.4f + br * .3f; break;
            case Anim.RodHold: case Anim.Cast: case Anim.Reel:
                {
                    p.LegL0 = -.2f; p.LegL1 = -.1f; p.LegR0 = .2f; p.LegR1 = .1f; float lead = .5f, sup = .2f, lean = 0;
                    if (State == Anim.Cast) { lead = 2.6f; sup = .6f; lean = -d * .16f; }
                    if (State == Anim.Reel) { lean = -d * (.06f + Strain * .24f); lead = .7f + Strain * .5f + M.Sin(t * 15) * Strain * .06f; p.LegL0 = -.3f - (d > 0 ? Strain * .15f : 0); p.LegR0 = .3f + (d < 0 ? Strain * .15f : 0); p.LegL1 = -.05f; p.LegR1 = .05f; p.Brow = Strain > .6f ? -.6f : .3f; }
                    // the leading arm extends the rod while the other cranks the reel
                    float crank = State == Anim.Reel ? M.Sin(Phase) * .22f * MathF.Min(1, ReelSpeed * 2) : 0; p.Lean = lean;
                    if (d > 0) { p.ArmR0 = lead * .5f; p.ArmR1 = lead + .75f; p.ArmL0 = sup; p.ArmL1 = sup + 1.05f + crank; } else { p.ArmL0 = -lead * .5f; p.ArmL1 = -lead - .75f; p.ArmR0 = -sup; p.ArmR1 = -sup - 1.05f + crank; }
                    p.LookX = d; p.LookY = .4f; break;
                }
            case Anim.Wave:
                {
                    // a wave travels down the arm: shoulder leads, then the elbow, then the wrist
                    float w = s * 8.5f, rise = M.Smooth(0, .22f, s), a = M.Sin(w), b = M.Sin(w - .75f), c = M.Sin(w - 1.5f);
                    p.ArmR0 = M.Lerp(.15f, 1.40f + a * .20f, rise); p.ArmR1 = M.Lerp(0, 2.92f + b * .42f, rise); p.WristR = c * .48f * rise; p.OpenR = 1; p.ArmL0 -= a * .02f; p.Lean = -.03f + a * .012f; p.HeadTilt = .07f + a * .025f; p.HeadX = .4f; p.Mouth = .5f; p.Brow = .55f; p.LookX = 0; p.LookY = 0; break;
                }
            case Anim.Dance: { float c = M.Sin(Phase), c2 = M.Sin(Phase * 2); p.RootX = c * 2.2f; p.LegL0 = -.15f + MathF.Max(0, c) * .45f; p.LegL1 = -.1f - MathF.Max(0, c) * .35f; p.LegR0 = .15f - MathF.Max(0, -c) * .45f; p.LegR1 = .1f + MathF.Max(0, -c) * .35f; p.ArmL0 = -1.1f + c * .7f; p.ArmL1 = -2.1f + c * .9f; p.ArmR0 = 1.1f + c * .7f; p.ArmR1 = 2.1f + c * .9f; p.Lean = -c * .09f; p.HeadTilt = c * .12f; p.Lift = MathF.Max(0, c2) * 1.2f; p.Mouth = .4f; p.Brow = .6f; break; }
            case Anim.Laugh: { float c = MathF.Abs(M.Sin(s * 9)); p.Lean = -.05f; p.HeadTilt = -.1f; p.HeadY = -c * .8f; p.ArmL0 = -.35f; p.ArmL1 = 1.15f; p.ArmR0 = .35f; p.ArmR1 = -1.15f; p.Mouth = .5f + c * .5f; p.Breath = c * 2; break; }
            case Anim.Point: if (d > 0) { p.ArmR0 = 1.45f; p.ArmR1 = 1.55f; } else { p.ArmL0 = -1.45f; p.ArmL1 = -1.55f; } p.LookX = d; p.Brow = .5f; p.Lean = d * .04f; break;
            case Anim.Cheer: { float c = MathF.Abs(M.Sin(s * 8)); p.ArmL0 = -2.2f - c * .2f; p.ArmL1 = -2.75f - c * .3f; p.ArmR0 = 2.2f + c * .2f; p.ArmR1 = 2.75f + c * .3f; p.OpenL = p.OpenR = 1; p.WristL = c * .2f; p.WristR = -c * .2f; p.Lift = c * 5; p.LegL0 = -.2f - c * .2f; p.LegR0 = .2f + c * .2f; p.LegL1 = .1f; p.LegR1 = -.1f; p.Mouth = .9f; p.Brow = 1; break; }
            case Anim.Think: p.ArmR0 = .35f; p.ArmR1 = -2.35f; p.ArmL0 = -.13f; p.ArmL1 = .1f; p.HeadTilt = .12f; p.LookX = .8f; p.LookY = -.9f; p.Brow = .4f; break;
            case Anim.Shrug: p.ArmL0 = -.55f; p.ArmL1 = -1.85f; p.ArmR0 = .55f; p.ArmR1 = 1.85f; p.OpenL = p.OpenR = 1; p.WristL = -.35f; p.WristR = .35f; p.HeadTilt = .14f; p.Brow = 1; p.HeadY = .6f; break;
            case Anim.Bow: { float k = M.Smooth(0, .4f, s) * (1 - M.Smooth(1.1f, 1.5f, s)); p.Lean = .5f * k; p.HeadTilt = .3f * k; p.ArmL0 = -.4f; p.ArmL1 = .8f * k; p.ArmR0 = .5f; p.ArmR1 = 1.2f * k; break; }
            case Anim.ShowOff: p.ArmL0 = -2.1f; p.ArmL1 = -2.7f; p.ArmR0 = 2.1f; p.ArmR1 = 2.7f; p.OpenL = p.OpenR = 1; p.Mouth = .6f; p.Brow = .9f; p.LookY = -.4f; p.HeadY = -.3f; break;
            case Anim.Admire:
                switch (AdmireSlot)
                {
                    case 0: p.LookY = 1; p.HeadY = .8f; p.ArmL0 = -.4f; p.ArmL1 = -.1f; p.ArmR0 = .4f; p.ArmR1 = .1f; break;                       // look down at the top
                    case 1: p.LookX = .9f; p.LookY = .6f; p.HeadTilt = .08f; p.ArmR0 = .85f; p.ArmR1 = 1.15f; p.ArmL0 = -.2f; break;                  // check a sleeve
                    case 2: p.LookY = 1; p.HeadY = .9f; p.HeadTilt = -.06f; p.LegR0 = .3f; p.LegR1 = .18f; p.ArmL0 = -.3f; p.ArmR0 = .3f; p.RootX = -.6f; break;
                    case 3: p.LookY = 1; p.LookX = .5f; p.HeadY = 1; p.LegR0 = .55f; p.LegR1 = .1f; p.Lean = -.03f; p.ArmL0 = -.3f; p.ArmR0 = .3f; break;
                    case 4: p.ArmR0 = 2.0f; p.ArmR1 = 3.7f; p.ArmL0 = -2.0f; p.ArmL1 = -3.7f; p.LookY = -.9f; p.HeadTilt = M.Sin(s * 5) * .06f; p.Brow = .6f; break; // adjust the hat
                    case 5: p.ArmR0 = .75f; p.ArmR1 = 3.1f; p.HeadTilt = -.1f; p.LookX = -.5f; p.Brow = .5f; break;
                    default: p.ArmL0 = -.3f; p.ArmR0 = .3f; p.HeadTilt = M.Sin(s * 3) * .05f; p.Brow = .5f; break;
                }
                break;
        }
        return p;
    }
    void Idle(ref Pose p, float br)
    {
        switch (IdleStyle)
        {
            case 1: p.ArmL0 = -.62f; p.ArmL1 = .72f; p.ArmR0 = .62f; p.ArmR1 = -.72f; p.LegL0 = -.1f; p.LegR0 = .1f; break;
            case 2: { float c = MathF.Abs(M.Sin(t * 3.2f)); p.LegL0 = -.1f - c * .09f; p.LegL1 = c * .09f; p.LegR0 = .1f + c * .09f; p.LegR1 = -c * .09f; p.ArmL0 = -.2f; p.ArmL1 = -.05f + c * .06f; p.ArmR0 = .2f; p.ArmR1 = .05f - c * .06f; p.Brow = .4f; break; }
            case 3: p.ArmL0 = -.12f; p.ArmL1 = .34f; p.ArmR0 = .12f; p.ArmR1 = -.34f; p.LegL0 = -.02f; p.LegL1 = .03f; p.LegR0 = .02f; p.LegR1 = -.03f; p.HeadTilt = .09f; p.HeadY = .4f; p.LookY += .3f; break;
            case 4: p.ArmL0 = -.24f; p.ArmL1 = -.1f; p.ArmR0 = .24f; p.ArmR1 = .1f; p.LegL0 = -.15f; p.LegL1 = -.1f; p.LegR0 = .15f; p.LegR1 = .1f; p.HeadY = -.4f; break;
            case 5: p.HeadTilt = M.Sin(t * .9f) * .08f; p.HeadY = .6f + br * .25f; break;
            default: { float a = M.Sin(t * 1.05f), b = M.Sin(t * 1.05f - .8f); p.ArmL0 += a * .025f - br * .012f; p.ArmL1 += b * .045f; p.ArmR0 += a * .022f + br * .012f; p.ArmR1 += b * .045f; p.RootX = M.Sin(t * .52f) * .5f; p.HeadTilt += M.Sin(t * .52f + .6f) * .02f; break; } // slack arms drift with the breath
        }
        p.HeadY += br * .12f;
    }
}

using System.Numerics;
namespace UntitledGame;

public enum Anim { Idle, Walk, Run, Jump, Fall, Land, Crouch, CrouchWalk, Slide, Climb, Swim, Tread, Sit, RodHold, Cast, Reel, Wave, Dance, Laugh, Point, Cheer, Think, Sleep, Admire, Ledge, ShowOff, Shrug, Bow, SitGround }

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

    /// How hard the legs are driving right now, 0..1. A foot lands at phase pi/2, 3pi/2...; the rear leg's push peaks just
    /// before that, then speed bleeds off through the landing and the legs passing, until the next push.
    public float Drive
    {
        get { float x = (Phase - (M.PI / 2 - .32f)) / M.PI; x -= MathF.Floor(x); const float ease = .58f; return x < ease ? 1 - M.Smooth(0, ease, x) : M.Smooth(ease, 1, x); }
    }
    /// Starting from a standstill, begin as the legs pass so the first thing you feel is a push.
    public void StartStep() => Phase = MathF.Round(Phase / M.PI) * M.PI - .12f;
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
        turn.Update(Moving ? Dir : State is Anim.RodHold or Anim.Cast or Anim.Reel or Anim.Slide || (State is Anim.Jump or Anim.Fall && v > 12) ? Dir * .9f : 0, 240, 26, dt); if (State is Anim.Climb or Anim.Ledge) turn = new();
        blinkT -= dt; if (blinkT < 0) { blinkT = rng.R(1.8f, 5); blink = .16f; if (rng.P(.15f)) blinkT = .28f; }
        blink = MathF.Max(0, blink - dt); lookT -= dt;
        if (lookT < 0) { lookT = rng.R(1.5f, 4.5f); if (rng.P(.5f)) { lookX = rng.R(-1, 1); lookY = rng.R(-.4f, .6f); } else { lookX = lookY = 0; } }
        acc += dt; float fr = 1 / Fps; if (acc < fr) return false; acc -= fr; if (acc > fr) acc = 0;
        var target = Build(); bool cyc = State is Anim.Walk or Anim.Run or Anim.Swim or Anim.Climb or Anim.Dance or Anim.CrouchWalk;
        Pose = Mix(Pose, target, StateT < .12f ? .6f : cyc ? 1 : .55f);
        Pose.HemX = hemX.X; Pose.HemY = hemY.X; Pose.HairX = hairX.X; Pose.HairY = hairY.X; Pose.SleeveX = slX.X; Pose.SleeveY = slY.X; Pose.T = t;
        Pose.Wind = MathF.Min(1.3f, MathF.Abs(gust) * .55f + v * .0045f + MathF.Abs(Vel.Y) * .0025f);
        Pose.Blink = blink > 0 || State is Anim.Sleep or Anim.Laugh || (State == Anim.Bow && Pose.Bend > .4f) ? 1 : 0; Pose.Turn = M.Clamp(turn.X, -1, 1); Pose.Squash = squash.X;
        return true;
    }
    bool Moving => State is Anim.Walk or Anim.Run or Anim.CrouchWalk or Anim.Swim;

    static Pose Mix(Pose a, Pose b, float k)
    {
        Pose r = b; float L(float x, float y) => x + (y - x) * k;
        r.RootX = L(a.RootX, b.RootX); r.Lift = L(a.Lift, b.Lift); r.Lean = L(a.Lean, b.Lean); r.HeadTilt = L(a.HeadTilt, b.HeadTilt); r.HeadX = L(a.HeadX, b.HeadX); r.HeadY = L(a.HeadY, b.HeadY); r.Breath = L(a.Breath, b.Breath);
        r.ArmL0 = L(a.ArmL0, b.ArmL0); r.ArmL1 = L(a.ArmL1, b.ArmL1); r.ArmR0 = L(a.ArmR0, b.ArmR0); r.ArmR1 = L(a.ArmR1, b.ArmR1); r.LegL0 = L(a.LegL0, b.LegL0); r.LegL1 = L(a.LegL1, b.LegL1); r.LegR0 = L(a.LegR0, b.LegR0); r.LegR1 = L(a.LegR1, b.LegR1); r.FootL = L(a.FootL, b.FootL); r.FootR = L(a.FootR, b.FootR);
        r.LookX = L(a.LookX, b.LookX); r.LookY = L(a.LookY, b.LookY); r.Brow = L(a.Brow, b.Brow); r.Mouth = L(a.Mouth, b.Mouth); r.RootY = L(a.RootY, b.RootY); r.WristL = L(a.WristL, b.WristL); r.WristR = L(a.WristR, b.WristR); r.OpenL = b.OpenL; r.OpenR = b.OpenR; r.ForeL = L(a.ForeL, b.ForeL); r.ForeR = L(a.ForeR, b.ForeR); r.ForeS = L(a.ForeS, b.ForeS); r.Bend = L(a.Bend, b.Bend); r.Seat = L(a.Seat, b.Seat); r.Plant = L(a.Plant, b.Plant); if (b.Plant < .01f) { r.PlantL = a.PlantL; r.PlantR = a.PlantR; } return r;
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
            case Anim.CrouchWalk:
                {
                    // low sneaking steps: knees stay bent, body leans over the front foot
                    float q = M.Sin(Phase), q2 = M.Cos(Phase); float a = .78f + .34f * q, b = .78f - .34f * q; p.LegL0 = d * a; p.LegL1 = d * (a - 1.42f - .34f * MathF.Max(0, q2)); p.LegR0 = d * b; p.LegR1 = d * (b - 1.42f - .34f * MathF.Max(0, -q2)); p.FootL = MathF.Max(0, q2) * .2f; p.FootR = MathF.Max(0, -q2) * .2f;
                    p.Lean = d * .42f; p.HeadTilt = -d * .08f; p.HeadX = d * 1.4f; p.ArmL0 = d * (.02f - .30f * q); p.ArmL1 = d * (.62f - .30f * q); p.ArmR0 = d * (.02f + .30f * q); p.ArmR1 = d * (.62f + .30f * q); p.LookX = d * .8f; break;
                }
            case Anim.Crouch: // a tidy front-on squat: thighs point at the camera, hands rest on the knees
                p.ForeL = p.ForeR = .74f; p.ForeS = .16f; p.LegL0 = -.34f; p.LegL1 = .06f; p.LegR0 = .34f; p.LegR1 = -.06f; p.Bend = .34f; p.ArmL0 = -.34f; p.ArmL1 = .22f; p.ArmR0 = .34f; p.ArmR1 = -.22f; p.LookX = d * .4f; p.HeadY = br * .15f; break;
            case Anim.Slide:
                {
                    // feet-first slide: seat on the ground, one leg long, one knee up, back propped, head up, both hands trailing on the ground
                    float j = M.Sin(t * 31) * .02f; p.Seat = 1; p.RootX = d * -3; p.Lean = -d * (.86f + j); p.HeadTilt = d * .36f; p.HeadX = d * 3.6f; p.HeadY = -1.4f - j * 8; p.LookX = d; p.LookY = -.1f; p.Brow = .6f; p.Mouth = .42f;
                    float la = d * (1.56f + j), lb = d * 1.60f, ka = d * 2.02f, kb = d * (1.06f + j * 2); if (d > 0) { p.LegL0 = la; p.LegL1 = lb; p.FootL = -1.28f; p.LegR0 = ka; p.LegR1 = kb; p.FootR = -.12f; } else { p.LegR0 = la; p.LegR1 = lb; p.FootR = -1.28f; p.LegL0 = ka; p.LegL1 = kb; p.FootL = -.12f; }
                    p.Plant = 1; var back = new Vector2(-d * 48, -2.2f); var fwd = new Vector2(-d * 34, -2.2f); p.PlantL = d > 0 ? back : fwd; p.PlantR = d > 0 ? fwd : back; float w = d * 1.15f; p.WristL = w; p.WristR = w; break;
                }
            case Anim.Jump: case Anim.Fall:
                {
                    // u: -1 rising fast, 0 apex, +1 falling fast. Poses blend through the arc instead of one held shape.
                    float u = M.Clamp(Vel.Y / 330f, -1, 1), rise = M.Clamp(-u * 1.4f), fall = M.Clamp(u * 1.5f), apex = 1 - MathF.Max(rise, fall); float B(float x, float y, float z) => x * rise + y * apex + z * fall;
                    if (MathF.Abs(Vel.X) > 12)
                    {
                        // running leap: lead knee drives up, trail leg pushes off, arms swing opposite
                        p.LegL0 = d * B(1.0f, .82f, .52f); p.LegL1 = d * B(-.18f, -.28f, .16f); p.FootL = B(.1f, .05f, -.18f); p.LegR0 = d * B(-.42f, -.12f, -.16f); p.LegR1 = d * B(-.78f, -1.15f, -.62f); p.FootR = B(.65f, .5f, .3f);
                        p.ArmR0 = d * B(.72f, .5f, .34f); p.ArmR1 = d * B(2.0f, 1.5f, 1.1f); p.ArmL0 = d * B(-.62f, -.55f, -.75f); p.ArmL1 = d * B(.2f, .05f, -.15f); p.Lean = d * B(.13f, .07f, .02f); p.HeadTilt = d * B(.02f, 0, -.03f); p.LookX = d * .7f;
                    }
                    else
                    {
                        // straight up: knees tuck toward the camera at the top, then legs reach for the ground
                        p.ForeL = B(.02f, .34f, .08f); p.ForeR = B(.16f, .42f, .14f); p.LegL0 = B(-.05f, -.16f, -.10f); p.LegL1 = B(-.02f, .02f, -.02f); p.LegR0 = B(.08f, .18f, .12f); p.LegR1 = B(.02f, -.02f, .04f);
                        p.ArmL0 = -B(.26f, .36f, .52f); p.ArmL1 = -B(.06f, .30f, .86f); p.ArmR0 = B(.26f, .36f, .52f); p.ArmR1 = B(.06f, .30f, .86f);
                    }
                    p.HeadY = B(-.5f, -.2f, .3f); p.LookY = B(-.6f, 0, .7f); p.Brow = B(.4f, .3f, .7f); p.Mouth = Vel.Y > 470 ? .6f : p.Mouth; break;
                }
            case Anim.Land: // knees absorb the landing straight down, no splaying
                p.ForeL = p.ForeR = .42f; p.LegL0 = -.12f; p.LegL1 = .02f; p.LegR0 = .12f; p.LegR1 = -.02f; p.Bend = .24f; p.ArmL0 = -.5f; p.ArmL1 = -.3f; p.ArmR0 = .5f; p.ArmR1 = .3f; p.LookY = .4f; break;
            case Anim.Climb:
                {
                    // seen from behind: hand over hand, opposite foot follows, hips sway toward the planted foot
                    float c = M.Sin(Phase), reach = 5.2f; p.Back = true; p.FreeRoot = true; p.RootX = -c * .8f; p.Lean = c * .035f; p.HeadY = -MathF.Abs(c) * .3f; p.HeadTilt = -c * .04f;
                    Reach(-1, new(-9.6f, -48.5f - c * reach), ref p.ArmL0, ref p.ArmL1); Reach(1, new(9.6f, -48.5f + c * reach), ref p.ArmR0, ref p.ArmR1);
                    p.ForeL = .10f + MathF.Max(0, -c) * .46f; p.ForeR = .10f + MathF.Max(0, c) * .46f; p.LegL0 = -.10f - MathF.Max(0, -c) * .22f; p.LegL1 = .03f + MathF.Max(0, -c) * .10f; p.LegR0 = .10f + MathF.Max(0, c) * .22f; p.LegR1 = -.03f - MathF.Max(0, c) * .10f; break;
                }
            case Anim.Ledge:
                { float k = M.Clamp(s / .2f); p.Back = true; p.FreeRoot = true; Reach(-1, new(-11, M.Lerp(-52, -32, k)), ref p.ArmL0, ref p.ArmL1); Reach(1, new(11, M.Lerp(-52, -32, k)), ref p.ArmR0, ref p.ArmR1); p.ForeL = .1f; p.ForeR = .1f + k * .5f; p.LegR0 = .1f + k * .2f; p.LegR1 = -.1f; break; }
            case Anim.Swim:
                {
                    // front crawl: the body lies along the water, arms wheel past each other, feet flutter, face looks ahead
                    float tilt = 1.06f + M.Clamp(Vel.Y / 110f, -.9f, .75f), kick = M.Sin(Phase * 2.2f), kick2 = M.Sin(Phase * 2.2f - .7f); p.FreeRoot = true; p.RootY = -11 + M.Sin(Phase) * .8f + M.Clamp(Vel.Y * .1f, 0, 8); p.Lean = d * (tilt + M.Sin(Phase) * .05f);
                    void Crawl(float ph, ref float up, ref float lo) { float a = ph % M.TAU; if (a < 0) a += M.TAU; float w = a < M.PI ? Ease.InOut(a / M.PI) * M.PI : a; bool over = a > M.PI; float ang = tilt + 1.55f - w; up = d * ang; lo = d * (ang + (over ? -.15f + M.Sin(a - M.PI) * -.85f : .25f + M.Sin(a) * .45f)); }
                    Crawl(Phase, ref p.ArmL0, ref p.ArmL1); Crawl(Phase + M.PI, ref p.ArmR0, ref p.ArmR1);
                    float tr = -(M.PI - tilt) + 1.66f; tr = -(tilt + .06f); p.LegL0 = d * (tr + kick * .20f); p.LegL1 = d * (tr - .10f + kick2 * .30f); p.LegR0 = d * (tr - kick * .20f); p.LegR1 = d * (tr - .10f - kick2 * .30f); p.FootL = p.FootR = .75f; p.HeadTilt = -d * (tilt * .34f); p.HeadX = d * .8f; p.HeadY = -3.4f; p.LookX = d; p.Mouth = M.Sin(Phase) > .75f ? .5f : 0; break;
                }
            case Anim.Tread:
                {
                    // upright float: hands scull side to side just under the surface, legs egg-beat toward the camera
                    float c = M.Sin(Phase), c2 = M.Cos(Phase); p.FreeRoot = true; p.RootY = M.Sin(Phase * .5f) * 1.1f; p.ArmL0 = -.78f + c * .16f; p.ArmL1 = -1.42f - c * .30f; p.ArmR0 = .78f - c * .16f; p.ArmR1 = 1.42f + c * .30f; p.OpenL = p.OpenR = 1; p.WristL = -c * .3f; p.WristR = c * .3f; p.ForeL = .26f + c * .22f; p.ForeR = .26f - c * .22f; p.ForeS = .1f; p.LegL0 = -.22f; p.LegL1 = -.05f + c2 * .16f; p.LegR0 = .22f; p.LegR1 = .05f + c2 * .16f; p.HeadY = -.6f; break;
                }
            case Anim.Sit: // on a seat: knees together, thighs toward the camera, shins straight down
                p.ForeL = p.ForeR = .72f; p.LegL0 = -.10f; p.LegL1 = -.02f; p.LegR0 = .10f; p.LegR1 = .02f; p.ArmL0 = -.22f; p.ArmL1 = .36f; p.ArmR0 = .22f; p.ArmR1 = -.36f; p.HeadY = br * .15f; p.RootX = M.Sin(t * .5f) * .3f; break;
            case Anim.SitGround: // cross-legged on the ground
                p.ForeL = p.ForeR = .30f; p.ForeS = .34f; p.LegL0 = -1.36f; p.LegL1 = 1.42f; p.LegR0 = 1.36f; p.LegR1 = -1.42f; p.ArmL0 = -.50f; p.ArmL1 = .18f; p.ArmR0 = .50f; p.ArmR1 = -.18f; p.HeadY = br * .15f; p.Bend = .05f; break;
            case Anim.Sleep: p.ForeL = p.ForeR = .30f; p.ForeS = .34f; p.LegL0 = -1.36f; p.LegL1 = 1.42f; p.LegR0 = 1.36f; p.LegR1 = -1.42f; p.ArmL0 = -.32f; p.ArmL1 = .42f; p.ArmR0 = .32f; p.ArmR1 = -.42f; p.Bend = .30f + br * .03f; p.HeadTilt = .16f; break;
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
            case Anim.Bow: // folds forward, toward the camera
                { float k = M.Smooth(0, .38f, s) * (1 - M.Smooth(1.35f, 1.8f, s)); p.Bend = k; p.ArmL0 = M.Lerp(-.17f, -.20f, k); p.ArmL1 = M.Lerp(.05f, 1.15f, k); p.ArmR0 = M.Lerp(.15f, .26f, k); p.ArmR1 = M.Lerp(-.08f, .10f, k); p.LookY = k; p.LookX = 0; if (k > .5f) p.Blink = 1; break; }
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
    /// Two-bone reach: bends the elbow outward so the hand lands on a target relative to the pelvis.
    static void Reach(int side, Vector2 target, ref float upper, ref float lower)
    {
        const float U = 16.8f, F = 15.2f; var sh = new Vector2(side * 9.7f, -25); var v = target - sh; float D = M.Clamp(v.Length(), 4, U + F - .3f); float an = MathF.Atan2(v.X, v.Y); if (side < 0 && an > 0) an -= M.TAU; if (side > 0 && an < 0) an += M.TAU;
        float A = MathF.Acos(M.Clamp((U * U + D * D - F * F) / (2 * U * D), -1, 1)); upper = an - side * A; var el = M.Dir(upper, U); var w = Vector2.Normalize(v) * D - el; lower = MathF.Atan2(w.X, w.Y); if (side < 0 && lower > 0) lower -= M.TAU; if (side > 0 && lower < 0) lower += M.TAU;
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

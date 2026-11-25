using System.Numerics;
namespace UnnamedGame;

public struct Pose
{
    public float RootX, Lift, Lean, HeadTilt, HeadX, HeadY, Turn, Breath, Squash, Crouch;
    public float ArmL0, ArmL1, ArmR0, ArmR1;   // L/R = screen side. 0 hangs down, + swings to screen right
    public float LegL0, LegL1, LegR0, LegR1, FootL, FootR;
    public float WristL, WristR, OpenL, OpenR; // wrist turn and how open the hand is
    public float Blink, Mouth, LookX, LookY, Brow;
    public float HemX, HemY, HairX, HairY, SleeveX, SleeveY, T, Wind;
    public bool FreeRoot; public float RootY;  // swimming / climbing: don't auto ground
    public static Pose Stand => new() { ArmL0 = -.17f, ArmL1 = .05f, ArmR0 = .15f, ArmR1 = -.08f, LegL0 = -.05f, LegL1 = -.03f, LegR0 = .09f, LegR1 = .02f, HeadTilt = .025f };
}

/// Body measurements derived from the sliders. Units: an average adult is ~100 tall, y points down, ground is 0.
public sealed class Rig
{
    public float HeadRx, HeadRy, Neck, NeckLen, ShX, Chest, Waist, Hip, Torso = 30, UArm, LArm, ArmR, ForeR, WristR, HandR, Thigh, Shin, ThighR, KneeR, CalfR, AnkleR, AnkleH = 3.4f, Fat, Belly, Total;
    public Rig(AvatarData d)
    {
        float hs = M.Lerp(.87f, 1.12f, d.Height) * (d.Age == 0 ? .95f : d.Age == 3 ? .97f : 1), ws = MathF.Sqrt(hs);
        float w = (d.Weight - .42f) * 2, f = w > 0 ? w : w * .62f, sh = (d.Shoulder - .5f) * 2, wa = (d.Waist - .5f) * 2, hi = (d.Hip - .5f) * 2;
        Fat = f; Belly = MathF.Max(0, w - .25f);
        float hd = M.Lerp(.91f, 1.09f, d.Head) * M.Lerp(1, hs, .35f) * (d.Age == 0 ? 1.05f : 1);
        HeadRx = (7.25f + f * .65f) * hd; HeadRy = (8.95f + f * .12f) * hd;
        Neck = (3.1f + f * .55f + sh * .3f) * ws; NeckLen = 3.4f * hs;
        ShX = (12.7f + sh * 2.1f + f * 2.2f) * ws; Chest = (9.0f + sh * 1.2f + f * 2.3f) * ws; Waist = (7.8f + wa * 1.7f + f * 4.0f + Belly * 1.5f) * ws; Hip = (8.8f + hi * 2.0f + f * 2.9f) * ws;
        Torso = 29 * hs; float ar = M.Lerp(.9f, 1.1f, d.Arm), lg = M.Lerp(.88f, 1.12f, d.Leg);
        UArm = 16.8f * hs * ar; LArm = 15.2f * hs * ar; ArmR = (2.75f + f * .75f) * ws; ForeR = (2.35f + f * .5f) * ws; WristR = (1.75f + f * .25f) * ws; HandR = (2.45f + f * .15f) * ws;
        Thigh = 22f * hs * lg; Shin = 21.5f * hs * lg; ThighR = (4.6f + f * 1.2f + hi * .35f) * ws; KneeR = (3.3f + f * .7f) * ws; CalfR = (3.3f + f * .7f) * ws; AnkleR = (2.15f + f * .35f) * ws;
        Total = AnkleH + Thigh + Shin + Torso + NeckLen + HeadRy * 2;
    }
    public float Half(float up)
    {
        float t = Torso, s = up / t;
        if (s < 0) return M.Lerp(Hip, Hip * .93f, M.Clamp(-s / .25f));
        if (s < .36f) return M.Lerp(Hip, Waist, M.Smooth(0, .36f, s));
        if (s < .72f) return M.Lerp(Waist, Chest, M.Smooth(.36f, .72f, s));
        return M.Lerp(Chest, MathF.Max(Chest, ShX - ArmR * 1.1f), M.Smooth(.74f, .9f, s));
    }
}

public enum Show { All, Mannequin }

public sealed partial class AvatarPainter
{
    Painter p; AvatarData d; Rig r; Pose po; float S; bool fine; Show show; Slot? only;
    Vector2 P; // pelvis
    Vector2 shL, elL, haL, shR, elR, haR, hpL, knL, anL, hpR, knR, anR, HC;
    float cosL, sinL, at;
    int near; // which side (-1 left, +1 right) is drawn last
    Col skin, ink;
    public Vector2 HandL => haL; public Vector2 HandR => haR; public Vector2 HeadC => HC; public Rig Rig => r;
    static readonly AvatarData mannequin = new() { Skin = 0xd9c7ad, Hair = new HairData { Id = Catalog.Hair.Length - 1 } };

    Vector2 Tp(float x, float up)
    {
        float q = x * (1 - .13f * at), b = 1 + po.Breath * .02f * M.Smooth(0, 20, up);
        q *= b; return new(P.X + q * cosL + up * sinL, P.Y - up * cosL + q * sinL);
    }
    Vector2 H(float x, float y) // head space, in radii
    {
        float c = MathF.Cos(po.HeadTilt), s = MathF.Sin(po.HeadTilt); x *= r.HeadRx; y *= r.HeadRy;
        return new(HC.X + x * c - y * s, HC.Y + x * s + y * c);
    }

    void Layout()
    {
        at = MathF.Abs(po.Turn); float lean = po.Lean; cosL = MathF.Cos(lean); sinL = MathF.Sin(lean);
        float hipSep = r.Hip * .52f * (1 - .5f * at), legLen = r.Thigh + r.Shin + r.AnkleH;
        P = new(po.RootX, -legLen);
        void Leg(float sx, float a0, float a1, out Vector2 h, out Vector2 k, out Vector2 a)
        {
            h = P + new Vector2(sx, 1.5f); k = h + M.Dir(a0, r.Thigh); a = k + M.Dir(a1, r.Shin);
        }
        Leg(-hipSep, po.LegL0, po.LegL1, out hpL, out knL, out anL); Leg(hipSep, po.LegR0, po.LegR1, out hpR, out knR, out anR);
        if (!po.FreeRoot)
        {
            float dirn = MathF.Sign(po.Turn);
            float Low(Vector2 a, float f) => a.Y + r.AnkleH + (MathF.Abs(f) > .1f ? MathF.Abs(f) * 2.2f : 0);
            float low = MathF.Max(Low(anL, po.FootL), Low(anR, po.FootR));
            float dy = -low - po.Lift; P.Y += dy; hpL.Y += dy; knL.Y += dy; anL.Y += dy; hpR.Y += dy; knR.Y += dy; anR.Y += dy;
        }
        else { P.Y += po.RootY; hpL.Y += po.RootY; knL.Y += po.RootY; anL.Y += po.RootY; hpR.Y += po.RootY; knR.Y += po.RootY; anR.Y += po.RootY; }
        float sy = r.Torso * .865f, sx2 = (r.ShX - r.ArmR * 1.05f) * (1 - .22f * at);
        shL = Tp(-sx2, sy); shR = Tp(sx2, sy);
        elL = shL + M.Dir(po.ArmL0, r.UArm); haL = elL + M.Dir(po.ArmL1, r.LArm);
        elR = shR + M.Dir(po.ArmR0, r.UArm); haR = elR + M.Dir(po.ArmR1, r.LArm);
        HC = Tp(0, r.Torso + r.NeckLen + r.HeadRy * .80f) + new Vector2(po.HeadX + po.Turn * .9f, po.HeadY + (d.Age == 3 ? .8f : 0));
        near = po.Turn >= 0 ? -1 : 1;
    }

    public void Draw(Painter painter, AvatarData data, in Pose pose, float scale, float ox, float groundY, Show sh = Show.All, Slot? onlySlot = null, int outlinePx = 0)
    {
        p = painter; d = data; po = pose; S = scale; show = sh; only = onlySlot;
        fine = S >= 1.3f; r = new Rig(d); p.View(S, ox, groundY);
        skin = sh == Show.Mannequin ? mannequin.Skin : d.Skin; ink = skin.Ink();
        Layout();
        bool skirt = d[Slot.Bottom].Id >= 60 && Vis(Slot.Bottom);
        var jk = Catalog.Jackets[d[Slot.Jacket].Id]; bool jacket = d[Slot.Jacket].Id > 0 && Vis(Slot.Jacket);
        if (Vis(Slot.Back)) BackBody();
        if (jacket && (jk.Cape || jk.Hem > 8)) JacketBack(jk);
        HairBack();
        int far = -near;
        bool side = at > .3f;
        bool overL = HandHigh(-1), overR = HandHigh(1); // hands raised to the head are drawn last
        behindL = behindR = false;
        if (side) Arm(far, true);
        Legs();
        if (skirt) Skirt(Catalog.Skirts[d[Slot.Bottom].Id - 60]);
        TorsoSkin();
        bool tuck = Vis(Slot.Top) && Tucked;
        if (Vis(Slot.Top) && tuck) Top();
        if (!skirt) BottomsUpper();
        if (Vis(Slot.Top) && !tuck) Top();
        if (!skirt && Vis(Slot.Bottom)) Bib(); else if (skirt && Catalog.Skirts[d[Slot.Bottom].Id - 60].Straps) SkirtStraps();
        if (jacket) Jacket(jk);
        if (Vis(Slot.Back)) BackStraps();
        if (!side) { if (!overL) Arm(-1, true); if (!overR) Arm(1, true); }
        NeckSkinAndHead(() => { if (jacket) JacketCollar(jk); });
        if (Vis(Slot.Neck)) Neckwear();
        FaceAll();
        HairFront();
        if (Vis(Slot.Glasses)) Glasses();
        if (Vis(Slot.Earrings)) Earrings();
        if (Vis(Slot.Hat)) Hat();
        if (Vis(Slot.Neck)) NeckwearFront();
        if (side) Arm(near, true); else { if (overL) Arm(-1, true); if (overR) Arm(1, true); }
        if (outlinePx > 0) p.Outline(d.OutlineCol, outlinePx);
    }
    bool Tucked => Catalog.Tops[d[Slot.Top].Id].Tuck || (Vis(Slot.Bottom) && d[Slot.Bottom].Id < 60 && (d[Slot.Bottom].Id < 30 ? Catalog.Pants[d[Slot.Bottom].Id] : Catalog.Shorts[d[Slot.Bottom].Id - 30]).Bib);
    bool Vis(Slot s)
    {
        if (only == null) return true;
        if (only == s) return true;
        return false;
    }

    // ---------------------------------------------------------------- limbs
    static void Samples(Vector2 a, Vector2 b, Vector2 c, float end, Span<Vector2> o)
    {
        float l0 = Vector2.Distance(a, b), l1 = Vector2.Distance(b, c), L = (l0 + l1) * end; int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float s = L * i / (n - 1);
            // round the joint slightly so knees and elbows bend like cloth rather than hinges
            float bl = MathF.Min(l0, l1) * .42f; // soft, rounded joints
            if (s < l0 - bl) o[i] = Vector2.Lerp(a, b, s / l0);
            else if (s > l0 + bl) o[i] = b + (c - b) / l1 * (s - l0);
            else { float t = (s - l0 + bl) / (2 * bl); var q0 = Vector2.Lerp(a, b, (l0 - bl) / l0); var q1 = Vector2.Lerp(b, c, bl / l1); o[i] = (1 - t) * (1 - t) * q0 + 2 * t * (1 - t) * b + t * t * q1; }
        }
    }
    Func<float, float, (float u, float v)> LimbUv(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 d0 = Vector2.Normalize(b - a), d1 = Vector2.Normalize(c - b); float l0 = Vector2.Distance(a, b);
        return (x, y) =>
        {
            var q = new Vector2(x, y); float s1 = Vector2.Dot(q - b, d1);
            if (s1 > 0) { var e = q - b; return (e.X * -d1.Y + e.Y * d1.X, l0 + s1); }
            var f = q - a; return (f.X * -d0.Y + f.Y * d0.X, Vector2.Dot(f, d0));
        };
    }
    Func<float, float, Col> LimbPat(Piece pc, Vector2 a, Vector2 b, Vector2 c, Col baseA)
    {
        var uv = LimbUv(a, b, c); Col B = pc.B, C = pc.C; int pat = pc.Pat; float s = pc.Scale;
        if (pat == 0) return (x, y) => baseA;
        return (x, y) => { var (u, v) = uv(x, y); return Patterns.Get(pat == 9 ? 0 : pat, u, v, baseA, B, C, s); };
    }
    Func<float, float, Col> TorsoPat(Piece pc, Col baseA)
    {
        Col B = pc.B, C = pc.C; int pat = pc.Pat; float s = pc.Scale; var o = P; float tp = r.Torso;
        if (pat == 0) return (x, y) => baseA;
        return (x, y) => Patterns.Get(pat, x - o.X - (y - o.Y) * -sinL, y - o.Y + tp, baseA, B, C, s);
    }
    static float KnitTone(Knit k, float u, float v) => k switch
    {
        Knit.Rib => M.Sin(u * 2.9f) > .25f ? -.10f : .03f,
        Knit.Cable => MathF.Abs(M.Mod((int)MathF.Floor(u + 40), 6) - 2.5f) < 1.6f ? (M.Sin(v * 1.5f + M.Sin(u * 1.2f) * 2) > 0 ? .10f : -.12f) : (M.Sin(u * 3) > 0 ? -.07f : 0),
        Knit.Fleece => M.Noise(u * 1.3f, v * 1.3f, 9) * .06f,
        Knit.Flannel => M.Noise(u * .8f, v * .8f, 3) * .05f,
        Knit.Denim => (M.Hash((int)MathF.Floor(u * 3), (int)MathF.Floor(v * 3 + u), 2) - .5f) * .10f + M.Noise(u * .3f, v * .3f, 8) * .10f,
        _ => 0
    };
    Ink Cloth(Func<float, float, Col> f, Knit k, Func<float, float, (float, float)> uv, float round = 3.2f)
    {
        var i = new Ink(f, round, .5f); if (k != Knit.Flat && fine) i.Tone = (x, y) => { var (u, v) = uv(x, y); return KnitTone(k, u, v); };
        else if (k == Knit.Denim) i.Grain = .06f; return i;
    }
    (float, float) BodyUv(float x, float y) => (x - P.X, y - P.Y + r.Torso);

    void Limb(Vector2 a, Vector2 b, Vector2 c, float end, Func<float, float> rad, Vector2 flutter, bool flat = true)
    {
        const int N = 12; Span<Vector2> q = stackalloc Vector2[N]; Span<float> rr = stackalloc float[N];
        Samples(a, b, c, end, q);
        for (int i = 0; i < N; i++) { float t = i / (N - 1f); rr[i] = rad(t * end); float f = M.Smooth(.45f, 1, t); q[i] += flutter * f * f; }
        p.Tube(q, rr);
        if (flat)
        {
            var dir = Vector2.Normalize(q[N - 1] - q[N - 2]); var n = new Vector2(-dir.Y, dir.X); float w = rr[N - 1] + 3; var e = q[N - 1] + dir * .4f;
            p.Cut(e + n * w, e + n * w + dir * (w + 2), e - n * w + dir * (w + 2), e - n * w);
        }
    }
    (Vector2 pos, Vector2 dir) LimbAt(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float l0 = Vector2.Distance(a, b), l1 = Vector2.Distance(b, c), s = (l0 + l1) * t;
        return s < l0 ? (Vector2.Lerp(a, b, s / l0), Vector2.Normalize(b - a)) : (b + Vector2.Normalize(c - b) * (s - l0), Vector2.Normalize(c - b));
    }
    float ArmRad(float t) => t < .5f ? M.Lerp(r.ArmR, r.ForeR, t * 2) : M.Lerp(r.ForeR, r.WristR, (t - .5f) * 2);
    float LegRad(float t) => t < .5f ? M.Lerp(r.ThighR, r.KneeR, t * 2) : t < .68f ? M.Lerp(r.KneeR, r.CalfR, (t - .5f) / .18f) : M.Lerp(r.CalfR, r.AnkleR, (t - .68f) / .32f);

    bool behindL, behindR;
    bool HandHigh(int sd) => (sd < 0 ? haL : haR).Y < P.Y - r.Torso * .9f;
    /// Where an arm hangs behind the torso edge, the garment's shoulder flows straight into the sleeve.
    Func<float, float, float> TorsoMerge()
    {
        if (!behindL && !behindR) return null; var L = shL; var R = shR; bool bl = behindL, br = behindR; float a = r.ArmR; float cx = P.X;
        return (x, y) =>
        {
            bool left = x < cx; if (left ? !bl : !br) return 0; var q = left ? L : R; float dx = (x - q.X) * (left ? -1 : 1), dy = y - q.Y;
            return M.Smooth(-a * 1.7f, -a * .7f, dx) * (1 - M.Smooth(a * .1f, a * 1.2f, dy));
        };
    }
    /// Melts the top of an arm or sleeve into the torso so the shoulder reads as one continuous form.
    Func<float, float, float> ShoulderMerge(int sd, Vector2 a, float rad)
    {
        if (at > .3f && sd == -near || (sd < 0 ? behindL : behindR)) return null; // sits behind the body
        float px = a.X, py = a.Y, inner = -sd;
        return (x, y) =>
        {
            float dx = (x - px) * inner, dy = y - py;          // + dx points toward the chest
            float top = 1 - M.Smooth(rad * .35f, rad * 1.5f, dy); // fades out below the armpit
            float side = M.Smooth(-rad * .62f, rad * .1f, dx);   // keeps the outer silhouette line
            if (dy < -rad * .3f) side = MathF.Max(side, M.Smooth(-rad * 1.0f, -rad * .3f, dx));
            return top * side;
        };
    }
    void Arm(int sd, bool withJacket)
    {
        Vector2 a = sd < 0 ? shL : shR, b = sd < 0 ? elL : elR, c = sd < 0 ? haL : haR;
        float dk = at > .3f && sd == -near ? -.12f : 0;
        var sk = new Ink(skin, 2.4f, .5f) { Ao = .8f, Tone = dk != 0 ? (x, y) => dk : null };
        // hand
        var dir = Vector2.Normalize(c - b); { float wr = sd < 0 ? po.WristL : po.WristR; if (wr != 0) { float wc = M.Cos(wr), ws = M.Sin(wr); dir = new(dir.X * wc - dir.Y * ws, dir.X * ws + dir.Y * wc); } }
        var n = new Vector2(-dir.Y, dir.X) * sd; bool open = (sd < 0 ? po.OpenL : po.OpenR) > .5f;
        var pc = d[Slot.Gloves]; int g = Vis(Slot.Gloves) ? pc.Id : 0; bool glove = g is 1 or 2 or 3 or 7;
        var hk = glove ? new Ink((Col)pc.A, 2, .5f) { Ao = .6f } : sk;
        float hr = r.HandR * (g == 2 ? 1.2f : 1);
        p.Begin().Limb2(this, a, b, c); p.Shade(sk with { Merge = ShoulderMerge(sd, a, r.ArmR * 1.1f) });
        if (open && g != 2)
        {
            // open palm: four spread fingers and a thumb that all turn with the wrist
            p.Begin().Capsule(c + dir * hr * .1f, hr * .7f, c + dir * hr * 1.0f, hr * .98f);
            for (int i = 0; i < 4; i++) { float f = (i - 1.5f) * .62f, spread = (i - 1.5f) * .24f, L = i is 1 or 2 ? 1.5f : i == 0 ? 1.15f : 1.3f; var fb = c + dir * hr * 1.35f + n * hr * f * .78f; var fd = dir * M.Cos(spread) + n * M.Sin(spread); p.Capsule(fb, hr * .34f, fb + fd * hr * L, hr * .27f); }
            p.Capsule(c + n * -hr * .55f + dir * hr * .5f, hr * .38f, c - n * hr * 1.45f + dir * hr * 1.15f, hr * .28f); p.Shade(hk);
            if (fine) { Col fl = (glove ? (Col)pc.A : skin).Shade(-.45f); for (int i = 0; i < 3; i++) { float f = (i - 1f) * .62f; var fb = c + dir * hr * 1.5f + n * hr * f * .78f; p.Line(fb, fb + dir * hr * .5f, .28f, fl); } p.Curve(c + dir * hr * .6f - n * hr * .5f, c + dir * hr * .95f, c + dir * hr * .85f + n * hr * .55f, .28f, fl); }
        }
        else
        {
        p.Begin().Capsule(c + dir * hr * .1f, hr * .72f, c + dir * hr * 1.05f, hr * .76f).Capsule(c + dir * hr * 1.05f, hr * .74f, c + dir * hr * 1.95f + n * hr * .25f, hr * .46f);
        p.Capsule(c + n * hr * .35f + dir * hr * .35f, hr * .42f, c + n * hr * .85f + dir * hr * 1.25f, hr * .28f);
        p.Shade(hk);
        }
        if (!open && fine && g != 2) { Col fl = (glove ? (Col)pc.A : skin).Shade(-.5f); for (int i = 0; i < 2; i++) { var t = c + dir * hr * 1.35f - n * hr * (.28f * i - .12f); p.Line(t, t + dir * hr * .8f, .3f, fl); } }
        if (g == 3) { p.Begin().Ellipse(c.X + dir.X * hr * .1f, c.Y + dir.Y * hr * .1f, hr, hr * .9f); p.Shade(new Ink((Col)pc.A, 2, .5f) { Ao = 0 }); }
        if (g is 1 or 2 or 3) { p.Begin(); Limb3(a, b, c, .87f, 1f, .5f); p.Shade(new Ink((Col)pc.B, 1.5f, .5f) { Ao = 0 }); }
        if (g == 7) { p.Begin(); Limb3(a, b, c, .56f, 1f, .45f); p.Shade(new Ink((Col)pc.A, 2, .5f) { Ao = .5f }); }
        // shirt sleeve
        if (Vis(Slot.Top)) Sleeve(Catalog.Tops[d[Slot.Top].Id], sd, a, b, c, dk);
        if (g == 4 && sd > 0) { p.Begin(); Limb3(a, b, c, .86f, .94f, .7f); p.Shade(new Ink((Col)pc.B, 1, .4f) { Ao = 0 }); var w = LimbAt(a, b, c, .9f).pos; p.Begin().Ellipse(w.X, w.Y, 1.5f, 1.5f); p.Shade(new Ink((Col)pc.C, 1, .5f)); }
        if (g == 5) for (int i = 0; i < 3; i++) { p.Begin(); Limb3(a, b, c, .78f + i * .06f, .81f + i * .06f, .6f); p.Shade(new Ink((Col)(i == 0 ? pc.A : i == 1 ? pc.B : pc.C), 1, .4f, .6f) { Ao = 0 }); }
        if (g == 6) { p.Begin(); Limb3(a, b, c, .8f, .93f, .6f); p.Shade(new Ink((Col)pc.A, 1.4f, .5f) { Ao = 0 }); p.Begin(); Limb3(a, b, c, .855f, .875f, .7f); p.Shade(new Ink((Col)pc.B, 1.4f, .5f, 0) { Ao = 0 }); }
        if (g == 8) { p.Begin(); Limb3(a, b, c, .6f, 1f, .9f); var uv = LimbUv(a, b, c); Col A = pc.A, B = pc.B; p.Shade(new Ink((x, y) => ((int)(uv(x, y).v / 2.6f) & 1) == 0 ? A : B, 2, .5f) { Ao = .5f }); }
        if (withJacket && d[Slot.Jacket].Id > 0 && Vis(Slot.Jacket)) { var jk = Catalog.Jackets[d[Slot.Jacket].Id]; if (!jk.Cape) JacketSleeve(jk, sd); }
    }
    internal void Limb2Impl(Vector2 a, Vector2 b, Vector2 c) => Limb(a, b, c, 1, ArmRad, default, false);
    /// Band around an arm between two parameters.
    void Limb3(Vector2 a, Vector2 b, Vector2 c, float t0, float t1, float extra)
    {
        var (q0, d0) = LimbAt(a, b, c, t0); var (q1, d1) = LimbAt(a, b, c, t1); float r0 = ArmRad(t0) + extra, r1 = ArmRad(t1) + extra;
        var n0 = new Vector2(-d0.Y, d0.X); var n1 = new Vector2(-d1.Y, d1.X);
        p.Poly(q0 + n0 * r0, q1 + n1 * r1, q1 - n1 * r1, q0 - n0 * r0);
        if (t0 < .5f && t1 > .5f) p.Ellipse(b.X, b.Y, ArmRad(.5f) + extra, ArmRad(.5f) + extra);
    }

    void Sleeve(TopDef t, int sd, Vector2 a, Vector2 b, Vector2 c, float dk)
    {
        if (t.Sleeve <= 0) return;
        var pc = d[Slot.Top]; Col ca = t.Raglan || t.ContrastSleeve ? pc.B : pc.A;
        float end = t.Sleeve * (t.Sleeve >= 1 ? .99f : 1), loose = .4f + t.Fit * .42f;
        var fl = new Vector2(po.SleeveX, po.SleeveY) * (t.Flare ? 1.5f : .75f) * (.35f + t.Fit * .5f) + new Vector2(M.Sin(po.T * 7 + sd) * po.Wind * .45f, 0);
        float off = t.Neck == Neckline.OffShoulder ? .15f : 0;
        p.Begin();
        Limb(a, b, c, end, s =>
        {
            float q = s / end, rad = (ArmRad(s) + loose) * M.Lerp(.95f, 1, M.Smooth(0, .16f, s));
            if (t.Flare) rad += M.Smooth(.3f, 1, q) * 2.2f;
            if (t.Puff) rad += M.Sin(MathF.Pow(q, .7f) * M.PI) * 1.9f;
            if (t.RibCuff && t.Sleeve > .9f) rad += M.Smooth(.6f, .86f, q) * .9f - M.Smooth(.88f, .93f, q) * (1.3f + t.Fit * .35f);
            else if (t.Sleeve < .6f) rad += q * .55f;
            return rad;
        }, fl);
        if (off > 0) { var tp = Tp(0, r.Torso * .83f); p.Cut(new(tp.X - 40, tp.Y - 30), new(tp.X + 40, tp.Y - 30), new(tp.X + 40, tp.Y), new(tp.X - 40, tp.Y)); }
        var uv = LimbUv(a, b, c); float total = r.UArm + r.LArm; Col B = pc.B, C = pc.C; var f = LimbPat(pc, a, b, c, ca); bool rc = t.RibCuff, stripe = t.G == Graphic.Stripe, fair = t.G == Graphic.Fair, two = t.G == Graphic.TwoStripe; bool flan = t.Knit == Knit.Flannel;
        Func<float, float, Col> fn = (x, y) =>
        {
            var (u, v) = uv(x, y); float s = v / total / end;
            if (rc && s > (end > .9f ? .9f : .82f)) return end > .9f ? (t.Knit == Knit.Fleece || t.G == Graphic.TwoStripe ? (Col)(t.G == Graphic.TwoStripe ? C : pc.A) : C) : C;
            if (stripe) return ((int)MathF.Floor(v / 2.6f) & 1) == 0 ? ca : B;
            if (flan && pc.Pat == 0) return Patterns.Get(4, u, v, ca, B, C);
            if (two && s > .76f && s < .86f) return B;
            return f(x, y);
        };
        var k = Cloth(fn, t.Knit, uv, 2.8f); if (dk != 0) { var o = k.Tone; k.Tone = o == null ? (x, y) => dk : (x, y) => o(x, y) + dk; }
        k.Merge = ShoulderMerge(sd, a, r.ArmR + loose); p.Shade(k);
        if (fine) { float rr = r.ArmR + loose; p.Curve(a + new Vector2(-sd * rr * .35f, -rr * .75f), a + new Vector2(-sd * rr * .75f, rr * .1f), a + new Vector2(-sd * rr * .8f, rr * .95f), .4f, default, true, 1, .88f); }
        // cuff edge + elbow creases
        float tc = end * (rc ? (end > .9f ? .9f : .82f) : .93f); var (cp, cd) = LimbAt(a, b, c, tc); var cn = new Vector2(-cd.Y, cd.X) * 6; cp += fl * M.Smooth(.45f, 1, tc / end);
        p.Crease(cp + cn, cp - cn, .4f, .72f);
        if (fine && end > .5f)
        {
            float bend = po.ArmL1 - po.ArmL0; if (sd > 0) bend = po.ArmR1 - po.ArmR0;
            var bn = Vector2.Normalize(c - a); var pn = new Vector2(-bn.Y, bn.X);
            for (int i = -1; i <= 1; i++) p.Crease(b + pn * 1f + bn * i * 1.7f, b - pn * 3f + bn * (i * 1.7f + .8f), .4f, .8f);
            p.Crease(a + new Vector2(sd * -2, 2), a + new Vector2(sd * 1.5f, 5), .4f, .82f);
        }
        if (t.Buttons && end > .9f) { var (bp, _) = LimbAt(a, b, c, .95f); p.Dot(bp.X, bp.Y, C); }
    }

    // ---------------------------------------------------------------- torso
    void TorsoSkin()
    {
        Span<Vector2> q = stackalloc Vector2[26]; int n = 0;
        for (int i = 0; i <= 8; i++) { float up = M.Lerp(-3, r.Torso * .88f, i / 8f); q[n++] = Tp(-r.Half(up) + .3f, up); }
        q[n++] = Tp(-r.Neck, r.Torso + 1); q[n++] = Tp(-r.Neck, r.Torso + r.NeckLen); q[n++] = Tp(r.Neck, r.Torso + r.NeckLen); q[n++] = Tp(r.Neck, r.Torso + 1);
        for (int i = 8; i >= 0; i--) { float up = M.Lerp(-3, r.Torso * .88f, i / 8f); q[n++] = Tp(r.Half(up) - .3f, up); }
        p.Begin().Poly(q[..n]); var k = new Ink(skin, 4, .5f) { Ao = 0, Merge = TorsoMerge() }; p.Shade(k);
        // collarbones & chin shadow
        var t = r.Torso;
        if (fine) { p.Curve(Tp(-6.5f, t - 2.2f), Tp(-3, t - 2.6f), Tp(-1, t - 3.4f), .4f, skin.Shade(-.35f)); p.Curve(Tp(6.5f, t - 2.2f), Tp(3, t - 2.6f), Tp(1, t - 3.4f), .4f, skin.Shade(-.35f)); }
        // plain undershirt so that skin only ever shows where a garment intends it to
        if (only != null && only != Slot.Top && show == Show.Mannequin) return;
    }

    Vector2 HemOff(float i, float amt) => new((po.HemX + M.Sin(po.T * 6.5f + i * 1.7f) * po.Wind * .55f) * amt, (po.HemY - MathF.Abs(po.HemX) * .25f + M.Cos(po.T * 5.3f + i * 2.1f) * po.Wind * .25f) * amt);

    float ClothHalf(float up, float fit, float hemUp, bool flare)
    {
        float b = r.Half(up), straight = M.Lerp(r.Hip, r.Chest, M.Clamp(up / (r.Torso * .72f)));
        float k = M.Clamp(fit * .62f); b = MathF.Max(b, M.Lerp(b, MathF.Max(straight, b), k)) + .35f + fit * .6f;
        if (flare) b += M.Smooth(r.Torso * .5f, hemUp, up) * 2.6f;
        return b;
    }
    /// Builds a torso garment outline into the mask. Returns the neckline centre bottom.
    void TorsoShape(float fit, float hem, Neckline neck, bool flare, bool sleeveless, float ribHem, float flutter, float split = -1, int sideOnly = 0)
    {
        float T = r.Torso, hu = -hem; var L = new List<Vector2>(); var R = new List<Vector2>();
        int N = 9; float top = T * (neck == Neckline.OffShoulder ? .80f : sleeveless ? .86f : .86f);
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N, up = M.Lerp(hu, top, t), w = ClothHalf(up, fit, hu, flare);
            if (ribHem > 0 && t < .12f) w -= ribHem;
            if (sleeveless && t > .85f) w = MathF.Min(w, r.ShX - r.ArmR * .7f);
            var f = (1 - t) * (1 - t);
            L.Add(Tp(-w, up) + HemOff(0, f * flutter)); R.Add(Tp(w, up) + HemOff(3, f * flutter));
        }
        float nk = r.Neck + .2f, sl = r.ShX - r.ArmR * .75f + fit * .15f;
        void Sh(List<Vector2> o, int s)
        {
            if (neck == Neckline.OffShoulder) { o.Add(Tp(s * (r.ShX + .6f), T * .84f)); return; }
            if (neck == Neckline.Tank) { o.Add(Tp(s * (sl - 4.2f), T * .93f)); o.Add(Tp(s * (nk + 2.6f), T + .4f)); return; }
            if (sleeveless) { o.Add(Tp(s * (sl - 1.6f), T * .95f)); o.Add(Tp(s * (nk + 1), T + .9f)); return; }
            o.Add(Tp(s * sl, T * .93f + fit * .35f)); o.Add(Tp(s * (sl - 1.6f), T * .965f + fit * .3f)); o.Add(Tp(s * (sl - 4.4f), T * .998f + fit * .12f)); o.Add(Tp(s * (nk + .5f), T + 1.1f));
        }
        Sh(L, -1); Sh(R, 1);
        // neckline
        var mid = new List<Vector2>();
        float depth = neck switch { Neckline.V or Neckline.Wrap => 9, Neckline.Scoop => 6, Neckline.Tank => 7, Neckline.Boat => 1.1f, Neckline.Collar or Neckline.Polo => 4.5f, Neckline.Sailor => 8, Neckline.Hood => 3, Neckline.Henley => 2.6f, Neckline.Turtle or Neckline.Mock or Neckline.Cowl => -2, _ => 2.2f };
        if (neck == Neckline.OffShoulder) { for (int i = 1; i < 6; i++) { float t = i / 6f; mid.Add(Tp(M.Lerp(-r.ShX, r.ShX, t), T * .84f - M.Sin(t * M.PI) * 2.2f)); } }
        else if (neck is Neckline.V or Neckline.Wrap or Neckline.Collar or Neckline.Polo or Neckline.Sailor) mid.Add(Tp(po.Turn * 1.2f, T - depth));
        else for (int i = 1; i < 6; i++) { float t = i / 6f; mid.Add(Tp(M.Lerp(-nk, nk, t), T + .9f - M.Sin(t * M.PI) * (depth + .9f))); }
        // hem
        var bot = new List<Vector2>(); int hn = 8; float hw = ClothHalf(hu, fit, hu, flare) - ribHem;
        for (int i = hn - 1; i > 0; i--) { float t = i / (float)hn; bot.Add(Tp(M.Lerp(-hw, hw, t), hu - M.Sin(t * M.PI) * .9f) + HemOff(t * 3, flutter)); }
        var all = new List<Vector2>(L); all.AddRange(mid); R.Reverse(); all.AddRange(R); all.AddRange(bot);
        p.Poly(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(all), 1);
    }

    void Folds(float fit, float hem, int seed)
    {
        float T = r.Torso, w = r.Chest;
        p.Crease(Tp(-w + .5f, T * .72f), Tp(-w + 4.5f, T * .62f), .45f, .82f); p.Crease(Tp(w - .5f, T * .72f), Tp(w - 4.5f, T * .62f), .45f, .82f);
        if (!fine) return;
        p.Crease(Tp(-w - .2f, T * .66f), Tp(-w + 3.2f, T * .52f), .4f, .86f); p.Crease(Tp(w + .2f, T * .66f), Tp(w - 3.2f, T * .52f), .4f, .86f);
        int n = 2 + (int)(fit * 1.5f); float wa = ClothHalf(T * .3f, fit, -hem, false);
        for (int i = 0; i < n; i++)
        {
            float u = T * (.22f + .1f * i) - M.Hash(seed, i, 3) * 2, l = 2.5f + M.Hash(seed, i, 4) * 3;
            p.Crease(Tp(-wa, u), Tp(-wa + l, u + 1.2f), .4f, .85f); p.Crease(Tp(wa, u + 1), Tp(wa - l, u + 1.8f), .4f, .85f);
        }
        for (int i = 0; i < 4; i++) { float x = M.Lerp(-.7f, .7f, i / 3f) * r.Hip + M.Hash(seed, i, 9) * 1.5f; p.Crease(Tp(x, -hem + .6f), Tp(x * .92f + .4f, -hem + 4 + M.Hash(seed, i, 6) * 4), .4f, .87f); }
    }

    void Top()
    {
        var t = Catalog.Tops[d[Slot.Top].Id]; var pc = d[Slot.Top]; float T = r.Torso; Col A = pc.A, B = pc.B, C = pc.C;
        bool tk = Tucked; float hem = tk ? 1 : t.Hem; bool sl = t.Sleeve <= 0; float rib = t.RibCuff && hem > 0 && t.Knit != Knit.Flat ? .7f : 0;
        p.Begin(); TorsoShape(t.Fit, hem, t.Neck, t.Flare, sl, rib, tk ? 0 : .5f + t.Fit * .45f);
        var f = TorsoPat(pc, A); Vector2 o = P; float hipY = P.Y; float cx = po.Turn * 1.3f;
        bool stripe = t.G == Graphic.Stripe, flan = t.Knit == Knit.Flannel, rag = t.Raglan, yoke = t.G == Graphic.Yoke, fair = t.G == Graphic.Fair, two = t.G == Graphic.TwoStripe, sail = t.G == Graphic.Wave;
        float hy = P.Y + hem; float ribH = rib > 0 ? 2.8f : 0; bool ribC = t.Knit is Knit.Cable or Knit.Rib && t.G != Graphic.TwoStripe;
        Func<float, float, Col> fn = (x, y) =>
        {
            float u = x - o.X, v = y - o.Y + T;
            if (ribH > 0 && y > hy - ribH) return two ? C : ribC ? C : A;
            if (stripe) return v > 5 && ((int)MathF.Floor(v / 2.6f) & 1) == 0 ? B : A;
            if (flan && pc.Pat == 0) return Patterns.Get(4, u, v, A, B, C);
            if (rag && MathF.Abs(u) > 4.2f + (v) * .62f && v < 12) return B;
            if (yoke && v < 6.5f + MathF.Abs(MathF.Abs(u) - 5) * -.55f + 2) return B;
            if (two && y > hy - 6.2f && y < hy - 4.4f) return B;
            if (fair && v > 3 && v < 11) { int gx = M.Mod((int)MathF.Floor(u / .9f + 60), 6), gy = (int)((v - 3) / 1.15f); return gy is 0 or 6 ? B : gy == 3 ? ((gx & 1) == 0 ? C : A) : Math.Abs(gx - 3) + Math.Abs(gy - 3) == 3 ? B : Math.Abs(gx - 3) + Math.Abs(gy - 3) < 2 ? C : A; }
            if (sail && v > 13 + M.Sin(u * .6f) * 1.6f) return v > 17.5f + M.Sin(u * .6f + 1) * 1.6f ? C : B;
            return f(x, y);
        };
        var k = Cloth(fn, t.Knit, BodyUv, 4.5f); k.Ao = 1.4f; if (t.Sleeve > 0) k.Merge = TorsoMerge(); p.Shade(k);
        Folds(t.Fit, hem, d[Slot.Top].Id);
        if (ribH > 0) { float w = r.Hip + 4; p.Crease(Tp(-w, -hem + ribH), Tp(w, -hem + ribH), .4f, .75f); if (fine) for (float x = -w; x < w; x += 1.3f) p.Crease(Tp(x, -hem), Tp(x, -hem + ribH), .3f, .88f); }
        else if (fine) { float w = r.Hip + 5; p.Crease(Tp(-w, -hem + 1.2f), Tp(w, -hem + 1.2f), .3f, .85f); }
        if (r.Belly > .1f) p.Curve(Tp(-r.Waist * .7f, T * .16f), Tp(0, T * .06f), Tp(r.Waist * .7f, T * .16f), .5f, default, true, 1, .88f);
        // graphics
        Vector2 gc = Tp(cx, T * .62f);
        switch (t.G)
        {
            case Graphic.Star: Star(gc, 4.2f, B, C); break;
            case Graphic.Heart: Heart(gc, 3.6f, B); break;
            case Graphic.Fish: FishIcon(gc, 5f, B, C); break;
            case Graphic.Letter: p.Begin().Ellipse(gc.X, gc.Y, 4, 4).CutEllipse(gc.X, gc.Y, 2.3f, 2.3f).Cut(gc + new Vector2(-2.3f, -5), gc + new Vector2(2.3f, -5), gc + new Vector2(2.3f, 0), gc + new Vector2(-2.3f, 0)); p.Poly(gc + new Vector2(-4, -4), gc + new Vector2(-2.3f, -4), gc + new Vector2(-2.3f, 0), gc + new Vector2(-4, 0)); p.Poly(gc + new Vector2(2.3f, -4), gc + new Vector2(4, -4), gc + new Vector2(4, 0), gc + new Vector2(2.3f, 0)); p.Shade(new Ink(B, 1, .3f, fine ? .7f : 0) { Ao = 0 }); break;
            case Graphic.Mountain: p.Begin().Poly(gc + new Vector2(-6.5f, 3.5f), gc + new Vector2(-2, -3.6f), gc + new Vector2(.2f, -.5f), gc + new Vector2(2.2f, -2.6f), gc + new Vector2(6.5f, 3.5f)); p.Shade(new Ink(B, 1, .3f, fine ? .8f : 0) { Ao = 0 }); p.Begin().Poly(gc + new Vector2(-3.2f, -1.7f), gc + new Vector2(-2, -3.6f), gc + new Vector2(-.7f, -1.7f), gc + new Vector2(-2, -1)); p.Flat(C); p.Begin().Ellipse(gc.X + 4, gc.Y - 4.4f, 1.5f, 1.5f); p.Flat(C); break;
            case Graphic.Leaf: p.Begin().Blob(gc + new Vector2(0, -4), gc + new Vector2(3, -1), gc + new Vector2(0, 4), gc + new Vector2(-3, -1)); p.Shade(new Ink(B, 1.5f, .4f, .8f) { Ao = 0 }); p.Line(gc + new Vector2(0, -3), gc + new Vector2(0, 4), .4f, C); break;
            case Graphic.Mushroom: p.Begin().Rect(gc.X - 1.3f, gc.Y - .5f, 2.6f, 4.2f); p.Shade(new Ink(C, 1, .4f, .8f) { Ao = 0 }); p.Begin().Ellipse(gc.X, gc.Y, 4.4f, 3.8f).Cut(gc + new Vector2(-6, .3f), gc + new Vector2(6, .3f), gc + new Vector2(6, 6), gc + new Vector2(-6, 6)); p.Shade(new Ink(B, 1.6f, .5f, .8f) { Ao = 0 }); p.Begin().Ellipse(gc.X - 1.6f, gc.Y - 1.7f, .8f, .8f).Ellipse(gc.X + 1.6f, gc.Y - 1.1f, .7f, .7f).Ellipse(gc.X + .4f, gc.Y - 2.8f, .6f, .6f); p.Flat(C); break;
            case Graphic.Pocket when t.Neck != Neckline.Hood: { var q = Tp(4.2f + cx, T * .68f); p.Begin().Poly(q + new Vector2(-2.2f, -2.2f), q + new Vector2(2.2f, -2.2f), q + new Vector2(2.2f, 1.6f), q + new Vector2(0, 2.6f), q + new Vector2(-2.2f, 1.6f)); p.Shade(new Ink(B, 1, .3f) { Ao = 0 }); break; }
            case Graphic.Ruffle: for (int i = 0; i < 4; i++) { var q = Tp(cx, T * (.82f - i * .13f)); p.Begin().Blob(q + new Vector2(-3.4f, -1), q + new Vector2(0, -1.6f), q + new Vector2(3.4f, -1), q + new Vector2(2.2f, 1.3f), q + new Vector2(.6f, .5f), q + new Vector2(-1, 1.4f), q + new Vector2(-2.4f, .7f)); p.Shade(new Ink(B, 1.2f, .4f, .9f) { Ao = .4f }); } break;
        }
        // kangaroo pocket
        if (t.Neck == Neckline.Hood) { float py = T * .12f; p.Begin().Poly(Tp(-5.5f + cx, py + 5.4f), Tp(5.5f + cx, py + 5.4f), Tp(7.8f + cx, py), Tp(7f + cx, py - 2.6f), Tp(-7f + cx, py - 2.6f), Tp(-7.8f + cx, py)); p.Shade(new Ink(fn, 1.4f, .35f, .75f) { Ao = 0 }); }
        // placket
        if (t.Buttons || t.Neck is Neckline.Henley or Neckline.Polo)
        {
            float y0 = T - (t.Neck == Neckline.Henley ? 2.6f : 4.5f), y1 = t.Buttons ? -hem : T * .62f;
            p.Begin(); TorsoShape(t.Fit, hem, t.Neck, t.Flare, sl, rib, .5f);
            if (fine) { p.Crease(Tp(cx - 1.1f, y0), Tp(cx - 1.1f, y1), .3f, .8f); p.Crease(Tp(cx + 1.1f, y0), Tp(cx + 1.1f, y1), .3f, .8f); } else p.Crease(Tp(cx + .9f, y0), Tp(cx + .9f, y1), .3f, .78f);
            for (float y = y0 - 1.6f; y > y1 + .8f; y -= t.Buttons ? 4.2f : 2.4f) { var b = Tp(cx, y); if (fine) { p.Begin().Ellipse(b.X, b.Y, .6f, .6f); p.Shade(new Ink(C, .5f, .4f, .9f) { Ao = 0 }); } else p.Dot(b.X, b.Y, C); }
        }
        NeckTrim(t, pc, fn, cx);
    }

    void NeckTrim(TopDef t, Piece pc, Func<float, float, Col> fn, float cx)
    {
        float T = r.Torso, nk = r.Neck + .4f; Col A = pc.A, B = pc.B, C = pc.C;
        Col trim = t.Knit is Knit.Fleece ? A : C;
        switch (t.Neck)
        {
            case Neckline.Crew: case Neckline.Boat: case Neckline.Scoop: case Neckline.Tank: case Neckline.Henley:
                { float dp = t.Neck switch { Neckline.Scoop => 6, Neckline.Tank => 7, Neckline.Boat => 1.1f, _ => 2.3f }; Span<Vector2> q = stackalloc Vector2[9]; for (int i = 0; i < 9; i++) { float s = i / 8f; q[i] = Tp(M.Lerp(-nk - .6f, nk + .6f, s), T + .8f - M.Sin(s * M.PI) * (dp + .7f)); } p.Stroke(q, fine ? 1.5f : 1.1f, trim.Ink(), 1, false); p.Stroke(q, fine ? .9f : .6f, trim, 1, false); break; }
            case Neckline.V: p.Stroke([Tp(-nk - .4f, T + .8f), Tp(cx, T - 9), Tp(nk + .4f, T + .8f)], 1.5f, trim.Ink(), 1, false); p.Stroke([Tp(-nk - .4f, T + .8f), Tp(cx, T - 9), Tp(nk + .4f, T + .8f)], .9f, trim, 1, false); break;
            case Neckline.Wrap: p.Stroke([Tp(-nk - .4f, T + .8f), Tp(cx, T - 9), Tp(r.Waist * .8f, T * .2f)], .9f, C, 1, false); p.Stroke([Tp(nk + .4f, T + .8f), Tp(cx, T - 9)], .9f, C, 1, false); break;
            case Neckline.Turtle: case Neckline.Mock: case Neckline.Cowl:
                { float h = t.Neck == Neckline.Mock ? 1.2f : t.Neck == Neckline.Cowl ? 2.2f : 2.8f, w = nk + (t.Neck == Neckline.Cowl ? 2.6f : .5f), lo = t.Neck == Neckline.Cowl ? -4.2f : -1.6f; p.Begin().Blob(Tp(-w, T + h), Tp(0, T + h - .6f), Tp(w, T + h), Tp(w + .6f, T + lo * .4f), Tp(0, T + lo), Tp(-w - .6f, T + lo * .4f)); p.Shade(new Ink(t.Knit == Knit.Rib && pc.Pat == 0 ? A : t.Neck == Neckline.Mock ? C : A, 2, .5f) { Ao = .8f }); if (t.Neck != Neckline.Mock) { p.Curve(Tp(-w, T + h * .3f), Tp(0, T + lo * .5f), Tp(w, T + h * .3f), .4f, default, true, 1, .76f); if (fine) for (float x = -w; x < w; x += 1.2f) p.Crease(Tp(x, T + h), Tp(x, T + lo), .25f, .9f); } break; }
            case Neckline.Collar: case Neckline.Polo:
                { Col cc = t.Neck == Neckline.Polo ? C : pc.Pat == 0 && t.Knit != Knit.Flannel ? A : B; for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(Tp(s * (nk - .2f), T + 2), Tp(s * (nk + 1.2f), T + .6f), Tp(s * (nk + 1.6f) + cx * .4f, T - 4.2f), Tp(s * .8f + cx, T - 3.6f)); p.Shade(new Ink(cc, 1, .4f) { Ao = .7f }); } break; }
            case Neckline.Sailor:
                { for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(Tp(s * (nk - .2f), T + 1.6f), Tp(s * (nk + 5.2f), T - .4f), Tp(s * (nk + 4.6f), T - 4.6f), Tp(cx, T - 10)); p.Shade(new Ink(B, 1.2f, .4f) { Ao = .7f }); p.Line(Tp(s * (nk + 4.2f), T - 1), Tp(s * (nk + 3.6f), T - 4.3f), .4f, C); p.Line(Tp(s * (nk + 3.6f), T - 4.3f), Tp(cx + s * .5f, T - 9.2f), .4f, C); } var q = Tp(cx, T - 10.4f); p.Begin().Poly(q, q + new Vector2(-3, 4.5f), q + new Vector2(-.6f, 4), q + new Vector2(0, 1), q + new Vector2(.6f, 4), q + new Vector2(3, 4.5f)); p.Shade(new Ink(C, 1, .4f) { Ao = 0 }); p.Begin().Ellipse(q.X, q.Y, 1.1f, 1); p.Shade(new Ink(C, 1, .4f)); break; }
            case Neckline.Hood:
                HoodCollar(A, B, C, fn); break;
            case Neckline.OffShoulder: { Span<Vector2> q = stackalloc Vector2[9]; for (int i = 0; i < 9; i++) { float s = i / 8f; q[i] = Tp(M.Lerp(-r.ShX - .6f, r.ShX + .6f, s), T * .83f - M.Sin(s * M.PI) * 2.2f); } p.Stroke(q, 2.2f, C.Ink(), 1, false); p.Stroke(q, 1.5f, C, 1, false); break; }
        }
    }
    void HoodCollar(Col A, Col B, Col C, Func<float, float, Col> fn)
    {
        float T = r.Torso, nk = r.Neck;
        p.Begin().Blob(Tp(-nk - 1, T + 3.4f), Tp(-nk - 4.6f, T + 1.2f), Tp(-nk - 3.2f, T - 2.4f), Tp(0, T - 4.6f), Tp(nk + 3.2f, T - 2.4f), Tp(nk + 4.6f, T + 1.2f), Tp(nk + 1, T + 3.4f), Tp(nk + .6f, T + .6f), Tp(0, T - 2.2f), Tp(-nk - .6f, T + .6f));
        p.Shade(new Ink(fn, 2, .5f) { Ao = 1 });
        for (int s = -1; s <= 1; s += 2) { var a = Tp(s * 2f, T - 3.2f); var e = Tp(s * 2.3f, T - 9) + new Vector2(po.HemX * .35f, 0); p.Line(a, e, .6f, C, false); if (fine) { p.Begin().Ellipse(e.X, e.Y, .6f, .9f); p.Flat(C.Shade(.3f)); } }
    }

    void Star(Vector2 c, float rad, Col a, Col b)
    {
        Span<Vector2> q = stackalloc Vector2[10]; for (int i = 0; i < 10; i++) { float an = i * M.PI / 5, rr = (i & 1) == 0 ? rad : rad * .45f; q[i] = c + new Vector2(M.Sin(an) * rr, -M.Cos(an) * rr); }
        p.Begin().Poly(q); p.Shade(new Ink(a, 1.2f, .4f, fine ? .8f : 0) { Ao = 0 });
    }
    void Heart(Vector2 c, float s, Col a)
    {
        p.Begin().Ellipse(c.X - s * .5f, c.Y - s * .3f, s * .58f, s * .58f).Ellipse(c.X + s * .5f, c.Y - s * .3f, s * .58f, s * .58f).Poly(c + new Vector2(-s, -.1f * s), c + new Vector2(s, -.1f * s), c + new Vector2(0, s * 1.1f)); p.Shade(new Ink(a, 1, .4f, fine ? .8f : 0) { Ao = 0 });
    }
    void FishIcon(Vector2 c, float s, Col a, Col b)
    {
        p.Begin().Ellipse(c.X - s * .1f, c.Y, s * .7f, s * .42f).Poly(c + new Vector2(s * .4f, 0), c + new Vector2(s, -s * .45f), c + new Vector2(s * .85f, 0), c + new Vector2(s, s * .45f)); p.Shade(new Ink(a, 1.3f, .4f, fine ? .8f : 0) { Ao = 0 }); p.Dot(c.X - s * .5f, c.Y - s * .08f, b);
    }

    // ---------------------------------------------------------------- legs & bottoms
    void Legs()
    {
        var pc = d[Slot.Bottom]; bool vis = Vis(Slot.Bottom); int far = -near; bool side = at > .3f;
        bool skirt = pc.Id >= 60; PantsDef pd = skirt ? null : pc.Id < 30 ? Catalog.Pants[pc.Id] : Catalog.Shorts[pc.Id - 30];
        for (int i = 0; i < 2; i++)
        {
            int sd = i == 0 ? far : near; Vector2 a = sd < 0 ? hpL : hpR, b = sd < 0 ? knL : knR, c = sd < 0 ? anL : anR; float foot = sd < 0 ? po.FootL : po.FootR;
            float dk = side && sd == far ? -.16f : 0;
            p.Begin(); Limb(a, b, c, 1, LegRad, default, false); p.Shade(new Ink(skin, 3, .5f) { Ao = .6f, Tone = dk != 0 ? (x, y) => dk : null });
            if (fine && (skirt || pd.Len < .6f)) { p.Curve(b + new Vector2(-1.6f, .6f), b + new Vector2(0, 1.8f), b + new Vector2(1.6f, .6f), .35f, skin.Shade(-.35f)); }
            if (skirt && vis) { p.Begin(); Limb(a, b, c, .28f, s => LegRad(s) + .3f, default); p.Shade(new Ink(((Col)pc.B).Shade(-.3f), 2, .4f) { Ao = .4f }); }
            if (Vis(Slot.Shoes)) Shoe(sd, b, c, foot, dk, true);
            else BareFoot(sd, b, c, foot, dk);
            if (!skirt && vis && !(side == false && i == 0 && false)) PantLeg(pd, pc, sd, a, b, c, dk);
            if (Vis(Slot.Shoes)) Shoe(sd, b, c, foot, dk, false);
        }
    }
    void BareFoot(int sd, Vector2 b, Vector2 c, float foot, float dk) { FootShape(sd, c, foot, 0, out _, 0, .85f); p.Shade(new Ink(skin, 1.6f, .5f) { Tone = dk != 0 ? (x, y) => dk : null }); }

    void PantLeg(PantsDef t, Piece pc, int sd, Vector2 a, Vector2 b, Vector2 c, float dk)
    {
        var fl = new Vector2(po.HemX * .16f + M.Sin(po.T * 6 + sd * 2) * po.Wind * .3f, 0) * M.Clamp(t.Hem, 0, 3) * .55f;
        float end = MathF.Min(t.Len, 1.04f); float th = t.Thigh, kn = t.Knee, hm = t.Hem;
        p.Begin();
        Limb(a, b, c, end, s =>
        {
            float q = s / end, lo = q < .5f ? M.Lerp(th, kn, q * 2) : M.Lerp(kn, hm, M.Smooth(.5f, 1, q));
            if (t.Len < .6f) lo = M.Lerp(th, hm, q);
            float rad = LegRad(MathF.Min(s, 1)) + .35f + lo * .8f;
            if (t.RibCuff) rad += t.Len > .6f ? M.Smooth(.78f, .9f, q) * 1.2f - M.Smooth(.91f, .95f, q) * (1.2f + .2f * lo) : -M.Smooth(.82f, .9f, q) * 1.2f;
            if (t.Cuff) rad += M.Smooth(.88f, .9f, q) * .55f;
            return rad;
        }, fl);
        var uv = LimbUv(a, b, c); float L = (r.Thigh + r.Shin) * end; Col A = pc.A, B = pc.B, C = pc.C; var f = LimbPat(pc, a, b, c, A); int outS = sd; bool flan = t.Knit == Knit.Flannel;
        Func<float, float, Col> fn = (x, y) =>
        {
            var (u, v) = uv(x, y); float s = v / L;
            if (t.Cuff && s > .885f) return t.Knit == Knit.Denim ? A.Shade(.38f) : B;
            if (t.RibCuff && s > (t.Len > .6f ? .92f : .86f)) return C;
            if (t.Stripe && u * -outS > 1.4f && u * -outS < 3.4f) return B;
            if (t.ColorBlock && s > .46f) return B;
            if (flan && pc.Pat == 0) return Patterns.Get(4, u, v, A, B, C);
            return f(x, y);
        };
        var k = Cloth(fn, t.Knit, uv, 3.6f); var old = k.Tone; bool denim = t.Knit == Knit.Denim;
        k.Tone = (x, y) => { float o = old?.Invoke(x, y) ?? 0; if (denim) { var (u, v) = uv(x, y); o += .16f * MathF.Exp(-u * u * .12f) * M.Smooth(.05f, .2f, v / L) * M.Smooth(.62f, .4f, v / L); } return o + dk; };
        k.Ao = .8f; p.Shade(k);
        if (t.Seam && fine) { var (q0, _) = LimbAt(a, b, c, .12f * end); p.Stroke([q0 + new Vector2(sd * 2.8f, 0), b + new Vector2(sd * 2.4f, 0), c + new Vector2(sd * 1.5f, 0)], .3f, default, 1, true, .84f); }
        // knee + hem creases
        if (t.Len > .6f)
        {
            p.Crease(b + new Vector2(-3, -.5f), b + new Vector2(-.6f, .4f), .4f, .82f); p.Crease(b + new Vector2(3, .8f), b + new Vector2(.8f, 1.4f), .4f, .82f);
            if (fine) { var (q1, _) = LimbAt(a, b, c, end * .9f); for (int i = 0; i < 2 + (int)hm; i++) p.Crease(q1 + new Vector2(-4, -i * 1.7f), q1 + new Vector2(1 - i, .9f - i * 1.7f), .4f, .83f); }
        }
        var (ep, ed) = LimbAt(a, b, c, end * (t.Cuff ? .885f : t.RibCuff ? .91f : .965f)); ep += fl * .8f; var en = new Vector2(-ed.Y, ed.X) * 9;
        p.Crease(ep + en, ep - en, .35f, t.Cuff || t.RibCuff ? .68f : .8f);
        if (t.Pleat) { var (q0, _) = LimbAt(a, b, c, .04f); var (q1, _) = LimbAt(a, b, c, end * .95f); p.Stroke([q0, b, q1 + fl], .4f, default, 1, true, .84f); }
        if (t.Cargo)
        {
            var (q, dd) = LimbAt(a, b, c, t.Len > .6f ? .4f : .3f); var n = new Vector2(sd * (LegRad(.35f) + th * .5f - 1.2f), 0); var o = q + n;
            p.Begin().Poly(o + new Vector2(-2.3f, -2.6f), o + new Vector2(2.3f, -2.6f), o + new Vector2(2.5f, 3), o + new Vector2(-2.3f, 3)); p.Shade(new Ink(fn, 1.2f, .5f) { Ao = .4f, Tone = (x, y) => dk });
            p.Begin().Rect(o.X - 2.6f, o.Y - 3, 5.2f, 1.9f); p.Shade(new Ink(B, .8f, .4f) { Ao = .4f, Tone = (x, y) => dk });
        }
        if (t.KneePatch) { var o = t.Len > .6f ? b : LimbAt(a, b, c, end * .6f).pos; p.Begin().Poly(o + new Vector2(-2.6f, -3), o + new Vector2(2.4f, -3.2f), o + new Vector2(2.7f, 2.8f), o + new Vector2(-2.4f, 3)); p.Shade(new Ink(sd < 0 ? B : C, 1, .35f, .8f) { Ao = 0, Tone = (x, y) => dk }); if (fine) for (int i = 0; i < 4; i++) { p.Dot(o.X - 2 + i * 1.3f, o.Y - 2.4f, C.Shade(.4f)); p.Dot(o.X - 2 + i * 1.3f, o.Y + 2.1f, C.Shade(.4f)); } }
        for (int i = 0; i < t.Rips; i++)
        {
            float q = t.Len > .6f ? (i == 0 ? .5f : i == 1 ? .28f : .66f) : end * (.55f + i * .25f); if (i == 1 && sd > 0) q += .06f; var o = LimbAt(a, b, c, q).pos;
            p.Begin().Blob(o + new Vector2(-2.2f, -.3f), o + new Vector2(0, -1), o + new Vector2(2.1f, 0), o + new Vector2(0, 1.1f)); p.Shade(new Ink(skin, 1, .3f, .8f) { Ao = 0 }); p.Line(o + new Vector2(-1.7f, .2f), o + new Vector2(1.6f, .1f), .3f, A.Shade(.6f));
        }
        if (t.Scallop) { p.Begin(); for (int i = -2; i <= 2; i++) { var e = LimbAt(a, b, c, end).pos + fl; p.Ellipse(e.X + i * 1.8f, e.Y, 1.2f, 1.2f); } p.IntersectAlpha(); p.Shade(new Ink(C, .9f, .4f, .9f) { Ao = 0 }); }
    }

    void BottomsUpper()
    {
        if (!Vis(Slot.Bottom)) return;
        var pc = d[Slot.Bottom]; var t = pc.Id < 30 ? Catalog.Pants[pc.Id] : Catalog.Shorts[pc.Id - 30];
        Col A = pc.A, B = pc.B, C = pc.C; float up = 5.5f, lo = t.Thigh * .55f + .5f;
        float wH = r.Half(up) + .5f + lo * .25f, wB = r.Hip + .4f + lo * .6f;
        p.Begin().Poly(Tp(-wH, up), Tp(-wB, 0), Tp(-wB, -3.5f), hpL + new Vector2(-r.ThighR * .75f, 5), hpL + new Vector2(r.ThighR * .5f, 5.5f), P + new Vector2(0, 5.2f), hpR + new Vector2(-r.ThighR * .5f, 5.5f), hpR + new Vector2(r.ThighR * .75f, 5), Tp(wB, -3.5f), Tp(wB, 0), Tp(wH, up));
        bool flan = t.Knit == Knit.Flannel; var f = TorsoPat(pc, A);
        Func<float, float, Col> fn = (x, y) => flan && pc.Pat == 0 ? Patterns.Get(4, x - P.X, y - P.Y, A, B, C) : f(x, y);
        var k = Cloth(fn, t.Knit, BodyUv, 3); k.Line = .0f; k.Ao = 0; k.Grad = 0; p.Shade(k);
        float cx = po.Turn * 1.2f;
        p.Crease(P + new Vector2(cx, -4.6f), P + new Vector2(cx * .6f, 5), .4f, .7f);
        p.Crease(P + new Vector2(cx * .6f, 4.6f), P + new Vector2(-3.2f, 2.8f), .4f, .8f); p.Crease(P + new Vector2(cx * .6f, 4.6f), P + new Vector2(3.2f, 2.8f), .4f, .8f);
        if (!t.Bib) { p.Stroke([Tp(-wB + .4f, -1), Tp(-wB + 2.8f, 2), Tp(-wH + 3.6f, up - 2)], .35f, default, 1, true, .76f); p.Stroke([Tp(wB - .4f, -1), Tp(wB - 2.8f, 2), Tp(wH - 3.6f, up - 2)], .35f, default, 1, true, .76f); }
        if (t.Flap) for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 3; i++) { var q = Tp(s * 4.6f + cx, 2.6f - i * 2.3f); p.Begin().Ellipse(q.X, q.Y, .75f, .75f); p.Shade(new Ink(C, .5f, .4f, .8f) { Ao = 0 }); }
        // waistband
        p.Begin().Poly(Tp(-wH - .1f, up + .3f), Tp(wH + .1f, up + .3f), Tp(wH + .5f, up - 2.2f), Tp(-wH - .5f, up - 2.2f));
        p.Shade(new Ink(t.Belt ? B : t.Draw ? (Col)pc.A : A, 1.2f, .4f) { Ao = .5f });
        if (t.Belt) { var q = Tp(cx, up - 1); p.Begin().Rect(q.X - 1.7f, q.Y - 1.3f, 3.4f, 2.6f); p.Shade(new Ink(C, .9f, .5f) { Ao = 0 }); p.Begin().Rect(q.X - .7f, q.Y - .5f, 1.4f, 1); p.Flat(((Col)B).Shade(-.3f)); if (fine) for (int s = -1; s <= 1; s += 2) { var l = Tp(s * 6, up - 1); p.Begin().Rect(l.X - .5f, l.Y - 1.4f, 1, 2.8f); p.Shade(new Ink(A, .5f, .3f) { Ao = 0 }); } }
        else if (!t.Draw && t.Seam) { var q = Tp(cx, up - 1); p.Begin().Ellipse(q.X, q.Y, .75f, .75f); p.Shade(new Ink(C, .6f, .5f, .9f) { Ao = 0 }); }
        if (t.Draw) for (int s = -1; s <= 1; s += 2) { var q = Tp(cx + s * .8f, up - 1.6f); p.Curve(q, q + new Vector2(s * .7f, 2), q + new Vector2(s * 1.1f + po.HemX * .3f, 4.6f), .55f, C, false); }
    }
    void Bib()
    {
        var pc = d[Slot.Bottom]; if (pc.Id >= 60) return; var t = pc.Id < 30 ? Catalog.Pants[pc.Id] : Catalog.Shorts[pc.Id - 30]; if (!t.Bib) return;
        float T = r.Torso, cx = po.Turn * 1.2f; Col A = pc.A, B = pc.B, C = pc.C; var f = TorsoPat(pc, A);
        for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(Tp(s * 5f + cx, T * .62f), Tp(s * 6.8f + cx, T * .62f), Tp(s * (r.Neck + 4.2f), T * .97f), Tp(s * (r.Neck + 2.2f), T + .2f)); p.Shade(new Ink(A, 1, .4f) { Ao = .6f }); }
        float wb = r.Half(5) + .9f + t.Thigh * .2f;
        p.Begin().Poly(Tp(-6.8f + cx, T * .65f), Tp(6.8f + cx, T * .65f), Tp(8 + cx, T * .42f), Tp(wb, T * .26f), Tp(wb + .4f, 2), Tp(-wb - .4f, 2), Tp(-wb, T * .26f), Tp(-8 + cx, T * .42f));
        p.Shade(Cloth(f, t.Knit, BodyUv, 3) with { Ao = 1 });
        var q = Tp(cx, T * .5f); p.Begin().Poly(q + new Vector2(-3.6f, -2.2f), q + new Vector2(3.6f, -2.2f), q + new Vector2(3.6f, 2), q + new Vector2(0, 3), q + new Vector2(-3.6f, 2)); p.Shade(new Ink(f, 1, .35f, .85f) { Ao = 0 });
        for (int s = -1; s <= 1; s += 2) { var b = Tp(s * 5.7f + cx, T * .61f); p.Begin().Ellipse(b.X, b.Y, .9f, .9f); p.Shade(new Ink(C, .7f, .5f, .9f) { Ao = 0 }); }
    }

    /// How freely the legs can swing in this outfit. Long or narrow skirts shorten the stride.
    public static float StrideOf(AvatarData d)
    {
        int b = d[Slot.Bottom].Id; if (b < 60) return 1; var k = Catalog.Skirts[b - 60];
        float s = M.Lerp(1, .5f, M.Smooth(.3f, .8f, k.Len)); if (k.Kind == SkirtKind.Pencil) s = MathF.Min(s, .46f); else s += MathF.Max(0, k.Flare - 1.2f) * .07f * (1 - M.Smooth(.6f, .9f, k.Len)); return M.Clamp(s, .4f, 1);
    }
    void Skirt(SkirtDef t)
    {
        var pc = d[Slot.Bottom]; Col A = pc.A, B = pc.B, C = pc.C; float legLen = r.Thigh + r.Shin, len = t.Len * legLen, up = 5.5f, wT = r.Half(up) + .6f, wH = r.Hip + .9f;
        float flare = t.Flare * (3 + t.Len * 5);
        if (t.Kind == SkirtKind.Pencil) flare = .5f;
        // the legs shape the cloth: gather how far they reach at every height under the skirt
        float yHem = P.Y + len; const int LV = 17; Span<float> exL = stackalloc float[LV], exR = stackalloc float[LV]; float y0w = Tp(0, up).Y, span = len + up;
        for (int leg = 0; leg < 2; leg++)
        {
            Vector2 a = leg == 0 ? hpL : hpR, b = leg == 0 ? knL : knR, c = leg == 0 ? anL : anR; var toe = c + new Vector2(at > .35f ? (po.Turn > 0 ? 1 : -1) * 6.5f : 0, 2.5f); var heel = c + new Vector2(at > .35f ? (po.Turn > 0 ? -1 : 1) * 3f : 0, 2);
            for (int i = 0; i <= 26; i++)
            {
                var q = i <= 24 ? LimbAt(a, b, c, i / 24f).pos : i == 25 ? toe : heel; float rad = i <= 24 ? LegRad(i / 24f) + .9f : 1.4f; if (q.Y > yHem + 1.5f) continue;
                float lv = (q.Y - y0w) / span * (LV - 1); int l0 = Math.Clamp((int)MathF.Floor(lv), 0, LV - 1), l1 = Math.Min(LV - 1, l0 + 1);
                for (int l = l0; l <= l1; l++) { exL[l] = MathF.Max(exL[l], P.X - (q.X - rad)); exR[l] = MathF.Max(exR[l], q.X + rad - P.X); }
            }
        }
        // cloth hangs straight down below whatever pushes it, and stretches taut from the waist above it
        for (int l = 1; l < LV; l++) { exL[l] = MathF.Max(exL[l], exL[l - 1] * .985f); exR[l] = MathF.Max(exR[l], exR[l - 1] * .985f); }
        for (int l = LV - 2; l > 0; l--) { float k = l / (l + 1f); exL[l] = MathF.Max(exL[l], M.Lerp(wT, exL[l + 1], k * .96f)); exR[l] = MathF.Max(exR[l], M.Lerp(wT, exR[l + 1], k * .96f)); }
        float[] eL = exL.ToArray(), eR = exR.ToArray();
        float Ext(float q, bool left) { float v = M.Clamp(q) * (LV - 1); int a = (int)v, b = Math.Min(LV - 1, a + 1); var e = left ? eL : eR; return M.Lerp(e[a], e[b], v - a); }
        float hwL = MathF.Max(wH + flare, Ext(1, true) + .6f), hwR = MathF.Max(wH + flare, Ext(1, false) + .6f);
        if (at > .3f && t.Len > .5f) { float tight = M.Smooth(.5f, .9f, t.Len) * .35f * at; hwL = MathF.Max(Ext(1, true) + .8f, M.Lerp(hwL, wH + flare * .5f, tight)); hwR = MathF.Max(Ext(1, false) + .8f, M.Lerp(hwR, wH + flare * .5f, tight)); }
        bool slit = t.Kind is SkirtKind.Wrap or SkirtKind.Petal;
        int tiers = t.Kind == SkirtKind.Tiered ? 3 : t.Kind == SkirtKind.Tulle ? 3 : 1;
        var f = TorsoPat(pc, A); int pl = t.Kind is SkirtKind.Pleated or SkirtKind.Kilt ? 11 : t.Kind == SkirtKind.Tulle ? 9 : 5;
        float FlutterAmt(float q) => (.5f + t.Flare * .75f) * q * q * (t.Kind == SkirtKind.Pencil ? .3f : 1);
        Vector2 Pt(float x, float q) // x -1..1, q 0 waist..1 hem
        {
            float h = q < .2f ? M.Lerp(wT, wH, q / .2f) : M.Lerp(wH, x < 0 ? hwL : hwR, MathF.Pow((q - .2f) / .8f, t.Kind == SkirtKind.Plain && t.Name.StartsWith("Bubble") ? .5f : .9f));
            if (t.Name.StartsWith("Bubble") && q > .85f) h -= (q - .85f) * 18;
            h = MathF.Max(h, Ext(q, x < 0));
            var b = Tp(0, up); float y = b.Y + (len + up) * q + MathF.Sqrt(MathF.Max(0, 1 - x * x)) * (.6f + q * 1.6f);
            return new Vector2(b.X - sinL * (up * q) + x * h * (1 - .08f * at), y) + HemOff(x * 3 + q * 2, FlutterAmt(q) * 1.5f);
        }
        for (int ti = tiers - 1; ti >= 0; ti--)
        {
            float q0 = t.Kind == SkirtKind.Tiered ? ti / 3f : 0, q1 = t.Kind == SkirtKind.Tiered ? (ti + 1) / 3f + .02f : t.Kind == SkirtKind.Tulle ? 1 - (tiers - 1 - ti) * .0f - (2 - ti) * .12f : 1;
            if (t.Kind == SkirtKind.Tulle) q1 = 1 - (2 - ti) * .13f + 0;
            var pts = new List<Vector2>(); int N = 24;
            for (int i = 0; i <= 8; i++) pts.Add(Pt(-1, M.Lerp(q0, q1, i / 8f)));
            for (int i = 1; i < N; i++)
            {
                float x = -1 + 2f * i / N; var q = Pt(x, q1);
                if (t.Kind is SkirtKind.Pleated or SkirtKind.Kilt) q.Y += ((i & 1) == 0 ? .55f : -.35f); if (t.Kind is SkirtKind.Ruffle or SkirtKind.Tiered or SkirtKind.Tulle) q.Y += M.Sin(i * 1.4f + ti) * .9f;
                if (t.Kind == SkirtKind.Wrap) q.Y -= M.Smooth(.0f, .9f, x) * len * .16f; if (t.Kind == SkirtKind.Petal) q.Y -= MathF.Abs(M.Sin(x * M.PI * 1.5f)) * -2 + 2;
                pts.Add(q);
            }
            float qr = t.Kind == SkirtKind.Wrap ? q1 - .16f * len / (len + up) : q1; for (int i = 8; i >= 0; i--) pts.Add(Pt(1, M.Lerp(q0, qr, i / 8f)));
            p.Begin().Poly(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(pts), 1);
            int tt = ti; float y0 = P.Y, H2 = len;
            bool kilt = t.Kind == SkirtKind.Kilt;
            Func<float, float, Col> fn = (x, y) =>
            {
                float v = (y - y0) / H2, u = x - P.X;
                if (t.Kind == SkirtKind.Tulle) return tt == 2 ? A : tt == 1 ? Col.Lerp(A, B, .5f) : B;
                if (t.Kind == SkirtKind.Tiered && pc.Pat == 0) return tt == 1 ? B : A;
                if (t.Kind == SkirtKind.Panel && ((int)MathF.Floor(u / (3.2f + v * 2.6f) + 50) & 1) == 0) return B;
                if (t.Trim && v > .86f) return v > .9f && v < .95f ? C : B;
                if (kilt && pc.Pat == 0) return Patterns.Get(4, u, y, A, B, C, 1.2f);
                return f(x, y);
            };
            float slot = 2f / pl; var base0 = Tp(0, up); float hw = (hwL + hwR) * .5f;
            Vector2 ka = hpL, kb = knL, kc = hpR, kd = knR; float move = at; float Press(float x, float y) { if (move < .2f) return 0; float one(Vector2 a, Vector2 b) { var ab = b - a; float u = M.Clamp(Vector2.Dot(new Vector2(x, y) - a, ab) / ab.LengthSquared()); var d2 = new Vector2(x, y) - (a + ab * u); return MathF.Exp(-d2.LengthSquared() * .07f); } float f1 = one(ka, kb), f2 = one(kc, kd); return (MathF.Max(f1, f2) * .2f - .06f) * move; }
            p.Shade(new Ink(fn, 4.5f, .5f) { Ao = 1.2f, Tone = t.Kind is SkirtKind.Pleated or SkirtKind.Kilt ? (x, y) => { float v = M.Clamp((y - base0.Y) / (len + up)); float u = (x - P.X) / M.Lerp(wT, hw, v); float fr = u / slot - MathF.Floor(u / slot); return (fr < .25f ? -.22f * M.Smooth(.05f, .5f, v) : fr > .75f ? .06f : 0) + Press(x, y); } : Press });
            if (at > .3f && t.Len > .34f) foreach (var kn in new[] { knL, knR }) if (kn.Y < yHem - 2) { float dx = kn.X - P.X; p.Curve(Tp(0, up - 2), Vector2.Lerp(Tp(0, up), kn, .7f), kn + new Vector2(0, 1), .45f, default, true, 1, .86f); p.Curve(kn + new Vector2(0, 1), kn + new Vector2(-MathF.Sign(dx) * 1.5f, (yHem - kn.Y) * .5f), new(kn.X - dx * .18f, yHem - .5f), .45f, default, true, 1, .82f); }
            // drape folds
            if (t.Kind is not (SkirtKind.Pleated or SkirtKind.Kilt or SkirtKind.Pencil))
                for (int i = 0; i < pl; i++) { float x = M.Lerp(-.78f, .78f, i / (pl - 1f)) + M.Hash(i, 3) * .1f; p.Crease(Pt(x, M.Lerp(q0, q1, .25f + M.Hash(i, 7) * .25f)), Pt(x * 1.04f, q1 - .01f), .45f, .84f); }
            if (t.Kind == SkirtKind.Ruffle) { var rp = new Vector2[19]; for (int i = 0; i < 19; i++) rp[i] = Pt(-1 + i / 9f, .82f) + new Vector2(0, M.Sin(i * 1.7f) * .4f); p.Stroke(rp, .5f, default, 1, true, .65f); for (int i = 0; i < 18; i += 2) p.Crease(rp[i], Pt(-1 + i / 9f, .95f), .4f, .8f); }
            if (t.Kind == SkirtKind.Wrap) { p.Stroke([Pt(-.3f, 0), Pt(.3f, .4f), Pt(.62f, .9f)], .6f, C, 1, true); }
            if (t.Kind == SkirtKind.Petal) { p.Stroke([Pt(-.1f, .05f), Pt(.25f, .5f), Pt(.1f, 1)], .5f, default, 1, true, .72f); p.Stroke([Pt(.1f, .05f), Pt(-.35f, .55f), Pt(-.3f, 1)], .5f, default, 1, true, .78f); }
            if (t.Kind == SkirtKind.Buttons) { p.Stroke([Pt(po.Turn * .1f - .1f, 0), Pt(-.07f + po.Turn * .1f, 1)], .35f, default, 1, true, .78f); for (float q = .12f; q < .95f; q += 4.4f / len) { var b = Pt(po.Turn * .1f, q); p.Begin().Ellipse(b.X, b.Y, .75f, .75f); p.Shade(new Ink(C, .6f, .5f, .9f) { Ao = 0 }); } }
            if (t.Kind == SkirtKind.Apron) { p.Begin().Poly(Pt(-.5f, 0), Pt(.5f, 0), Pt(.58f, .5f), Pt(.45f, .84f), Pt(0, .9f), Pt(-.45f, .84f), Pt(-.58f, .5f)); p.Shade(new Ink(B, 2, .4f) { Ao = .6f }); var e = Pt(0, .45f); p.Line(Pt(-.45f, .8f), Pt(.45f, .8f), .4f, C); }
            if (t.Pockets) for (int s = -1; s <= 1; s += 2) { var e = Pt(s * .5f, .34f); p.Begin().Poly(e + new Vector2(-2.4f, -2), e + new Vector2(2.4f, -2), e + new Vector2(2.3f, 2.4f), e + new Vector2(-2.3f, 2.4f)); p.Shade(new Ink(A, 1, .4f) { Ao = 0 }); }
            if (fine && t.Kind is SkirtKind.Plain) { var rp = new Vector2[13]; for (int i = 0; i < 13; i++) rp[i] = Pt(-1 + i / 6f, .95f); p.Stroke(rp, .3f, default, 1, true, .84f); }
        }
        p.Begin().Poly(Tp(-wT - .2f, up + .5f), Tp(wT + .2f, up + .5f), Tp(wT + .7f, up - 2), Tp(-wT - .7f, up - 2)); p.Shade(new Ink(t.Belt ? C : t.Kind == SkirtKind.Tulle ? A : B, 1.2f, .4f) { Ao = .6f });
        if (t.Belt) { var q = Tp(po.Turn, up - .8f); p.Begin().Rect(q.X - 1.6f, q.Y - 1.2f, 3.2f, 2.4f); p.Shade(new Ink(0xd9b25a, .9f, .5f) { Ao = 0 }); }
    }
    void SkirtStraps()
    {
        var pc = d[Slot.Bottom]; float T = r.Torso; for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(Tp(s * 4.4f, 5), Tp(s * 6.2f, 5), Tp(s * (r.Neck + 4), T * .97f), Tp(s * (r.Neck + 2.2f), T + .2f)); p.Shade(new Ink((Col)pc.B, 1, .4f) { Ao = .6f }); var b = Tp(s * 5.2f, 7.5f); p.Begin().Ellipse(b.X, b.Y, .9f, .9f); p.Shade(new Ink((Col)pc.C, .7f, .5f, .9f) { Ao = 0 }); }
    }

    // ---------------------------------------------------------------- shoes
    Vector2 fo; float fDir, fCos, fSin; bool fSide;
    Vector2 F(float x, float y) => new(fo.X + x * fCos - y * fSin, fo.Y + x * fSin + y * fCos);
    void FootShape(int sd, Vector2 c, float foot, float sole, out float len, float extra, float scale)
    {
        float dirn = po.Turn >= 0 ? 1 : -1; fSide = at > .35f; fDir = dirn; fo = c; float rot = fSide ? foot * dirn : 0; fCos = M.Cos(rot); fSin = M.Sin(rot);
        float ah = r.AnkleH, w = r.AnkleR;
        len = 0;
        p.Begin();
        if (fSide)
        {
            float e = 7.4f * scale + extra, h = 2.9f + extra; len = e;
            Span<Vector2> q = [F(-dirn * (w + .5f + extra), -1.2f), F(dirn * (w + .4f + extra), -1.2f), F(dirn * (w + 2.2f), .2f - extra * .5f), F(dirn * (e - .8f), ah - 3 - extra), F(dirn * (e + .4f), ah - 1.5f), F(dirn * e, ah), F(-dirn * (w + 1 + extra), ah), F(-dirn * (w + 1.3f + extra), ah - 2)];
            p.Blob(q, 1, 4); p.Cut(F(-20, ah), F(20, ah), F(20, ah + 8), F(-20, ah + 8));
        }
        else
        {
            float o = sd * .9f, hw = 3.5f * scale + extra * .9f; len = hw;
            p.Blob(F(-w - .3f - extra, -1.2f), F(w + .3f + extra, -1.2f), F(o + hw - .2f, ah - 2.2f), F(o + hw, ah + .4f), F(o, ah + 1.5f), F(o - hw, ah + .4f), F(o - hw + .2f, ah - 2.2f));
            p.Cut(F(-20, ah + 1.1f), F(20, ah + 1.1f), F(20, ah + 8), F(-20, ah + 8));
        }
    }
    void Shoe(int sd, Vector2 knee, Vector2 c, float foot, float dk, bool under)
    {
        var t = Catalog.Shoes[d[Slot.Shoes].Id]; var pc = d[Slot.Shoes]; Col A = pc.A, B = pc.B, C = pc.C; float ah = r.AnkleH, dirn = po.Turn >= 0 ? 1 : -1;
        bool wide = d[Slot.Bottom].Id < 30 && Catalog.Pants[d[Slot.Bottom].Id].Hem > .8f && Vis(Slot.Bottom);
        bool shaftOver = t.Shaft > .3f && !wide; // tall boots go over slim trousers
        float dkk = dk; Func<float, float, float> tone = dk != 0 ? (x, y) => dkk : null;
        bool open = t.K is ShoeKind.Sandal or ShoeKind.Slide;
        bool socks = t.K is ShoeKind.Sneaker or ShoeKind.Runner or ShoeKind.Skate or ShoeKind.MaryJane or ShoeKind.Oxford or ShoeKind.HighTop;
        void Shaft()
        {
            if (t.Shaft <= 0) return; var dir = Vector2.Normalize(knee - c); var e = c + dir * r.Shin * t.Shaft; var n = new Vector2(-dir.Y, dir.X); float w0 = r.AnkleR + .9f, w1 = LegRad(1 - t.Shaft * .5f) + (t.K == ShoeKind.Rain ? 1.5f : t.K == ShoeKind.Fur ? 1.5f : t.K == ShoeKind.Cowboy ? 1.5f : .9f);
            p.Begin().Poly(c + n * w0 - dir * 1.5f, e + n * w1, e - n * w1, c - n * w0 - dir * 1.5f); Col sc = t.K == ShoeKind.Rain && t.Stripe ? B : A;
            p.Shade(new Ink(sc, 2, .55f) { Ao = .5f, Tone = tone });
            if (t.Fur) { p.Begin(); for (int i = -2; i <= 2; i++) p.Ellipse(e.X + n.X * w1 * i * .45f, e.Y + n.Y * w1 * i * .45f, 1.5f, 1.4f); p.Shade(new Ink(B, 1.3f, .5f) { Ao = .6f, Tone = tone }); }
            else { p.Begin().Poly(e + n * (w1 + .1f), e - n * (w1 + .1f), e - n * w1 - dir * 1.2f, e + n * w1 - dir * 1.2f); p.Shade(new Ink(t.K is ShoeKind.Chelsea ? A : B, 1, .4f, .8f) { Ao = .3f, Tone = tone }); }
            if (t.Laces) for (float s = .9f; s < r.Shin * t.Shaft - 1; s += 1.9f) { var o = c + dir * s; p.Line(o - n * 1.3f, o + n * 1.3f, .5f, C, false); }
            if (t.K == ShoeKind.Chelsea) { p.Begin().Poly(c + n * w0 * .3f, e + n * w1 * .35f, e - n * w1 * .35f, c - n * w0 * .3f); p.Shade(new Ink(B, 1, .3f, .7f) { Ao = 0, Tone = tone }); }
            if (t.K == ShoeKind.Cowboy && fine) { var m = Vector2.Lerp(c, e, .6f); p.Curve(m - n * 2, m + dir * 4, m + n * 2, .4f, C, false); p.Curve(m - n * 1.4f - dir, m + dir * 2, m + n * 1.4f - dir, .4f, C, false); }
        }
        if (under)
        {
            if (socks) { var dir = Vector2.Normalize(knee - c); p.Begin().Capsule(c, r.AnkleR + .25f, c + dir * r.Shin * .16f, LegRad(.92f) + .35f); p.Shade(new Ink(C.Lum < .4f ? C.Shade(.7f) : C, 1.5f, .4f) { Ao = .3f, Tone = tone }); }
            if (!shaftOver && !(wide && t.Shaft > .15f)) Shaft();
            if (open) BareFoot(sd, knee, c, foot, dk);
            return;
        }
        if (shaftOver) Shaft();
        float ex = t.K switch { ShoeKind.Runner => .45f, ShoeKind.Fur => .7f, ShoeKind.Slipper => .5f, ShoeKind.Flat => -.2f, ShoeKind.Rain or ShoeKind.Boot or ShoeKind.Hiking or ShoeKind.Combat => .35f, _ => .1f };
        if (!open)
        {
            FootShape(sd, c, foot, t.Sole, out _, ex, 1);
            if (t.K is ShoeKind.Flat or ShoeKind.MaryJane or ShoeKind.Loafer or ShoeKind.Slipper or ShoeKind.Clog) p.CutEllipse(fo.X, fo.Y - .6f, r.AnkleR + .2f, 1.9f);
            p.Shade(new Ink(A, 2.1f, .6f) { Ao = .6f, Tone = tone });
        }
        else if (fSide)
        {
            p.Begin().Poly(F(dirn * 2.6f, ah - 3.2f), F(dirn * 5.6f, ah - 2.4f), F(dirn * 6, ah), F(dirn * 2.2f, ah)); p.Shade(new Ink(A, 1, .5f) { Ao = .4f, Tone = tone }); if (t.Strap) { p.Begin().Poly(F(-dirn * (r.AnkleR + .5f), -.6f), F(dirn * (r.AnkleR + .5f), -.6f), F(dirn * (r.AnkleR + .5f), .8f), F(-dirn * (r.AnkleR + .5f), .8f)); p.Shade(new Ink(A, 1, .5f) { Ao = 0, Tone = tone }); }
        }
        else { float o = sd * .9f; p.Begin().Poly(F(o - 3.4f, ah - 1.9f), F(o + 3.4f, ah - 1.9f), F(o + 3.7f, ah - .1f), F(o - 3.7f, ah - .1f)); p.Shade(new Ink(A, 1, .5f) { Ao = .4f, Tone = tone }); if (t.Strap) { p.Begin().Rect(c.X - r.AnkleR - .4f, c.Y - .7f, r.AnkleR * 2 + .8f, 1.3f); p.Shade(new Ink(A, 1, .5f) { Ao = 0, Tone = tone }); } }
        // sole
        float so = MathF.Max(.9f, t.Sole * 1.05f);
        if (fSide)
        {
            float e = 7.5f + ex; p.Begin().Poly(F(-dirn * (r.AnkleR + 1.3f + ex), ah - so), F(dirn * (e + .2f), ah - so), F(dirn * (e + .5f), ah - so * .4f), F(dirn * (e - .3f), ah + .1f), F(-dirn * (r.AnkleR + 1 + ex), ah + .1f));
            if (t.K is ShoeKind.Cowboy or ShoeKind.Oxford or ShoeKind.Chelsea or ShoeKind.Loafer) p.Cut(F(dirn * .6f, ah - .2f), F(dirn * 2.6f, ah - .2f), F(dirn * 2.4f, ah + .2f), F(dirn * .8f, ah + .2f));
            p.Shade(new Ink(B, .8f, .35f, .9f) { Ao = 0, Tone = tone });
            if (t.Toe) { p.Begin().Ellipse(fo.X, fo.Y, 0, 0); p.Begin().Blob(F(dirn * (e - 2.9f), ah - 2.4f), F(dirn * (e - .6f), ah - 2.3f), F(dirn * (e + .3f), ah - so), F(dirn * (e - 2.9f), ah - so)); p.IntersectAlpha(); p.Shade(new Ink(t.K is ShoeKind.Oxford ? A.Shade(-.3f) : B, 1, .5f, .9f) { Ao = 0, Tone = tone }); }
            if (t.Stripe) { p.Line(F(dirn * .4f, ah - so - .2f), F(dirn * 2.4f, ah - 3.6f), .8f, C, false); if (fine) p.Line(F(dirn * 1.8f, ah - so - .2f), F(dirn * 3.6f, ah - 3.2f), .6f, C, false); }
            if (t.Laces) for (int i = 0; i < 3; i++) p.Line(F(dirn * (2 + i * 1.1f), -.5f + i * .7f), F(dirn * (3.1f + i * 1.1f), -.9f + i * .75f), .5f, t.K is ShoeKind.Oxford or ShoeKind.Combat ? C : C, false);
        }
        else
        {
            float o = sd * .9f, hw = 3.6f + ex; p.Begin().Poly(F(o - hw, ah + .2f - so * .55f), F(o, ah + .9f - so * .55f), F(o + hw, ah + .2f - so * .55f), F(o + hw - .1f, ah + 1.1f), F(o - hw + .1f, ah + 1.1f)); p.IntersectAlpha(); p.Shade(new Ink(B, .8f, .35f, .9f) { Ao = 0, Tone = tone });
            if (t.Toe) { p.Begin().Ellipse(fo.X + o, fo.Y + ah - .1f, hw - .9f, 1.2f); p.Shade(new Ink(t.K is ShoeKind.Oxford ? A.Shade(-.3f) : B, 1, .5f, .8f) { Ao = 0, Tone = tone }); }
            if (t.Laces) for (int i = 0; i < 2; i++) p.Line(F(o * .6f - 1.2f, .1f + i * 1.15f), F(o * .6f + 1.2f, .1f + i * 1.15f), .5f, C, false);
            if (t.Stripe) { p.Line(F(o + sd * 2.4f, ah - 2), F(o + sd * 3.2f, ah - .4f), .7f, C, false); }
        }
        if (t.Strap && !open) { if (fSide) p.Line(F(dirn * 1.2f, -.3f), F(dirn * 3.6f, .4f), 1.1f, t.K == ShoeKind.Sneaker ? C : A.Shade(-.2f), false); else p.Line(F(-r.AnkleR, .4f), F(r.AnkleR, .4f), 1.1f, t.K == ShoeKind.Sneaker ? C : A.Shade(-.2f), false); }
        if (t.Bow) { var q = fSide ? F(dirn * 4.6f, ah - 2.8f) : F(sd * .9f, ah - 1.6f); p.Begin().Poly(q, q + new Vector2(-1.6f, -1), q + new Vector2(-1.6f, 1)).Poly(q, q + new Vector2(1.6f, -1), q + new Vector2(1.6f, 1)); p.Flat(C); }
        if (t.Fur && t.K is ShoeKind.Slipper or ShoeKind.Loafer && t.Fur) { p.Begin(); for (int i = -2; i <= 2; i++) p.Ellipse(c.X + i * 1.2f, c.Y - .3f, 1, .9f); p.Shade(new Ink(B, 1, .4f) { Ao = 0, Tone = tone }); }
    }

    // ---------------------------------------------------------------- jacket
    Func<float, float, Col> JacketFn(JacketDef t, Piece pc, Func<float, float, Col> f)
    {
        Col A = pc.A, B = pc.B, C = pc.C; float T = r.Torso; bool flan = t.Knit == Knit.Flannel;
        return (x, y) =>
        {
            float u = x - P.X, v = y - P.Y + T;
            if (t.RibHem && y > P.Y + t.Hem - 2.8f) return t.Knit == Knit.Flat || t.Knit == Knit.Cable ? B : A;
            if (t.Yoke && v < 8 - MathF.Abs(u) * .1f && v > 7.4f) return C;
            if (t.Stripe && !t.RibHem && v > 8 && v < 11) return B;
            if (flan && pc.Pat == 0) return Patterns.Get(4, u, v, A, B, C);
            if (t.Cape && t.Knit == Knit.Rib && pc.Pat == 0) { int k = M.Mod((int)MathF.Floor((v + MathF.Abs(u) * .35f) / 3), 5); return k == 1 ? B : k == 3 ? C : A; }
            return f(x, y);
        };
    }
    void JacketBack(JacketDef t)
    {
        var pc = d[Slot.Jacket]; float T = r.Torso, w = ClothHalf(0, t.Fit, -t.Hem, t.Hem > 8) + 1.2f + t.Hem * .14f;
        p.Begin().Poly(Tp(-r.ShX, T * .8f), Tp(r.ShX, T * .8f), Tp(w, -t.Hem + 1) + HemOff(1, 1.6f), Tp(0, -t.Hem - .4f) + HemOff(2, 1.6f), Tp(-w, -t.Hem + 1) + HemOff(3, 1.6f)); Col A = pc.A; p.Shade(new Ink(A.Shade(-.45f), 3, .3f) { Ao = 0 });
    }
    void JacketSleeve(JacketDef t, int sd)
    {
        if (t.Sleeve <= 0) return; var pc = d[Slot.Jacket]; Vector2 a = sd < 0 ? shL : shR, b = sd < 0 ? elL : elR, c = sd < 0 ? haL : haR; float dk = at > .3f && sd == -near ? -.12f : 0;
        float end = t.Sleeve * .97f, loose = .7f + t.Fit * .4f; bool kim = t.Name.StartsWith("Kimono");
        var fl = new Vector2(po.SleeveX, po.SleeveY) * (kim ? 2 : .9f) + new Vector2(M.Sin(po.T * 7 + sd) * po.Wind * .4f, 0);
        p.Begin(); Limb(a, b, c, end, s => { float q = s / end, rad = (ArmRad(s) + loose) * M.Lerp(.95f, 1, M.Smooth(0, .16f, s)); if (kim) rad += q * 3; if (t.Quilt) rad += MathF.Abs(M.Sin(q * 9)) * .9f - .2f; if (t.RibHem) rad += M.Smooth(.6f, .86f, q) * .7f - M.Smooth(.88f, .92f, q) * 1.7f; return rad; }, fl);
        Col A = pc.A, B = pc.B, C = pc.C, ca = t.ContrastSleeve ? B : A; var uv = LimbUv(a, b, c); var f = LimbPat(pc, a, b, c, ca); float L = r.UArm + r.LArm; bool flan = t.Knit == Knit.Flannel;
        Func<float, float, Col> fn = (x, y) => { var (u, v) = uv(x, y); float s = v / L / end; if (t.RibHem && s > .9f) return t.Knit == Knit.Flat || t.Knit == Knit.Cable ? (t.ContrastSleeve ? A : B) : A; if (t.Lapel is Lapel.Shearling && s > .88f) return B; if (t.Stripe && u * -sd > .6f && u * -sd < 2.2f) return C; if (flan && pc.Pat == 0) return Patterns.Get(4, u, v, ca, B, C); return f(x, y); };
        var k = Cloth(fn, t.Knit, uv, 2.8f); k.Ao = .9f; var old = k.Tone; k.Tone = (x, y) => (old?.Invoke(x, y) ?? 0) + dk; k.Merge = ShoulderMerge(sd, a, r.ArmR + loose); p.Shade(k);
        if (fine) { float rr = r.ArmR + loose; p.Curve(a + new Vector2(-sd * rr * .35f, -rr * .75f), a + new Vector2(-sd * rr * .75f, rr * .1f), a + new Vector2(-sd * rr * .8f, rr * .95f), .45f, default, true, 1, .82f); }
        if (t.Quilt) for (float s = .14f; s < .95f; s += .15f) { var (q, dd) = LimbAt(a, b, c, s * end); var n = new Vector2(-dd.Y, dd.X) * 8; p.Crease(q + n, q - n, .45f, .74f); }
        else { var bn = Vector2.Normalize(c - a); var pn = new Vector2(-bn.Y, bn.X); for (int i = -1; i <= 1; i++) p.Crease(b + pn * 1.5f + bn * i * 1.9f, b - pn * 3.6f + bn * (i * 1.9f + .9f), .4f, .82f); }
        var (cp, cd) = LimbAt(a, b, c, end * (t.RibHem ? .9f : .87f)); cp += fl * .7f; var cn = new Vector2(-cd.Y, cd.X) * 7; p.Crease(cp + cn, cp - cn, .4f, .72f);
        if (t.ElbowPatch) { p.Begin().Ellipse(b.X, b.Y, 2, 2.8f); p.Shade(new Ink(B, 1, .4f, .85f) { Ao = 0 }); }
        if (t.Fringe) for (int i = 0; i < 7; i++) { var (q, _) = LimbAt(a, b, c, .15f + i * .11f); var s0 = q + new Vector2(sd * 2.6f, 0); p.Line(s0, s0 + new Vector2(sd * .8f + po.SleeveX, 3.6f), .5f, B, false); }
        if (t.Patch && sd > 0) { var q = LimbAt(a, b, c, .25f).pos; p.Begin().Ellipse(q.X, q.Y, 1.6f, 1.6f); p.Shade(new Ink(C, 1, .4f, .85f) { Ao = 0 }); }
    }
    void Jacket(JacketDef t)
    {
        var pc = d[Slot.Jacket]; Col A = pc.A, B = pc.B, C = pc.C; float T = r.Torso, cx = po.Turn * 1.3f, hem = t.Hem; var f = TorsoPat(pc, A); var fn = JacketFn(t, pc, f);
        if (t.Cape)
        {
            float w0 = r.ShX + 1.2f, w1 = r.ShX + 7; Span<Vector2> q = stackalloc Vector2[19]; int n = 0; q[n++] = Tp(-r.Neck - 1, T + 1.2f); q[n++] = Tp(-w0, T * .9f); q[n++] = Tp(-w1, T * .35f) + HemOff(1, .6f); q[n++] = Tp(-w1 - .6f, -hem * .5f) + HemOff(2, 1.3f);
            for (int i = 0; i <= 10; i++) { float s = i / 10f; q[n++] = Tp(M.Lerp(-w1 + 1, w1 - 1, s), -hem + (t.Open > .1f ? 0 : 3 - MathF.Abs(s - .5f) * -6 - 3)) + HemOff(s * 4, 1.7f); }
            q[n++] = Tp(w1 + .6f, -hem * .5f) + HemOff(3, 1.3f); q[n++] = Tp(w1, T * .35f) + HemOff(4, .6f); q[n++] = Tp(w0, T * .9f); q[n++] = Tp(r.Neck + 1, T + 1.2f);
            p.Begin().Poly(q[..n]);
            if (t.Open > .1f) p.Cut(Tp(0, T - 3), Tp(r.Waist * .9f, -hem - 6), Tp(-r.Waist * .9f, -hem - 6)); else p.CutEllipse(Tp(0, T + 1).X, Tp(0, T + 1).Y, r.Neck + .4f, 2.2f);
            p.Shade(Cloth(fn, t.Knit, BodyUv, 4) with { Ao = 1.6f });
            for (int i = 0; i < 6; i++) { float x = M.Lerp(-.85f, .85f, i / 5f); p.Crease(Tp(x * w0 * .8f, T * .6f), Tp(x * w1, -hem + 1) + HemOff(i, 1), .45f, .84f); }
            if (t.Fringe) for (int i = 0; i <= 16; i++) { float s = i / 16f; var b = Tp(M.Lerp(-w1 + 1, w1 - 1, s), -hem + .4f) + HemOff(s * 4, 1.7f); p.Line(b, b + new Vector2(po.HemX * .4f, 2.4f), .5f, B, false); }
            return;
        }
        var nk = t.Lapel == Lapel.None && t.Open < .05f ? Neckline.V : t.Open < .15f && t.Lapel != Lapel.Notch ? Neckline.Crew : Neckline.V;
        p.Begin(); TorsoShape(t.Fit, hem, nk, hem > 8, t.Sleeve <= 0, t.RibHem ? .9f : 0, .6f + t.Fit * .3f + hem * .07f);
        // opening shows whatever is underneath
        float oT = 1.1f + t.Open * 4.2f, oB = t.Open < .15f ? -1 : t.Open * (6.4f + hem * .14f), vT = T - (t.Lapel is Lapel.Notch or Lapel.Shawl ? 9 + (t.Close == Close.Double ? -1 : 0) : 5.5f);
        if (t.Open >= .15f) { p.Cut(Tp(cx - oT, T + 2), Tp(cx + oT, T + 2), Tp(cx + M.Lerp(oT, oB, .4f), T * .45f), Tp(cx + oB, -hem - 4) + HemOff(1, 1.2f), Tp(cx - oB, -hem - 4) + HemOff(2, 1.2f), Tp(cx - M.Lerp(oT, oB, .4f), T * .45f)); }
        var k = Cloth(fn, t.Knit, BodyUv, 4.5f); k.Ao = 1.4f; if (t.Sleeve > 0) k.Merge = TorsoMerge(); p.Shade(k);
        if (t.Quilt) for (float u = -hem + 3; u < T * .9f; u += 4.2f) { p.Curve(Tp(-r.ShX - 6, u + .8f), Tp(0, u - .6f), Tp(r.ShX + 6, u + .8f), .5f, default, true, 1, .70f); if (fine) p.Curve(Tp(-r.ShX - 6, u + 2.1f), Tp(0, u + .8f), Tp(r.ShX + 6, u + 2.1f), .5f, default, true, 1, 1.1f); }
        else Folds(t.Fit, hem, 40 + d[Slot.Jacket].Id);
        float w = r.Hip + 8; if (t.RibHem) { p.Crease(Tp(-w, -hem + 2.8f), Tp(w, -hem + 2.8f), .4f, .72f); if (fine) for (float x = -w; x < w; x += 1.3f) p.Crease(Tp(x, -hem), Tp(x, -hem + 2.7f), .3f, .86f); }
        else if (fine) p.Crease(Tp(-w, -hem + 1.1f), Tp(w, -hem + 1.1f), .3f, .84f);
        if (hem > 8) for (int s = -1; s <= 1; s++) if (s != 0 || t.Open < .15f) p.Crease(Tp(s * r.Hip * .55f, -hem + 1), Tp(s * r.Hip * .45f, 3), .45f, .84f);
        // closures
        float ey = -hem + 1.5f;
        if (t.Open < .15f && t.Close != Close.Open)
        {
            p.Crease(Tp(cx + .2f, T - 2), Tp(cx + .2f, -hem), .4f, .62f);
            if (t.Close == Close.Zip) p.Line(Tp(cx - .4f, T - 1), Tp(cx - .4f, -hem + 1), .5f, C, true);
        }
        void Button(Vector2 q, Col c) { if (fine) { p.Begin().Ellipse(q.X, q.Y, .95f, .95f); p.Shade(new Ink(c, .7f, .5f, .9f) { Ao = .3f }); } else p.Dot(q.X, q.Y, c); }
        float bx = t.Open >= .15f ? M.Lerp(oT, MathF.Max(oB, 1), .5f) + 1.1f : 1.4f;
        switch (t.Close)
        {
            case Close.Buttons: case Close.Snaps: for (float u = vT - 1; u > MathF.Max(-hem + 2, -6); u -= 4.8f) Button(Tp(cx + bx, u), C); break;
            case Close.Double: for (float u = vT - 1.5f; u > MathF.Max(-hem + 3, -7); u -= 5.2f) { Button(Tp(cx + MathF.Max(bx, 1) + 2.6f, u), C); Button(Tp(cx - MathF.Max(bx, 1) - 2.6f, u), C); } break;
            case Close.Toggles: for (float u = T - 7; u > -hem + 4; u -= 7) { p.Line(Tp(cx - 3.4f, u), Tp(cx + 3.4f, u), .6f, B.Shade(.2f), false); p.Begin().Capsule(Tp(cx - 1.4f, u), .75f, Tp(cx + 1.4f, u), .75f); p.Shade(new Ink(C, .8f, .5f) { Ao = .3f }); } break;
            case Close.Zip when t.Open >= .15f: p.Line(Tp(cx + oT, T - 2), Tp(cx + oB, -hem + 1), .5f, C, false); p.Line(Tp(cx - oT, T - 2), Tp(cx - oB, -hem + 1), .5f, C, false); break;
        }
        // pockets
        if (t.Pockets >= 1) for (int s = -1; s <= 1; s += 2) { float px = s * (r.Waist * .62f + 1.6f); if (t.Pockets >= 2 && t.Close != Close.Double || hem > 6) { var q = Tp(px, hem > 8 ? -2 : 4); p.Begin().Poly(q + new Vector2(-2.6f, -2), q + new Vector2(2.6f, -2), q + new Vector2(2.6f, 2.8f), q + new Vector2(-2.6f, 2.8f)); p.IntersectAlpha(); p.Shade(new Ink(fn, 1.1f, .4f, .9f) { Ao = 0 }); p.Begin().Rect(q.X - 2.8f, q.Y - 2.4f, 5.6f, 1.6f); p.Shade(new Ink(t.Knit == Knit.Denim ? A : B, .8f, .4f, .9f) { Ao = .3f }); } else p.Line(Tp(px - s * 1.5f, 8), Tp(px + s * 1.4f, 4), .7f, A.Ink(), false); }
        if (t.Pockets >= 4) for (int s = -1; s <= 1; s += 2) { var q = Tp(s * (r.Chest * .55f + 1.5f), T * .68f); p.Begin().Rect(q.X - 2.2f, q.Y - 2, 4.4f, 4.2f); p.Shade(new Ink(fn, 1, .4f) { Ao = 0 }); p.Begin().Rect(q.X - 2.4f, q.Y - 2.3f, 4.8f, 1.5f); p.Shade(new Ink(B, .8f, .4f) { Ao = 0 }); }
        if (t.Yoke) for (int s = -1; s <= 1; s += 2) { var q = Tp(s * (r.Chest * .55f + 1), T * .7f); p.Begin().Poly(q + new Vector2(-2.4f, -1.4f), q + new Vector2(2.4f, -1.4f), q + new Vector2(2.1f, 2.6f), q + new Vector2(0, 3.4f), q + new Vector2(-2.1f, 2.6f)); p.Shade(new Ink(fn, 1, .4f, .8f) { Ao = 0 }); }
        if (t.Patch) { var q = Tp(r.Chest * .55f + 1, T * .72f); p.Begin().Rect(q.X - 1.5f, q.Y - 2.2f, 3, 4.2f); p.Shade(new Ink(C, 1, .4f) { Ao = 0 }); }
        if (t.Belt) { float bu = hem > 8 ? T * .22f : -hem + 3; float bw = ClothHalf(bu, t.Fit, -hem, hem > 8) + .5f; p.Begin().Poly(Tp(-bw, bu + 1.3f), Tp(bw, bu + 1.3f), Tp(bw, bu - 1.3f), Tp(-bw, bu - 1.3f)); p.IntersectAlpha(); p.Shade(new Ink(B, 1, .4f) { Ao = .7f }); var q = Tp(cx + 1, bu); p.Begin().Rect(q.X - 1.6f, q.Y - 1.6f, 3.2f, 3.2f); p.Shade(new Ink(C, .8f, .5f) { Ao = 0 }); if (hem > 8 && t.Lapel == Lapel.Shawl) { p.Begin().Poly(q, q + new Vector2(2.4f + po.HemX * .4f, 8), q + new Vector2(.2f + po.HemX * .4f, 8.4f)); p.Shade(new Ink(B, 1, .4f)); } }
        if (t.Fringe) for (int i = 0; i <= 14; i++) { float x = M.Lerp(-r.Chest, r.Chest, i / 14f); if (MathF.Abs(x - cx) < oT) continue; var q = Tp(x, T * .7f); p.Line(q, q + new Vector2(po.HemX * .4f, 4), .5f, B, false); }
    }
    void JacketCollar(JacketDef t)
    {
        var pc = d[Slot.Jacket]; Col A = pc.A, B = pc.B, C = pc.C; float T = r.Torso, cx = po.Turn * 1.3f, nk = r.Neck + .4f, oT = 1.1f + t.Open * 4.2f;
        void Pair(Func<int, Vector2[]> g, Ink k) { for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(g(s)); p.Shade(k); } }
        switch (t.Lapel)
        {
            case Lapel.Notch: Pair(s => [Tp(s * nk, T + 2), Tp(s * (nk + 1.8f), T + .4f), Tp(s * (nk + 2.4f), T - 3.2f), Tp(s * (nk + 4.4f), T - 4.4f), Tp(cx + s * (oT + .8f), T - 12), Tp(cx + s * oT * .9f, T - 11)], new Ink(t.Name.Contains("Peacoat") || t.Name.Contains("Moto") ? A.Shade(-.1f) : A, 1.4f, .45f) { Ao = .8f }); break;
            case Lapel.Shawl: Pair(s => [Tp(s * nk, T + 2), Tp(s * (nk + 2.6f), T + .2f), Tp(s * (nk + 3), T - 6), Tp(cx + s * (oT + 1.6f), T * .3f), Tp(cx + s * oT * .9f, T * .3f), Tp(cx + s * oT, T - 6)], new Ink(B, 1.4f, .45f) { Ao = .8f }); break;
            case Lapel.Shearling: case Lapel.Fur: { p.Begin(); int N = t.Lapel == Lapel.Fur ? 7 : 5; for (int s = -1; s <= 1; s += 2) for (int i = 0; i < N; i++) { float q = i / (N - 1f); var v = Tp(s * M.Lerp(nk + .4f, nk + 2.8f, M.Sin(q * 2.4f)), T + 2.6f - q * 8.5f); p.Ellipse(v.X, v.Y, 2, 1.9f); } p.Shade(new Ink(B, 1.8f, .55f) { Ao = 1, Grain = .12f }); break; }
            case Lapel.Stand: Pair(s => [Tp(s * (nk + .2f), T + 3.2f), Tp(s * (nk + 2), T + .6f), Tp(cx + s * (oT * .7f + .8f), T - 3.4f), Tp(cx + s * oT * .7f, T - .4f), Tp(s * (nk - 1.4f), T + 1.6f)], new Ink(t.ContrastSleeve ? B : A, 1.4f, .5f) { Ao = .8f }); break;
            case Lapel.Bomber: Pair(s => [Tp(s * nk, T + 2.2f), Tp(s * (nk + 1.5f), T + .5f), Tp(cx + s * (oT + .8f), T - 4.4f), Tp(cx + s * (oT - .5f), T - 4), Tp(s * (nk - .8f), T)], new Ink(B, 1, .4f) { Ao = .6f }); break;
            case Lapel.Camp: case Lapel.Peter: { bool pt = t.Lapel == Lapel.Peter; Pair(s => [Tp(s * (nk - .2f), T + 2.2f), Tp(s * (nk + 2.2f), T + .6f), Tp(s * (nk + 4.2f), T - 3.4f), Tp(s * (nk + 2.6f), T - 5), Tp(cx + s * MathF.Min(oT, 2.4f), T - 3.2f)], new Ink(pt ? B : A, 1.2f, .45f) { Ao = .8f }); break; }
            case Lapel.Sailor: Pair(s => [Tp(s * (nk - .2f), T + 1.8f), Tp(s * (nk + 5.4f), T - .4f), Tp(s * (nk + 4.8f), T - 5), Tp(cx, T - 10)], new Ink(B, 1.2f, .45f) { Ao = .8f }); for (int s = -1; s <= 1; s += 2) p.Line(Tp(s * (nk + 4.2f), T - 4.4f), Tp(cx + s * .4f, T - 9), .4f, C, false); break;
            case Lapel.Hood: HoodCollar(A, B, C, (x, y) => A); break;
        }
    }

    // ---------------------------------------------------------------- back & neck
    void BackBody()
    {
        var pc = d[Slot.Back]; if (pc.Id == 0) return; Col A = pc.A, B = pc.B, C = pc.C; float T = r.Torso, w = r.ShX;
        switch (pc.Id)
        {
            case 1: case 7: case 2:
                float ex = pc.Id == 2 ? 4 : 2.2f, top = pc.Id == 2 ? T + 8 : T - 1; p.Begin().Blob(Tp(-w - ex, 3), Tp(-w - ex + .5f, T * .6f), Tp(-w + 2, top), Tp(0, top + 1), Tp(w - 2, top), Tp(w + ex - .5f, T * .6f), Tp(w + ex, 3), Tp(0, 0)); p.Shade(new Ink(A, 3, .5f)); if (pc.Id == 2) { p.Begin().Capsule(Tp(-w + 1, T + 9), 3, Tp(w - 1, T + 9), 3); p.Shade(new Ink(B, 2, .5f)); p.Line(Tp(-5, T + 12), Tp(-5, T + 6), 1, C); p.Line(Tp(5, T + 12), Tp(5, T + 6), 1, C); }
                if (pc.Id == 7) for (int s = -1; s <= 1; s += 2) { var e = Tp(s * (w + .5f), T - 1); p.Begin().Ellipse(e.X, e.Y, 2.6f, 2.6f); p.Shade(new Ink(A, 2, .5f)); p.Begin().Ellipse(e.X + s * .3f, e.Y, 1.3f, 1.5f); p.Flat(0xf6f0e4); p.Dot(e.X + s * .4f, e.Y, 0x2a1a1c); }
                break;
            case 5: { p.Begin().Poly(Tp(w - 1, 12), Tp(w + 7, 12), Tp(w + 6.4f, 1), Tp(w, 1)); Col aa = A; p.Shade(new Ink((x, y) => (((int)(x / 1.3f) + (int)(y / 1.3f)) & 1) == 0 ? aa : aa.Shade(-.2f), 2, .5f)); p.Begin().Rect(Tp(w - 1.4f, 13.4f).X, Tp(0, 13.4f).Y, 8.8f, 2.4f); p.Shade(new Ink(B, 1, .4f)); break; }
            case 8: { var a = Tp(-w - 2, -4); var b = Tp(w * .6f, T + 18); p.Begin().Capsule(a, 6, Vector2.Lerp(a, b, .4f), 5).Capsule(Vector2.Lerp(a, b, .4f), 4, b, 2.2f); p.Shade(new Ink(A, 2.5f, .5f)); break; }
        }
    }
    void BackStraps()
    {
        var pc = d[Slot.Back]; if (pc.Id == 0) return; Col A = pc.A, B = pc.B, C = pc.C; float T = r.Torso, w = r.ShX;
        void Cross(Col c) { p.Begin().Poly(Tp(-r.Neck - 3.6f, T + .4f), Tp(-r.Neck - 1.4f, T + 1), Tp(r.Hip + 1.2f, 2.5f), Tp(r.Hip + .6f, -.6f)); p.IntersectAlpha(); p.Shade(new Ink(c, 1, .4f) { Ao = .6f }); }
        void Bag(float bw, float bh, Col c, Col fl) { var q = Tp(r.Hip + 1.5f, -1) + new Vector2(po.HemX * .3f, 0); p.Begin().Blob(q + new Vector2(-bw, -bh * .4f), q + new Vector2(bw, -bh * .4f), q + new Vector2(bw * 1.05f, bh * .6f), q + new Vector2(-bw * 1.05f, bh * .6f)); p.Shade(new Ink(c, 2, .5f)); p.Begin().Poly(q + new Vector2(-bw, -bh * .42f), q + new Vector2(bw, -bh * .42f), q + new Vector2(bw * .8f, bh * .12f), q + new Vector2(-bw * .8f, bh * .12f)); p.Shade(new Ink(fl, 1.2f, .4f) { Ao = .4f }); p.Begin().Ellipse(q.X, q.Y + bh * .1f, .9f, .9f); p.Shade(new Ink(C, .6f, .5f) { Ao = 0 }); }
        switch (pc.Id)
        {
            case 1: case 2: case 7: for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(Tp(s * (r.Neck + 2), T + .6f), Tp(s * (r.Neck + 4.6f), T), Tp(s * (w - 1.6f), T * .62f), Tp(s * (w - 1), T * .3f), Tp(s * (w - 3), T * .3f), Tp(s * (w - 4), T * .62f)); p.IntersectAlpha(); p.Shade(new Ink(B, 1, .45f) { Ao = .5f }); } if (pc.Id == 2) { p.Begin().Poly(Tp(-w + 3, T * .58f), Tp(w - 3, T * .58f), Tp(w - 3, T * .52f), Tp(-w + 3, T * .52f)); p.Shade(new Ink(C, 1, .4f) { Ao = .4f }); } break;
            case 3: Cross(B); Bag(5, 8, A, A.Shade(-.1f)); break;
            case 4: { p.Begin().Poly(Tp(w - 3, T * .95f), Tp(w - 1.4f, T * .95f), Tp(w + 2.4f, 7), Tp(w + .6f, 7)); p.Shade(new Ink(B, 1, .4f)); var q = Tp(w + 1, 1); p.Begin().Poly(q + new Vector2(-5, -5), q + new Vector2(5, -5), q + new Vector2(5.4f, 7), q + new Vector2(-5.4f, 7)); p.Shade(new Ink(A, 2, .4f)); FishIcon(q + new Vector2(0, 1), 3.6f, C, A); break; }
            case 5: Cross(C); break;
            case 6: Cross(B); Bag(3.6f, 5, A, B); break;
            case 8: Cross(C); break;
            case 9: Cross(B); Bag(6.4f, 7, A, B); break;
        }
    }
    void Neckwear()
    {
        var pc = d[Slot.Neck]; if (pc.Id == 0) return; Col A = pc.A, B = pc.B, C = pc.C; float T = r.Torso, nk = r.Neck, cx = po.Turn * 1.2f;
        Func<float, float, Col> sf = pc.Pat == 0 ? (x, y) => A : (x, y) => Patterns.Get(pc.Pat, x - P.X, y - P.Y, A, B, C, pc.Scale * .8f);
        void Tail(float x, float len, float w, bool fr)
        {
            var a = Tp(x, T - 1); var sw = new Vector2(po.HemX * 1.1f + po.HairX * .6f + M.Sin(po.T * 5) * po.Wind, po.HemY * .6f); var e = a + new Vector2(0, len) + sw * len * .12f; var m = a + new Vector2(sw.X * len * .03f, len * .5f);
            p.Begin().Poly(a + new Vector2(-w * .8f, 0), a + new Vector2(w * .8f, 0), m + new Vector2(w, 0), e + new Vector2(w * 1.05f, 0), e + new Vector2(-w * 1.05f, .5f), m + new Vector2(-w, 0)); p.Shade(new Ink(sf, 1.6f, .45f) { Ao = 1 }); if (fr) for (int i = -2; i <= 2; i++) p.Line(e + new Vector2(i * w * .45f, 0), e + new Vector2(i * w * .5f + sw.X * .1f, 2), .5f, B, false); p.Crease(m + new Vector2(-w * .2f, -len * .3f), e + new Vector2(-w * .2f, -1), .4f, .85f);
        }
        switch (pc.Id)
        {
            case 1: case 2: case 3: { float k = pc.Id == 3 ? 1.5f : 1; Tail(3.2f + cx, pc.Id == 2 ? 24 : 13, 2.6f * k, true); if (pc.Id == 2) Tail(-2.4f + cx, 19, 2.6f, true); p.Begin().Blob(Tp(-nk - 2.4f * k, T + 2.6f), Tp(0, T + 2 * k), Tp(nk + 2.4f * k, T + 2.6f), Tp(nk + 3 * k, T - .6f), Tp(cx, T - 3.6f * k), Tp(-nk - 3 * k, T - .6f)); p.Shade(new Ink(sf, 2, .5f)); p.Curve(Tp(-nk - 2, T + 1), Tp(cx, T - 1.6f), Tp(nk + 2, T + 1), .5f, default, true, 1, .76f); break; }
            case 4: case 11: { p.Begin().Poly(Tp(-nk - 1.4f, T + 2), Tp(nk + 1.4f, T + 2), Tp(nk + 2, T - .6f), Tp(cx, T - (pc.Id == 4 ? 8 : 5)), Tp(-nk - 2, T - .6f)); p.Shade(new Ink(sf, 1.6f, .45f) { Ao = 1 }); if (pc.Id == 11) { var q = Tp(cx, T - 3.4f); p.Begin().Poly(q, q + new Vector2(-2.8f, 3.4f), q + new Vector2(-.4f, 3)).Poly(q, q + new Vector2(2.8f, 3.4f), q + new Vector2(.4f, 3)).Ellipse(q.X, q.Y, 1.2f, 1.1f); p.Shade(new Ink(B, 1, .4f)); } break; }
            case 5: case 12: { p.Curve(Tp(-nk - .6f, T + 1), Tp(cx, T - (pc.Id == 12 ? 22 : 13)), Tp(nk + .6f, T + 1), pc.Id == 12 ? 1 : .5f, pc.Id == 12 ? C : A, false); var q = Tp(cx, T - (pc.Id == 12 ? 11 : 6.4f)) + new Vector2(po.HemX * .25f, 0); if (pc.Id == 5) { p.Begin().Poly(q + new Vector2(0, -1.6f), q + new Vector2(1.4f, 0), q + new Vector2(0, 2.2f), q + new Vector2(-1.4f, 0)); p.Shade(new Ink(B, 1, .6f)); } else { p.Begin().Rect(q.X - 3.4f, q.Y - 1.8f, 6.8f, 4.6f); p.Shade(new Ink(A, 1.2f, .5f)); p.Begin().Ellipse(q.X, q.Y + .5f, 1.7f, 1.7f); p.Shade(new Ink(B, 1, .5f)); p.Dot(q.X - .4f, q.Y, 0xffffff); } break; }
            case 6: case 7: { for (int i = 0; i < 11; i++) { float s = i / 10f; var q = Tp(M.Lerp(-nk - .6f, nk + .6f, s), T + .8f - M.Sin(s * M.PI) * (pc.Id == 6 ? 3.4f : 6)); p.Begin().Ellipse(q.X, q.Y, .8f, .8f); p.Shade(new Ink(pc.Id == 6 ? A : (i % 3 == 0 ? A : i % 3 == 1 ? B : C), .7f, .6f, .7f) { Ao = 0 }); } break; }
            case 8: { var q = Tp(cx, T - 1.6f); p.Begin().Poly(q + new Vector2(-1.2f, 1.4f), q + new Vector2(1.2f, 1.4f), q + new Vector2(2 + po.HemX * .3f, 12), q + new Vector2(po.HemX * .3f, 14.4f), q + new Vector2(-2 + po.HemX * .3f, 12)); p.Shade(new Ink(pc.Pat == 0 ? (x, y) => ((int)MathF.Floor((x + y) / 2) % 3 == 0 ? B : A) : sf, 1.2f, .4f)); p.Begin().Poly(q + new Vector2(-1.6f, -1.2f), q + new Vector2(1.6f, -1.2f), q + new Vector2(1, 1.8f), q + new Vector2(-1, 1.8f)); p.Shade(new Ink(A, 1, .4f)); break; }
            case 9: { var q = Tp(cx, T - 1.4f); p.Begin().Poly(q, q + new Vector2(-4, -2), q + new Vector2(-4, 2)).Poly(q, q + new Vector2(4, -2), q + new Vector2(4, 2)); p.Shade(new Ink(sf, 1.2f, .45f)); p.Begin().Ellipse(q.X, q.Y, 1.1f, 1.2f); p.Shade(new Ink(B, .8f, .4f)); break; }
            case 10: p.Begin().Poly(Tp(-nk, T + 2.2f), Tp(nk, T + 2.2f), Tp(nk, T + .8f), Tp(0, T + .3f), Tp(-nk, T + .8f)); p.Shade(new Ink(A, .8f, .4f) { Ao = 0 }); { var q = Tp(cx, T); p.Dot(q.X, q.Y, B); } break;
        }
    }
    void NeckwearFront() { }
}

static class PainterExt { public static Painter Limb2(this Painter p, AvatarPainter a, Vector2 x, Vector2 y, Vector2 z) { a.Limb2Impl(x, y, z); return p; } }

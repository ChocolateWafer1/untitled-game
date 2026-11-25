using System.Numerics;
namespace UntitledGame;

/// The pieces that only certain haircuts have: hawks, spikes, rolls, knots, braids, razor lines, tiers.
public sealed partial class AvatarPainter
{
    bool Fx(HairFx f) => (hd.Fx & f) != 0;

    /// Styles that are mostly bare scalp with one big feature on top. Returns true when nothing else should be drawn.
    bool Hawk(float T, float fx)
    {
        if (hatCover) return false; float rx = r.HeadRx, ry = r.HeadRy;
        switch (hd.U)
        {
            case Updo.Liberty:
                {
                    int[] order = [0, 6, 1, 5, 2, 4, 3]; foreach (int i in order)
                    {
                        float an = M.Lerp(-1.02f, 1.02f, i / 6f), sn = MathF.Sin(an), cs = MathF.Cos(an); var sw = HairSway(.5f, i); Vector2 a = H(sn * .52f, -.50f - cs * .26f), tip = H(sn * (1.0f + T * .85f), -.2f - cs * (1.0f + T)) + sw, pp = Vector2.Normalize(new(-(tip - a).Y, (tip - a).X));
                        p.Begin().Poly(a + pp * rx * .19f, Vector2.Lerp(a, tip, .55f) + pp * rx * .13f, tip, Vector2.Lerp(a, tip, .55f) - pp * rx * .13f, a - pp * rx * .19f); p.Shade(HairInk(i * 3 + 1, false, i % 2 == 0 ? .03f : -.04f, 2)); StrandLines(a, Vector2.Lerp(a, tip, .5f), tip, .76f); StrandLines(a + pp * rx * .08f, Vector2.Lerp(a, tip, .5f) + pp * rx * .06f, tip, 1.18f);
                    }
                    return true;
                }
            case Updo.DeathHawk:
                {
                    for (int i = 0; i < 17; i++) { float u = ((i * 7) % 17) / 16f * 2 - 1, h1 = M.Hash(i, 3); var a = H(u * .42f, -.60f - (1 - u * u) * .12f); var m = H(u * .86f, -1.06f - T * .5f); var b = H(u * 1.12f + (h1 - .5f) * .3f, -1.0f - T * (1.12f - u * u * .62f) + (M.Hash(i, 5) - .5f) * .16f) + HairSway(.9f, i); p.Begin(); Lock(a, m, b, rx * .17f, rx * .21f, rx * .05f, i); p.Shade(HairInk(i + 30, false, (h1 - .5f) * .16f)); StrandLines(a, m, b, .74f); }
                    p.Begin().Ellipse(H(0, -.68f).X, H(0, -.64f).Y, rx * .40f, ry * .10f); p.Shade(HairInk()); return true;
                }
            case Updo.FroHawk:
                {
                    p.Begin(); for (int i = 0; i < 30; i++) { float u = i % 5 / 4f * 2 - 1, v = i / 5 / 5f; var c = H(u * (.40f + v * .12f), -.62f - v * (.5f + T * .7f) - (1 - u * u) * .12f * (1 + v)) + HairSway(.2f + v * .4f, i); p.Ellipse(c.X, c.Y, rx * .24f, rx * .22f); } p.Ellipse(H(0, -.85f).X, H(0, -.85f).Y, rx * .44f, ry * .36f); p.Shade(HairInk(-1, false, 0, 3.4f));
                    if (fine) for (int i = 0; i < 12; i++) { var c = H((M.Hash(i, 2) - .5f) * .9f, -.66f - M.Hash(i, 4) * (.4f + T * .7f)); p.Curve(c + new Vector2(-1.2f, .3f), c + new Vector2(0, -1.5f), c + new Vector2(1.2f, .3f), .4f, default, true, 1, i % 3 == 0 ? 1.22f : .76f); }
                    return true;
                }
            case Updo.Chelsea:
                for (int i = 0; i < 7; i++) { float u = i / 6f * 2 - 1; p.Begin(); Lock(H(u * .46f, -.78f + MathF.Abs(u) * .1f), H(u * .62f + fx, -.62f), H(u * .70f + fx + (M.Hash(i, 2) - .5f) * .1f, -.34f + M.Hash(i, 6) * .1f + MathF.Abs(u) * .08f) + HairSway(.4f, i), rx * .17f, rx * .16f, rx * .05f, i); p.Shade(HairInk(i + 40)); }
                return true;
        }
        return false;
    }

    /// Details painted onto the hair itself, once the skull cap is down and its mask is still active.
    void CapDetails(float fx, float hl, float T)
    {
        float rx = r.HeadRx; Vector2 Q(float x, float y) => H(x + fx * .5f, y);
        if (Fx(HairFx.Waves360)) for (int k = 0; k < 6; k++) { float y = -.50f - k * .11f, w = .95f - k * .07f; p.Curve(Q(-w, y + .24f), Q(0, y - .14f), Q(w, y + .24f), .5f, default, true, 1, .66f); p.Curve(Q(-w, y + .20f), Q(0, y - .18f), Q(w, y + .20f), .45f, default, true, 1, 1.5f); }
        if (Fx(HairFx.FingerWaves)) for (int k = 0; k < 6; k++) { float y = -.98f + k * .155f; for (int j = 0; j < 6; j++) { float x0 = -1.02f + j * .34f, bow = (j & 1) == 0 ? .12f : -.12f; p.Curve(Q(x0, y), Q(x0 + .17f, y + bow), Q(x0 + .34f, y), .7f, default, true, 1, .62f); p.Curve(Q(x0, y - .06f), Q(x0 + .17f, y - .06f + bow), Q(x0 + .34f, y - .06f), .6f, default, true, 1, 1.14f); } }
        if (Fx(HairFx.Rows)) for (int i = -3; i <= 3; i++) { p.Curve(Q(i * .22f, hl), H(i * .26f, -.85f), H(i * .2f, -1.05f), .6f, default, true, 1, .62f); p.Curve(Q(i * .22f + .1f, hl), H(i * .26f + .1f, -.85f), H(i * .2f + .06f, -1.05f), .4f, default, true, 1, 1.25f); }
        if (Fx(HairFx.TwoRows)) { p.Line(Q(0, hl - .02f), H(0, -1.06f - T), .5f, Col.Lerp(skin, d.Hair.Base, .35f)); for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 6; i++) { float t = i / 5f; var c = H(s * M.Lerp(.28f, .86f, t) + fx * .5f * (1 - t), M.Lerp(-.66f, -.12f, t * t)); p.Begin().Ellipse(c.X, c.Y, rx * .17f, rx * .13f); p.Shade(HairInk(-1, false, (i & 1) == 0 ? -.07f : .07f, 1.6f)); } }
        if (Fx(HairFx.CenterPart)) p.Line(Q(0, hl + .02f), H(0, -1.02f - T), .55f, Col.Lerp(skin, d.Hair.Base, .3f));
    }
    /// Drawn last, over the fringe.
    void FrontDetails(float fx, float T)
    {
        float rx = r.HeadRx; if (Fx(HairFx.Lightning) && !hatCover) for (int s = -1; s <= 1; s += 2) { Col c = skin.Shade(.22f); Vector2[] z = [H(s * .76f, -.62f), H(s * .93f, -.50f), H(s * .82f, -.44f), H(s * .99f, -.28f), H(s * .88f, -.23f), H(s * .96f, -.10f)]; for (int i = 0; i + 1 < z.Length; i++) p.Line(z[i], z[i + 1], fine ? .8f : 1.1f, c, false); }
        if (hd.U == Updo.VictoryRolls && !hatCover) for (int s = -1; s <= 1; s += 2)
            {
                var c = H(s * .58f + fx * .5f, -.90f) + HairSway(.12f, s); float R = rx * .42f; p.Begin().Ellipse(c.X, c.Y, R * 1.12f, R * .96f); p.Shade(HairInk(-1, false, .02f, R * .8f)); var sp = new Vector2[22]; for (int k = 0; k < 22; k++) { float t = k / 21f, an = s * (t * 9.4f + 1.2f), q = M.Lerp(.86f, .10f, t) * R; sp[k] = c + new Vector2(M.Sin(an) * q * 1.12f, -M.Cos(an) * q * .96f); } p.Begin().Ellipse(c.X, c.Y, R * 1.12f, R * .96f); p.Stroke(sp, .6f, default, 1, true, .58f);
            }
        if (hd.U == Updo.BantuKnots && !hatCover) foreach (var (x, y, q) in new[] { (-.60f, -.64f, .20f), (0f, -.80f, .21f), (.60f, -.64f, .20f) }) Knot(H(x + fx * .6f, y), rx * q);
        if (hd.U == Updo.VikingBraid) Braid(H(.70f, .86f), H(.86f, 2.5f) + HairSway(2), 2.8f, 1.6f, 9); if (hd.U == Updo.RatTail) Braid(H(.60f, .92f), H(.70f, 2.3f) + HairSway(2), 1.3f, .8f, 11);
        if (Fx(HairFx.Tier)) for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 3; i++) { p.Begin(); Lock(H(s * (.86f + i * .11f), .60f), H(s * (.98f + i * .10f), 1.3f), H(s * (.78f + i * .17f), 2.5f - i * .22f) + HairSway(2.2f, s * 3 + i), rx * .13f, rx * .12f, rx * .03f, i); p.Shade(HairInk(i + s * 4 + 50, true, -.06f)); }
        if (Fx(HairFx.HardPart) && !hatCover) p.Line(H(.50f + fx * .5f, -.60f), H(.74f, -.96f), fine ? .8f : 1f, skin.Shade(.05f), false);
        if (hd.U == Updo.SideBraid) { var a = H(-.86f, .5f); Braid(a, H(-.80f, 2.15f) + HairSway(2), 3.3f, 1.9f, 9); }
        if (Fx(HairFx.BeadBraids)) for (int s = -1; s <= 1; s += 2) { var a = H(s * .52f + fx, -.50f); var b = H(s * .90f, .72f) + HairSway(1.2f, s); p.Begin(); Lock(a, H(s * .98f + fx * .4f, -.2f), b, 1, 1, 1, s + 5); p.Shade(HairInk(s + 60, false, .05f)); foreach (var (t, col) in new[] { (.62f, 0xe8b84a), (.82f, 0xf3ead8), (1f, 0xe8b84a) }) { var q = H(s * M.Lerp(.78f, .90f, t), M.Lerp(-.1f, .72f, t)) + HairSway(1.2f * t, s); p.Begin().Ellipse(q.X, q.Y, 1.15f, 1); p.Shade(new Ink(col, .8f, .7f, .7f) { Ao = 0 }); } }
    }
    void Knot(Vector2 c, float R)
    {
        p.Begin().Ellipse(c.X, c.Y, R, R * .92f); p.Shade(HairInk(-1, false, .04f, R * .9f)); p.Curve(c + new Vector2(-R * .8f, R * .1f), c + new Vector2(0, -R * .9f), c + new Vector2(R * .7f, 0), .5f, default, true, 1, .64f); p.Curve(c + new Vector2(-R * .5f, R * .5f), c + new Vector2(R * .3f, R * .4f), c + new Vector2(R * .3f, -R * .2f), .5f, default, true, 1, .7f);
    }
    void Braid(Vector2 a, Vector2 b, float w0, float w1, int n)
    {
        for (int i = 0; i <= n; i++) { float t = i / (float)n; var q = Vector2.Lerp(a, b, t); q.X += M.Sin(t * 3) * .8f; float w = M.Lerp(w0, w1, t); p.Begin().Ellipse(q.X + ((i & 1) == 0 ? w * .26f : -w * .26f), q.Y, w, w * .85f); p.Shade(HairInk(-1, false, (i & 1) == 0 ? -.06f : .05f, 1.8f)); }
        Tie(Vector2.Lerp(a, b, 1.04f), w1 * .85f); p.Begin(); var e = Vector2.Lerp(a, b, 1.06f); p.Poly(e + new Vector2(-w1 * .8f, 0), e + new Vector2(w1 * .8f, 0), e + new Vector2(w1 * .3f, w1 * 2), e + new Vector2(-w1 * .4f, w1 * 1.8f)); p.Shade(HairInk());
    }
    /// Gathered hair that hangs or rises behind the head.
    void WildUpdos(bool c)
    {
        float rx = r.HeadRx; switch (hd.U)
        {
            case Updo.BubblePony:
                {
                    Vector2 A = c ? H(.5f, .6f) : H(.40f, -1.10f), Mi = c ? H(1.1f, .8f) : H(1.75f, -1.10f), B = H(1.46f, c ? 2.1f : 1.5f); Vector2 At(float t) => (1 - t) * (1 - t) * A + 2 * t * (1 - t) * Mi + t * t * B + HairSway(.4f + t * 1.6f, 3); p.Begin(); for (int k = 0; k <= 16; k++) { var q = At(k / 16f); p.Ellipse(q.X, q.Y, rx * .12f, rx * .12f); } p.Shade(HairInk(-1, false, -.2f)); // the bound core that shows between the bubbles
                    for (int i = 4; i >= 0; i--) { float t = .10f + i * .215f, w = rx * (.31f - i * .026f); var q = At(t); p.Begin().Ellipse(q.X, q.Y, w, w * .98f); p.Shade(HairInk(-1, false, -.03f, w * .8f)); p.Curve(q + new Vector2(-w * .5f, -w * .7f), q + new Vector2(-w * .75f, 0), q + new Vector2(-w * .4f, w * .7f), .45f, default, true, 1, .78f); p.Curve(q + new Vector2(w * .3f, -w * .8f), q + new Vector2(w * .55f, 0), q + new Vector2(w * .2f, w * .8f), .45f, default, true, 1, .8f); if (i > 0) Tie(At(t - .105f), .95f); }
                    break;
                }
            case Updo.BraidPony: { if (!c) { Puff(.36f, -1.08f, rx * .28f, 1); Tie(H(.5f, -1.0f), 1.8f); } var a = c ? H(.6f, .6f) : H(.78f, -1.0f); var sw = HairSway(2.2f); for (int i = 0; i <= 15; i++) { float t = i / 15f; var q = (1 - t) * (1 - t) * a + 2 * t * (1 - t) * H(1.5f, c ? .8f : -.75f) + t * t * H(1.22f, c ? 2.2f : 1.6f) + sw * t; float w = M.Lerp(2.9f, 1.6f, t); p.Begin().Ellipse(q.X + ((i & 1) == 0 ? .8f : -.8f), q.Y, w, w * .85f); p.Shade(HairInk(-1, false, (i & 1) == 0 ? -.07f : .05f, 1.8f)); } Tie(H(1.22f, c ? 2.2f : 1.6f) + sw + new Vector2(0, 1.2f), 1.3f); break; }
            case Updo.HalfSpaceBuns: if (!c) { Puff(-.62f, -1.0f, rx * .34f, 1); Puff(.62f, -1.0f, rx * .34f, 2); } break;
            case Updo.VikingBraid: case Updo.RatTail: if (po.Back) Braid(H(0, .5f), H(0, hd.U == Updo.RatTail ? 2.0f : 2.4f) + HairSway(2), hd.U == Updo.RatTail ? 1.35f : 3, hd.U == Updo.RatTail ? .8f : 1.7f, 9); break;
            case Updo.Odango: for (int s = -1; s <= 1; s += 2) { var sw = HairSway(2.4f, s); if (c) Tail(H(s * .9f, .5f), H(s * 1.4f, .8f) + sw * .4f, H(s * 1.3f, 2.5f) + sw, rx * .2f, rx * .26f, rx * .06f); else { Tail(H(s * .78f, -.95f), H(s * 1.6f, -.3f) + sw * .4f, H(s * 1.5f, 2.3f) + sw, rx * .17f, rx * .25f, rx * .05f); Puff(s * .72f, -1.02f, rx * .36f, s + 2); } } break;
            case Updo.BantuKnots: if (!c) foreach (var (x, y, q) in new[] { (-.34f, -1.04f, .21f), (.34f, -1.04f, .21f), (-.92f, -.62f, .2f), (.92f, -.62f, .2f) }) Knot(H(x, y), rx * q); break;
            case Updo.Beehive:
                {
                    if (c) { Puff(.4f, .85f, rx * .42f, 1); break; } float t = hTop; var sw = HairSway(.2f); p.Begin().Blob(H(-.98f, -.4f), H(-1.06f, -1.0f), H(-.72f, -1.2f - t) + sw * .6f, H(0, -1.42f - t) + sw, H(.72f, -1.2f - t) + sw * .6f, H(1.06f, -1.0f), H(.98f, -.4f)); p.Shade(HairInk(-1, false, .02f, 5)); for (int k = 0; k < 5; k++) { float y = -.96f - k * (t + .3f) * .2f, w = 1.0f - k * .11f; p.Curve(H(-w, y + .1f), H(0, y - .2f), H(w, y + .02f), .5f, default, true, 1, k % 2 == 0 ? .74f : 1.16f); }
                    break;
                }
        }
        if (Fx(HairFx.Spikes) && !c)
        {
            // loose spikes breaking out of the silhouette all the way round
            int n = 11; for (int i = 0; i < n; i++) { int k = (i * 5) % n; float an = M.Lerp(-2.0f, 2.0f, k / (n - 1f)), sn = MathF.Sin(an), cs = MathF.Cos(an), q = .30f + M.Hash(k, 7) * .22f; var a = H(sn * .58f, -cs * .6f - .1f); var b = H(sn * (1.14f + q) + .04f, -cs * (1.06f + hTop * .8f + q * .7f) + (cs < 0 ? .04f : -.04f)) + HairSway(.6f, k); var m = Vector2.Lerp(a, b, .6f) + new Vector2(-(b - a).Y, (b - a).X) * .12f; p.Begin(); Lock(a, m, b, rx * .34f, rx * .24f, .1f, k); p.Shade(HairInk(k + 10, false, -.04f)); StrandLines(a, m, b, .76f); }
        }
    }
    /// A short bell of hair over a long thin under-layer.
    void Tier()
    {
        float rx = r.HeadRx; p.Begin(); var c = H(0, -.1f); p.Ellipse(c.X, c.Y, rx * 1.12f, r.HeadRy * .98f); for (int i = 0; i < 7; i++) { float u = i / 6f * 2 - 1; var sw = HairSway(.8f, i); Lock(H(u * .78f, -.45f), H(u * 1.3f, .2f), H(u * 1.2f, .78f - u * u * .06f) + sw, rx * .36f, rx * .42f, rx * .38f, i); }
        HairClip(); p.Shade(HairInk(-1, true, -.08f, 3)); for (int i = 0; i < 6; i++) { float u = (i + .5f) / 6 * 2 - 1; StrandLines(H(u * .8f, -.2f), H(u * 1.25f, .25f), H(u * 1.16f, .72f), .74f); }
    }
}

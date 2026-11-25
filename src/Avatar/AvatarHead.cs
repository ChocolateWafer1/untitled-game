using System.Numerics;
namespace UntitledGame;

public sealed partial class AvatarPainter
{
    HairDef hd; HatDef hatd; bool hatCover; float hTop, hSide; int strand;

    void HairSetup()
    {
        hd = Catalog.Hair[show == Show.Mannequin && only != null ? Catalog.Hair.Length - 1 : d.Hair.Id];
        hatd = Catalog.Hats[Vis(Slot.Hat) ? d[Slot.Hat].Id : 0]; hatCover = hatd.Cover;
        // a hat presses the hair down: volume shrinks by how squashable the style is
        float press = hatd.K == HatKind.None ? 0 : hd.Flat * hatd.Flat;
        hTop = hd.Top * (1 - press); hSide = hd.SideVol * (1 - press * .35f);
        if (hatCover) hTop = MathF.Min(hTop, .07f);
    }

    Col HairC(float x, float y, int id, bool back)
    {
        var h = d.Hair; float hx = (x - HC.X) / r.HeadRx, hy = (y - HC.Y) / r.HeadRy;
        Col c = h.Base; bool right = hx > 0;
        if (h.SplitOn && right) c = h.Split;
        if (h.UnderOn && (back || hy > .42f)) c = h.Under;
        int sid = id >= 0 ? id : (int)MathF.Floor(MathF.Atan2(hx, 1.6f - hy) * 13 + 60);
        if (h.HighlightAmt > 0.01f && M.Hash(sid, 17) < h.HighlightAmt * .62f && hy > -1.2f + M.Hash(sid, 5) * .5f) c = Col.Lerp(c, h.Highlight, .82f);
        if (h.Roots > .01f) { float ry = -.98f + h.Roots * .75f + (M.Hash(sid, 8) - .5f) * .22f; if (hy < ry) c = Col.Lerp(c, h.RootCol, hy < ry - .12f ? 1 : .55f); }
        if (h.Ombre > .01f) { float L = MathF.Max(hd.BackLen, MathF.Max(hd.SideLen, .15f)); if (hd.U is Updo.HighPony or Updo.LowPony or Updo.BraidTails or Updo.Pigtails or Updo.SidePony) L = 1.9f; float t = (hy + 1.05f) / (L + 1.05f), e = 1 - h.Ombre * .85f + (M.Hash(sid, 9) - .5f) * .14f; c = Col.Lerp(c, h.OmbreCol, M.Smooth(e - .12f, e + .16f, t)); }
        return c;
    }
    Ink HairInk(int id = -1, bool back = false, float dark = 0, float round = 2.6f)
    {
        int tex = hd.Tex;
        var k = new Ink((x, y) => HairC(x, y, id, back), round, .5f) { Ao = .8f, Grad = .16f };
        k.Tone = (x, y) =>
        {
            float hx = (x - HC.X) / r.HeadRx, hy = (y - HC.Y) / r.HeadRy, t = dark;
            float an = MathF.Atan2(hx, 1.6f - hy);
            if (tex <= 1 && fine) t += M.Noise(an * 26, hy * 1.2f, 3) * .13f;
            if (tex == 2 && fine) t += M.Sin(hx * 9 + M.Sin(hy * 8) * 2) * .07f;
            if (tex == 3) t += (M.Hash((int)MathF.Floor(x * (fine ? 1.4f : .9f)), (int)MathF.Floor(y * (fine ? 1.4f : .9f)), 4) - .5f) * .24f;
            if (tex <= 1 && !back) { float b = hy + .60f + hx * hx * .28f; t += .30f * MathF.Exp(-b * b * 60) * (.6f + .4f * M.Noise(an * 18, 0, 8)); }
            return t;
        };
        return k;
    }
    void HairClip()
    {
        if (!hatCover) return;
        Vector2 a = H(-3, -.38f), b = H(3, -.38f), c = H(-1.12f, -.38f), e = H(1.12f, -.38f); float up = r.HeadRy * 3;
        p.Cut(a, c, c - new Vector2(0, up), a - new Vector2(0, up)); p.Cut(e, b, b - new Vector2(0, up), e - new Vector2(0, up));
        var q = H(0, -1.1f); p.Cut(q + new Vector2(-40, 0), q + new Vector2(40, 0), q + new Vector2(40, -40), q + new Vector2(-40, -40));
    }
    /// Wind on hair. Roots never move; the offset grows with strand length and each strand has its own rhythm.
    Vector2 HairSway(float len, float id = 0)
    {
        float w = po.Wind, br = MathF.Min(1, w * 3.5f); float g = M.Sin(po.T * 3.1f + id * 1.3f - len * .55f) * (.34f * br + w * .42f) + M.Sin(po.T * 6.7f + id * 2.9f - len) * w * .22f;
        float x = (po.HairX * .30f + g) * len; float m = 2.2f + len * 1.5f; x = m * MathF.Tanh(x / m);
        return new(x, (po.HairY * .18f - MathF.Abs(po.HairX) * .06f + M.Cos(po.T * 4.1f + id * 2.1f) * w * .12f) * len);
    }

    /// One lock of hair along a quadratic curve, styled by hair texture.
    void Lock(Vector2 a, Vector2 m, Vector2 b, float w0, float w1, float w2, int id)
    {
        int tex = hd.Tex; float len = Vector2.Distance(a, m) + Vector2.Distance(m, b);
        Vector2 At(float t) => (1 - t) * (1 - t) * a + 2 * t * (1 - t) * m + t * t * b;
        if (tex >= 2)
        {
            float step = tex == 2 ? 1.5f : tex == 3 ? 1.9f : tex == 4 ? 1.5f : 1.1f; int n = Math.Max(2, (int)(len / step));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, w = t < .5f ? M.Lerp(w0, w1, t * 2) : M.Lerp(w1, w2, (t - .5f) * 2); var q = At(t);
                if (tex == 2) { float s = M.Sin(i * 1.9f + id * 2); q.X += s * .95f; p.Ellipse(q.X, q.Y, MathF.Max(w, 1.7f) * .95f, MathF.Max(w, 1.7f) * .9f); }
                else if (tex == 3) p.Ellipse(q.X + (M.Hash(i, id) - .5f) * 1.6f, q.Y, MathF.Max(w, 1.3f), MathF.Max(w, 1.3f));
                else if (tex == 4) { float ww = MathF.Min(MathF.Max(w2, .9f), 1.25f); p.Ellipse(q.X + ((i & 1) == 0 ? .25f : -.25f), q.Y, ww, ww * .95f); }
                else { float ww = 1.15f; p.Ellipse(q.X, q.Y, ww + (M.Hash(i, id) - .5f) * .25f, ww); }
            }
            return;
        }
        const int N = 12; Span<Vector2> q2 = stackalloc Vector2[N * 2 + 1];
        for (int i = 0; i < N; i++)
        {
            float t = i / (N - 1f), w = t < .5f ? M.Lerp(w0, w1, t * 2) : M.Lerp(w1, w2, (t - .5f) * 2); var c = At(t); var dd = At(MathF.Min(1, t + .05f)) - At(MathF.Max(0, t - .05f)); dd = dd.LengthSquared() < 1e-6f ? new(0, 1) : Vector2.Normalize(dd); var n = new Vector2(-dd.Y, dd.X);
            if (tex == 1) c += n * M.Sin(t * len * .42f + id * 1.7f) * (.35f + t * .9f);
            q2[i] = c + n * w; q2[N * 2 - i] = c - n * w;
        }
        { var dd = b - At(.9f); q2[N] = b + (dd.LengthSquared() < 1e-6f ? default : Vector2.Normalize(dd)) * MathF.Max(.4f, w2 * .8f); if (tex == 1) q2[N] = (q2[N - 1] + q2[N + 1]) * .5f + new Vector2(0, .6f); }
        p.Poly(q2);
    }
    void StrandLines(Vector2 a, Vector2 m, Vector2 b, float mult = .8f) { if (fine || hd.Tex < 2) p.Curve(a, m, b, fine ? .45f : .4f, default, true, 1, mult); }

    /// Back of the head for climbing: the skull cap, then whatever length hangs down.
    void HairBehind()
    {
        HairSetup(); if (hd.Name == "Bald") return; float T = hTop, SV = MathF.Min(hSide, .5f), rx = r.HeadRx, ry = r.HeadRy; bool bumpy = hd.Tex is 2 or 3 && T + SV > .15f;
        if (hd.Fade > .05f)
        {
            p.Begin().Blob(H(-1f, .1f), H(-1.02f, -.35f), H(-.7f, -.86f), H(0, -1.04f), H(.7f, -.86f), H(1.02f, -.35f), H(1f, .1f), H(.72f, .62f), H(0, .78f), H(-.72f, .62f)); HairClip(); p.Shade(new Ink((x, y) => Col.Lerp(skin, HairC(x, y, -1, false), M.Clamp(.8f - (y - HC.Y) / ry * .55f, .25f, .85f)), 2, .3f, .3f) { Ao = 0, Grain = .05f });
            p.Begin().Blob(H(-.86f, -.28f), H(-.84f, -.8f - T * .5f), H(-.42f, -1f - T * .92f), H(0, -1.04f - T), H(.42f, -1f - T * .92f), H(.84f, -.8f - T * .5f), H(.86f, -.28f), H(.4f, -.1f), H(-.4f, -.1f)); HairClip(); p.Shade(HairInk(-1, false, 0, 3.4f));
        }
        else
        {
            float nape = hd.BackLen > .05f ? .72f : .6f; p.Begin().Blob(H(-1.02f - SV * .7f, .15f), H(-1.0f - SV * .6f, -.4f), H(-.82f - SV * .3f, -.82f - T * .55f), H(-.42f, -1f - T * .92f), H(0, -1.04f - T), H(.42f, -1f - T * .92f), H(.82f + SV * .3f, -.82f - T * .55f), H(1.0f + SV * .6f, -.4f), H(1.02f + SV * .7f, .15f), H(.82f + SV * .5f, nape), H(0, nape + .12f), H(-.82f - SV * .5f, nape));
            if (bumpy && !hatCover) for (int i = 0; i < 18; i++) { float an = i * M.TAU / 18; var c = H(MathF.Sin(an) * (1 + SV * .75f), -MathF.Cos(an) * (1 + T * .85f) * (MathF.Cos(an) < 0 ? .75f : 1)) + HairSway(.3f, i); p.Ellipse(c.X, c.Y, rx * .24f, ry * .2f); }
            HairClip(); p.Shade(HairInk(-1, false, -.03f, 3.6f)); if (hd.Tex < 2) for (int i = -3; i <= 3; i++) StrandLines(H(i * .06f, -1 - T), H(i * .36f, -.5f), H(i * .27f, .62f), .8f);
        }
        if (hd.BackLen > .55f || hd.U != Updo.None) HairBack();
    }
    void HairBack()
    {
        HairSetup(); if (hd.Name == "Bald") return;
        float rx = r.HeadRx, ry = r.HeadRy; var cs = HairSway(1);
        // long back mass
        if (hd.BackLen > .05f && hd.BackW > 0)
        {
            float L = hd.BackLen; int n = hd.Tex >= 4 ? 9 : 7; float bw = hd.BackW * (hatCover ? .96f : 1);
            bool seps = hd.Tex >= 4;
            p.Begin();
            if (!seps) { var c = H(0, -.1f); p.Ellipse(c.X, c.Y, rx * MathF.Min(bw, 1.06f + hSide * .6f), ry * (.95f + hTop * .5f)); }
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0 : i / (n - 1f) * 2 - 1, au = MathF.Abs(u); if (seps) p.Begin();
                float tip = L * (1 - au * au * (hd.Tex == 0 && L > 2 ? .04f : .14f) - M.Hash(i, 3) * .06f); var sw = HairSway(MathF.Max(.3f, tip) * 1.6f, i);
                var a = H(u * .75f, -.45f); var m = H(u * (bw + hSide * .55f), tip * .42f) + sw * .35f; var b = H(u * bw * (L > 1.2f ? .98f : .9f), tip) + sw;
                // never let hair droop through the floor
                if (b.Y > -6) b.Y = -6;
                float w = rx * bw * (seps ? .17f : .34f); Lock(a, m, b, w * .9f, w, hd.Name == "Mullet" || hd.Name == "Wolf Cut" ? w * .25f : w * .72f, i);
                if (seps) { HairClip(); p.Shade(HairInk(i, true, -.1f - .1f * ((i & 1)))); }
            }
            if (!seps)
            {
                HairClip(); p.Shade(HairInk(-1, true, -.13f, 3.4f));
                for (int i = 0; i < 6; i++) { float u = (i + .5f) / 6 * 2 - 1; var sw = HairSway(L * 1.6f, i * 1.2f); StrandLines(H(u * .8f, .2f), H(u * (bw + hSide * .4f), L * .5f) + sw * .4f, H(u * bw * .93f, L * .96f) + sw, .74f); }
            }
        }
        Updos();
    }

    void Puff(float hx, float hy, float rr, int id, float dark = 0)
    {
        var c = H(hx, hy) + HairSway(.35f); p.Begin();
        if (hd.Tex >= 2 && hd.Tex <= 3) { for (int i = 0; i < 11; i++) { float an = i * M.TAU / 11; p.Ellipse(c.X + M.Sin(an) * rr * .68f, c.Y + M.Cos(an) * rr * .62f, rr * .42f, rr * .42f); } p.Ellipse(c.X, c.Y, rr * .8f, rr * .75f); }
        else p.Ellipse(c.X, c.Y, rr, rr * .9f);
        p.Shade(HairInk(-1, false, dark, rr * .8f));
        if (hd.Tex < 2) { p.Curve(c + new Vector2(-rr, 0), c + new Vector2(0, -rr * .9f), c + new Vector2(rr * .8f, -rr * .1f), .45f, default, true, 1, .74f); p.Curve(c + new Vector2(-rr * .7f, rr * .5f), c + new Vector2(rr * .3f, rr * .2f), c + new Vector2(rr * .5f, -rr * .7f), .45f, default, true, 1, .76f); }
    }
    void Tail(Vector2 a, Vector2 m, Vector2 b, float w0, float w1, float w2)
    {
        p.Begin(); Lock(a, m, b, w0, w1, w2, 3); if (hd.Tex < 2) Lock(a, m + new Vector2(.8f, 0), b + new Vector2(1.4f, -2), w0 * .8f, w1 * .8f, w2 * .5f, 4); p.Shade(HairInk(-1, false, -.05f)); StrandLines(a, m, b, .76f); StrandLines(a, m + new Vector2(w1 * .5f, 0), b + new Vector2(w2 * .4f, -2), .8f);
    }
    void Tie(Vector2 c, float w = 1.5f) { p.Begin().Ellipse(c.X, c.Y, w, w * .7f); p.Shade(new Ink(Vis(Slot.Hat) && d[Slot.Hat].Id == 0 ? (Col)d[Slot.Top].B : (Col)0x8c2f35, 1, .5f)); }
    void BraidTail(Vector2 a, Vector2 b)
    {
        int n = 9; for (int i = 0; i <= n; i++) { float t = i / (float)n; var q = Vector2.Lerp(a, b, t); q.X += M.Sin(t * 3) * .8f; float w = M.Lerp(2.5f, 1.5f, t); p.Begin().Ellipse(q.X + ((i & 1) == 0 ? .6f : -.6f), q.Y, w, w * .85f); p.Shade(HairInk(-1, false, (i & 1) == 0 ? -.06f : .05f, 1.8f)); }
        Tie(Vector2.Lerp(a, b, 1.03f), 1.2f); p.Begin(); var e = Vector2.Lerp(a, b, 1.05f); p.Poly(e + new Vector2(-1.2f, 0), e + new Vector2(1.2f, 0), e + new Vector2(.4f, 3), e + new Vector2(-.6f, 2.6f)); p.Shade(HairInk());
    }
    void Updos()
    {
        float rx = r.HeadRx; var u = hd.U; bool c = hatCover;
        switch (u)
        {
            case Updo.HighPony: if (c) goto case Updo.LowPony; { var s = HairSway(2.4f); Tail(H(0, -1.05f), H(.75f, -1.35f) + s * .4f, H(1.35f, .95f) + s, rx * .24f, rx * .34f, rx * .12f); Tie(H(.08f, -1.06f), 2); break; }
            case Updo.LowPony: { var s = HairSway(2); Tail(H(.35f, .55f), H(.95f, .7f) + s * .4f, H(.95f, 2.0f) + s, rx * .24f, rx * .3f, rx * .1f); break; }
            case Updo.SidePony: { var s = HairSway(2.2f); Tail(H(.85f, .3f), H(1.5f, .4f) + s * .4f, H(1.2f, 1.9f) + s, rx * .26f, rx * .32f, rx * .1f); Tie(H(.95f, .32f), 1.8f); break; }
            case Updo.Bun: if (c) { Puff(.45f, .75f, rx * .4f, 1); break; } Puff(.05f, -1.22f, rx * .5f, 1); { var s = HairSway(.6f); p.Begin(); Lock(H(.3f, -1.3f), H(.5f, -1.45f), H(.58f, -1.15f) + s, .8f, .6f, .2f, 5); Lock(H(-.2f, -1.3f), H(-.4f, -1.45f), H(-.46f, -1.2f) + s, .8f, .5f, .2f, 6); p.Shade(HairInk()); } break;
            case Updo.TopKnot: case Updo.ManBun: if (c) { Puff(.5f, .7f, rx * .3f, 1); break; } Puff(0, -1.16f, rx * .34f, 1); Tie(H(0, -.94f), 1.8f); break;
            case Updo.LowBun: Puff(.4f, .85f, rx * .42f, 1); break;
            case Updo.HalfUp: if (!c) { Puff(0, -1.12f, rx * .32f, 1); Tie(H(0, -.92f), 1.6f); } break;
            case Updo.SpaceBuns: if (c) { Puff(-.95f, .75f, rx * .36f, 1); Puff(.95f, .75f, rx * .36f, 2); } else { Puff(-.72f, -1.0f, rx * .42f, 1); Puff(.72f, -1.0f, rx * .42f, 2); } break;
            case Updo.AfroPuffs: if (c) { Puff(-1.1f, .5f, rx * .5f, 1); Puff(1.1f, .5f, rx * .5f, 2); } else { Puff(-.86f, -.92f, rx * .66f, 1); Puff(.86f, -.92f, rx * .66f, 2); } break;
            case Updo.Pigtails: { var s = HairSway(1.8f); Tail(H(-.95f, .2f), H(-1.5f, .5f) + s * .4f, H(-1.25f, 1.75f) + s, rx * .24f, rx * .3f, rx * .1f); Tail(H(.95f, .2f), H(1.5f, .5f) + s * .4f, H(1.25f, 1.75f) + s, rx * .24f, rx * .3f, rx * .1f); Tie(H(-1, .25f)); Tie(H(1, .25f)); break; }
        }
    }

    void NeckSkinAndHead(Action collar)
    {
        collar();
        // ears
        for (int s = -1; s <= 1; s += 2)
        {
            float turnIn = po.Turn * s > 0 ? at * .12f : 0; var e = H(s * (1.0f - turnIn) - po.Turn * .05f, .14f); p.Begin().Ellipse(e.X, e.Y, r.HeadRx * .19f, r.HeadRy * .2f); p.Shade(new Ink(skin, 1.4f, .5f) { Ao = 0 }); if (fine) p.Curve(e + new Vector2(s * .7f, -.9f), e + new Vector2(-s * .2f, 0), e + new Vector2(s * .5f, .9f), .35f, skin.Shade(-.4f));
        }
        Span<Vector2> q = [H(0, -1), H(-.62f, -.82f), H(-.97f, -.3f), H(-.96f, .25f), H(-.76f, .68f), H(-.32f, .96f), H(0, 1), H(.32f, .96f), H(.76f, .68f), H(.96f, .25f), H(.97f, -.3f), H(.62f, -.82f)];
        p.Begin().Blob(q, 1, 5);
        p.Shade(new Ink(skin, 4.2f, .5f) { Ao = 2.4f, Grad = .1f });
    }

    void FaceAll()
    {
        if (show == Show.Mannequin && only != Slot.Beard) return;
        float fx = po.Turn * .17f, fd = po.Bend * .30f; Vector2 F2(float x, float y) => H(x + fx, y + fd * (1 - MathF.Max(0, y) * .35f));
        int mk = d.Marks; Col blush = Col.Lerp(skin, 0xe0564f, .38f);
        // soft cheeks & forehead light give the face volume
        for (int s = -1; s <= 1; s += 2)
        {
            var c = F2(s * .55f, .42f); p.Begin().Ellipse(c.X, c.Y, r.HeadRx * .2f, r.HeadRy * .11f); p.IntersectAlpha(); p.Tint(blush, mk is 2 or 3 or 7 ? .75f : .28f);
            if (mk is 1 or 3 or 7 && (fine || s > -2)) for (int i = 0; i < (fine ? 5 : 2); i++) { var f = F2(s * (.3f + M.Hash(i, 3) * .42f), .28f + M.Hash(i, 5) * .2f); if (fine) { p.Begin().Ellipse(f.X, f.Y, .32f, .32f); p.Tint(skin.Shade(-.5f), .8f); } else p.Dot(f.X, f.Y, skin.Shade(-.28f)); }
        }
        if (mk == 4) { var f = F2(.42f, .55f); if (fine) { p.Begin().Ellipse(f.X, f.Y, .45f, .45f); p.Flat(ink); } else p.Dot(f.X, f.Y, skin.Shade(-.7f)); }
        if (mk == 5) for (int s = -1; s <= 1; s += 2) { var f = F2(s * .5f, .58f); p.Curve(f + new Vector2(s * .2f, -.7f), f + new Vector2(s * .7f, 0), f + new Vector2(s * .2f, .7f), .3f, skin.Shade(-.45f), false); }
        if (mk == 6) { var f = F2(-.5f, .36f); p.Begin().Poly(f + new Vector2(-2, -.4f), f + new Vector2(1.6f, -1.1f), f + new Vector2(2, .3f), f + new Vector2(-1.6f, 1)); p.Shade(new Ink(0xe8c49a, .6f, .3f, .7f) { Ao = 0 }); }
        if (d.Age == 3)
        {
            for (int s = -1; s <= 1; s += 2) { p.Curve(F2(s * .22f, .42f), F2(s * .36f, .5f), F2(s * .4f, .66f), .3f, skin.Shade(-.3f), false); p.Curve(F2(s * .24f, .22f), F2(s * .4f, .27f), F2(s * .55f, .2f), .3f, skin.Shade(-.28f), false); if (fine) p.Line(F2(s * .68f, .02f), F2(s * .8f, -.04f), .3f, skin.Shade(-.3f), false); }
            if (fine) { p.Curve(F2(-.35f, -.42f), F2(0, -.38f), F2(.35f, -.42f), .3f, skin.Shade(-.25f), false); }
        }
        Eyes(F2); Brows(F2); Nose(F2); Mouth(F2);
        if (Vis(Slot.Beard) && d[Slot.Beard].Id > 0) Beard(F2);
    }

    void Eyes(Func<float, float, Vector2> F2)
    {
        float blink = po.Blink; Col lash = Col.Lerp(0x1c1216, d.Hair.Base, .25f); Col iris = d.EyeCol; float lx = po.LookX, ly = po.LookY;
        (float w, float h, float tilt, float lid, bool lashes) e = d.Eye switch
        {
            0 => (1.55f, 1.7f, 0, 0, false), 1 => (1.75f, 1.35f, .12f, .05f, false), 2 => (1.55f, 1.5f, -.06f, .12f, false), 3 => (1.75f, 1.95f, 0, 0, false), 4 => (1.65f, 1.4f, -.08f, .4f, false), 5 => (1.6f, 1.7f, .08f, .05f, true),
            6 => (1.7f, 1.45f, .28f, .05f, true), 7 => (1.65f, 1.5f, -.26f, .08f, false), 8 => (1.75f, 1.0f, .08f, .1f, false), 9 => (1.75f, 2f, .04f, 0, true), 10 => (1.65f, 1.4f, 0, .28f, false), _ => (1.15f, 1.2f, 0, 0, false)
        };
        for (int s = -1; s <= 1; s += 2)
        {
            var c = F2(s * .40f, .07f);
            float open = M.Clamp(1 - blink); if (d.Idle == 5) open *= .75f;
            if (!fine)
            {
                // tiny, pixel-placed eyes: two columns each, always mirrored around the face centre
                var mid = F2(0, .07f); float mx = OX(mid.X); int y0 = (int)MathF.Floor(OY(c.Y)) - 1; int cx0 = (int)MathF.Round(mx);
                int x0 = s < 0 ? cx0 - 4 : cx0 + 1; int outer = s < 0 ? x0 : x0 + 2, inner = s < 0 ? x0 + 2 : x0;
                if (open < .35f) { for (int i = 0; i < 3; i++) Pix(x0 + i, y0 + 1, lash); continue; }
                for (int i = 0; i < 3; i++) Pix(x0 + i, y0, e.lid > .2f ? skin.Shade(-.35f) : lash);
                if (e.lid > .2f) for (int i = 0; i < 3; i++) Pix(x0 + i, y0 + 1, lash);
                if (e.lashes) Pix(s < 0 ? x0 - 1 : x0 + 3, y0, lash);
                if (e.tilt < -.1f) Pix(outer, y0 + 1, lash); else if (e.tilt > .2f) Pix(s < 0 ? x0 - 1 : x0 + 3, y0 - 1, lash);
                int r0 = y0 + (e.lid > .2f ? 2 : 1), rows = e.lid > .2f || open < .7f || e.h < 1.1f ? 1 : 2;
                int look = lx > .3f ? 1 : lx < -.3f ? -1 : 0;
                for (int j = 0; j < rows; j++)
                {
                    for (int i = 0; i < 3; i++) Pix(x0 + i, r0 + j, 0xf6eee2);
                    int ix = x0 + 1 + look; Col dk = iris.Shade(-.72f);
                    Pix(ix, r0 + j, j == 0 ? dk : iris.Shade(-.25f)); int ix2 = look == 0 ? inner : x0 + 1; Pix(ix2, r0 + j, j == 0 ? dk : iris.Shade(-.05f));
                }
                continue;
            }
            float w = e.w, h = e.h; float ct = M.Cos(e.tilt * -s), st = M.Sin(e.tilt * -s);
            Vector2 E(float x, float y) => c + new Vector2(x * ct - y * st, x * st + y * ct);
            if (open < .3f) { p.Begin(); p.Curve(E(-w, .1f), E(0, h * .55f), E(w, .1f), .55f, lash, false); if (e.lashes) p.Line(E(s * w, .1f), E(s * (w + .7f), .5f), .4f, lash, false); continue; }
            float topY = -h + e.lid * h * 1.4f + (1 - open) * h * 1.2f;
            p.Begin().Blob(E(-w, .15f), E(-w * .55f, topY * .85f), E(w * .1f, topY), E(w * .75f, topY * .7f), E(w, .1f), E(w * .5f, h * .78f), E(-w * .5f, h * .8f));
            var em = p.CopyMask(); p.Flat(0xfaf4ea);
            p.Begin().Ellipse(c.X + lx * .45f + s * -.12f, c.Y + ly * .3f + .05f, MathF.Min(w, h) * .72f + .12f, MathF.Min(1.25f, h * .85f) + .2f); p.IntersectMask(em); p.Shade(new Ink(iris, 1.2f, .5f, 1) { Ao = 0, Grad = -.5f });
            p.Begin().Ellipse(c.X + lx * .45f + s * -.12f, c.Y + ly * .3f + .05f, .55f, .68f); p.IntersectMask(em); p.Flat(0x1a1014);
            p.Begin().Ellipse(c.X + lx * .45f - .45f, c.Y + ly * .3f - .5f, .42f, .42f); p.IntersectMask(em); p.Flat(0xffffff);
            if (d.Eye == 9) { p.Begin().Ellipse(c.X + lx * .45f + .45f, c.Y + .55f, .28f, .28f); p.IntersectMask(em); p.Flat(0xffffff); }
            // upper shade on sclera, lashes and lid
            p.Begin(); p.Curve(E(-w, .15f), E(0, topY * 1.75f - .1f), E(w, .05f), .62f, lash, false);
            p.Curve(E(-w * .7f, h * .72f), E(0, h * 1.0f), E(w * .7f, h * .7f), .25f, skin.Shade(-.45f), false);
            p.Curve(E(-w * .9f, topY - .35f), E(0, topY * 1.6f - .95f), E(w * .9f, topY - .4f), .28f, skin.Shade(-.38f), false);
            if (e.lashes) { p.Line(E(s * w * .95f, -.1f), E(s * (w + .95f), -.85f), .45f, lash, false); p.Line(E(s * w * .65f, topY * .8f), E(s * (w * .65f + .75f), topY - .75f), .4f, lash, false); p.Line(E(s * w * .25f, topY), E(s * (w * .25f + .4f), topY - .9f), .35f, lash, false); }
        }
    }
    float OX(float x) => p.OX + x * S;
    float OY(float y) => p.OY + y * S;
    void Pix(int x, int y, Col c) { if (x >= 0 && y >= 0 && x < p.W && y < p.H && p.Px[y * p.W + x].A > 0) p.Px[y * p.W + x] = c.Rl(); }

    void Brows(Func<float, float, Vector2> F2)
    {
        if (d.Brow == 9) return; Col c = Col.Lerp(d.Hair.Roots > .2f ? d.Hair.RootCol : d.Hair.Base, 0x2a1a1c, .3f); if (d.Hair.Id == Catalog.Hair.Length - 1) c = Col.Lerp(d.Hair.Base, 0x2a1a1c, .2f);
        (float th, float arch, float tilt, float len) b = d.Brow switch { 0 => (.75f, .5f, 0, 1.9f), 1 => (1.15f, .45f, 0, 2.1f), 2 => (.45f, .55f, 0, 1.8f), 3 => (.65f, 1.0f, .1f, 2), 4 => (.8f, .05f, 0, 2), 5 => (1.35f, .5f, -.05f, 2.2f), 6 => (.6f, .4f, -.15f, 1.7f), 7 => (.85f, .3f, .3f, 2), _ => (.8f, .5f, 0, 2) };
        for (int s = -1; s <= 1; s += 2)
        {
            var m = F2(s * .41f, -.235f) + new Vector2(0, -po.Brow * .6f);
            if (!fine) { int cx0 = (int)MathF.Round(OX(F2(0, .07f).X)), y0 = (int)MathF.Floor(OY(F2(0, .07f).Y)) - 3 - (int)MathF.Round(po.Brow * .6f); int x0 = s < 0 ? cx0 - 4 : cx0 + 1; for (int i = 0; i < 3; i++) { bool ou = s < 0 ? i == 0 : i == 2; int up = b.arch > .8f && !ou ? -1 : b.tilt > .2f && ou ? -1 : 0; Pix(x0 + i, y0 + up, c); if (b.th > 1) Pix(x0 + i, y0 + up - 1, c); } continue; }
            Vector2 i0 = m + new Vector2(-s * b.len * .85f, .35f + b.tilt * 1.4f), o0 = m + new Vector2(s * b.len, .4f - b.tilt * 1.2f), mid = m + new Vector2(s * .3f, -b.arch * 1.3f);
            p.Begin(); Vector2 Q(float t) => (1 - t) * (1 - t) * i0 + 2 * t * (1 - t) * mid + t * t * o0;
            Span<Vector2> q = stackalloc Vector2[14]; for (int i = 0; i < 7; i++) { float t = i / 6f, w = b.th * .5f * (t < .75f ? 1 : 1 - (t - .75f) * 3.2f); q[i] = Q(t) - new Vector2(0, w); q[13 - i] = Q(t) + new Vector2(0, w); }
            p.Poly(q); if (d.Brow == 8 && s > 0) p.Cut(m + new Vector2(.5f, -2), m + new Vector2(.9f, -2), m + new Vector2(.7f, 2), m + new Vector2(.3f, 2)); p.Flat(c);
            if (fine && b.th > .9f) for (int i = 0; i < 5; i++) { var t = Q(.12f + i * .16f); p.Line(t + new Vector2(0, b.th * .3f), t + new Vector2(s * .5f, -b.th * .7f), .3f, c, false); }
        }
    }
    void Nose(Func<float, float, Vector2> F2)
    {
        var c = F2(po.Turn * .06f, .34f); Col sh = skin.Shade(-.58f), hl = skin.Shade(.6f);
        if (!fine) { int x = (int)MathF.Floor(OX(c.X)), y = (int)MathF.Floor(OY(c.Y)); Pix(x, y, skin.Shade(-.3f)); if (d.Nose is 1 or 3 or 7) Pix(x - 1, y, skin.Shade(-.3f)); if (d.Nose is 2 or 4 or 7) Pix(x, y - 1, skin.Shade(-.2f)); if (d.Nose == 3) Pix(x + 1, y, skin.Shade(-.3f)); return; }
        float w = d.Nose switch { 0 => 1.1f, 1 => 1.5f, 2 => 1.1f, 3 => 1.9f, 4 => 1.2f, 5 => .8f, 6 => 1.15f, _ => 1.3f }, l = d.Nose switch { 2 => 3, 4 => 3.8f, 7 => 3.2f, _ => 1.4f };
        p.Begin();
        p.Curve(c + new Vector2(-w, -.2f), c + new Vector2(-w * .7f, .85f), c + new Vector2(-w * .2f, .5f), .45f, sh, false);
        p.Curve(c + new Vector2(w, -.2f), c + new Vector2(w * .7f, .85f), c + new Vector2(w * .2f, .5f), .45f, sh, false);
        p.Curve(c + new Vector2(-w * .3f, .5f), c + new Vector2(0, .9f), c + new Vector2(w * .3f, .5f), .4f, sh, false);
        if (l > 2) { float bx = d.Nose == 7 ? 1.1f : .5f; p.Curve(c + new Vector2(.5f, -l), c + new Vector2(bx, -l * .4f), c + new Vector2(.75f, -.5f), .35f, skin.Shade(-.28f), false); }
        p.Begin().Ellipse(c.X - .25f, c.Y - .45f, w * .4f, .5f); p.Tint(hl, .85f);
    }
    void Mouth(Func<float, float, Vector2> F2)
    {
        var c = F2(po.Turn * .03f, .615f); Col lip = Col.Lerp(skin, d.Lip, .62f), dk = Col.Lerp(skin.Ink(), 0x5a1c24, .45f); float o = po.Mouth;
        if (!fine)
        {
            int x = (int)MathF.Floor(OX(c.X)), y = (int)MathF.Floor(OY(c.Y)); Col m = Col.Lerp(skin.Shade(-.6f), d.Lip, .4f);
            if (o > .35f) { for (int i = -1; i <= 1; i++) { Pix(x + i, y, dk); if (o > .6f || i == 0) Pix(x + i, y + 1, i == 0 ? 0xb5504f : dk); } return; }
            switch (d.Mouth)
            {
                case 1: for (int i = -2; i <= 2; i++) Pix(x + i, y, Math.Abs(i) == 2 ? m : 0xfaf4ea); for (int i = -1; i <= 1; i++) Pix(x + i, y + 1, m); break;
                case 2: for (int i = -1; i <= 1; i++) Pix(x + i, y, m); break;
                case 3: for (int i = -1; i <= 1; i++) Pix(x + i, y, m); Pix(x + 2, y - 1, m); break;
                case 4: for (int i = -1; i <= 1; i++) { Pix(x + i, y, dk); } Pix(x, y + 1, 0xb5504f); Pix(x - 2, y - 1, m); Pix(x + 2, y - 1, m); break;
                case 5: case 7: for (int i = -1; i <= 1; i++) { Pix(x + i, y, m); Pix(x + i, y + 1, lip); } break;
                case 6: Pix(x, y, m); Pix(x + 1, y, m); break;
                case 8: Pix(x - 2, y - 1, m); Pix(x - 1, y, m); Pix(x, y - 1, m); Pix(x + 1, y, m); Pix(x + 2, y - 1, m); break;
                default: for (int i = -1; i <= 1; i++) Pix(x + i, y, m); Pix(x - 2, y - 1, m); Pix(x + 2, y - 1, m); break;
            }
            return;
        }
        p.Begin(); float w = d.Mouth switch { 6 => 1.2f, 1 => 2.9f, 4 => 2.6f, 5 => 2.1f, 7 => 1.7f, _ => 2.4f };
        if (o > .2f || d.Mouth is 4)
        {
            // a proper open smile: curved upper lip, rounded jaw, inset teeth and a soft tongue
            float ow = d.Mouth == 6 ? 1.25f : 1.95f, h = d.Mouth == 4 ? MathF.Max(1.7f, o * 2.4f) : 1.0f + o * 1.5f; Col inside = 0x6e2430;
            p.Blob(c + new Vector2(-ow, -.75f), c + new Vector2(-ow * .5f, -.45f), c + new Vector2(0, -.35f), c + new Vector2(ow * .5f, -.45f), c + new Vector2(ow, -.75f), c + new Vector2(ow * .8f, h * .35f), c + new Vector2(ow * .35f, h * .82f), c + new Vector2(0, h * .92f), c + new Vector2(-ow * .35f, h * .82f), c + new Vector2(-ow * .8f, h * .35f));
            var mm = p.CopyMask(); p.Flat(inside);
            p.Begin().Ellipse(c.X, c.Y + h * .85f, ow * .55f, h * .42f); p.IntersectMask(mm); p.Flat(0xd8747a);
            p.Begin().Blob(c + new Vector2(-ow * .8f, -.9f), c + new Vector2(0, -.8f), c + new Vector2(ow * .8f, -.9f), c + new Vector2(ow * .62f, .02f), c + new Vector2(0, .22f), c + new Vector2(-ow * .62f, .02f)); p.IntersectMask(mm); p.Flat(0xfaf4ea);
            FrameEdge(mm, dk); p.Dot(c.X - ow - .25f, c.Y - .85f, skin.Shade(-.4f)); p.Dot(c.X + ow + .15f, c.Y - .85f, skin.Shade(-.4f));
            p.Curve(c + new Vector2(-ow * .45f, h + .75f), c + new Vector2(0, h + 1f), c + new Vector2(ow * .45f, h + .75f), .3f, skin.Shade(-.3f), false); return;
        }
        switch (d.Mouth)
        {
            case 1: case 9:
                {
                    float gw = 1.9f; p.Blob(c + new Vector2(-gw, -.65f), c + new Vector2(0, -.25f), c + new Vector2(gw, -.65f), c + new Vector2(gw * .62f, .55f), c + new Vector2(0, .95f), c + new Vector2(-gw * .62f, .55f)); var gm = p.CopyMask(); p.Flat(0xfaf4ea); p.Begin().Ellipse(c.X, c.Y + 1.15f, gw, .5f); p.IntersectMask(gm); p.Flat(0xe8dccb); FrameEdge(gm, dk);
                    if (d.Mouth == 9) p.Line(c + new Vector2(0, -.1f), c + new Vector2(0, .55f), .42f, dk, false); p.Dot(c.X - gw - .3f, c.Y - .8f, skin.Shade(-.45f)); p.Dot(c.X + gw + .2f, c.Y - .8f, skin.Shade(-.45f)); p.Curve(c + new Vector2(-gw * .4f, 1.75f), c + new Vector2(0, 2f), c + new Vector2(gw * .4f, 1.75f), .3f, lip, false); break;
                }
            case 2: p.Line(c + new Vector2(-w * .8f, 0), c + new Vector2(w * .8f, 0), .45f, dk, false); p.Line(c + new Vector2(-w * .4f, 1.1f), c + new Vector2(w * .4f, 1.1f), .35f, lip, false); break;
            case 3: p.Curve(c + new Vector2(-w * .7f, .2f), c + new Vector2(.6f, .7f), c + new Vector2(w, -.9f), .45f, dk, false); p.Line(c + new Vector2(w, -1.2f), c + new Vector2(w + .4f, -.6f), .35f, dk, false); break;
            case 5: case 7: { float h = d.Mouth == 5 ? 1.15f : .95f; p.Blob(c + new Vector2(-w, 0), c + new Vector2(-.7f, -h * .85f), c + new Vector2(0, -h * .5f), c + new Vector2(.7f, -h * .85f), c + new Vector2(w, 0), c + new Vector2(0, h * 1.25f)); p.Shade(new Ink(lip, .9f, .5f, .5f) { Ao = 0 }); p.Curve(c + new Vector2(-w * .85f, 0), c + new Vector2(0, .35f), c + new Vector2(w * .85f, 0), .35f, dk, false); p.Dot(c.X - .5f, c.Y + h * .6f, lip.Shade(.6f)); break; }
            case 6: p.Curve(c + new Vector2(-w, -.1f), c + new Vector2(0, .7f), c + new Vector2(w, -.1f), .45f, dk, false); break;
            case 8: p.Curve(c + new Vector2(-w, -.5f), c + new Vector2(-w * .5f, 1.2f), c, .45f, dk, false); p.Curve(c, c + new Vector2(w * .5f, 1.2f), c + new Vector2(w, -.5f), .45f, dk, false); break;
            default: p.Curve(c + new Vector2(-w, -.5f), c + new Vector2(0, 1.1f), c + new Vector2(w, -.5f), .48f, dk, false); p.Curve(c + new Vector2(-w * .4f, 1.5f), c + new Vector2(0, 1.8f), c + new Vector2(w * .4f, 1.5f), .35f, lip, false); p.Dot(c.X - w - .3f, c.Y - .8f, skin.Shade(-.45f)); p.Dot(c.X + w + .2f, c.Y - .8f, skin.Shade(-.45f)); break;
        }
    }

    void Beard(Func<float, float, Vector2> F2)
    {
        int id = d[Slot.Beard].Id; Col c = d[Slot.Beard].A; float sw = po.HairX * .12f;
        Ink K(float dark = 0) => new Ink(c, 1.8f, .5f) { Ao = .4f, Tone = fine ? (x, y) => dark + M.Noise(x * 1.6f, y * .7f, 6) * .16f : (x, y) => dark };
        void Stache(float w, float h) { p.Begin().Blob(F2(-w, .56f), F2(-w * .5f, .47f), F2(0, .48f), F2(w * .5f, .47f), F2(w, .56f), F2(w * .5f, .47f + h), F2(0, .53f), F2(-w * .5f, .47f + h)); p.Shade(K()); }
        void Jaw(float lo, float inner, bool mus)
        {
            p.Begin().Blob(F2(-.98f, .0f), F2(-.97f, .35f), F2(-.78f, .75f), F2(-.35f, lo), F2(0, lo + .05f), F2(.35f, lo), F2(.78f, .75f), F2(.97f, .35f), F2(.98f, .0f), F2(.82f, .3f), F2(.6f, inner), F2(0, inner - .04f), F2(-.6f, inner), F2(-.82f, .3f)); p.CutEllipse(F2(0, .63f).X, F2(0, .63f).Y, r.HeadRx * .27f, r.HeadRy * .085f); p.Shade(K()); if (mus) Stache(.44f, .12f);
        }
        switch (id)
        {
            case 1: case 11: if (id == 11) { for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(F2(s * 1f, -.15f), F2(s * .8f, -.05f), F2(s * .72f, .5f), F2(s * .9f, .45f)); p.Shade(K()); } break; } p.Begin().Blob(F2(-.96f, .2f), F2(-.72f, .74f), F2(-.3f, .97f), F2(.3f, .97f), F2(.72f, .74f), F2(.96f, .2f), F2(.62f, .46f), F2(0, .44f), F2(-.62f, .46f)); p.CutEllipse(F2(0, .62f).X, F2(0, .62f).Y, r.HeadRx * .28f, r.HeadRy * .09f); p.IntersectAlpha(); p.Tint(c, .42f); break;
            case 2: Stache(.46f, .13f); break;
            case 3: Stache(.5f, .1f); for (int s = -1; s <= 1; s += 2) p.Curve(F2(s * .44f, .55f), F2(s * .72f, .7f), F2(s * .66f, .42f), .7f, c.Shade(-.2f), false); break;
            case 4: p.Begin().Blob(F2(-.2f, .75f), F2(.2f, .75f), F2(.16f, 1.12f), F2(-.16f, 1.12f)); p.Shade(K()); break;
            case 5: p.Begin().Blob(F2(-.1f, .72f), F2(.1f, .72f), F2(0, .9f)); p.Shade(K()); break;
            case 6: p.Begin().Blob(F2(-.98f, .0f), F2(-.95f, .4f), F2(-.72f, .78f), F2(-.3f, 1.02f), F2(.3f, 1.02f), F2(.72f, .78f), F2(.95f, .4f), F2(.98f, 0), F2(.86f, .38f), F2(.6f, .72f), F2(.25f, .88f), F2(-.25f, .88f), F2(-.6f, .72f), F2(-.86f, .38f)); p.Shade(K()); break;
            case 7: Jaw(1.04f, .52f, true); break;
            case 8: Jaw(1.28f, .48f, true); break;
            case 9: case 10: { p.Begin().Blob(F2(-.8f, .6f), F2(-.6f, 1.2f), F2(-.1f, 1.95f) + new Vector2(sw, 0), F2(.1f, 1.95f) + new Vector2(sw, 0), F2(.6f, 1.2f), F2(.8f, .6f)); p.Shade(K(-.08f)); for (int i = -2; i <= 2; i++) p.Curve(F2(i * .2f, .9f), F2(i * .17f, 1.3f), F2(i * .06f, 1.8f) + new Vector2(sw, 0), .4f, default, true, 1, .8f); if (id == 10) for (int i = 0; i < 3; i++) { var q = F2(0, 1.35f + i * .22f) + new Vector2(sw * i * .3f, 0); p.Line(q + new Vector2(-2, 0), q + new Vector2(2, 0), .7f, (Col)d[Slot.Beard].B, false); } Jaw(1.1f, .48f, true); break; }
            case 12: Stache(.4f, .1f); p.Begin().Blob(F2(-.18f, .78f), F2(.18f, .78f), F2(0, 1.25f)); p.Shade(K()); break;
            case 13: p.Begin().Blob(F2(-.6f, .72f), F2(-.48f, .5f), F2(0, .44f), F2(.48f, .5f), F2(.6f, .72f), F2(.3f, .66f), F2(0, .6f), F2(-.3f, .66f)); p.Shade(K()); break;
        }
    }

    // ---------------------------------------------------------------- front hair
    void HairFront()
    {
        if (hd.Name == "Bald") return; float rx = r.HeadRx, ry = r.HeadRy, T = hTop, SV = hSide, fx = po.Turn * .1f; bool fade = hd.Fade > .05f;
        // faded / shaved sides
        if (fade)
        {
            p.Begin().Blob(H(-.99f, .12f), H(-1.03f, -.25f), H(-.78f, -.8f), H(0, -1.04f), H(.78f, -.8f), H(1.03f, -.25f), H(.99f, .12f), H(.84f, -.02f), H(.74f, -.36f), H(.4f, -.55f), H(-.4f, -.55f), H(-.74f, -.36f), H(-.84f, -.02f));
            var k = new Ink((x, y) => { float hy = (y - HC.Y) / ry; float q = M.Clamp((-.02f - hy) * 1.7f); return Col.Lerp(skin, HairC(x, y, -1, false), M.Lerp(.28f, .86f, q) * (1 - hd.Fade * .2f)); }, 1.5f, .3f, .3f) { Ao = 0, Grain = .05f }; HairClip(); p.Shade(k);
        }
        // side locks in front of ears
        if (hd.SideLen > .1f)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float L = hd.SideLen; var sw = HairSway(MathF.Max(.7f, L * 1.3f), s * 2); int n = hd.Tex >= 4 ? 3 : 1;
                for (int i = 0; i < n; i++)
                {
                    float off = n > 1 ? (i - 1) * .17f : 0; p.Begin();
                    float wx = 1 + SV * .8f; var a = H(s * .62f, -.72f); var m = H(s * (wx + .12f + off), -.1f + L * .2f); var b = H(s * (wx - (L > 1.2f ? .1f : .16f) + off), L) + sw; float w = rx * (n > 1 ? .13f : L > 1 ? .24f : .2f);
                    Lock(a, m, b, w * 1.2f, w * (1 + SV), L < 1 ? w * .7f : w * .35f, i + s); if (n == 1 && hd.Tex < 2 && L > .5f) { Lock(a, m + new Vector2(-s * 1.2f, 0), H(s * (wx - .34f), L * .8f) + sw * .8f, w, w * .7f, w * .2f, i + 7); }
                    HairClip(); p.Shade(HairInk(n > 1 ? i + s * 5 + 20 : -1, false, -.04f)); if (hd.Tex < 2) { StrandLines(H(s * .7f, -.5f), H(s * (wx + .05f), 0), H(s * (wx - .12f), L * .94f) + sw, .78f); }
                }
            }
        }
        var f = hd.F;
        if (hd.U == Updo.Mohawk && !hatCover)
        {
            p.Begin(); for (int i = 0; i < 7; i++) { float u = i / 6f * 2 - 1; Lock(H(u * .24f, -.72f), H(u * .3f, -1.1f - T * .6f), H(u * .5f, -1.1f - T * (1 - u * u * .3f)) + HairSway(.8f, i), rx * .13f, rx * .1f, .2f, i); }
            p.Ellipse(H(0, -.75f).X, H(0, -.75f).Y, rx * .34f, ry * .26f); p.Shade(HairInk()); for (int i = 0; i < 4; i++) StrandLines(H(-.2f + i * .13f, -.7f), H(-.22f + i * .14f, -1.1f), H(-.3f + i * .2f, -1 - T), .75f); return;
        }
        // skull cap with hairline
        float side = fade ? .86f : 1.0f + MathF.Min(SV, .25f) * .7f; float low = fade ? -.42f : hd.SideLen > .05f ? .12f : .02f; if (hd.Name == "Buzz Cut" ) { side = 1.0f; low = -.02f; }
        float hl = f switch { Fringe.Slick => -.6f, Fringe.Crop => -.5f, Fringe.Puff => -.48f, Fringe.Pomp or Fringe.Quiff => -.56f, _ => -.5f };
        p.Begin();
        bool bumpy = hd.Tex is 2 or 3 && T + SV > .15f;
        var cv = HairSway(.45f * M.Clamp(T * 4)); hl += po.Bend * .16f; Span<Vector2> cap = [H(-side, low), H(-side - .02f, -.35f), H(-.82f - SV * .3f, -.82f - T * .55f), H(-.42f, -1.0f - T * .92f) + cv, H(0, -1.04f - T) + cv, H(.42f, -1.0f - T * .92f) + cv, H(.82f + SV * .3f, -.82f - T * .55f) + cv * .5f, H(side + .02f, -.35f), H(side, low), H(.86f, low - .02f), H(.8f, -.3f), H(.5f + fx * .5f, hl - .02f), H(fx, hl + .02f), H(-.5f + fx * .5f, hl - .02f), H(-.8f, -.3f), H(-.86f, low - .02f)];
        if (hd.Name == "High Top Fade" && !hatCover) { p.Poly(H(-.86f, -.45f), H(-.92f, -1.0f - T * .9f), H(-.8f, -1.05f - T), H(.8f, -1.05f - T), H(.92f, -1.0f - T * .9f), H(.86f, -.45f), H(.6f, hl), H(-.6f, hl)); }
        else p.Blob(cap, 1, 4);
        if (bumpy && !hatCover)
        {
            int n = 13; for (int i = 0; i < n; i++) { float an = M.Lerp(-2.1f, 2.1f, i / (n - 1f)); float rr = .30f + .16f * T + SV * .1f; float ex = MathF.Sin(an) * (1 + SV * .75f), ey = -MathF.Cos(an) * (1 + T * .85f) - (hd.Tex == 3 && T > .5f ? .05f : 0); if (fade && MathF.Abs(an) > 1.25f) continue; if (ey > .35f + hd.SideLen * .3f) continue; var c = H(ex, ey) + HairSway(.35f + MathF.Max(0, -ey - .6f) * .5f, i * .6f); p.Ellipse(c.X, c.Y, rx * rr * .72f, ry * rr * .62f); }
            p.CutBlob(H(.8f, -.2f), H(.5f, hl + .02f), H(0, hl + .04f), H(-.5f, hl + .02f), H(-.8f, -.2f), H(-.84f, .5f), H(0, .9f), H(.84f, .5f));
        }
        HairClip(); p.Shade(HairInk(-1, false, 0, 3.4f));
        if (hd.Tex == 2 && bumpy && fine) for (int i = 0; i < 16; i++) { float an = M.Lerp(-1.6f, 1.6f, M.Hash(i, 2)), q = .72f + M.Hash(i, 4) * (.3f + T); var c = H(MathF.Sin(an) * q, -MathF.Cos(an) * q); p.Curve(c + new Vector2(-1.4f, .4f), c + new Vector2(0, -1.7f), c + new Vector2(1.4f, .3f), .4f, default, true, 1, i % 3 == 0 ? 1.25f : .74f); }
        if (hd.Name is "Cornrow Tails") for (int i = -3; i <= 3; i++) { p.Curve(H(i * .22f + fx, hl), H(i * .26f, -.85f), H(i * .2f, -1.05f), .6f, default, true, 1, .62f); p.Curve(H(i * .22f + .1f + fx, hl), H(i * .26f + .1f, -.85f), H(i * .2f + .06f, -1.05f), .4f, default, true, 1, 1.25f); }
        if (hd.U == Updo.CrownBraid && !hatCover) for (int i = 0; i < 13; i++) { float an = M.Lerp(-1.35f, 1.35f, i / 12f); var c = H(MathF.Sin(an) * .95f, -MathF.Cos(an) * .9f - .04f); p.Begin().Ellipse(c.X, c.Y, 1.9f, 1.6f); p.Shade(HairInk(-1, false, (i & 1) == 0 ? -.08f : .08f, 1.6f)); }
        Fringes(f, fx, hl, T, SV);
        if (hd.U == Updo.BraidTails) { for (int s = -1; s <= 1; s += 2) { var a = H(s * .98f, .42f); BraidTail(a, H(s * 1.02f, 2.05f) + HairSway(2)); } }
        if (hd.U is Updo.HighPony or Updo.TopKnot or Updo.ManBun && hd.Tex < 2) for (int i = -2; i <= 2; i++) StrandLines(H(i * .3f + fx, hl - .03f), H(i * .25f, -.8f), H(i * .08f, -1.02f - T), .8f);
    }

    void Fringes(Fringe f, float fx, float hl, float T, float SV)
    {
        float rx = r.HeadRx; var sw0 = HairSway(.95f); var sw = sw0; bool hat = hatCover; float lift = hat ? 0 : T; float w = rx * .2f;
        void L(float x0, float y0, float x1, float y1, float x2, float y2, float w0, float w1, float w2, int id)
        { float len = Vector2.Distance(H(x0, y0), H(x2, y2)) / r.HeadRy; var sw = HairSway(MathF.Min(1.5f, .35f + len * .8f), id); p.Begin(); Lock(H(x0 + fx * .5f, y0), H(x1 + fx, y1 + po.Bend * .1f) + sw * .25f, H(x2 + fx, y2 + po.Bend * .2f) + sw, w0, w1, w2, id); HairClip(); p.Shade(HairInk(hd.Tex >= 4 ? id : -1)); if (hd.Tex < 2 && fine) StrandLines(H(x0 + fx * .5f, y0), H(x1 + fx, y1), H(x2 + fx, y2) + sw, .8f); }
        float top = hat ? -.62f : -.95f - lift * .75f;
        switch (f)
        {
            case Fringe.Blunt:
                p.Begin().Blob(H(-.9f, -.5f), H(-.5f, -.86f - lift * .3f), H(0, -.92f - lift * .4f), H(.5f, -.86f - lift * .3f), H(.9f, -.5f), H(.92f, -.05f) + sw * .3f, H(.5f + fx, -.14f) + sw, H(fx, -.12f) + sw, H(-.5f + fx, -.14f) + sw, H(-.92f, -.05f) + sw * .3f); HairClip(); p.Shade(HairInk(-1, false, 0, 2.4f)); for (int i = 0; i < 7; i++) { float u = (i + .5f) / 7 * 2 - 1; StrandLines(H(u * .7f, -.7f), H(u * .85f + fx, -.4f), H(u * .86f + fx, -.1f) + sw, .78f); } break;
            case Fringe.Curtain: case Fringe.Middle:
                { float o = f == Fringe.Curtain ? 1 : .75f, dy = f == Fringe.Curtain ? -.02f : .12f; for (int s = -1; s <= 1; s += 2) { L(s * .4f, top + .06f, s * .88f, -.8f - lift * .3f, s * (1.0f + SV * .4f), dy + .2f, w, w * 1.2f, w * .4f, 13 + s); L(s * .2f, top + .02f, s * .5f * o, -.72f - lift * .5f, s * .88f, dy, w * .85f, w * 1.2f, w * .35f, 11 + s); } if (hd.Tex < 4) p.Line(H(fx * .5f, top), H(fx, hl), .4f, ((Col)d.Hair.Base).Shade(-.7f)); break; }
            case Fringe.Side: L(.5f, top + .06f, -.35f, -1.0f - lift * .4f, -.92f, -.02f, w * 1.1f, w * 1.6f, w * .4f, 12); L(.45f, top + .05f, -.1f, -.8f - lift * .3f, -.55f, -.22f, w, w * 1.2f, w * .25f, 14); L(.48f, top + .05f, .85f, -.8f, .95f, -.1f, w * .9f, w, w * .4f, 15); break;
            case Fringe.Messy: for (int i = 0; i < 6; i++) { float u = i / 5f * 2 - 1; L(u * .5f + .15f, top + .1f + MathF.Abs(u) * .12f, u * .75f + .1f, -.72f, u * .78f + (M.Hash(i, 2) - .5f) * .25f - .05f, -.2f - M.Hash(i, 5) * .14f, w * .8f, w * .95f, w * .15f, 20 + i); } break;
            case Fringe.Wispy: for (int i = 0; i < 5; i++) { float u = i / 4f * 2 - 1; L(u * .35f, top + .1f, u * .6f, -.66f, u * .68f, -.16f + MathF.Abs(u) * .2f, w * .55f, w * .5f, w * .08f, 30 + i); } for (int s = -1; s <= 1; s += 2) L(s * .4f, top + .12f, s * .95f, -.6f, s * 1.0f, .3f, w * .7f, w * .6f, w * .1f, 40 + s); break;
            case Fringe.Spiky: if (hat) { for (int i = 0; i < 5; i++) { float u = i / 4f * 2 - 1; L(u * .5f, -.6f, u * .7f, -.45f, u * .75f, -.2f, w * .7f, w * .6f, w * .1f, i); } break; } for (int i = 0; i < 9; i++) { float u = i / 8f * 2 - 1; L(u * .7f, -.72f, u * .95f, -1.1f - T * .4f, u * 1.05f + .12f, -1.12f - T * (1.05f - u * u * .7f), w * .8f, w * .6f, .15f, 50 + i); } for (int i = 0; i < 5; i++) { float u = i / 4f * 2 - 1; L(u * .5f, -.75f, u * .6f, -.6f, u * .66f, -.28f, w * .7f, w * .6f, .15f, 70 + i); } break;
            case Fringe.Quiff: if (hat) { L(.4f, -.6f, -.2f, -.5f, -.8f, -.22f, w, w, w * .3f, 3); break; } for (int i = 0; i < 6; i++) { float u = i / 5f * 2 - 1; L(u * .68f, hl + .02f, u * .85f - .1f, hl - .5f - T * .7f, u * .6f + .24f, -1.02f - T * (1.05f - MathF.Abs(u) * .35f), w * 1.1f, w * 1.2f, w * .4f, 60 + i); } break;
            case Fringe.Pomp: if (hat) { L(.4f, -.6f, -.2f, -.5f, -.8f, -.22f, w, w, w * .3f, 3); break; } p.Begin().Blob(H(-.72f, hl), H(-.92f, -.95f - T * .4f), H(-.45f, -1.15f - T) + sw * .5f, H(.2f, -1.2f - T) + sw * .6f, H(.8f, -1 - T * .6f) + sw * .4f, H(.8f, -.62f), H(.4f, hl - .06f), H(0, hl + .02f), H(-.4f, hl)); p.Shade(HairInk(-1, false, .04f, 4.5f)); for (int i = 0; i < 5; i++) StrandLines(H(-.6f + i * .3f, hl), H(-.75f + i * .34f, -1 - T * .4f), H(-.1f + i * .16f, -1.1f - T * .9f), .78f); break;
            case Fringe.Slick: for (int i = -3; i <= 3; i++) { var a = H(i * .22f + fx, hl); p.Begin().Ellipse(HC.X, HC.Y, rx * 1.2f, r.HeadRy * 1.4f); p.IntersectAlpha(); StrandLines(a, H(i * .27f, -.85f), H(i * .2f, -1.03f - T * .8f), .8f); } break;
            case Fringe.Emo: L(.55f, top + .06f, -.5f, -1.0f, -.72f, .34f, w * 1.2f, w * 1.9f, w * .3f, 12); L(.5f, top + .05f, -.15f, -.8f, -.3f, .1f, w, w * 1.6f, w * .25f, 14); L(.45f, top + .06f, .1f, -.7f, .12f, -.2f, w * .9f, w, w * .2f, 15); L(.52f, top + .06f, .9f, -.75f, .98f, .1f, w, w * 1.1f, w * .4f, 16); break;
            case Fringe.Puff:
                if (hd.Tex is 2 or 3 && (T > .2f || hat)) { p.Begin(); int n = 7; for (int i = 0; i < n; i++) { float u = i / (n - 1f) * 2 - 1; var c = H(u * .68f + fx, hl + .0f - (1 - u * u) * .06f + (hd.Tex == 2 ? M.Hash(i, 3) * .14f : 0)); p.Ellipse(c.X, c.Y, rx * .17f, rx * .16f); } HairClip(); p.Shade(HairInk(-1, false, .03f, 1.6f)); }
                break;
        }
    }

    // ---------------------------------------------------------------- accessories
    void Glasses()
    {
        var pc = d[Slot.Glasses]; if (pc.Id == 0) return; Col A = pc.A, B = pc.B; float fx = po.Turn * .17f; Vector2 F2(float x, float y) => H(x + fx, y);
        bool shade = pc.Id is 3 or 6 or 7 or 8; float al = shade ? .82f : .18f;
        for (int s = -1; s <= 1; s += 2)
        {
            if (pc.Id == 9 && s < 0) continue; var c = F2(s * .41f, .09f); float rx = 2.7f, ry = 2.5f; p.Begin();
            switch (pc.Id)
            {
                case 2: case 6: p.Poly(c + new Vector2(-rx, -ry * .85f), c + new Vector2(rx, -ry * .85f), c + new Vector2(rx * .92f, ry * .8f), c + new Vector2(-rx * .92f, ry * .8f)); break;
                case 3: p.Blob(c + new Vector2(-rx, -ry * .9f), c + new Vector2(rx, -ry * .9f), c + new Vector2(rx * (s > 0 ? 1.05f : .6f), ry * .8f), c + new Vector2(0, ry * 1.2f), c + new Vector2(-rx * (s > 0 ? .6f : 1.05f), ry * .8f)); break;
                case 4: p.Blob(c + new Vector2(-s * rx, -ry * .4f), c + new Vector2(s * rx * 1.25f, -ry * 1.15f), c + new Vector2(s * rx * .8f, ry * .6f), c + new Vector2(-s * rx * .5f, ry * .8f)); break;
                case 8: p.Ellipse(c.X - 1.1f, c.Y - .8f, 1.6f, 1.6f).Ellipse(c.X + 1.1f, c.Y - .8f, 1.6f, 1.6f).Poly(c + new Vector2(-2.6f, -.4f), c + new Vector2(2.6f, -.4f), c + new Vector2(0, 2.8f)); break;
                case 10: p.Ellipse(c.X, c.Y, rx * 1.25f, ry * 1.2f); break;
                case 11: p.Blob(c + new Vector2(-rx * .9f, .5f), c + new Vector2(rx * .9f, .5f), c + new Vector2(rx * .6f, ry * .9f), c + new Vector2(-rx * .6f, ry * .9f)); break;
                default: p.Ellipse(c.X, c.Y, rx * .95f, ry); break;
            }
            if (pc.Id == 5) { p.Tint(B, .15f); p.Line(c + new Vector2(-rx, -ry * .6f), c + new Vector2(rx, -ry * .6f), .9f, A, false); continue; }
            var m = p.CopyMask(); if (pc.Id == 10) { p.Shade(new Ink(A, 1.2f, .5f)); p.Begin().Ellipse(c.X, c.Y, rx * .85f, ry * .8f); m = p.CopyMask(); p.Flat(B); al = 1; }
            else { p.Tint(shade ? B.Shade(-.2f) : B, al); }
            if (fine) { p.Line(c + new Vector2(-1.4f, .8f), c + new Vector2(.6f, -1.4f), .5f, 0xffffff, true, .55f); }
            else p.Dot(c.X - 1, c.Y - 1.2f, 0xffffff, .5f);
            // frame = edge of the lens mask
            if (pc.Id != 10) { p.Shade(new Ink((x, y) => A, .1f, 0, 0) { Ao = 0, Alpha = 0.001f }); FrameEdge(m, A); }
        }
        if (pc.Id != 9) { p.Curve(F2(-.12f, .04f), F2(0, -.04f), F2(.12f, .04f), .55f, A, false); for (int s = -1; s <= 1; s += 2) p.Line(F2(s * .72f, .02f), F2(s * .98f, .06f), .5f, A, false); }
        else p.Curve(F2(.6f, .2f), F2(.8f, .7f), F2(.6f, 1.1f), .35f, A, false);
        if (pc.Id == 10) for (int s = -1; s <= 1; s += 2) p.Line(F2(s * .75f, .05f), F2(s * 1.02f, .0f), 1.4f, A.Shade(-.3f), false);
    }
    void FrameEdge(byte[] m, Col c)
    {
        int W = p.W, H2 = p.H; int th = fine ? Math.Max(1, (int)(S * .4f)) : 1; var rc = c.Rl();
        for (int y = th; y < H2 - th; y++) for (int x = th; x < W - th; x++)
            {
                if (m[y * W + x] == 0) continue; bool e = false; for (int k = 1; k <= th && !e; k++) e = m[y * W + x - k] == 0 || m[y * W + x + k] == 0 || m[(y - k) * W + x] == 0 || m[(y + k) * W + x] == 0;
                if (e && p.Px[y * W + x].A > 0) p.Px[y * W + x] = rc;
            }
    }
    void Earrings()
    {
        var pc = d[Slot.Earrings]; if (pc.Id == 0) return; Col A = pc.A, B = pc.B;
        for (int s = -1; s <= 1; s += 2)
        {
            if (pc.Id == 7 && s < 0) continue; var e = H(s * 1.03f, .36f); var sw = new Vector2(po.HairX * .2f, 0);
            switch (pc.Id)
            {
                case 1: case 4: p.Begin().Ellipse(e.X, e.Y, .75f, .75f); p.Shade(new Ink(A, .6f, .7f, .7f) { Ao = 0 }); break;
                case 2: case 7: p.Begin().Ellipse(e.X, e.Y + 1.7f, 1.7f, 1.9f).CutEllipse(e.X, e.Y + 1.7f, 1, 1.2f); p.Shade(new Ink(A, .6f, .6f, .5f) { Ao = 0 }); break;
                case 3: p.Line(e, e + sw + new Vector2(0, 2), .35f, A, false); p.Begin().Blob(e + sw + new Vector2(0, 1.4f), e + sw + new Vector2(.9f, 3.2f), e + sw + new Vector2(0, 4), e + sw + new Vector2(-.9f, 3.2f)); p.Shade(new Ink(B, .8f, .6f) { Ao = 0 }); break;
                case 5: p.Line(e, e + sw + new Vector2(0, 1), .35f, A, false); Star(e + sw + new Vector2(0, 2.2f), 1.5f, A, B); break;
                case 6: p.Begin().Blob(e, e + sw + new Vector2(1.1f, 3), e + sw + new Vector2(0, 5.4f), e + sw + new Vector2(-1.1f, 3)); p.Shade(new Ink(A, 1, .5f) { Ao = 0 }); p.Line(e, e + sw + new Vector2(0, 5), .3f, B); break;
                case 8: p.Line(e, e + sw + new Vector2(0, 2.4f), .45f, A, false); p.Curve(e + sw + new Vector2(0, 2.4f), e + sw + new Vector2(-s * .2f, 4), e + sw + new Vector2(-s * 1.4f, 2.8f), .45f, A, false); p.Dot(e.X, e.Y + 1.4f, B); break;
            }
        }
    }

    void Hat()
    {
        var pc = d[Slot.Hat]; if (pc.Id == 0) return; var t = Catalog.Hats[pc.Id]; Col A = pc.A, B = pc.B, C = pc.C; float w = 1.07f + MathF.Min(.22f, hd.SideVol * (1 - hd.Flat * .6f) * .45f) + (hd.Name == "Bald" || hd.Fade > .4f ? -.02f : .03f), fx = po.Turn * .16f, rx = r.HeadRx, ry = r.HeadRy;
        var hc = HC; float rot = po.HeadTilt; Func<float, float, Col> fn = pc.Pat == 0 ? (x, y) => A : (x, y) => Patterns.Get(pc.Pat, x - hc.X, y - hc.Y + ry, A, B, C, pc.Scale * .8f);
        Ink K(Col c, float rd = 2.6f, float ao = 1) => new Ink(c, rd, .55f) { Ao = ao }; Ink KF(float rd = 3.4f) => new Ink(fn, rd, .55f) { Ao = 1 };
        float by = -.36f; // where the hat meets the forehead
        void Dome(float top, float ww, float bottom = -.36f) => p.Begin().Blob(H(-ww, bottom), H(-ww * .98f, bottom - .35f), H(-ww * .62f, top * .9f), H(0, top), H(ww * .62f, top * .9f), H(ww * .98f, bottom - .35f), H(ww, bottom), H(ww * .5f, bottom + .06f), H(0, bottom + .09f), H(-ww * .5f, bottom + .06f));
        void Band(float y0, float y1, float ww, Col c, float bow = .09f) { p.Begin().Blob(H(-ww, y0), H(-ww * .5f, y0 + bow * .7f), H(0, y0 + bow), H(ww * .5f, y0 + bow * .7f), H(ww, y0), H(ww, y1), H(ww * .5f, y1 + bow * .7f), H(0, y1 + bow), H(-ww * .5f, y1 + bow * .7f), H(-ww, y1)); p.Shade(K(c, 1.6f, .8f)); }
        void Brim(float bw, float y, float droop, float th, Col c, float curl = 0)
        {
            p.Begin().Blob(H(-bw, y + droop - curl), H(-bw * .6f, y - .04f), H(0, y - .08f), H(bw * .6f, y - .04f), H(bw, y + droop - curl), H(bw * .96f, y + droop + th * .7f - curl), H(bw * .55f, y + th), H(0, y + th + .05f), H(-bw * .55f, y + th), H(-bw * .96f, y + droop + th * .7f - curl)); p.Shade(K(c, 1.8f, 1.6f));
        }
        void Rib(float y0, float y1, float ww) { if (fine) for (float x = -ww + .06f; x < ww; x += .13f) p.Line(H(x, y0 + (1 - x * x) * .07f), H(x, y1 + (1 - x * x) * .07f), .3f, default, true, 0.0001f); }
        switch (t.K)
        {
            case HatKind.Beanie: case HatKind.PomBeanie: case HatKind.CatBeanie: case HatKind.Frog:
                if (t.K == HatKind.PomBeanie) { var s = HairSway(.3f); var q = H(0, -1.42f) + s; p.Begin(); for (int i = 0; i < 9; i++) { float an = i * M.TAU / 9; p.Ellipse(q.X + M.Sin(an) * 2.1f, q.Y + M.Cos(an) * 2, 1.4f, 1.4f); } p.Ellipse(q.X, q.Y, 2.4f, 2.4f); p.Shade(K(C, 2.4f)); }
                if (t.K == HatKind.CatBeanie) for (int s = -1; s <= 1; s += 2) { p.Begin().Poly(H(s * .3f, -1.1f), H(s * .82f, -1.62f), H(s * .98f, -.86f)); p.Shade(KF(2)); p.Begin().Poly(H(s * .48f, -1.12f), H(s * .78f, -1.42f), H(s * .88f, -.98f)); p.Shade(K(C, 1, 0)); }
                if (t.K == HatKind.Frog) for (int s = -1; s <= 1; s += 2) { var q = H(s * .58f, -1.18f); p.Begin().Ellipse(q.X, q.Y, 3, 3); p.Shade(KF(2)); if (po.Back) continue; p.Begin().Ellipse(q.X, q.Y - .2f, 1.9f, 1.9f); p.Shade(K(0xfaf4ea, 1.4f, 0)); p.Begin().Ellipse(q.X + po.LookX * .6f, q.Y, .9f, 1.1f); p.Flat(0x1a1014); }
                Dome(-1.3f, w); p.Shade(KF()); if (fine && t.K != HatKind.Frog) for (int i = -3; i <= 3; i++) p.Curve(H(i * .28f, -.5f), H(i * .27f, -1), H(i * .1f, -1.28f), .4f, default, true, 1, .84f);
                if (t.K == HatKind.Frog && !po.Back) { p.Curve(H(-.4f + fx, -.72f), H(fx, -.55f), H(.4f + fx, -.72f), .5f, A.Ink()); for (int s = -1; s <= 1; s += 2) { var q = H(s * .62f + fx, -.66f); p.Begin().Ellipse(q.X, q.Y, 1, .7f); p.Tint(0xe58a8a, .7f); } break; }
                Band(-.66f, by + .02f, w + .04f, B); if (fine) for (float x = -w; x < w; x += .16f) p.Curve(H(x, -.68f), H(x, -.5f), H(x, by + .12f), .3f, default, true, 1, .8f); break;
            case HatKind.Cap: case HatKind.CapBack: case HatKind.Propeller:
                Dome(-1.2f, w, by - .06f); Func<float, float, Col> cf = fn; p.Shade(KF()); p.Curve(H(fx, -1.2f), H(fx * 2.4f, -.8f), H(fx * 2.4f, by), .4f, default, true, 1, .74f); p.Curve(H(-.1f, -1.18f), H(-.7f, -1), H(-.75f, by), .4f, default, true, 1, .8f); p.Curve(H(.1f, -1.18f), H(.7f, -1), H(.75f, by), .4f, default, true, 1, .8f);
                { var q = H(0, -1.21f); p.Begin().Ellipse(q.X, q.Y, 1, .6f); p.Shade(K(B, .8f, 0)); }
                if (t.K == HatKind.Propeller) { var q = H(0, -1.26f); p.Line(q, q + new Vector2(0, -1.8f), .6f, C, false); float sp = MathF.Abs(M.Cos(po.T * (4 + MathF.Abs(po.HairX) * 2))); p.Begin().Ellipse(q.X - 2.6f * sp, q.Y - 2, 2.6f * sp + .3f, .7f); p.Shade(K(B, .8f, 0)); p.Begin().Ellipse(q.X + 2.6f * sp, q.Y - 2, 2.6f * sp + .3f, .7f); p.Shade(K(C, .8f, 0)); }
                if (po.Back) { Band(by - .14f, by + .02f, w + .01f, A.Shade(-.08f)); if (t.K == HatKind.CapBack) { p.Begin().Blob(H(-.84f, by - .06f), H(0, by - .12f), H(.84f, by - .06f), H(.92f, by + .16f), H(0, by + .3f), H(-.92f, by + .16f)); p.Shade(K(B, 2, 2)); } break; }
                if (t.K == HatKind.CapBack) { Band(by - .14f, by + .02f, w + .01f, A.Shade(-.08f)); p.Begin().Blob(H(-.36f + fx, by + .02f), H(fx, by - .34f), H(.36f + fx, by + .02f), H(fx, by - .16f)); p.Shade(K(HairC(H(fx, -.5f).X, H(fx, -.5f).Y, 1, false).Shade(-.2f), 1, 0)); p.Curve(H(-.4f + fx, by + .04f), H(fx, by - .6f), H(.4f + fx, by + .04f), .6f, B, false); }
                else { { var q = H(fx * 1.6f, -.78f); Star(q, 1.9f, C, B); } p.Begin().Blob(H(-.86f + fx, by - .08f), H(fx, by - .16f), H(.86f + fx, by - .08f), H(.98f + fx * 2.4f, by + .12f), H(.5f + fx * 2.4f, by + .27f), H(fx * 2.4f, by + .31f), H(-.5f + fx * 2.4f, by + .27f), H(-.98f + fx * 2.4f, by + .12f)); p.Shade(K(B, 2, 2)); if (fine) p.Curve(H(-.85f + fx * 2, by + .08f), H(fx * 2.4f, by + .25f), H(.85f + fx * 2, by + .08f), .3f, default, true, 1, .78f); }
                break;
            case HatKind.Bucket: Dome(-1.18f, w * .95f, by - .1f); p.Shade(KF()); Brim(w + .22f, by - .06f, .16f, .2f, B); if (fine) for (int i = 0; i < 2; i++) p.Curve(H(-w - .1f, by + .1f + i * .06f), H(0, by + .08f + i * .09f), H(w + .1f, by + .1f + i * .06f), .25f, default, true, 1, .82f); { var q = H(-.6f, -.75f); p.Begin().Ellipse(q.X, q.Y, .5f, .5f); p.Flat(C); } break;
            case HatKind.Wide: case HatKind.Fedora: case HatKind.Cowboy: case HatKind.Safari:
                {
                    float ct = t.K == HatKind.Cowboy ? -1.62f : t.K == HatKind.Safari ? -1.35f : -1.5f, cw = t.K == HatKind.Wide ? .92f : .9f;
                    if (t.K == HatKind.Cowboy) { p.Begin().Blob(H(-cw, by - .1f), H(-cw * .95f, -1.15f), H(-.55f, ct), H(-.12f, ct + .2f), H(.12f, ct + .2f), H(.55f, ct), H(cw * .95f, -1.15f), H(cw, by - .1f)); }
                    else if (t.K == HatKind.Safari) Dome(ct, cw, by - .1f);
                    else p.Begin().Blob(H(-cw, by - .1f), H(-cw * .9f, -1.1f), H(-.6f, ct + .06f), H(0, ct + (t.K == HatKind.Fedora ? .14f : .04f)), H(.6f, ct + .06f), H(cw * .9f, -1.1f), H(cw, by - .1f));
                    p.Shade(KF()); if (t.K != HatKind.Safari) { p.Curve(H(-.15f, ct + .16f), H(-.05f, ct + .4f), H(0, ct + .5f), .6f, default, true, 1, .74f); p.Curve(H(.3f, ct + .2f), H(.5f, ct + .4f), H(.55f, ct + .6f), .5f, default, true, 1, .8f); }
                    Band(by - .36f, by - .12f, cw + .02f, B); Brim(t.K == HatKind.Cowboy ? 1.95f : t.K == HatKind.Wide ? 1.8f : 1.5f, by - .06f, t.K == HatKind.Safari ? .16f : .04f, .17f, A, t.K == HatKind.Cowboy ? .26f : 0); { var q = H(.62f, by - .22f); p.Begin().Blob(q, q + new Vector2(2.4f, -3), q + new Vector2(1, -.6f)); if (t.K == HatKind.Fedora) p.Shade(K(C, 1, 0)); else { p.Begin().Ellipse(q.X, q.Y, 1, 1); p.Shade(K(C, .8f, 0)); } }
                    break;
                }
            case HatKind.Beret: p.Begin().Blob(H(-1.02f, by - .04f), H(-1.2f, -.62f), H(-.5f, -1.2f), H(.4f, -1.3f), H(1.3f, -1.02f), H(1.36f, -.62f), H(1.0f, by - .06f), H(0, by - .02f)); p.Shade(KF(4)); p.Line(H(.25f, -1.28f), H(.32f, -1.44f), .8f, A.Shade(-.3f), false); p.Curve(H(1.2f, -.8f), H(.6f, -.75f), H(.4f, -.5f), .5f, default, true, 1, .82f); Band(by - .14f, by, 1.02f, B, .06f); break;
            case HatKind.Top: p.Begin().Poly(H(-.8f, by - .1f), H(-.86f, -1.9f), H(.86f, -1.9f), H(.8f, by - .1f)); p.Shade(KF(3)); Band(by - .42f, by - .12f, .84f, B); Brim(1.35f, by - .06f, -.02f, .16f, A, .06f); { var q = H(0, -1.9f); p.Begin().Ellipse(q.X, q.Y, rx * .86f, 1); p.Shade(K(A.Shade(.15f), 1, 0)); } break;
            case HatKind.Headband: Band(-.66f, -.5f, 1.02f + hSide * .4f, A, .12f); if (pc.Pat != 0) { p.Shade(new Ink(fn, 1.2f, .4f)); } { var q = H(fx + .02f, -.5f); } break;
            case HatKind.Crown: p.Begin().Poly(H(-.78f, -.72f), H(-.92f, -1.42f), H(-.5f, -1.12f), H(-.28f, -1.55f), H(0, -1.16f), H(.28f, -1.55f), H(.5f, -1.12f), H(.92f, -1.42f), H(.78f, -.72f), H(0, -.62f)); p.Shade(K(A, 2, 1.5f)); Band(-.86f, -.72f, .8f, A.Shade(.2f), .1f); for (int i = -1; i <= 1; i++) { var q = H(i * .45f, -.78f + .08f * (1 - MathF.Abs(i))); p.Begin().Ellipse(q.X, q.Y, 1, 1); p.Shade(K(i == 0 ? B : C, .8f, 0)); } break;
            case HatKind.Flower: for (int i = 0; i < 9; i++) { float an = M.Lerp(-1.25f, 1.25f, i / 8f); var q = H(MathF.Sin(an) * (1.02f + hSide * .4f), -MathF.Cos(an) * .42f - .42f + .16f); p.Begin().Ellipse(q.X + 1.4f, q.Y + .6f, 1.2f, .7f); p.Shade(K(0x5f8a52, .8f, .4f)); p.Begin(); for (int k = 0; k < 5; k++) { float a2 = k * M.TAU / 5 + i; p.Ellipse(q.X + M.Sin(a2) * 1.2f, q.Y + M.Cos(a2) * 1.2f, 1, 1); } p.Shade(K(i % 3 == 0 ? A : i % 3 == 1 ? B : C, 1, .6f)); p.Begin().Ellipse(q.X, q.Y, .7f, .7f); p.Flat(0xf0d070); } break;
            case HatKind.Phones: { float tp = -1.06f - hTop * .9f; var ar = new Vector2[15]; for (int i = 0; i < 15; i++) { float an = M.Lerp(-M.PI / 2, M.PI / 2, i / 14f); ar[i] = H(MathF.Sin(an) * 1.1f, MathF.Cos(an) * (tp - .06f)); } p.Begin(); p.Stroke(ar, 2f, A.Ink(), 1, false); p.Stroke(ar, 1.2f, A, 1, false); for (int s = -1; s <= 1; s += 2) { var q = H(s * 1.1f, .12f); p.Begin().Ellipse(q.X, q.Y, 2.2f, 3.1f); p.Shade(K(B, 1.6f)); p.Begin().Ellipse(q.X + s * .5f, q.Y, 1.2f, 2); p.Shade(K(A, 1, 0)); } break; }
            case HatKind.Earmuffs: { float tp = -1.06f - hTop * .9f; var ar = new Vector2[15]; for (int i = 0; i < 15; i++) { float an = M.Lerp(-M.PI / 2, M.PI / 2, i / 14f); ar[i] = H(MathF.Sin(an) * 1.08f, MathF.Cos(an) * (tp - .04f)); } p.Stroke(ar, 1.3f, B, 1, false); for (int s = -1; s <= 1; s += 2) { var q = H(s * 1.08f, .14f); p.Begin(); for (int i = 0; i < 8; i++) { float an = i * M.TAU / 8; p.Ellipse(q.X + M.Sin(an) * 1.8f, q.Y + M.Cos(an) * 1.9f, 1.3f, 1.3f); } p.Ellipse(q.X, q.Y, 2, 2); p.Shade(K(A, 2.2f)); } break; }
            case HatKind.Trapper: for (int s = -1; s <= 1; s += 2) { p.Begin().Blob(H(s * .86f, by), H(s * 1.22f, by), H(s * 1.26f, .5f) + HairSway(.3f), H(s * 1.02f, .72f) + HairSway(.4f), H(s * .88f, .4f)); p.Shade(K(A, 2)); p.Begin(); for (int i = 0; i < 5; i++) { var q = H(s * 1.06f, -.1f + i * .17f); p.Ellipse(q.X, q.Y, 1.5f, 1.4f); } p.Shade(K(B, 1.4f, 0)); } Dome(-1.24f, w); p.Shade(KF()); p.Begin(); for (int i = 0; i < 9; i++) { float u = i / 8f * 2 - 1; var q = H(u * w, -.5f + (1 - u * u) * .08f); p.Ellipse(q.X, q.Y, 1.8f, 2.1f); } p.Shade(K(B, 1.8f)); break;
            case HatKind.Witch: { var s = HairSway(.8f); p.Begin().Blob(H(-.88f, by - .1f), H(-.6f, -1.2f), H(-.12f, -2.1f) + s * .5f, H(.55f, -2.5f) + s, H(.36f, -2.0f) + s * .6f, H(.5f, -1.3f), H(.88f, by - .1f)); p.Shade(KF(3)); p.Curve(H(-.5f, -1.3f), H(0, -1.15f), H(.4f, -1.4f), .5f, default, true, 1, .74f); p.Curve(H(-.3f, -1.8f), H(.1f, -1.7f), H(.4f, -1.9f), .5f, default, true, 1, .78f); Band(by - .34f, by - .12f, .88f, B); Brim(1.85f, by - .06f, .08f, .16f, A); { var q = H(0, by - .16f); p.Begin().Rect(q.X - 1.5f, q.Y - 1.5f, 3, 3); p.Shade(K(C, .8f, 0)); p.Begin().Rect(q.X - .7f, q.Y - .7f, 1.4f, 1.4f); p.Flat(B); } Star(H(-.3f, -1.05f), 1.2f, C, B); Star(H(.2f, -1.55f), .9f, C, B); break; }
            case HatKind.Straw: { Dome(-1.14f, .92f, by - .1f); Func<float, float, float> weave = (x, y) => (M.Mod((int)MathF.Floor(y * 1.1f) + (int)MathF.Floor(x * .9f), 2) == 0 ? -.10f : .05f); p.Shade(new Ink(fn, 3, .5f) { Tone = weave }); Band(by - .34f, by - .12f, .94f, B); float bw = 1.9f, y = by - .06f; p.Begin().Blob(H(-bw, y + .1f), H(-bw * .6f, y - .05f), H(0, y - .09f), H(bw * .6f, y - .05f), H(bw, y + .1f), H(bw * .95f, y + .22f), H(bw * .5f, y + .2f), H(0, y + .24f), H(-bw * .5f, y + .2f), H(-bw * .95f, y + .22f)); p.Shade(new Ink(A, 1.6f, .5f) { Ao = 1.8f, Tone = weave }); break; }
            case HatKind.Visor: Band(-.62f, -.44f, 1.04f + hSide * .4f, A, .12f); p.Begin().Blob(H(-.86f + fx, -.42f), H(fx, -.46f), H(.86f + fx, -.42f), H(.96f + fx * 2.4f, -.22f), H(.5f + fx * 2.4f, -.08f), H(fx * 2.4f, -.04f), H(-.5f + fx * 2.4f, -.08f), H(-.96f + fx * 2.4f, -.22f)); p.Shade(K(B, 2, 2)); break;
            case HatKind.Bandana: Dome(-1.14f, w * .99f); p.Shade(new Ink(pc.Pat == 0 ? (x, y) => Patterns.Get(6, x - hc.X, y - hc.Y, A, B, C, .8f) : fn, 3, .5f)); { var k = H(1.04f, by - .1f); var s = HairSway(1.1f); p.Begin().Blob(k, k + new Vector2(4, -.8f) + s, k + new Vector2(4.4f, 1.6f) + s).Blob(k, k + new Vector2(3.4f, 2) + s, k + new Vector2(2.6f, 4) + s); p.Shade(K(A, 1.2f)); p.Begin().Ellipse(k.X, k.Y, 1.3f, 1.2f); p.Shade(K(A, 1)); } p.Curve(H(-.9f, -.5f), H(0, -.62f), H(.9f, -.5f), .4f, default, true, 1, .8f); break;
            case HatKind.Party: { var s = HairSway(.3f); float b0 = -.86f - hTop * .7f; p.Begin().Poly(H(-.46f, b0), H(.1f, b0 - 1.1f) + s, H(.5f, b0)); p.Shade(new Ink(pc.Pat == 0 ? (x, y) => Patterns.Get(15, x - hc.X, y - hc.Y, A, B, C, 1) : fn, 2, .5f)); var q = H(.1f, b0 - 1.14f) + s; p.Begin().Ellipse(q.X, q.Y, 1.5f, 1.5f); p.Shade(K(C, 1.2f)); p.Curve(H(-.46f, b0), H(-1.0f, -.4f), H(-.88f, .4f), .3f, C, false); p.Curve(H(.5f, b0), H(1.0f, -.4f), H(.88f, .4f), .3f, C, false); break; }
            case HatKind.Sailor: p.Begin().Blob(H(-.82f, -.7f), H(-.6f, -1.14f), H(0, -1.22f), H(.6f, -1.14f), H(.82f, -.7f)); p.Shade(K(A, 2.6f)); p.Begin().Blob(H(-1.02f, by - .1f), H(-1.1f, -.82f), H(0, -.74f), H(1.1f, -.82f), H(1.02f, by - .1f), H(0, by - .02f)); p.Shade(K(A, 1.6f)); p.Curve(H(-1.04f, -.52f), H(0, -.42f), H(1.04f, -.52f), .6f, B); break;
            case HatKind.Newsboy: p.Begin().Blob(H(-1.06f, by - .08f), H(-1.3f, -.72f), H(-.7f, -1.2f), H(0, -1.24f), H(.7f, -1.2f), H(1.3f, -.72f), H(1.06f, by - .08f), H(0, by)); p.Shade(KF(3.6f)); for (int i = -1; i <= 1; i++) p.Curve(H(i * .06f, -1.22f), H(i * .75f, -.95f), H(i * .72f + fx, by - .05f), .4f, default, true, 1, .76f); { var q = H(0, -1.23f); p.Begin().Ellipse(q.X, q.Y, 1, .7f); p.Shade(K(B, .8f, 0)); } p.Begin().Blob(H(-.74f + fx, by - .1f), H(fx, by - .16f), H(.74f + fx, by - .1f), H(.8f + fx * 2, by + .06f), H(fx * 2, by + .18f), H(-.8f + fx * 2, by + .06f)); p.Shade(K(A.Shade(-.12f), 1.6f, 1.8f)); break;
            case HatKind.Bowler: Dome(-1.24f, .95f, by - .1f); p.Shade(KF()); Band(by - .3f, by - .1f, .97f, B); Brim(1.34f, by - .04f, .02f, .14f, A, .12f); break;
            case HatKind.Chef: p.Begin(); for (int i = 0; i < 5; i++) { float u = i / 4f * 2 - 1; var q = H(u * .72f, -1.32f + u * u * .16f); p.Ellipse(q.X, q.Y, rx * .38f, ry * .32f); } p.Poly(H(-.85f, -1.3f), H(.85f, -1.3f), H(.92f, -.6f), H(-.92f, -.6f)); p.Shade(K(A, 2.6f)); for (int i = -2; i <= 2; i++) p.Curve(H(i * .34f, -.68f), H(i * .36f, -1), H(i * .4f, -1.2f), .4f, default, true, 1, .82f); Band(-.72f, by, 1.0f, B); break;
            case HatKind.Bow: { var q = H(.5f, -.86f - hTop * .6f); var s = HairSway(.25f); p.Begin().Blob(q, q + new Vector2(-5, -3) + s, q + new Vector2(-5, 2.6f) + s).Blob(q, q + new Vector2(5, -3) + s, q + new Vector2(5, 2.6f) + s); p.Shade(KF(2)); p.Begin().Ellipse(q.X, q.Y, 1.5f, 1.7f); p.Shade(K(B, 1.2f)); p.Curve(q + new Vector2(-1, 0), q + new Vector2(-2.5f, -.6f), q + new Vector2(-4, -.4f), .4f, A.Shade(-.4f), false); p.Curve(q + new Vector2(1, 0), q + new Vector2(2.5f, -.6f), q + new Vector2(4, -.4f), .4f, A.Shade(-.4f), false); break; }
        }
    }
}

using System.Numerics;
namespace UntitledGame;

/// The finer things that make a character look like a particular person: skin, makeup, tattoos and piercings.
public sealed partial class AvatarPainter
{
    /// Skin colour at a point, with vitiligo patches where the person has them.
    Col SkinAt(float x, float y)
    {
        if (d.Vitiligo < .02f || show == Show.Mannequin) return skin; float n = M.Noise((x - HC.X) * .32f + 3, (y - HC.Y) * .32f, 41) + M.Noise((x - HC.X) * .8f, (y - HC.Y) * .8f, 42) * .3f; float t = .75f - d.Vitiligo * 1.1f;
        return n > t + .06f ? Col.Lerp(skin, 0xf8e8dc, .78f) : n > t ? Col.Lerp(skin, 0xf8e8dc, .4f) : skin;
    }
    Ink SkinInk(float round, float light, float ao, float grad = .12f) => d.Vitiligo < .02f || show == Show.Mannequin ? new Ink(skin, round, light) { Ao = ao, Grad = grad } : new Ink(SkinAt, round, light) { Ao = ao, Grad = grad };
    Col LipCol => Col.Lerp(d.Lip, d.LipstickCol, d.Lipstick);

    /// The head outline, reshaped by the jaw, chin and cheek sliders.
    void HeadShape()
    {
        float j = M.Lerp(.74f, 1.2f, d.Jaw), ch = M.Lerp(-.1f, .18f, d.Chin), cp = M.Clamp((d.Chin - .5f) * 1.6f), k = M.Lerp(.88f, 1.1f, d.Cheeks);
        Span<Vector2> q = [H(0, -1), H(-.62f, -.82f), H(-.97f, -.3f), H(-.96f * k, .22f), H(-.76f * j, .68f + ch * .3f), H(-.32f * j * (1 - cp * .35f), .96f + ch * .6f), H(0, 1 + ch), H(.32f * j * (1 - cp * .35f), .96f + ch * .6f), H(.76f * j, .68f + ch * .3f), H(.96f * k, .22f), H(.97f, -.3f), H(.62f, -.82f)];
        p.Begin().Blob(q, 1, 5); p.Shade(SkinInk(4.2f, .5f, 2.4f, .1f));
    }

    /// Freckles, lines, shadows, moles and birthmarks: painted on the skin before the features.
    void SkinDetail(Func<float, float, Vector2> F2)
    {
        if (show == Show.Mannequin) return; float rx = r.HeadRx, ry = r.HeadRy;
        if (d.Freckles > .02f) { int n = (int)(d.Freckles * (fine ? 46 : 7)); for (int i = 0; i < n; i++) { float u = M.Hash(i, 61) * ((i & 1) == 0 ? 1 : -1), v = M.Hash(i, 62); var f = F2(u * (.25f + .5f * v), .16f + v * .34f + MathF.Abs(u) * .05f); if (fine) { p.Begin().Ellipse(f.X, f.Y, .32f, .3f); p.IntersectAlpha(); p.Tint(SkinAt(f.X, f.Y).Shade(-.45f), .6f + M.Hash(i, 63) * .3f); } else Pix((int)MathF.Floor(OX(f.X)), (int)MathF.Floor(OY(f.Y)), skin.Shade(-.25f)); } }
        if (d.UnderEyes > .02f) for (int s = -1; s <= 1; s += 2) { var c = F2(s * EyeX, EyeY + .17f); p.Begin().Ellipse(c.X, c.Y, rx * .2f, ry * .055f); p.IntersectAlpha(); p.Tint(0x6a4458, d.UnderEyes * .45f); }
        if (!fine) { foreach (var (mx, my) in Moles()) { var m = F2(mx, my); Pix((int)MathF.Floor(OX(m.X)), (int)MathF.Floor(OY(m.Y)), 0x4a2a22); } return; }
        Col line = skin.Shade(-.42f);
        if (d.Wrinkles > .04f) { float a = d.Wrinkles; int rows = a > .66f ? 3 : a > .33f ? 2 : 1; for (int i = 0; i < rows; i++) p.Curve(F2(-.38f + i * .04f, -.48f - i * .1f), F2(0, -.44f - i * .1f), F2(.38f - i * .04f, -.48f - i * .1f), .28f, line, true, .4f + a * .5f); for (int s = -1; s <= 1; s += 2) { p.Line(F2(s * .70f, .02f), F2(s * .82f, -.04f), .26f, line, true, a * .9f); if (a > .5f) p.Line(F2(s * .70f, .10f), F2(s * .82f, .14f), .26f, line, true, a * .8f); if (a > .4f) p.Curve(F2(s * .28f, .28f), F2(s * .4f, .34f), F2(s * .52f, .28f), .24f, line, true, a * .6f); } }
        if (d.LaughLines > .04f) for (int s = -1; s <= 1; s += 2) p.Curve(F2(s * .22f, .40f), F2(s * .38f, .52f), F2(s * .34f, .70f), .3f, line, true, .35f + d.LaughLines * .55f);
        foreach (var (mx, my) in Moles()) { var m = F2(mx, my); p.Begin().Ellipse(m.X, m.Y, .45f, .42f); p.IntersectAlpha(); p.Flat(0x4a2a22); p.Dot(m.X - .2f, m.Y - .2f, 0x7a5040); }
        if (d.Birthmark > 0) { (float x, float y, float w, float h) b = d.Birthmark switch { 1 => (.55f, .42f, .22f, .14f), 2 => (-.32f, -.62f, .26f, .12f), 3 => (-.62f, .78f, .2f, .12f), 4 => (.80f, -.24f, .14f, .2f), _ => (.32f, 1.14f, .22f, .1f) }; var c = F2(b.x, b.y); p.Begin().Blob(c + new Vector2(-b.w * rx, 0), c + new Vector2(-b.w * rx * .4f, -b.h * ry), c + new Vector2(b.w * rx * .5f, -b.h * ry * .8f), c + new Vector2(b.w * rx, b.h * ry * .1f), c + new Vector2(b.w * rx * .3f, b.h * ry), c + new Vector2(-b.w * rx * .6f, b.h * ry * .7f)); p.IntersectAlpha(); p.Tint(0x9a3a4a, .42f); }
    }
    IEnumerable<(float, float)> Moles() { var m = d.Moles; if (m == null) yield break; for (int i = 0; i + 1 < m.Count; i += 2) yield return (m[i], m[i + 1]); }
    float EyeX => .40f + (d.EyeGap - .5f) * .14f; float EyeY => .07f - (d.EyeHeight - .5f) * .16f;

    void Scar(Func<float, float, Vector2> F2, bool afterEyes)
    {
        if (d.Scar == 0 || (d.Scar == 5) != afterEyes) return; Col c = Col.Lerp(skin, 0xf6c8c0, .55f).Shade(.28f); (float, float, float, float) l = d.Scar switch { 1 => (.40f, -.40f, .50f, -.12f), 2 => (-.56f, .24f, -.38f, .46f), 3 => (.12f, .54f, .07f, .74f), 4 => (-.12f, .14f, .12f, .28f), 5 => (.40f, -.30f, .46f, .34f), _ => (-.1f, .93f, .12f, .88f) };
        var a = F2(l.Item1, l.Item2); var b = F2(l.Item3, l.Item4); if (!fine) { Pix((int)MathF.Floor(OX((a.X + b.X) / 2)), (int)MathF.Floor(OY((a.Y + b.Y) / 2)), c); return; } p.Line(a, b, .95f, c, true); p.Line(a + new Vector2(.4f, .2f), b + new Vector2(.4f, .2f), .3f, c.Shade(-.35f), true, .6f); if (d.Scar is 2 or 5) for (int i = 1; i < 4; i++) { var q = Vector2.Lerp(a, b, i / 4f); var n = Vector2.Normalize(new(-(b - a).Y, (b - a).X)) * .7f; p.Line(q - n, q + n, .25f, c.Shade(-.2f), true, .7f); }
    }

    // ---------------------------------------------------------------- makeup
    void EyeShadowAt(Vector2 c, float w, float h, int s)
    {
        if (d.Shadow > .02f) { p.Begin().Ellipse(c.X + s * w * .1f, c.Y - h * .55f, w * 1.25f, h * .95f); p.IntersectAlpha(); p.Tint(d.ShadowCol, d.Shadow * .7f); }
        if (d.Liner == 3) { p.Begin().Ellipse(c.X, c.Y, w * 1.35f, h * 1.15f); p.IntersectAlpha(); p.Tint(d.LinerCol, .32f); }
    }
    void EyeLinerAt(Func<float, float, Vector2> E, float w, float h, float topY, int s)
    {
        if (d.Liner == 0) return; Col c = d.LinerCol; float th = d.Liner is 4 ? .85f : d.Liner == 1 ? .42f : .6f;
        if (d.Liner != 5) { p.Curve(E(-w, .05f), E(0, topY * 1.8f - .2f), E(w, -.05f), th, c, false); if (d.Liner is 2 or 4 or 3) { float L = d.Liner == 4 ? 1.9f : 1.2f; p.Line(E(s * w * .9f, -.05f), E(s * (w + L), -.9f - (d.Liner == 4 ? .4f : 0)), th, c, false); } }
        if (d.Liner is 3 or 5) p.Curve(E(-w * .85f, h * .6f), E(0, h * 1.05f), E(w * .85f, h * .55f), .4f, c, false);
    }
    void Blusher(Func<float, float, Vector2> F2) { if (d.Blush < .02f) return; for (int s = -1; s <= 1; s += 2) { var c = F2(s * .55f, .40f); p.Begin().Ellipse(c.X, c.Y, r.HeadRx * .23f, r.HeadRy * .12f); p.IntersectAlpha(); p.Tint(d.BlushCol, d.Blush * .55f); } }
    /// Painted nails, drawn on whatever fingertips are showing.
    void Nails(Vector2 c, Vector2 dir, Vector2 n, float hr, bool open)
    {
        if (d.Nails == 0) return; Col k = d.Nails; if (!fine) { var t = c + dir * hr * 1.9f; Pix((int)MathF.Floor(OX(t.X)), (int)MathF.Floor(OY(t.Y)), k); return; }
        if (open) for (int i = 0; i < 4; i++) { float f = (i - 1.5f) * .62f, spread = (i - 1.5f) * .24f, L = i is 1 or 2 ? 1.5f : i == 0 ? 1.15f : 1.3f; var fb = c + dir * hr * 1.35f + n * hr * f * .78f; var fd = dir * M.Cos(spread) + n * M.Sin(spread); var t = fb + fd * hr * (L - .1f); p.Begin().Ellipse(t.X, t.Y, hr * .22f, hr * .22f); p.Flat(k); }
        else for (int i = 0; i < 3; i++) { var t = c + dir * hr * 1.7f + n * hr * (.35f - i * .3f); p.Begin().Ellipse(t.X, t.Y, hr * .2f, hr * .18f); p.Flat(k); }
    }

    // ---------------------------------------------------------------- piercings
    void Piercings(Func<float, float, Vector2> F2)
    {
        int f = d.Piercings; if (f == 0 || show == Show.Mannequin) return; Col m = d.Metal; float nz = M.Lerp(.75f, 1.3f, d.NoseSize) * M.Lerp(.8f, 1.3f, d.NoseWidth);
        void Stud(Vector2 c, float rr = .45f) { if (!fine) { Pix((int)MathF.Floor(OX(c.X)), (int)MathF.Floor(OY(c.Y)), m); return; } p.Begin().Ellipse(c.X, c.Y, rr, rr); p.Shade(new Ink(m, .4f, .8f, .5f) { Ao = 0 }); p.Dot(c.X - .15f, c.Y - .2f, ((Col)m).Shade(.7f)); }
        void Ring(Vector2 c, float rr, float from, float to) { if (!fine) { Pix((int)MathF.Floor(OX(c.X)), (int)MathF.Floor(OY(c.Y + rr)), m); return; } var q = new Vector2[9]; for (int i = 0; i < 9; i++) { float a = M.Lerp(from, to, i / 8f); q[i] = c + new Vector2(M.Sin(a), M.Cos(a)) * rr; } p.Stroke(q, .38f, m, 1, false); }
        bool On(int i) => (f & (1 << i)) != 0;
        if (On(0)) Stud(F2(.13f * nz + po.Turn * .06f, .34f));
        if (On(1)) Ring(F2(.15f * nz, .38f), .7f, -1.2f, 1.8f);
        if (On(2)) Ring(F2(po.Turn * .06f, .44f), .8f, -1.3f, 1.3f);
        if (On(3)) { var b = F2(EyeX + .2f, -.235f - (d.BrowHeight - .5f) * .16f - .06f); Stud(b + new Vector2(0, -1.1f), .35f); Stud(b + new Vector2(.3f, 1.1f), .35f); }
        if (On(4)) Ring(F2(.18f * M.Lerp(.8f, 1.25f, d.MouthWidth), .69f), .75f, -.3f, 2.6f);
        if (On(5)) Stud(F2(0, .76f), .4f);
        if (On(6)) for (int s = -1; s <= 1; s += 2) Stud(F2(s * .15f * M.Lerp(.8f, 1.25f, d.MouthWidth), .74f), .38f);
        if (On(7)) for (int s = -1; s <= 1; s += 2) Stud(F2(s * .07f, .07f), .32f);
        if (On(8)) Stud(F2(-.22f, .52f), .36f);
        if (On(9)) for (int s = -1; s <= 1; s += 2) Stud(F2(s * .46f, .60f), .38f);
        float es = M.Lerp(.8f, 1.3f, d.EarSize);
        if (On(10)) Ring(H(1.0f + .12f * es, .02f), .9f * es, 3.4f, 5.6f);
        if (On(11)) { var a = H(.98f, -.07f); var b = H(1.1f + .08f * es, .14f); if (fine) { p.Line(a, b, .4f, m, false); Stud(a, .4f); Stud(b, .4f); } else Pix((int)MathF.Floor(OX(b.X)), (int)MathF.Floor(OY(b.Y)), m); }
    }

    // ---------------------------------------------------------------- tattoos
    Col TatInk => d.TatCol;
    /// Draws one design. L maps (along, across) in design units to the canvas; k is the size of one unit.
    public static void TattooArt(Painter p, int id, Func<float, float, Vector2> L, float k, Col ink, bool color)
    {
        if (id <= 0) return; Vector2 V(float u, float v) => L(u, v); float w = MathF.Max(.3f, k * .14f); Col red = color ? 0xc8384a : ink, green = color ? 0x3f8a4a : ink, gold = color ? 0xe0a83a : ink, blue = color ? 0x3a72b8 : ink;
        void Fill(Col c) { p.IntersectAlpha(); p.Flat(c); }
        switch (id)
        {
            case 1: p.Line(V(-2.2f, 0), V(2.2f, 0), w, ink, true); p.Line(V(-1.2f, -1.1f), V(-1.2f, 1.1f), w, ink, true); p.Curve(V(1.4f, -1.8f), V(3.2f, 0), V(1.4f, 1.8f), w, ink, true); p.Begin().Ellipse(V(-2.7f, 0).X, V(-2.7f, 0).Y, k * .55f, k * .55f).CutEllipse(V(-2.7f, 0).X, V(-2.7f, 0).Y, k * .28f, k * .28f); Fill(ink); break;
            case 2: { var a = V(-.6f, -.75f); var b = V(-.6f, .75f); p.Begin().Ellipse(a.X, a.Y, k * .95f, k * .95f).Ellipse(b.X, b.Y, k * .95f, k * .95f).Poly(V(-.5f, -1.65f), V(-.5f, 1.65f), V(1.9f, 0)); Fill(red); if (color) { p.Begin().Ellipse(a.X, a.Y, k * .95f, k * .95f).Ellipse(b.X, b.Y, k * .95f, k * .95f).Poly(V(-.5f, -1.65f), V(-.5f, 1.65f), V(1.9f, 0)); p.Stroke([V(-1.5f, -.75f), V(-.6f, -1.7f), V(0, 0), V(-.6f, 1.7f), V(-1.5f, .75f)], w * .7f, ink, 1, true); } break; }
            case 3: { var c = V(-.4f, 0); p.Begin().Ellipse(c.X, c.Y, k * 1.25f, k * 1.25f); Fill(red); for (int i = 0; i < 3; i++) { float rr = (1 - i * .3f) * 1.1f; p.Curve(V(-.4f - rr, 0), V(-.4f, -rr * 1.2f), V(-.4f + rr, 0), w * .7f, ink, true); } p.Line(V(.8f, 0), V(2.6f, .2f), w, green, true); p.Begin().Ellipse(V(1.7f, .7f).X, V(1.7f, .7f).Y, k * .5f, k * .32f); Fill(green); break; }
            case 4: { var q = new Vector2[10]; for (int i = 0; i < 10; i++) { float a = i * M.PI / 5, R = (i & 1) == 0 ? 2.1f : .9f; q[i] = V(-M.Cos(a) * R, M.Sin(a) * R); } p.Begin().Poly(q); Fill(color ? gold : ink); break; }
            case 5: p.Begin().Blob(V(-1.6f, -2.2f), V(-.2f, -.8f), V(.2f, 0), V(-.4f, 1.1f), V(1.2f, 1.6f), V(.9f, .3f), V(1.6f, -.4f), V(.4f, -.6f)); Fill(blue); p.Begin().Ellipse(V(.8f, .9f).X, V(.8f, .9f).Y, k * .35f, k * .35f); Fill(red); break;
            case 6: p.Begin().Poly(V(-2.6f, 0), V(.6f, -.5f), V(.6f, .5f)); Fill(color ? 0xb8c0c8 : ink); p.Line(V(.7f, -1.2f), V(.7f, 1.2f), w * 1.4f, gold, true); p.Line(V(.8f, 0), V(2.3f, 0), w * 1.6f, ink, true); p.Begin().Ellipse(V(2.5f, 0).X, V(2.5f, 0).Y, k * .4f, k * .4f); Fill(red); break;
            case 7: { var q = new Vector2[12]; for (int i = 0; i < 12; i++) { float t = i / 11f; q[i] = V(-2.8f + t * 5.4f, M.Sin(t * 9) * 1.1f); } p.Stroke(q, w * 2.2f, green, 1, true); p.Begin().Ellipse(V(2.8f, M.Sin(9) * 1.1f).X, V(2.8f, M.Sin(9) * 1.1f).Y, k * .6f, k * .45f); Fill(green); break; }
            case 8: { var c = V(0, 0); var o = V(-.5f, .55f); p.Begin().Ellipse(c.X, c.Y, k * 1.7f, k * 1.7f).CutEllipse(o.X, o.Y, k * 1.45f, k * 1.45f); Fill(color ? gold : ink); break; }
            case 9: for (int i = 0; i < 3; i++) p.Curve(V(-1.8f + i * 1.3f, -1.6f), V(-2.6f + i * 1.3f, 0), V(-1.8f + i * 1.3f, 1.4f), w, blue, true); p.Curve(V(1.6f, -1.8f), V(2.6f, 0), V(1.6f, 1.8f), w, blue, true); break;
            case 10: for (int i = 0; i < 3; i++) { var q = new Vector2[10]; for (int j = 0; j < 10; j++) q[j] = V(-1.2f + i * 1.2f + M.Sin(j * 2.1f + i) * .25f, -2.2f + j * .5f); p.Stroke(q, w * .8f, ink, 1, true); } break;
            case 14: p.Line(V(-2.4f, 0), V(2.4f, 0), w * 1.4f, ink, true); p.Line(V(-1, -1.3f), V(-1, 1.3f), w * 1.4f, ink, true); break;
            case 15: { p.Begin().Ellipse(V(.8f, 0).X, V(.8f, 0).Y, k * 1, k * .85f); Fill(ink); foreach (var (u, v) in new[] { (-.9f, -1.1f), (-1.4f, -.35f), (-1.4f, .35f), (-.9f, 1.1f) }) { p.Begin().Ellipse(V(u, v).X, V(u, v).Y, k * .38f, k * .38f); Fill(ink); } break; }
            case 16: foreach (var (u, v, rr) in new[] { (-.8f, -1.2f, 1.1f), (-.8f, 1.2f, 1.1f), (.8f, -.9f, .8f), (.8f, .9f, .8f) }) { p.Begin().Ellipse(V(u, v).X, V(u, v).Y, k * rr, k * rr * .85f); Fill(v < 0 ? blue : (color ? 0x8a5ac8 : ink)); } p.Line(V(-1.6f, 0), V(1.6f, 0), w * 1.2f, ink, true); break;
            case 17: p.Stroke([V(-2.6f, .6f), V(-.4f, -.6f), V(-.2f, .5f), V(2.6f, -.6f)], w * 1.6f, gold, 1, true); break;
            case 18: for (int i = 0; i < 3; i++) { p.Begin().Ellipse(V(-1.2f + i * 1.2f, 0).X, V(-1.2f + i * 1.2f, 0).Y, k * .38f, k * .38f); Fill(ink); } break;
            case 19: { var c = V(0, 0); p.Begin().Ellipse(c.X, c.Y, k * 1.1f, k * 1.1f); Fill(gold); for (int i = 0; i < 8; i++) { float a = i * M.TAU / 8; p.Line(V(M.Cos(a) * 1.5f, M.Sin(a) * 1.5f), V(M.Cos(a) * 2.3f, M.Sin(a) * 2.3f), w, gold, true); } break; }
            case 20: p.Line(V(-2.6f, .3f), V(2.6f, -.3f), w, ink, true); for (int i = 0; i < 7; i++) { float u = -1.8f + i * .6f, s = (i & 1) == 0 ? 1 : -1; p.Line(V(u, -u * .1f), V(u - .9f, -u * .1f + s * 1.1f), w * .7f, color ? (i < 3 ? blue : (Col)0x2f8a8a) : ink, true); } break;
            default: break;
        }
    }
    /// Tattoos on the skin of an arm or the back of a hand. Sleeves are drawn afterwards and cover them.
    void ArmTattoos(int sd, Vector2 a, Vector2 b, Vector2 c, Vector2 hand, Vector2 dir, Vector2 n, float hr)
    {
        if (show == Show.Mannequin || d.Tattoos == null) return; Col ink = TatInk;
        for (int up = 0; up < 2; up++)
        {
            int id = d.Tattoos[(sd < 0 ? 0 : 1) + up * 2]; if (id == 0) continue; float t = up == 1 ? .26f : .74f;
            if (id is 11 or 12)
            {
                float t0 = id == 11 ? t - .05f : (up == 1 ? .04f : .56f), t1 = id == 11 ? t + .05f : (up == 1 ? .48f : .97f); p.Begin(); Limb3(a, b, c, t0, t1, 0); p.IntersectAlpha();
                if (id == 11) { var (q0, d0) = LimbAt(a, b, c, t0 + .015f); var (q1, d1) = LimbAt(a, b, c, t1 - .015f); var n0 = new Vector2(-d0.Y, d0.X) * 8; var n1 = new Vector2(-d1.Y, d1.X) * 8; p.Line(q0 - n0, q0 + n0, .9f, ink); p.Line(q1 - n1, q1 + n1, .9f, ink); var qm = LimbAt(a, b, c, t).pos; var dm = LimbAt(a, b, c, t).dir; var nm = new Vector2(-dm.Y, dm.X); for (int i = -2; i <= 2; i++) p.Begin().Poly(qm + nm * i * 1.6f - dm * 1.1f, qm + nm * (i * 1.6f + .8f) + dm * 1.1f, qm + nm * (i * 1.6f - .8f) + dm * 1.1f).IntersectAlpha().Flat(ink); continue; }
                // a full sleeve: swirls and petals packed along the arm
                for (int i = 0; i < 9; i++) { float tt = M.Lerp(t0, t1, (i + .5f) / 9); var (q, dd) = LimbAt(a, b, c, tt); var nn = new Vector2(-dd.Y, dd.X); float s = (i & 1) == 0 ? 1 : -1; p.Begin(); Limb3(a, b, c, t0, t1, 0); p.Curve(q - nn * 3 * s, q + dd * 2.4f, q + nn * 3 * s, .55f, i % 3 == 0 && d.TatColor ? 0xc8384a : ink, true); p.Curve(q + nn * 1.2f * s, q - dd * 1.4f, q - nn * 1.4f * s, .45f, d.TatColor && i % 3 == 1 ? 0x3a72b8 : ink, true); }
                continue;
            }
            var (pos, dr) = LimbAt(a, b, c, t); var nr = new Vector2(-dr.Y, dr.X) * sd; float k = MathF.Max(.5f, ArmRad(t) * .42f); p.Begin(); Limb3(a, b, c, t - .16f, t + .16f, 0); var mask = p.CopyMask();
            TattooArt(p, id, (u, v) => pos + dr * u * k + nr * v * k * .82f, k, ink, d.TatColor);
        }
        int hd2 = d.Tattoos[6]; if (hd2 > 0) { float k = hr * .32f; if (hd2 == 18) { for (int i = 0; i < 3; i++) { var q = hand + dir * hr * 1.3f + n * hr * (.4f - i * .4f); if (fine) { p.Begin().Ellipse(q.X, q.Y, .3f, .3f); p.IntersectAlpha(); p.Flat(ink); } } } else { p.Begin().Ellipse(hand.X + dir.X * hr * .8f, hand.Y + dir.Y * hr * .8f, hr * .7f, hr * .7f); var ctr = hand + dir * hr * .85f; TattooArt(p, hd2, (u, v) => ctr + dir * u * k + n * v * k, k, ink, d.TatColor); } }
    }
    void NeckTattoo()
    {
        int id = d.Tattoos?[4] ?? 0; if (id == 0 || show == Show.Mannequin) return; var c = Tp(r.Neck * .38f, r.Torso + r.NeckLen * .45f); float k = r.Neck * .24f; p.Begin().Ellipse(c.X, c.Y, r.Neck * 1.2f, r.NeckLen * 2); TattooArt(p, id, (u, v) => c + new Vector2(v * k, u * k), k, TatInk, d.TatColor);
    }
    void FaceTattoo(Func<float, float, Vector2> F2)
    {
        int id = d.Tattoos?[5] ?? 0; if (id == 0 || show == Show.Mannequin) return; var c = F2(-EyeX - .02f, EyeY + .30f); float k = fine ? .34f : .5f; if (!fine) { Pix((int)MathF.Floor(OX(c.X)), (int)MathF.Floor(OY(c.Y)), TatInk); return; }
        p.Begin().Ellipse(c.X, c.Y, 3, 3); if (id == 2 || id == 4 || id == 14 || id == 18 || id == 8) TattooArt(p, id, (u, v) => c + new Vector2(v * k, u * k), k, TatInk, d.TatColor); else { p.Begin().Blob(c + new Vector2(0, -.9f), c + new Vector2(.6f, .3f), c + new Vector2(0, 1.1f), c + new Vector2(-.6f, .3f)); p.IntersectAlpha(); p.Flat(TatInk); } // anything else becomes a small teardrop
    }
}

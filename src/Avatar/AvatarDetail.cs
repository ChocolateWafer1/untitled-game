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
    /// Whole-arm tattoo patterns. w runs around the arm and v down it (both in body units, v = 0 at the shoulder);
    /// L is the arm's length. Returns 0 for bare skin, 1 for ink and 2 for an accent part (coloured when TatColor is on).
    public static int TatPattern(int id, float w, float v, float L)
    {
        static float Fr(float x) => x - MathF.Floor(x);
        static float H(int a, int b) { uint h = (uint)(a * 374761393 + b * 668265263); h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; }
        switch (id)
        {
            case 1: { // tribal: bold blades that sweep across the arm, each one hooking the other way
                for (int o = 0; o < 2; o++) { float vv = v + o * 4.5f; int i = (int)MathF.Floor(vv / 9); float x = Fr(vv / 9), sd = ((i + o) & 1) == 0 ? 1 : -1; float cw = sd * 2.6f * MathF.Cos(x * M.PI), wd = 1.25f * MathF.Pow(MathF.Sin(x * M.PI), .75f) + (x > .5f ? 0 : .15f); if (MathF.Abs(w - cw) < wd && Fr(vv / 9 + .5f) > .04f) return 1; }
                return 0; }
            case 2: return Fr(v / 8) < .32f ? 1 : 0; // bands
            case 3: return Fr(v / 3.2f) < .3f ? 1 : 0; // fine rings
            case 4: return Fr((v + MathF.Abs(w) * 1.1f) / 5.5f) < .34f ? 1 : 0; // chevrons
            case 5: return Fr((v + w) / 5) < .2f || Fr((v - w) / 5) < .2f ? 1 : 0; // diamonds
            case 6: return Fr((v + MathF.Sin(w * 1.1f) * 1.6f) / 5) < .32f ? 2 : 0; // waves
            case 7: return Fr((v + w * 2.4f) / 6) < .34f ? 1 : 0; // spiral
            case 8: { int row = (int)MathF.Floor(v / 2.8f); float off = (row & 1) * 1.4f, cx = MathF.Round((w - off) / 2.8f) * 2.8f + off, cy = (row + .5f) * 2.8f; float dx = w - cx, dy = v - cy; return dx * dx + dy * dy < .72f ? 1 : 0; } // dotwork
            case 9: { // scales
                int row = (int)MathF.Floor(v / 2.6f); for (int rr = row; rr >= row - 1; rr--) { float off = (rr & 1) * 1.8f, cx = MathF.Round((w - off) / 3.6f) * 3.6f + off, cy = rr * 2.6f; float dd = MathF.Sqrt((w - cx) * (w - cx) + (v - cy) * (v - cy)); if (v > cy && MathF.Abs(dd - 1.75f) < .38f) return 2; } return 0; }
            case 10: { // vines: a winding stem with leaves on alternating sides
                float ws = MathF.Sin(v * .22f) * 2.2f; if (MathF.Abs(w - ws) < .42f) return 1;
                int i = (int)MathF.Round(v / 3.6f); float lv = i * 3.6f, s = (i & 1) == 0 ? 1 : -1, lw = MathF.Sin(lv * .22f) * 2.2f + s * 1.25f; float du = (w - lw) / 1.15f, dv = (v - lv - s * (w - lw) * .5f) / .6f; return du * du + dv * dv < 1 ? 2 : 0; }
            case 11: { // flowers
                int row = (int)MathF.Floor(v / 5.6f); float off = (row & 1) * 3, cx = MathF.Round((w - off) / 6) * 6 + off, cy = (row + .5f) * 5.6f; float dx = w - cx, dy = v - cy, rr = MathF.Sqrt(dx * dx + dy * dy), a = MathF.Atan2(dy, dx);
                if (rr < .55f) return 1; return rr < 2f * (.45f + .55f * MathF.Abs(MathF.Cos(a * 2.5f))) ? 2 : 0; }
            case 12: { // stars scattered over the arm
                for (int gy = (int)MathF.Floor(v / 5) - 1; gy <= (int)MathF.Floor(v / 5) + 1; gy++) for (int gx = (int)MathF.Floor(w / 5) - 1; gx <= (int)MathF.Floor(w / 5) + 1; gx++)
                { float cx = (gx + .2f + H(gx, gy) * .6f) * 5, cy = (gy + .2f + H(gy, gx + 7) * .6f) * 5, sz = .9f + H(gx + 3, gy) * .9f; float dx = w - cx, dy = v - cy, rr = MathF.Sqrt(dx * dx + dy * dy), a = MathF.Atan2(dy, dx) + H(gx, gy + 5); if (rr < sz * (.42f + .58f * MathF.Pow(MathF.Abs(MathF.Cos(a * 2.5f)), 3))) return 2; }
                return 0; }
            case 13: return (((int)MathF.Floor(v / 2.6f) + (int)MathF.Floor(w / 2.6f)) & 1) == 0 ? 1 : 0; // checkered
            case 14: { float x = Fr(w / 3.2f) * 2 - 1; return Fr(v / 5.5f) < .62f * (1 - MathF.Abs(x)) ? 1 : 0; } // triangles
            case 15: { float zig = MathF.Abs(Fr(v / 4) * 2 - 1) * 2.6f; return Fr((w + zig) / 4.6f) < .3f ? 1 : 0; } // zigzag
            case 16: { // mandalas on the upper arm and the forearm, joined by rings
                foreach (float c in new[] { L * .27f, L * .72f }) { float dy = v - c, rr = MathF.Sqrt(w * w + dy * dy); if (rr < 4.4f) { float a = MathF.Atan2(dy, w); if (rr < .8f) return 2; if (rr > 1.5f && rr < 2.2f && MathF.Cos(a * 8) > -.2f) return 2; if (MathF.Abs(rr - 3.3f) < .32f) return 1; if (rr > 3.6f && MathF.Cos(a * 14) > .55f) return 1; return 0; } }
                return Fr(v / 2.2f) < .22f ? 1 : 0; }
            case 17: { float t = v / L; if (t < .5f) return Fr(v / 9) < .12f ? 1 : 0; return Fr((v - L * .5f) / 7.5f) < .1f ? 0 : 1; } // blackout forearm
            default: return 0;
        }
    }
    public static Col TatAccent(int id) => id switch { 6 => 0x3a72b8, 9 => 0x2f8a8a, 10 => 0x3f8a4a, 11 => 0xc8384a, 12 => 0xe0a83a, 16 => 0xc8384a, _ => 0xc8384a };
    public const int TW = 128, TH = 32; const float T0 = .02f, T1 = .96f;
    public static float ArmRadOf(Rig r, float t) => t < .5f ? M.Lerp(r.ArmR, r.ForeR, t * 2) : M.Lerp(r.ForeR, r.WristR, (t - .5f) * 2);
    /// Where a canvas column sits along the arm (0 shoulder .. 1 wrist).
    public static float ArmT(float ix) => T0 + (ix + .5f) / TW * (T1 - T0);
    /// Paints a pattern onto a fresh arm canvas in the character's chosen ink.
    public static byte[] Raster(int id, AvatarData d)
    {
        if (id <= 0) return null; var r = new Rig(d); float L = r.UArm + r.LArm; var pal = d.TatPal ?? Catalog.TatInks;
        int ink = Array.IndexOf(pal, d.TatCol) + 1; if (ink <= 0) ink = 2; int acc = d.TatColor ? Array.IndexOf(pal, TatAccent(id).Hex) + 1 : ink; if (acc <= 0) acc = ink;
        var a = new byte[TW * TH];
        for (int ix = 0; ix < TW; ix++) { float t = ArmT(ix), rad = ArmRadOf(r, t); for (int iy = 0; iy < TH; iy++) { float sa = (iy + .5f) / TH * 2 - 1; int k = TatPattern(id, sa * M.PI / 2 * rad, t * L, L); if (k > 0) a[iy * TW + ix] = (byte)(k == 2 ? acc : ink); } }
        return a;
    }
    /// The arm's skin with its tattoo painted into it, so the ink takes the same light and shadow. Sleeves cover it.
    Ink ArmInk(Ink sk, int sd, Vector2 a, Vector2 b, Vector2 c)
    {
        var art = show == Show.Mannequin || d.Art == null || d.Art.Length < 2 ? null : d.Art[sd < 0 ? 0 : 1]; if (art == null || art.Length != TW * TH) return sk;
        var uv = LimbUv(a, b, c); float L = r.UArm + r.LArm; var baseFn = sk.Fn; Col sc = sk.C; var pal = d.TatPal ?? Catalog.TatInks;
        sk.Fn = (x, y) =>
        {
            var b0 = baseFn != null ? baseFn(x, y) : sc; var (u, v) = uv(x, y); float t = v / L; if (t < T0 || t >= T1) return b0;
            int ix = Math.Clamp((int)((t - T0) / (T1 - T0) * TW), 0, TW - 1); float rad = ArmRad(t); float sa = MathF.Asin(M.Clamp(u / rad, -1, 1)) / (M.PI / 2) * sd;
            int iy = Math.Clamp((int)((sa + 1) * .5f * TH), 0, TH - 1); int k = art[iy * TW + ix];
            return k == 0 || k > pal.Length ? b0 : Col.Lerp(b0, pal[k - 1], .88f);
        };
        return sk;
    }
}

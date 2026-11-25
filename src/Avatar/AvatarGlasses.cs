using System.Numerics;
namespace UntitledGame;

public enum GShape { None, Round, Square, Aviator, CatEye, HalfRim, Heart, Monocle, Goggles, Reading, Rect, Wayfarer, Browline, Oval, Rimless, Big, Hex, Wire, Shield }
/// Thick is how many pixels the frame is when seen up close.
public sealed record GlassDef(string Name, GShape Shape, bool Shade = false, int Thick = 1);

public sealed partial class AvatarPainter
{
    void RoundRect(Vector2 c, float hw, float hh, float rr) { rr = MathF.Min(rr, MathF.Min(hw, hh)); p.Rect(c.X - hw + rr, c.Y - hh, (hw - rr) * 2, hh * 2).Rect(c.X - hw, c.Y - hh + rr, hw * 2, (hh - rr) * 2).Ellipse(c.X - hw + rr, c.Y - hh + rr, rr, rr).Ellipse(c.X + hw - rr, c.Y - hh + rr, rr, rr).Ellipse(c.X - hw + rr, c.Y + hh - rr, rr, rr).Ellipse(c.X + hw - rr, c.Y + hh - rr, rr, rr); }
    void Glasses()
    {
        var pc = d[Slot.Glasses]; if (pc.Id == 0) return; var g = Catalog.GlassDefs[pc.Id]; Col A = pc.A, B = pc.B; float fx = po.Turn * .17f; Vector2 F2(float x, float y) => H(x + fx, y);
        bool shade = g.Shade; float al = shade ? .84f : .16f; int th = fine ? Math.Max(1, (int)(S * .4f)) * g.Thick : 1;
        if (g.Shape == GShape.Shield)
        {
            // one curved shield across both eyes
            p.Begin().Blob(F2(-1.0f, -.06f), F2(-.62f, -.16f), F2(0, -.12f), F2(.62f, -.16f), F2(1.0f, -.06f), F2(.86f, .2f), F2(.5f, .28f), F2(.12f, .2f), F2(0, .12f), F2(-.12f, .2f), F2(-.5f, .28f), F2(-.86f, .2f)); var sm = p.CopyMask(); p.Tint(B.Shade(-.15f), .86f);
            if (fine) p.Line(F2(-.7f, .16f), F2(-.45f, -.06f), .5f, 0xffffff, true, .5f); else p.Dot(F2(-.55f, 0).X, F2(-.55f, 0).Y, 0xffffff, .5f); FrameEdge(sm, A, th); for (int s = -1; s <= 1; s += 2) p.Line(F2(s * .98f, -.02f), F2(s * 1.04f, .02f), .6f, A, false); return;
        }
        for (int s = -1; s <= 1; s += 2)
        {
            if (g.Shape == GShape.Monocle && s < 0) continue; var c = F2(s * .41f, .09f); float rx = 2.7f, ry = 2.5f; p.Begin();
            switch (g.Shape)
            {
                case GShape.Square: p.Poly(c + new Vector2(-rx, -ry * .85f), c + new Vector2(rx, -ry * .85f), c + new Vector2(rx * .92f, ry * .8f), c + new Vector2(-rx * .92f, ry * .8f)); break;
                case GShape.Aviator: p.Blob(c + new Vector2(-rx, -ry * .9f), c + new Vector2(rx, -ry * .9f), c + new Vector2(rx * (s > 0 ? 1.05f : .6f), ry * .8f), c + new Vector2(0, ry * 1.2f), c + new Vector2(-rx * (s > 0 ? .6f : 1.05f), ry * .8f)); break;
                case GShape.CatEye: p.Blob(c + new Vector2(-s * rx, -ry * .4f), c + new Vector2(s * rx * 1.25f, -ry * 1.15f), c + new Vector2(s * rx * .8f, ry * .6f), c + new Vector2(-s * rx * .5f, ry * .8f)); break;
                case GShape.Heart: p.Ellipse(c.X - 1.1f, c.Y - .8f, 1.6f, 1.6f).Ellipse(c.X + 1.1f, c.Y - .8f, 1.6f, 1.6f).Poly(c + new Vector2(-2.6f, -.4f), c + new Vector2(2.6f, -.4f), c + new Vector2(0, 2.8f)); break;
                case GShape.Goggles: p.Ellipse(c.X, c.Y, rx * 1.25f, ry * 1.2f); break;
                case GShape.Reading: p.Blob(c + new Vector2(-rx * .9f, .5f), c + new Vector2(rx * .9f, .5f), c + new Vector2(rx * .6f, ry * .9f), c + new Vector2(-rx * .6f, ry * .9f)); break;
                case GShape.Rect: case GShape.Rimless: RoundRect(c + new Vector2(0, .1f), rx * 1.04f, ry * .66f, .9f); break;
                case GShape.Big: RoundRect(c + new Vector2(s * .1f, .15f), rx * 1.18f, ry * 1.08f, 1.5f); break;
                case GShape.Wayfarer: p.Blob(c + new Vector2(-rx * 1.08f, -ry * .78f), c + new Vector2(rx * 1.08f, -ry * .86f), c + new Vector2(rx * .94f, ry * .42f), c + new Vector2(rx * .5f, ry * .78f), c + new Vector2(-rx * .5f, ry * .78f), c + new Vector2(-rx * .94f, ry * .42f)); break;
                case GShape.Browline: p.Blob(c + new Vector2(-rx * 1.04f, -ry * .7f), c + new Vector2(rx * 1.04f, -ry * .7f), c + new Vector2(rx * .98f, ry * .25f), c + new Vector2(rx * .45f, ry * .82f), c + new Vector2(-rx * .45f, ry * .82f), c + new Vector2(-rx * .98f, ry * .25f)); break;
                case GShape.Oval: p.Ellipse(c.X, c.Y + .1f, rx * 1.06f, ry * .72f); break;
                case GShape.Hex: { var q = new Vector2[6]; for (int i = 0; i < 6; i++) { float an = i * M.TAU / 6 + M.PI / 6; q[i] = c + new Vector2(M.Cos(an) * rx * 1.02f, M.Sin(an) * ry * .98f); } p.Poly(q); break; }
                case GShape.Wire: p.Ellipse(c.X, c.Y, rx * .82f, ry * .82f); break;
                default: p.Ellipse(c.X, c.Y, rx * .95f, ry); break;
            }
            if (g.Shape == GShape.HalfRim) { p.Tint(B, .15f); p.Line(c + new Vector2(-rx, -ry * .6f), c + new Vector2(rx, -ry * .6f), .9f, A, false); continue; }
            var m = p.CopyMask(); if (g.Shape == GShape.Goggles) { p.Shade(new Ink(A, 1.2f, .5f)); p.Begin().Ellipse(c.X, c.Y, rx * .85f, ry * .8f); m = p.CopyMask(); p.Flat(B); al = 1; }
            else p.Tint(shade ? B.Shade(-.2f) : B, al);
            if (fine) p.Line(c + new Vector2(-1.4f, .8f), c + new Vector2(.6f, -1.4f), .5f, 0xffffff, true, shade ? .4f : .55f); else p.Dot(c.X - 1, c.Y - 1.2f, 0xffffff, .5f);
            // the frame is the edge of the lens; browlines are heavy on top and fine underneath
            if (g.Shape == GShape.Rimless) { p.Dot(c.X + s * rx, c.Y - .3f, A); continue; }
            if (g.Shape != GShape.Goggles) FrameEdge(m, A, g.Shape == GShape.Browline ? Math.Max(1, th / 2) : th);
            if (g.Shape == GShape.Browline) { p.Begin(); RoundRect(c + new Vector2(0, -ry * .55f), rx * 1.12f, ry * .32f, .8f); p.Shade(new Ink(A, 1, .5f, .6f) { Ao = 0 }); }
            if (g.Shape == GShape.Wayfarer && fine) { p.Dot(c.X + s * rx * .9f, c.Y - ry * .6f, 0xe8d8c0); }
        }
        float bw = g.Thick > 1 ? .8f : .55f, by = g.Shape == GShape.Browline ? -.06f : 0;
        if (g.Shape != GShape.Monocle) { p.Curve(F2(-.12f, .04f + by), F2(0, -.04f + by), F2(.12f, .04f + by), bw, A, false); for (int s = -1; s <= 1; s += 2) p.Line(F2(s * .72f, .02f + by), F2(s * .98f, .06f), bw, A, false); }
        else p.Curve(F2(.6f, .2f), F2(.8f, .7f), F2(.6f, 1.1f), .35f, A, false);
        if (g.Shape == GShape.Goggles) for (int s = -1; s <= 1; s += 2) p.Line(F2(s * .75f, .05f), F2(s * 1.02f, .0f), 1.4f, A.Shade(-.3f), false);
    }
}

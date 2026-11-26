using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// Digging and building. A block fractures from the point you hit: its own shape splits into shards that loosen with each blow,
/// burst apart, then gather themselves back into a little chunk you can walk over and pick up.
public sealed partial class GameScreen
{
    const float Reach = 112; // how far your arms go, in pixels from the chest
    const int DTS = World.TS, Pad = 7, DW = DTS + Pad * 2, ShardN = 9;

    /// One cracked block: its pixels, cut into shards, and how loose they are.
    sealed class Dig
    {
        public int X, Y; public T Block; public float P, Shown = -1, Idle, Wob, WobV; public Vector2 Hit, Kick; public readonly Color[] Src; public readonly byte[] Id = new byte[DTS * DTS]; public readonly float[] Order = new float[DTS * DTS]; public readonly Vector2[] Cen = new Vector2[ShardN]; public readonly Vector2[] Way = new Vector2[ShardN]; public readonly Painter Pt = new(DW, DW); public Texture2D Tex; readonly bool[] edge = new bool[DTS * DTS];
        public Dig(int x, int y, T b, Color[] src, Vector2 hit, Rng r)
        {
            X = x; Y = y; Block = b; Src = src; Hit = hit; Span<Vector2> s = stackalloc Vector2[ShardN]; for (int i = 0; i < ShardN; i++) s[i] = new((i % 3 + .5f + r.R(-.42f, .42f)) * 8, (i / 3 + .5f + r.R(-.42f, .42f)) * 8); int seed = r.I(99999); var cnt = new int[ShardN];
            for (int py = 0; py < DTS; py++) for (int px = 0; px < DTS; px++) { float jx = px + M.Noise(px * .34f, py * .34f, seed) * 2.6f, jy = py + M.Noise(px * .34f, py * .34f, seed + 5) * 2.6f; float bd = 1e9f; int bi = 0; for (int i = 0; i < ShardN; i++) { float dd = (s[i].X - jx) * (s[i].X - jx) + (s[i].Y - jy) * (s[i].Y - jy); if (dd < bd) { bd = dd; bi = i; } } int k = py * DTS + px; Id[k] = (byte)bi; Cen[bi] += new Vector2(px + .5f, py + .5f); cnt[bi]++; Order[k] = Vector2.Distance(new(px, py), hit) / 30 + M.Hash(px / 3, py / 3, seed) * .14f; }
            for (int i = 0; i < ShardN; i++) { Cen[i] = cnt[i] > 0 ? Cen[i] / cnt[i] : s[i]; var w = Cen[i] - new Vector2(12, 12); Way[i] = (w.Length() > .5f ? Vector2.Normalize(w) : new Vector2(r.R(-1, 1), r.R(-1, 1))) * r.R(.7f, 1.35f); }
            for (int py = 0; py < DTS; py++) for (int px = 0; px < DTS; px++) { int k = py * DTS + px; edge[k] = (px + 1 < DTS && Id[k] != Id[k + 1]) || (py + 1 < DTS && Id[k] != Id[k + DTS]); }
            Tex = Pt.ToTexture();
        }
        /// Cracks run outwards from the impact; once they have crossed the block the shards start to part.
        public void Draw(Col glow, bool glows)
        {
            Array.Clear(Pt.Px); float part = M.Smooth(.42f, 1, P) * 2.5f, run = P * 1.34f; Col dark = ((Col)Blocks.Of(Block).Color).Shade(-.86f) * .8f;
            for (int k = 0; k < DTS * DTS; k++) if (Src[k].A > 0) Pt.Px[(k / DTS + Pad) * DW + k % DTS + Pad] = (glows ? Col.Lerp(dark, glow, M.Smooth(.2f, 1, P) * .8f) : dark).Rl();
            Span<int> ox = stackalloc int[ShardN], oy = stackalloc int[ShardN]; for (int i = 0; i < ShardN; i++) { float loosen = part * M.Clamp(1.5f - Vector2.Distance(Cen[i], Hit) / 30); ox[i] = (int)MathF.Round(Way[i].X * loosen); oy[i] = (int)MathF.Round(Way[i].Y * loosen + (loosen > .6f ? .4f : 0)); }
            for (int py = 0; py < DTS; py++) for (int px = 0; px < DTS; px++)
                {
                    int k = py * DTS + px; if (Src[k].A == 0) continue; int i = Id[k]; int o = (py + oy[i] + Pad) * DW + px + ox[i] + Pad; var c = Src[k]; bool crack = edge[k] && Order[k] < run;
                    if (crack) { var q = new Col(c.R / 255f, c.G / 255f, c.B / 255f); c = (glows ? Col.Lerp(q.Shade(-.7f), glow, .85f) : q.Shade(-.86f) * .75f).Rl(); } else { bool lit = (px > 0 && edge[k - 1] && Order[k - 1] < run) || (py > 0 && edge[k - DTS] && Order[k - DTS] < run); if (lit) { var q = new Col(c.R / 255f, c.G / 255f, c.B / 255f); c = q.Shade(.42f).Rl(); } }
                    Pt.Px[o] = c;
                }
            Pt.Upload(Tex); Shown = P;
        }
    }
    /// The moment of breaking: shards fly, hang, then rush back together into the drop.
    sealed class Shatter { public Texture2D Tex; public Vector2[] P = new Vector2[ShardN], V = new Vector2[ShardN], From = new Vector2[ShardN]; public float[] R = new float[ShardN], RV = new float[ShardN]; public Vector2[] Cen; public float T; public Loot Into; }
    sealed class Loot { public int Id, N = 1; public Vector2 P, V; public float T, Born, Sq, SqV, HopT, Pull; public bool Ground, Fresh; public Vector2 Swirl; }
    readonly Dictionary<int, Dig> digs = new(); readonly List<Shatter> shatters = new(); readonly List<Loot> loot = new(); readonly List<(int x, int y, float t, Vector2 from)> pops = new();
    (int x, int y) aim; bool digArmed, putArmed, aimOk, placeOk; float placeCd; Dig cur; float cursorA; Vector2 cursorP; float strikeFx; Vector2 strikeAt, strikeDir; int queued; float follow; readonly List<(Vector2 p, float t, Col c)> glints = new();

    Vector2 Chest => P.P + new Vector2(0, P.Crouch ? -26 : -38);
    static float BoxDist(Vector2 p, int x, int y) { float dx = MathF.Max(0, MathF.Max(x * DTS - p.X, p.X - (x + 1) * DTS)), dy = MathF.Max(0, MathF.Max(y * DTS - p.Y, p.Y - (y + 1) * DTS)); return MathF.Sqrt(dx * dx + dy * dy); }
    bool Exposed(int x, int y) => !W.Solid(x - 1, y) || !W.Solid(x + 1, y) || !W.Solid(x, y - 1) || !W.Solid(x, y + 1);
    public bool CanReach(int x, int y) => BoxDist(Chest, x, y) < Reach;
    bool CanPlace(int x, int y)
    {
        if (x < 2 || y < 2 || x >= World.W - 2 || y >= World.H - 4 || W.Solid(x, y) || !CanReach(x, y)) return false; if (!(W.Solid(x - 1, y) || W.Solid(x + 1, y) || W.Solid(x, y - 1) || W.Solid(x, y + 1) || W.WallAt(x, y) != T.Air)) return false;
        if (x * DTS < P.P.X + Player.HW && (x + 1) * DTS > P.P.X - Player.HW && y * DTS < P.P.Y && (y + 1) * DTS > P.P.Y - P.HH) return false; foreach (var p in W.RootedAt(x, y + 1)) if (p.Kind == 1) return false; return true;
    }
    T HandBlock { get { var d = Store.Data; return d.Hand >= 0 && !d.Slots[d.Hand].Empty ? (T)d.Slots[d.Hand].Id : T.Air; } }

    void Digging(float dt, bool control)
    {
        var wm = Ui.Mouse + new Vector2(CamX, CamY); int tx = (int)MathF.Floor(wm.X / DTS), ty = (int)MathF.Floor(wm.Y / DTS); bool free = control && !OverAdmin && !OverBar; if (free && Ui.Click) { digArmed = true; queued = 1; } if (!Ui.Held || !control) digArmed = false; if (free && Ui.RightClick) putArmed = true; if (!Ui.RightHeld || !control) putArmed = false; aim = (tx, ty); bool solid = W.Solid(tx, ty);
        aimOk = free && solid && Blocks.Minable(W.At(tx, ty)) && CanReach(tx, ty) && Exposed(tx, ty); placeOk = free && HandBlock != T.Air && CanPlace(tx, ty); var goal = new Vector2(tx * DTS, ty * DTS); cursorP = cursorA < .05f ? goal : M.Damp(cursorP, goal, 34, dt); cursorA = M.Damp(cursorA, aimOk || placeOk ? 1 : 0, 16, dt);
        // digging: holding keeps swinging; a single click still lands one whole blow and follows through
        if (!aimOk) { queued = 0; follow = 0; } follow -= dt;
        bool dig = aimOk && (digArmed || queued > 0 || follow > 0) && !P.Climb && !P.Frozen; P.Mining = dig; cur = null;
        if (dig)
        {
            var bd = Blocks.Of(W.At(tx, ty)); P.An.StrikeLen = bd.Time / Blows(bd); P.EmoteT = 0; var c = new Vector2(tx * DTS + 12, ty * DTS + 12); var v = c - Chest; P.MineAim = v.Length() > 1 ? Vector2.Normalize(v) : new(P.Dir, 0); int key = ty * World.W + tx; if (!digs.TryGetValue(key, out cur) || cur.Block != W.At(tx, ty)) cur = null; if (cur != null) cur.Idle = 0; P.An.Final = cur == null ? Blows(bd) == 1 : cur.P + 1f / Blows(bd) >= .999f; P.DustCol = bd.Color;
        }
        for (int i = digs.Count - 1; i >= 0; i--)
        {
            var (k, g) = digs.ElementAt(i); g.Idle += dt; g.WobV += (-g.Wob * 900 - g.WobV * 26) * dt; g.Wob += g.WobV * dt; if (W.At(g.X, g.Y) != g.Block) { Raylib.UnloadTexture(g.Tex); digs.Remove(k); continue; } if (g.Idle > 3.5f) { g.P -= dt * .5f; if (g.P <= 0) { Raylib.UnloadTexture(g.Tex); digs.Remove(k); continue; } } if (MathF.Abs(g.P - g.Shown) > .02f) g.Draw(Blocks.Of(g.Block).Glow, Blocks.Glows(g.Block));
        }
        strikeFx = MathF.Max(0, strikeFx - dt * 5); for (int i = glints.Count - 1; i >= 0; i--) { var q = glints[i]; q.t += dt; if (q.t > .45f) glints.RemoveAt(i); else glints[i] = q; }
        // building
        placeCd -= dt; bool again = putArmed && placeCd <= 0; if ((Ui.RightClick || again) && control && !OverAdmin && !OverBar) { if (placeOk) Place(tx, ty); else if (Ui.RightClick && HandBlock != T.Air && !solid && CanReach(tx, ty)) Snd.Play("error", .25f); else if (Ui.RightClick && HandBlock == T.Air && !solid) Toast("Nothing in your hand to place"); }
        for (int i = pops.Count - 1; i >= 0; i--) { var q = pops[i]; q.t += dt; if (q.t > .22f) pops.RemoveAt(i); else pops[i] = q; }
        Shards(dt); Looting(dt);
    }
    /// Every block takes a whole number of blows; harder ones take more, and the rhythm is tuned so the total time is exact.
    static int Blows(BlockDef d) => Math.Max(1, (int)MathF.Round(d.Time / .36f));
    /// Called by the animation at the exact frame the fist lands.
    public void OnStrike()
    {
        var (tx, ty) = aim; if (!aimOk || !P.Mining) return; int key = ty * World.W + tx; T b = W.At(tx, ty); var def = Blocks.Of(b); var ctr = new Vector2(tx * DTS + 12, ty * DTS + 12); var from = Chest; var face = Vector2.Clamp(from, new(tx * DTS + 1, ty * DTS + 1), new(tx * DTS + 22, ty * DTS + 22)); // the nearest point of the block to you is where it gets hit
        if (cur == null) { cur = new Dig(tx, ty, b, tiles.Paint(tx * DTS, ty * DTS, DTS, DTS), face - new Vector2(tx * DTS, ty * DTS), rng); if (digs.Remove(key, out var old)) Raylib.UnloadTexture(old.Tex); digs[key] = cur; }
        cur.P += 1f / Blows(def) + .0001f; cur.Idle = 0; var dir = P.MineAim; cur.Kick = dir; cur.WobV = 95; strikeFx = 1; strikeAt = face; strikeDir = dir; string mat = Blocks.Mat(b); string sn = def.Surf == "sand" ? "sand" : def.Surf; Snd.Play("dig_" + mat, .8f, .9f + cur.P * .18f + rng.F() * .08f); Snd.Play("step_" + sn, .3f, .72f + cur.P * .25f + rng.F() * .1f); Life.Debris(b, face - dir * 2, -dir, 3 + (int)(cur.P * 4), false); Shake(.35f + (def.Time > 1.5f ? .35f : 0));
        if (!digArmed) { queued = 0; follow = P.An.StrikeLen * (1 - AvatarAnim.Hit) * .8f; }
        if (mat is "stone" or "ore" or "crystal" or "ice" && def.Time >= 1.1f) Life.Sparks(face - dir * 3, -dir, mat == "ore" ? 5 : 2, mat == "ore" ? (Col)def.Glow : mat == "stone" ? (Col)0xffd890 : (Col)0xd8f0ff, mat == "ore" ? .42f : .26f);
        if (mat is "ore" or "crystal" or "ice") for (int i = 0; i < (mat == "ice" ? 1 : 2); i++) glints.Add((new Vector2(tx * DTS + rng.R(4, 20), ty * DTS + rng.R(4, 20)), -i * .08f, def.Glow != 0 ? (Col)def.Glow : (Col)0xe8f6ff));
        if (cur.P >= 1) Break(tx, ty, cur); else cur.Draw(def.Glow, Blocks.Glows(b));
    }
    public float DigProgressForTest(int x, int y) => digs.TryGetValue(y * World.W + x, out var g) ? g.P : W.At(x, y) == T.Air ? 1 : 0;
    readonly Painter atlas = new(DTS * ShardN, DTS);
    void Break(int x, int y, Dig g)
    {
        var def = Blocks.Of(g.Block); var o = new Vector2(x * DTS, y * DTS); var drop = new Loot { Id = (int)g.Block, P = o + new Vector2(12, 14), V = new(rng.R(-26, 26), -90), Born = .62f, T = 0, HopT = rng.R(1, 4), Fresh = true }; loot.Add(drop);
        var s = new Shatter { Into = drop, Cen = g.Cen }; Array.Clear(atlas.Px); for (int k = 0; k < DTS * DTS; k++) if (g.Src[k].A > 0) atlas.Px[(k / DTS) * atlas.W + g.Id[k] * DTS + k % DTS] = g.Src[k]; s.Tex = atlas.ToTexture(); for (int i = 0; i < ShardN; i++) { s.P[i] = o + g.Cen[i]; s.V[i] = g.Way[i] * rng.R(60, 125) - P.MineAim * rng.R(10, 46) + new Vector2(0, -rng.R(40, 90)); s.RV[i] = rng.R(-560, 560); } shatters.Add(s); if (shatters.Count > 14) { Raylib.UnloadTexture(shatters[0].Tex); shatters[0].Into.Born = 0; shatters.RemoveAt(0); }
        digs.Remove(y * World.W + x); Raylib.UnloadTexture(g.Tex); cur = null; Store.Data.Mined++; string mat = Blocks.Mat(g.Block); Life.Debris(g.Block, o + new Vector2(12, 12), -P.MineAim, 12, true); if (def.Glow != 0) Life.Sparkle(o + new Vector2(12, 12), 12, ((Col)def.Glow).Rl()); if (mat == "ore") Life.Sparks(o + new Vector2(12, 12), -P.MineAim, 9, def.Glow, .6f); else if (mat == "stone" && def.Time >= 1.1f) Life.Sparks(o + new Vector2(12, 12), -P.MineAim, 4, 0xffd890, .35f);
        Shake(1.1f); Snd.Play("break_" + mat, 1, .95f + rng.F() * .1f); Snd.Play("land", .5f, 1.25f); W.Set(x, y, T.Air);
    }
    void Place(int x, int y)
    {
        var d = Store.Data; ref var st = ref d.Slots[d.Hand]; T b = (T)st.Id; placeCd = .15f; W.Set(x, y, b); st.N--; d.Placed++; var c = new Vector2(x * DTS + 12, y * DTS + 12); pops.Add((x, y, 0, Vector2.Normalize(c - Chest + new Vector2(.01f, 0)))); Snd.Play("step_" + Blocks.Of(b).Surf, .9f, .85f + rng.F() * .1f); Snd.Play("pick", .4f, .8f); Life.Puff(c + new Vector2(0, 8), 6, ((Col)Blocks.Of(b).Color).Shade(.25f), 14); P.An.Punch(1.4f); if (MathF.Abs(c.X - P.P.X) > 10 && MathF.Abs(P.V.X) < 20) P.Dir = c.X > P.P.X ? 1 : -1; placeSwing = .2f;
        if (st.N <= 0) { int id = st.Id; st = default; int next = Array.FindIndex(d.Slots, s => s.Id == id && !s.Empty); d.Hand = next; barPop.V += 4; }
        foreach (var l in loot) if (l.P.X > x * DTS && l.P.X < (x + 1) * DTS && l.P.Y > y * DTS && l.P.Y <= (y + 1) * DTS) { l.P.Y = y * DTS - 1; l.V = new(rng.R(-30, 30), -80); }
    }
    float placeSwing;
    void Shards(float dt)
    {
        for (int i = shatters.Count - 1; i >= 0; i--)
        {
            var s = shatters[i]; float t0 = s.T; s.T += dt; if (s.T > .64f) { Raylib.UnloadTexture(s.Tex); shatters.RemoveAt(i); if (loot.Contains(s.Into)) { s.Into.Sq = -.5f; s.Into.SqV = 7; Life.Sparkle(s.Into.P + new Vector2(0, -6), 5, ((Col)Blocks.All[s.Into.Id].Color).Shade(.5f).Rl()); Snd.Play("pick", .35f, 1.5f); } continue; }
            for (int k = 0; k < ShardN; k++)
            {
                if (s.T < .36f) { s.V[k].Y += 520 * dt; s.V[k] *= MathF.Exp(-1.6f * dt); var n = s.P[k] + s.V[k] * dt; if (W.Solid((int)MathF.Floor(n.X / DTS), (int)MathF.Floor(n.Y / DTS))) { s.V[k] *= -.4f; s.RV[k] *= .5f; } else s.P[k] = n; s.R[k] += s.RV[k] * dt; s.From[k] = s.P[k]; }
                else { float u = M.Clamp((s.T - .36f - k * .012f) / .18f), e = u * u * (3 - 2 * u); var m = s.Into.P + new Vector2(0, -6); var d = m - s.From[k]; s.P[k] = s.From[k] + d * e + new Vector2(-d.Y, d.X) * M.Sin(u * M.PI) * .34f; s.R[k] += s.RV[k] * dt * 1.6f; }
            }
        }
    }
    void Looting(float dt)
    {
        var chest = P.P + new Vector2(0, -26);
        for (int i = loot.Count - 1; i >= 0; i--)
        {
            var l = loot[i]; l.T += dt; l.SqV += (-l.Sq * 420 - l.SqV * 14) * dt; l.Sq += l.SqV * dt; if (MathF.Abs(l.P.X - P.P.X) > 1500 || MathF.Abs(l.P.Y - P.P.Y) > 1100) continue;
            var dv = chest - l.P; float dist = dv.Length(); bool ready = l.T > l.Born + .12f; if (ready && dist < (l.Fresh ? 190 : 62) && Bag.Fits(Store.Data, l.Id)) { if (l.Pull <= 0) { l.Swirl = new(-MathF.Sign(dv.X == 0 ? 1 : dv.X) * 40, -110); Snd.Play("whoosh", .12f, 1.8f); } l.Pull += dt; } else if (l.Pull > 0 && dist > (l.Fresh ? 220 : 90)) l.Pull = 0; if (l.T > 6) l.Fresh = false;
            if (l.Pull > 0)
            {
                // a short hop upwards, then a curl into the hand, trailing sparks
                var to = P.HandPos(P.Dir) - l.P; float k = M.Clamp(l.Pull / .42f); l.V = l.Swirl * (1 - k) + (to.Length() > 1 ? Vector2.Normalize(to) : default) * (60 + 520 * k * k); l.P += l.V * dt; l.Ground = false; if (rng.P(dt * 30)) Life.Sparkle(l.P + new Vector2(0, -6), 1, ((Col)Blocks.All[l.Id].Color).Shade(.5f).Rl()); if (to.Length() < 12 || l.Pull > 1.2f) { Collect(l); loot.RemoveAt(i); } continue;
            }
            bool wet = W.PoolAt(l.P.X, l.P.Y - 4) != null; l.V.Y += (wet ? -60 : 640) * dt; if (wet) l.V *= MathF.Exp(-3 * dt); l.V.Y = MathF.Min(l.V.Y, 420); l.V.X = M.Damp(l.V.X, 0, l.Ground ? 9 : .8f, dt); float nx = l.P.X + l.V.X * dt; if (W.Solid((int)MathF.Floor((nx + MathF.Sign(l.V.X) * 6) / DTS), (int)MathF.Floor((l.P.Y - 5) / DTS))) l.V.X *= -.4f; else l.P.X = nx;
            float ny = l.P.Y + l.V.Y * dt; int gx = (int)MathF.Floor(l.P.X / DTS); l.Ground = false; if (l.V.Y >= 0 && W.Solid(gx, (int)MathF.Floor(ny / DTS))) { float fy = MathF.Floor(ny / DTS) * DTS; if (!W.Solid(gx, (int)MathF.Floor((fy - 2) / DTS))) ny = fy; else ny = W.GroundNear(l.P.X, l.P.Y); if (l.V.Y > 70) { l.SqV += l.V.Y * .035f; l.V.Y *= -.36f; l.V.X += rng.R(-8, 8); } else { l.V.Y = 0; l.Ground = true; } l.P.Y = ny; } else if (l.V.Y < 0 && W.Solid(gx, (int)MathF.Floor((ny - 11) / DTS))) l.V.Y = 0; else l.P.Y = ny;
            if (l.Ground) { l.HopT -= dt; if (l.HopT < 0) { l.HopT = rng.R(2.4f, 5); l.V = new(rng.R(-10, 10), -70); l.SqV = -3; } }
            for (int j = i - 1; j >= 0 && l.T > l.Born; j--) { var o = loot[j]; if (o.Id == l.Id && o.N + l.N <= Bag.MaxStack && o.T > o.Born && o.Pull <= 0 && Vector2.DistanceSquared(o.P, l.P) < 15 * 15) { o.N += l.N; o.SqV += 5; loot.RemoveAt(i); break; } }
        }
        placeSwing = MathF.Max(0, placeSwing - dt);
    }
    void Collect(Loot l)
    {
        var before = (ItemStack[])Store.Data.Slots.Clone(); int left = Bag.Add(Store.Data, l.Id, l.N), got = l.N - left; for (int i = 0; i < before.Length; i++) { var sNow = Store.Data.Slots[i]; if (sNow.Id == l.Id && sNow.N != (before[i].Id == l.Id ? before[i].N : 0)) slotPop[i] = (0, before[i].Id == l.Id ? before[i].N : 0); } if (got > 0) { Snd.Play("pick", .55f, 1.15f + rng.F() * .15f); Life.Sparkle(l.P, 4, ((Col)Blocks.All[l.Id].Color).Shade(.5f).Rl()); Gained(l.Id, got); P.An.Punch(.8f); } if (left > 0) { l.N = left; l.Pull = 0; loot.Add(l); }
    }
    void DrawDigs(int cx, int cy)
    {
        foreach (var g in digs.Values) { float x = g.X * DTS - cx - Pad + MathF.Round(g.Kick.X * g.Wob), y = g.Y * DTS - cy - Pad + MathF.Round(g.Kick.Y * g.Wob); if (x < -60 || y < -60 || x > Ui.VW + 60 || y > Ui.VH + 60 || g.Shown < 0) continue; if (MathF.Abs(g.Wob) > .7f) Raylib.DrawRectangle(g.X * DTS - cx + 2, g.Y * DTS - cy + 2, 20, 20, (((Col)Blocks.Of(g.Block).Color).Shade(-.86f) * .8f).Rl()); Ui.Tex(g.Tex, x, y, 1, Color.White); }
        foreach (var (x, y, t, from) in pops) { float k = t / .22f, s = Ease.OutBack(M.Clamp(k), 2.4f), off = (1 - M.Clamp(k * 1.6f)) * 12; var b = W.At(x, y); if (b == T.Air) continue; var q = new Vector2(x * DTS + 12 - cx, y * DTS + 12 - cy) - from * off; Items.Draw((int)b, q.X, q.Y, 24, .5f + s * .56f); }
    }
    void DrawLoot(int cx, int cy)
    {
        foreach (var l in loot) if (l.T > l.Born) { float x = MathF.Round(l.P.X - cx), y = MathF.Round(l.P.Y - cy); if (x < -20 || x > Ui.VW + 20 || y < -20 || y > Ui.VH + 20) continue; if (l.Ground) Raylib.DrawEllipse((int)x, (int)y, 6, 1.6f, new Color(30, 18, 34, 90)); float sx = 1 - l.Sq * .5f, sy = 1 + l.Sq * .5f; int n = l.N > 20 ? 3 : l.N > 1 ? 2 : 1; for (int k = n - 1; k >= 0; k--) { var t = Items.Tex(l.Id, 12); float w = 12 * sx, h = 12 * sy; Raylib.DrawTexturePro(t, new Rectangle(0, 0, 12, 12), new Rectangle(x - w / 2 + k * 3 - (n - 1) * 1.5f, y - h - k * 2, w, h), Vector2.Zero, 0, Color.White); } }
        foreach (var s in shatters) for (int k = 0; k < ShardN; k++) { var c = s.Cen[k]; float sc = s.T < .36f ? 1 : M.Lerp(1, .5f, M.Clamp((s.T - .36f) / .24f)); Raylib.DrawTexturePro(s.Tex, new Rectangle(k * DTS, 0, DTS, DTS), new Rectangle(MathF.Round(s.P[k].X - cx), MathF.Round(s.P[k].Y - cy), DTS * sc, DTS * sc), c * sc, s.R[k], Color.White); }
    }
    /// The block in your hand, carried low and swung up when placing.
    void DrawHeld(int cx, int cy) { var b = HandBlock; if (b == T.Air || P.Mining || P.Climb || P.An.State is Anim.Slide or Anim.Ledge) return; var h = P.HandPos(P.Dir); float s = M.Sin(M.Clamp(placeSwing / .2f) * M.PI); Items.Draw((int)b, MathF.Round(h.X - cx + P.Dir * s * 8), MathF.Round(h.Y - cy + 3 - s * 10), 12, 1 + s * .2f, P.Dir * (8 - s * 30)); }
    /// Drawn over everything: where you are aiming, the impact star, and a ghost of what would be placed.
    void DrawAim(int cx, int cy)
    {
        if (strikeFx > .5f && W.Solid((int)MathF.Floor((strikeAt.X + strikeDir.X * 4) / 24), (int)MathF.Floor((strikeAt.Y + strikeDir.Y * 4) / 24))) { int bx = (int)MathF.Floor((strikeAt.X + strikeDir.X * 4) / 24) * 24 - cx, by = (int)MathF.Floor((strikeAt.Y + strikeDir.Y * 4) / 24) * 24 - cy; Raylib.DrawRectangle(bx + 1, by + 1, 22, 22, new Color(255, 246, 220, (int)((strikeFx - .5f) * 150))); }
        if (strikeFx > 0) { float k = strikeFx; var c = strikeAt - new Vector2(cx, cy); for (int i = 0; i < 7; i++) { float a = i * M.TAU / 7 + .3f; var d = new Vector2(M.Cos(a), M.Sin(a)); float r0 = (1 - k) * 9 + 2, r1 = r0 + k * (i % 2 == 0 ? 8 : 5); Raylib.DrawLineEx(c + d * r0, c + d * r1, 1 + k, new Color(255, 244, 214, (int)(230 * k))); } }
        foreach (var (gp, gt, gc) in glints) { if (gt < 0) continue; float k = M.Sin(M.Clamp(gt / .45f) * M.PI), r = 1 + k * 4; var q = new Vector2(MathF.Round(gp.X - cx), MathF.Round(gp.Y - cy)); Ui.GlowAt(q.X, q.Y, 8 * k, gc.Rl(.5f * k)); var wc = new Color(255, 255, 255, (int)(240 * k)); Raylib.DrawLineV(q - new Vector2(r, 0), q + new Vector2(r + 1, 0), wc); Raylib.DrawLineV(q - new Vector2(0, r), q + new Vector2(0, r + 1), wc); Raylib.DrawRectangleV(q - new Vector2(1, 1), new(2, 2), wc); }
        if (cursorA < .04f || hideHud) return; float x = MathF.Round(cursorP.X - cx), y = MathF.Round(cursorP.Y - cy); bool place = placeOk && !aimOk; if (place) { float b = .55f + .12f * M.Sin(time * 5); Items.Draw((int)HandBlock, x + 12, y + 12, 24, 1, 0, b * cursorA); }
        float g = (P.Mining ? 1.5f + strikeFx * 2 : 1 + M.Sin(time * 4) * .8f); var col = new Color(255, 250, 226, (int)(220 * cursorA)); var dk = new Color(48, 26, 28, (int)(190 * cursorA)); void Corner(float px, float py, int sx, int sy) { Raylib.DrawRectangleV(new(px - (sx < 0 ? 6 : 1), py - (sy < 0 ? 2 : 1)), new(7, 3), dk); Raylib.DrawRectangleV(new(px - (sx < 0 ? 2 : 1), py - (sy < 0 ? 6 : 1)), new(3, 7), dk); Raylib.DrawRectangleV(new(px - (sx < 0 ? 5 : 0), py - (sy < 0 ? 1 : 0)), new(5, 1), col); Raylib.DrawRectangleV(new(px - (sx < 0 ? 1 : 0), py - (sy < 0 ? 5 : 0)), new(1, 5), col); }
        Corner(x - g, y - g, 1, 1); Corner(x + 24 + g, y - g, -1, 1); Corner(x - g, y + 24 + g, 1, -1); Corner(x + 24 + g, y + 24 + g, -1, -1);
    }
    /// Trees whose ground is dug away lean, crash and burst into leaves. Smaller plants just puff.
    readonly List<Prop> falling = new();
    void Uproot(Prop p)
    {
        if (p.Kind == 1) { p.Fall = 0; p.FallDir = p.X > P.P.X ? 1 : -1; falling.Add(p); Snd.Play("rustle", .8f, .7f); Snd.Play("climb", .7f, .6f); return; }
        Col c = p.Kind == 7 ? 0x8a8490 : World.GrassCol(p.X / 24); Life.Leaves(new(p.X, p.Y - (p.Kind == 7 ? -8 : 8)), p.Kind == 3 ? 12 : 5, c); Snd.Play("rustle", .4f, 1.1f);
    }
    void Falling(float dt)
    {
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            var p = falling[i]; p.Fall += dt; if (p.Fall < 1.15f) continue; falling.RemoveAt(i); float h = Sprites.Get(p.S).H; Col lc = p.S switch { var s when s.StartsWith("maple") => 0xd8743a, var s when s.StartsWith("cherry") || s.StartsWith("bloss") => 0xf2bcc8, var s when s.StartsWith("moon") => 0x58b0c0, var s when s.StartsWith("char") || s.StartsWith("cactus") || s.StartsWith("spire") => Blocks.Of(T.Ash).Color, _ => World.GrassCol(p.X / 24) }; for (int k = 0; k < 9; k++) { var q = new Vector2(p.X + p.FallDir * h * (.2f + k * .1f), p.Y - 10 + rng.R(-16, 4)); Life.Leaves(q, 4, lc); Life.Puff(q, 2, lc.Shade(.2f), 12); } Shake(2.2f); Snd.Play("land", .9f, .6f); Snd.Play("rustle", .9f, .8f);
        }
    }
    void ClearDig() { foreach (var g in digs.Values) Raylib.UnloadTexture(g.Tex); digs.Clear(); foreach (var s in shatters) Raylib.UnloadTexture(s.Tex); shatters.Clear(); loot.Clear(); pops.Clear(); falling.Clear(); cur = null; cursorA = 0; aimOk = placeOk = false; queued = 0; follow = 0; glints.Clear(); if (P != null) P.Mining = false; }
}

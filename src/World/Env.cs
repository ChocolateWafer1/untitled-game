using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public enum Weather { Clear, Breezy, Cloudy, Mist, Rain, Storm, Snow }

/// Time of day, weather fronts and the colours they produce.
public sealed class Env
{
    public float Clock = .3f; public int Day = 1; public const float DaySeconds = 1260;
    public Weather Now = Weather.Clear; public float WeatherT = 150, Cloud, Rain, Mist, Wind, Storm, WindBase, Flash, Rainbow, Wet, GustT; public float T; readonly Rng r = new(Environment.TickCount);
    public Col Top, Mid, Hor, Sun, Amb, Fog; public float Night, Golden; public Vector2 SunDir; public float SunElev; public Action<float> Thunder; float nextBolt = 6, thunderIn = -1; public float BoltX; public float BoltT = 9; public int BoltSeed; public float Snow, SnowCover, Eclipse, EclipseT, EclipseOff = 9, MeteorT, MeteorLevel, RainbowGoal, FlashHold, SkipTo = -1, skipRate; public bool Melting; public const float EclipseLen = 46;
    /// A single meteor: its head flies, its tail follows, and it finishes burning on its own.
    public sealed class Meteor { public Vector2 P, V; public float T, Burn, Trail, Width, Bright, Hue; public bool Flared; public Vector2 Head => P + V * MathF.Min(T, Burn); public Vector2 Tail => P + V * MathF.Max(0, MathF.Min(T, Burn) - Trail * (1 - M.Clamp((T - Burn) / Trail))); public bool Dead => T > Burn + Trail; public float Glow { get { float a = M.Smooth(0, .12f, T) * (T < Burn ? 1 - .35f * M.Smooth(Burn * .6f, Burn, T) : .65f * (1 - M.Clamp((T - Burn) / Trail))); return a * Bright; } } }
    public readonly List<Meteor> Meteors = new(); float meteorNext; readonly float radiant = -.06f;
    public void SpawnMeteor(bool shower, bool fireball = false)
    {
        if (Meteors.Count >= 16) return; fireball |= shower && r.P(.07f); float ang = (shower ? .48f : r.R(.25f, .7f)) + r.R(-.13f, .13f), sp = (fireball ? r.R(.34f, .5f) : r.R(.55f, 1.15f)); if (!shower && r.P(.5f)) ang = M.PI - ang;
        // a shower fans out from one point in the sky, so paths spread apart the further they start from it
        var p = new Vector2(r.R(-.12f, 1.0f), r.R(-.10f, .30f)); if (shower) ang += (p.X - radiant) * .10f - (p.Y) * .25f; if (!shower && MathF.Cos(ang) < 0) p.X = r.R(.2f, 1.1f);
        Meteors.Add(new Meteor { P = p, V = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * sp, Burn = fireball ? r.R(1.3f, 2.2f) : r.R(.45f, 1.15f), Trail = fireball ? r.R(.5f, .8f) : r.R(.16f, .34f), Width = fireball ? 1.9f : r.R(.6f, 1.15f), Bright = fireball ? 1.25f : r.R(.35f, 1), Hue = fireball ? r.R(.55f, 1) : r.F() * .5f });
    }
    public void FastForward(float to) { SkipTo = to; float d = to - Clock; if (d < 0) d += 1; skipRate = d / 3.2f; }
    public float WindForce = -1, CloudX, TimeScale = 1; public bool ForceAurora; public float ShootT = 9; public Vector2 ShootP; public float Aurora;
    struct Key { public float T; public Col Top, Mid, Hor, Sun, Amb, Fog; }
    static Key K(float t, int top, int mid, int hor, int sun, float si, int amb, float ai, int fog) => new() { T = t, Top = top, Mid = mid, Hor = hor, Sun = (Col)sun * si, Amb = (Col)amb * ai, Fog = fog };
    static readonly Key[] keys = [
        K(0, 0x0e1430, 0x1c2850, 0x35497a, 0x8fa8e8, .20f, 0x6f84c8, .46f, 0x2a3a68), K(.19f, 0x141a3c, 0x2c3468, 0x58588e, 0x8fa8e8, .18f, 0x7484c4, .48f, 0x3a4474), K(.235f, 0x34467e, 0x8a6c9a, 0xf0a088, 0xff9a6a, .5f, 0xa090b8, .58f, 0xc08a94),
        K(.275f, 0x5a86b8, 0xd8a8a0, 0xffd098, 0xffb868, .95f, 0xc8a8a0, .66f, 0xf0c09a), K(.36f, 0x5a9ad0, 0xa8cfe0, 0xffe8bc, 0xffd696, .88f, 0xc4c4c4, .72f, 0xe8dcc0), K(.50f, 0x4a90d2, 0x94cdec, 0xe4f0e0, 0xfff0cc, .80f, 0xc8d0d4, .78f, 0xcfe0e0), K(.62f, 0x5292c8, 0xb0cdd8, 0xfbe4b8, 0xffdca0, .88f, 0xc8c4bc, .74f, 0xe8d8b8),
        K(.70f, 0x5a7eb0, 0xe0b090, 0xffcc80, 0xffb45c, 1.02f, 0xc4a494, .68f, 0xf4bc88), K(.755f, 0x4c5a92, 0xd88478, 0xffa458, 0xff8a48, .98f, 0xb088a0, .60f, 0xe89468), K(.80f, 0x2c3468, 0x84588e, 0xe88070, 0xe07a88, .42f, 0x8a80b4, .54f, 0xa06a88), K(.85f, 0x161e46, 0x343c72, 0x6c5a8c, 0x8fa8e8, .2f, 0x7484c4, .48f, 0x4a4678), K(1, 0x0e1430, 0x1c2850, 0x35497a, 0x8fa8e8, .20f, 0x6f84c8, .46f, 0x2a3a68)];
    public string TimeText { get { int m = (int)(Clock * 24 * 60) / 10 * 10; int h = m / 60 % 24; return (h % 12 == 0 ? 12 : h % 12) + ":" + (m % 60).ToString("00") + (h < 12 ? " am" : " pm"); } }
    public When Period => Clock < .2f || Clock > .82f ? When.Night : Clock < .34f ? When.Dawn : Clock < .66f ? When.Day : When.Dusk;
    public Sky SkyKind => Storm > .5f ? Sky.Storm : Rain > .3f ? Sky.Rain : Mist > .4f ? Sky.Mist : Cloud < .55f ? Sky.Clear : Sky.Any;
    public string WeatherIcon => Storm > .5f ? "storm" : Rain > .3f ? "rain" : Mist > .4f ? "mist" : Cloud > .6f ? "cloud" : Night > .6f ? "moon" : "sun";
    public void SetWeather(Weather w, bool instant = false)
    {
        Now = w; WeatherT = r.R(170, 320) * (w == Weather.Clear ? 1.4f : 1); if (instant) { Cloud = TgtCloud; Rain = TgtRain; Mist = TgtMist; WindBase = TgtWind; Storm = w == Weather.Storm ? 1 : 0; }
    }
    float TgtCloud => Now switch { Weather.Clear => .22f, Weather.Breezy => .38f, Weather.Cloudy => .78f, Weather.Mist => .5f, Weather.Rain => .9f, Weather.Snow => .82f, _ => 1 }; float TgtRain => Now == Weather.Rain ? .65f : Now == Weather.Storm ? 1 : 0; float TgtMist => Now == Weather.Mist ? 1 : Now == Weather.Rain ? .25f : 0; float TgtWind => Now switch { Weather.Breezy => .55f, Weather.Storm => .8f, Weather.Rain => .42f, Weather.Mist => .12f, _ => .28f };
    public void Update(float dt)
    {
        T += dt; if (SkipTo >= 0) { float left = SkipTo - Clock; if (left < -.0001f) left += 1; float step = MathF.Min(left, dt * skipRate * M.Clamp(.25f + left * 14, .25f, 1.15f)); Clock += step; if (Clock >= 1) { Clock -= 1; Day++; } if (left - step < .0006f) SkipTo = -1; }
        float speed = Clock > .86f || Clock < .18f ? 1.9f : (Clock > .64f && Clock < .8f) || (Clock > .22f && Clock < .34f) ? .72f : 1; Clock += dt / DaySeconds * speed * TimeScale; if (Clock >= 1) { Clock -= 1; Day++; }
        WeatherT -= dt; if (WeatherT <= 0)
        {
            bool wasWet = Rain > .4f; float q = r.F(); bool morning = Clock > .18f && Clock < .32f; SetWeather(wasWet ? (q < .7f ? Weather.Clear : Weather.Cloudy) : morning && q < .4f ? Weather.Mist : q < .42f ? Weather.Clear : q < .62f ? Weather.Breezy : q < .76f ? Weather.Cloudy : q < .93f ? Weather.Rain : Weather.Storm); if (wasWet && Clock > .24f && Clock < .76f) RainbowGoal = 60;
        }
        Cloud = M.Approach(M.Damp(Cloud, TgtCloud, .012f, dt), TgtCloud, .0065f * dt); Rain = M.Damp(Rain, TgtRain * M.Smooth(.45f, .68f, Cloud), .16f, dt); Mist = M.Damp(Mist, TgtMist, .10f, dt); WindBase = M.Damp(WindBase, TgtWind, .05f, dt); Storm = M.Damp(Storm, Now == Weather.Storm ? M.Smooth(.55f, .8f, Cloud) : 0, .2f, dt); Wet = M.Damp(Wet, Rain > .2f ? 1 : 0, Rain > .2f ? .2f : .03f, dt); RainbowGoal = MathF.Max(0, RainbowGoal - dt); Rainbow = M.Approach(Rainbow, RainbowGoal > 0 ? 1 : 0, dt * .12f);
        float g = M.Noise(T * .035f, 3) * .28f + M.Noise(T * .09f, 9) * .10f; float want = WindForce >= 0 ? WindForce : WindBase * (1 + g); Wind = M.Damp(Wind, want, .35f, dt); CloudX += dt * (.0010f + Wind * .0016f); // always the same way, drifting slowly between calm and breezy
        Snow = M.Damp(Snow, Now == Weather.Snow ? M.Smooth(.45f, .65f, Cloud) : 0, .25f, dt); SnowCover = M.Damp(SnowCover, Snow > .3f ? 1 : 0, Snow > .3f ? .06f : .035f, dt); EclipseT = MathF.Max(0, EclipseT - dt); { float q = 1 - 2 * EclipseT / EclipseLen; EclipseOff = EclipseT > 0 ? MathF.Sign(q) * MathF.Pow(MathF.Abs(q), 1.25f) * .135f : 9; Eclipse = 1 - M.Smooth(.006f, .104f, MathF.Abs(EclipseOff)); } // the moon really crosses the sun, and the light follows how much is covered
        if (Melting) { SnowCover = M.Approach(SnowCover, 0, dt * .12f); if (SnowCover <= .001f) Melting = false; } MeteorT = MathF.Max(0, MeteorT - dt); MeteorLevel = M.Damp(MeteorLevel, MeteorT > 0 ? 1 : 0, .5f, dt); meteorNext -= dt * MeteorLevel; if (MeteorLevel > .04f && meteorNext <= 0 && Night > .3f) { meteorNext = -MathF.Log(MathF.Max(.02f, r.F())) * .34f; SpawnMeteor(true); if (r.P(.16f)) SpawnMeteor(true); }
        for (int i = Meteors.Count - 1; i >= 0; i--) { var m = Meteors[i]; m.T += dt; if (m.Width > 1.5f && !m.Flared && m.T > m.Burn * .86f) { m.Flared = true; FlashHold = .10f; } var h = m.Tail; if (m.Dead || h.X < -.4f || h.X > 1.5f || h.Y > .9f) Meteors.RemoveAt(i); }
        if (FlashHold > 0) { FlashHold -= dt; Flash = MathF.Max(Flash, .10f); }
        Colors();
        Flash = M.Damp(Flash, 0, 5, dt); BoltT += dt; if (Storm > .6f) { nextBolt -= dt; if (nextBolt <= 0) { nextBolt = r.R(5, 14); Flash = r.R(.7f, 1.1f); BoltX = r.F(); BoltSeed = r.I(9999); BoltT = 0; thunderIn = r.R(.25f, 1.8f); } }
        if (thunderIn > 0) { thunderIn -= dt; if (thunderIn <= 0) Thunder?.Invoke(1); }
        ShootT += dt; if (Night > .8f && Cloud < .5f && ShootT > 12 && r.P(dt * .08f)) { ShootT = 0; SpawnMeteor(false); }
        Aurora = M.Damp(Aurora, ForceAurora || Night > .8f && Cloud < .4f && (Day * 7 + 3) % 3 == 0 ? 1 : 0, .2f, dt);
    }
    public void Colors()
    {
        int i = 0; while (i < keys.Length - 2 && keys[i + 1].T < Clock) i++; var a = keys[i]; var b = keys[i + 1]; float t = M.Smooth(a.T, b.T, Clock); Top = Col.Lerp(a.Top, b.Top, t); Mid = Col.Lerp(a.Mid, b.Mid, t); Hor = Col.Lerp(a.Hor, b.Hor, t); Sun = Col.Lerp(a.Sun, b.Sun, t); Amb = Col.Lerp(a.Amb, b.Amb, t); Fog = Col.Lerp(a.Fog, b.Fog, t);
        Night = Clock < .5f ? 1 - M.Smooth(.2f, .27f, Clock) : M.Smooth(.775f, .85f, Clock); Golden = MathF.Max(M.Smooth(.235f, .27f, Clock) * (1 - M.Smooth(.33f, .40f, Clock)), M.Smooth(.6f, .68f, Clock) * (1 - M.Smooth(.77f, .80f, Clock)));
        // overcast softens and cools everything
        float oc = M.Smooth(.45f, 1, Cloud); float l = Hor.Lum; Col grey = Col.Lerp(new Col(.62f, .66f, .70f), new Col(.28f, .30f, .40f), Storm) * (.35f + l * .75f); Top = Col.Lerp(Top, grey * .72f, oc * .75f); Mid = Col.Lerp(Mid, grey * .9f, oc * .7f); Hor = Col.Lerp(Hor, grey * 1.08f, oc * .6f); Fog = Col.Lerp(Fog, grey, oc * .6f); Sun = Col.Lerp(Sun, Sun * .28f, oc); Amb = Col.Lerp(Amb, new Col(Amb.Lum, Amb.Lum, Amb.Lum * 1.1f) * (1 - Storm * .28f), oc * .6f);
        if (Eclipse > .01f) { float k = Eclipse * (1 - Night); Top = Col.Lerp(Top, 0x101430, k * .88f); Mid = Col.Lerp(Mid, 0x2a2448, k * .85f); Hor = Col.Lerp(Hor, 0x8a4a5a, k * .7f); Sun = Sun * (1 - k * .82f); Amb = Col.Lerp(Amb, new Col(.36f, .34f, .56f), k * .8f); Night = MathF.Max(Night, k * .8f); }
        if (Mist > .01f) { Fog = Col.Lerp(Fog, Hor, .4f * Mist); Sun = Sun * (1 - Mist * .35f); }
        Flash = M.Clamp(Flash, 0, 2); Amb = Amb + new Col(.7f, .75f, .95f) * (Flash * .7f);
        // sun by day, moon by night, both crossing left to right
        float dayT = (Clock - .24f) / (.79f - .24f); bool sunUp = dayT is > -.03f and < 1.03f; float s = sunUp ? dayT : Clock > .5f ? (Clock - .79f) / .45f : (Clock + .21f) / .45f; float ang = M.Lerp(M.PI * .97f, M.PI * .03f, M.Clamp(s)); SunElev = MathF.Sin(ang); SunDir = new(MathF.Cos(ang) * -1 * -1, -MathF.Sin(ang)); SunDir = new(MathF.Cos(ang), -MathF.Sin(ang) * .95f - .05f); SunT = s; SunUp = sunUp;
    }
    public float SunT; public bool SunUp;
    /// Screen-space position (0..1) of the sun or moon disc.
    public Vector2 SunScreen => new(.5f + SunDir.X * .46f, .86f + SunDir.Y * .74f);
}

/// Parallax backdrop: hand painted hills, mountains and drifting clouds.
public sealed class Backdrop
{
    readonly Texture2D[] hills = new Texture2D[4]; Shader sh; int uTint, uFog, uAmt, uLit; const int TW = 1024;
    public Backdrop(Shader hill)
    {
        sh = hill; uTint = Raylib.GetShaderLocation(sh, "tint"); uFog = Raylib.GetShaderLocation(sh, "fog"); uAmt = Raylib.GetShaderLocation(sh, "amt"); uLit = Raylib.GetShaderLocation(sh, "lit"); for (int i = 0; i < 4; i++) hills[i] = Hill(i).ToTexture(); for (int i = 0; i < 4; i++) Raylib.SetTextureWrap(hills[i], TextureWrap.Repeat); 
    }
    static float Per(float x, int seed, float amp, int f) { float r = 0; for (int i = 1; i <= 5; i++) { int k = f * i + (seed * i) % 3; r += M.Sin(x / TW * M.TAU * k + M.Hash(i, seed) * 6.28f) * amp / (i * 1.1f); } return r; }
    static Painter Hill(int layer)
    {
        var p = new Painter(TW + 96, 300); p.View(1, 48, 0); var r = new Rng(layer * 13 + 2); float[] baseY = [96, 112, 118, 128], amp = [34, 20, 16, 12]; Col[] c = [0x9aa6c4, 0x8fae9a, 0x86ab74, 0x7aa05a]; float H(float x) => baseY[layer] + (layer == 0 ? MathF.Abs(Per(x, 3, 30, 3)) * -1.5f + 26 + Per(x, 7, 5, 9) : Per(x, layer * 5 + 1, amp[layer], 1 + layer));
        var pts = new List<Vector2>(); for (int x = -60; x <= TW + 60; x += 4) pts.Add(new(x, H(x))); pts.Add(new(TW + 60, 340)); pts.Add(new(-60, 340)); Col cc = c[layer];
        p.Begin().Poly(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(pts), 1); p.Shade(new Ink((x, y) => { float d = y - H(x); var k = cc; if (layer == 0) { float nx = x + y * .55f, nz = x - y * .5f; float ridge = M.Sin(nx * M.TAU / TW * 21 + M.Sin(y * .07f) * 2) * .5f + M.Sin(nz * M.TAU / TW * 34 + M.Sin(y * .11f + 1) * 2) * .35f; k = k.Shade(ridge * .13f); float sl = 78 + M.Sin(x * M.TAU / TW * 46) * 4 + M.Sin(x * M.TAU / TW * 83 + 1) * 3 + ridge * 5; if (y < sl) k = Col.Lerp(0xf6f2f4, k, .12f).Shade(ridge * .10f); } else { float band = M.Sin((d + M.Sin(x / TW * M.TAU * (3 + layer)) * 12) * .12f); k = k.Shade(band * .05f + M.Noise(M.Mod((int)x, TW) * .03f, y * .06f, layer) * .13f); if (d < 5) k = k.Shade(.3f); else if (d < 9) k = k.Shade(.14f); } return k.Shade(-M.Clamp(d / 170) * .25f); }, 2, 0, .75f) { Ao = 0, Grad = 0, LinePx = layer == 0 ? 1 : 1.5f });
        if (layer > 0)
        {
            // fields, hedges, tiny trees and cottages; everything drawn twice near the seam so it tiles
            void Wrap(float x, Action<float> f) { f(x); if (x < 90) f(x + TW); if (x > TW - 90) f(x - TW); }
            int n = layer == 1 ? 60 : layer == 2 ? 46 : 34; for (int i = 0; i < n; i++)
            {
                float x = r.F() * TW, off = r.F() * r.F() * (layer == 1 ? 50 : 34), s = (layer == 1 ? 3.6f : layer == 2 ? 6.5f : 11) * r.R(.7f, 1.3f); bool pine = r.P(layer == 1 ? .5f : .25f); Col tc = cc.Shade(r.R(-.42f, -.16f)); int seed = r.I(999);
                Wrap(x, xx => { float y = H(xx) + off; p.Begin().Rect(xx - s * .12f, y - s, s * .24f, s * 1.2f); p.Flat(0x6a5348); if (pine) { p.Begin().Poly(new(xx - s * .7f, y), new(xx, y - s * 3), new(xx + s * .7f, y)); p.Shade(new Ink(tc, s * .5f, .5f, .6f) { Ao = 0 }); } else { p.Begin().Ellipse(xx, y - s * 1.5f, s * .95f, s).Ellipse(xx - s * .6f, y - s * 1.1f, s * .6f, s * .6f).Ellipse(xx + s * .6f, y - s * 1.15f, s * .65f, s * .6f); p.Shade(new Ink(tc, s * .6f, .6f, .6f) { Ao = 0 }); } });
            }
            if (layer >= 2) for (int i = 0; i < 3; i++) { float x = r.F() * TW; float s = layer == 2 ? 7 : 11; float off = r.R(10, 30); int rc = r.I(3); Wrap(x, xx => { float y = H(xx) + off; p.Begin().Rect(xx - s, y - s * 1.2f, s * 2, s * 1.2f); p.Shade(new Ink(0xf0e2c4, 2, .4f, .7f) { Ao = 0 }); p.Begin().Poly(new(xx - s * 1.3f, y - s * 1.1f), new(xx - s * .7f, y - s * 2.1f), new(xx + s * .7f, y - s * 2.1f), new(xx + s * 1.3f, y - s * 1.1f)); p.Shade(new Ink(rc == 0 ? 0xb85a44 : rc == 1 ? 0x8a6a7a : 0xa8704a, 2, .5f, .7f) { Ao = 0 }); p.Begin().Rect(xx - s * .6f, y - s * .8f, s * .45f, s * .45f).Rect(xx + s * .2f, y - s * .8f, s * .45f, s * .45f); p.Flat(0xffe0a0); }); }
            for (int i = 0; i < 8; i++) { float x = r.F() * TW, off = r.R(26, 70); Wrap(x, xx => { for (float q = 0; q < 40; q += 3) { float yy = H(xx + q) + off; p.Begin().Ellipse(xx + q, yy, 3.4f, 2.4f); p.Shade(new Ink(cc.Shade(-.3f), 2, .5f, .4f) { Ao = 0 }); } }); }
        }
        // crop the margin so the edge of the canvas never becomes a drawn outline
        var o = new Painter(TW, 300); for (int y = 0; y < 300; y++) Array.Copy(p.Px, y * p.W + 48, o.Px, y * TW, TW); return o;
    }
    public void Draw(Env e, float camX, float camY, int w, int h, float groundY, float tx)
    {
        Vector3 V(Col c) => new(c.R, c.G, c.B); float und = M.Clamp((camY + h * .5f - groundY - 60) / 400);
        Raylib.BeginShaderMode(sh);
        float[] px = [.04f, .09f, .17f, .3f], py = [.02f, .045f, .08f, .14f], by = [.36f, .44f, .54f, .64f], fog = [.56f, .36f, .19f, .06f]; Col g = World.GrassCol(tx); float gl = MathF.Max(.2f, g.Lum); Col biome = new(g.R / gl * .56f + .42f, g.G / gl * .56f + .42f, g.B / gl * .56f + .42f);
        Col light = e.Amb * .82f + e.Sun * (.28f + .10f * e.Golden);
        for (int i = 0; i < 4; i++)
        {
            Col tint = light * (i == 0 ? new Col(1, 1, 1) : Col.Lerp(new Col(1, 1, 1), biome, .35f + i * .2f)); Raylib.SetShaderValue(sh, uTint, V(tint), ShaderUniformDataType.Vec3); Raylib.SetShaderValue(sh, uFog, V(Col.Lerp(e.Hor, e.Fog, .5f)), ShaderUniformDataType.Vec3); float am = M.Clamp(fog[i] + e.Mist * .35f + e.Rain * .16f); Raylib.SetShaderValue(sh, uAmt, am, ShaderUniformDataType.Float); Raylib.SetShaderValue(sh, uLit, e.Night, ShaderUniformDataType.Float);
            float oy = h * by[i] - (camY + h * .5f - 88 * 24) * py[i] - 60; float ox = M.Mod((int)(camX * px[i] + i * 211), TW); Rlgl.SetTexture(0); Raylib.DrawTexturePro(hills[i], new Rectangle(ox, 0, w, 300), new Rectangle(0, (int)oy, w, 300), Vector2.Zero, 0, Color.White); if (oy + 299 < h) Raylib.DrawTexturePro(hills[i], new Rectangle(ox, 298, w, 1), new Rectangle(0, (int)oy + 299, w, h - oy), Vector2.Zero, 0, Color.White); Rlgl.DrawRenderBatchActive();
        }
        Raylib.EndShaderMode(); if (und > 0) Raylib.DrawRectangle(0, 0, w, h, new Color(26, 20, 34, (int)(und * 255)));
    }
}

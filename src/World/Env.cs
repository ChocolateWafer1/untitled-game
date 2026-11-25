using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

public enum Weather { Clear, Breezy, Cloudy, Mist, Rain, Storm, Snow }

/// Time of day, weather fronts and the colours they produce.
public sealed class Env
{
    /// What the biome around the player does to the sky.
    public Col LTop = new(1, 1, 1), LHor = new(1, 1, 1), LSun = new(1, 1, 1), LAmb = new(1, 1, 1); public float LMist, Cold, Dry; float rainW, mistW, snowW;
    public float Clock = .3f; public int Day = 1; public const float DaySeconds = 1260;
    public Weather Now = Weather.Clear; public float WeatherT = 150, Cloud, Rain, Mist, Wind, Storm, WindBase, Flash, Rainbow, Wet, GustT; public float T; readonly Rng r = new(Environment.TickCount);
    public Col Top, Mid, Hor, Sun, Amb, Fog; public float Night, Golden; public Vector2 SunDir; public float SunElev; public Action<float> Thunder; float nextBolt = 6, thunderIn = -1; public float BoltX; public float BoltT = 9; public int BoltSeed; public float SwayT, GrassT; public float Snow, SnowCover, Eclipse, EclipseT, EclipseOff = 9, MeteorT, MeteorLevel, RainbowGoal, FlashHold, SkipTo = -1, skipRate; public bool Melting; public const float EclipseLen = 46;
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
    public string WeatherIcon => Storm > .5f ? "storm" : Rain > .3f ? "rain" : Mist > .4f ? "mist" : Cloud > .6f ? "cloud" : Night > .6f ? "moon" : "sun";
    public void SetWeather(Weather w, bool instant = false)
    {
        Now = w; WeatherT = r.R(170, 320) * (w == Weather.Clear ? 1.4f : 1); if (instant) { Cloud = TgtCloud; Rain = rainW = TgtRain; Mist = mistW = TgtMist; WindBase = TgtWind; Storm = w == Weather.Storm ? 1 : 0; }
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
        Cloud = M.Approach(M.Damp(Cloud, TgtCloud, .012f, dt), TgtCloud, .0065f * dt); rainW = M.Damp(rainW, TgtRain * M.Smooth(.45f, .68f, Cloud), .16f, dt); mistW = M.Damp(mistW, TgtMist, .10f, dt); Rain = rainW * (1 - Cold) * (1 - Dry * .9f); Mist = MathF.Max(mistW * (1 - Dry * .6f), LMist * (.7f + .3f * Golden)); WindBase = M.Damp(WindBase, TgtWind, .05f, dt); Storm = M.Damp(Storm, Now == Weather.Storm ? M.Smooth(.55f, .8f, Cloud) : 0, .2f, dt); Wet = M.Damp(Wet, Rain > .2f ? 1 : 0, Rain > .2f ? .2f : .03f, dt); RainbowGoal = MathF.Max(0, RainbowGoal - dt); Rainbow = M.Approach(Rainbow, RainbowGoal > 0 ? 1 : 0, dt * .12f);
        float g = M.Noise(T * .035f, 3) * .28f + M.Noise(T * .09f, 9) * .10f; float want = WindForce >= 0 ? WindForce : WindBase * (1 + g); Wind = M.Damp(Wind, want, .35f, dt); SwayT += dt * (1.1f + Wind * .7f); GrassT += dt * (1.6f + Wind); // motion is accumulated, so a change in wind changes speed and never position
        CloudX += dt * (.0010f + Wind * .0016f); // always the same way, drifting slowly between calm and breezy
        snowW = M.Damp(snowW, Now == Weather.Snow ? M.Smooth(.45f, .65f, Cloud) : 0, .25f, dt); Snow = MathF.Max(snowW, rainW * Cold) * (1 - Dry); SnowCover = M.Damp(SnowCover, Snow > .3f ? 1 : 0, Snow > .3f ? .06f : .035f, dt); EclipseT = MathF.Max(0, EclipseT - dt); { float q = 1 - 2 * EclipseT / EclipseLen; EclipseOff = EclipseT > 0 ? MathF.Sign(q) * MathF.Pow(MathF.Abs(q), 1.25f) * .135f : 9; Eclipse = 1 - M.Smooth(.006f, .104f, MathF.Abs(EclipseOff)); } // the moon really crosses the sun, and the light follows how much is covered
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
        float lk = 1 - .35f * Night; Top = Top * Col.Lerp(new Col(1, 1, 1), LTop, lk); Mid = Mid * Col.Lerp(new Col(1, 1, 1), Col.Lerp(LTop, LHor, .55f), lk); Hor = Hor * Col.Lerp(new Col(1, 1, 1), LHor, lk); Fog = Fog * Col.Lerp(new Col(1, 1, 1), LHor, lk); Sun = Sun * LSun; Amb = Amb * LAmb;
        if (Mist > .01f) { Fog = Col.Lerp(Fog, Hor, .4f * Mist); Sun = Sun * (1 - Mist * .35f); }
        Flash = M.Clamp(Flash, 0, 2); Amb = Amb + new Col(.7f, .75f, .95f) * (Flash * .7f);
        // sun by day, moon by night, both crossing left to right
        float dayT = (Clock - .24f) / (.79f - .24f); bool sunUp = dayT is > -.03f and < 1.03f; float s = sunUp ? dayT : Clock > .5f ? (Clock - .79f) / .45f : (Clock + .21f) / .45f; float ang = M.Lerp(M.PI * .97f, M.PI * .03f, M.Clamp(s)); SunElev = MathF.Sin(ang); SunDir = new(MathF.Cos(ang) * -1 * -1, -MathF.Sin(ang)); SunDir = new(MathF.Cos(ang), -MathF.Sin(ang) * .95f - .05f); SunT = s; SunUp = sunUp;
    }
    public float SunT; public bool SunUp;
    /// Screen-space position (0..1) of the sun or moon disc.
    public Vector2 SunScreen => new(.5f + SunDir.X * .46f, .86f + SunDir.Y * .74f);
}

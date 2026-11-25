using System.Numerics;
namespace UnnamedGame;

public static class M
{
    public const float PI = MathF.PI, TAU = MathF.PI * 2;
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float Clamp(float v, float a = 0, float b = 1) => v < a ? a : v > b ? b : v;
    public static float Smooth(float a, float b, float v) { float t = Clamp((v - a) / (b - a)); return t * t * (3 - 2 * t); }
    public static float Remap(float v, float a, float b, float c, float d) => c + (d - c) * Clamp((v - a) / (b - a));
    /// Frame-rate independent smoothing.
    public static float Damp(float a, float b, float lambda, float dt) => Lerp(a, b, 1 - MathF.Exp(-lambda * dt));
    public static Vector2 Damp(Vector2 a, Vector2 b, float l, float dt) => Vector2.Lerp(a, b, 1 - MathF.Exp(-l * dt));
    public static Vector2 Catmull(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) => .5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t * t * t);
    public static float Sin(float x) => MathF.Sin(x);
    public static float Cos(float x) => MathF.Cos(x);
    public static Vector2 V(float x, float y) => new(x, y);
    public static Vector2 Dir(float ang, float len) => new(MathF.Sin(ang) * len, MathF.Cos(ang) * len); // 0 = down
    public static float Hash(int x, int y = 0, int s = 0)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + s * 974711);
        h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
    static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    public static float Noise(float x, float y = 0, int s = 0)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float u = Fade(x - xi), v = Fade(y - yi);
        return Lerp(Lerp(Hash(xi, yi, s), Hash(xi + 1, yi, s), u), Lerp(Hash(xi, yi + 1, s), Hash(xi + 1, yi + 1, s), u), v) * 2 - 1;
    }
    public static float Fbm(float x, float y, int oct, int s = 0, float gain = .5f)
    {
        float a = 1, r = 0, n = 0;
        for (int i = 0; i < oct; i++) { r += Noise(x, y, s + i * 31) * a; n += a; a *= gain; x *= 2.03f; y *= 2.03f; }
        return r / n;
    }
    public static int Mod(int a, int b) => ((a % b) + b) % b;
    public static float Approach(float a, float b, float d) => a < b ? MathF.Min(a + d, b) : MathF.Max(a - d, b);
}

public static class Ease
{
    public static float OutBack(float t, float s = 1.70158f) { t -= 1; return t * t * ((s + 1) * t + s) + 1; }
    public static float OutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
    public static float InCubic(float t) => t * t * t;
    public static float InOut(float t) => t < .5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
    public static float OutElastic(float t) => t <= 0 ? 0 : t >= 1 ? 1 : MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - .75f) * M.TAU / 3) + 1;
}

/// Damped spring; the backbone of nearly every UI / cloth wobble.
public struct Spring
{
    public float X, V;
    public Spring(float x) { X = x; V = 0; }
    public void Update(float target, float stiff, float damp, float dt)
    {
        dt = MathF.Min(dt, 1 / 20f);
        for (int i = 0; i < 4; i++) { float h = dt / 4; V += ((target - X) * stiff - V * damp) * h; X += V * h; }
    }
}

public sealed class Rng
{
    ulong s;
    public Rng(int seed) { s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567; Next(); Next(); }
    public uint Next() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return (uint)(s >> 32); }
    public float F() => Next() / 4294967296f;
    public float R(float a, float b) => a + (b - a) * F();
    public int I(int n) => n <= 0 ? 0 : (int)(Next() % (uint)n);
    public int I(int a, int b) => a + I(b - a);
    public bool P(float p) => F() < p;
    public T Pick<T>(IReadOnlyList<T> l) => l[I(l.Count)];
}

public struct Col
{
    public float R, G, B;
    public Col(float r, float g, float b) { R = r; G = g; B = b; }
    public Col(int hex) { R = ((hex >> 16) & 255) / 255f; G = ((hex >> 8) & 255) / 255f; B = (hex & 255) / 255f; }
    public static implicit operator Col(int hex) => new(hex);
    public int Hex => ((int)(M.Clamp(R) * 255 + .5f) << 16) | ((int)(M.Clamp(G) * 255 + .5f) << 8) | (int)(M.Clamp(B) * 255 + .5f);
    public static Col Lerp(Col a, Col b, float t) => new(M.Lerp(a.R, b.R, t), M.Lerp(a.G, b.G, t), M.Lerp(a.B, b.B, t));
    public static Col operator *(Col a, float f) => new(a.R * f, a.G * f, a.B * f);
    public static Col operator *(Col a, Col b) => new(a.R * b.R, a.G * b.G, a.B * b.B);
    public static Col operator +(Col a, Col b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public float Lum => R * .3f + G * .59f + B * .11f;
    /// Hue shifting ramp: shadows drift toward plum, lights toward warm cream.
    public Col Shade(float t)
    {
        if (t < 0)
        {
            t = MathF.Min(-t, 1);
            var d = new Col(R * .34f + .02f, G * .26f + .01f, B * .38f + .045f);
            return Lerp(this, d, t);
        }
        t = MathF.Min(t, 1);
        var l = new Col(R + (1 - R) * .55f, G + (.97f - G) * .5f, B + (.84f - B) * .40f);
        return Lerp(this, l, t);
    }
    public Col Ink() { var d = Shade(-.82f); return d * .82f; }
    public static Col Hsv(float h, float s, float v)
    {
        h = (h % 1 + 1) % 1 * 6; int i = (int)h; float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        return i switch { 0 => new(v, t, p), 1 => new(q, v, p), 2 => new(p, v, t), 3 => new(p, q, v), 4 => new(t, p, v), _ => new(v, p, q) };
    }
    public (float h, float s, float v) ToHsv()
    {
        float mx = MathF.Max(R, MathF.Max(G, B)), mn = MathF.Min(R, MathF.Min(G, B)), d = mx - mn, h = 0;
        if (d > 0) { if (mx == R) h = (G - B) / d / 6; else if (mx == G) h = ((B - R) / d + 2) / 6; else h = ((R - G) / d + 4) / 6; }
        return ((h + 1) % 1, mx <= 0 ? 0 : d / mx, mx);
    }
    public Raylib_cs.Color Rl(float a = 1) => new((byte)(M.Clamp(R) * 255), (byte)(M.Clamp(G) * 255), (byte)(M.Clamp(B) * 255), (byte)(M.Clamp(a) * 255));
}

using Raylib_cs;
namespace UnnamedGame;

/// Sound effects, ambience loops and crossfading music. Files live in assets/audio; names group variants ("step_grass1..5").
public static class Snd
{
    static readonly Dictionary<string, List<Sound>> bank = new(); static readonly Rng rng = new(99); static bool ok;
    sealed class LoopS { public Music M; public float Vol, Target; public bool Playing; }
    static readonly Dictionary<string, LoopS> loops = new(); static readonly Dictionary<string, LoopS> music = new(); static string cur; static float musicGap;
    public static string Root; static readonly Dictionary<string, float> last = new();
    static readonly HashSet<string> uiNames = ["hover", "click", "pick", "tick", "type", "error", "open", "close", "page", "whoosh", "confirm", "tab", "dice", "undo", "iris_in", "iris_out", "finish"];
    public static float Duck = 1;

    public static void Load(bool silent)
    {
        Root = Path.Combine(AppContext.BaseDirectory, "assets", "audio");
        if (silent) return;
        Raylib.InitAudioDevice(); if (!Raylib.IsAudioDeviceReady()) return; ok = true; Raylib.SetAudioStreamBufferSizeDefault(8192);
        Dictionary<string, List<string>> f = new();
        void Scan(string dir, Action<string, string> add) { var d = Path.Combine(Root, dir); if (Directory.Exists(d)) foreach (var p in Directory.GetFiles(d)) { var e = Path.GetExtension(p).ToLower(); if (e is ".ogg" or ".wav" or ".mp3") add(Path.GetFileNameWithoutExtension(p), p); } }
        Scan("sfx", (n, p) => { var k = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'); if (!bank.TryGetValue(k, out var l)) bank[k] = l = new(); var s = Raylib.LoadSound(p); if (Raylib.IsSoundValid(s)) { l.Add(s); l.Add(Raylib.LoadSoundAlias(s)); } });
        Scan("loop", (n, p) => { var m = Raylib.LoadMusicStream(p); if (Raylib.IsMusicValid(m)) { m.Looping = true; loops[n] = new LoopS { M = m }; } });
        Scan("music", (n, p) => { var m = Raylib.LoadMusicStream(p); if (Raylib.IsMusicValid(m)) { m.Looping = false; music[n] = new LoopS { M = m }; } });
        Synth.FillMissing(bank);
    }
    public static bool Has(string n) => bank.ContainsKey(n);
    public static void Play(string n, float vol = 1, float pitch = 1, float pan = .5f)
    {
        if (!ok || !bank.TryGetValue(n, out var l) || l.Count == 0) return;
        float now = (float)Raylib.GetTime(); if (last.TryGetValue(n, out float t) && now - t < .025f) return; last[n] = now;
        int i = rng.I(l.Count); for (int k = 0; k < l.Count && Raylib.IsSoundPlaying(l[i]); k++) i = (i + 1) % l.Count; var s = l[i];
        Raylib.SetSoundVolume(s, vol * Store.Cfg.Master * (uiNames.Contains(n) ? Store.Cfg.Ui : Store.Cfg.Sfx)); Raylib.SetSoundPitch(s, M.Clamp(pitch, .3f, 3)); Raylib.SetSoundPan(s, M.Clamp(1 - pan, .1f, .9f)); Raylib.PlaySound(s);
    }
    public static void At(string n, float dx, float dy, float vol = 1, float pitch = 1)
    {
        float d = MathF.Sqrt(dx * dx + dy * dy), v = M.Clamp(1 - d / 420); if (v > .02f) Play(n, vol * v * v, pitch, M.Clamp(.5f + dx / 700, .1f, .9f));
    }
    /// Request an ambience loop at a volume this frame; anything not requested fades out.
    public static void Loop(string n, float vol) { if (loops.TryGetValue(n, out var l)) l.Target = MathF.Max(l.Target, vol); }
    public static void Music(string n) { if (cur != n) { cur = n; musicGap = .5f; } }
    public static bool HasMusic(string n) => music.ContainsKey(n);
    public static void Update(float dt)
    {
        if (!ok) return;
        foreach (var l in loops.Values)
        {
            l.Vol = M.Damp(l.Vol, l.Target, 1.8f, dt); l.Target = 0;
            if (l.Vol > .004f) { if (!l.Playing) { Raylib.PlayMusicStream(l.M); l.Playing = true; } Raylib.SetMusicVolume(l.M, l.Vol * Store.Cfg.Master * Store.Cfg.Ambience); Raylib.UpdateMusicStream(l.M); }
            else if (l.Playing) { Raylib.PauseMusicStream(l.M); l.Playing = false; }
        }
        musicGap -= dt;
        foreach (var (n, l) in music)
        {
            bool on = n == cur && musicGap <= 0; l.Vol = M.Damp(l.Vol, on ? 1 : 0, on ? .9f : 1.6f, dt);
            if (on && !Raylib.IsMusicStreamPlaying(l.M)) { if (l.Playing) { l.Playing = false; musicGap = 14; continue; } Raylib.PlayMusicStream(l.M); l.Playing = true; }
            if (l.Vol > .004f && l.Playing) { Raylib.SetMusicVolume(l.M, l.Vol * Duck * Store.Cfg.Master * Store.Cfg.Music); Raylib.UpdateMusicStream(l.M); }
            else if (l.Playing && !on) { Raylib.StopMusicStream(l.M); l.Playing = false; }
        }
    }
    public static void Close() { if (ok) Raylib.CloseAudioDevice(); }
}

/// Small synthesiser (original, released CC0) for goofy voices and anything without a recorded file.
static class Synth
{
    const int SR = 44100;
    static unsafe Sound Make(float len, Func<float, float> f)
    {
        int n = (int)(len * SR); var d = new short[n]; for (int i = 0; i < n; i++) { float t = i / (float)SR; d[i] = (short)(M.Clamp(f(t), -1, 1) * 30000); }
        fixed (short* p = d) { var w = new Wave { SampleCount = (uint)n, SampleRate = SR, SampleSize = 16, Channels = 1, Data = p }; return Raylib.LoadSoundFromWave(w); }
    }
    static float Env(float t, float a, float d) => t < a ? t / a : MathF.Exp(-(t - a) / d);
    static float Nz(float t, int s = 0) => M.Hash((int)(t * SR), s) * 2 - 1;
    public static void FillMissing(Dictionary<string, List<Sound>> b)
    {
        void S(string n, params Sound[] s) { if (!b.ContainsKey(n)) b[n] = new(s); }
        float Sn(float f, float t) => MathF.Sin(t * f * M.TAU);
        float Pluck(float f, float t) => (Sn(f, t) + .4f * Sn(f * 2, t) + .18f * Sn(f * 3.01f, t)) * Env(t, .003f, .16f) * .5f;
        S("hover", Make(.06f, t => Sn(680, t) * Env(t, .004f, .014f) * .25f)); S("tick", Make(.04f, t => Sn(900, t) * Env(t, .002f, .01f) * .3f)); S("click", Make(.12f, t => Pluck(420, t))); S("pick", Make(.16f, t => Pluck(520 + t * 900, t))); S("type", Make(.06f, t => (Sn(740, t) + Nz(t) * .3f) * Env(t, .002f, .012f) * .3f));
        S("error", Make(.22f, t => (Sn(196, t) + Sn(185, t)) * Env(t, .005f, .09f) * .3f)); S("open", Make(.3f, t => Pluck(330 + (t > .07f ? 165 : 0) + (t > .14f ? 165 : 0), t % .07f + (t > .14f ? 0 : 0)) * .8f)); S("close", Make(.26f, t => Pluck(t < .08f ? 495 : 330, t < .08f ? t : t - .08f) * .7f));
        S("whoosh", Make(.35f, t => Nz(t) * MathF.Sin(t / .35f * M.PI) * .16f)); S("confirm", Make(.5f, t => Pluck(t < .1f ? 523 : t < .2f ? 659 : 784, t < .1f ? t : t < .2f ? t - .1f : t - .2f))); S("finish", Make(1.2f, t => { float[] n = [523, 659, 784, 1047, 1319]; int i = Math.Min(4, (int)(t / .11f)); return Pluck(n[i], t - i * .11f) + Pluck(n[Math.Max(0, i - 1)], t - MathF.Max(0, i - 1) * .11f) * .4f; }));
        S("tab", Make(.1f, t => Pluck(360, t) * .8f)); S("dice", Make(.5f, t => Nz(t) * MathF.Pow(MathF.Max(0, MathF.Sin(t * 60)), 8) * (1 - t * 2) * .4f)); S("undo", Make(.18f, t => Pluck(520 - t * 1200, t))); S("page", Make(.22f, t => Nz(t) * MathF.Sin(MathF.Pow(t / .22f, .5f) * M.PI) * .14f));
        S("iris_in", Make(.7f, t => (Sn(520 - t * 520, t) * .3f + Nz(t) * .04f) * (1 - t / .7f))); S("iris_out", Make(.7f, t => (Sn(180 + t * 520, t) * .3f + Nz(t) * .04f) * (1 - t / .7f)));
        S("poof", Make(.3f, t => Nz(t) * Env(t, .01f, .07f) * .3f)); S("cloth", Make(.16f, t => Nz(t) * MathF.Sin(t / .16f * M.PI) * (.6f + .4f * MathF.Sin(t * 180)) * .14f));
        S("jump", Make(.2f, t => (Nz(t) * .12f + Sn(220 + t * 500, t) * .12f) * Env(t, .01f, .06f))); S("land", Make(.22f, t => (Sn(90 - t * 120, t) * .6f + Nz(t) * .2f) * Env(t, .003f, .05f)));
        foreach (var (n, f, ns) in new[] { ("step_grass", 140, .28f), ("step_dirt", 110, .3f), ("step_stone", 240, .22f), ("step_wood", 190, .18f), ("step_sand", 90, .24f), ("step_snow", 120, .3f), ("step_leaves", 200, .34f) }) S(n, Make(.1f, t => (Nz(t) * ns + Sn(f, t) * .25f) * Env(t, .004f, .022f)), Make(.1f, t => (Nz(t, 3) * ns + Sn(f * 1.12f, t) * .25f) * Env(t, .004f, .02f)));
        S("splash", Make(.6f, t => Nz(t) * Env(t, .01f, .16f) * .4f)); S("splash_small", Make(.25f, t => (Nz(t) * .2f + Sn(700 - t * 1400, t) * .2f) * Env(t, .004f, .06f))); S("plop", Make(.2f, t => Sn(320 + t * 1400, t) * Env(t, .003f, .05f) * .5f)); S("stroke", Make(.4f, t => Nz(t) * MathF.Sin(t / .4f * M.PI) * .12f)); S("bubble", Make(.14f, t => Sn(400 + t * 2400, t) * Env(t, .01f, .04f) * .3f));
        S("cast", Make(.4f, t => Nz(t) * MathF.Pow(MathF.Sin(t / .4f * M.PI), 3) * .3f)); S("reel", Make(.05f, t => (Nz(t) * .5f + Sn(1400, t) * .3f) * Env(t, .001f, .006f) * .5f)); S("bite", Make(.3f, t => Pluck(t < .1f ? 880 : 1175, t < .1f ? t : t - .1f))); S("snap", Make(.2f, t => (Nz(t) * .6f + Sn(1800 - t * 6000, t) * .3f) * Env(t, .001f, .03f))); S("tension", Make(.12f, t => (Sn(260, t) + Sn(392, t) * .4f) * MathF.Sin(t / .12f * M.PI) * .16f)); S("charge", Make(.06f, t => Sn(500, t) * Env(t, .003f, .02f) * .2f));
        S("catch", Make(1.1f, t => { float[] n = [587, 740, 880, 1175]; int i = Math.Min(3, (int)(t / .12f)); return Pluck(n[i], t - i * .12f) * 1.1f; })); S("legend", Make(2.2f, t => { float[] n = [392, 494, 587, 784, 988, 1175]; int i = Math.Min(5, (int)(t / .16f)); float u = t - i * .16f; return (Pluck(n[i], u) + Sn(n[0] / 2, t) * .1f * Env(t, .2f, .8f)); })); S("coin", Make(.3f, t => Pluck(t < .06f ? 988 : 1319, t < .06f ? t : t - .06f) * .8f)); S("coins", Make(.7f, t => { int i = (int)(t / .07f); return Pluck(988 + (i % 4) * 130, t - i * .07f) * .6f; }));
        S("forage", Make(.22f, t => (Nz(t) * .14f + Sn(600 + t * 900, t) * .2f) * Env(t, .01f, .07f))); S("rustle", Make(.3f, t => Nz(t) * MathF.Sin(t / .3f * M.PI) * (.5f + .5f * MathF.Sin(t * 120)) * .12f)); S("leaf", Make(.2f, t => Nz(t) * Env(t, .05f, .05f) * .08f)); S("area", Make(1.4f, t => (Pluck(392, t) + (t > .16f ? Pluck(587, t - .16f) : 0) + (t > .32f ? Pluck(784, t - .32f) : 0)) * .7f));
        S("bird", Make(.3f, t => Sn(2200 + MathF.Sin(t * 70) * 500, t) * MathF.Sin(t / .3f * M.PI) * .1f), Make(.24f, t => Sn(2800 - t * 2600, t) * MathF.Sin(t / .24f * M.PI) * .1f)); S("flap", Make(.4f, t => Nz(t) * MathF.Pow(MathF.Max(0, MathF.Sin(t * 70)), 3) * .16f)); S("frog", Make(.3f, t => Sn(180, t) * MathF.Pow(MathF.Max(0, MathF.Sin(t * 130)), 2) * MathF.Sin(t / .3f * M.PI) * .3f)); S("duck", Make(.22f, t => (Sn(330 - t * 300, t) + Sn(660 - t * 600, t) * .6f + Sn(990, t) * .3f) * MathF.Sin(t / .22f * M.PI) * .14f)); S("cricket", Make(.3f, t => Sn(4200, t) * MathF.Pow(MathF.Max(0, MathF.Sin(t * 120)), 2) * .05f)); S("owl", Make(.9f, t => Sn(360 - (t > .4f ? 30 : 0), t) * MathF.Pow(MathF.Max(0, MathF.Sin(t / .45f * M.PI)), 1) * .2f)); S("hop", Make(.1f, t => Sn(260 + t * 800, t) * Env(t, .005f, .03f) * .14f)); S("gull", Make(.5f, t => Sn(1200 + MathF.Sin(t * 22) * 300, t) * MathF.Sin(t / .5f * M.PI) * .1f));
        S("thunder", Make(3.2f, t => { float lp = M.Noise(t * 90, 0, 4) + M.Noise(t * 220, 0, 8) * .5f; return lp * (Env(t, .02f, .5f) + Env(MathF.Max(0, t - .5f), .3f, .9f) * .5f) * .6f; })); S("climb", Make(.1f, t => (Nz(t) * .2f + Sn(180, t) * .2f) * Env(t, .004f, .025f))); S("slide", Make(.4f, t => Nz(t) * MathF.Sin(t / .4f * M.PI) * .16f)); S("sit", Make(.2f, t => (Sn(120, t) + Nz(t) * .2f) * Env(t, .01f, .05f) * .3f)); S("snore", Make(.8f, t => (Nz(t) * .3f + Sn(70, t)) * MathF.Sin(t / .8f * M.PI) * .1f));
        float[] bases = [210, 290, 130, 420, 180, 250];
        for (int v = 0; v < 6; v++)
        {
            int vv = v; var l = new List<Sound>(); for (int k = 0; k < 4; k++) { int kk = k; float f0 = bases[vv] * (1 + (kk - 1.5f) * .09f); l.Add(Make(.13f, t => { float ff = f0 * (1 + .06f * MathF.Sin(t * 40)); float form = MathF.Sin(t * (600 + kk * 240) * M.TAU) * .35f; float saw = (t * ff % 1) * 2 - 1; return (saw * .4f + Sn(ff, t) * .6f) * (.65f + form) * MathF.Sin(t / .13f * M.PI) * (vv == 5 ? .14f : .22f) + (vv == 5 ? Nz(t) * .03f : 0); })); } S("voice" + (char)('a' + v), l.ToArray());
        }
    }
}

using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// F1: a testing menu for weather, sky events, time, travel and items. The game keeps running behind it.
public sealed partial class GameScreen
{
    bool admin, hideHud; int admTab; float quakeT, quakeLen, quakeDust; const float AW = 190; float AX => 70; public bool OverAdmin => admin && panel == Panel.None && Ui.Mouse.X > AX - 4 && Ui.Mouse.X < AX + AW + 6 && Ui.Mouse.Y > 12;
    public void ToggleAdminForTest() => admin = !admin;
    void AdminTick(float dt) { if (quakeT > 0) { quakeT -= dt; float k = M.Smooth(0, 1.2f, quakeLen - quakeT) * M.Smooth(0, 1.8f, quakeT); Shake(.4f + 3.4f * k * (.6f + .4f * M.Noise(time * 3))); quakeDust -= dt; if (quakeDust < 0 && k > .25f) { quakeDust = .08f; float qx = CamX + rng.F() * Ui.VW; Life.Dust(new(qx, W.Top[Math.Clamp((int)(qx / 24), 0, World.W - 1)] * 24), "dirt", 3, 0); if (rng.P(.3f)) Life.Leaves(new(qx, CamY + 60), 2, World.GrassCol(qx / 24)); } } }
    public void Travel(Vector2 p) { back.Get(W.BiomeAt(p.X / 24), true); P.P = p; P.V = default; P.Climb = false; CamX = P.P.X - Ui.VW / 2; CamY = P.P.Y - 34 - Ui.VH * .66f; lookX = lookY = 0; Life.Sparkle(P.P + new Vector2(0, -40), 14, new Color(255, 236, 170, 255)); Snd.Play("whoosh", .6f); }
    /// A standing spot inside a biome: open ground for surface ones, a cave floor for the others.
    public Vector2? FindBiome(Biome b)
    {
        if (!Biomes.Of(b).Under) { var s = W.Segs.FirstOrDefault(q => q.B == b); if (s == null) return null; int c = b == Biome.Coast ? 150 : (s.X0 + s.X1) / 2; for (int k = 0; k < 60; k++) foreach (int x in new[] { c + k, c - k }) { var p = new Vector2(x * 24 + 12, W.Top0[x] * 24); if (W.FitsPlayer(p) && W.Solid(x, W.Top0[x]) && !W.WetTile(x, W.Top0[x] - 1)) return p; } return null; }
        for (int k = 0; k < 4000; k++) { int x = rng.I(170, World.W - 170), y = rng.I(Math.Min(World.H - 12, W.Top[x] + 24), World.H - 9); if (W.UnderAt(x, y) != b || W.Solid(x, y)) continue; while (y < World.H - 5 && !W.Solid(x, y + 1)) y++; var p = new Vector2(x * 24 + 12, (y + 1) * 24); if (W.UnderAt(x, y) == b && W.FitsPlayer(p) && !W.WetTile(x, y)) return p; }
        return null;
    }
    void Admin()
    {
        float x = AX, y = 16, w = AW, h = Ui.VH - 30; Ui.Nine("panel", x, y, w, h, Color.White); Font.Draw("Admin", x + 13, y + 11, Ui.Ink, 1.5f); Font.Draw("F1 to close", x + w - 13, y + 14, Ui.Soft, .85f, 1); string[] tb = ["Weather", "Sky", "Time", "Travel", "Items"]; float tw = (w - 24 - 8) / 5; for (int i = 0; i < 5; i++) if (Ui.Button("ad" + i, tb[i], x + 12 + i * (tw + 2), y + 27, tw, 16, "btnP", on: admTab == i, text: .8f)) admTab = i;
        float cx = x + 13, cy = y + 50, cw = w - 26; int n = 0; float rowH = 22; bool B(string label, string style = "btn", string tip = null) { float bw = (cw - 4) / 2; bool r = Ui.Button("adb" + admTab + "_" + n, label, cx + (n % 2) * (bw + 4), cy + (n / 2) * rowH, bw, rowH - 3, style, null, tip, text: Font.Width(label) > bw - 8 ? .8f : 1); n++; return r; } void Gap(string title) { if (n % 2 == 1) n++; cy += (n / 2) * rowH + 4; n = 0; Font.Draw(title, cx, cy, Ui.Soft); cy += 11; }
        switch (admTab)
        {
            case 0:
                foreach (var q in Enum.GetValues<Weather>()) if (B(q.ToString(), Env.Now == q ? "btnG" : "btnB")) { Env.SetWeather(q); Env.WeatherT = 600; Toast(q + " is building. Use Instant to skip ahead"); }
                if (B("Instant", "btnP", "Skip the slow change")) { var now = Env.Now; Env.SetWeather(now, true); }
                Gap("Wind"); { float v = Env.WindForce < 0 ? Env.Wind / 1.6f : Env.WindForce / 1.6f; if (Ui.Slider("adw", ref v, cx + 2, cy, cw - 6)) Env.WindForce = v * 1.6f; cy += 16; } if (B("Natural wind", "btnP")) Env.WindForce = -1; if (B("Gentle gust")) { P.An.Gust(1.4f); Life.Leaves(P.P + new Vector2(-60, -50), 16, World.GrassCol(P.P.X / 24)); Snd.Play("whoosh", .6f, .7f); }
                Gap("Events"); if (B("Lightning")) { Env.Flash = 1.1f; Env.BoltT = 0; Env.BoltX = .15f + rng.F() * .7f; Env.BoltSeed = rng.I(9999); Snd.Play("thunder", .9f); Shake(2); } if (B("Rainbow")) Env.RainbowGoal = 70; if (B("Petal shower")) Life.Petals(0, 26); if (B("Leaf storm")) Life.Petals(1, 26); if (B("Earthquake", "btnR")) { quakeT = quakeLen = 6; Snd.Play("thunder", .8f, .5f); Life.Scatter(CamX, CamY, (int)Ui.VW); } if (B("Melt snow", "btnP")) { Env.Melting = true; if (Env.Now == Weather.Snow) Env.SetWeather(Weather.Clear); } break;
            case 1:
                if (B("Shooting star")) { Env.SpawnMeteor(false, rng.P(.35f)); if (Env.Night < .5f) Toast("Only visible at night"); } if (B("Meteor shower")) { Env.MeteorT = 45; if (Env.Night < .5f) Env.FastForward(.93f); } if (B(Env.ForceAurora ? "Aurora off" : "Aurora", Env.ForceAurora ? "btnG" : "btn")) { Env.ForceAurora = !Env.ForceAurora; if (Env.ForceAurora && Env.Night < .5f) Env.FastForward(.93f); } if (B("Solar eclipse")) { if (Env.Night > .4f) Env.FastForward(.45f); Env.EclipseT = Env.EclipseLen; Snd.Play("area", .6f, .7f); } if (B("Clear sky")) Env.SetWeather(Weather.Clear, true); if (B("Full clouds")) Env.SetWeather(Weather.Cloudy, true);
                Gap("Wildlife"); if (B("Bird flock")) Life.Flock(CamX, CamY, (int)Ui.VW); if (B("Scare birds")) Life.Scatter(CamX, CamY, (int)Ui.VW); if (B("Firefly swarm")) Life.Swarm(P.P + new Vector2(0, -40)); if (B("Sparkles")) Life.Sparkle(P.P + new Vector2(0, -50), 40, new Color(255, 226, 140, 255)); break;
            case 2:
                (string l, float c)[] tm = [("Sunrise", .262f), ("Morning", .33f), ("Noon", .5f), ("Afternoon", .62f), ("Golden hour", .70f), ("Sunset", .757f), ("Dusk", .80f), ("Night", .90f), ("Midnight", .999f), ("Before dawn", .22f)]; foreach (var q in tm) if (B(q.l, "btnB")) Env.FastForward(q.c);
                Gap("Time of day"); { float v = Env.Clock; if (Ui.Slider("adt", ref v, cx + 2, cy, cw - 6)) { Env.Clock = M.Clamp(v, 0, .9995f); Env.Colors(); } cy += 16; } Font.Draw(Env.TimeText + "   Day " + Env.Day, cx, cy, Ui.Ink); cy += 12; if (B(Env.TimeScale == 0 ? "Unfreeze" : "Freeze time", Env.TimeScale == 0 ? "btnG" : "btn")) Env.TimeScale = Env.TimeScale == 0 ? 1 : 0; if (B("Fast x30", Env.TimeScale > 5 ? "btnG" : "btn")) Env.TimeScale = Env.TimeScale > 5 ? 1 : 30; if (B("Next day")) Env.Day++; break;
            case 3:
                rowH = 21; foreach (var b in Enum.GetValues<Biome>()) if (B(Biomes.Of(b).Name, Biomes.Of(b).Under ? "btn" : "btnB")) { var p = FindBiome(b); if (p != null) Travel(p.Value); else Toast("Could not find a spot"); }
                Gap("World " + W.Seed); if (B("Spawn point", "btnG")) Travel(W.SafeSpawn()); if (B(hideHud ? "Show HUD" : "Hide HUD", hideHud ? "btnG" : "btnP")) hideHud = !hideHud; break;
            default:
                rowH = 18; var d = Store.Data; if (B("20 of each", "btnG")) { for (int i = 1; i < Blocks.All.Length; i++) if (Items.Valid(i)) Bag.Add(d, i, 20); Toast("Items added"); } if (B("Empty bag", "btnR")) { Array.Clear(d.Slots); held = default; d.Hand = -1; Toast("Bag emptied"); }
                for (int i = 1; i < Blocks.All.Length; i++) if (Items.Valid(i) && B(Items.Name(i), "btnP")) { if (Bag.Add(d, i, 10) > 0) Toast("Your satchel is full"); else Gained(i, 10); } break;
        }
    }
}

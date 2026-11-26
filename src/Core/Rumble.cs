using System.Runtime.InteropServices;
namespace UntitledGame;

/// Controller rumble through XInput (Xbox pads, and PlayStation or Switch pads through Steam Input).
/// Every kick is a short pulse that fades out; overlapping pulses take the stronger motor of each.
public static class Rumble
{
    [StructLayout(LayoutKind.Sequential)] struct Vib { public ushort L, R; }
    [StructLayout(LayoutKind.Sequential)] public struct State { public uint Packet; public ushort Buttons; public byte LT, RT; public short LX, LY, RX, RY; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] static extern uint Set14(uint i, ref Vib v);
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern uint Get14(uint i, out State s);
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")] static extern uint Set91(uint i, ref Vib v);
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] static extern uint Get91(uint i, out State s);

    static int api = -1, pad = -1; static float scan, sinceSend; static ushort sentL, sentR;
    static readonly List<(float lo, float hi, float len, float t)> pulses = new();
    public static bool Connected => pad >= 0;
    /// low = the heavy motor (thuds), high = the light motor (buzz and ticks), in 0..1; len in seconds.
    public static void Kick(float low, float high, float len)
    {
        float k = Store.Cfg.Rumble; if (k <= 0 || !In.PadOn) return; if (pulses.Count > 12) pulses.RemoveAt(0);
        // the motors barely turn below about a quarter speed, so anything asked for is at least that strong
        static float Felt(float v) => v <= 0 ? 0 : M.Clamp(.25f + .75f * v);
        pulses.Add((Felt(low * k), Felt(high * k), MathF.Max(.06f, len), 0));
    }
    public static void Update(float dt)
    {
        scan -= dt; if (scan <= 0) { scan = 2; Find(); }
        float l = 0, r = 0;
        for (int i = pulses.Count - 1; i >= 0; i--) { var p = pulses[i]; p.t += dt; if (p.t >= p.len) { pulses.RemoveAt(i); continue; } pulses[i] = p; float f = MathF.Sqrt(1 - p.t / p.len); l = MathF.Max(l, p.lo * f); r = MathF.Max(r, p.hi * f); }
        sinceSend += dt; Send((ushort)(l * 65535), (ushort)(r * 65535));
    }
    /// For diagnosis: what XInput reports for each slot.
    public static string Probe()
    {
        var sb = new System.Text.StringBuilder();
        foreach (int a in new[] { 0, 1 }) { try { for (uint i = 0; i < 4; i++) { uint e = a == 0 ? Get14(i, out var st) : Get91(i, out st); sb.AppendLine($"xinput{(a == 0 ? "1_4" : "9_1_0")}[{i}] = {e}{(e == 0 ? $" buttons {st.Buttons:X} LT {st.LT} RT {st.RT} R {st.RX},{st.RY}" : "")}"); } } catch (Exception ex) { sb.AppendLine($"xinput api {a}: {ex.GetType().Name}"); } }
        Find(); sb.Append($"chosen api {api} pad {pad}"); return sb.ToString();
    }
    /// The current state of the XInput pad, if there is one.
    public static bool Read(out State st)
    {
        st = default; if (scan <= 0 && pad < 0) { scan = 2; Find(); } if (pad < 0 || api < 0) return false;
        try { uint e = api == 0 ? Get14((uint)pad, out st) : Get91((uint)pad, out st); if (e == 0) return true; pad = -1; return false; } catch { api = -2; pad = -1; return false; }
    }
    public static string Status => pad >= 0 ? "Xbox-style controller found. Rumble is on." : In.Kind == In.PadKind.PlayStation ? "PlayStation controller found. For rumble, play through Steam." : In.Pad0 ? "Controller found. Rumble needs an Xbox-style controller." : "No controller found.";
    public static void Stop() { pulses.Clear(); Send(0, 0); }
    static void Send(ushort l, ushort r)
    {
        if (pad < 0 || api < 0 || (l == sentL && r == sentR)) return; if (sinceSend < .03f && l != 0 && r != 0 && Math.Abs(l - sentL) < 12000 && Math.Abs(r - sentR) < 12000) return; sinceSend = 0; var v = new Vib { L = l, R = r }; // wireless pads drop a flood of tiny changes
        try { uint e = api == 0 ? Set14((uint)pad, ref v) : Set91((uint)pad, ref v); if (e != 0) pad = -1; else { sentL = l; sentR = r; } } catch { api = -2; pad = -1; }
    }
    static void Find()
    {
        if (api == -2 || !OperatingSystem.IsWindows()) return;
        for (int a = api < 0 ? 0 : api; a <= 1; a++)
        {
            try { for (uint i = 0; i < 4; i++) { uint e = a == 0 ? Get14(i, out _) : Get91(i, out _); if (e == 0) { api = a; if (pad != (int)i) { pad = (int)i; sentL = sentR = 1; } return; } } api = a; pad = -1; return; }
            catch (DllNotFoundException) { continue; } catch (EntryPointNotFoundException) { continue; } catch { break; }
        }
        api = -2; pad = -1;
    }
}

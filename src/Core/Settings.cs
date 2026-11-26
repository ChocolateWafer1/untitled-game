using System.Numerics;
using System.Text.Json;
using Raylib_cs;
namespace UntitledGame;

public sealed class Settings
{
    public float Master { get; set; } = .8f;
    public float Music { get; set; } = .55f;
    public float Sfx { get; set; } = .8f;
    public float Ambience { get; set; } = .7f;
    public float Ui { get; set; } = .7f;
    public int Window { get; set; } = 0; // 0 borderless fullscreen, 1 windowed fullscreen (maximised)
    public bool Vsync { get; set; } = true;
    public int Light { get; set; } = 2; // 0 low 1 medium 2 high
    public float Shake { get; set; } = 1;
    public bool PixelPerfect { get; set; } = false;
    public bool ReduceMotion { get; set; }
    public bool BigText { get; set; }
    public bool Contrast { get; set; }
    public float Rumble { get; set; } = .8f;
    public Dictionary<string, int> Keys { get; set; } = new();
}

public static class Store
{
    static readonly JsonSerializerOptions O = new() { WriteIndented = true };
    public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Untitled Game");
    public static Settings Cfg = new();
    public static SaveData Data = new(); public static Saves.Slot Current; public static List<Piece[]> Outfits = new();
    public static bool ReadOnly, TestSaves;
    public static T Load<T>(string f) where T : new()
    {
        try { var p = Path.Combine(Dir, f); if (File.Exists(p)) return JsonSerializer.Deserialize<T>(File.ReadAllText(p), O) ?? new T(); } catch { }
        return new T();
    }
    static void Put(string f, object o)
    {
        if (ReadOnly) return;
        // write to a temp file first so a crash mid-save can never corrupt progress
        try { Directory.CreateDirectory(Dir); var p = Path.Combine(Dir, f); File.WriteAllText(p + ".tmp", JsonSerializer.Serialize(o, o.GetType(), O)); File.Move(p + ".tmp", p, true); } catch { }
    }
    /// The game used to have another name: copy saves from the old folder once. Never overwrites and never deletes the originals.
    public static void Migrate(string oldDir, string newDir)
    {
        try { if (!Directory.Exists(oldDir)) return; foreach (var f in new[] { "save.json", "settings.json" }) { string a = Path.Combine(oldDir, f), b = Path.Combine(newDir, f); if (File.Exists(a) && !File.Exists(b)) { Directory.CreateDirectory(newDir); File.Copy(a, b); } } } catch { }
    }
    public static void LoadAll()
    {
        if (!ReadOnly) Migrate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UNNAMED GAME"), Dir);
        Cfg = Load<Settings>("settings.json"); Outfits = Load<List<Piece[]>>("outfits.json") ?? new(); Outfits.RemoveAll(o => o == null || o.Length != new AvatarData().P.Length); Saves.Refresh();
        Cfg.Light = Math.Clamp(Cfg.Light, 0, 2); Cfg.Keys ??= new();
    }
    public static void SaveCfg() => Put("settings.json", Cfg);
    /// Writes the save being played. Does nothing on the title screen or while a character is still being made.
    public static void SaveGame() { if (Current != null) { Program.Game?.Flush(); Current.Data.Played = DateTime.UtcNow; Saves.Write(Current); } }
    public static void SaveOutfits() => Put("outfits.json", Outfits);
}

public enum Act { Left, Right, Up, Down, Jump, Run, Bag, Map, Emote, Menu }
public static class In
{
    public static readonly Dictionary<Act, KeyboardKey> Def = new()
    {
        [Act.Left] = KeyboardKey.A, [Act.Right] = KeyboardKey.D, [Act.Up] = KeyboardKey.W, [Act.Down] = KeyboardKey.S, [Act.Jump] = KeyboardKey.Space, [Act.Run] = KeyboardKey.LeftShift, [Act.Bag] = KeyboardKey.Tab, [Act.Map] = KeyboardKey.M, [Act.Emote] = KeyboardKey.Q, [Act.Menu] = KeyboardKey.Escape
    };
    static readonly Dictionary<Act, KeyboardKey> Alt = new() { [Act.Left] = KeyboardKey.Left, [Act.Right] = KeyboardKey.Right, [Act.Up] = KeyboardKey.Up, [Act.Down] = KeyboardKey.Down, [Act.Run] = KeyboardKey.RightShift, [Act.Bag] = KeyboardKey.E };
    static readonly Dictionary<Act, GamepadButton> Pad = new() { [Act.Jump] = GamepadButton.RightFaceDown, [Act.Bag] = GamepadButton.RightFaceUp, [Act.Map] = GamepadButton.MiddleLeft, [Act.Emote] = GamepadButton.RightFaceLeft, [Act.Menu] = GamepadButton.MiddleRight, [Act.Left] = GamepadButton.LeftFaceLeft, [Act.Right] = GamepadButton.LeftFaceRight, [Act.Down] = GamepadButton.LeftFaceDown, [Act.Up] = GamepadButton.LeftFaceUp };
    public static HashSet<Act> Force = new(); // scripted input for automated tests
    public static HashSet<Act> ForcePress = new();
    public static bool Locked;
    /// E is a second inventory key unless the player has bound it to something else.
    static bool AltFree(Act a, KeyboardKey k) => a != Act.Bag || !Enum.GetValues<Act>().Any(o => o != Act.Bag && Key(o) == k);
    public static KeyboardKey Key(Act a) => Store.Cfg.Keys.TryGetValue(a.ToString(), out int k) ? (KeyboardKey)k : Def[a];
    public static bool Down(Act a)
    {
        if (Force.Contains(a)) return true;
        if (Locked) return false;
        if (Raylib.IsKeyDown(Key(a)) || (Alt.TryGetValue(a, out var k) && AltFree(a, k) && Raylib.IsKeyDown(k))) return true;
        if (Pad0)
        {
            if (Pad.TryGetValue(a, out var b) && PadDown(b)) return true;
            var st = LStick; if (a == Act.Run && (runLock || MathF.Abs(st.X) > .9f)) return true; // push the stick all the way to run, or click it
            if ((a == Act.Left && st.X < -.3f) || (a == Act.Right && st.X > .3f) || (a == Act.Up && st.Y < -.45f) || (a == Act.Down && st.Y > .45f)) return true;
        }
        return false;
    }
    public static bool Press(Act a)
    {
        if (ForcePress.Contains(a)) return true;
        if (Locked) return false;
        if (Raylib.IsKeyPressed(Key(a)) || (Alt.TryGetValue(a, out var k) && AltFree(a, k) && Raylib.IsKeyPressed(k))) return true;
        return Pad0 && Pad.TryGetValue(a, out var b) && PadPress(b);
    }
    public static string Name(Act a) => KeyName(Key(a));
    // ---------------------------------------------------------------- controller
    // Xbox-style pads (and anything Steam Input presents as one) are read straight from XInput, which always reports the
    // right buttons, sticks and triggers. Other pads, such as PlayStation ones without Steam, come through raylib's mappings.
    public enum PadKind { None, Xbox, PlayStation, Other }
    public static PadKind Kind; public static string PadName = "";
    static uint btn, prev; static Vector2 ls, rs; static float lt, rt; static bool runLock;
    const uint LT2 = 0x10000, RT2 = 0x20000;
    public static bool Pad0 => Kind != PadKind.None;
    public static bool PadOn => Pad0 || FakePad;
    /// True while the controller is the thing being used (switches back as soon as the mouse or keyboard is touched).
    public static bool UsingPad; public static bool FakePad; public static Vector2 FakeR; public static bool FakeRT, FakeLT;
    static uint Bit(GamepadButton b) => b switch
    {
        GamepadButton.LeftFaceUp => 0x1, GamepadButton.LeftFaceDown => 0x2, GamepadButton.LeftFaceLeft => 0x4, GamepadButton.LeftFaceRight => 0x8, GamepadButton.MiddleRight => 0x10, GamepadButton.MiddleLeft => 0x20,
        GamepadButton.LeftThumb => 0x40, GamepadButton.RightThumb => 0x80, GamepadButton.LeftTrigger1 => 0x100, GamepadButton.RightTrigger1 => 0x200, GamepadButton.RightFaceDown => 0x1000, GamepadButton.RightFaceRight => 0x2000,
        GamepadButton.RightFaceLeft => 0x4000, GamepadButton.RightFaceUp => 0x8000, GamepadButton.LeftTrigger2 => LT2, GamepadButton.RightTrigger2 => RT2, _ => 0
    };
    static readonly GamepadButton[] All = [GamepadButton.LeftFaceUp, GamepadButton.LeftFaceDown, GamepadButton.LeftFaceLeft, GamepadButton.LeftFaceRight, GamepadButton.MiddleRight, GamepadButton.MiddleLeft, GamepadButton.LeftThumb, GamepadButton.RightThumb, GamepadButton.LeftTrigger1, GamepadButton.RightTrigger1, GamepadButton.RightFaceDown, GamepadButton.RightFaceRight, GamepadButton.RightFaceLeft, GamepadButton.RightFaceUp, GamepadButton.LeftTrigger2, GamepadButton.RightTrigger2];
    public static bool PadDown(GamepadButton b) => (btn & Bit(b)) != 0;
    public static bool PadPress(GamepadButton b) => (btn & Bit(b)) != 0 && (prev & Bit(b)) == 0;
    public static bool PadRelease(GamepadButton b) => (btn & Bit(b)) == 0 && (prev & Bit(b)) != 0;
    static Vector2 Dz(Vector2 v) { float l = v.Length(); return l < .24f ? default : v / l * M.Clamp((l - .24f) / .7f); }
    public static Vector2 LStick => Dz(ls);
    public static Vector2 RStick => FakePad ? FakeR : Dz(rs);
    public static bool RTDown, RTPress, LTDown, LTPress;
    static bool IsPlayStation(string n) { n = n.ToLowerInvariant(); return n.Contains("ps3") || n.Contains("ps4") || n.Contains("ps5") || n.Contains("dualsense") || n.Contains("dualshock") || n.Contains("sony") || n == "wireless controller"; }
    static void Poll()
    {
        prev = btn; btn = 0; ls = rs = default; lt = rt = 0;
        if (Rumble.Read(out var x))
        {
            Kind = PadKind.Xbox; PadName = "Xbox controller"; btn = x.Buttons; ls = new(x.LX / 32767f, -x.LY / 32767f); rs = new(x.RX / 32767f, -x.RY / 32767f); lt = x.LT / 255f; rt = x.RT / 255f;
        }
        else if (Raylib.IsGamepadAvailable(0))
        {
            PadName = Raylib.GetGamepadName_(0) ?? ""; Kind = IsPlayStation(PadName) ? PadKind.PlayStation : PadKind.Other;
            foreach (var b in All) if (Raylib.IsGamepadButtonDown(0, b)) btn |= Bit(b);
            ls = new(Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftX), Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftY)); rs = new(Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightX), Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightY));
            lt = (btn & LT2) != 0 ? 1 : 0; rt = (btn & RT2) != 0 ? 1 : 0;
        }
        else Kind = PadKind.None;
        if (lt > .25f) btn |= LT2; else btn &= ~LT2; if (rt > .25f) btn |= RT2; else btn &= ~RT2;
    }
    /// Once a frame, before anything reads input: the controller's state, trigger edges, the run toggle, and which device was used last.
    public static void Track()
    {
        Poll();
        bool r2 = FakePad ? FakeRT : PadDown(GamepadButton.RightTrigger2), l2 = FakePad ? FakeLT : PadDown(GamepadButton.LeftTrigger2);
        RTPress = r2 && !RTDown; LTPress = l2 && !LTDown; RTDown = r2; LTDown = l2;
        if (PadPress(GamepadButton.LeftThumb)) runLock = !runLock; if (LStick.X == 0 && !PadDown(GamepadButton.LeftFaceLeft) && !PadDown(GamepadButton.LeftFaceRight)) runLock = false;
        if (FakePad) { UsingPad = true; return; }
        if (Pad0 && ((btn & ~prev) != 0 || LStick != default || RStick != default)) UsingPad = true;
        if (Raylib.GetMouseDelta().LengthSquared() > 1 || Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonPressed(MouseButton.Right) || Raylib.GetMouseWheelMove() != 0) UsingPad = false;
        foreach (var k in KeysWatched) if (Raylib.IsKeyPressed(k)) { UsingPad = false; break; }
        if (!Pad0) UsingPad = false;
    }
    static readonly KeyboardKey[] KeysWatched = [KeyboardKey.W, KeyboardKey.A, KeyboardKey.S, KeyboardKey.D, KeyboardKey.Space, KeyboardKey.E, KeyboardKey.Tab, KeyboardKey.Escape, KeyboardKey.Enter, KeyboardKey.Left, KeyboardKey.Right, KeyboardKey.Up, KeyboardKey.Down, KeyboardKey.LeftShift];
    /// B (Circle) on the controller, used to go back out of menus.
    public static bool PadBack => PadPress(GamepadButton.RightFaceRight);
    /// A button's name as printed on the controller in use.
    public static string PadLabel(GamepadButton b)
    {
        bool ps = Kind == PadKind.PlayStation;
        return b switch
        {
            GamepadButton.RightFaceDown => ps ? "Cross" : "A", GamepadButton.RightFaceRight => ps ? "Circle" : "B", GamepadButton.RightFaceLeft => ps ? "Square" : "X", GamepadButton.RightFaceUp => ps ? "Triangle" : "Y",
            GamepadButton.LeftTrigger1 => ps ? "L1" : "LB", GamepadButton.RightTrigger1 => ps ? "R1" : "RB", GamepadButton.LeftTrigger2 => ps ? "L2" : "LT", GamepadButton.RightTrigger2 => ps ? "R2" : "RT",
            GamepadButton.MiddleLeft => ps ? "Share" : "View", GamepadButton.MiddleRight => ps ? "Options" : "Menu", GamepadButton.LeftThumb => ps ? "L3" : "Left stick", _ => b.ToString()
        };
    }
    public static string KeyName(KeyboardKey k) => k switch { KeyboardKey.LeftShift => "Shift", KeyboardKey.RightShift => "R Shift", KeyboardKey.LeftControl => "Ctrl", KeyboardKey.Escape => "Esc", KeyboardKey.Space => "Space", KeyboardKey.Comma => ",", _ => k.ToString().Replace("Key", "") };
}

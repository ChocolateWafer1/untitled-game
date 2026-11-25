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
    static readonly Dictionary<Act, GamepadButton> Pad = new() { [Act.Jump] = GamepadButton.RightFaceDown, [Act.Run] = GamepadButton.RightTrigger1, [Act.Bag] = GamepadButton.RightFaceUp, [Act.Map] = GamepadButton.MiddleLeft, [Act.Emote] = GamepadButton.LeftFaceUp, [Act.Menu] = GamepadButton.MiddleRight, [Act.Left] = GamepadButton.LeftFaceLeft, [Act.Right] = GamepadButton.LeftFaceRight, [Act.Down] = GamepadButton.LeftFaceDown };
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
        if (Raylib.IsGamepadAvailable(0))
        {
            if (Pad.TryGetValue(a, out var b) && Raylib.IsGamepadButtonDown(0, b)) return true;
            float x = Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftX), y = Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftY);
            if ((a == Act.Left && x < -.4f) || (a == Act.Right && x > .4f) || (a == Act.Up && y < -.5f) || (a == Act.Down && y > .5f)) return true;
        }
        return false;
    }
    public static bool Press(Act a)
    {
        if (ForcePress.Contains(a)) return true;
        if (Locked) return false;
        if (Raylib.IsKeyPressed(Key(a)) || (Alt.TryGetValue(a, out var k) && AltFree(a, k) && Raylib.IsKeyPressed(k))) return true;
        return Raylib.IsGamepadAvailable(0) && Pad.TryGetValue(a, out var b) && Raylib.IsGamepadButtonPressed(0, b);
    }
    public static string Name(Act a) => KeyName(Key(a));
    public static string KeyName(KeyboardKey k) => k switch { KeyboardKey.LeftShift => "Shift", KeyboardKey.RightShift => "R Shift", KeyboardKey.LeftControl => "Ctrl", KeyboardKey.Escape => "Esc", KeyboardKey.Space => "Space", KeyboardKey.Comma => ",", _ => k.ToString().Replace("Key", "") };
}

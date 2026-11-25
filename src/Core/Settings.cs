using System.Text.Json;
using Raylib_cs;
namespace UnnamedGame;

public sealed class FishRecord { public int Count { get; set; } public float BestKg { get; set; } public float BestCm { get; set; } }
public sealed class FishItem { public int Id { get; set; } public float Kg { get; set; } public float Cm { get; set; } }

public sealed class SaveData
{
    public AvatarData Avatar { get; set; } = new();
    public AvatarData[] Outfits { get; set; } = new AvatarData[5];
    public bool Created { get; set; }
    public int Coins { get; set; } = 30;
    public int Rod { get; set; } = 0;
    public bool[] Rods { get; set; } = [true, false, false, false, false];
    public int Bait { get; set; } = 0;
    public int[] Baits { get; set; } = new int[12];
    public int Lure { get; set; } = 0;
    public bool[] Lures { get; set; } = [true, false, false, false, false, false];
    public List<FishItem> Fish { get; set; } = new();
    public List<FishItem> Tank { get; set; } = new();
    public Dictionary<int, FishRecord> Journal { get; set; } = new();
    public int[] Forage { get; set; } = new int[12];
    public Dictionary<string, float> ForageTimes { get; set; } = new();
    public HashSet<string> Areas { get; set; } = new();
    public float Clock { get; set; } = .30f; // 0..1 through the day, starts in the morning
    public int Day { get; set; } = 1;
    public float X { get; set; } = -1;
    public float Y { get; set; } = -1;
    public bool TutorialDone { get; set; }
}

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
    public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UNNAMED GAME");
    public static Settings Cfg = new();
    public static SaveData Data = new();
    public static bool ReadOnly;
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
    public static void LoadAll()
    {
        Cfg = Load<Settings>("settings.json"); Data = Load<SaveData>("save.json"); Data.Avatar ??= new(); Data.Avatar.Validate();
        if (Data.Baits == null || Data.Baits.Length != 12) Data.Baits = new int[12];
        if (Data.Forage == null || Data.Forage.Length != 12) Data.Forage = new int[12];
        if (Data.Outfits == null || Data.Outfits.Length != 5) Data.Outfits = new AvatarData[5];
        foreach (var o in Data.Outfits) o?.Validate();
        if (Data.Rods == null || Data.Rods.Length != 5) Data.Rods = [true, false, false, false, false];
        if (Data.Lures == null || Data.Lures.Length != 6) Data.Lures = [true, false, false, false, false, false];
        Data.Rod = Math.Clamp(Data.Rod, 0, 4); Data.Lure = Math.Clamp(Data.Lure, 0, 5); Data.Bait = Math.Clamp(Data.Bait, 0, 11); Data.Coins = Math.Max(0, Data.Coins);
        Data.Fish ??= new(); Data.Tank ??= new(); Data.Journal ??= new(); Data.Areas ??= new(); Data.ForageTimes ??= new();
        Data.Fish.RemoveAll(f => f == null || f.Id < 0 || f.Id >= FishDex.All.Length); Data.Tank.RemoveAll(f => f == null || f.Id < 0 || f.Id >= FishDex.All.Length);
        Cfg.Light = Math.Clamp(Cfg.Light, 0, 2); Cfg.Keys ??= new();
    }
    public static void SaveCfg() => Put("settings.json", Cfg);
    public static void SaveGame() => Put("save.json", Data);
}

public enum Act { Left, Right, Up, Down, Jump, Run, Interact, Fish, Bag, Journal, Map, Emote, Menu }
public static class In
{
    public static readonly Dictionary<Act, KeyboardKey> Def = new()
    {
        [Act.Left] = KeyboardKey.A, [Act.Right] = KeyboardKey.D, [Act.Up] = KeyboardKey.W, [Act.Down] = KeyboardKey.S, [Act.Jump] = KeyboardKey.Space, [Act.Run] = KeyboardKey.LeftShift, [Act.Interact] = KeyboardKey.E, [Act.Fish] = KeyboardKey.F, [Act.Bag] = KeyboardKey.Tab, [Act.Journal] = KeyboardKey.J, [Act.Map] = KeyboardKey.M, [Act.Emote] = KeyboardKey.Q, [Act.Menu] = KeyboardKey.Escape
    };
    static readonly Dictionary<Act, KeyboardKey> Alt = new() { [Act.Left] = KeyboardKey.Left, [Act.Right] = KeyboardKey.Right, [Act.Up] = KeyboardKey.Up, [Act.Down] = KeyboardKey.Down, [Act.Run] = KeyboardKey.RightShift, [Act.Interact] = KeyboardKey.Enter };
    static readonly Dictionary<Act, GamepadButton> Pad = new() { [Act.Jump] = GamepadButton.RightFaceDown, [Act.Run] = GamepadButton.RightTrigger1, [Act.Interact] = GamepadButton.RightFaceLeft, [Act.Fish] = GamepadButton.RightFaceRight, [Act.Bag] = GamepadButton.RightFaceUp, [Act.Journal] = GamepadButton.LeftTrigger1, [Act.Map] = GamepadButton.MiddleLeft, [Act.Emote] = GamepadButton.LeftFaceUp, [Act.Menu] = GamepadButton.MiddleRight, [Act.Left] = GamepadButton.LeftFaceLeft, [Act.Right] = GamepadButton.LeftFaceRight, [Act.Down] = GamepadButton.LeftFaceDown };
    public static HashSet<Act> Force = new(); // scripted input for automated tests
    public static HashSet<Act> ForcePress = new();
    public static bool Locked;
    public static KeyboardKey Key(Act a) => Store.Cfg.Keys.TryGetValue(a.ToString(), out int k) ? (KeyboardKey)k : Def[a];
    public static bool Down(Act a)
    {
        if (Force.Contains(a)) return true;
        if (Locked) return false;
        if (Raylib.IsKeyDown(Key(a)) || (Alt.TryGetValue(a, out var k) && Raylib.IsKeyDown(k))) return true;
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
        if (Raylib.IsKeyPressed(Key(a)) || (Alt.TryGetValue(a, out var k) && Raylib.IsKeyPressed(k))) return true;
        return Raylib.IsGamepadAvailable(0) && Pad.TryGetValue(a, out var b) && Raylib.IsGamepadButtonPressed(0, b);
    }
    public static string Name(Act a) => KeyName(Key(a));
    public static string KeyName(KeyboardKey k) => k switch { KeyboardKey.LeftShift => "Shift", KeyboardKey.RightShift => "R Shift", KeyboardKey.LeftControl => "Ctrl", KeyboardKey.Escape => "Esc", KeyboardKey.Space => "Space", KeyboardKey.Comma => ",", _ => k.ToString().Replace("Key", "") };
}

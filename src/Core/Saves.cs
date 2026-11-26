using System.Text.Json;
using Raylib_cs;
namespace UntitledGame;

public struct ItemStack { public int Id { get; set; } public int N { get; set; } public readonly bool Empty => Id <= 0 || N <= 0; public ItemStack(int id, int n) { Id = id; N = n; } }
public sealed class DropData { public int Id { get; set; } public int N { get; set; } public float X { get; set; } public float Y { get; set; } }

/// One character's whole life in one world. The seed is set when the save is made and never changes.
public sealed class SaveData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Ver { get; set; } = ""; // the version that last wrote this save
    public AvatarData Avatar { get; set; } = new();
    public int Seed { get; set; } = 1;
    public int Gen { get; set; } = World.GenVer;
    public string Edits { get; set; } = "";
    public List<int> Gone { get; set; } = new();
    public List<DropData> Drops { get; set; } = new();
    public ItemStack[] Slots { get; set; } = new ItemStack[Bag.Size];
    public int Hand { get; set; } = -1;
    public float X { get; set; }
    public float Y { get; set; }
    public HashSet<string> Areas { get; set; } = new();
    public float Clock { get; set; } = .30f;
    public int Day { get; set; } = 1;
    public int Mined { get; set; }
    public int Placed { get; set; }
    public double Seconds { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Played { get; set; } = DateTime.UtcNow;
    public void Validate()
    {
        Avatar ??= new(); Avatar.Validate(); Avatar.Name = string.IsNullOrWhiteSpace(Avatar.Name) ? "Nobody" : Avatar.Name.Trim(); if (Avatar.Name.Length > 10) Avatar.Name = Avatar.Name[..10]; Areas ??= new(); Gone ??= new(); Drops ??= new(); Drops.RemoveAll(d => d == null || !Items.Valid(d.Id) || d.N <= 0 || !float.IsFinite(d.X) || !float.IsFinite(d.Y)); Edits ??= ""; Day = Math.Max(1, Day); Clock = float.IsFinite(Clock) ? M.Clamp(Clock, 0, .9999f) : .3f; if (!float.IsFinite(X) || !float.IsFinite(Y)) X = Y = 0;
        var s = Slots ?? []; if (s.Length != Bag.Size) Array.Resize(ref s, Bag.Size); for (int i = 0; i < s.Length; i++) { if (!Items.Valid(s[i].Id) || s[i].N <= 0) s[i] = default; else s[i].N = Math.Min(s[i].N, Bag.MaxStack); } Slots = s; if (Hand < -1 || Hand >= Bag.Size) Hand = -1; if (string.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString("N");
    }
}

/// Up to nine save files, each named after its one character.
public static class Saves
{
    public const int Max = 9;
    public sealed class Slot { public string File; public SaveData Data; public bool Newer; }
    /// This build's version, as players see it (MAJOR.MINOR.PATCH).
    public static string GameVer { get { var v = typeof(Saves).Assembly.GetName().Version; return v == null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}"; } }
    /// A save written by a newer version is never overwritten by an older one.
    static bool IsNewer(string ver) => Version.TryParse(ver, out var a) && Version.TryParse(GameVer, out var b) && a > b;
    static readonly JsonSerializerOptions O = new() { WriteIndented = true };
    public static string Dir => Path.Combine(Store.Dir, "saves");
    public static List<Slot> All = new();
    static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
    /// Names only ever contain letters, digits, spaces, _ and -, so they are safe as file names apart from a few Windows reserves.
    static string FileFor(string name) { var clean = new string(name.Trim().Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-').ToArray()).Trim(); if (clean.Length == 0) clean = "save"; if (reserved.Contains(clean)) clean += "_"; return Path.Combine(Dir, clean + ".json"); }
    static bool Locked => Store.ReadOnly && !Store.TestSaves;
    public static void Refresh()
    {
        Import(); All = new(); var seen = new HashSet<string>();
        try { if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir, "*.json")) { try { var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(f), O); if (d == null) continue; d.Validate(); if (!seen.Add(d.Id)) continue; /* a copied file is the same character, and a character lives in one save */ All.Add(new Slot { File = f, Data = d, Newer = IsNewer(d.Ver) }); } catch { } } } catch { }
        All = All.OrderBy(s => s.Data.Created).Take(Max).ToList();
    }
    public static bool Full => All.Count >= Max;
    /// A save being edited may keep its own name.
    public static bool NameTaken(string n, Slot own = null) { string f = FileFor(n); return All.Any(s => s != own && string.Equals(s.Data.Avatar.Name, n.Trim(), StringComparison.OrdinalIgnoreCase)) || (File.Exists(f) && !(own != null && string.Equals(Path.GetFullPath(f), Path.GetFullPath(own.File), StringComparison.OrdinalIgnoreCase))); }
    /// Changes how a save's character looks. If the name changed, the file is renamed to match.
    public static bool Edit(Slot s, AvatarData a)
    {
        if (NameTaken(a.Name, s)) return false; s.Data.Avatar = a.Clone(); s.Data.Validate(); string to = FileFor(s.Data.Avatar.Name), from = s.File; s.File = to; if (Locked) return true;
        if (!Write(s)) { s.File = from; return false; } try { if (!string.Equals(Path.GetFullPath(from), Path.GetFullPath(to), StringComparison.OrdinalIgnoreCase)) { if (File.Exists(from)) File.Delete(from); } else if (from != to) File.Move(from, to, true); } catch { }
        return true;
    }
    /// A seed no other save is using.
    static int NewSeed() { for (; ; ) { int s = Random.Shared.Next(1, int.MaxValue); if (All.All(q => q.Data.Seed != s)) return s; } }
    public static Slot Create(AvatarData a)
    {
        if (Full || NameTaken(a.Name)) return null; var d = new SaveData { Avatar = a.Clone(), Seed = NewSeed() }; d.Validate(); var s = new Slot { File = FileFor(d.Avatar.Name), Data = d }; All.Add(s); Write(s); return s;
    }
    public static bool Write(Slot s)
    {
        if (Locked || s.Newer) return false;
        // written beside the real file first, so a crash mid-save can never destroy a character
        try { Directory.CreateDirectory(Dir); Backup(s); s.Data.Ver = GameVer; File.WriteAllText(s.File + ".tmp", JsonSerializer.Serialize(s.Data, O)); File.Move(s.File + ".tmp", s.File, true); return true; } catch { return false; }
    }
    /// Keeps the last five copies of each character's save, at most one every ten minutes, in saves/backups.
    static void Backup(Slot s)
    {
        try
        {
            if (!File.Exists(s.File)) return; var dir = Path.Combine(Dir, "backups", s.Data.Id); Directory.CreateDirectory(dir); var have = Directory.GetFiles(dir, "*.json").OrderBy(f => f).ToList();
            if (have.Count > 0 && (DateTime.Now - File.GetLastWriteTime(have[^1])).TotalMinutes < 10) return;
            File.Copy(s.File, Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json"), true); have = Directory.GetFiles(dir, "*.json").OrderBy(f => f).ToList(); while (have.Count > 5) { File.Delete(have[0]); have.RemoveAt(0); }
        }
        catch { }
    }
    /// Removes a save from the game. The file is kept in a "deleted" folder rather than destroyed.
    public static void Delete(Slot s)
    {
        All.Remove(s); if (Locked) return; try { var bin = Path.Combine(Store.Dir, "deleted"); Directory.CreateDirectory(bin); File.Move(s.File, Path.Combine(bin, Path.GetFileNameWithoutExtension(s.File) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), true); } catch { }
    }
    /// Older versions had a single save.json in one shared world. Its character becomes the first save, once; the original file is left untouched.
    static void Import()
    {
        try
        {
            string old = Path.Combine(Store.Dir, "save.json"); if (Directory.Exists(Dir) || !File.Exists(old) || Locked) return; using var doc = JsonDocument.Parse(File.ReadAllText(old)); var r = doc.RootElement; if (!r.TryGetProperty("Created", out var cr) || cr.ValueKind != JsonValueKind.True) return;
            var d = new SaveData { Avatar = r.GetProperty("Avatar").Deserialize<AvatarData>(O), Seed = NewSeed() }; if (r.TryGetProperty("Day", out var dy) && dy.TryGetInt32(out int n)) d.Day = n; if (r.TryGetProperty("Clock", out var ck)) d.Clock = ck.GetSingle(); d.Validate(); Write(new Slot { File = FileFor(d.Avatar.Name), Data = d });
        }
        catch { }
    }
}

/// Items are blocks for now, so an item's id is simply its block.
public static class Items
{
    static readonly Dictionary<(int, int), Texture2D> tex = new();
    public static bool Valid(int id) => id > 0 && id < Blocks.All.Length && Blocks.Minable((T)id);
    public static string Name(int id) => Blocks.All[id].Name;
    /// Painted at the exact size it will be shown, so it is never a blurry scaled copy.
    public static Texture2D Tex(int id, int size) { if (!tex.TryGetValue((id, size), out var t)) tex[(id, size)] = t = Tiles.Chunk((T)id, size).ToTexture(); return t; }
    public static void Draw(int id, float cx, float cy, int size, float scale = 1, float rot = 0, float alpha = 1) { var t = Tex(id, size); float s = size * scale; Raylib.DrawTexturePro(t, new Rectangle(0, 0, size, size), new Rectangle(cx, cy, s, s), new(s / 2, s / 2), rot, new Color(255, 255, 255, (int)(255 * M.Clamp(alpha)))); }
}
public static class Bag
{
    public const int Cols = 12, Rows = 7, Size = Cols * Rows, MaxStack = 100;
    /// Adds items, filling existing stacks first. Returns how many did not fit.
    public static int Add(SaveData d, int id, int n)
    {
        for (int i = 0; i < Size && n > 0; i++) if (d.Slots[i].Id == id && !d.Slots[i].Empty) { int k = Math.Min(n, MaxStack - d.Slots[i].N); d.Slots[i].N += k; n -= k; }
        for (int i = 0; i < Size && n > 0; i++) if (d.Slots[i].Empty) { int k = Math.Min(n, MaxStack); d.Slots[i] = new(id, k); n -= k; }
        return n;
    }
    public static bool Fits(SaveData d, int id) => d.Slots.Any(s => s.Empty || (s.Id == id && s.N < MaxStack));
    public static int Count(SaveData d, int id) => d.Slots.Where(s => s.Id == id).Sum(s => s.N);
    public static int Used(SaveData d) => d.Slots.Count(s => !s.Empty);
    public static void Sort(SaveData d) { var t = d.Slots.Where(s => !s.Empty).GroupBy(s => s.Id).Select(g => (id: g.Key, n: g.Sum(s => s.N))).OrderBy(q => q.id).ToList(); int hand = d.Hand >= 0 ? d.Slots[d.Hand].Id : 0; Array.Clear(d.Slots); foreach (var (id, n) in t) Add(d, id, n); d.Hand = hand > 0 ? Array.FindIndex(d.Slots, s => s.Id == hand) : -1; }
}

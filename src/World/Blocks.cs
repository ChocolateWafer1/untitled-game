namespace UntitledGame;

public enum T : byte { Air, Dirt, Stone, Sand, Clay, Moss, Snow, Ice, Sandstone, RedRock, Loam, Mud, RoseEarth, VioletEarth, Sporesoil, Ash, Basalt, Chalk, Slate, MoonEarth, Crystal, Amberwood, Glowrock, Geode, Ember, Goldsoil, Gravel, Coral, Heartstone }
public enum Water { Lake, River, Cave, Sea, Spring }

/// What a block is called, how long it takes to dig by hand, how it sounds and whether it shines.
public sealed record BlockDef(string Name, string Text, int Color, float Time, string Surf, bool Grows = false, int Glow = 0, float GlowR = 0);
public static class Blocks
{
    public static readonly BlockDef[] All = [
        new("", "", 0, 0, "dirt"),
        new("Dirt", "Good, plain earth. Grass will creep onto it if there is some nearby.", 0x9a6a48, .72f, "dirt", true),
        new("Stone", "Grey, patient and everywhere.", 0x857f88, 1.9f, "stone"),
        new("Sand", "Every grain used to be a mountain.", 0xe3c98e, .48f, "sand"),
        new("Clay", "Cool and smooth. It keeps your fingerprints.", 0xa87058, .95f, "dirt"),
        new("Mossy Stone", "Soft on top, stubborn underneath.", 0x76806e, 1.6f, "stone"),
        new("Snow", "Squeaks when you press it.", 0xdde8f2, .34f, "snow"),
        new("Ice", "You can almost see through to the other side.", 0xa8d8ee, 1.15f, "stone"),
        new("Sandstone", "A desert, pressed flat and kept.", 0xd8a868, 1.35f, "stone"),
        new("Painted Rock", "Striped by ten thousand sunsets.", 0xb8603f, 1.5f, "stone"),
        new("Loam", "So rich that it smells like rain.", 0x5c4232, .66f, "dirt", true),
        new("Mud", "It does not want to let go of your hands.", 0x5f5344, .82f, "dirt", true),
        new("Rose Earth", "Pink from a hundred years of fallen petals.", 0xb87a78, .68f, "dirt", true),
        new("Violet Earth", "Smells faintly of lavender, even when dry.", 0x80648c, .68f, "dirt", true),
        new("Sporesoil", "Puffs a little when you poke it.", 0x74608a, .58f, "dirt", true),
        new("Ash", "Lighter than it looks.", 0x5e585e, .42f, "sand"),
        new("Basalt", "Cooled slowly, in no hurry at all.", 0x494550, 2.6f, "stone"),
        new("Chalk", "Leaves white palms.", 0xe4e0d0, 1.0f, "stone"),
        new("Slate", "Splits into neat, cold pages.", 0x6a7484, 2.2f, "stone"),
        new("Moon Earth", "Cool to the touch, like the far side of a pillow.", 0x47597a, .74f, "dirt", true),
        new("Crystal Rock", "Rings like a glass when you hit it.", 0x92bfe2, 2.9f, "stone", false, 0x9ad0ff, 70),
        new("Amberwood", "Root so old that it turned to honey.", 0xb8783a, 1.45f, "wood"),
        new("Glowrock", "Full of something small and cheerful.", 0x4e8f8e, 1.7f, "stone", false, 0x58e8d8, 120),
        new("Geode", "Plain outside. Not plain inside.", 0x9870c4, 2.5f, "stone", false, 0xb890ff, 64),
        new("Emberstone", "Still warm from the beginning of the world.", 0x8a4438, 3.2f, "stone", false, 0xff8438, 135),
        new("Gold Earth", "A whole autumn, swept under the rug.", 0xa8763c, .70f, "dirt", true),
        new("Gravel", "Pebbles that never agreed on anything.", 0x938a84, .6f, "sand"),
        new("Coral Stone", "Built by very small architects.", 0xe0a090, 1.2f, "stone"),
        new("Heartstone", "The bottom of the world. It hums.", 0x39304a, 0, "stone")];
    public static BlockDef Of(T t) => All[(int)t];
    public static bool Minable(T t) => t != T.Air && t != T.Heartstone;
    public static bool Grows(T t) => All[(int)t].Grows;
    public static bool Glows(T t) => All[(int)t].Glow != 0;
    /// The material a block sounds and breaks like.
    public static string Mat(T t) => t switch { T.Ice => "ice", T.Crystal => "crystal", T.Glowrock or T.Geode or T.Ember => "ore", T.Amberwood => "wood", T.Snow => "snow", _ => All[(int)t].Surf switch { "sand" => "sand", "dirt" => "dirt", "wood" => "wood", "snow" => "snow", _ => "stone" } };
}

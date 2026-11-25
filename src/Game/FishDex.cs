namespace UnnamedGame;

public enum Water { Lake, River, Cave, Sea }
[Flags] public enum When { Dawn = 1, Day = 2, Dusk = 4, Night = 8, Any = 15, Light = 7, Low = 13 }
public enum Sky { Any, Clear, Rain, Mist, Storm }
public enum Move { Steady, Darter, Diver, Jumper, Drifter, Thrasher, Circler }
public enum Body { Slim, Round, Long, Flat, Deep, Eel, Ray, Squid, Star, Angler, Tail, Sword }

public sealed record FishDef(string Name, Water W, When T, Sky S, float MinKg, float MaxKg, float MinCm, float MaxCm, int Rarity, float Hard, Move M, int Price, Body B, int A, int C, int D, int Bait, string Text)
{
    public bool Legend => Rarity == 5;
}

public static class FishDex
{
    public static readonly string[] BaitNames = ["Bare Hook", "Worm", "Cricket", "Meadow Berry", "Autumn Acorn", "Glow Cap", "Shore Snail", "Pine Grub", "Bread Ball", "Shrimp", "Firefly", "Golden Grub"];
    public static readonly int[] BaitPrice = [0, 3, 4, 0, 0, 0, 0, 0, 2, 6, 0, 40];
    public static readonly string[] BaitText = ["No bait. Patience required.", "A dependable wiggler. Most fish like it.", "Chirpy. Surface feeders love it.", "Sweet berry foraged from bushes.", "Nutty and buoyant, from the autumn wood.", "A glowing mushroom cap from the caves.", "Found on the beach after the tide.", "Dug out of highland pine bark.", "Cheap and cheerful.", "Salty. Sea fish come running.", "Catch one in the meadow at night.", "Legendary fish cannot resist it."];
    public static readonly string[] RodNames = ["Twig Rod", "Bamboo Rod", "Oak Caster", "Lakeglass Rod", "Moonlit Rod"];
    public static readonly int[] RodPrice = [0, 120, 420, 1100, 2800];
    public static readonly string[] RodText = ["A stick with ambition.", "Springy and forgiving.", "Casts further, reels harder.", "A calm rod that hides line tension.", "Feels like the fish wants to be caught."];
    public static readonly float[] RodPower = [1f, 1.14f, 1.3f, 1.46f, 1.7f];
    public static readonly float[] RodLine = [1f, 1.18f, 1.36f, 1.6f, 1.95f];
    public static readonly float[] RodCast = [1f, 1.15f, 1.35f, 1.5f, 1.7f];
    public static readonly string[] LureNames = ["Plain Hook", "Feather Spinner", "Copper Spoon", "Bell Float", "Deep Sinker", "Moon Lure"];
    public static readonly int[] LurePrice = [0, 60, 150, 240, 380, 900];
    public static readonly string[] LureText = ["Just a hook.", "Fish bite sooner.", "Attracts larger fish.", "A longer moment to set the hook.", "Reaches rarer deep fish.", "Rare fish at night."];

    public static readonly FishDef[] All = [
        // lake
        new("Pondskipper", Water.Lake, When.Any, Sky.Any, .05f, .3f, 6, 14, 1, .05f, Move.Steady, 4, Body.Slim, 0x9fb7c4, 0xe6e2cf, 0x4d6b7a, 1, "Tiny, polite and always first to the hook."),
        new("Butter Perch", Water.Lake, When.Light, Sky.Any, .2f, 1.1f, 14, 30, 1, .15f, Move.Steady, 8, Body.Deep, 0xe0b650, 0xf6ead0, 0x8a5a38, 1, "Golden and round as a warm bread roll."),
        new("Reed Bream", Water.Lake, When.Any, Sky.Any, .4f, 2.4f, 20, 42, 1, .2f, Move.Drifter, 11, Body.Deep, 0x9a9a6a, 0xe8e0bc, 0x5a5a3a, 8, "Dozes among the reeds for most of its life."),
        new("Copper Carp", Water.Lake, When.Any, Sky.Any, 1.5f, 9, 34, 80, 2, .38f, Move.Circler, 24, Body.Round, 0xc77d45, 0xf0cfa0, 0x7a4028, 8, "Pulls like a wheelbarrow, argues like one too."),
        new("Lily Tetra", Water.Lake, When.Day, Sky.Clear, .02f, .12f, 4, 8, 2, .18f, Move.Darter, 14, Body.Slim, 0xd86f86, 0xf6d3d0, 0x5f8a52, 3, "Hides beneath lily pads and blushes."),
        new("Mirror Trout", Water.Lake, When.Dawn | When.Dusk, Sky.Any, .8f, 4.2f, 28, 60, 3, .45f, Move.Jumper, 38, Body.Slim, 0xc8d6da, 0xf4f4ee, 0xd98a7a, 2, "Its scales show you the sky."),
        new("Grandpa Catfish", Water.Lake, When.Night, Sky.Any, 4, 22, 60, 130, 3, .6f, Move.Diver, 70, Body.Long, 0x6b5a4a, 0xd0c0a0, 0x3a2c24, 1, "Whiskered, wise and very much asleep by day."),
        new("Rainbow Dapple", Water.Lake, When.Light, Sky.Rain, .6f, 3, 24, 52, 3, .5f, Move.Jumper, 52, Body.Slim, 0x7fa8c9, 0xf0dca8, 0xd86f86, 2, "Only surfaces to taste the rain."),
        new("Mist Pike", Water.Lake, When.Dawn, Sky.Mist, 3, 14, 60, 118, 4, .72f, Move.Darter, 110, Body.Long, 0x8fa89a, 0xe0e8dc, 0x4d6a5a, 2, "Glides through fog as though it were water."),
        new("Old Lantern", Water.Lake, When.Night, Sky.Clear, 26, 58, 130, 210, 5, .96f, Move.Thrasher, 620, Body.Angler, 0x35496b, 0xe8b650, 0x2b2a30, 11, "LEGENDARY. The light the village was named for."),
        // river
        new("Pebble Minnow", Water.River, When.Any, Sky.Any, .02f, .1f, 4, 9, 1, .06f, Move.Darter, 4, Body.Slim, 0xb0a58f, 0xf0e8d4, 0x6b5f4a, 1, "Looks exactly like a pebble until it does not."),
        new("Brook Dace", Water.River, When.Light, Sky.Any, .1f, .5f, 10, 22, 1, .16f, Move.Darter, 8, Body.Slim, 0x8fb5a0, 0xeef0e0, 0x4d7a62, 2, "Quick, bright and slightly smug."),
        new("Maple Trout", Water.River, When.Any, Sky.Any, .4f, 2.6f, 20, 46, 2, .32f, Move.Jumper, 20, Body.Slim, 0xd8793f, 0xf6dfb0, 0x8c2f35, 4, "Turns the colour of the falling leaves."),
        new("Acorn Goby", Water.River, When.Dusk | When.Night, Sky.Any, .08f, .4f, 7, 15, 2, .24f, Move.Diver, 15, Body.Round, 0x8a5a38, 0xd8b080, 0x4a3428, 4, "Wears a little brown cap."),
        new("Silver Salmon", Water.River, When.Dawn | When.Dusk, Sky.Any, 2.5f, 11, 50, 95, 3, .56f, Move.Jumper, 62, Body.Long, 0xb9c6cc, 0xf4efe6, 0xd07a6a, 9, "Leaps for the joy of it. And to annoy you."),
        new("Ribbon Eel", Water.River, When.Night, Sky.Any, .8f, 4, 50, 110, 3, .55f, Move.Circler, 55, Body.Eel, 0x3a4f36, 0x9aa870, 0x22301f, 1, "Ties itself in bows when nervous."),
        new("Storm Barbel", Water.River, When.Any, Sky.Storm, 3, 12, 55, 100, 4, .74f, Move.Thrasher, 120, Body.Long, 0x4e4c62, 0xb0b4cc, 0xe0b650, 7, "Its whiskers crackle before thunder."),
        new("Amber Sturgeon", Water.River, When.Dusk, Sky.Clear, 18, 60, 110, 220, 4, .82f, Move.Diver, 190, Body.Sword, 0xb5783a, 0xe8c890, 0x5a3a26, 7, "Old when the bridge was young."),
        new("King Autumn", Water.River, When.Dawn | When.Dusk, Sky.Any, 30, 72, 130, 200, 5, .95f, Move.Jumper, 640, Body.Long, 0xc65a3a, 0xf0c060, 0x8c2f35, 11, "LEGENDARY. Wears a crown of red fins."),
        // cave
        new("Blindfin", Water.Cave, When.Any, Sky.Any, .05f, .3f, 6, 14, 1, .12f, Move.Drifter, 9, Body.Slim, 0xe8d8d0, 0xf8f0ea, 0xd0a8a0, 5, "Never seen the sun and has no plans to."),
        new("Glowtail", Water.Cave, When.Any, Sky.Any, .1f, .6f, 8, 20, 2, .26f, Move.Darter, 22, Body.Slim, 0x3f8a8a, 0x9af0d8, 0x1f4a52, 5, "Handy if you have dropped your keys."),
        new("Stone Loach", Water.Cave, When.Any, Sky.Any, .3f, 1.4f, 14, 32, 2, .34f, Move.Diver, 24, Body.Eel, 0x7a7480, 0xc0bac8, 0x4a4650, 1, "Sleeps under rocks it has named."),
        new("Crystal Char", Water.Cave, When.Any, Sky.Any, 1, 5, 30, 64, 3, .52f, Move.Circler, 64, Body.Slim, 0x9ac0e0, 0xe8f4fc, 0x6a7ac0, 5, "You can almost read a book through it."),
        new("Mushroom Puffer", Water.Cave, When.Any, Sky.Any, .5f, 2, 14, 28, 3, .46f, Move.Drifter, 58, Body.Round, 0xc65a5a, 0xf6ead8, 0xf0e8da, 5, "Puffs up, then looks embarrassed."),
        new("Gloom Salamander", Water.Cave, When.Night, Sky.Any, 2, 8, 40, 80, 4, .7f, Move.Thrasher, 130, Body.Long, 0x3a3048, 0xe0a050, 0x221a2c, 10, "Technically not a fish. Do not tell it."),
        new("The Deep Dreamer", Water.Cave, When.Night, Sky.Any, 40, 90, 160, 260, 5, .98f, Move.Diver, 760, Body.Angler, 0x2a3a5a, 0x80f0e0, 0x141c30, 11, "LEGENDARY. Hums in its sleep. The cave hums back."),
        // sea
        new("Sand Sprat", Water.Sea, When.Any, Sky.Any, .03f, .2f, 6, 14, 1, .08f, Move.Darter, 5, Body.Slim, 0xd8c8a0, 0xf6f0e0, 0x9a8a62, 6, "Travels in crowds, gossips constantly."),
        new("Blue Mackerel", Water.Sea, When.Light, Sky.Any, .3f, 1.6f, 20, 42, 1, .24f, Move.Darter, 13, Body.Slim, 0x4a82b0, 0xe8eef0, 0x24466a, 9, "Striped like a favourite jumper."),
        new("Sunny Flounder", Water.Sea, When.Any, Sky.Any, .4f, 2.8f, 22, 50, 2, .3f, Move.Diver, 22, Body.Flat, 0xc9a96a, 0xefe0b8, 0x8a6a3c, 6, "Both eyes on you."),
        new("Coral Wrasse", Water.Sea, When.Day, Sky.Any, .3f, 1.4f, 16, 34, 2, .34f, Move.Circler, 28, Body.Deep, 0xe58a6a, 0x6ac0b8, 0x9c4a7a, 9, "Dressed for a party nobody mentioned."),
        new("Starry Ray", Water.Sea, When.Night, Sky.Clear, 2, 9, 40, 90, 3, .5f, Move.Drifter, 66, Body.Ray, 0x35496b, 0xd8dcf0, 0xf0e070, 6, "A map of the sky on its back."),
        new("Dusk Squid", Water.Sea, When.Dusk | When.Night, Sky.Any, .5f, 3, 24, 60, 3, .48f, Move.Darter, 54, Body.Squid, 0xd08aa0, 0xf6dce0, 0x8a4a6a, 9, "Changes colour when complimented."),
        new("Harbour Bass", Water.Sea, When.Low, Sky.Any, 2, 8, 44, 80, 3, .54f, Move.Thrasher, 60, Body.Long, 0x7a8a94, 0xe8e8e0, 0x3f4a54, 9, "Loiters by docks, looking for trouble."),
        new("Rain Tuna", Water.Sea, When.Any, Sky.Rain, 20, 80, 90, 190, 4, .78f, Move.Circler, 180, Body.Round, 0x3f5a7a, 0xd8e0e8, 0xe0b650, 9, "Races the squall, usually wins."),
        new("Gale Swordfish", Water.Sea, When.Light, Sky.Storm, 40, 120, 160, 300, 4, .86f, Move.Jumper, 260, Body.Sword, 0x4a5a8a, 0xe0e4f0, 0x2a3358, 9, "Duels the lightning."),
        new("Moon Turtlefish", Water.Sea, When.Night, Sky.Clear, 8, 30, 60, 110, 4, .7f, Move.Drifter, 170, Body.Round, 0x6f9a8a, 0xe8f0d8, 0x3f6a5a, 10, "Half turtle by rumour, all fish by law."),
        new("Morning Whalet", Water.Sea, When.Dawn, Sky.Any, 60, 180, 180, 340, 5, .97f, Move.Diver, 820, Body.Tail, 0x5a7a9a, 0xe8eef0, 0x3a526a, 11, "LEGENDARY. The smallest whale that is secretly a fish."),
        new("Tidefin Darling", Water.Sea, When.Day, Sky.Clear, .4f, 1.8f, 18, 40, 2, .36f, Move.Jumper, 30, Body.Deep, 0xf0a0b0, 0xfff0e8, 0xe0b650, 9, "Waves back if you wave first."),
        new("Boot Crab", Water.Sea, When.Any, Sky.Any, .3f, 1.2f, 10, 22, 1, .2f, Move.Steady, 12, Body.Star, 0xc65a3a, 0xf0c090, 0x8c2f35, 6, "Lives in a boot. The boot is not for sale."),
        new("Willow Koi", Water.Lake, When.Day, Sky.Any, 1.2f, 6, 30, 70, 3, .42f, Move.Circler, 60, Body.Round, 0xf4ece0, 0xe0603a, 0x2b2a30, 8, "Every pattern is different. This one is yours."),
    ];
    public static (float kg, float cm) Roll(FishDef f, Rng r, float bonus)
    {
        float q = M.Clamp(MathF.Pow(r.F(), 1.7f - bonus) * (1 + bonus * .1f)); return (M.Lerp(f.MinKg, f.MaxKg, q * q * .7f + q * .3f), M.Lerp(f.MinCm, f.MaxCm, q));
    }
    public static int Value(FishItem f) { var d = All[f.Id]; return Math.Max(1, (int)(d.Price * (.75f + .75f * M.Clamp((f.Cm - d.MinCm) / (d.MaxCm - d.MinCm))))); }

    /// Draws a fish facing right, about 100 units long, centred at 0,0.
    public static void Draw(Painter p, FishDef f, float scale, float ox, float oy, float t = 0, bool shadow = false)
    {
        p.View(scale, ox, oy); Col A = f.A, B = f.C, D = f.D; if (shadow) { A = B = D = 0x15141c; }
        var V = (float x, float y) => new System.Numerics.Vector2(x, y); float w = M.Sin(t * 6) * 5;
        Func<float, float, Col> body = (x, y) => { float v = y / 24; var c = v > .28f ? B : v > .05f ? Col.Lerp(A, B, .5f) : A; int hx = (int)MathF.Floor(x / 6), hy = (int)MathF.Floor(y / 6); if (f.Name.Contains("Koi")) return M.Noise(x * .04f, y * .06f, 3) > .12f ? f.C : M.Noise(x * .06f, y * .07f, 8) > .35f ? D : A; if (v < .1f && M.Hash(hx, hy, f.Price) > .82f) c = Col.Lerp(c, D, .7f); return c; };
        var k = new Ink(shadow ? (x, y) => A : body, 15, .55f, shadow ? 0 : 1) { Ao = 0, Tone = shadow ? null : (x, y) => (((int)MathF.Floor(x / 5) + (int)MathF.Floor(y / 4)) & 1) == 0 ? .04f : -.03f };
        var fin = new Ink(shadow ? A : Col.Lerp(A, D, .65f), 6, .5f, shadow ? 0 : 1) { Ao = 0 };
        void Fin(params System.Numerics.Vector2[] q) { p.Begin().Blob(q); p.Shade(fin); if (!shadow && scale > .35f) for (int i = 1; i < q.Length - 1; i++) p.Line(q[0], q[i], 1.4f, D.Shade(-.2f), true, .5f); }
        float eyeX = 34, eyeY = -5, er = 5;
        switch (f.B)
        {
            case Body.Eel: { var q = new System.Numerics.Vector2[12]; var r = new float[12]; for (int i = 0; i < 12; i++) { float u = i / 11f; q[i] = V(-52 + u * 100, M.Sin(u * 7 + t * 5) * 9 * (1 - u * .4f)); r[i] = M.Lerp(2, 9, MathF.Sqrt(u)); } p.Begin().Tube(q, r); p.Shade(k); for (int i = 2; i < 9; i++) p.Line(q[i] + V(0, -r[i]), q[i] + V(-4, -r[i] - 5), 3, D); eyeX = q[11].X - 1; eyeY = q[11].Y - 3; er = 3.2f; break; }
            case Body.Ray: p.Begin().Capsule(V(-20, 0), 2.5f, V(-58, w), 1); p.Shade(fin); p.Begin().Blob(V(46, 0), V(18, -30 - w), V(-12, -38 - w * 1.6f), V(-28, -6), V(-28, 6), V(-12, 34 + w), V(18, 26)); p.Shade(k); for (int i = 0; i < 7 && !shadow; i++) { p.Begin().Ellipse(M.Hash(i, 2) * 44 - 20, M.Hash(i, 4) * 34 - 18, 2, 2); p.Flat(D); } eyeX = 32; eyeY = -8; break;
            case Body.Squid: for (int i = 0; i < 6; i++) p.Begin().Capsule(V(10, -10 + i * 4), 3.4f, V(52, -18 + i * 7 + M.Sin(t * 5 + i) * 4), 1.4f).Shade(fin); p.Begin().Blob(V(-50, 0), V(-30, -16), V(4, -15), V(20, 0), V(4, 15), V(-30, 16)); p.Shade(k); Fin(V(-34, 0), V(-54, -20), V(-24, -16)); Fin(V(-34, 0), V(-54, 20), V(-24, 16)); eyeX = 12; eyeY = -3; break;
            case Body.Star: for (int i = 0; i < 3; i++) for (int s = -1; s <= 1; s += 2) { p.Begin().Capsule(V(s * 16, 4 + i * 3), 3.4f, V(s * (30 + i * 4), 10 + i * 7), 2.6f).Capsule(V(s * (30 + i * 4), 10 + i * 7), 2.6f, V(s * (32 + i * 5), 24 + i * 3), 1.4f); p.Shade(fin); } for (int s = -1; s <= 1; s += 2) { p.Begin().Capsule(V(s * 20, -4), 4, V(s * 36, -18), 5).Ellipse(s * 40, -24, 9, 9).Cut(V(s * 40, -24), V(s * 46, -38), V(s * 34, -38)); p.Shade(k); } p.Begin().Blob(V(-28, 0), V(-18, -18), V(18, -18), V(28, 0), V(18, 16), V(-18, 16)); p.Shade(k); for (int s = -1; s <= 1; s += 2 ) { p.Begin().Ellipse(s * 8, -20, 5, 6); p.Shade(new Ink(0xfaf4ea, 2, .4f)); p.Begin().Ellipse(s * 8 + 1, -19, 2.2f, 3); p.Flat(0x1a1014); } return;
            case Body.Flat: Fin(V(-10, -10), V(-34, -34), V(10, -38), V(34, -20)); Fin(V(-10, 10), V(-34, 32), V(10, 34), V(34, 18)); Fin(V(-38, 0), V(-56, -14 + w), V(-50, 0), V(-56, 14 + w)); p.Begin().Blob(V(46, 2), V(24, -20), V(-12, -24), V(-42, -3), V(-42, 3), V(-12, 24), V(24, 20)); p.Shade(k); eyeX = 30; eyeY = -10; break;
            default:
                {
                    (float h, float l, float tail) s = f.B switch { Body.Round => (25, 42, 18), Body.Deep => (28, 40, 18), Body.Long => (15, 50, 18), Body.Angler => (28, 42, 16), Body.Tail => (24, 48, 24), Body.Sword => (14, 46, 22), _ => (16, 46, 17) };
                    Fin(V(-s.l + 8, 0), V(-s.l - 12, -s.tail + w), V(-s.l - 5, w), V(-s.l - 12, s.tail + w));
                    Fin(V(8, -s.h + 4), V(4, -s.h - 14), V(-18, -s.h - 10), V(-24, -s.h * .55f)); Fin(V(-8, s.h - 4), V(-12, s.h + 9), V(-24, s.h + 6), V(-24, s.h * .5f));
                    if (f.Name == "King Autumn") for (int i = 0; i < 4; i++) Fin(V(28 - i * 11, -s.h + 4), V(30 - i * 11, -s.h - 17), V(20 - i * 11, -s.h + 3));
                    p.Begin().Blob(V(s.l, 1), V(s.l * .72f, -s.h * .62f), V(4, -s.h), V(-s.l * .6f, -s.h * .45f), V(-s.l, -4), V(-s.l, 4), V(-s.l * .6f, s.h * .42f), V(4, s.h), V(s.l * .72f, s.h * .6f));
                    if (f.B == Body.Sword) p.Poly(V(s.l * .7f, -5), V(s.l + 24, -1), V(s.l * .8f, 3));
                    p.Shade(k);
                    Fin(V(10, 4), V(-4, 14 + w * .6f), V(-12, 6 + w * .4f)); p.Curve(V(s.l * .52f, -s.h * .6f), V(s.l * .36f, 0), V(s.l * .52f, s.h * .55f), 1.6f, shadow ? A : A.Shade(-.5f), true);
                    if (f.B == Body.Long && f.Name.Contains("Catfish") || f.Name.Contains("Barbel")) for (int i = 0; i < 2; i++) p.Curve(V(s.l - 3, 4), V(s.l + 8, 8 + i * 6), V(s.l + 2, 18 + i * 5), 1.4f, D, false);
                    if (f.B == Body.Angler) { p.Curve(V(22, -s.h + 3), V(40, -s.h - 22), V(s.l + 8, -s.h - 2), 1.8f, shadow ? A : D.Shade(.1f), false); p.Begin().Ellipse(s.l + 8, -s.h + 1, 5.5f, 5.5f); p.Shade(new Ink(shadow ? A : 0xfff0a0, 3, .3f)); for (int i = 0; i < 4 && !shadow; i++) p.Begin().Poly(V(s.l - 6 - i * 5, 8), V(s.l - 2 - i * 5, 8), V(s.l - 4 - i * 5, 14)).Flat(0xfaf4ea); }
                    eyeX = s.l * .68f; eyeY = -s.h * .18f; if (!shadow) p.Curve(V(s.l - 1, 4), V(s.l - 6, 8), V(s.l - 11, 7), 1.4f, D.Shade(-.5f), false); break;
                }
        }
        if (shadow) return;
        p.Begin().Ellipse(eyeX, eyeY, er, er); p.Shade(new Ink(0xfaf4ea, 2, .3f) { Ao = 0 }); p.Begin().Ellipse(eyeX + 1, eyeY, er * .55f, er * .6f); p.Flat(0x1a1014); p.Dot(eyeX - .6f, eyeY - 1.8f, 0xffffff);
    }
}

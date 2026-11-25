using System.Numerics;
namespace UntitledGame;

public enum Neckline { Crew, V, Scoop, Collar, Turtle, Mock, Boat, Henley, Hood, OffShoulder, Tank, Polo, Wrap, Sailor, Cowl }
public enum Knit { Flat, Rib, Cable, Fleece, Flannel, Denim, Lace }
public enum Graphic { None, Star, Heart, Fish, Sun, Mountain, Stripe, Pocket, Letter, Leaf, Wave, Mushroom, Moon, Yoke, TwoStripe, Fair, Ruffle, Patch }
public sealed record TopDef(string Name, float Sleeve, Neckline Neck, float Fit, float Hem, Knit Knit = Knit.Flat, Graphic G = Graphic.None, bool RibCuff = false, bool Raglan = false, bool Buttons = false, bool Flare = false, bool Tuck = false, bool ContrastSleeve = false, bool Puff = false);

public enum Lapel { None, Notch, Shawl, Stand, Shearling, Hood, Bomber, Camp, Peter, Fur, Sailor }
public enum Close { Zip, Buttons, Double, Toggles, Open, Snaps, Belt }
public sealed record JacketDef(string Name, float Sleeve, float Fit, float Hem, float Open, Lapel Lapel, Close Close, int Pockets = 0, Knit Knit = Knit.Flat, bool RibHem = false, bool Quilt = false, bool Belt = false, bool Fringe = false, bool ContrastSleeve = false, bool Stripe = false, bool Patch = false, bool Cape = false, bool ElbowPatch = false, bool Yoke = false);

public sealed record PantsDef(string Name, float Len, float Thigh, float Knee, float Hem, Knit Knit = Knit.Flat, bool Cargo = false, bool Stripe = false, bool Cuff = false, bool RibCuff = false, int Rips = 0, bool Belt = false, bool Draw = false, bool Bib = false, bool Pleat = false, bool KneePatch = false, bool Seam = true, bool Flap = false, bool Scallop = false, bool ColorBlock = false);
public enum SkirtKind { Plain, Pleated, Tiered, Wrap, Buttons, Ruffle, Pencil, Tulle, Panel, Apron, Petal, Kilt }
public sealed record SkirtDef(string Name, float Len, float Flare, SkirtKind Kind, bool Belt = false, bool Trim = false, bool Pockets = false, bool Straps = false);
public enum ShoeKind { Sneaker, HighTop, Boot, Chelsea, Sandal, Loafer, Slipper, Rain, Hiking, Platform, Clog, Flat, Cowboy, Fur, Runner, MaryJane, Slide, Combat, Oxford, Skate }
public sealed record ShoeDef(string Name, ShoeKind K, float Shaft, float Sole, bool Laces = false, bool Toe = false, bool Stripe = false, bool Fur = false, bool Strap = false, bool Bow = false);
public enum HatKind { None, Beanie, PomBeanie, Cap, CapBack, Bucket, Wide, Fedora, Cowboy, Beret, Top, Headband, Crown, Flower, Phones, Trapper, Witch, Straw, Visor, Bandana, Party, Frog, CatBeanie, Sailor, Newsboy, Bowler, Chef, Safari, Earmuffs, Bow, Pirate, Propeller, SunHat }
public sealed record HatDef(string Name, HatKind K, bool Cover, float Flat, float Brim);
public enum Fringe { None, Blunt, Curtain, Side, Messy, Spiky, Quiff, Slick, Middle, Emo, Baby, Wispy, Crop, Puff, Pomp }
public enum Updo { None, HighPony, LowPony, Bun, TopKnot, SpaceBuns, Pigtails, HalfUp, Mohawk, LowBun, BraidTails, SidePony, ManBun, AfroPuffs, CrownBraid }
/// Tex: 0 straight 1 wavy 2 curly 3 coily 4 braids 5 locs. Lengths are in head-heights from the head centre.
public sealed record HairDef(string Name, int Tex, float Top, float SideVol, float SideLen, float BackLen, float BackW, Fringe F, Updo U = Updo.None, float Fade = 0, float Flat = .6f);

public static class Catalog
{
    public static readonly TopDef[] Tops = [
        new("Plain Tee", .42f, Neckline.Crew, .9f, 3.5f), new("Pocket Tee", .42f, Neckline.Crew, 1.1f, 4, G: Graphic.Pocket), new("Striped Breton", 1, Neckline.Boat, .9f, 4, G: Graphic.Stripe), new("Baseball Raglan", .68f, Neckline.Crew, 1, 4, Raglan: true),
        new("Star Tee", .42f, Neckline.Crew, 1.2f, 4.5f, G: Graphic.Star), new("Flannel Shirt", 1, Neckline.Collar, 1.2f, 5, Knit.Flannel, Buttons: true), new("Camp Shirt", .45f, Neckline.Collar, 1.4f, 4, Buttons: true), new("Polo", .42f, Neckline.Polo, .9f, 3.5f, RibCuff: true),
        new("Henley", 1, Neckline.Henley, .8f, 4, RibCuff: true), new("Turtleneck", 1, Neckline.Turtle, .5f, 3, Knit.Rib), new("Cable Sweater", 1, Neckline.Crew, 1.6f, 4, Knit.Cable, RibCuff: true), new("Chunky Cowl Knit", 1, Neckline.Cowl, 1.9f, 5, Knit.Rib, RibCuff: true),
        new("Fair Isle Jumper", 1, Neckline.Crew, 1.4f, 4, Knit.Rib, Graphic.Fair, RibCuff: true), new("Hoodie", 1, Neckline.Hood, 1.7f, 4.5f, Knit.Fleece, Graphic.Pocket, RibCuff: true), new("Fish Hoodie", 1, Neckline.Hood, 1.9f, 5, Knit.Fleece, Graphic.Fish, RibCuff: true), new("Crewneck Sweat", 1, Neckline.Crew, 1.5f, 4, Knit.Fleece, Graphic.Letter, RibCuff: true),
        new("Tank Top", 0, Neckline.Tank, .5f, 3), new("Muscle Tee", .12f, Neckline.Crew, 1.1f, 4), new("Crop Tee", .42f, Neckline.Crew, .8f, -3), new("Off-Shoulder Knit", 1, Neckline.OffShoulder, 1.4f, 2, Knit.Rib, Flare: true),
        new("Wrap Blouse", .7f, Neckline.Wrap, .7f, 2, Tuck: true, Flare: true), new("Ruffle Blouse", 1, Neckline.V, 1, 2, G: Graphic.Ruffle, Tuck: true, Puff: true, RibCuff: true), new("Sailor Shirt", .45f, Neckline.Sailor, 1, 3), new("Peasant Top", .75f, Neckline.Scoop, 1.4f, 5, Flare: true, Puff: true),
        new("V-Neck Sweater", 1, Neckline.V, 1.2f, 3.5f, Knit.Rib, RibCuff: true, G: Graphic.TwoStripe), new("Mock Neck Tee", .42f, Neckline.Mock, 1, 3.5f), new("Mountain Long Sleeve", 1, Neckline.Crew, 1, 4, G: Graphic.Mountain, ContrastSleeve: true), new("Mushroom Tee", .45f, Neckline.Crew, 1.4f, 5, G: Graphic.Mushroom),
        new("Western Shirt", 1, Neckline.Collar, .9f, 3, G: Graphic.Yoke, Buttons: true, Tuck: true), new("Tunic", .6f, Neckline.V, 1.2f, 11, Flare: true), new("Wave Rash Top", 1, Neckline.Mock, .35f, 3, G: Graphic.Wave), new("Leaf Knit Vest", 0, Neckline.V, 1.1f, 3, Knit.Cable, Graphic.Leaf, RibCuff: true),
    ];
    public static readonly JacketDef[] Jackets = [
        new("None", 0, 0, 0, 1, Lapel.None, Close.Open),
        new("Denim Jacket", 1, 1.6f, 3, .35f, Lapel.Camp, Close.Buttons, 2, Knit.Denim, Yoke: true), new("Bomber", 1, 2.2f, 2, .3f, Lapel.Bomber, Close.Zip, 1, RibHem: true, Patch: true), new("Varsity", 1, 2.2f, 2.5f, .35f, Lapel.Bomber, Close.Snaps, 1, RibHem: true, ContrastSleeve: true, Patch: true), new("Puffer", 1, 3.2f, 3, .25f, Lapel.Stand, Close.Zip, 1, Quilt: true),
        new("Puffer Vest", 0, 2.8f, 3, .3f, Lapel.Stand, Close.Zip, 1, Quilt: true), new("Raincoat", 1, 2.4f, 12, .06f, Lapel.Hood, Close.Snaps, 1), new("Trench Coat", 1, 2, 18, .25f, Lapel.Notch, Close.Double, 1, Belt: true), new("Peacoat", 1, 2, 7, .15f, Lapel.Notch, Close.Double, 1),
        new("Duffle Coat", 1, 2.4f, 10, .12f, Lapel.Hood, Close.Toggles, 2), new("Blazer", 1, 1.3f, 5, .35f, Lapel.Notch, Close.Buttons, 1), new("Tweed Jacket", 1, 1.6f, 6, .3f, Lapel.Notch, Close.Buttons, 2, Knit.Flannel, ElbowPatch: true), new("Cardigan", 1, 1.6f, 6, .45f, Lapel.None, Close.Buttons, 1, Knit.Rib, RibHem: true),
        new("Long Cardigan", 1, 1.8f, 17, .6f, Lapel.Shawl, Close.Open, 1, Knit.Cable), new("Shearling Aviator", 1, 2.6f, 3, .3f, Lapel.Shearling, Close.Zip, 1), new("Leather Moto", 1, 1.4f, 2, .3f, Lapel.Notch, Close.Zip, 1, Belt: true), new("Track Jacket", 1, 1.5f, 3, .2f, Lapel.Stand, Close.Zip, 1, RibHem: true, Stripe: true),
        new("Zip Hoodie", 1, 1.8f, 4, .4f, Lapel.Hood, Close.Zip, 1, Knit.Fleece, RibHem: true), new("Flannel Overshirt", 1, 1.9f, 6, .55f, Lapel.Camp, Close.Open, 2, Knit.Flannel), new("Utility Vest", 0, 1.8f, 3, .35f, Lapel.None, Close.Zip, 4), new("Parka", 1, 3, 13, .12f, Lapel.Fur, Close.Zip, 2),
        new("Poncho", .6f, 4.5f, 8, 0, Lapel.None, Close.Open, 0, Knit.Rib, Fringe: true, Cape: true), new("Suede Fringe", 1, 1.6f, 4, .5f, Lapel.Camp, Close.Open, 0, Fringe: true, Yoke: true), new("Kimono Robe", .85f, 3, 16, .4f, Lapel.Shawl, Close.Open, Belt: true), new("Cape", .4f, 5, 14, .5f, Lapel.Peter, Close.Buttons, Cape: true),
        new("Windbreaker", 1, 2.2f, 4, .1f, Lapel.Stand, Close.Zip, 1, Stripe: true, ContrastSleeve: true), new("Corduroy Jacket", 1, 1.7f, 4, .4f, Lapel.Shearling, Close.Buttons, 2, Knit.Rib), new("Sweater Vest", 0, 1.2f, 3, .0f, Lapel.None, Close.Open, 0, Knit.Cable, RibHem: true), new("Sailor Jacket", 1, 1.5f, 2, .1f, Lapel.Sailor, Close.Double),
        new("Military Coat", 1, 1.7f, 11, .05f, Lapel.Stand, Close.Double, 2, Belt: true), new("Letterman Coat", 1, 2.4f, 9, .4f, Lapel.Stand, Close.Snaps, 1, ContrastSleeve: true, Patch: true),
    ];
    public static readonly PantsDef[] Pants = [
        new("Straight Jeans", 1, .8f, .9f, 1.2f, Knit.Denim, Belt: false), new("Skinny Jeans", .98f, .2f, .2f, .3f, Knit.Denim), new("Baggy Jeans", 1.03f, 1.9f, 2.4f, 2.8f, Knit.Denim), new("Bootcut", 1.03f, .6f, .6f, 2.4f, Knit.Denim), new("Flared Pants", 1.04f, .4f, .6f, 3.8f),
        new("Chinos", 1, .8f, .8f, .9f, Belt: true), new("Cargo Pants", 1, 1.5f, 1.7f, 1.6f, Cargo: true), new("Joggers", .95f, 1.3f, 1.1f, .4f, Knit.Fleece, RibCuff: true, Draw: true, Seam: false), new("Track Pants", .99f, 1.1f, 1.1f, 1.1f, Stripe: true, Draw: true), new("Ripped Jeans", .99f, .7f, .7f, .8f, Knit.Denim, Rips: 3),
        new("Cuffed Jeans", .9f, .8f, .9f, 1.2f, Knit.Denim, Cuff: true, Belt: true), new("Dress Trousers", 1, .9f, .8f, .9f, Pleat: true, Belt: true), new("Wide Trousers", 1.03f, 1.8f, 2.4f, 3, Pleat: true), new("Corduroys", 1, 1, 1, 1.2f, Knit.Rib), new("Overalls", 1, 1.4f, 1.5f, 1.7f, Knit.Denim, Bib: true),
        new("Paint Overalls", .92f, 1.6f, 1.7f, 1.8f, Bib: true, Cuff: true, KneePatch: true), new("Leggings", .97f, 0, 0, 0, Seam: false), new("Side Panel Leggings", .97f, .05f, .05f, .05f, Stripe: true, Seam: false), new("Harem Pants", .92f, 2.8f, 2.6f, .3f, RibCuff: true, Seam: false), new("Work Pants", 1, 1.2f, 1.3f, 1.3f, KneePatch: true, Belt: true),
        new("Sailor Pants", 1.02f, .7f, 1.2f, 2.8f, Flap: true), new("Plaid Pajamas", 1.02f, 1.6f, 1.8f, 1.9f, Knit.Flannel, Draw: true), new("Sweatpants", 1, 1.6f, 1.7f, 1.2f, Knit.Fleece, Draw: true, Seam: false), new("Hiking Pants", .98f, 1, 1.1f, .9f, Cargo: true, ColorBlock: true, Belt: true), new("Capri Pants", .74f, .5f, .5f, .7f),
        new("Culottes", .78f, 1.7f, 2.4f, 3.2f, Pleat: true), new("Waders", 1, 1.9f, 2, 1.7f, Bib: true, RibCuff: true, Seam: false), new("Patchwork Jeans", 1, 1, 1.1f, 1.4f, Knit.Denim, KneePatch: true, ColorBlock: true), new("Knit Lounge Pants", 1.01f, 1.4f, 1.6f, 1.9f, Knit.Rib, Draw: true, Seam: false), new("Scallop Trousers", .95f, .9f, 1.2f, 1.9f, Scallop: true, Belt: true),
    ];
    public static readonly PantsDef[] Shorts = [
        new("Denim Shorts", .36f, 1, 1.4f, 1.6f, Knit.Denim, Cuff: true), new("Chino Shorts", .42f, 1, 1.3f, 1.4f, Belt: true), new("Cargo Shorts", .48f, 1.6f, 1.9f, 2.1f, Cargo: true), new("Gym Shorts", .38f, 1.5f, 2, 2.3f, Stripe: true, Draw: true, Seam: false), new("Running Shorts", .26f, 1.4f, 1.9f, 2.3f, Scallop: true, Draw: true, Seam: false),
        new("Basketball Shorts", .5f, 1.8f, 2.4f, 2.8f, Stripe: true, Draw: true, Seam: false), new("Bike Shorts", .42f, 0, 0, 0, Seam: false), new("Swim Trunks", .37f, 1.3f, 1.7f, 2, Draw: true), new("Board Shorts", .48f, 1.2f, 1.5f, 1.7f, ColorBlock: true, Draw: true), new("Cutoffs", .3f, .8f, 1, 1.2f, Knit.Denim, Rips: 2),
        new("Long Jorts", .55f, 1.7f, 2.1f, 2.5f, Knit.Denim), new("Pleated Shorts", .4f, 1.3f, 1.7f, 2, Pleat: true, Belt: true), new("Bermudas", .5f, 1, 1.1f, 1.3f), new("Sweat Shorts", .4f, 1.5f, 1.7f, 1.8f, Knit.Fleece, Draw: true, Seam: false), new("Shortalls", .38f, 1.4f, 1.7f, 2, Knit.Denim, Bib: true, Cuff: true),
        new("Hiking Shorts", .44f, 1.1f, 1.3f, 1.4f, Cargo: true, Belt: true), new("Corduroy Shorts", .34f, .9f, 1.2f, 1.4f, Knit.Rib), new("Paperbag Shorts", .38f, 1.6f, 2, 2.2f, Belt: true, Seam: false), new("Lace Hem Shorts", .33f, 1, 1.3f, 1.6f, Scallop: true), new("Patch Shorts", .42f, 1.2f, 1.5f, 1.7f, Knit.Denim, KneePatch: true, Rips: 1),
        new("Culotte Shorts", .46f, 1.6f, 2.4f, 3.2f), new("Skater Shorts", .56f, 1.9f, 2.3f, 2.6f, Belt: true), new("Flannel Boxers", .34f, 1.2f, 1.6f, 1.9f, Knit.Flannel, Draw: true), new("Knit Shorts", .34f, 1.1f, 1.4f, 1.6f, Knit.Rib, RibCuff: true, Draw: true), new("Tailored Shorts", .41f, .9f, 1.1f, 1.2f, Pleat: true, Belt: true),
        new("Sailor Shorts", .36f, .9f, 1.2f, 1.4f, Flap: true), new("Pocket Shorts", .46f, 1.4f, 1.6f, 1.8f, Cargo: true, Draw: true), new("Two Tone Shorts", .4f, 1.2f, 1.5f, 1.7f, ColorBlock: true), new("Rolled Shorts", .37f, 1.1f, 1.3f, 1.4f, Cuff: true, Belt: true), new("Bloomers", .34f, 2.2f, 2.2f, .9f, RibCuff: true, Seam: false),
    ];
    public static readonly SkirtDef[] Skirts = [
        new("A-Line Skirt", .42f, 1, SkirtKind.Plain), new("Pleated Mini", .3f, 1.1f, SkirtKind.Pleated), new("Pleated Midi", .62f, 1.3f, SkirtKind.Pleated), new("Denim Skirt", .33f, .45f, SkirtKind.Buttons, Pockets: true), new("Button Midi", .62f, .9f, SkirtKind.Buttons, true),
        new("Maxi Skirt", .9f, 1.3f, SkirtKind.Plain), new("Tiered Maxi", .9f, 1.7f, SkirtKind.Tiered), new("Tiered Mini", .34f, 1.5f, SkirtKind.Tiered), new("Wrap Skirt", .52f, .9f, SkirtKind.Wrap), new("Wrap Maxi", .86f, .9f, SkirtKind.Wrap, true),
        new("Ruffle Skirt", .42f, 1.3f, SkirtKind.Ruffle), new("Ruffle Maxi", .86f, 1.4f, SkirtKind.Ruffle), new("Pencil Skirt", .5f, .1f, SkirtKind.Pencil, true), new("Long Pencil", .68f, .12f, SkirtKind.Pencil), new("Tulle Skirt", .46f, 2, SkirtKind.Tulle),
        new("Ballet Tutu", .26f, 2.6f, SkirtKind.Tulle), new("Circle Skirt", .46f, 2.1f, SkirtKind.Plain, Trim: true), new("Tennis Skirt", .28f, 1.3f, SkirtKind.Pleated, Trim: true), new("Kilt", .46f, 1, SkirtKind.Kilt, true), new("Mini Kilt", .32f, .9f, SkirtKind.Kilt),
        new("Panel Skirt", .5f, 1.2f, SkirtKind.Panel), new("Panel Maxi", .88f, 1.4f, SkirtKind.Panel), new("Apron Skirt", .58f, 1.2f, SkirtKind.Apron), new("Petal Skirt", .42f, 1.4f, SkirtKind.Petal), new("Petal Midi", .62f, 1.5f, SkirtKind.Petal),
        new("Cargo Skirt", .38f, .6f, SkirtKind.Plain, true, Pockets: true), new("Suspender Skirt", .4f, 1.1f, SkirtKind.Pleated, Straps: true), new("Pinafore Skirt", .44f, 1.2f, SkirtKind.Buttons, Straps: true), new("Border Skirt", .6f, 1.5f, SkirtKind.Plain, true, true), new("Bubble Skirt", .34f, 1.8f, SkirtKind.Plain),
    ];
    public static readonly ShoeDef[] Shoes = [
        new("Court Sneakers", ShoeKind.Sneaker, 0, 1, true, false, true), new("Canvas Lows", ShoeKind.Skate, 0, .9f, true, true), new("Canvas Highs", ShoeKind.HighTop, .18f, .9f, true, true), new("Chunky Runners", ShoeKind.Runner, 0, 1.5f, true, false, true), new("Retro Runners", ShoeKind.Runner, 0, 1, true, true, true),
        new("Basketball Highs", ShoeKind.HighTop, .2f, 1.3f, true, false, true), new("Work Boots", ShoeKind.Boot, .24f, 1.3f, true, true), new("Combat Boots", ShoeKind.Combat, .36f, 1.4f, true), new("Chelsea Boots", ShoeKind.Chelsea, .2f, 1), new("Cowboy Boots", ShoeKind.Cowboy, .42f, 1.1f, false, false, true),
        new("Rain Boots", ShoeKind.Rain, .46f, 1), new("Hiking Boots", ShoeKind.Hiking, .22f, 1.4f, true, true, true), new("Snow Boots", ShoeKind.Fur, .34f, 1.4f, false, false, false, true), new("Fluffy Boots", ShoeKind.Fur, .26f, 1, false, false, false, true), new("Platform Boots", ShoeKind.Platform, .3f, 2.2f, true),
        new("Sandals", ShoeKind.Sandal, 0, .9f, false, false, false, false, true), new("Slides", ShoeKind.Slide, 0, 1), new("Flip Flops", ShoeKind.Slide, 0, .6f), new("Loafers", ShoeKind.Loafer, 0, .8f), new("Oxfords", ShoeKind.Oxford, 0, .8f, true, true),
        new("Mary Janes", ShoeKind.MaryJane, 0, .9f, false, false, false, false, true), new("Ballet Flats", ShoeKind.Flat, 0, .5f, false, false, false, false, false, true), new("Clogs", ShoeKind.Clog, 0, 1.4f), new("Slippers", ShoeKind.Slipper, 0, .7f, false, false, false, true), new("Moccasins", ShoeKind.Loafer, .04f, .5f, false, true, false, true),
        new("Skate Shoes", ShoeKind.Skate, 0, 1.2f, true, false, true), new("Winter Highs", ShoeKind.HighTop, .24f, 1.4f, true, true, false, true), new("Duck Boots", ShoeKind.Boot, .26f, 1.1f, true, true, true), new("Velcro Kicks", ShoeKind.Sneaker, 0, 1.1f, false, true, false, false, true), new("Tall Waders", ShoeKind.Rain, .6f, 1.2f, false, false, true),
    ];
    public static readonly HatDef[] Hats = [
        new("None", HatKind.None, false, 0, 0), new("Beanie", HatKind.Beanie, true, .9f, 0), new("Pom Beanie", HatKind.PomBeanie, true, .9f, 0), new("Ball Cap", HatKind.Cap, true, .8f, 1), new("Backwards Cap", HatKind.CapBack, true, .8f, 0), new("Bucket Hat", HatKind.Bucket, true, .8f, 1.3f), new("Wide Felt Hat", HatKind.Wide, true, .75f, 2),
        new("Fedora", HatKind.Fedora, true, .75f, 1.5f), new("Cowboy Hat", HatKind.Cowboy, true, .75f, 2.1f), new("Beret", HatKind.Beret, true, .7f, 0), new("Top Hat", HatKind.Top, true, .75f, 1.4f), new("Headband", HatKind.Headband, false, .3f, 0), new("Crown", HatKind.Crown, false, .3f, 0),
        new("Flower Crown", HatKind.Flower, false, .25f, 0), new("Headphones", HatKind.Phones, false, .3f, 0), new("Trapper Hat", HatKind.Trapper, true, .9f, 0), new("Wizard Hat", HatKind.Witch, true, .75f, 1.9f), new("Straw Hat", HatKind.Straw, true, .75f, 2.2f), new("Visor", HatKind.Visor, false, .35f, 1),
        new("Bandana", HatKind.Bandana, true, .9f, 0), new("Party Hat", HatKind.Party, false, .2f, 0), new("Frog Hat", HatKind.Frog, true, .9f, 0), new("Cat Beanie", HatKind.CatBeanie, true, .9f, 0), new("Sailor Cap", HatKind.Sailor, true, .7f, 0), new("Newsboy Cap", HatKind.Newsboy, true, .8f, .8f),
        new("Bowler", HatKind.Bowler, true, .8f, 1.3f), new("Chef Hat", HatKind.Chef, true, .7f, 0), new("Safari Hat", HatKind.Safari, true, .75f, 1.8f), new("Earmuffs", HatKind.Earmuffs, false, .3f, 0), new("Big Bow", HatKind.Bow, false, .1f, 0), new("Propeller Cap", HatKind.Propeller, true, .85f, .8f),
    ];
    public static readonly HairDef[] Hair = [
        new("Buzz Cut", 3, .03f, .02f, -.1f, 0, 0, Fringe.Crop, Fade: .35f, Flat: 0), new("Crew Cut", 0, .12f, .03f, -.1f, 0, 0, Fringe.Crop, Fade: .6f, Flat: .5f), new("Curtains", 0, .2f, .1f, .12f, .55f, .95f, Fringe.Curtain, Flat: .8f), new("Textured Fringe", 1, .24f, .05f, 0, 0, 0, Fringe.Messy, Fade: .75f, Flat: .8f), new("Low Fade Quiff", 0, .3f, .03f, -.1f, 0, 0, Fringe.Quiff, Fade: .8f, Flat: .9f),
        new("Pompadour", 0, .42f, .03f, -.05f, 0, 0, Fringe.Pomp, Fade: .5f, Flat: .9f), new("Slick Back", 0, .16f, .06f, .05f, .5f, .85f, Fringe.Slick, Flat: .6f), new("Middle Part", 0, .18f, .12f, .25f, .6f, .98f, Fringe.Middle, Flat: .7f), new("Side Part", 0, .2f, .05f, 0, 0, 0, Fringe.Side, Fade: .3f, Flat: .75f), new("Spiky", 0, .34f, .06f, .0f, .3f, .85f, Fringe.Spiky, Flat: .9f),
        new("Wolf Cut", 1, .28f, .16f, .7f, 1.5f, 1.12f, Fringe.Curtain, Flat: .7f), new("Mullet", 1, .22f, .05f, .05f, 1.45f, .9f, Fringe.Messy, Fade: .25f, Flat: .7f), new("Shag", 1, .24f, .18f, .75f, 1.25f, 1.1f, Fringe.Wispy, Flat: .7f), new("Emo Sweep", 0, .22f, .14f, .7f, .9f, 1.05f, Fringe.Emo, Flat: .7f), new("Bowl Cut", 0, .16f, .14f, .12f, .4f, 1, Fringe.Blunt, Flat: .5f),
        new("Pixie", 0, .18f, .06f, .12f, .4f, .9f, Fringe.Side, Flat: .7f), new("Bob", 0, .16f, .2f, .85f, .95f, 1.16f, Fringe.Side, Flat: .5f), new("Bob with Bangs", 0, .16f, .2f, .85f, .95f, 1.16f, Fringe.Blunt, Flat: .5f), new("Long Bob", 1, .17f, .18f, 1.25f, 1.4f, 1.16f, Fringe.Middle, Flat: .5f), new("Long Straight", 0, .15f, .14f, 2.3f, 2.8f, 1.12f, Fringe.Middle, Flat: .5f),
        new("Hime Cut", 0, .15f, .16f, .95f, 2.7f, 1.12f, Fringe.Blunt, Flat: .5f), new("Beach Waves", 1, .2f, .2f, 1.9f, 2.2f, 1.2f, Fringe.Curtain, Flat: .6f), new("Butterfly Cut", 1, .24f, .24f, 1.5f, 2.3f, 1.25f, Fringe.Curtain, Flat: .6f), new("Curly Bob", 2, .3f, .34f, .75f, .9f, 1.25f, Fringe.Puff, Flat: .6f), new("Long Curls", 2, .28f, .32f, 1.7f, 2.1f, 1.35f, Fringe.Middle, Flat: .6f),
        new("Curly Top Fade", 2, .34f, .03f, -.1f, 0, 0, Fringe.Puff, Fade: .8f, Flat: .7f), new("Afro", 3, .6f, .5f, .4f, .8f, 1.5f, Fringe.Puff, Flat: .7f), new("Short Afro", 3, .28f, .16f, .1f, .4f, 1.1f, Fringe.Puff, Flat: .7f), new("High Top Fade", 3, .62f, .03f, -.1f, 0, 0, Fringe.Crop, Fade: .8f, Flat: .85f), new("Afro Puffs", 3, .08f, .04f, 0, .3f, .9f, Fringe.Slick, Updo.AfroPuffs, Flat: .3f),
        new("Box Braids", 4, .16f, .14f, 2, 2.5f, 1.16f, Fringe.Middle, Flat: .4f), new("Cornrow Tails", 4, .05f, .03f, 0, 1.7f, .8f, Fringe.Slick, Flat: .2f), new("Locs", 5, .22f, .2f, 1.1f, 1.5f, 1.18f, Fringe.Curtain, Flat: .4f), new("Long Locs", 5, .2f, .18f, 2.1f, 2.6f, 1.2f, Fringe.Middle, Flat: .4f), new("High Ponytail", 0, .1f, .04f, .35f, 0, 0, Fringe.Slick, Updo.HighPony, Flat: .5f),
        new("Low Ponytail", 1, .13f, .08f, .4f, 0, 0, Fringe.Curtain, Updo.LowPony, Flat: .5f), new("Messy Bun", 1, .15f, .1f, .5f, 0, 0, Fringe.Wispy, Updo.Bun, Flat: .5f), new("Space Buns", 0, .12f, .08f, .6f, 0, 0, Fringe.Blunt, Updo.SpaceBuns, Flat: .4f), new("Pigtail Braids", 0, .13f, .08f, .3f, 0, 0, Fringe.Side, Updo.BraidTails, Flat: .5f), new("Half-Up Knot", 0, .14f, .14f, 1.5f, 1.8f, 1.12f, Fringe.Middle, Updo.HalfUp, Flat: .5f),
        new("Man Bun", 0, .1f, .04f, .05f, 0, 0, Fringe.Slick, Updo.ManBun, Fade: .5f, Flat: .4f), new("Mohawk", 0, .5f, .02f, -.1f, .5f, .4f, Fringe.Crop, Updo.Mohawk, Fade: 1, Flat: .9f), new("Side Ponytail", 1, .14f, .08f, .4f, 0, 0, Fringe.Side, Updo.SidePony, Flat: .5f), new("Crown Braid", 0, .12f, .1f, .35f, 0, 0, Fringe.Wispy, Updo.CrownBraid, Flat: .4f), new("Pigtails", 1, .14f, .1f, .3f, 0, 0, Fringe.Blunt, Updo.Pigtails, Flat: .5f),
        new("Bald", 0, 0, 0, -1, 0, 0, Fringe.None, Flat: 0),
    ];
    public static readonly string[] Eyes = ["Round", "Almond", "Soft", "Wide", "Sleepy", "Lashes", "Upturned", "Downturned", "Narrow", "Sparkle", "Hooded", "Small"];
    public static readonly string[] Brows = ["Natural", "Thick", "Thin", "Arched", "Straight", "Bushy", "Soft", "Angled", "Slit", "None"];
    public static readonly string[] Noses = ["Button", "Round", "Straight", "Wide", "Long", "Small", "Upturned", "Hooked"];
    public static readonly string[] Mouths = ["Soft Smile", "Grin", "Neutral", "Smirk", "Open Smile", "Full Lips", "Small", "Pout", "Cat Smile", "Gap Tooth"];
    public static readonly string[] Marks = ["None", "Freckles", "Rosy Cheeks", "Freckles + Rosy", "Beauty Mark", "Dimples", "Band-Aid", "Sun Kissed"];
    public static readonly string[] Beards = ["None", "Stubble", "Mustache", "Handlebar", "Goatee", "Soul Patch", "Chin Strap", "Short Beard", "Full Beard", "Long Beard", "Braided Beard", "Sideburns", "Van Dyke", "Walrus"];
    public static readonly string[] Glasses = ["None", "Round", "Square", "Aviator", "Cat Eye", "Half Rim", "Sunglasses", "Round Shades", "Heart Shades", "Monocle", "Goggles", "Reading"];
    public static readonly string[] Earrings = ["None", "Studs", "Hoops", "Drops", "Pearls", "Stars", "Feathers", "Single Hoop", "Curls"];
    public static readonly string[] Necks = ["None", "Scarf", "Long Scarf", "Chunky Scarf", "Bandana", "Pendant", "Pearls", "Beads", "Necktie", "Bow Tie", "Choker", "Neckerchief", "Camera"];
    public static readonly string[] Backs = ["None", "Backpack", "Hiking Pack", "Satchel", "Tote Bag", "Wicker Basket", "Crossbody", "Frog Pack", "Guitar Case", "Duffel Bag"];
    public static readonly string[] Gloves = ["None", "Gloves", "Mittens", "Fingerless", "Watch", "Bracelets", "Wristband", "Long Gloves", "Arm Warmers"];
    public static readonly string[] Ages = ["Teen", "Young Adult", "Adult", "Elder"];
    public static readonly string[] Idles = ["Relaxed", "Hands on Hips", "Bouncy", "Shy", "Confident", "Sleepy"];
    public static readonly string[] Voices = ["Warm", "Bright", "Deep", "Squeaky", "Mumbly", "Airy"];

    public static int Count(Slot s) => s switch
    {
        Slot.Top => Tops.Length, Slot.Jacket => Jackets.Length, Slot.Bottom => 90, Slot.Shoes => Shoes.Length, Slot.Hat => Hats.Length,
        Slot.Glasses => Glasses.Length, Slot.Earrings => Earrings.Length, Slot.Neck => Necks.Length, Slot.Back => Backs.Length, Slot.Gloves => Gloves.Length, _ => Beards.Length
    };
    public static string NameOf(Slot s, int id) => s switch
    {
        Slot.Top => Tops[id].Name, Slot.Jacket => Jackets[id].Name, Slot.Bottom => id < 30 ? Pants[id].Name : id < 60 ? Shorts[id - 30].Name : Skirts[id - 60].Name,
        Slot.Shoes => Shoes[id].Name, Slot.Hat => Hats[id].Name, Slot.Glasses => Glasses[id], Slot.Earrings => Earrings[id], Slot.Neck => Necks[id], Slot.Back => Backs[id], Slot.Gloves => Gloves[id], _ => Beards[id]
    };
}

public static class Patterns
{
    public static readonly string[] Names = ["Solid", "Stripes", "Pinstripe", "Thin Stripes", "Plaid", "Gingham", "Dots", "Checker", "Argyle", "Half & Half", "Gradient", "Tie-Dye", "Floral", "Stars", "Camo", "Chevron", "Houndstooth", "Hearts", "Splatter", "Waves"];
    static int Fl(float v) => (int)MathF.Floor(v);
    public static Col Get(int p, float u, float v, Col a, Col b, Col c, float s = 1)
    {
        if (p == 0) return a;
        u /= s; v /= s;
        switch (p)
        {
            case 1: return (Fl(v / 3.4f) & 1) == 0 ? a : b;
            case 2: return M.Mod(Fl(u / 1.45f), 3) == 0 ? b : a;
            case 3: return M.Mod(Fl(v / 1.45f), 3) == 0 ? b : a;
            case 4:
                {
                    float f = 0; int x = M.Mod(Fl(u / 1.45f), 6), y = M.Mod(Fl(v / 1.45f), 6);
                    if (x < 2) f += .42f; if (y < 2) f += .42f; var k = Col.Lerp(a, b, f);
                    if (x == 4) k = Col.Lerp(k, c, .55f); if (y == 4) k = Col.Lerp(k, c, .55f); return k;
                }
            case 5: { int x = Fl(u / 2.2f) & 1, y = Fl(v / 2.2f) & 1; return Col.Lerp(a, b, (x + y) * .5f); }
            case 6: { int r = Fl(v / 3.2f); float x = u / 3.2f + (r & 1) * .5f; float dx = x - MathF.Floor(x) - .5f, dy = v / 3.2f - r - .5f; return dx * dx + dy * dy < .075f ? b : a; }
            case 7: return ((Fl(u / 2.9f) + Fl(v / 2.9f)) & 1) == 0 ? a : b;
            case 8:
                {
                    float x = u / 6, y = v / 8; float q = MathF.Abs(x - MathF.Round(x)) + MathF.Abs(y - MathF.Round(y));
                    float l1 = (x + y) * 2 + .5f, l2 = (x - y) * 2 + .5f; bool ln = MathF.Abs(l1 - MathF.Round(l1)) < .07f || MathF.Abs(l2 - MathF.Round(l2)) < .07f;
                    return ln ? c : q < .5f ? a : b;
                }
            case 9: return u * s < 0 ? a : b;
            case 10: return Col.Lerp(a, b, M.Clamp((v * s + 2) / 34f));
            case 11: { float d = MathF.Sqrt(u * u + (v - 12) * (v - 12)); float an = MathF.Atan2(v - 12, u); float w = MathF.Sin(d * .8f + an * 2 + M.Noise(u * .35f, v * .35f, 4) * 2.2f); return w > .35f ? b : w < -.4f ? c : a; }
            case 12: { int x = Fl(u / 5), y = Fl(v / 5); float ox = M.Hash(x, y, 1) * 1.6f - .8f, oy = M.Hash(x, y, 2) * 1.6f - .8f; float dx = u - x * 5 - 2.5f - ox, dy = v - y * 5 - 2.5f - oy; float d = MathF.Sqrt(dx * dx + dy * dy), an = MathF.Atan2(dy, dx); if (d < .7f) return c; return d < 1.45f + .5f * MathF.Cos(an * 5) ? b : a; }
            case 13: { int x = Fl(u / 5), y = Fl(v / 5); float dx = u - x * 5 - 2.5f, dy = v - y * 5 - 2.5f; if (((x + y) & 1) != 0) return a; float d = MathF.Abs(dx) + MathF.Abs(dy); return d < 1.5f || (MathF.Abs(dx) < .5f && MathF.Abs(dy) < 2) || (MathF.Abs(dy) < .5f && MathF.Abs(dx) < 2) ? b : a; }
            case 14: { float n = M.Noise(u * .22f, v * .28f, 7), m = M.Noise(u * .25f, v * .25f, 19); return n > .22f ? b : m > .28f ? c : m < -.4f ? a.Shade(-.3f) : a; }
            case 15: { float q = v + MathF.Abs(u / 5 - MathF.Round(u / 5)) * 6; return (Fl(q / 2.4f) & 1) == 0 ? a : b; }
            case 16: { int x = M.Mod(Fl(u / 1.45f), 4), y = M.Mod(Fl(v / 1.45f), 4); bool k = (x < 2 && y < 2) || (x >= 2 && y < 2 && M.Mod(x + y, 4) == 2) || (x < 2 && y >= 2 && M.Mod(x + y, 4) == 2); return k ? b : a; }
            case 17: { int x = Fl(u / 4.6f), y = Fl(v / 4.6f); if (((x + y) & 1) != 0) return a; float dx = MathF.Abs(u - x * 4.6f - 2.3f), dy = v - y * 4.6f - 2.3f; return (dx - .75f) * (dx - .75f) + (dy + .5f) * (dy + .5f) < .95f || (dy > -.4f && dx + dy * .9f < 1.5f) ? b : a; }
            case 18: { for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) { int x = Fl(u / 5) + i, y = Fl(v / 5) + j; float px = (x + M.Hash(x, y, 3)) * 5, py = (y + M.Hash(x, y, 4)) * 5, r = M.Hash(x, y, 5) * 1.5f; if ((u - px) * (u - px) + (v - py) * (v - py) < r * r) return M.Hash(x, y, 6) > .5f ? b : c; } return a; }
            default: { float q = v + MathF.Sin(u * .7f) * 1.1f; int k = M.Mod(Fl(q / 2.2f), 3); return k == 0 ? a : k == 1 ? b : Col.Lerp(a, c, .7f); }
        }
    }
}

namespace UntitledGame;

public enum Slot { Top, Jacket, Bottom, Shoes, Hat, Glasses, Earrings, Neck, Back, Gloves, Beard }

public sealed class Piece
{
    public int Id { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
    public int Pat { get; set; }
    public float Scale { get; set; } = 1;
    public Piece() { }
    public Piece(int id, int a, int b, int c, int pat = 0) { Id = id; A = a; B = b; C = c; Pat = pat; }
    public Piece Clone() => (Piece)MemberwiseClone();
}

public sealed class HairData
{
    public int Id { get; set; } = 2;
    public int Base { get; set; } = 0x5a3a26;
    public int Highlight { get; set; } = 0xc98f4e;
    public float HighlightAmt { get; set; }
    public bool SplitOn { get; set; }
    public int Split { get; set; } = 0xe06a8a;
    public float Ombre { get; set; }
    public int OmbreCol { get; set; } = 0xe8c27a;
    public float Roots { get; set; }
    public int RootCol { get; set; } = 0x2c1c16;
    public bool UnderOn { get; set; }
    public int Under { get; set; } = 0x5b8fd6;
    // shape: 0 natural, then high, low, receding, widow's peak, M-shaped, thinning, bald on top
    public int Hairline { get; set; }
    public float Length { get; set; } = .5f;
    public float Curl { get; set; } = .5f;
    public bool Flip { get; set; }
    public HairData Clone() => (HairData)MemberwiseClone();
}

public sealed class AvatarData
{
    public string Name { get; set; } = "";
    // body sliders, 0..1 with .5 = average
    public float Height { get; set; } = .5f;
    public float Weight { get; set; } = .42f;
    /// Weight reaches past the old 0..1 at both ends; the slider covers the whole span, and older saves keep their exact shape.
    public const float WeightMin = -.10f, WeightMax = 1.17f;
    public static float WeightToSlider(float w) => (w - WeightMin) / (WeightMax - WeightMin);
    public static float SliderToWeight(float v) => WeightMin + M.Clamp(v) * (WeightMax - WeightMin);
    public float Shoulder { get; set; } = .5f;
    public float Waist { get; set; } = .5f;
    public float Hip { get; set; } = .5f;
    public float Arm { get; set; } = .5f;
    public float Leg { get; set; } = .5f;
    public float Head { get; set; } = .5f;
    public int Age { get; set; } = 1;
    public int Skin { get; set; } = 0xe0a981;
    public int Eye { get; set; } = 0;
    public int EyeCol { get; set; } = 0x5b3a22;
    public int Brow { get; set; } = 0;
    public int Nose { get; set; } = 0;
    public int Mouth { get; set; } = 0;
    public int Lip { get; set; } = 0xb5605a;
    public int Marks { get; set; } = 0; // none, freckles, blush, both, beauty mark...
    // face sculpting, 0..1 with .5 = as drawn by default
    public float Jaw { get; set; } = .5f;
    public float Chin { get; set; } = .5f;
    public float Cheeks { get; set; } = .5f;
    public float FaceLength { get; set; } = .5f;
    public float EyeSize { get; set; } = .5f;
    public float EyeGap { get; set; } = .5f;
    public float EyeHeight { get; set; } = .5f;
    public float BrowHeight { get; set; } = .5f;
    public float NoseSize { get; set; } = .5f;
    public float NoseWidth { get; set; } = .5f;
    public float MouthWidth { get; set; } = .5f;
    public float LipSize { get; set; } = .5f;
    public float EarSize { get; set; } = .5f;
    // skin
    public float Freckles { get; set; }
    public float Wrinkles { get; set; }
    public float LaughLines { get; set; }
    public float UnderEyes { get; set; }
    public float Vitiligo { get; set; }
    public int Scar { get; set; }
    public int Birthmark { get; set; }
    public List<float> Moles { get; set; } = new(); // x, y pairs in head space
    public int BrowCol { get; set; } // 0 = same as the hair
    // makeup
    public int Liner { get; set; }
    public int LinerCol { get; set; } = 0x1c1216;
    public float Shadow { get; set; }
    public int ShadowCol { get; set; } = 0xb07a8a;
    public float Lipstick { get; set; }
    public int LipstickCol { get; set; } = 0xb8304a;
    public float Blush { get; set; }
    public int BlushCol { get; set; } = 0xe0564f;
    public int Nails { get; set; } // 0 = bare
    // tattoos: a design for each spot, and piercings as a set of flags
    public int[] Tattoos { get; set; } = new int[7]; // pattern last picked for [0] left arm, [1] right arm; -1 = drawn in the studio
    /// The ink on each arm: AvatarPainter.TW x TH cells, 0 = bare skin, n = TatPal[n - 1].
    [System.Text.Json.Serialization.JsonIgnore] public byte[][] Art { get; set; } = new byte[2][];
    public string TatL { get => Enc(0); set => Dec(0, value); }
    public string TatR { get => Enc(1); set => Dec(1, value); }
    public int[] TatPal { get; set; } = (int[])Catalog.TatInks.Clone();
    string Enc(int i) => Art == null || Art.Length < 2 || Art[i] == null ? null : Convert.ToBase64String(Art[i]);
    void Dec(int i, string v) { if (Art == null || Art.Length < 2) Art = new byte[2][]; try { Art[i] = string.IsNullOrEmpty(v) ? null : Convert.FromBase64String(v); } catch (FormatException) { Art[i] = null; } }
    public int TatCol { get; set; } = 0x2a2c38;
    public bool TatColor { get; set; }
    public int Piercings { get; set; }
    public int Metal { get; set; } = 0xc8ccd2;
    // beard
    public float BeardLen { get; set; } = .5f;
    public float BeardThick { get; set; } = .7f;
    public float BeardGrey { get; set; }
    public HairData Hair { get; set; } = new();
    public Piece[] P { get; set; } = Default();
    public int OutlineCol { get; set; } = 0x2a1a1c;
    public int Idle { get; set; } = 0;
    public int Voice { get; set; } = 0;

    public Piece this[Slot s] => P[(int)s];

    static Piece[] Default() => [
        new(0, 0xe9dcc3, 0xb8473f, 0x3a4a66), new(0, 0x4f6b52, 0xd8c49a, 0x3a2c24), new(0, 0x4a6285, 0x35496b, 0xc99a4b),
        new(1, 0xf0e8da, 0xb8473f, 0x3a4a66), new(0, 0xc65a3a, 0xe9dcc3, 0x3a2c24), new(0, 0x3a2c24, 0x9ec3cf, 0xc99a4b),
        new(0, 0xe0b650, 0xf0e8da, 0xb8473f), new(0, 0xb8473f, 0xe9dcc3, 0x3a4a66), new(0, 0x8a5a38, 0xc99a4b, 0x3a2c24),
        new(0, 0x6b4a33, 0xe9dcc3, 0x3a2c24), new(0, 0x5a3a26, 0x5a3a26, 0x5a3a26)];

    public AvatarData Clone()
    {
        var d = (AvatarData)MemberwiseClone();
        d.Hair = Hair.Clone(); d.P = P.Select(p => p.Clone()).ToArray(); d.Moles = new(Moles ?? new()); d.Tattoos = (int[])(Tattoos ?? new int[7]).Clone(); d.Art = [Art?.ElementAtOrDefault(0)?.ToArray(), Art?.ElementAtOrDefault(1)?.ToArray()]; d.TatPal = (int[])(TatPal ?? Catalog.TatInks).Clone();
        return d;
    }

    /// Repairs anything a hand edited or older save may contain. Clothes can never be missing.
    public void Validate()
    {
        var def = Default();
        if (P == null || P.Length != def.Length) P = def;
        Hair ??= new();
        for (int i = 0; i < P.Length; i++) { P[i] ??= def[i]; P[i].Id = Math.Clamp(P[i].Id, 0, Catalog.Count((Slot)i) - 1); P[i].Pat = Math.Clamp(P[i].Pat, 0, Patterns.Names.Length - 1); P[i].Scale = M.Clamp(P[i].Scale, .5f, 2); }
        Hair.Id = Math.Clamp(Hair.Id, 0, Catalog.Hair.Length - 1);
        Age = Math.Clamp(Age, 0, 3); Eye = Math.Clamp(Eye, 0, Catalog.Eyes.Length - 1); Brow = Math.Clamp(Brow, 0, Catalog.Brows.Length - 1);
        Nose = Math.Clamp(Nose, 0, Catalog.Noses.Length - 1); Mouth = Math.Clamp(Mouth, 0, Catalog.Mouths.Length - 1); Marks = Math.Clamp(Marks, 0, Catalog.Marks.Length - 1);
        Idle = Math.Clamp(Idle, 0, Catalog.Idles.Length - 1); Voice = Math.Clamp(Voice, 0, Catalog.Voices.Length - 1);
        Jaw = M.Clamp(Jaw); Chin = M.Clamp(Chin); Cheeks = M.Clamp(Cheeks); FaceLength = M.Clamp(FaceLength); EyeSize = M.Clamp(EyeSize); EyeGap = M.Clamp(EyeGap); EyeHeight = M.Clamp(EyeHeight); BrowHeight = M.Clamp(BrowHeight); NoseSize = M.Clamp(NoseSize); NoseWidth = M.Clamp(NoseWidth); MouthWidth = M.Clamp(MouthWidth); LipSize = M.Clamp(LipSize); EarSize = M.Clamp(EarSize);
        Freckles = M.Clamp(Freckles); Wrinkles = M.Clamp(Wrinkles); LaughLines = M.Clamp(LaughLines); UnderEyes = M.Clamp(UnderEyes); Vitiligo = M.Clamp(Vitiligo); Scar = Math.Clamp(Scar, 0, Catalog.Scars.Length - 1); Birthmark = Math.Clamp(Birthmark, 0, Catalog.Birthmarks.Length - 1);
        Moles ??= new(); if (Moles.Count % 2 == 1) Moles.RemoveAt(Moles.Count - 1); if (Moles.Count > 24) Moles.RemoveRange(24, Moles.Count - 24); for (int i = 0; i < Moles.Count; i++) Moles[i] = float.IsFinite(Moles[i]) ? Math.Clamp(Moles[i], -1.2f, 1.2f) : 0;
        Liner = Math.Clamp(Liner, 0, Catalog.Liners.Length - 1); Shadow = M.Clamp(Shadow); Lipstick = M.Clamp(Lipstick); Blush = M.Clamp(Blush);
        var t = Tattoos ?? new int[7]; if (t.Length != 7) Array.Resize(ref t, 7); for (int i = 0; i < 7; i++) t[i] = i < 2 ? Math.Clamp(t[i], -1, Catalog.TattooDesigns.Length - 1) : 0; Tattoos = t;
        if (TatPal == null || TatPal.Length != Catalog.TatInks.Length) TatPal = (int[])Catalog.TatInks.Clone();
        if (Art == null || Art.Length != 2) Art = [Art?.ElementAtOrDefault(0), Art?.ElementAtOrDefault(1)];
        for (int i = 0; i < 2; i++) { var a = Art[i]; if (a != null && a.Length != AvatarPainter.TW * AvatarPainter.TH) a = null; if (a != null) for (int k = 0; k < a.Length; k++) if (a[k] > TatPal.Length) a[k] = 0; if (a == null && t[i] > 0) a = AvatarPainter.Raster(t[i], this); if (a == null && t[i] < 0) t[i] = 0; Art[i] = a; } Piercings &= (1 << Catalog.Piercings.Length) - 1;
        Hair.Hairline = Math.Clamp(Hair.Hairline, 0, Catalog.Hairlines.Length - 1); Hair.Length = M.Clamp(Hair.Length); Hair.Curl = M.Clamp(Hair.Curl); BeardLen = M.Clamp(BeardLen); BeardThick = M.Clamp(BeardThick); BeardGrey = M.Clamp(BeardGrey);
        Height = M.Clamp(Height); Weight = float.IsFinite(Weight) ? Math.Clamp(Weight, WeightMin, WeightMax) : .42f; Shoulder = M.Clamp(Shoulder); Waist = M.Clamp(Waist); Hip = M.Clamp(Hip); Arm = M.Clamp(Arm); Leg = M.Clamp(Leg); Head = M.Clamp(Head);
    }

    public static readonly int[] SkinTones = [0xfbe3d0, 0xf6d3b8, 0xf0c4a0, 0xe8b48e, 0xe0a981, 0xd49a70, 0xc68a5e, 0xb67a4e, 0xa56a42, 0x925a38, 0x7e4b30, 0x6b3e29, 0x573222, 0x44271c];
    public static readonly int[] HairCols = [0x1c1618, 0x2c1c16, 0x432a1c, 0x5a3a26, 0x7a4f2e, 0x9a6a3c, 0xc08d4f, 0xe0b872, 0xf0dca8, 0xb5502c, 0x8a3324, 0xd06a3a, 0x9c9a98, 0xd8d6d2, 0xf4f0e8, 0xe06a8a, 0x9a5fc0, 0x5b8fd6, 0x3fa59a, 0x6fae4e];
    public static readonly int[] Cloth = [0xf3ebdc, 0xe9dcc3, 0xd8c49a, 0xc99a4b, 0xe0b650, 0xe58a3c, 0xc65a3a, 0xb8473f, 0x8c2f35, 0xe3a1a1, 0xd86f86, 0x9c4a7a, 0x6d4a8a, 0x9a8ac8, 0x4a6285, 0x35496b, 0x7fa8c9, 0x4d9a9c, 0x2f6a6a, 0x8fb584, 0x5f8a52, 0x4f6b52, 0x3a4f36, 0x8a5a38, 0x6b4a33, 0x4a3428, 0xb9b2a8, 0x83807f, 0x4e4c52, 0x2b2a30];
    public static readonly int[] EyeCols = [0x3a2418, 0x5b3a22, 0x7d552c, 0x8a7a3a, 0x5c7a42, 0x3f7a6a, 0x4a82b0, 0x6f9cc0, 0x7c8894, 0x7a5a9a];

    public static AvatarData Random(Rng r, AvatarData keep = null, Func<string, bool> locked = null)
    {
        var d = keep?.Clone() ?? new AvatarData();
        bool L(string s) => locked != null && locked(s);
        int Cl() => r.Pick(Cloth);
        // pick a small harmonious palette and reuse it, so random outfits look styled rather than noisy
        int[] pal = [Cl(), r.Pick(Cloth[..4]), r.Pick(Cloth[14..18]), r.Pick(Cloth[23..30])];
        int Pc() => r.Pick(pal);
        void Rand(Slot s, float none = 0, bool pat = true)
        {
            int n = Catalog.Count(s);
            d.P[(int)s] = new Piece(r.P(none) ? 0 : r.I(none > 0 ? 1 : 0, n), Pc(), Pc(), Pc(), pat && r.P(.35f) ? r.I(Patterns.Names.Length) : 0);
        }
        if (!L("Body")) { float G() => M.Clamp(.5f + (r.F() + r.F() - 1) * .45f); d.Height = G(); d.Weight = G(); d.Shoulder = G(); d.Waist = G(); d.Hip = G(); d.Arm = G(); d.Leg = G(); d.Head = G(); d.Skin = r.Pick(SkinTones); d.Age = r.P(.6f) ? 1 : r.I(4); }
        if (!L("Face")) { d.Eye = r.I(Catalog.Eyes.Length); d.EyeCol = r.Pick(EyeCols); d.Brow = r.I(Catalog.Brows.Length); d.Nose = r.I(Catalog.Noses.Length); d.Mouth = r.I(Catalog.Mouths.Length); d.Marks = r.P(.6f) ? 0 : r.I(Catalog.Marks.Length); d.P[(int)Slot.Beard] = new(r.P(.75f) ? 0 : r.I(Catalog.Beards.Length), 0x5a3a26, 0, 0); }
        if (!L("Hair"))
        {
            d.Hair = new HairData { Id = r.I(Catalog.Hair.Length), Base = r.P(.8f) ? r.Pick(HairCols[..15]) : r.Pick(HairCols), Highlight = r.Pick(HairCols), HighlightAmt = r.P(.35f) ? r.R(.3f, .9f) : 0, SplitOn = r.P(.06f), Split = r.Pick(HairCols), Ombre = r.P(.25f) ? r.R(.3f, .9f) : 0, OmbreCol = r.Pick(HairCols), Roots = r.P(.15f) ? r.R(.3f, .8f) : 0, RootCol = 0x2c1c16, UnderOn = r.P(.08f), Under = r.Pick(HairCols) };
        }
        d.P[(int)Slot.Beard].A = d.Hair.Base;
        if (!L("Tops")) Rand(Slot.Top);
        if (!L("Jackets")) Rand(Slot.Jacket, .5f);
        if (!L("Bottoms")) Rand(Slot.Bottom);
        if (!L("Shoes")) Rand(Slot.Shoes, 0, false);
        if (!L("Hats")) Rand(Slot.Hat, .5f);
        if (!L("Extras")) { Rand(Slot.Glasses, .75f, false); Rand(Slot.Earrings, .7f, false); Rand(Slot.Neck, .7f); Rand(Slot.Back, .8f); Rand(Slot.Gloves, .85f); }
        return d;
    }
}

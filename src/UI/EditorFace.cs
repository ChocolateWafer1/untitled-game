using System.Numerics;
using Raylib_cs;
namespace UntitledGame;

/// The detailed pages of the creator: face shape, skin, makeup, tattoos, piercings, beards and hair shape.
public sealed partial class EditorScreen
{
    Vector2 headC; float headRx = 1, headRy = 1;
    public void ShowForTest(int t, int sb) { tab = t; sub[t] = sb; tabSlide = 1; scroll = scrollT = 0; }
    public string TypeForTest(int i, string t) { var v = Parse(t, i == 1); if (v == null) return "unreadable"; SetMeasure(i, v.Value); numEdit = -1; return Measure(i); }
    public (Vector2 c, float rx, float ry) HeadForTest => (headC, headRx, headRy);
    static readonly int[] Inks = [0x2a2c38, 0x1c1216, 0x3a5a4a, 0x2f3a6a, 0x6a2a3a, 0x8a4a2a];
    static readonly int[] Metals = [0xc8ccd2, 0xe0b650, 0xd8a090, 0x2b2a30, 0xf4f0e8, 0x5fb0d0, 0xd86f86];
    static readonly int[] Makeup = [0x1c1216, 0x4a3428, 0xb8304a, 0x8c2f35, 0xd86f86, 0xe3a1a1, 0xe0564f, 0xe58a3c, 0xc99a4b, 0xb07a8a, 0x9c4a7a, 0x6d4a8a, 0x4a6285, 0x4d9a9c, 0x5f8a52, 0xf3ebdc, 0x83807f, 0x7a3a34, 0xe0b650, 0x2f6a6a];
    int FaceHash() => HashCode.Combine(HashCode.Combine(D.Jaw, D.Chin, D.Cheeks, D.FaceLength, D.EyeSize, D.EyeGap, D.EyeHeight, D.BrowHeight), HashCode.Combine(D.NoseSize, D.NoseWidth, D.MouthWidth, D.LipSize, D.EarSize, D.Freckles, D.Wrinkles, D.Vitiligo), HashCode.Combine(D.Liner, D.LinerCol, D.Shadow, D.ShadowCol, D.Lipstick, D.LipstickCol, D.Blush, D.BrowCol), HashCode.Combine(D.Piercings, D.Metal, D.BeardLen, D.BeardThick, D.BeardGrey, D.Scar, D.Birthmark, D.Moles.Count));
    /// A labelled slider that records an undo step when grabbed.
    bool Sl(string id, string label, float v, Action<float> set, float x, float y, float w, float lw = 78)
    {
        Font.Draw(label, x + 2, y, Ui.Ink); if (Ui.Click && Ui.Over(x + lw - 6, y - 3, w - lw + 6, 14)) Snap(); float q = v; if (Ui.Slider(id, ref q, x + lw, y, w - lw - 4)) { set(q); return true; } return false;
    }
    int Subs2(string[][] rows, float x, float y, float w)
    {
        int k = 0; for (int r = 0; r < rows.Length; r++) { var n = rows[r]; float bw = (w - (n.Length - 1) * 3) / n.Length; for (int i = 0; i < n.Length; i++, k++) { int kk = k; if (Ui.Button("sub" + tab + "_" + kk, n[i], x + i * (bw + 3), y + r * 20, bw, 18, "btnP", on: sub[tab] == kk, text: Font.Width(n[i]) > bw - 6 ? .85f : 1)) { if (sub[tab] != kk) { sub[tab] = kk; scroll = scrollT = 0; chip = 0; } } } }
        return sub[tab];
    }
    void Row(string id, string[] names, int cur, Action<int> set, float x, float y, float w, int cols)
    {
        float bw = (w - (cols - 1) * 3) / cols; for (int i = 0; i < names.Length; i++) { float bx = x + i % cols * (bw + 3), by = y + i / cols * 20; if (Ui.Button(id + i, names[i], bx, by, bw, 18, "btnP", on: cur == i, text: Font.Width(names[i]) > bw - 6 ? .8f : 1)) { Snap(); set(i); Changed(5); } }
    }
    void FaceTab(float x, float y, float w)
    {
        int s = Subs2([["Shape", "Eyes", "Brows", "Nose", "Mouth"], ["Skin", "Makeup", "Tattoos", "Piercings", "Beard"]], x, y, w); y += 44; float gh = 344 - (y - 6) - 120; int i;
        switch (s)
        {
            case 0:
                {
                    (string n, Func<float> g, Action<float> st)[] l = [("Face length", () => D.FaceLength, v => D.FaceLength = v), ("Jaw width", () => D.Jaw, v => D.Jaw = v), ("Chin", () => D.Chin, v => D.Chin = v), ("Cheekbones", () => D.Cheeks, v => D.Cheeks = v), ("Eye size", () => D.EyeSize, v => D.EyeSize = v), ("Eye spacing", () => D.EyeGap, v => D.EyeGap = v), ("Eye height", () => D.EyeHeight, v => D.EyeHeight = v), ("Brow height", () => D.BrowHeight, v => D.BrowHeight = v), ("Nose size", () => D.NoseSize, v => D.NoseSize = v), ("Nose width", () => D.NoseWidth, v => D.NoseWidth = v), ("Mouth width", () => D.MouthWidth, v => D.MouthWidth = v), ("Lips", () => D.LipSize, v => D.LipSize = v), ("Ears", () => D.EarSize, v => D.EarSize = v)];
                    for (int k = 0; k < l.Length; k++) Sl("fs" + k, l[k].n, l[k].g(), l[k].st, x, y + 2 + k * 17, w, 84);
                    if (Ui.Button("fsreset", "Reset shape", x, y + 2 + l.Length * 17 + 2, 90, 19, "btn")) { Snap(); D.Jaw = D.Chin = D.Cheeks = D.FaceLength = D.EyeSize = D.EyeGap = D.EyeHeight = D.BrowHeight = D.NoseSize = D.NoseWidth = D.MouthWidth = D.LipSize = D.EarSize = .5f; Changed(5); }
                    return;
                }
            case 1: i = Grid("eye", Catalog.Eyes.Length, k => D.Eye == k, k => FaceThumb("e", k, d => d.Eye = k), k => Catalog.Eyes[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Eye = i; Changed(5); } break;
            case 2: i = Grid("brow", Catalog.Brows.Length, k => D.Brow == k, k => FaceThumb("b", k, d => d.Brow = k), k => Catalog.Brows[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Brow = i; Changed(5); } break;
            case 3: i = Grid("nose", Catalog.Noses.Length, k => D.Nose == k, k => FaceThumb("n", k, d => d.Nose = k), k => Catalog.Noses[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Nose = i; Changed(5); } break;
            case 4: i = Grid("mouth", Catalog.Mouths.Length, k => D.Mouth == k, k => FaceThumb("m", k, d => d.Mouth = k), k => Catalog.Mouths[k], x, y + 4, w, gh); if (i >= 0) { Snap(); D.Mouth = i; Changed(5); } break;
            case 5: SkinPage(x, y, w); return;
            case 6: MakeupPage(x, y, w); return;
            case 7: TattooPage(x, y, w); return;
            case 8: PiercingPage(x, y, w); return;
            default:
                {
                    float bh = gh - 52; i = Grid("beard", Catalog.Beards.Length, k => D[Slot.Beard].Id == k, k => FaceThumb("d", k, d => d[Slot.Beard].Id = k), k => Catalog.Beards[k], x, y + 4, w, bh); if (i >= 0) { Snap(); D[Slot.Beard].Id = i; Changed(5); }
                    float by = y + bh + 13; Sl("bl", "Length", D.BeardLen, v => D.BeardLen = v, x, by, w / 2 - 4, 46); Sl("bt", "Thickness", D.BeardThick, v => D.BeardThick = v, x + w / 2 + 4, by, w / 2 - 4, 56); Sl("bg", "Grey", D.BeardGrey, v => D.BeardGrey = v, x, by + 17, w / 2 - 4, 46);
                    float cy2 = by + 38; Font.Draw("Beard color", x, cy2, Ui.Ink, 1.25f); Palette(AvatarData.HairCols, D[Slot.Beard].A, c => D[Slot.Beard].A = c, x, cy2 + 16, w, 5); return;
                }
        }
        float cy = y + gh + 14; if (s is 1 or 3) { Font.Draw("Eye color", x, cy, Ui.Ink, 1.25f); Palette(AvatarData.EyeCols, D.EyeCol, c => D.EyeCol = c, x, cy + 16, w, 5); }
        else if (s == 2) { Font.Draw("Brow color", x, cy, Ui.Ink, 1.25f); if (Ui.Button("browmatch", "Match hair", x + w - 76, cy - 3, 76, 17, "btnP", on: D.BrowCol == 0, text: .9f)) { Snap(); D.BrowCol = 0; Changed(5); } Palette(AvatarData.HairCols, D.BrowCol, c => D.BrowCol = c == 0 ? 1 : c, x, cy + 16, w, 5); }
        else { Font.Draw("Lip color", x, cy, Ui.Ink, 1.25f); Palette([0xb5605a, 0xc87a70, 0xa04a4a, 0x8c2f35, 0xd86f86, 0xe3a1a1, 0x7a3a34, 0x5a2a28, 0x9c4a7a, 0xb8473f, 0xd8a090, 0x6a3a3a], D.Lip, c => D.Lip = c, x, cy + 16, w, 5); }
    }
    void SkinPage(float x, float y, float w)
    {
        Sl("sk0", "Freckles", D.Freckles, v => D.Freckles = v, x, y + 2, w); Sl("sk1", "Wrinkles", D.Wrinkles, v => D.Wrinkles = v, x, y + 19, w); Sl("sk2", "Laugh lines", D.LaughLines, v => D.LaughLines = v, x, y + 36, w); Sl("sk3", "Under eyes", D.UnderEyes, v => D.UnderEyes = v, x, y + 53, w); Sl("sk4", "Vitiligo", D.Vitiligo, v => D.Vitiligo = v, x, y + 70, w);
        float yy = y + 90; Font.Draw("Scar", x + 2, yy + 4, Ui.Ink); Row("scar", Catalog.Scars, D.Scar, v => D.Scar = v, x + 52, yy, w - 52, 4); yy += 44; Font.Draw("Birthmark", x + 2, yy + 4, Ui.Ink); Row("bmk", Catalog.Birthmarks, D.Birthmark, v => D.Birthmark = v, x + 52, yy, w - 52, 3); yy += 44;
        Font.Draw("Marks", x + 2, yy + 4, Ui.Ink); Row("mrk", Catalog.Marks, D.Marks, v => D.Marks = v, x + 52, yy, w - 52, 4); yy += 46;
        Font.Draw("Moles: click your character's face to add or remove one.", x + 2, yy, Ui.Soft); if (D.Moles.Count > 0 && Ui.Button("molesclr", "Clear moles", x + w - 80, yy + 12, 80, 18, "btn", text: .9f)) { Snap(); D.Moles.Clear(); Changed(5); }
    }
    /// Clicking the big character while the skin page is open places a mole there, or takes one away.
    void MoleClick(float x0, float y0, float sx, float sy)
    {
        if (tab != 1 || sub[1] != 5 || !Ui.Click || finishT >= 0) return; float px = (Ui.Mouse.X - x0) / sx, py = (Ui.Mouse.Y - y0) / sy; float hx = (px - headC.X) / headRx, hy = (py - headC.Y) / headRy; if (hx * hx + hy * hy > 1.05f) return;
        for (int i = 0; i + 1 < D.Moles.Count; i += 2) if (MathF.Abs(D.Moles[i] - hx) < .1f && MathF.Abs(D.Moles[i + 1] - hy) < .1f) { Snap(); D.Moles.RemoveRange(i, 2); Snd.Play("undo", .6f, 1.3f); Changed(-1, false); return; }
        if (D.Moles.Count >= 24) { Snd.Play("error", .5f); return; } Snap(); D.Moles.Add(MathF.Round(hx, 3)); D.Moles.Add(MathF.Round(hy, 3)); Snd.Play("pick", .6f, 1.4f); Changed(-1, false);
    }
    void MakeupPage(float x, float y, float w)
    {
        Font.Draw("Eyeliner", x + 2, y + 4, Ui.Ink); Row("liner", Catalog.Liners, D.Liner, v => D.Liner = v, x + 52, y, w - 52, 3); float yy = y + 46;
        Sl("mk0", "Eyeshadow", D.Shadow, v => D.Shadow = v, x, yy, w); Sl("mk1", "Lipstick", D.Lipstick, v => D.Lipstick = v, x, yy + 17, w); Sl("mk2", "Blush", D.Blush, v => D.Blush = v, x, yy + 34, w); yy += 56;
        string[] n = ["Liner", "Shadow", "Lips", "Blush", "Nails"]; int[] c = [D.LinerCol, D.ShadowCol, D.LipstickCol, D.BlushCol, D.Nails]; for (int k = 0; k < 5; k++) Chip("mkc" + k, n[k], c[k] == 0 ? 0xe8d8c0 : c[k], x + k * 59, yy, chip == k, () => chip = k, 57);
        if (chip == 4 && Ui.Button("nailsoff", "Bare nails", x + w - 74, yy + 22, 74, 17, "btnP", on: D.Nails == 0, text: .9f)) { Snap(); D.Nails = 0; Changed(-1); }
        Palette(Makeup, c[Math.Min(chip, 4)], v => { switch (chip) { case 0: D.LinerCol = v; if (D.Liner == 0) D.Liner = 1; break; case 1: D.ShadowCol = v; if (D.Shadow < .05f) D.Shadow = .5f; break; case 2: D.LipstickCol = v; if (D.Lipstick < .05f) D.Lipstick = .7f; break; case 3: D.BlushCol = v; if (D.Blush < .05f) D.Blush = .5f; break; default: D.Nails = v == 0 ? 1 : v; break; } }, x, yy + 42, w, 5);
    }
    int tatSpot;
    void TattooPage(float x, float y, float w)
    {
        string[] sp = ["L arm", "R arm", "L upper", "R upper", "Neck", "Face", "Hands"]; float bw = (w - 6 * 3) / 7; for (int k = 0; k < 7; k++) { bool has = D.Tattoos[k] > 0; if (Ui.Button("tsp" + k, sp[k], x + k * (bw + 3), y, bw, 18, "btnP", on: tatSpot == k, text: .8f)) tatSpot = k; if (has) Raylib.DrawCircleV(new(x + k * (bw + 3) + bw - 4, y + 3), 2.4f, Ui.Green); }
        float gh = 344 - (y + 22 - 6) - 92; Col ink = D.TatCol; bool col = D.TatColor; int sk = D.Skin;
        int i = Grid("tat" + tatSpot, Catalog.TattooDesigns.Length, k => D.Tattoos[tatSpot] == k, k => Thumb("tat" + k, HashCode.Combine(D.TatCol, D.TatColor, D.Skin), p => { p.View(1, 0, 0); p.Begin().Ellipse(48, 44, 30, 30); p.Shade(new Ink(sk, 6, .4f, .3f) { Ao = 0 }); if (k == 0) return; if (k is 11 or 12) { for (int q = 0; q < 6; q++) p.Curve(new(26, 22 + q * 9), new(48, 14 + q * 9), new(70, 22 + q * 9), 2, q % 2 == 0 && col ? 0xc8384a : ink, true); return; } AvatarPainter.TattooArt(p, k, (u, v) => new Vector2(48 + v * 7, 44 + u * 7), 7, ink, col); }), k => Catalog.TattooDesigns[k], x, y + 26, w, gh);
        if (i >= 0) { Snap(); D.Tattoos[tatSpot] = i; Changed(tatSpot < 4 || tatSpot == 6 ? 1 : 5); }
        float cy = y + 26 + gh + 12; Font.Draw("Ink", x, cy, Ui.Ink, 1.25f); bool c2 = D.TatColor; Font.Draw("Colored", x + w - 70, cy + 1, Ui.Soft); if (Ui.Toggle("tatcol", ref c2, x + w - 28, cy - 2)) { Snap(); D.TatColor = c2; Changed(-1); } Palette(Inks, D.TatCol, v => D.TatCol = v, x, cy + 16, w, -1, false);
        Font.Draw("Arm tattoos show when sleeves are short.", x, cy + 40, Ui.Soft, .9f);
    }
    void PiercingPage(float x, float y, float w)
    {
        float bw = (w - 6) / 3; for (int k = 0; k < Catalog.Piercings.Length; k++) { bool on = (D.Piercings & (1 << k)) != 0; if (Ui.Button("prc" + k, Catalog.Piercings[k], x + k % 3 * (bw + 3), y + 2 + k / 3 * 22, bw, 19, "btnP", on: on)) { Snap(); D.Piercings ^= 1 << k; Changed(5); } }
        float cy = y + 2 + 4 * 22 + 12; Font.Draw("Metal", x, cy, Ui.Ink, 1.25f); Palette(Metals, D.Metal, v => D.Metal = v, x, cy + 16, w, 5, false); Font.Draw("Earrings are under Extras.", x, cy + 44, Ui.Soft, .9f);
    }
    void HairShapePage(float x, float y, float w)
    {
        var h = D.Hair; Font.Draw("Hairline", x + 2, y + 4, Ui.Ink); Row("hl", Catalog.Hairlines, h.Hairline, v => h.Hairline = v, x + 52, y, w - 52, 4); float yy = y + 48;
        Sl("hlen", "Length", h.Length, v => h.Length = v, x, yy, w, 60); Sl("hcurl", "Curl", h.Curl, v => h.Curl = v, x, yy + 17, w, 60); Font.Draw("Straighter", x + 60, yy + 29, Ui.Soft, .8f); Font.Draw("Curlier", x + w - 4, yy + 29, Ui.Soft, .8f, 1);
        yy += 44; Font.Draw("Flip the part", x + 2, yy + 1, Ui.Ink); bool f = h.Flip; if (Ui.Toggle("hflip", ref f, x + 96, yy - 2)) { Snap(); h.Flip = f; Changed(5); }
        yy += 24; Font.Draw("Brow color", x, yy, Ui.Ink, 1.25f); if (Ui.Button("browmatch2", "Match hair", x + w - 76, yy - 3, 76, 17, "btnP", on: D.BrowCol == 0, text: .9f)) { Snap(); D.BrowCol = 0; Changed(5); } Palette(AvatarData.HairCols, D.BrowCol, c => D.BrowCol = c == 0 ? 1 : c, x, yy + 16, w, 5);
    }

    // ---------------------------------------------------------------- typing exact sizes
    int numEdit = -1; string numText = "";
    static readonly string[] MeasureNames = ["Height", "Weight", "Shoulders", "Waist", "Hips", "Arm length", "Leg length", "Head size"];
    float MeasureNum(AvatarData d, int i)
    {
        var r = new Rig(d); const float inch = 67f / 95.2f;
        return i switch { 0 => r.Total * inch, 1 => Rig.Pounds(d), 2 => r.ShX * 2 * inch, 3 => r.Waist * inch * 5.8f, 4 => r.Hip * inch * 6.0f, 5 => (r.UArm + r.LArm + r.HandR * 2.4f) * inch, 6 => (r.Thigh + r.Shin + r.AnkleH) * inch, _ => r.HeadRx * inch * 4.4f };
    }
    /// Reads 5'10", 5 10, 70, 178cm, 165, 75kg... into inches or pounds.
    static float? Parse(string s, bool weight)
    {
        s = s.Trim().ToLowerInvariant().Replace("”", "\"").Replace("’", "'"); if (s.Length == 0) return null; bool cm = s.EndsWith("cm"), kg = s.EndsWith("kg");
        var nums = System.Text.RegularExpressions.Regex.Matches(s, @"\d+(\.\d+)?").Select(m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList(); if (nums.Count == 0) return null;
        if (weight) return kg ? nums[0] * 2.2046f : nums[0];
        if (cm) return nums[0] / 2.54f; if (nums.Count >= 2) return nums[0] * 12 + nums[1]; if (s.Contains('\'') || s.Contains("ft") || nums[0] <= 8) return nums[0] * 12; if (nums[0] > 100) return nums[0] / 2.54f; return nums[0];
    }
    /// Moves one body slider until the readout matches what was typed. Returns false when it is out of reach.
    bool SetMeasure(int i, float want)
    {
        Func<AvatarData, float> get = i switch { 0 => q => q.Height, 1 => q => q.Weight, 2 => q => q.Shoulder, 3 => q => q.Waist, 4 => q => q.Hip, 5 => q => q.Arm, 6 => q => q.Leg, _ => q => q.Head }; Action<AvatarData, float> put = i switch { 0 => (q, v) => q.Height = v, 1 => (q, v) => q.Weight = v, 2 => (q, v) => q.Shoulder = v, 3 => (q, v) => q.Waist = v, 4 => (q, v) => q.Hip = v, 5 => (q, v) => q.Arm = v, 6 => (q, v) => q.Leg = v, _ => (q, v) => q.Head = v };
        var t = D.Clone(); float lo = 0, hi = 1; put(t, 0); float a = MeasureNum(t, i); put(t, 1); float b = MeasureNum(t, i); bool up = b > a; float min = MathF.Min(a, b), max = MathF.Max(a, b);
        for (int k = 0; k < 30; k++) { float m = (lo + hi) / 2; put(t, m); if ((MeasureNum(t, i) < want) == up) lo = m; else hi = m; } Snap(); put(D, (lo + hi) / 2); Changed(6); return want >= min - .5f && want <= max + .5f;
    }
    void NumTyping()
    {
        int c; while ((c = Raylib.GetCharPressed()) > 0) if (numText.Length < 9 && (char.IsAsciiDigit((char)c) || c is '\'' or '"' or ' ' or '.' or 'c' or 'm' or 'k' or 'g' or 'f' or 't')) { numText += (char)c; Snd.Play("type", .7f, 1.1f); }
        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && numText.Length > 0) numText = numText[..^1];
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { numEdit = -1; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) { var v = Parse(numText, numEdit == 1); if (v is float f) { if (!SetMeasure(numEdit, f)) { shake = .3f; Snd.Play("error", .5f); } else Snd.Play("confirm", .5f); } numEdit = -1; }
    }
    /// The readout next to a body slider: click it to type an exact size.
    void Readout(int i, float x, float y, string shown)
    {
        float w = 46; var (a, hit, over) = Ui.Widget("ro" + i, x - w, y - 3, w + 2, 14, null, true);
        if (numEdit == i) { Raylib.DrawRectangleRounded(new Rectangle(x - w, y - 3, w + 2, 14), .4f, 4, new Color(255, 250, 236, 255)); Raylib.DrawRectangleRoundedLinesEx(new Rectangle(x - w, y - 3, w + 2, 14), .4f, 4, 1, Ui.Orange); string t = numText + ((int)(Ui.T * 2) % 2 == 0 ? "|" : " "); Font.Draw(t, x - 1, y, Ui.Ink, 1, 1); return; }
        if (over) { Raylib.DrawRectangleRounded(new Rectangle(x - w, y - 3, w + 2, 14), .4f, 4, new Color(255, 240, 210, 255)); Ui.Tip = i == 0 ? "Type a height, like 5'10\" or 178cm" : i == 1 ? "Type a weight in pounds, or kg" : "Type a size in inches"; }
        Font.Draw(shown, x - 1, y, over ? Ui.Orange : Ui.Ink, 1, 1); if (hit) { numEdit = i; numText = ""; typing = false; while (Raylib.GetCharPressed() > 0) { } Snd.Play("tick", .6f); }
    }
}

using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CC2CheatGUI.UI;

/// <summary>
/// Carrier Command 2's naval-command-console visual language, distilled from the game's own
/// source palette (rom_0/scripts/library_util.lua) and shaders. Flat black panels, 1px hard
/// borders, inverted (bright-fill / black-text) header bars, a single holographic-cyan accent,
/// semantic status colors, and the game's own LanaPixel bitmap font.
/// </summary>
public static class Cc2Theme
{
    // ---- palette ----
    public static readonly Color Black       = C(0x00, 0x00, 0x00); // console black
    public static readonly Color Screen      = C(0x05, 0x07, 0x0B); // cool-lifted black (form backdrop)
    public static readonly Color PanelBorder = C(0x25, 0x2B, 0x2B); // resting panel border
    public static readonly Color Grid        = C(0x10, 0x18, 0x18); // hairline dividers / grid lines
    public static readonly Color PanelDark   = C(0x10, 0x10, 0x10); // section-header bar, inactive
    public static readonly Color MidGrey     = C(0x6A, 0x77, 0x77); // secondary/label text
    public static readonly Color DimGrey     = C(0x3F, 0x3F, 0x3F); // disabled text / faint outline
    public static readonly Color White       = C(0xEC, 0xFB, 0xFB); // primary bright text
    public static readonly Color PureWhite   = C(0xFF, 0xFF, 0xFF);
    public static readonly Color ButtonBg    = C(0x08, 0x14, 0x14); // teal-tinted near-black button
    public static readonly Color ButtonHover = C(0x10, 0x40, 0x40); // highlight teal
    public static readonly Color ButtonDown  = C(0x18, 0x5A, 0x5A);
    public static readonly Color Cyan        = C(0x10, 0xFF, 0xFF); // friendly / active accent
    public static readonly Color CyanDim     = C(0x0A, 0x8C, 0x8C);
    public static readonly Color Green       = C(0x10, 0xFF, 0x7F); // OK / positive
    public static readonly Color Red         = C(0xFF, 0x30, 0x30); // bad / enemy / destructive
    public static readonly Color Yellow      = C(0xFF, 0xE0, 0x10); // warning
    public static readonly Color Orange      = C(0xFF, 0x80, 0x00); // industry / logistics
    public static readonly Color GridBlue    = C(0x00, 0x7B, 0xC9);
    public static readonly Color GlowCyan    = C(0x40, 0xCC, 0xFF);

    // ---- fonts ----
    private static readonly PrivateFontCollection _pfc = new();
    public static FontFamily PixelFamily { get; }
    public static FontFamily MonoFamily { get; }

    public static Font Nav        { get; }
    public static Font PixelSmall { get; }
    public static Font PixelBody  { get; }
    public static Font PixelHead  { get; }
    public static Font PixelTitle { get; }
    public static Font PixelHuge  { get; }
    public static Font Data       { get; }
    public static Font DataBold   { get; }

    static Cc2Theme()
    {
        PixelFamily = LoadEmbeddedFont("CC2CheatGUI.Assets.lanapixel.ttf") ?? FontFamily.GenericMonospace;

        // A crisp, always-available monospace for dense numeric data (grids / inputs).
        MonoFamily = FirstAvailable("Cascadia Mono", "Consolas", "Lucida Console") ?? FontFamily.GenericMonospace;

        // LanaPixel is pixel-perfect at 11px; 8.25pt ≈ 11px at 96dpi. Multiples stay crisp.
        PixelSmall = new Font(PixelFamily, 8.25f, FontStyle.Regular, GraphicsUnit.Point);
        Nav        = new Font(PixelFamily, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        PixelBody  = new Font(PixelFamily, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        PixelHead  = new Font(PixelFamily, 11.25f, FontStyle.Regular, GraphicsUnit.Point);
        PixelTitle = new Font(PixelFamily, 15f, FontStyle.Regular, GraphicsUnit.Point);
        PixelHuge  = new Font(PixelFamily, 27f, FontStyle.Regular, GraphicsUnit.Point);
        Data       = new Font(MonoFamily, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        DataBold   = new Font(MonoFamily, 9.75f, FontStyle.Bold, GraphicsUnit.Point);
    }

    private static FontFamily? LoadEmbeddedFont(string resource)
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
            if (stream == null) return null;
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                _pfc.AddMemoryFont(handle.AddrOfPinnedObject(), bytes.Length);
                return _pfc.Families.Length > 0 ? _pfc.Families[^1] : null;
            }
            finally { handle.Free(); }
        }
        catch { return null; }
    }

    private static FontFamily? FirstAvailable(params string[] names)
    {
        foreach (var n in names)
        {
            try { return new FontFamily(n); } catch { }
        }
        return null;
    }

    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    // ---- drawing helpers ----

    /// <summary>Crisp, un-antialiased pixel text (the game's rendering convention).</summary>
    public static void DrawPixelText(Graphics g, string text, Font font, Color color, int x, int y,
        bool shadow = false)
    {
        var prev = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        if (shadow)
            using (var sb = new SolidBrush(Black))
                g.DrawString(text, font, sb, x + 1, y + 1);
        using (var b = new SolidBrush(color))
            g.DrawString(text, font, b, x, y);
        g.TextRenderingHint = prev;
    }

    /// <summary>An inverted header bar: a bright filled rectangle with black text punched out of it.</summary>
    public static void DrawInvertedHeader(Graphics g, Rectangle bar, string text, Font font,
        Color fill, Color? textColor = null)
    {
        using (var b = new SolidBrush(fill)) g.FillRectangle(b, bar);
        var tc = textColor ?? Black;
        var prev = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        var sz = g.MeasureString(text, font);
        int ty = bar.Y + (bar.Height - (int)sz.Height) / 2;
        using (var tb = new SolidBrush(tc)) g.DrawString(text, font, tb, bar.X + 6, ty);
        g.TextRenderingHint = prev;
    }
}

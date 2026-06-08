using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// ============================================================
// Premium installer bitmaps — Starkive v1.1.0
//
// Design: Blue left sidebar (confident brand) + pure-white
//         right panel (WiX renders its black text here).
// No star, no gradients, no decorations — clean typography only.
// ============================================================

class GenBitmaps
{
    // Palette
    static readonly Color Blue      = Color.FromArgb(0x1A, 0x56, 0xDB);  // brand blue
    static readonly Color BlueMid   = Color.FromArgb(0x16, 0x4E, 0xC8);  // 5% darker, for subtle depth
    static readonly Color BlueLight = Color.FromArgb(0x93, 0xC5, 0xFD);  // pale blue for sub-text
    static readonly Color White     = Color.FromArgb(0xFF, 0xFF, 0xFF);
    static readonly Color OffWhite  = Color.FromArgb(0xF9, 0xFA, 0xFB);  // right panel bg
    static readonly Color TextDark  = Color.FromArgb(0x1E, 0x29, 0x3B);  // near-black, for wordmark

    static void Main()
    {
        string outDir = Path.Combine("..", "Installer");
        Directory.CreateDirectory(outDir);
        MakeDialog(Path.Combine(outDir, "dialog.bmp"));
        MakeBanner(Path.Combine(outDir, "banner.bmp"));
        Console.WriteLine("Done.");
    }

    // ── Dialog  493 × 312 ─────────────────────────────────────────────────────
    // Left  0–150  : Blue brand sidebar — confident, clean
    // Right 150–493: Pure white — WiX renders its black title/body text here
    static void MakeDialog(string path)
    {
        const int W = 493, H = 312;
        const int Split = 150;

        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // ── Right panel: white — never put dark colour here ─────────────────
        g.FillRectangle(new SolidBrush(OffWhite), Split, 0, W - Split, H);

        // ── Left sidebar: flat brand blue, no gradients ─────────────────────
        g.FillRectangle(new SolidBrush(Blue), 0, 0, Split, H);

        // Very subtle inner-right shadow on blue panel (1 px dark line)
        g.FillRectangle(new SolidBrush(Color.FromArgb(30, TextDark)), Split - 1, 0, 1, H);

        // ── Wordmark centred vertically in blue panel ───────────────────────
        int cx = Split / 2;   // horizontal centre of sidebar = 87 px

        using var fName = new Font("Segoe UI", 22, FontStyle.Bold,    GraphicsUnit.Pixel);
        using var fSub  = new Font("Segoe UI",  9, FontStyle.Regular, GraphicsUnit.Pixel);
        using var fVer  = new Font("Segoe UI",  8, FontStyle.Regular, GraphicsUnit.Pixel);

        // Product name — white, centred on blue panel
        SizeF nameSize = g.MeasureString("Starkive", fName);
        float nameX    = cx - nameSize.Width  / 2;
        float nameY    = H / 2f - nameSize.Height / 2 - 10;
        g.DrawString("Starkive", fName, new SolidBrush(White), nameX, nameY);

        // Thin white rule below name (40% opacity)
        float ruleY = nameY + nameSize.Height + 8;
        using var rulePen = new Pen(Color.FromArgb(100, White), 1);
        g.DrawLine(rulePen, 20, ruleY, Split - 20, ruleY);

        // Domain sub-label
        SizeF subSize = g.MeasureString("starkive.app", fSub);
        g.DrawString("starkive.app", fSub,
            new SolidBrush(BlueLight),
            cx - subSize.Width / 2, ruleY + 8);

        // Version — bottom of panel
        SizeF verSize = g.MeasureString("v 1.1.0", fVer);
        g.DrawString("v 1.1.0", fVer,
            new SolidBrush(Color.FromArgb(120, White)),
            cx - verSize.Width / 2, H - 20);

        // Right panel is intentionally blank — WiX renders its own title/body text here.

        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }

    // ── Banner  493 × 58 ──────────────────────────────────────────────────────
    // WiX renders its own page title (e.g. "License Agreement") ON TOP of this
    // bitmap at ~y=9. No text here — clean blue surface so WiX title is legible.
    static void MakeBanner(string path)
    {
        const int W = 493, H = 58;

        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);

        // Solid brand blue — matches dialog left sidebar
        g.FillRectangle(new SolidBrush(Blue), 0, 0, W, H);

        // Thin bottom separator
        g.FillRectangle(new SolidBrush(Color.FromArgb(40, TextDark)), 0, H - 1, W, 1);

        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// ============================================================
//  Starkive installer bitmaps — WiX + InnoSetup
// ============================================================

class GenBitmaps
{
    // ── Palette ──────────────────────────────────────────────
    static readonly Color Blue      = Color.FromArgb(0x1A, 0x56, 0xDB);
    static readonly Color BlueDark  = Color.FromArgb(0x0D, 0x1A, 0x4A);
    static readonly Color BlueLight = Color.FromArgb(0x93, 0xC5, 0xFD);
    static readonly Color White     = Color.FromArgb(0xFF, 0xFF, 0xFF);
    static readonly Color OffWhite  = Color.FromArgb(0xF9, 0xFA, 0xFB);
    static readonly Color TextDark  = Color.FromArgb(0x1E, 0x29, 0x3B);

    static void Main()
    {
        // Resolve Installer/ relative to this source file's location so the
        // output path is correct regardless of the working directory.
        string thisDir = AppContext.BaseDirectory;
        // BaseDirectory is  …/Tools/bin/…  — walk up to find Installer/
        string? root = thisDir;
        while (root != null && !Directory.Exists(Path.Combine(root, "Installer")))
            root = Directory.GetParent(root)?.FullName;
        string outDir = root != null
            ? Path.Combine(root, "Installer")
            : Path.Combine("..", "Installer");
        Directory.CreateDirectory(outDir);

        // WiX bitmaps (kept for reference / fallback)
        MakeWixDialog(Path.Combine(outDir, "dialog.bmp"));
        MakeWixBanner(Path.Combine(outDir, "banner.bmp"));

        // InnoSetup modern wizard bitmaps
        MakeInnoSide(Path.Combine(outDir, "wizard_side.bmp"));
        MakeInnoBanner(Path.Combine(outDir, "wizard_banner.bmp"));

        Console.WriteLine("Done.");
    }

    // ── InnoSetup: side panel  164 × 314  (shown on Welcome + Finish pages) ──
    // Modern style: dark blue gradient left panel, Starkive branding, compass mark
    static void MakeInnoSide(string path)
    {
        const int W = 164, H = 314;

        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background: vertical gradient dark navy → brand blue
        using var bgBrush = new LinearGradientBrush(
            new Point(0, 0), new Point(0, H),
            Color.FromArgb(0x0A, 0x10, 0x2A),   // very dark navy at top
            Color.FromArgb(0x12, 0x3A, 0x8A));   // mid blue at bottom
        g.FillRectangle(bgBrush, 0, 0, W, H);

        // Subtle diagonal overlay for depth
        using var overlayBrush = new LinearGradientBrush(
            new Point(0, 0), new Point(W, H),
            Color.FromArgb(15, White),
            Color.FromArgb(0, White));
        g.FillRectangle(overlayBrush, 0, 0, W, H);

        // ── Compass mark (centered, upper third) ─────────────────────────────
        int cx = W / 2;
        int markCY = 95;
        DrawCompassMark(g, cx, markCY, 38);

        // Thin divider below mark
        using var divPen = new Pen(Color.FromArgb(60, White), 1);
        g.DrawLine(divPen, 24, markCY + 52, W - 24, markCY + 52);

        // ── Product name ─────────────────────────────────────────────────────
        using var fName = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
        SizeF nameSize = g.MeasureString("Starkive", fName);
        g.DrawString("Starkive", fName, new SolidBrush(White),
            cx - nameSize.Width / 2, markCY + 62);

        // ── Tagline ───────────────────────────────────────────────────────────
        using var fTag = new Font("Segoe UI", 9, FontStyle.Regular, GraphicsUnit.Pixel);
        string tag = "Zip. Encrypt. Stay safe.";
        SizeF tagSize = g.MeasureString(tag, fTag);
        g.DrawString(tag, fTag, new SolidBrush(BlueLight),
            cx - tagSize.Width / 2, markCY + 62 + nameSize.Height + 4);

        // ── Version (bottom) ─────────────────────────────────────────────────
        using var fVer = new Font("Segoe UI", 8, FontStyle.Regular, GraphicsUnit.Pixel);
        string ver = "v 1.2.0";
        SizeF verSize = g.MeasureString(ver, fVer);
        g.DrawString(ver, fVer,
            new SolidBrush(Color.FromArgb(110, White)),
            cx - verSize.Width / 2, H - 22);

        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }

    // ── InnoSetup: top banner  55 × 55  (inner pages, top-right corner) ──────
    static void MakeInnoBanner(string path)
    {
        const int W = 55, H = 55;

        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // White background (InnoSetup inner pages are white)
        g.FillRectangle(new SolidBrush(White), 0, 0, W, H);

        // Draw compass mark centred
        DrawCompassMark(g, W / 2, H / 2, 20);

        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }

    // ── Compass mark helper ───────────────────────────────────────────────────
    static void DrawCompassMark(Graphics g, int cx, int cy, int r)
    {
        // Outer glow circle
        using var glowBrush = new SolidBrush(Color.FromArgb(25, Blue));
        g.FillEllipse(glowBrush, cx - r - 4, cy - r - 4, (r + 4) * 2, (r + 4) * 2);

        // Dark circle background
        using var bgBrush = new SolidBrush(Color.FromArgb(0x08, 0x0E, 0x1C));
        g.FillEllipse(bgBrush, cx - r, cy - r, r * 2, r * 2);

        // Circle border
        using var borderPen = new Pen(Color.FromArgb(0x1A, 0x3A, 0x6E), 1f);
        g.DrawEllipse(borderPen, cx - r, cy - r, r * 2, r * 2);

        // Cardinal spikes
        float spike = r * 0.85f;
        float body  = r * 0.28f;
        float wide  = r * 0.14f;

        PointF[] north = { new(cx, cy - spike), new(cx + wide, cy - body), new(cx - wide, cy - body) };
        PointF[] south = { new(cx, cy + spike), new(cx + wide, cy + body), new(cx - wide, cy + body) };
        PointF[] west  = { new(cx - spike, cy), new(cx - body, cy - wide), new(cx - body, cy + wide) };
        PointF[] east  = { new(cx + spike, cy), new(cx + body, cy - wide), new(cx + body, cy + wide) };

        using var blueBrush = new SolidBrush(Blue);
        g.FillPolygon(blueBrush, north);
        g.FillPolygon(blueBrush, south);
        g.FillPolygon(blueBrush, west);
        g.FillPolygon(blueBrush, east);

        // Diagonal half-spikes (slightly dimmer)
        float ds = r * 0.55f;
        float db = r * 0.22f;
        using var dimBrush = new SolidBrush(Color.FromArgb(0xCC, 0x2D, 0x6F, 0xD4));
        PointF[] ne = { new(cx + ds * 0.71f, cy - ds * 0.71f), new(cx + db, cy - db * 0.2f), new(cx + db * 0.2f, cy - db) };
        PointF[] nw = { new(cx - ds * 0.71f, cy - ds * 0.71f), new(cx - db, cy - db * 0.2f), new(cx - db * 0.2f, cy - db) };
        PointF[] se = { new(cx + ds * 0.71f, cy + ds * 0.71f), new(cx + db, cy + db * 0.2f), new(cx + db * 0.2f, cy + db) };
        PointF[] sw = { new(cx - ds * 0.71f, cy + ds * 0.71f), new(cx - db, cy + db * 0.2f), new(cx - db * 0.2f, cy + db) };
        g.FillPolygon(dimBrush, ne);
        g.FillPolygon(dimBrush, nw);
        g.FillPolygon(dimBrush, se);
        g.FillPolygon(dimBrush, sw);

        // Centre dot
        float dotR = r * 0.12f;
        g.FillEllipse(new SolidBrush(Color.FromArgb(0x5B, 0xA3, 0xF5)),
            cx - dotR, cy - dotR, dotR * 2, dotR * 2);
    }

    // ── WiX dialog  493 × 312 ────────────────────────────────────────────────
    static void MakeWixDialog(string path)
    {
        const int W = 493, H = 312, Split = 150;
        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.FillRectangle(new SolidBrush(OffWhite), Split, 0, W - Split, H);
        g.FillRectangle(new SolidBrush(Blue), 0, 0, Split, H);
        g.FillRectangle(new SolidBrush(Color.FromArgb(30, TextDark)), Split - 1, 0, 1, H);
        int cx = Split / 2;
        using var fName = new Font("Segoe UI", 22, FontStyle.Bold,    GraphicsUnit.Pixel);
        using var fSub  = new Font("Segoe UI",  9, FontStyle.Regular, GraphicsUnit.Pixel);
        using var fVer  = new Font("Segoe UI",  8, FontStyle.Regular, GraphicsUnit.Pixel);
        SizeF nameSize = g.MeasureString("Starkive", fName);
        float nameX = cx - nameSize.Width / 2;
        float nameY = H / 2f - nameSize.Height / 2 - 10;
        g.DrawString("Starkive", fName, new SolidBrush(White), nameX, nameY);
        float ruleY = nameY + nameSize.Height + 8;
        using var rulePen = new Pen(Color.FromArgb(100, White), 1);
        g.DrawLine(rulePen, 20, ruleY, Split - 20, ruleY);
        SizeF subSize = g.MeasureString("starkive.app", fSub);
        g.DrawString("starkive.app", fSub, new SolidBrush(BlueLight), cx - subSize.Width / 2, ruleY + 8);
        SizeF verSize = g.MeasureString("v 1.2.0", fVer);
        g.DrawString("v 1.2.0", fVer, new SolidBrush(Color.FromArgb(120, White)), cx - verSize.Width / 2, H - 20);
        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }

    // ── WiX banner  493 × 58 ─────────────────────────────────────────────────
    static void MakeWixBanner(string path)
    {
        const int W = 493, H = 58;
        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.FillRectangle(new SolidBrush(Blue), 0, 0, W, H);
        g.FillRectangle(new SolidBrush(Color.FromArgb(40, TextDark)), 0, H - 1, W, 1);
        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }
}

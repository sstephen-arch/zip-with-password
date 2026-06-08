using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// ============================================================
//  Starkive installer bitmaps + app.ico — WiX + InnoSetup
// ============================================================

class GenBitmaps
{
    // ── Palette ──────────────────────────────────────────────
    static readonly Color Blue      = Color.FromArgb(0x1A, 0x56, 0xDB);
    static readonly Color BlueLight = Color.FromArgb(0x93, 0xC5, 0xFD);
    static readonly Color White     = Color.FromArgb(0xFF, 0xFF, 0xFF);
    static readonly Color OffWhite  = Color.FromArgb(0xF9, 0xFA, 0xFB);
    static readonly Color TextDark  = Color.FromArgb(0x1E, 0x29, 0x3B);
    static readonly Color IconBg    = Color.FromArgb(0xFF, 0x0A, 0x0E, 0x1A);

    static void Main()
    {
        string thisDir = AppContext.BaseDirectory;
        string? root   = thisDir;
        while (root != null && !Directory.Exists(Path.Combine(root, "Installer")))
            root = Directory.GetParent(root)?.FullName;
        string outDir = root != null
            ? Path.Combine(root, "Installer")
            : Path.Combine("..", "Installer");

        // Starkive source folder (for app.ico)
        string? srcRoot = root;
        string? appDir  = srcRoot != null && Directory.Exists(Path.Combine(srcRoot, "Starkive"))
            ? Path.Combine(srcRoot, "Starkive")
            : null;

        Directory.CreateDirectory(outDir);

        // WiX bitmaps
        MakeWixDialog(Path.Combine(outDir, "dialog.bmp"));
        MakeWixBanner(Path.Combine(outDir, "banner.bmp"));

        // InnoSetup wizard bitmaps
        MakeInnoSide(Path.Combine(outDir, "wizard_side.bmp"));
        MakeInnoBanner(Path.Combine(outDir, "wizard_banner.bmp"));

        // App icon
        string icoPath = appDir != null
            ? Path.Combine(appDir, "app.ico")
            : Path.Combine(outDir, "app.ico");
        MakeAppIcon(icoPath);

        Console.WriteLine("Done.");
    }

    // ── InnoSetup: side panel  164 × 314 ─────────────────────────────────────
    static void MakeInnoSide(string path)
    {
        const int W = 164, H = 314;
        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Dark navy gradient background
        using var bg = new LinearGradientBrush(
            new Point(0, 0), new Point(0, H),
            Color.FromArgb(0x0A, 0x10, 0x2A),
            Color.FromArgb(0x12, 0x3A, 0x8A));
        g.FillRectangle(bg, 0, 0, W, H);

        // Wheel mark centred in upper half
        int cx = W / 2, markCY = 95;
        DrawWheelMark(g, cx, markCY, 36);

        // Thin divider
        using var div = new Pen(Color.FromArgb(55, White), 1);
        g.DrawLine(div, 24, markCY + 50, W - 24, markCY + 50);

        // Product name
        using var fName = new Font("Segoe UI", 20, FontStyle.Bold, GraphicsUnit.Pixel);
        SizeF ns = g.MeasureString("Starkive", fName);
        g.DrawString("Starkive", fName, new SolidBrush(White),
            cx - ns.Width / 2, markCY + 60);

        // Tagline
        using var fTag = new Font("Segoe UI", 9, FontStyle.Regular, GraphicsUnit.Pixel);
        string tag = "Zip. Encrypt. Stay safe.";
        SizeF ts = g.MeasureString(tag, fTag);
        g.DrawString(tag, fTag, new SolidBrush(BlueLight),
            cx - ts.Width / 2, markCY + 60 + ns.Height + 4);

        // Version
        using var fVer = new Font("Segoe UI", 8, FontStyle.Regular, GraphicsUnit.Pixel);
        string ver = "v 1.2.0";
        SizeF vs = g.MeasureString(ver, fVer);
        g.DrawString(ver, fVer, new SolidBrush(Color.FromArgb(110, White)),
            cx - vs.Width / 2, H - 22);

        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }

    // ── InnoSetup: top-right banner  55 × 55 ─────────────────────────────────
    static void MakeInnoBanner(string path)
    {
        const int W = 55, H = 55;
        using var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillRectangle(new SolidBrush(White), 0, 0, W, H);
        DrawWheelMark(g, W / 2, H / 2, 19);
        bmp.Save(path, ImageFormat.Bmp);
        Console.WriteLine($"Written {path}");
    }

    // ── Wheel / compass mark — new Starkive brand ────────────────────────────
    // cx, cy = centre pixel; r = outer circle radius
    static void DrawWheelMark(Graphics g, int cx, int cy, int r)
    {
        float sw = Math.Max(1.1f, r * 0.05f);

        // Outer circle
        using var cp = new Pen(Blue, sw * 1.3f) { LineJoin = LineJoin.Round };
        g.DrawEllipse(cp, cx - r, cy - r, r * 2, r * 2);

        // 4 cardinal spokes
        using var sp = new Pen(Blue, sw)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(sp, cx,       (float)(cy - r), cx,       (float)(cy + r));
        g.DrawLine(sp, (float)(cx - r), cy,       (float)(cx + r), cy);

        // 4 diagonal spokes (slightly dimmer)
        float d = r * 0.707f;
        using var dp = new Pen(Color.FromArgb(175, Blue), sw * 0.82f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(dp, cx - d, cy - d, cx + d, cy + d);
        g.DrawLine(dp, cx + d, cy - d, cx - d, cy + d);

        // Centre dot
        float dot  = r * 0.20f;
        float hiDot = dot * 0.52f;
        g.FillEllipse(new SolidBrush(Blue),
            cx - dot, cy - dot, dot * 2, dot * 2);
        g.FillEllipse(new SolidBrush(Color.FromArgb(0x5B, 0xA3, 0xF5)),
            cx - hiDot, cy - hiDot, hiDot * 2, hiDot * 2);
    }

    // ── App icon — 16/32/48/256 px PNG frames ────────────────────────────────
    static void MakeAppIcon(string path)
    {
        int[] sizes = [16, 32, 48, 256];
        var frames  = new List<(int sz, byte[] png)>();

        foreach (int sz in sizes)
        {
            using var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
            using var g   = Graphics.FromImage(bmp);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Rounded-square background
            float pad    = sz * 0.04f;
            float corner = sz * 0.22f;
            using var bgPath = RoundedRect(pad, pad, sz - pad * 2, sz - pad * 2, corner);
            g.FillPath(new SolidBrush(IconBg), bgPath);

            // Wheel mark, padded ~18% from edge
            int r = (int)(sz * 0.31f);
            DrawWheelMark(g, sz / 2, sz / 2, r);

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            frames.Add((sz, ms.ToArray()));
        }

        WriteIco(path, frames);
        Console.WriteLine($"Written {path}");
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

        // Wheel mark on blue sidebar
        DrawWheelMark(g, cx, H / 2 - 30, 28);

        using var fName = new Font("Segoe UI", 18, FontStyle.Bold,    GraphicsUnit.Pixel);
        using var fSub  = new Font("Segoe UI",  9, FontStyle.Regular, GraphicsUnit.Pixel);
        using var fVer  = new Font("Segoe UI",  8, FontStyle.Regular, GraphicsUnit.Pixel);
        SizeF ns = g.MeasureString("Starkive", fName);
        float ny = H / 2f + 10;
        g.DrawString("Starkive", fName, new SolidBrush(White), cx - ns.Width / 2, ny);
        float ry = ny + ns.Height + 6;
        using var rp = new Pen(Color.FromArgb(90, White), 1);
        g.DrawLine(rp, 18, ry, Split - 18, ry);
        SizeF ss = g.MeasureString("starkive.app", fSub);
        g.DrawString("starkive.app", fSub, new SolidBrush(BlueLight), cx - ss.Width / 2, ry + 7);
        SizeF vs = g.MeasureString("v 1.2.0", fVer);
        g.DrawString("v 1.2.0", fVer, new SolidBrush(Color.FromArgb(110, White)),
            cx - vs.Width / 2, H - 18);

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

    // ── Helpers ───────────────────────────────────────────────────────────────
    static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        p.AddArc(x,             y,             r * 2, r * 2, 180, 90);
        p.AddArc(x + w - r * 2, y,             r * 2, r * 2, 270, 90);
        p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2,   0, 90);
        p.AddArc(x,             y + h - r * 2, r * 2, r * 2,  90, 90);
        p.CloseFigure();
        return p;
    }

    // Writes a multi-frame ICO using embedded PNG data (Vista+ compatible)
    static void WriteIco(string path, List<(int sz, byte[] png)> frames)
    {
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        bw.Write((short)0);                  // reserved
        bw.Write((short)1);                  // type: icon
        bw.Write((short)frames.Count);

        int dataOffset = 6 + 16 * frames.Count;
        foreach (var (sz, png) in frames)
        {
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)0);               // color count
            bw.Write((byte)0);               // reserved
            bw.Write((short)1);              // planes
            bw.Write((short)32);             // bit depth
            bw.Write((int)png.Length);
            bw.Write(dataOffset);
            dataOffset += png.Length;
        }
        foreach (var (_, png) in frames)
            bw.Write(png);
    }
}

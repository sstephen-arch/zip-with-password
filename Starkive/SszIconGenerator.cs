using System.IO;
using System.Runtime.InteropServices;

namespace Starkive;

/// <summary>
/// Generates a distinctive .ico file for Starkive Secure Container (.ssz) files.
/// Called once during --install so the file association has a real icon.
/// </summary>
internal static class SszIconGenerator
{
    internal static string IconPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Starkive", "ssz.ico");

    /// <summary>
    /// Writes ssz.ico to %AppData%\Starkive\ and returns its path.
    /// Uses raw ICO format — no System.Drawing dependency needed.
    /// </summary>
    internal static string EnsureIcon()
    {
        string path = IconPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Always regenerate so updates replace the old icon.
        byte[] ico = BuildIco();
        File.WriteAllBytes(path, ico);
        return path;
    }

    // ── ICO builder ──────────────────────────────────────────────────────────
    // Writes a 32×32 RGBA ICO with no external dependencies.
    // Design: dark navy (#0A1628) background, gold star, white padlock.

    private static byte[] BuildIco()
    {
        const int size = 32;
        uint[] pixels = RenderIcon(size);

        // BMP DIB header (BITMAPINFOHEADER) for ICO XOR mask
        byte[] bmpHeader = BuildBmpHeader(size);
        byte[] xorData   = PixelsToArgbBytes(pixels, size);
        byte[] andMask   = new byte[(size * ((size + 31) / 32) * 4)]; // all transparent

        byte[] imageData = [.. bmpHeader, .. xorData, .. andMask];

        // ICO header + directory entry
        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        // ICONDIR
        w.Write((ushort)0);       // reserved
        w.Write((ushort)1);       // type = ICO
        w.Write((ushort)1);       // 1 image

        // ICONDIRENTRY
        w.Write((byte)size);      // width
        w.Write((byte)size);      // height
        w.Write((byte)0);         // color count
        w.Write((byte)0);         // reserved
        w.Write((ushort)1);       // planes
        w.Write((ushort)32);      // bpp
        w.Write((uint)imageData.Length);
        w.Write((uint)6 + 16);    // offset = ICONDIR(6) + ICONDIRENTRY(16)

        w.Write(imageData);
        return ms.ToArray();
    }

    private static uint[] RenderIcon(int size)
    {
        uint[] px = new uint[size * size];

        // Background: dark navy
        uint navy  = Rgba(0x0A, 0x16, 0x28, 0xFF);
        uint gold  = Rgba(0xF5, 0xBF, 0x30, 0xFF);
        uint white = Rgba(0xFF, 0xFF, 0xFF, 0xFF);

        for (int i = 0; i < px.Length; i++) px[i] = navy;

        // Rounded-rect border clipping (clip corners to make it look like a tile)
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (IsCorner(x, y, size, 4)) px[y * size + x] = 0; // transparent
        }

        // ── Star (5-pointed) centred at (16, 14), radius 11 outer, 5 inner ──
        DrawStar(px, size, cx: 16f, cy: 13f, outer: 10f, inner: 4.2f, color: gold);

        // ── Padlock body (small, bottom-centre) ──────────────────────────────
        DrawLock(px, size, cx: 16, cy: 24, color: white);

        return px;
    }

    private static void DrawStar(uint[] px, int size,
        float cx, float cy, float outer, float inner, uint color)
    {
        const int points = 5;
        // Build star polygon vertices
        var verts = new (float x, float y)[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI / points * i - Math.PI / 2;
            float r = i % 2 == 0 ? outer : inner;
            verts[i] = (cx + r * (float)Math.Cos(angle),
                        cy + r * (float)Math.Sin(angle));
        }

        // Scan-fill the star
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            if (PointInPolygon(x + 0.5f, y + 0.5f, verts))
                px[y * size + x] = color;
        }
    }

    private static void DrawLock(uint[] px, int size, int cx, int cy, uint color)
    {
        // Shackle (arc, 3px wide, top half of circle r=3)
        int r = 3;
        for (double a = Math.PI; a <= 2 * Math.PI; a += 0.04)
        {
            for (int t = -1; t <= 1; t++)
            {
                int lx = (int)Math.Round(cx + (r + t) * Math.Cos(a));
                int ly = (int)Math.Round(cy - 4 + (r + t) * Math.Sin(a));
                SetPixel(px, size, lx, ly, color);
            }
        }

        // Body rectangle  6×5
        for (int dy = -1; dy <= 3; dy++)
        for (int dx = -3; dx <= 3; dx++)
            SetPixel(px, size, cx + dx, cy + dy, color);

        // Keyhole (small dark oval)
        uint bg = Rgba(0x0A, 0x16, 0x28, 0xFF);
        SetPixel(px, size, cx,     cy,     bg);
        SetPixel(px, size, cx,     cy + 1, bg);
        SetPixel(px, size, cx - 1, cy,     bg);
        SetPixel(px, size, cx + 1, cy,     bg);
    }

    // ── Polygon fill helpers ─────────────────────────────────────────────────

    private static bool PointInPolygon(float x, float y, (float x, float y)[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = poly[i].x, yi = poly[i].y;
            float xj = poly[j].x, yj = poly[j].y;
            if ((yi > y) != (yj > y) &&
                x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    private static bool IsCorner(int x, int y, int size, int radius)
    {
        int dx = Math.Min(x, size - 1 - x);
        int dy = Math.Min(y, size - 1 - y);
        if (dx >= radius || dy >= radius) return false;
        int ex = radius - dx - 1, ey = radius - dy - 1;
        return ex * ex + ey * ey > (radius - 1) * (radius - 1);
    }

    private static void SetPixel(uint[] px, int size, int x, int y, uint color)
    {
        if (x >= 0 && x < size && y >= 0 && y < size)
            px[y * size + x] = color;
    }

    // ── ICO binary helpers ───────────────────────────────────────────────────

    private static byte[] BuildBmpHeader(int size)
    {
        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);
        // BITMAPINFOHEADER (40 bytes)
        w.Write((uint)40);          // biSize
        w.Write((int)size);         // biWidth
        w.Write((int)(size * 2));   // biHeight (doubled for ICO XOR+AND)
        w.Write((ushort)1);         // biPlanes
        w.Write((ushort)32);        // biBitCount
        w.Write((uint)0);           // biCompression = BI_RGB
        w.Write((uint)0);           // biSizeImage
        w.Write((int)0);            // biXPelsPerMeter
        w.Write((int)0);            // biYPelsPerMeter
        w.Write((uint)0);           // biClrUsed
        w.Write((uint)0);           // biClrImportant
        return ms.ToArray();
    }

    private static byte[] PixelsToArgbBytes(uint[] pixels, int size)
    {
        // ICO stores rows bottom-to-top, channel order BGRA
        byte[] data = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            uint p   = pixels[(size - 1 - y) * size + x]; // flip Y
            int  idx = (y * size + x) * 4;
            data[idx + 0] = (byte)(p & 0xFF);           // B
            data[idx + 1] = (byte)((p >> 8)  & 0xFF);   // G
            data[idx + 2] = (byte)((p >> 16) & 0xFF);   // R
            data[idx + 3] = (byte)((p >> 24) & 0xFF);   // A
        }
        return data;
    }

    private static uint Rgba(byte r, byte g, byte b, byte a)
        => (uint)(a << 24 | r << 16 | g << 8 | b);
}

"""
gen_installer_assets.py
Generates installer_banner.bmp (493x58), installer_dialog.bmp (493x312),
and app.ico (16,32,48,64,128,256 px) for the Starkive installer.
Run: python gen_installer_assets.py
Requires: Pillow  (pip install Pillow)
"""

import math, sys
from PIL import Image, ImageDraw, ImageFont

# ── Palette ───────────────────────────────────────────────────────────────────
BG_PRIMARY   = (10,  14,  22)       # #0A0E16
BG_SURFACE   = (13,  18,  25)       # #0D1219
ACCENT       = (26,  86, 219)       # #1A56DB
TEXT_PRI     = (232, 240, 251)      # #E8F0FB  (white-ish)
TEXT_MUT     = (58,  84, 112)       # #3A5470
STAR_BLUE    = (26,  86, 219, 255)  # #1A56DB
STAR_BLUE_HL = (91, 163, 245, 255)  # #5BA3F5
STAR_BG      = ( 8,  14,  28, 255)  # #080E1C
STAR_MID     = (45, 111, 212, 255)  # #2D6FD4


def _font(path, size):
    fonts = [
        path,
        "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    ]
    for f in fonts:
        if f is None:
            continue
        try:
            return ImageFont.truetype(f, size)
        except Exception:
            pass
    return ImageFont.load_default()


def _bold_font(size):
    for path in [
        "C:/Windows/Fonts/segoeuib.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ]:
        try:
            return ImageFont.truetype(path, size)
        except Exception:
            pass
    return ImageFont.load_default()


def draw_star(d, cx, cy, r_outer, r_inner, n=6):
    """Draw an n-point star centered at (cx,cy) using blue palette."""
    pts = []
    for i in range(n * 2):
        angle = math.pi * i / n - math.pi / 2
        r     = r_outer if i % 2 == 0 else r_inner
        pts.append((cx + math.cos(angle) * r, cy + math.sin(angle) * r))
    d.polygon(pts, fill=STAR_BLUE[:3])
    # inner ring
    pts2 = []
    for i in range(n * 2):
        angle = math.pi * i / n - math.pi / 2
        r     = (r_outer * 0.55) if i % 2 == 0 else (r_inner * 0.7)
        pts2.append((cx + math.cos(angle) * r, cy + math.sin(angle) * r))
    d.polygon(pts2, fill=STAR_MID[:3])
    # center highlight
    dot_r = max(1, r_outer * 0.16)
    d.ellipse([cx - dot_r, cy - dot_r, cx + dot_r, cy + dot_r],
              fill=STAR_BLUE_HL[:3])


# ── Banner 493×58 ─────────────────────────────────────────────────────────────
def make_banner():
    img = Image.new("RGB", (493, 58), BG_PRIMARY)
    d   = ImageDraw.Draw(img)

    # Blue star in left panel (centered vertically)
    draw_star(d, 29, 29, 14, 7)

    # Text to the right of star
    d.text((52,  8), "Starkive",
           fill=TEXT_PRI, font=_bold_font(18))
    d.text((52, 32), "Secure file delivery. AES-256 encrypted.",
           fill=TEXT_MUT, font=_font(None, 11))

    # Copyright bottom left
    d.text((4, 46), "v1.1.0 © 2026 DePaolo Consulting LLC",
           fill=TEXT_MUT, font=_font(None, 8))

    img.save("installer_banner.bmp", "BMP")
    print("  installer_banner.bmp  493x58")


# ── Dialog 493×312 ────────────────────────────────────────────────────────────
def make_dialog():
    img = Image.new("RGB", (493, 312), BG_PRIMARY)
    d   = ImageDraw.Draw(img)

    # Solid left panel (no gradient, no dots) — just slightly lighter background
    d.rectangle([0, 0, 170, 312], fill=BG_PRIMARY)

    # Blue star centered in left panel
    draw_star(d, 85, 110, 44, 22)

    # Text below star (centered in left panel, x=22)
    d.text((22, 172), "Starkive",
           fill=TEXT_PRI, font=_bold_font(22))
    d.text((22, 202), "Secure file delivery.",
           fill=TEXT_MUT, font=_font(None, 12))
    d.text((22, 218), "AES-256 encrypted.",
           fill=TEXT_MUT, font=_font(None, 12))

    # Copyright bottom left
    d.text((8, 296), "v1.1.0  © 2026 DePaolo Consulting LLC",
           fill=TEXT_MUT, font=_font(None, 9))

    img.save("installer_dialog.bmp", "BMP")
    print("  installer_dialog.bmp  493x312")


# ── App icon (multi-size ICO) ─────────────────────────────────────────────────
def make_ico():
    sizes   = [16, 32, 48, 64, 128, 256]
    frames  = []

    for sz in sizes:
        img = Image.new("RGBA", (sz, sz), (0, 0, 0, 0))
        d   = ImageDraw.Draw(img)

        # Rounded background
        pad  = max(1, sz // 8)
        r    = max(2, sz // 5)
        d.rounded_rectangle([pad, pad, sz - pad - 1, sz - pad - 1],
                            radius=r, fill=STAR_BG[:3])

        # Star
        cx = sz / 2
        cy = sz / 2
        r_o = sz * 0.36
        r_i = sz * 0.18
        draw_star(d, cx, cy, r_o, r_i)

        frames.append(img)

    frames[0].save(
        "Starkive/app.ico",
        format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=frames[1:],
    )
    print("  Starkive/app.ico  16,32,48,64,128,256px")


if __name__ == "__main__":
    print("Generating installer assets...")
    try:
        make_banner()
        make_dialog()
        make_ico()
        print("Done.")
    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)

"""Generate Steam Workshop thumbnail for Wetlands (960x540)."""
from __future__ import annotations

import os

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "Wetlands", "thumbnail.png")
BASE = os.path.join(ROOT, "tools", "workshop_mod_blueprint_base.png")
HERO = os.path.join(ROOT, "tools", "wetlands_hero.jpg")
FONT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"
FONT_REG = r"C:\Windows\Fonts\arial.ttf"
W, H = 960, 540


def outlined_text(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int],
    outline: tuple[int, int, int] = (12, 32, 16),
    width: int = 2,
) -> None:
    x, y = xy
    for dx in range(-width, width + 1):
        for dy in range(-width, width + 1):
            if dx * dx + dy * dy <= width * width + 1:
                draw.text((x + dx, y + dy), text, font=font, fill=outline)
    draw.text((x, y), text, font=font, fill=fill)


def cut_out_hero(path: str) -> Image.Image:
    """Drop the flat dark-teal backdrop behind the marsh diorama."""
    image = Image.open(path).convert("RGB")
    rgb = np.array(image, dtype=np.float32)
    corners = np.stack(
        [rgb[0, 0], rgb[0, -1], rgb[-1, 0], rgb[-1, -1]], axis=0
    )
    bg = corners.mean(axis=0)
    dist = np.linalg.norm(rgb - bg, axis=-1)
    # Soft edge: background is nearly uniform; grass and mud sit well above it.
    alpha = np.clip((dist - 18) / 22, 0, 1)
    alpha = np.array(
        Image.fromarray((alpha * 255).astype(np.uint8), "L").filter(
            ImageFilter.GaussianBlur(0.6)
        ),
        dtype=np.float32,
    ) / 255.0
    rgba = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def main() -> None:
    base = Image.open(BASE).convert("RGB")
    hsv = np.array(base.convert("HSV"), dtype=np.float32)
    hh, ss, vv = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    # Blueprint blue sits near hue 160; marsh olive is near 75.
    target = 75.0
    hh = hh * 0.2 + target * 0.8
    ss = np.clip(ss * 0.72 + 40, 0, 255)
    vv = np.clip(vv * 0.72, 0, 255)
    canvas = Image.fromarray(
        np.stack([hh, ss, vv], axis=-1).astype(np.uint8), mode="HSV"
    ).convert("RGB")
    canvas = ImageEnhance.Contrast(canvas).enhance(1.06)

    wash = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    wdraw = ImageDraw.Draw(wash)
    for y in range(H):
        t = y / (H - 1)
        wdraw.line([(0, y), (W, y)], fill=(24, 72, 28, int(30 + 60 * t)))
    wdraw.ellipse((480, -60, 1080, 620), fill=(16, 58, 22, 80))
    canvas = Image.alpha_composite(canvas.convert("RGBA"), wash)

    hero = cut_out_hero(HERO)
    target_h = 520
    scale = target_h / hero.height
    hero = hero.resize(
        (int(hero.width * scale), target_h), Image.Resampling.LANCZOS
    )
    hero = hero.filter(ImageFilter.UnsharpMask(radius=1.0, percent=70, threshold=2))

    comp = canvas.copy()
    bx = W - hero.width + 36
    by = (H - hero.height) // 2 + 8
    comp.alpha_composite(hero, (bx, by))

    draw = ImageDraw.Draw(comp)
    title_font = ImageFont.truetype(FONT_BOLD, 40)
    sub_font = ImageFont.truetype(FONT_BOLD, 26)
    body_font = ImageFont.truetype(FONT_REG, 20)

    outlined_text(draw, (48, 48), "EROMRAB'S WETLANDS", title_font, (236, 248, 226))
    outlined_text(draw, (48, 96), "Landscaping · 0.1.0", sub_font, (168, 214, 120))
    draw.line([(48, 142), (470, 142)], fill=(190, 220, 150), width=2)
    lines = (
        "Marshy ground on dry land",
        "Paths sit on top",
        "Shallow-water plants can grow",
        "Required by Tidetails",
    )
    y = 158
    for line in lines:
        outlined_text(draw, (48, y), f"• {line}", body_font, (245, 250, 240))
        y += 30

    comp.convert("RGB").save(OUT, format="PNG", optimize=True)
    print("Wrote", OUT)


if __name__ == "__main__":
    main()

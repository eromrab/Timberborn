"""Generate Steam Workshop thumbnail for Tidetails faction (960x540)."""
from __future__ import annotations

import os

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "Tidetails", "thumbnail.png")
BASE = os.path.join(ROOT, "tools", "workshop_mod_blueprint_base.png")
HERO = os.path.join(
    ROOT, "Tidetails", "Sprites", "Avatars", "TidetailsFullNewGame.png"
)
LOGO = os.path.join(ROOT, "Tidetails", "Sprites", "Avatars", "TidetailsLogo.png")
FONT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"
FONT_REG = r"C:\Windows\Fonts\arial.ttf"
W, H = 960, 540


def outlined_text(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int],
    outline: tuple[int, int, int] = (8, 24, 48),
    width: int = 2,
) -> None:
    x, y = xy
    for dx in range(-width, width + 1):
        for dy in range(-width, width + 1):
            if dx * dx + dy * dy <= width * width + 1:
                draw.text((x + dx, y + dy), text, font=font, fill=outline)
    draw.text((x, y), text, font=font, fill=fill)


def main() -> None:
    base = Image.open(BASE).convert("RGB")
    hsv = np.array(base.convert("HSV"), dtype=np.float32)
    hh, ss, vv = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    hh = (hh + 8) % 256
    ss = np.clip(ss * 1.05, 0, 255)
    vv = np.clip(vv * 0.9, 0, 255)
    canvas = Image.fromarray(
        np.stack([hh, ss, vv], axis=-1).astype(np.uint8), mode="HSV"
    ).convert("RGB")
    canvas = ImageEnhance.Contrast(canvas).enhance(1.04)

    wash = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    wdraw = ImageDraw.Draw(wash)
    for y in range(H):
        t = y / (H - 1)
        wdraw.line([(0, y), (W, y)], fill=(20, 95, 115, int(28 + 55 * t)))
    wdraw.ellipse((520, -40, 1100, 580), fill=(12, 70, 95, 75))
    canvas = Image.alpha_composite(canvas.convert("RGBA"), wash)

    hero = Image.open(HERO).convert("RGBA")
    target_h = 500
    scale = target_h / hero.height
    hero = hero.resize(
        (int(hero.width * scale), target_h), Image.Resampling.LANCZOS
    )
    hero = hero.filter(ImageFilter.UnsharpMask(radius=1.0, percent=85, threshold=2))

    comp = canvas.copy()
    bx = W - hero.width + 30
    by = H - hero.height + 8
    comp.alpha_composite(hero, (bx, by))

    logo = Image.open(LOGO).convert("RGBA")
    logo = logo.resize((72, 72), Image.Resampling.LANCZOS)
    comp.alpha_composite(logo, (44, 44))

    draw = ImageDraw.Draw(comp)
    title_font = ImageFont.truetype(FONT_BOLD, 40)
    sub_font = ImageFont.truetype(FONT_BOLD, 26)
    body_font = ImageFont.truetype(FONT_REG, 20)

    outlined_text(draw, (128, 48), "EROMRAB'S TIDETAILS", title_font, (235, 248, 255))
    outlined_text(draw, (128, 96), "Faction · 0.1.0", sub_font, (150, 215, 235))
    draw.line([(48, 142), (500, 142)], fill=(180, 220, 240), width=2)
    lines = (
        "Fast swimmers, slow walkers",
        "Water-built homes & workshops",
        "Romance Based Breeding",
        "Custom trees, paths & dams",
    )
    y = 158
    for line in lines:
        outlined_text(draw, (48, y), f"• {line}", body_font, (245, 250, 252))
        y += 30

    comp.convert("RGB").save(OUT, format="PNG", optimize=True)
    print("Wrote", OUT)


if __name__ == "__main__":
    main()

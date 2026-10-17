"""Generate Steam Workshop thumbnail for BadwaterRush (960x540)."""
from __future__ import annotations

import os

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "BadwaterRush", "thumbnail.png")
BASE = os.path.join(ROOT, "tools", "workshop_mod_blueprint_base.png")
BEAVER_TEAL = os.path.join(ROOT, "Tidetails", "Sprites", "Avatars", "TidetailsAdult.png")
BEAVER_SICK = os.path.join(
    ROOT, "Tidetails", "Sprites", "Avatars", "TidetailsContaminatedAdult.png"
)
FONT_BOLD = r"C:\Windows\Fonts\arialbd.ttf"
FONT_REG = r"C:\Windows\Fonts\arial.ttf"
W, H = 960, 540


def _soft_mask(size: tuple[int, int], draw_fn, blur: int = 12) -> np.ndarray:
    mask = Image.new("L", size, 0)
    draw_fn(ImageDraw.Draw(mask))
    return np.array(mask.filter(ImageFilter.GaussianBlur(blur)), dtype=np.float32) / 255.0


def _push_teal_fur(rgb: np.ndarray) -> np.ndarray:
    hsv = np.array(Image.fromarray(rgb.astype(np.uint8)).convert("HSV"), dtype=np.float32)
    hh, ss, vv = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    fur = (ss > 40) & (vv > 35) & (vv < 245)
    target = 170 / 360 * 255
    w = np.clip((ss - 30) / 80, 0, 1) * np.clip((vv - 20) / 60, 0, 1) * fur.astype(np.float32)
    hh = hh * (1 - w * 0.55) + target * (w * 0.55)
    ss = np.clip(ss * (1 - w * 0.05) + np.maximum(ss, 150) * (w * 0.12), 0, 255)
    hsv[..., 0], hsv[..., 1], hsv[..., 2] = hh, ss, vv
    return np.array(
        Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB"), dtype=np.float32
    )


def rushed_badwater_portrait() -> Image.Image:
    """Sick contaminated look + manic rush; teal fur; adult mouth (no double teeth)."""
    adult = np.array(Image.open(BEAVER_TEAL).convert("RGBA"), dtype=np.float32)
    sick = np.array(Image.open(BEAVER_SICK).convert("RGBA"), dtype=np.float32)
    w, h = adult.shape[1], adult.shape[0]
    alpha = adult[..., 3:4] / 255.0

    def _both_eyes(d: ImageDraw.ImageDraw, boxes: tuple) -> None:
        for box in boxes:
            d.ellipse(box, fill=255)

    # Tight per-eye masks: always sample the teal adult here (sick art shifts pupils).
    pupil_boxes = (
        (0.27 * w, 0.34 * h, 0.41 * w, 0.44 * h),
        (0.59 * w, 0.34 * h, 0.73 * w, 0.44 * h),
    )
    eye_pupil = _soft_mask((w, h), lambda d: _both_eyes(d, pupil_boxes), blur=8)

    eye_boxes = (
        (0.22 * w, 0.30 * h, 0.44 * w, 0.48 * h),
        (0.56 * w, 0.30 * h, 0.78 * w, 0.48 * h),
    )
    eye_open = _soft_mask((w, h), lambda d: _both_eyes(d, eye_boxes))
    red_boxes = (
        (0.24 * w, 0.33 * h, 0.42 * w, 0.46 * h),
        (0.58 * w, 0.33 * h, 0.76 * w, 0.46 * h),
    )
    eye_red = _soft_mask((w, h), lambda d: _both_eyes(d, red_boxes))
    sclera_red = np.clip(eye_red - eye_pupil * 0.85, 0, 1)

    y = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    y = np.broadcast_to(y, (h, w))
    forehead_sick = np.clip((0.40 - y) / 0.40, 0, 1) * (1 - eye_open * 0.98)

    mouth_keep = _soft_mask(
        (w, h),
        lambda d: d.ellipse((0.22 * w, 0.56 * h, 0.78 * w, 0.93 * h), fill=255),
    )

    rgb = adult[..., :3].copy()
    rgb = rgb * (1 - forehead_sick[..., None] * 0.72) + sick[..., :3] * (
        forehead_sick[..., None] * 0.72
    )
    rgb = rgb * (1 - mouth_keep[..., None] * 0.88) + adult[..., :3] * (
        mouth_keep[..., None] * 0.88
    )

    cheek_boxes = (
        (0.12 * w, 0.48 * h, 0.34 * w, 0.64 * h),
        (0.66 * w, 0.48 * h, 0.88 * w, 0.64 * h),
    )
    cheek_sick = _soft_mask((w, h), lambda d: _both_eyes(d, cheek_boxes))
    cheek_sick = cheek_sick * (1 - eye_open * 0.95)
    rgb = rgb * (1 - cheek_sick[..., None] * 0.58) + sick[..., :3] * (
        cheek_sick[..., None] * 0.58
    )

    rgb = rgb * (1 - eye_pupil[..., None]) + adult[..., :3] * eye_pupil[..., None]

    blood = np.zeros_like(rgb)
    blood[..., 0] = 215
    blood[..., 1] = 38
    blood[..., 2] = 48
    rgb = rgb * (1 - sclera_red[..., None] * 0.34) + blood * (sclera_red[..., None] * 0.34)
    rgb = np.clip(rgb * (1 + eye_open[..., None] * 0.2), 0, 255)

    bag_boxes = (
        (0.20 * w, 0.46 * h, 0.44 * w, 0.56 * h),
        (0.56 * w, 0.46 * h, 0.80 * w, 0.56 * h),
    )
    eye_bags = _soft_mask((w, h), lambda d: _both_eyes(d, bag_boxes))
    eye_bags = eye_bags * (1 - eye_pupil * 0.9)
    bag_tint = np.array([55, 42, 68], dtype=np.float32)
    rgb = rgb * (1 - eye_bags[..., None] * 0.35) + bag_tint * (eye_bags[..., None] * 0.35)

    mouth_tense = _soft_mask(
        (w, h),
        lambda d: d.ellipse((0.30 * w, 0.64 * h, 0.70 * w, 0.82 * h), fill=255),
    )
    grey = np.array([95, 88, 102], dtype=np.float32)
    rgb = rgb * (1 - mouth_tense[..., None] * 0.48) + grey * (mouth_tense[..., None] * 0.48)
    rgb[..., 0] = np.clip(rgb[..., 0] - mouth_tense * 8, 0, 255)
    rgb[..., 1] = np.clip(rgb[..., 1] - mouth_tense * 22, 0, 255)
    rgb[..., 2] = np.clip(rgb[..., 2] - mouth_tense * 14, 0, 255)

    rgb = _push_teal_fur(rgb)
    rgb = np.clip(rgb * 0.96, 0, 255)

    streak = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sdraw = ImageDraw.Draw(streak)
    for i in range(14):
        y0 = int(0.22 * h + i * 0.045 * h)
        sdraw.line(
            (int(-0.05 * w), y0, int(0.92 * w), y0 - int(0.06 * h)),
            fill=(200, 235, 220, 28 + i * 2),
            width=2,
        )
    streak_arr = np.array(streak, dtype=np.float32)
    sa = streak_arr[..., 3:4] / 255.0
    rgb = rgb * (1 - sa) + streak_arr[..., :3] * sa

    out = np.concatenate([rgb, alpha * 255], axis=-1).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def outlined_text(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    font: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int],
    outline: tuple[int, int, int] = (12, 28, 22),
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
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    h = (h + 18) % 256
    s = np.clip(s * 1.08, 0, 255)
    v = np.clip(v * 0.82, 0, 255)
    tinted = Image.fromarray(
        np.stack([h, s, v], axis=-1).astype(np.uint8), mode="HSV"
    ).convert("RGB")
    canvas = ImageEnhance.Contrast(tinted).enhance(1.05)

    overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    odraw = ImageDraw.Draw(overlay)
    for y in range(H):
        t = y / (H - 1)
        alpha = int(35 + 75 * t)
        odraw.line([(0, y), (W, y)], fill=(28, 95, 55, alpha))
    odraw.ellipse((-120, 80, 520, 620), fill=(18, 70, 42, 90))
    canvas = Image.alpha_composite(canvas.convert("RGBA"), overlay).convert("RGB")

    beaver = rushed_badwater_portrait()
    target_h = 430
    scale = target_h / beaver.height
    beaver = beaver.resize(
        (int(beaver.width * scale), target_h), Image.Resampling.LANCZOS
    )
    beaver = beaver.filter(ImageFilter.UnsharpMask(radius=1.2, percent=90, threshold=2))

    comp = canvas.convert("RGBA")
    bx = W - beaver.width + 20
    by = H - beaver.height - 10
    comp.alpha_composite(beaver, (bx, by))

    draw = ImageDraw.Draw(comp)
    title_font = ImageFont.truetype(FONT_BOLD, 46)
    sub_font = ImageFont.truetype(FONT_BOLD, 28)
    body_font = ImageFont.truetype(FONT_REG, 22)
    tag_font = ImageFont.truetype(FONT_REG, 18)

    outlined_text(draw, (48, 52), "BADWATER RUSH", title_font, (235, 248, 238))
    outlined_text(draw, (48, 108), "Optional add-on", sub_font, (160, 220, 175))
    draw.line([(48, 152), (420, 152)], fill=(200, 230, 210), width=2)
    outlined_text(
        draw,
        (48, 168),
        "Contaminated beavers",
        body_font,
        (245, 245, 240),
    )
    outlined_text(draw, (48, 198), "age 2× faster", body_font, (245, 245, 240))
    outlined_text(draw, (48, 228), "work & move 4× faster", body_font, (245, 245, 240))

    draw.rounded_rectangle(
        (48, 278, 248, 318),
        radius=6,
        fill=(22, 58, 38),
        outline=(140, 200, 155),
        width=2,
    )
    draw.text(
        (62, 286),
        "Not required for Tidetails",
        font=tag_font,
        fill=(190, 225, 200),
    )

    for i, y in enumerate(range(260, 500, 22)):
        x0 = 400 + i * 10
        draw.line(
            [(x0, y), (x0 + 72, y - 22)],
            fill=(210, 240, 225, 140),
            width=3,
        )

    comp.convert("RGB").save(OUT, format="PNG", optimize=True)
    print("Wrote", OUT)


if __name__ == "__main__":
    main()

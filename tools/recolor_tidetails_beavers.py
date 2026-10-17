"""Tint Iron Teeth beaver fur maps toward blue-green / teal for Tidetails."""
import os
import sys

import numpy as np
from PIL import Image

TARGET_HUE = 170 / 360 * 255
MIN_SAT = 0.62 * 255
SAT_BOOST = 1.3


def fur_weight(h: np.ndarray, s: np.ndarray, v: np.ndarray) -> np.ndarray:
    hue_deg = h / 255 * 360
    warm = np.clip(1 - np.abs(hue_deg - 35) / 22, 0, 1) * np.clip((s - 45) / 40, 0, 1)
    cool = np.clip(1 - np.abs(hue_deg - 225) / 80, 0, 1)
    grey_body = np.clip((s - 20) / 50, 0, 1) * np.clip(1 - warm, 0, 1)
    w = np.maximum(cool * 0.95, grey_body * 0.9)
    w *= np.clip((v - 8) / 32, 0, 1)
    w *= np.clip((235 - v) / 90, 0, 1)
    w *= 1 - warm * 0.98
    return np.clip(w, 0, 0.92)


def recolor(path: str, out_path: str) -> None:
    img = Image.open(path).convert("RGBA")
    alpha = img.getchannel("A")
    hsv = np.array(img.convert("RGB").convert("HSV")).astype(np.float32)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    w = fur_weight(h, s, v)

    new_h = h * (1 - w) + TARGET_HUE * w
    boosted = np.clip(s * SAT_BOOST, 0, 255)
    new_s = np.clip(s * (1 - w) + np.maximum(boosted, MIN_SAT) * w, 0, 255)
    new_v = np.clip(v * (1 + 0.06 * w), 0, 255)
    hsv[..., 0], hsv[..., 1], hsv[..., 2] = new_h, new_s, new_v

    out = Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB")
    out.putalpha(alpha)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    out.save(out_path)


def main() -> None:
    repo = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
    out_root = os.path.join(repo, "Tidetails", "Materials", "Beavers", "Tidetails")
    src_root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.environ.get("TEMP", "."), "tb_research", "beaver_textures_it"
    )
    if not os.path.isdir(src_root):
        print("Source folder missing:", src_root)
        sys.exit(1)
    for name in sorted(os.listdir(src_root)):
        if "IronTeeth" not in name or not name.endswith(".png"):
            continue
        if name.startswith("BeaverAdult"):
            sub = "Adult"
        elif name.startswith("BeaverChild"):
            sub = "Child"
        else:
            continue
        out_name = name.replace(".IronTeeth", ".Tidetails")
        out_path = os.path.join(out_root, sub, out_name)
        recolor(os.path.join(src_root, name), out_path)
        print(out_path)


if __name__ == "__main__":
    main()

# -*- coding: utf-8 -*-
"""Heal lobby desk PNG: bottom-right watermark and/or bottom-center map plate."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image


def luminance(rgb: tuple[int, int, int]) -> float:
    r, g, b = rgb
    return 0.299 * r + 0.587 * g + 0.114 * b


def heal_corner_horizontal(
    im: Image.Image,
    *,
    x0: int,
    y0: int,
    src_band: int = 200,
) -> Image.Image:
    """Replace [x0,w) x [y0,h) by horizontally resampling pixels from [x0-src_band, x0)."""
    im = im.convert("RGB")
    w, h = im.size
    x0 = max(1, min(w - 2, x0))
    y0 = max(0, min(h - 1, y0))
    src_band = min(src_band, x0)
    px = im.load()
    out = im.copy()
    op = out.load()
    span = w - x0
    if span <= 0:
        return out
    for y in range(y0, h):
        for xi, x in enumerate(range(x0, w)):
            t = xi / max(1, span - 1)
            sx = int(x0 - 1 - t * (src_band - 1))
            sx = max(x0 - src_band, min(x0 - 1, sx))
            op[x, y] = px[sx, y]
    return out


def heal_rect_horizontal(
    im: Image.Image,
    *,
    x0: int,
    x1: int,
    y0: int,
    src_band: int = 200,
) -> Image.Image:
    """Replace [x0,x1) x [y0,h) by resampling the strip [x0-src_band, x0) along x (same as corner heal)."""
    im = im.convert("RGB")
    w, h = im.size
    x0 = max(1, min(w - 2, x0))
    x1 = max(x0 + 1, min(w, x1))
    y0 = max(0, min(h - 1, y0))
    src_band = min(src_band, x0)
    px = im.load()
    out = im.copy()
    op = out.load()
    span = x1 - x0
    if span <= 0:
        return out
    for y in range(y0, h):
        for xi, x in enumerate(range(x0, x1)):
            t = xi / max(1, span - 1)
            sx = int(x0 - 1 - t * (src_band - 1))
            sx = max(x0 - src_band, min(x0 - 1, sx))
            op[x, y] = px[sx, y]
    return out


def remove_bottom_center_map_plate(im: Image.Image) -> Image.Image:
    """Remove the small raised map icon plate on the bottom bar (center)."""
    w, h = im.size
    x0 = int(w * 0.415)
    x1 = int(w * 0.585)
    y0 = int(h * 0.868)
    return heal_rect_horizontal(im, x0=x0, x1=x1, y0=y0, src_band=min(220, x0))


def scrub_bright_sparks(
    im: Image.Image,
    *,
    x0: int,
    y0: int,
    lum: float = 118.0,
) -> Image.Image:
    """Any remaining bright specks in ROI: pull from a few px left."""
    out = im.convert("RGB").copy()
    w, h = out.size
    op = out.load()
    for y in range(y0, h):
        for x in range(x0, w):
            if luminance(op[x, y]) >= lum:
                sx = max(0, x - 4)
                op[x, y] = op[sx, y]
    return out


def main() -> int:
    legacy_default = Path(
        r"C:\Users\Administrator\.cursor\projects\e-code-c-UnityRTS\assets"
        r"\c__Users_Administrator_AppData_Roaming_Cursor_User_workspaceStorage_d84dd013307824c5a97e8355f6e32b4c_images_image-f6a84dc4-5f4d-402b-a62b-042cb60c3d11.png"
    )
    p = argparse.ArgumentParser(description="Heal lobby desk PNG (watermark / bottom map plate).")
    p.add_argument("input", nargs="?", type=Path, default=None)
    p.add_argument("output", nargs="?", type=Path, default=None)
    p.add_argument("--watermark-br", action="store_true", help="Remove bottom-right AI watermark strip")
    p.add_argument("--bottom-map", action="store_true", help="Remove bottom-center map icon button plate")
    args = p.parse_args()

    src = args.input or legacy_default
    if not src.is_file():
        print("Missing input:", src)
        return 1

    desk = Path.home() / "Desktop"
    out = args.output or (desk / "RTS_lobby_map_no_watermark.png")

    cleaned = Image.open(src).convert("RGB")
    wm = args.watermark_br
    bm = args.bottom_map
    if not wm and not bm:
        wm = True

    if bm:
        cleaned = remove_bottom_center_map_plate(cleaned)
    if wm:
        w, h = cleaned.size
        x0 = int(w * 0.855)
        y0 = int(h * 0.905)
        cleaned = heal_corner_horizontal(cleaned, x0=x0, y0=y0, src_band=min(220, x0))
        cleaned = scrub_bright_sparks(cleaned, x0=x0, y0=y0, lum=115.0)

    out.parent.mkdir(parents=True, exist_ok=True)
    cleaned.save(out, "PNG", optimize=True)
    print("Saved:", out)

    alt = src.parent / (src.stem + "_cleaned.png")
    cleaned.save(alt, "PNG", optimize=True)
    print("Saved:", alt)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

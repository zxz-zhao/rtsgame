import os
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

def generate():
    master_path = r"C:\Users\Administrator\.gemini\antigravity\brain\25e5410e-4d95-4f85-8d87-d2833faafc23\rts_mobile_app_icon_1792684130538.jpg"
    if not os.path.exists(master_path):
        raise FileNotFoundError(f"Master icon not found: {master_path}")

    raw_img = Image.open(master_path).convert("RGBA")
    w, h = raw_img.size

    # 1. Mask out the white outside corners with smooth alpha
    arr = np.array(raw_img)
    alpha = np.ones((h, w), dtype=np.uint8) * 255

    corner_limit = int(w * 0.22) # ~225px
    for y in range(h):
        for x in range(w):
            if (x < corner_limit or x > w - corner_limit) and (y < corner_limit or y > h - corner_limit):
                r, g, b = arr[y, x, 0], arr[y, x, 1], arr[y, x, 2]
                if r > 240 and g > 240 and b > 240:
                    alpha[y, x] = 0

    alpha_img = Image.fromarray(alpha).filter(ImageFilter.GaussianBlur(1.0))
    hero_card = raw_img.copy()
    hero_card.putalpha(alpha_img)

    # 2. Also create full-bleed version with dark navy filled corners
    arr_full = np.array(raw_img.convert("RGB"))
    is_white = (arr_full[:, :, 0] > 240) & (arr_full[:, :, 1] > 240) & (arr_full[:, :, 2] > 240)
    for y in range(h):
        for x in range(w):
            if (x < corner_limit or x > w - corner_limit) and (y < corner_limit or y > h - corner_limit):
                if is_white[y, x]:
                    arr_full[y, x] = [8, 16, 26] # Deep dark navy to match ambient vignette
    
    full_bleed_1024 = Image.fromarray(arr_full)

    os.makedirs("assets/icons", exist_ok=True)

    # 3. app_icon_512.png (High-Res store / PC icon)
    icon_512 = full_bleed_1024.resize((512, 512), Image.Resampling.LANCZOS)
    icon_512.save("assets/icons/app_icon_512.png", "PNG", optimize=True)
    print("Saved assets/icons/app_icon_512.png")

    # 4. app_icon_192.png (Legacy launcher icon)
    icon_192 = full_bleed_1024.resize((192, 192), Image.Resampling.LANCZOS)
    icon_192.save("assets/icons/app_icon_192.png", "PNG", optimize=True)
    print("Saved assets/icons/app_icon_192.png")

    # 5. Windows native .ico
    ico_sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    icon_512.save("assets/icons/app_icon.ico", format="ICO", sizes=ico_sizes)
    print("Saved assets/icons/app_icon.ico")

    # 6. Android Adaptive Icon:
    # Size: 432x432
    # Background (432x432 full bleed):
    # Deep tactical military steel-navy with radial lighting
    bg_size = 432
    bg = Image.new("RGBA", (bg_size, bg_size), (8, 16, 26, 255))
    draw_bg = ImageDraw.Draw(bg)
    cx, cy = bg_size // 2, bg_size // 2
    for r in range(bg_size // 2 + 60, 0, -2):
        factor = r / (bg_size // 2 + 60)
        # Gradient from center cyan-tinted steel to deep dark edge
        cr = int(14 + 16 * (1.0 - factor))
        cg = int(24 + 28 * (1.0 - factor))
        cb = int(36 + 42 * (1.0 - factor))
        draw_bg.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(cr, cg, cb, 255))

    # Add subtle tactical radar accents to background
    draw_bg.ellipse([cx - 150, cy - 150, cx + 150, cy + 150], outline=(30, 65, 95, 60), width=1)
    draw_bg.ellipse([cx - 190, cy - 190, cx + 190, cy + 190], outline=(25, 50, 75, 45), width=1)

    bg_path = "assets/icons/icon_background_432.png"
    bg.save(bg_path, "PNG", optimize=True)
    print(f"Saved {bg_path}")

    # Foreground (432x432):
    # Scale hero card to ~300x300 px so it fits perfectly in the 288px safe squircle/circle
    target_hero_size = 304
    hero_scaled = hero_card.resize((target_hero_size, target_hero_size), Image.Resampling.LANCZOS)
    
    fg = Image.new("RGBA", (bg_size, bg_size), (0, 0, 0, 0))
    paste_x = (bg_size - target_hero_size) // 2
    paste_y = (bg_size - target_hero_size) // 2

    # Drop shadow behind hero card
    shadow = Image.new("RGBA", (bg_size, bg_size), (0, 0, 0, 0))
    shadow_mask = hero_scaled.split()[-1]
    shadow.paste(Image.new("RGBA", (target_hero_size, target_hero_size), (0, 0, 0, 180)), (paste_x, paste_y + 4), shadow_mask)
    shadow = shadow.filter(ImageFilter.GaussianBlur(8))
    fg = Image.alpha_composite(fg, shadow)

    # Paste hero card
    fg.paste(hero_scaled, (paste_x, paste_y), hero_scaled)

    fg_path = "assets/icons/icon_foreground_432.png"
    fg.save(fg_path, "PNG", optimize=True)
    print(f"Saved {fg_path}")

    # 7. Composite icon for legacy / launcher preview
    composite_adaptive = Image.alpha_composite(bg, fg)

    # 8. Update android/build/res/ mipmap densities
    densities = {
        "mipmap-mdpi-v4": (48, 108),
        "mipmap-hdpi-v4": (72, 162),
        "mipmap-xhdpi-v4": (96, 216),
        "mipmap-xxhdpi-v4": (144, 324),
        "mipmap-xxxhdpi-v4": (192, 432),
        "mipmap": (192, 432),
    }

    for folder, (icon_sz, adaptive_sz) in densities.items():
        dir_path = os.path.join("android/build/res", folder)
        os.makedirs(dir_path, exist_ok=True)

        # Composite icon (for legacy / standard launcher display)
        # Use full bleed with a subtle rounded squircle mask for crisp edges
        ic = composite_adaptive.resize((icon_sz, icon_sz), Image.Resampling.LANCZOS)
        ic.save(os.path.join(dir_path, "icon.webp"), "WEBP", quality=95)

        # Adaptive foreground
        fg_scaled = fg.resize((adaptive_sz, adaptive_sz), Image.Resampling.LANCZOS)
        fg_scaled.save(os.path.join(dir_path, "icon_foreground.webp"), "WEBP", quality=95)

        # Adaptive background
        bg_scaled = bg.resize((adaptive_sz, adaptive_sz), Image.Resampling.LANCZOS)
        bg_scaled.save(os.path.join(dir_path, "icon_background.webp"), "WEBP", quality=95)
        print(f"Updated {dir_path}")

    # 9. Create a preview image for user verification
    preview = Image.new("RGBA", (800, 320), (22, 28, 38, 255))
    draw_preview = ImageDraw.Draw(preview)
    
    # Left: Full 512 icon scaled to 256
    preview.paste(icon_512.resize((240, 240), Image.Resampling.LANCZOS), (30, 40))
    # Middle: Squircle (Android launcher preview)
    squircle_mask = Image.new("L", (240, 240), 0)
    draw_sq = ImageDraw.Draw(squircle_mask)
    draw_sq.rounded_rectangle([0, 0, 240, 240], radius=54, fill=255)
    
    comp_sq = composite_adaptive.resize((240, 240), Image.Resampling.LANCZOS)
    comp_sq.putalpha(squircle_mask)
    preview.paste(comp_sq, (290, 40), comp_sq)

    # Right: Circle (Pixel / Store preview)
    circle_mask = Image.new("L", (240, 240), 0)
    draw_c = ImageDraw.Draw(circle_mask)
    draw_c.ellipse([0, 0, 240, 240], fill=255)
    comp_c = composite_adaptive.resize((240, 240), Image.Resampling.LANCZOS)
    comp_c.putalpha(circle_mask)
    preview.paste(comp_c, (550, 40), comp_c)

    preview_path = r"C:\Users\Administrator\.gemini\antigravity\brain\25e5410e-4d95-4f85-8d87-d2833faafc23\icon_preview_comparison.png"
    preview.save(preview_path, "PNG")
    print(f"Generated comparison preview at {preview_path}")

if __name__ == "__main__":
    generate()

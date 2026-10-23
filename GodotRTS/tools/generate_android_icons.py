import os
from PIL import Image, ImageDraw, ImageFilter

def create_icons():
    os.makedirs("assets/icons", exist_ok=True)

    # 1. Background (432x432)
    bg_size = 432
    bg = Image.new("RGBA", (bg_size, bg_size), (15, 23, 34, 255))
    draw_bg = ImageDraw.Draw(bg)

    # Radial gradient in background
    for r in range(bg_size // 2, 0, -2):
        factor = r / (bg_size / 2)
        # Deep military navy-steel to darker edge
        c_r = int(14 + 18 * (1.0 - factor))
        c_g = int(22 + 25 * (1.0 - factor))
        c_b = int(32 + 35 * (1.0 - factor))
        draw_bg.ellipse(
            [bg_size // 2 - r, bg_size // 2 - r, bg_size // 2 + r, bg_size // 2 + r],
            fill=(c_r, c_g, c_b, 255)
        )

    # Subtle tactical border accents
    border_color = (212, 160, 23, 70)
    draw_bg.rectangle([12, 12, bg_size - 13, bg_size - 13], outline=border_color, width=2)
    draw_bg.rectangle([20, 20, bg_size - 21, bg_size - 21], outline=(50, 80, 110, 45), width=1)

    bg_path = "assets/icons/icon_background_432.png"
    bg.save(bg_path, "PNG")
    print(f"Generated {bg_path}")

    # 2. Foreground (432x432)
    # Safe zone for Android adaptive icon is central 264px circle (r = 132px, cx=cy=216)
    fg = Image.new("RGBA", (bg_size, bg_size), (0, 0, 0, 0))
    draw_fg = ImageDraw.Draw(fg)

    cx, cy = bg_size // 2, bg_size // 2

    # Draw golden tactical ring
    ring_radius = 110
    draw_fg.ellipse(
        [cx - ring_radius, cy - ring_radius, cx + ring_radius, cy + ring_radius],
        outline=(245, 180, 28, 220),
        width=4
    )
    draw_fg.ellipse(
        [cx - ring_radius + 6, cy - ring_radius + 6, cx + ring_radius - 6, cy + ring_radius - 6],
        outline=(180, 130, 20, 140),
        width=2
    )

    # Load tank icon or draw RTS emblem inside
    tank_src = "assets/unity_migrated/Assets/Resources/LobbyGen/gen_icon_tank.png"
    if os.path.exists(tank_src):
        tank_img = Image.open(tank_src).convert("RGBA")
        tank_scaled = tank_img.resize((150, 150), Image.Resampling.LANCZOS)
        # Tint tank gold/amber
        r, g, b, a = tank_scaled.split()
        gold_tank = Image.new("RGBA", (150, 150), (255, 205, 50, 255))
        tank_tinted = Image.composite(gold_tank, tank_scaled, a)
        
        # Shadow
        shadow = Image.new("RGBA", (bg_size, bg_size), (0, 0, 0, 0))
        shadow.paste(Image.new("RGBA", (150, 150), (0, 0, 0, 160)), (cx - 75, cy - 65), a)
        shadow = shadow.filter(ImageFilter.GaussianBlur(6))
        fg = Image.alpha_composite(fg, shadow)
        fg.paste(tank_tinted, (cx - 75, cy - 70), a)
    else:
        # Fallback star emblem
        draw_fg.polygon([
            (cx, cy - 60), (cx + 18, cy - 18), (cx + 64, cy - 18),
            (cx + 28, cy + 12), (cx + 42, cy + 56), (cx, cy + 30),
            (cx - 42, cy + 56), (cx - 28, cy + 12), (cx - 64, cy - 18),
            (cx - 18, cy - 18)
        ], fill=(245, 180, 28, 255), outline=(255, 230, 120, 255))

    fg_path = "assets/icons/icon_foreground_432.png"
    fg.save(fg_path, "PNG")
    print(f"Generated {fg_path}")

    # 3. Main Legacy Icon (192x192) - Composite of bg and fg
    composite = Image.alpha_composite(bg, fg)
    legacy_192 = composite.resize((192, 192), Image.Resampling.LANCZOS)
    legacy_path = "assets/icons/app_icon_192.png"
    legacy_192.save(legacy_path, "PNG")
    print(f"Generated {legacy_path}")

    # 4. Generate into android/build/res mipmaps
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
        
        # Legacy icon
        ic = composite.resize((icon_sz, icon_sz), Image.Resampling.LANCZOS)
        ic.save(os.path.join(dir_path, "icon.webp"), "WEBP")
        
        # Adaptive foreground
        fg_scaled = fg.resize((adaptive_sz, adaptive_sz), Image.Resampling.LANCZOS)
        fg_scaled.save(os.path.join(dir_path, "icon_foreground.webp"), "WEBP")
        
        # Adaptive background
        bg_scaled = bg.resize((adaptive_sz, adaptive_sz), Image.Resampling.LANCZOS)
        bg_scaled.save(os.path.join(dir_path, "icon_background.webp"), "WEBP")
        print(f"Populated {dir_path} with icon.webp, icon_foreground.webp, icon_background.webp")

if __name__ == "__main__":
    create_icons()

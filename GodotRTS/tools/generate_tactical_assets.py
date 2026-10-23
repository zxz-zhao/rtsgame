import os
import math
from PIL import Image, ImageDraw, ImageFont, ImageFilter

def create_tactical_assets():
    os.makedirs("assets/icons", exist_ok=True)
    os.makedirs("assets/splash", exist_ok=True)

    # =========================================================================
    # 1. GENERATE MASTER GAME ICON (512x512)
    # =========================================================================
    size = 512
    icon = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(icon)
    cx, cy = size // 2, size // 2

    # Draw rounded-corner tactical square container (Chamfered shield)
    shield_pts = [
        (60, 40), (size - 60, 40),
        (size - 30, 70), (size - 30, size - 90),
        (size - 80, size - 40), (80, size - 40),
        (30, size - 90), (30, 70)
    ]

    # Drop shadow
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw_sh = ImageDraw.Draw(shadow)
    draw_sh.polygon(shield_pts, fill=(0, 0, 0, 180))
    shadow = shadow.filter(ImageFilter.GaussianBlur(14))
    icon = Image.alpha_composite(icon, shadow)
    draw = ImageDraw.Draw(icon)

    # Base shield gradient
    for i in range(120):
        factor = i / 120.0
        r = int(12 + factor * 14)
        g = int(18 + factor * 22)
        b = int(28 + factor * 35)
        inset = int(i * 0.4)
        poly = [
            (60 + inset, 40 + inset), (size - 60 - inset, 40 + inset),
            (size - 30 - inset, 70 + inset), (size - 30 - inset, size - 90 - inset),
            (size - 80 - inset, size - 40 - inset), (80 + inset, size - 40 - inset),
            (30 + inset, size - 90 - inset), (30 + inset, 70 + inset)
        ]
        draw.polygon(poly, fill=(r, g, b, 255))

    # Outer tactical border (Cyan & Gold)
    draw.polygon(shield_pts, outline=(0, 190, 245, 230), width=4)
    inner_pts = [
        (66, 46), (size - 66, 46),
        (size - 36, 74), (size - 36, size - 94),
        (size - 84, size - 46), (84, size - 46),
        (36, size - 94), (36, 74)
    ]
    draw.polygon(inner_pts, outline=(245, 175, 30, 180), width=2)

    # Corner brackets (Sci-Fi UI accents)
    b_len = 32
    draw.line([(30, 70), (30 + b_len, 70)], fill=(0, 220, 255, 255), width=3)
    draw.line([(30, 70), (30, 70 + b_len)], fill=(0, 220, 255, 255), width=3)
    draw.line([(size - 30, 70), (size - 30 - b_len, 70)], fill=(0, 220, 255, 255), width=3)
    draw.line([(size - 30, 70), (size - 30, 70 + b_len)], fill=(0, 220, 255, 255), width=3)

    # Concentric tactical radar rings in center
    for r in [170, 130, 90]:
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(30, 80, 120, 90), width=1)
    draw.ellipse([cx - 150, cy - 150, cx + 150, cy + 150], outline=(245, 180, 35, 120), width=2)

    # Radar crosshair ticks
    for angle_deg in range(0, 360, 30):
        rad = math.radians(angle_deg)
        x1 = cx + math.cos(rad) * 142
        y1 = cy + math.sin(rad) * 142
        x2 = cx + math.cos(rad) * 158
        y2 = cy + math.sin(rad) * 158
        color = (255, 200, 50, 200) if angle_deg % 90 == 0 else (0, 200, 255, 140)
        draw.line([(x1, y1), (x2, y2)], fill=color, width=2 if angle_deg % 90 == 0 else 1)

    # Stylized central chevron / military star
    # Load tank image if available, or draw high-tech Star & Chevron
    tank_src = "assets/unity_migrated/Assets/Resources/LobbyGen/gen_icon_tank.png"
    has_tank = False
    if os.path.exists(tank_src):
        try:
            tank_im = Image.open(tank_src).convert("RGBA")
            t_w, t_h = 240, 240
            tank_scaled = tank_im.resize((t_w, t_h), Image.Resampling.LANCZOS)
            
            # Gold tint overlay
            gold_tint = Image.new("RGBA", (t_w, t_h), (255, 215, 60, 255))
            tank_gold = Image.composite(gold_tint, tank_scaled, tank_scaled.split()[3])
            
            # Tank drop shadow
            t_sh = Image.new("RGBA", (size, size), (0, 0, 0, 0))
            t_sh.paste(Image.new("RGBA", (t_w, t_h), (0, 0, 0, 200)), (cx - t_w//2, cy - t_h//2 + 8), tank_scaled.split()[3])
            t_sh = t_sh.filter(ImageFilter.GaussianBlur(8))
            icon = Image.alpha_composite(icon, t_sh)
            icon.paste(tank_gold, (cx - t_w//2, cy - t_h//2), tank_scaled.split()[3])
            draw = ImageDraw.Draw(icon)
            has_tank = True
        except Exception as e:
            print("Fallback to geometric star:", e)

    if not has_tank:
        # 5-pointed sharp commander star with bevel
        points_outer = []
        for i in range(10):
            r = 100 if i % 2 == 0 else 44
            angle = math.radians(i * 36 - 90)
            points_outer.append((cx + math.cos(angle) * r, cy + math.sin(angle) * r))
        draw.polygon(points_outer, fill=(245, 185, 30, 255), outline=(255, 235, 130, 255))

    # Tactical Chevron Wings at bottom
    chev_pts = [
        (cx - 110, cy + 120), (cx, cy + 70), (cx + 110, cy + 120),
        (cx + 90, cy + 145), (cx, cy + 100), (cx - 90, cy + 145)
    ]
    draw.polygon(chev_pts, fill=(0, 200, 255, 230), outline=(180, 240, 255, 255))

    # Save 512x512 Master Icon
    icon_512_path = "assets/icons/app_icon_512.png"
    icon.save(icon_512_path, "PNG")
    print(f"Generated {icon_512_path}")

    # Save 192x192 Legacy Icon
    icon_192 = icon.resize((192, 192), Image.Resampling.LANCZOS)
    icon_192_path = "assets/icons/app_icon_192.png"
    icon_192.save(icon_192_path, "PNG")
    print(f"Generated {icon_192_path}")

    # Generate multi-resolution Windows .ico
    ico_path = "assets/icons/app_icon.ico"
    icon.save(ico_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
    print(f"Generated Windows Native {ico_path}")

    # Generate Android adaptive foreground & background
    bg_432 = Image.new("RGBA", (432, 432), (11, 19, 32, 255))
    draw_bg = ImageDraw.Draw(bg_432)
    for r in range(216, 0, -2):
        f = r / 216.0
        draw_bg.ellipse([216 - r, 216 - r, 216 + r, 216 + r], fill=(int(11 + 18*(1-f)), int(19 + 25*(1-f)), int(32 + 35*(1-f)), 255))
    draw_bg.rectangle([12, 12, 419, 419], outline=(0, 190, 245, 60), width=2)
    bg_432.save("assets/icons/icon_background_432.png", "PNG")

    fg_432 = Image.new("RGBA", (432, 432), (0, 0, 0, 0))
    fg_center = icon.resize((300, 300), Image.Resampling.LANCZOS)
    fg_432.paste(fg_center, (66, 66), fg_center.split()[3])
    fg_432.save("assets/icons/icon_foreground_432.png", "PNG")
    print("Generated Android adaptive icons in assets/icons/")

    # Also update mipmaps in android/build/res
    densities = {
        "mipmap-mdpi-v4": (48, 108),
        "mipmap-hdpi-v4": (72, 162),
        "mipmap-xhdpi-v4": (96, 216),
        "mipmap-xxhdpi-v4": (144, 324),
        "mipmap-xxxhdpi-v4": (192, 432),
        "mipmap": (192, 432),
    }
    for folder, (isz, asz) in densities.items():
        dp = os.path.join("android/build/res", folder)
        if os.path.exists(dp):
            icon.resize((isz, isz), Image.Resampling.LANCZOS).save(os.path.join(dp, "icon.webp"), "WEBP")
            fg_432.resize((asz, asz), Image.Resampling.LANCZOS).save(os.path.join(dp, "icon_foreground.webp"), "WEBP")
            bg_432.resize((asz, asz), Image.Resampling.LANCZOS).save(os.path.join(dp, "icon_background.webp"), "WEBP")

    # =========================================================================
    # 2. GENERATE TACTICAL BOOT SPLASH SCREEN (1280x720)
    # =========================================================================
    sw, sh = 1280, 720
    splash = Image.new("RGBA", (sw, sh), (9, 14, 22, 255))
    draw_sp = ImageDraw.Draw(splash)

    # Ambient radial gradient from center
    for r in range(int(math.sqrt(sw**2 + sh**2) // 2), 0, -4):
        f = r / (math.sqrt(sw**2 + sh**2) // 2)
        r_c = int(8 + (1.0 - f) * 16)
        g_c = int(12 + (1.0 - f) * 24)
        b_c = int(20 + (1.0 - f) * 38)
        draw_sp.ellipse([sw//2 - r, sh//2 - r, sw//2 + r, sh//2 + r], fill=(r_c, g_c, b_c, 255))

    # Subtle tactical grid lines
    grid_color = (0, 180, 240, 18)
    for x in range(0, sw, 60):
        draw_sp.line([(x, 0), (x, sh)], fill=grid_color, width=1)
    for y in range(0, sh, 60):
        draw_sp.line([(0, y), (sw, y)], fill=grid_color, width=1)

    # Tactical sonar circles in background
    for rad in [220, 340, 480]:
        draw_sp.ellipse([sw//2 - rad, sh//2 - 40 - rad, sw//2 + rad, sh//2 - 40 + rad], outline=(0, 200, 255, 30), width=1)

    # Paste central emblem (260x260)
    emblem = icon.resize((260, 260), Image.Resampling.LANCZOS)
    em_x, em_y = sw // 2 - 130, sh // 2 - 165
    splash.paste(emblem, (em_x, em_y), emblem.split()[3])

    # Try loading custom TrueType fonts if available, otherwise draw crisp geometric lines
    draw_sp = ImageDraw.Draw(splash)

    # Horizontal tactical dividers
    div_y = sh // 2 + 130
    draw_sp.line([(sw // 2 - 320, div_y), (sw // 2 + 320, div_y)], fill=(0, 200, 255, 140), width=2)
    draw_sp.line([(sw // 2 - 200, div_y + 4), (sw // 2 + 200, div_y + 4)], fill=(245, 175, 30, 180), width=1)
    
    # Tick marks on divider
    for tx in range(sw // 2 - 300, sw // 2 + 301, 50):
        draw_sp.line([(tx, div_y - 4), (tx, div_y + 4)], fill=(0, 220, 255, 200), width=1)

    # Title & Subtitle using PIL default or TTF
    font_title = None
    font_sub = None
    for font_candidate in ["C:/Windows/Fonts/msyh.ttc", "C:/Windows/Fonts/simhei.ttf", "C:/Windows/Fonts/arial.ttf"]:
        if os.path.exists(font_candidate):
            try:
                font_title = ImageFont.truetype(font_candidate, 42)
                font_sub = ImageFont.truetype(font_candidate, 18)
                font_status = ImageFont.truetype(font_candidate, 14)
                break
            except Exception:
                pass

    if font_title:
        title_text = "星 火  R T S"
        sub_text = "X I N G H U O   T A C T I C A L   W A R F A R E"
        status_text = "INITIALIZING TACTICAL BATTLEFIELD // 战术指挥系统加载中..."

        # Calculate bounding boxes
        tb = draw_sp.textbbox((0, 0), title_text, font=font_title)
        tw = tb[2] - tb[0]
        draw_sp.text((sw // 2 - tw // 2, div_y + 20), title_text, font=font_title, fill=(245, 195, 50, 255))

        sb = draw_sp.textbbox((0, 0), sub_text, font=font_sub)
        sw_w = sb[2] - sb[0]
        draw_sp.text((sw // 2 - sw_w // 2, div_y + 75), sub_text, font=font_sub, fill=(120, 180, 220, 230))

        stb = draw_sp.textbbox((0, 0), status_text, font=font_status)
        stw = stb[2] - stb[0]
        draw_sp.text((sw // 2 - stw // 2, sh - 55), status_text, font=font_status, fill=(0, 200, 255, 180))

    # Corner technical annotations
    draw_sp.text((40, 35), "SYS: VER 4.6-MONO [VULKAN/COMPAT]", fill=(70, 110, 140, 180), font=font_status)
    draw_sp.text((40, sh - 45), "LOC: 31°12'N 121°28'E // NET: READY", fill=(70, 110, 140, 180), font=font_status)
    draw_sp.text((sw - 280, sh - 45), "TACTICAL NETWORK: ONLINE", fill=(70, 110, 140, 180), font=font_status)

    splash_path = "assets/splash/boot_splash.png"
    splash.save(splash_path, "PNG")
    print(f"Generated Tactical Boot Splash {splash_path}")

    # =========================================================================
    # 3. UPDATE icon.svg
    # =========================================================================
    svg_content = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="128" height="128">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#0c1524"/>
      <stop offset="100%" stop-color="#18263d"/>
    </linearGradient>
    <linearGradient id="gold" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="#ffd54f"/>
      <stop offset="100%" stop-color="#f59e0b"/>
    </linearGradient>
  </defs>
  <!-- Background Shield -->
  <polygon points="18,12 110,12 118,20 118,104 104,116 24,116 10,104 10,20" fill="url(#bg)" stroke="#00c8ff" stroke-width="2.5"/>
  <polygon points="22,16 106,16 114,23 114,101 101,112 27,112 14,101 14,23" fill="none" stroke="#f59e0b" stroke-width="1.2" opacity="0.6"/>
  
  <!-- Radar circles -->
  <circle cx="64" cy="62" r="38" fill="none" stroke="#00c8ff" stroke-width="1" opacity="0.4"/>
  <circle cx="64" cy="62" r="26" fill="none" stroke="#ffd54f" stroke-width="1.5" opacity="0.6"/>
  <line x1="64" y1="20" x2="64" y2="104" stroke="#00c8ff" stroke-width="1" opacity="0.3"/>
  <line x1="22" y1="62" x2="106" y2="62" stroke="#00c8ff" stroke-width="1" opacity="0.3"/>

  <!-- Military Star / Tank Emblem -->
  <polygon points="64,36 71,53 89,53 74,64 80,81 64,70 48,81 54,64 39,53 57,53" fill="url(#gold)" stroke="#fff" stroke-width="1"/>
  <!-- Tactical Chevron -->
  <polygon points="36,92 64,78 92,92 86,98 64,87 42,98" fill="#00c8ff"/>
</svg>'''
    with open("icon.svg", "w", encoding="utf-8") as f:
        f.write(svg_content)
    print("Updated icon.svg")

if __name__ == "__main__":
    create_tactical_assets()

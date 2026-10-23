# -*- coding: utf-8 -*-
"""
Generate high-fidelity, military-satellite tactical preview images for all 12 playable RTS maps.
Produces 320x180 16:9 widescreen tactical thumbnails with biome-specific terrain,
elevation contours, water bodies, and military HUD coordinates.
"""
import os
import math
import random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "assets", "maps", "previews")
os.makedirs(OUTPUT_DIR, exist_ok=True)

WIDTH = 320
HEIGHT = 180

def create_noise_map(w, h, scale=20.0, seed=42):
    random.seed(seed)
    # Simple layered noise
    grid_w = int(w / scale) + 2
    grid_h = int(h / scale) + 2
    vals = [[random.random() for _ in range(grid_w)] for _ in range(grid_h)]
    
    img_data = []
    for y in range(h):
        gy = y / scale
        y0 = int(gy)
        y1 = y0 + 1
        fy = gy - y0
        # smoothstep
        fy = fy * fy * (3.0 - 2.0 * fy)
        
        for x in range(w):
            gx = x / scale
            x0 = int(gx)
            x1 = x0 + 1
            fx = gx - x0
            fx = fx * fx * (3.0 - 2.0 * fx)
            
            top = vals[y0][x0] * (1.0 - fx) + vals[y0][x1] * fx
            bot = vals[y1][x0] * (1.0 - fx) + vals[y1][x1] * fx
            val = top * (1.0 - fy) + bot * fy
            img_data.append(val)
    return img_data

def lerp_color(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return (
        int(c1[0] + (c2[0] - c1[0]) * t),
        int(c1[1] + (c2[1] - c1[1]) * t),
        int(c1[2] + (c2[2] - c1[2]) * t),
    )

def draw_tactical_hud(draw, map_id, biome_tag, scale_text="400M // 2-4 PLAYERS"):
    # Grid lines
    grid_color = (255, 255, 255, 22)
    for x in range(40, WIDTH, 60):
        draw.line([(x, 0), (x, HEIGHT)], fill=grid_color, width=1)
    for y in range(30, HEIGHT, 45):
        draw.line([(0, y), (WIDTH, y)], fill=grid_color, width=1)
        
    # Crosshairs
    cx, cy = WIDTH // 2, HEIGHT // 2
    ch_color = (255, 220, 100, 60)
    draw.line([(cx - 15, cy), (cx + 15, cy)], fill=ch_color, width=1)
    draw.line([(cx, cy - 15), (cx, cy + 15)], fill=ch_color, width=1)
    draw.ellipse([(cx - 6, cy - 6), (cx + 6, cy + 6)], outline=ch_color, width=1)
    
    # Outer tactical border
    border_color = (120, 160, 200, 80)
    draw.rectangle([(2, 2), (WIDTH - 3, HEIGHT - 3)], outline=border_color, width=1)
    
    # Corner brackets
    corner_len = 12
    c_color = (255, 200, 60, 180)
    # Top-Left
    draw.line([(4, 4), (4 + corner_len, 4)], fill=c_color, width=2)
    draw.line([(4, 4), (4, 4 + corner_len)], fill=c_color, width=2)
    # Top-Right
    draw.line([(WIDTH - 5, 4), (WIDTH - 5 - corner_len, 4)], fill=c_color, width=2)
    draw.line([(WIDTH - 5, 4), (WIDTH - 5, 4 + corner_len)], fill=c_color, width=2)
    # Bottom-Left
    draw.line([(4, HEIGHT - 5), (4 + corner_len, HEIGHT - 5)], fill=c_color, width=2)
    draw.line([(4, HEIGHT - 5), (4, HEIGHT - 5 - corner_len)], fill=c_color, width=2)
    # Bottom-Right
    draw.line([(WIDTH - 5, HEIGHT - 5), (WIDTH - 5 - corner_len, HEIGHT - 5)], fill=c_color, width=2)
    draw.line([(WIDTH - 5, HEIGHT - 5), (WIDTH - 5, HEIGHT - 5 - corner_len)], fill=c_color, width=2)
    
    # Biome Tag pill in Top-Right
    tag_w = 80
    tag_h = 18
    tag_x = WIDTH - tag_w - 10
    tag_y = 8
    draw.rectangle([(tag_x, tag_y), (tag_x + tag_w, tag_y + tag_h)], fill=(10, 18, 26, 200), outline=(0, 200, 255, 140))
    draw.text((tag_x + 6, tag_y + 2), biome_tag, fill=(180, 230, 255, 240))
    
    # Sector Code in Top-Left
    draw.rectangle([(10, 8), (10 + 64, 8 + 18)], fill=(10, 18, 26, 200), outline=(255, 180, 50, 140))
    draw.text((16, 10), map_id, fill=(255, 200, 80, 240))
    
    # Scale in Bottom-Left
    draw.text((12, HEIGHT - 20), scale_text, fill=(200, 220, 240, 160))

MAPS_CONFIG = [
    {
        "filename": "map_sand_oasis.png",
        "name": "沙漠绿洲",
        "id": "SEC-01",
        "tag": "DESERT",
        "c_base1": (220, 180, 115),  # Dunes light
        "c_base2": (175, 130, 75),   # Dunes shadow
        "c_feature": (30, 160, 180), # Turquoise Oasis
        "feature_type": "oasis",
        "seed": 101,
    },
    {
        "filename": "map_ice_fortress.png",
        "name": "冰雪要塞",
        "id": "SEC-02",
        "tag": "GLACIAL",
        "c_base1": (215, 230, 245),  # Snow bright
        "c_base2": (140, 165, 190),  # Ice rock shadow
        "c_feature": (70, 140, 190),  # Frozen river
        "feature_type": "river_diag",
        "seed": 202,
    },
    {
        "filename": "map_jungle.png",
        "name": "丛林战场",
        "id": "SEC-03",
        "tag": "JUNGLE",
        "c_base1": (35, 95, 35),     # Dense canopy
        "c_base2": (18, 55, 18),     # Shadow forest
        "c_feature": (30, 110, 160), # Meandering river
        "feature_type": "river_s",
        "seed": 303,
    },
    {
        "filename": "map_city_ruins.png",
        "name": "城市废墟",
        "id": "SEC-04",
        "tag": "URBAN",
        "c_base1": (95, 95, 92),     # Concrete rubble
        "c_base2": (48, 48, 52),     # Dark asphalt
        "c_feature": (180, 130, 40), # Glowing hazard street
        "feature_type": "city_grid",
        "seed": 404,
    },
    {
        "filename": "map_sea_chart.png",
        "name": "海图群岛",
        "id": "SEC-05",
        "tag": "OCEANIC",
        "c_base1": (18, 55, 110),    # Deep ocean
        "c_base2": (28, 110, 160),   # Lagoon shallows
        "c_feature": (40, 140, 60),  # Island atolls
        "feature_type": "islands",
        "seed": 505,
    },
    {
        "filename": "map_volcano.png",
        "name": "火山要塞",
        "id": "SEC-06",
        "tag": "VOLCANIC",
        "c_base1": (45, 38, 40),     # Charred basalt
        "c_base2": (25, 20, 22),     # Black obsidian
        "c_feature": (255, 80, 20),  # Glowing magma rivers
        "feature_type": "volcano_crater",
        "seed": 606,
    },
    {
        "filename": "map_gobi.png",
        "name": "戈壁荒漠",
        "id": "SEC-07",
        "tag": "CANYON",
        "c_base1": (195, 115, 65),   # Terracotta sandstone
        "c_base2": (135, 65, 35),    # Canyon shadow
        "c_feature": (230, 175, 110),# Sand wash
        "feature_type": "double_canyon",
        "seed": 707,
    },
    {
        "filename": "map_arctic_tundra.png",
        "name": "极地冰川",
        "id": "SEC-08",
        "tag": "TUNDRA",
        "c_base1": (190, 215, 235),  # Ice shelf
        "c_base2": (110, 145, 180),  # Glacial depths
        "c_feature": (40, 180, 210), # Crevasse chasm
        "feature_type": "crevasse",
        "seed": 808,
    },
    {
        "filename": "map_tianshan.png",
        "name": "天山高峡",
        "id": "SEC-09",
        "tag": "ALPINE",
        "c_base1": (50, 85, 60),     # Pine ridge
        "c_base2": (180, 200, 215),  # Snow peak
        "c_feature": (50, 130, 170), # Alpine river gorge
        "feature_type": "alpine_pass",
        "seed": 909,
    },
    {
        "filename": "map_kunlun.png",
        "name": "昆仑黑山",
        "id": "SEC-10",
        "tag": "OBSIDIAN",
        "c_base1": (35, 35, 42),     # Black peak
        "c_base2": (65, 60, 75),     # Violet mist rock
        "c_feature": (160, 140, 200),# Mystic valley mist
        "feature_type": "black_peaks",
        "seed": 1010,
    },
    {
        "filename": "map_green_valley.png",
        "name": "绿谷雄峰",
        "id": "SEC-11",
        "tag": "VALLEY",
        "c_base1": (55, 130, 45),    # Lush meadow
        "c_base2": (85, 105, 55),    # Terraced plateau
        "c_feature": (40, 145, 195), # Mountain lake
        "feature_type": "valley_lake",
        "seed": 1111,
    },
    {
        "filename": "map_tri_lane.png",
        "name": "三路大会战",
        "id": "SEC-12",
        "tag": "3-LANE WAR",
        "c_base1": (70, 75, 70),     # Symmetrical battlefield
        "c_base2": (40, 42, 45),     # Fortified grounds
        "c_feature": (220, 160, 50), # Golden central junction
        "feature_type": "tri_lane",
        "seed": 1212,
    },
]

def render_map_image(cfg):
    img = Image.new("RGBA", (WIDTH, HEIGHT))
    noise = create_noise_map(WIDTH, HEIGHT, scale=24.0, seed=cfg["seed"])
    noise2 = create_noise_map(WIDTH, HEIGHT, scale=12.0, seed=cfg["seed"] + 55)
    
    pixels = []
    ftype = cfg["feature_type"]
    
    for y in range(HEIGHT):
        ny = y / float(HEIGHT)
        for x in range(WIDTH):
            nx = x / float(WIDTH)
            idx = y * WIDTH + x
            n1 = noise[idx]
            n2 = noise2[idx]
            n = n1 * 0.7 + n2 * 0.3
            
            # Base color gradient
            color = lerp_color(cfg["c_base2"], cfg["c_base1"], n)
            
            # Feature overlays
            if ftype == "oasis":
                # Lake in center with palm fringe
                dx = (nx - 0.5) * 1.6
                dy = (ny - 0.5)
                dist = math.sqrt(dx*dx + dy*dy) + (n1 - 0.5) * 0.2
                if dist < 0.22:
                    color = lerp_color(cfg["c_feature"], (15, 80, 110), dist / 0.22)
                elif dist < 0.28:
                    color = (40, 120, 40) # Oasis greenery
            elif ftype == "river_diag":
                # Diagonal river with bridge crossing
                d = abs(nx - ny + (n1 - 0.5) * 0.25)
                if d < 0.08:
                    color = cfg["c_feature"]
                elif d < 0.12:
                    color = (180, 200, 220) # Ice bank
            elif ftype == "river_s":
                # S-curve river
                target_x = 0.5 + 0.25 * math.sin(ny * math.pi * 2.0) + (n1 - 0.5) * 0.15
                if abs(nx - target_x) < 0.07:
                    color = cfg["c_feature"]
            elif ftype == "city_grid":
                # Grid streets
                gx = (x % 36) < 4
                gy = (y % 30) < 4
                if gx or gy:
                    color = cfg["c_feature"] if (gx and gy) else (40, 40, 44)
            elif ftype == "islands":
                # Atoll islands in blue ocean
                if n > 0.55:
                    color = cfg["c_feature"]
                elif n > 0.48:
                    color = (210, 195, 140) # Sand beach
                else:
                    color = lerp_color(cfg["c_base1"], cfg["c_base2"], n / 0.48)
            elif ftype == "volcano_crater":
                # Caldera ring & lava fissures
                dx = (nx - 0.5) * 1.4
                dy = (ny - 0.5)
                dist = math.sqrt(dx*dx + dy*dy)
                if abs(dist - 0.32) < 0.06 + (n1 - 0.5) * 0.08:
                    color = cfg["c_feature"] # Magma
                elif dist < 0.20:
                    color = (255, 140, 30) # Core heat
            elif ftype == "double_canyon":
                # 2 canyons
                c1 = abs(ny - 0.33 + (n1 - 0.5) * 0.12)
                c2 = abs(ny - 0.67 + (n1 - 0.5) * 0.12)
                if c1 < 0.06 or c2 < 0.06:
                    color = cfg["c_feature"]
            elif ftype == "crevasse":
                # Jagged polar rift
                c = abs(nx - 0.5 + (n1 - 0.5) * 0.3)
                if c < 0.06:
                    color = cfg["c_feature"]
                elif c < 0.10:
                    color = (70, 110, 150)
            elif ftype == "alpine_pass":
                # High altitude pass
                if ny < 0.35 + (n1 - 0.5) * 0.2:
                    color = cfg["c_base2"] # Snow peak
                elif abs(nx - 0.5) < 0.09:
                    color = cfg["c_feature"] # Stream
            elif ftype == "black_peaks":
                # Obsidian jagged peaks
                if n > 0.62:
                    color = (15, 15, 18)
                elif n < 0.38:
                    color = cfg["c_feature"]
            elif ftype == "valley_lake":
                # Mountain lake in bottom half
                if ny > 0.55 and abs(nx - 0.5) < 0.35 + (n1 - 0.5) * 0.15:
                    color = cfg["c_feature"]
            elif ftype == "tri_lane":
                # Top, Mid, Bot lanes
                l_top = abs(ny - 0.2)
                l_mid = abs(ny - (0.15 + nx * 0.7))
                l_bot = abs(ny - 0.8)
                if l_top < 0.05 or l_mid < 0.05 or l_bot < 0.05:
                    color = (90, 100, 110)
                # Bases
                if (nx < 0.15 and ny < 0.3) or (nx > 0.85 and ny > 0.7):
                    color = cfg["c_feature"]
                    
            # Vignette
            vx = abs(nx - 0.5) * 2.0
            vy = abs(ny - 0.5) * 2.0
            vign = math.sqrt(vx*vx + vy*vy) * 0.35
            vign = max(0.0, min(0.65, vign))
            color = lerp_color(color, (5, 8, 12), vign)
            
            pixels.append((color[0], color[1], color[2], 255))
            
    img.putdata(pixels)
    
    # Slight blur to soften pixel noise
    img = img.filter(ImageFilter.GaussianBlur(radius=0.7))
    
    # Overlay HUD
    overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw_tactical_hud(draw, cfg["id"], cfg["tag"])
    
    final_img = Image.alpha_composite(img, overlay)
    
    out_path = os.path.join(OUTPUT_DIR, cfg["filename"])
    final_img.save(out_path, "PNG")
    print(f"Generated map preview: {out_path} ({os.path.getsize(out_path)} bytes)")

def main():
    print(f"Rendering 12 tactical satellite battlefield previews into {OUTPUT_DIR}...")
    for cfg in MAPS_CONFIG:
        render_map_image(cfg)
    print("All 12 map previews successfully generated!")

if __name__ == "__main__":
    main()

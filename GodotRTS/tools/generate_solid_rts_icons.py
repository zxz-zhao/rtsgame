import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

# -------------------------------------------------------------------------
# CONSTANTS & UTILITIES
# -------------------------------------------------------------------------
SIZE = 512
CX, CY = SIZE // 2, SIZE // 2

def create_canvas():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

def downsample(img, target_size=128):
    return img.resize((target_size, target_size), Image.Resampling.LANCZOS)

def draw_badge_plate(d, primary_col=(22, 30, 40), accent_col=(255, 200, 50), r=236, chamfer=60):
    """Draws a solid military/tactical chamfered shield badge with bevel and rim."""
    pts = [
        (CX - r + chamfer, CY - r), (CX + r - chamfer, CY - r),
        (CX + r, CY - r + chamfer), (CX + r, CY + r - chamfer),
        (CX + r - chamfer, CY + r), (CX - r + chamfer, CY + r),
        (CX - r, CY + r - chamfer), (CX - r, CY - r + chamfer)
    ]
    # Outer dark shadow / border plate
    d.polygon(pts, fill=(10, 14, 18, 255))
    
    # Gradient filled inner badge
    steps = 28
    for i in range(1, steps):
        t = i / float(steps)
        r_curr = r - i
        c_curr = max(8, int(chamfer - i * 0.75))
        # Top-to-bottom subtle gradient
        cr = int(primary_col[0] * (1.1 - t * 0.4) + 10 * t)
        cg = int(primary_col[1] * (1.1 - t * 0.4) + 12 * t)
        cb = int(primary_col[2] * (1.1 - t * 0.4) + 16 * t)
        poly = [
            (CX - r_curr + c_curr, CY - r_curr), (CX + r_curr - c_curr, CY - r_curr),
            (CX + r_curr, CY - r_curr + c_curr), (CX + r_curr, CY + r_curr - c_curr),
            (CX + r_curr - c_curr, CY + r_curr), (CX - r_curr + c_curr, CY + r_curr),
            (CX - r_curr, CY + r_curr - c_curr), (CX - r_curr, CY - r_curr + c_curr)
        ]
        d.polygon(poly, fill=(cr, cg, cb, 255))

    # Inner subtle grid / tech texture
    for y in range(CY - r + 30, CY + r - 30, 36):
        d.line([(CX - r + 40, y), (CX + r - 40, y)], fill=(primary_col[0] + 15, primary_col[1] + 18, primary_col[2] + 22, 90), width=1)

    # Accent metallic rim
    d.polygon(pts, outline=accent_col, width=6)
    
    # Inner border line
    in_pts = [
        (CX - r + chamfer + 8, CY - r + 8), (CX + r - chamfer - 8, CY - r + 8),
        (CX + r - 8, CY - r + chamfer + 8), (CX + r - 8, CY + r - chamfer - 8),
        (CX + r - chamfer - 8, CY + r - 8), (CX - r + chamfer + 8, CY + r - 8),
        (CX - r + 8, CY + r - chamfer - 8), (CX - r + 8, CY - r + chamfer + 8)
    ]
    rim_dark = (max(0, accent_col[0] // 2), max(0, accent_col[1] // 2), max(0, accent_col[2] // 2), 200)
    d.polygon(in_pts, outline=rim_dark, width=2)

    # Corner tactical rivets
    for pt in pts:
        rx, ry = pt
        dx = 1 if rx < CX else -1
        dy = 1 if ry < CY else -1
        bx = rx + dx * 16
        by = ry + dy * 16
        d.ellipse([bx - 5, by - 5, bx + 5, by + 5], fill=(220, 230, 240, 255), outline=(30, 40, 50, 255), width=2)
        d.ellipse([bx - 2, by - 2, bx + 1, by + 1], fill=(255, 255, 255, 255))

# -------------------------------------------------------------------------
# HUD ACTION BUTTONS
# -------------------------------------------------------------------------
def generate_hud_build_icon(path):
    img = create_canvas()
    d = ImageDraw.Draw(img)
    # 1. Solid Gold/Amber Tactical Badge
    draw_badge_plate(d, primary_col=(35, 28, 18), accent_col=(255, 195, 45), r=236, chamfer=60)

    # 2. Heavy Military Fortress / Bunker Base
    # Foundation base block
    d.rectangle([CX - 160, CY + 80, CX + 160, CY + 170], fill=(60, 68, 76), outline=(90, 102, 114), width=4)
    # Foundation bevel / shadow
    d.rectangle([CX - 160, CY + 150, CX + 160, CY + 170], fill=(38, 44, 50))
    # Fortress Wall with battlements
    wall_pts = [
        (CX - 140, CY + 80), (CX + 140, CY + 80),
        (CX + 140, CY - 10), (CX + 110, CY - 10),
        (CX + 110, CY + 20), (CX + 60, CY + 20),
        (CX + 60, CY - 20), (CX + 25, CY - 20),
        (CX + 25, CY + 10), (CX - 25, CY + 10),
        (CX - 25, CY - 20), (CX - 60, CY - 20),
        (CX - 60, CY + 20), (CX - 110, CY + 20),
        (CX - 110, CY - 10), (CX - 140, CY - 10)
    ]
    d.polygon(wall_pts, fill=(85, 96, 108), outline=(130, 145, 160), width=4)
    
    # Wall hazard stripes at foundation
    for sx in range(CX - 150, CX + 150, 30):
        d.polygon([(sx, CY + 84), (sx + 15, CY + 84), (sx + 5, CY + 140), (sx - 10, CY + 140)], fill=(230, 175, 25))

    # Fortress main armored gate
    d.rectangle([CX - 30, CY + 90, CX + 30, CY + 170], fill=(25, 30, 35), outline=(160, 175, 190), width=3)
    d.line([(CX, CY + 90), (CX, CY + 170)], fill=(120, 135, 150), width=3)
    # Firing slits
    for fx in [CX - 90, CX + 70]:
        d.rectangle([fx, CY + 35, fx + 25, CY + 55], fill=(20, 24, 28), outline=(180, 200, 220), width=2)
        d.rectangle([fx + 4, CY + 42, fx + 21, CY + 48], fill=(255, 215, 60))

    # 3. Heavy Construction Tower Crane
    crane_col = (255, 185, 20)
    crane_dark = (170, 115, 10)
    # Mast tower
    d.rectangle([CX + 35, CY - 160, CX + 75, CY - 10], fill=crane_col, outline=(30, 30, 30), width=3)
    for my in range(CY - 150, CY - 10, 25):
        d.line([(CX + 35, my), (CX + 75, my + 15)], fill=crane_dark, width=4)
        d.line([(CX + 75, my), (CX + 35, my + 15)], fill=crane_dark, width=4)
    
    # Top cab
    d.rectangle([CX + 25, CY - 175, CX + 85, CY - 155], fill=(50, 60, 70), outline=crane_col, width=3)
    d.rectangle([CX + 35, CY - 172, CX + 55, CY - 158], fill=(120, 220, 255)) # Cab glass

    # Horizontal Jib Boom
    d.polygon([(CX - 180, CY - 185), (CX + 140, CY - 185), (CX + 130, CY - 165), (CX - 180, CY - 165)], fill=crane_col, outline=(30, 30, 30), width=3)
    # Counterweight block on right
    d.rectangle([CX + 85, CY - 195, CX + 140, CY - 160], fill=(80, 90, 100), outline=(40, 45, 50), width=3)
    
    # Tie cables from apex
    apex = (CX + 55, CY - 215)
    d.polygon([(CX + 45, CY - 185), apex, (CX + 65, CY - 185)], fill=crane_col)
    d.line([apex, (CX - 170, CY - 185)], fill=(220, 230, 240), width=3)
    d.line([apex, (CX + 125, CY - 185)], fill=(220, 230, 240), width=3)

    # Trolley, Hoist Cable & Heavy Forged Hook
    trolley_x = CX - 90
    d.rectangle([trolley_x - 12, CY - 165, trolley_x + 12, CY - 155], fill=(40, 50, 60))
    d.line([(trolley_x, CY - 155), (trolley_x, CY - 60)], fill=(230, 240, 255), width=3)
    # Hook pulley block
    d.rectangle([trolley_x - 14, CY - 60, trolley_x + 14, CY - 40], fill=crane_col, outline=(30, 30, 30), width=2)
    d.arc([trolley_x - 16, CY - 40, trolley_x + 16, CY - 8], 0, 180, fill=(230, 235, 240), width=7)
    d.line([(trolley_x - 16, CY - 25), (trolley_x - 16, CY - 35)], fill=(230, 235, 240), width=7)

    # Steel I-Beam suspended by hook
    beam_x = trolley_x
    beam_y = CY + 8
    d.line([(beam_x - 10, CY - 14), (beam_x - 45, beam_y - 8)], fill=(200, 210, 220), width=2)
    d.line([(beam_x + 6, CY - 14), (beam_x + 45, beam_y - 8)], fill=(200, 210, 220), width=2)
    # Solid I-Beam
    d.rectangle([beam_x - 65, beam_y - 8, beam_x + 65, beam_y + 8], fill=(160, 40, 30), outline=(230, 80, 60), width=2)

    out = downsample(img, 128)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    out.save(path)
    print(f"Generated solid HUD build icon: {path}")

def generate_hud_tech_icon(path):
    img = create_canvas()
    d = ImageDraw.Draw(img)
    # 1. Solid Cyan/Electric Blue Tactical Tech Badge
    draw_badge_plate(d, primary_col=(14, 28, 42), accent_col=(40, 215, 255), r=236, chamfer=60)

    # 2. Glowing Quantum Tech Gyro Rings (Rendered as tilted solid rings)
    ring_img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    rd = ImageDraw.Draw(ring_img)
    # Outer ring
    rd.ellipse([CX - 170, CY - 65, CX + 170, CY + 65], outline=(30, 160, 230), width=14)
    rd.ellipse([CX - 170, CY - 65, CX + 170, CY + 65], outline=(180, 245, 255), width=6)
    # Electron nodes
    rd.ellipse([CX - 165, CY - 12, CX - 140, CY + 12], fill=(255, 255, 255), outline=(40, 220, 255), width=3)
    rd.ellipse([CX + 140, CY - 12, CX + 165, CY + 12], fill=(255, 255, 255), outline=(40, 220, 255), width=3)

    r1 = ring_img.rotate(35, resample=Image.Resampling.BICUBIC, center=(CX, CY))
    r2 = ring_img.rotate(-35, resample=Image.Resampling.BICUBIC, center=(CX, CY))
    img.alpha_composite(r1)
    img.alpha_composite(r2)

    d = ImageDraw.Draw(img)
    # 3. Outer Hexagonal Circuit Bus
    hex_pts = []
    for i in range(6):
        ang = math.radians(60 * i - 30)
        hex_pts.append((CX + 145 * math.cos(ang), CY + 145 * math.sin(ang)))
    d.polygon(hex_pts, fill=(18, 45, 68), outline=(60, 200, 255), width=5)
    for pt in hex_pts:
        d.ellipse([pt[0] - 10, pt[1] - 10, pt[0] + 10, pt[1] + 10], fill=(240, 255, 255), outline=(20, 180, 255), width=3)
        # Radial bus line toward center
        d.line([pt, (CX, CY)], fill=(30, 120, 180), width=3)

    # 4. Central Fusion Core / Atomic Sphere
    # Glow aura
    for r in range(95, 30, -5):
        alpha = int(220 * (1.0 - (r - 30) / 65.0))
        d.ellipse([CX - r, CY - r, CX + r, CY + r], fill=(20, 180, 255, alpha))
    # Solid Core Sphere with 3D spherical shading
    d.ellipse([CX - 50, CY - 50, CX + 50, CY + 50], fill=(80, 230, 255), outline=(255, 255, 255), width=5)
    d.ellipse([CX - 32, CY - 32, CX + 10, CY + 10], fill=(255, 255, 255)) # Specular highlight

    # Microchip cross circuit bars
    d.rectangle([CX - 180, CY - 4, CX - 120, CY + 4], fill=(60, 220, 255))
    d.rectangle([CX + 120, CY - 4, CX + 180, CY + 4], fill=(60, 220, 255))
    d.rectangle([CX - 4, CY - 180, CX + 4, CY - 120], fill=(60, 220, 255))
    d.rectangle([CX - 4, CY + 120, CX + 4, CY + 180], fill=(60, 220, 255))

    out = downsample(img, 128)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    out.save(path)
    print(f"Generated solid HUD tech icon: {path}")

# -------------------------------------------------------------------------
# BUILDING ICONS (96x96 for cards & HUD)
# -------------------------------------------------------------------------
def generate_building_icon(path, title, draw_fn, primary_col=(25, 32, 40), accent_col=(255, 200, 50)):
    img = create_canvas()
    d = ImageDraw.Draw(img)
    # Solid tactical badge
    draw_badge_plate(d, primary_col=primary_col, accent_col=accent_col, r=236, chamfer=60)
    # Custom solid artwork
    draw_fn(d)
    out = downsample(img, 96)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    out.save(path)
    print(f"Generated solid building icon: {title} -> {path}")

def draw_barracks_solid(d):
    # Military Garrison Barracks (Camouflage green roofs, brick/concrete walls, antenna mast)
    # Ground slab
    d.rectangle([CX - 160, CY + 110, CX + 160, CY + 145], fill=(45, 50, 48), outline=(80, 90, 85), width=3)
    # Left Barrack Building
    d.rectangle([CX - 150, CY + 10, CX - 10, CY + 110], fill=(95, 102, 90), outline=(40, 45, 40), width=4)
    # Right Barrack Building
    d.rectangle([CX + 10, CY + 10, CX + 150, CY + 110], fill=(85, 92, 80), outline=(40, 45, 40), width=4)
    # Sloped Camouflage Roofs (Green & Khaki)
    roof_left = [(CX - 165, CY + 15), (CX - 80, CY - 65), (CX + 5, CY + 15)]
    d.polygon(roof_left, fill=(55, 88, 52), outline=(130, 180, 120), width=4)
    roof_right = [(CX - 5, CY + 15), (CX + 80, CY - 65), (CX + 165, CY + 15)]
    d.polygon(roof_right, fill=(45, 76, 42), outline=(130, 180, 120), width=4)
    
    # Roof ridges / camouflage pattern
    d.polygon([(CX - 130, CY + 15), (CX - 80, CY - 65), (CX - 55, CY + 15)], fill=(75, 108, 68))
    d.polygon([(CX + 30, CY + 15), (CX + 80, CY - 65), (CX + 115, CY + 15)], fill=(65, 98, 58))

    # Military Entrances
    d.rectangle([CX - 100, CY + 55, CX - 60, CY + 110], fill=(25, 30, 28), outline=(180, 200, 170), width=2)
    d.rectangle([CX + 60, CY + 55, CX + 100, CY + 110], fill=(25, 30, 28), outline=(180, 200, 170), width=2)
    # Illuminated Windows
    for wx in [CX - 135, CX - 25, CX + 25, CX + 135]:
        d.rectangle([wx - 10, CY + 25, wx + 10, CY + 45], fill=(255, 230, 120), outline=(40, 40, 40), width=2)

    # Radio Communications Mast / Antenna
    d.line([(CX, CY + 10), (CX, CY - 165)], fill=(220, 230, 240), width=6)
    for ay in [CY - 145, CY - 115, CY - 85]:
        w = int(25 * (1.0 - (CY - ay) / 200.0))
        d.line([(CX - w, ay), (CX + w, ay)], fill=(220, 230, 240), width=4)
    # Red Warning Beacon on mast tip
    d.ellipse([CX - 9, CY - 174, CX + 9, CY - 156], fill=(255, 50, 40), outline=(255, 255, 255), width=2)

    # Infantry Cross-Rifles / Chevron Emblem in center
    d.polygon([(CX - 22, CY + 25), (CX, CY + 5), (CX + 22, CY + 25), (CX, CY + 42)], fill=(255, 200, 50), outline=(20, 20, 20), width=2)

def draw_tank_factory_solid(d):
    # Armor Factory / 坦克工厂 (Heavy battle tank chassis, turret, cannon, industrial gear)
    # Industrial Cog Gear in background
    gear_pts = []
    for i in range(16):
        r = 175 if i % 2 == 0 else 145
        ang = math.radians(22.5 * i)
        gear_pts.append((CX + r * math.cos(ang), CY - 10 + r * math.sin(ang)))
    d.polygon(gear_pts, fill=(50, 58, 68), outline=(85, 96, 110), width=4)

    # Heavy Tank Chassis
    chassis_pts = [
        (CX - 150, CY + 120), (CX + 150, CY + 120),
        (CX + 165, CY + 60), (CX + 125, CY + 35),
        (CX - 125, CY + 35), (CX - 165, CY + 60)
    ]
    d.polygon(chassis_pts, fill=(70, 78, 65), outline=(130, 145, 120), width=5)
    # Track treads
    d.polygon([(CX - 155, CY + 115), (CX + 155, CY + 115), (CX + 160, CY + 75), (CX - 160, CY + 75)], fill=(35, 38, 40), outline=(20, 20, 20), width=3)
    # Roadwheels
    for wx in range(CX - 120, CX + 130, 48):
        d.ellipse([wx - 18, CY + 77, wx + 18, CY + 113], fill=(80, 88, 92), outline=(20, 20, 20), width=4)
        d.ellipse([wx - 7, CY + 88, wx + 7, CY + 102], fill=(210, 220, 230))

    # Heavy Turret
    d.polygon([(CX - 85, CY + 35), (CX + 85, CY + 35), (CX + 65, CY - 25), (CX - 65, CY - 25)], fill=(90, 102, 85), outline=(145, 165, 135), width=4)
    # Commander cupola
    d.rectangle([CX - 40, CY - 40, CX + 10, CY - 25], fill=(60, 68, 56), outline=(145, 165, 135), width=3)

    # Massive 120mm Tank Cannon Barrel pointing up-right
    d.rectangle([CX - 12, CY - 155, CX + 12, CY - 25], fill=(55, 62, 68), outline=(140, 155, 170), width=3)
    # Double-baffle Muzzle Brake
    d.rectangle([CX - 22, CY - 175, CX + 22, CY - 150], fill=(40, 45, 50), outline=(255, 195, 45), width=3)

def draw_special_factory_solid(d):
    # Special Factory / 特勤工厂 (Rocket Artillery MLRS vehicle with multi-tube launch pack)
    # Armored Heavy 8x8 Truck Chassis
    d.polygon([(CX - 145, CY + 120), (CX + 145, CY + 120), (CX + 155, CY + 65), (CX - 155, CY + 65)], fill=(65, 75, 82), outline=(120, 135, 145), width=4)
    # Heavy off-road tires
    for wx in [-115, -45, 45, 115]:
        d.rectangle([CX + wx - 20, CY + 85, CX + wx + 20, CY + 135], fill=(30, 32, 35), outline=(15, 15, 15), width=3)
        d.rectangle([CX + wx - 10, CY + 95, CX + wx + 10, CY + 125], fill=(90, 100, 108))

    # Armored Crew Cab on Right
    d.polygon([(CX + 45, CY + 65), (CX + 140, CY + 65), (CX + 125, CY + 5), (CX + 55, CY + 5)], fill=(80, 92, 100), outline=(140, 160, 175), width=4)
    # Bulletproof windshields
    d.polygon([(CX + 75, CY + 45), (CX + 120, CY + 45), (CX + 110, CY + 15), (CX + 75, CY + 15)], fill=(120, 210, 245), outline=(20, 30, 40), width=2)

    # Elevated MLRS Rocket Pod (Tilted launch rack)
    pod_pts = [
        (CX - 135, CY + 45), (CX + 15, CY - 85),
        (CX - 25, CY - 130), (CX - 175, CY)
    ]
    d.polygon(pod_pts, fill=(50, 60, 65), outline=(160, 185, 200), width=4)
    
    # 6 Rocket Tubes facing top-right
    for off in range(0, 4):
        bx = CX - 120 + off * 25
        by = CY + 15 - off * 25
        d.polygon([(bx, by), (bx + 85, by - 75), (bx + 70, by - 90), (bx - 15, by - 15)], fill=(40, 48, 52), outline=(80, 95, 105), width=2)
        # Rocket warhead tip
        d.polygon([(bx + 85, by - 75), (bx + 102, by - 90), (bx + 87, by - 105), (bx + 70, by - 90)], fill=(255, 80, 50), outline=(255, 220, 100), width=2)

def draw_airfield_solid(d):
    # Airfield / 停机坪 (Tarmac, Helipad 'H', runway lights, ATC tower)
    # Hexagonal tarmac slab
    slab_pts = [
        (CX - 165, CY + 60), (CX - 75, CY + 150),
        (CX + 75, CY + 150), (CX + 165, CY + 60),
        (CX + 120, CY - 60), (CX - 120, CY - 60)
    ]
    d.polygon(slab_pts, fill=(48, 54, 60), outline=(90, 102, 115), width=5)
    
    # Runway approach centerline stripes
    for y in range(CY - 45, CY + 140, 35):
        d.rectangle([CX - 8, y, CX + 8, y + 20], fill=(255, 205, 40))

    # Helipad White Circle & 'H'
    d.ellipse([CX - 85, CY - 15, CX + 85, CY + 115], fill=(38, 44, 50), outline=(245, 250, 255), width=8)
    # Bold 'H'
    d.rectangle([CX - 45, CY + 12, CX - 25, CY + 88], fill=(255, 255, 255))
    d.rectangle([CX + 25, CY + 12, CX + 45, CY + 88], fill=(255, 255, 255))
    d.rectangle([CX - 25, CY + 40, CX + 25, CY + 60], fill=(255, 255, 255))

    # Runway edge lights (Green & Amber)
    for lx, ly in [(CX - 145, CY + 50), (CX + 145, CY + 50), (CX - 70, CY + 135), (CX + 70, CY + 135)]:
        d.ellipse([lx - 8, ly - 8, lx + 8, ly + 8], fill=(60, 255, 120), outline=(255, 255, 255), width=2)

    # Air Traffic Control (ATC) Tower in background
    d.rectangle([CX + 75, CY - 140, CX + 125, CY - 30], fill=(70, 80, 92), outline=(130, 145, 160), width=3)
    # Glass observation cab
    d.polygon([(CX + 65, CY - 140), (CX + 135, CY - 140), (CX + 125, CY - 165), (CX + 75, CY - 165)], fill=(100, 220, 255), outline=(40, 50, 60), width=3)
    # Dome radar
    d.ellipse([CX + 90, CY - 180, CX + 110, CY - 160], fill=(255, 255, 255), outline=(255, 50, 50), width=2)

def draw_air_factory_solid(d):
    # Air Factory / 飞机工厂 (High-tech hangar bay with delta-wing jet fighter inside)
    # Massive Hangar Arch
    d.arc([CX - 170, CY - 140, CX + 170, CY + 160], 180, 360, fill=(70, 85, 100), width=24)
    d.rectangle([CX - 170, CY + 10, CX - 146, CY + 135], fill=(70, 85, 100))
    d.rectangle([CX + 146, CY + 10, CX + 170, CY + 135], fill=(70, 85, 100))
    # Interior hangar deep space
    d.rectangle([CX - 146, CY + 10, CX + 146, CY + 135], fill=(25, 32, 40))
    d.arc([CX - 146, CY - 116, CX + 146, CY + 136], 180, 360, fill=(35, 45, 56))

    # Interior Hangar Floodlights
    d.ellipse([CX - 80, CY - 75, CX - 60, CY - 60], fill=(255, 240, 150))
    d.ellipse([CX + 60, CY - 75, CX + 80, CY - 60], fill=(255, 240, 150))

    # Modern Supersonic Jet Fighter ready on apron
    jet_pts = [
        (CX, CY - 110),          # Nose tip
        (CX + 20, CY - 30),      # Right forward fuselage
        (CX + 135, CY + 45),     # Right wingtip
        (CX + 135, CY + 75),     # Right trailing edge
        (CX + 35, CY + 60),      # Right engine intake root
        (CX + 30, CY + 115),     # Right exhaust
        (CX + 10, CY + 115),     # Tail center right
        (CX, CY + 95),           # Tail center
        (CX - 10, CY + 115),     # Tail center left
        (CX - 30, CY + 115),     # Left exhaust
        (CX - 35, CY + 60),      # Left engine intake root
        (CX - 135, CY + 75),     # Left trailing edge
        (CX - 135, CY + 45),     # Left wingtip
        (CX - 20, CY - 30),      # Left forward fuselage
    ]
    d.polygon(jet_pts, fill=(195, 215, 230), outline=(50, 70, 90), width=4)
    # Jet cockpit glass
    d.polygon([(CX, CY - 85), (CX + 12, CY - 40), (CX, CY - 20), (CX - 12, CY - 40)], fill=(40, 170, 240), outline=(20, 30, 40), width=2)
    # Wing missile rails
    d.rectangle([CX - 140, CY + 40, CX - 130, CY + 80], fill=(255, 60, 40))
    d.rectangle([CX + 130, CY + 40, CX + 140, CY + 80], fill=(255, 60, 40))

def draw_naval_yard_solid(d):
    # Naval Yard / 船坞 (Drydock basin with warship prow, twin gantry cranes, ocean waves)
    # Deep blue ocean basin
    d.rectangle([CX - 160, CY + 40, CX + 160, CY + 155], fill=(20, 65, 110), outline=(50, 120, 180), width=4)
    # Ocean wave crests
    for wx in range(CX - 150, CX + 150, 40):
        d.arc([wx, CY + 120, wx + 40, CY + 150], 180, 360, fill=(120, 210, 255), width=3)

    # Drydock Concrete Piers
    d.rectangle([CX - 165, CY - 30, CX - 110, CY + 155], fill=(75, 85, 95), outline=(120, 135, 150), width=4)
    d.rectangle([CX + 110, CY - 30, CX + 165, CY + 155], fill=(75, 85, 95), outline=(120, 135, 150), width=4)

    # Massive Overhead Gantry Crane
    d.rectangle([CX - 160, CY - 150, CX + 160, CY - 115], fill=(245, 140, 20), outline=(40, 40, 40), width=4)
    for cx in range(CX - 130, CX + 140, 35):
        d.line([(cx, CY - 150), (cx + 20, CY - 115)], fill=(160, 90, 10), width=3)
    # Crane legs
    d.line([(CX - 135, CY - 115), (CX - 135, CY - 30)], fill=(245, 140, 20), width=8)
    d.line([(CX + 135, CY - 115), (CX + 135, CY - 30)], fill=(245, 140, 20), width=8)

    # Warship Bow cutting through water
    bow_pts = [
        (CX, CY - 60),
        (CX + 60, CY + 90),
        (CX, CY + 125),
        (CX - 60, CY + 90)
    ]
    d.polygon(bow_pts, fill=(145, 160, 175), outline=(40, 50, 60), width=4)
    d.line([(CX, CY - 60), (CX, CY + 125)], fill=(220, 235, 245), width=4)
    
    # Heavy Gold Admiralty Anchor emblem on bow
    d.line([(CX, CY + 15), (CX, CY + 85)], fill=(255, 210, 50), width=8)
    d.line([(CX - 25, CY + 35), (CX + 25, CY + 35)], fill=(255, 210, 50), width=6)
    d.arc([CX - 38, CY + 35, CX + 38, CY + 95], 0, 180, fill=(255, 210, 50), width=8)

def draw_turret_solid(d):
    # Defense Turret / 防御炮台 (Reinforced concrete barbette, armored dome turret, twin autocannons)
    # Concrete barbette pedestal
    d.polygon([(CX - 140, CY + 145), (CX + 140, CY + 145), (CX + 115, CY + 75), (CX - 115, CY + 75)], fill=(75, 82, 88), outline=(130, 140, 150), width=5)
    # Foundation hazard stripes
    for hx in range(CX - 110, CX + 100, 30):
        d.polygon([(hx, CY + 85), (hx + 12, CY + 85), (hx + 4, CY + 135), (hx - 8, CY + 135)], fill=(240, 180, 30))

    # Rotating Armored Turret Dome
    d.ellipse([CX - 110, CY - 20, CX + 110, CY + 80], fill=(95, 108, 120), outline=(160, 180, 200), width=5)
    d.ellipse([CX - 85, CY - 10, CX + 85, CY + 50], fill=(70, 80, 90)) # Inner ring

    # Sensor optics pod in center
    d.ellipse([CX - 22, CY + 5, CX + 22, CY + 35], fill=(255, 60, 50), outline=(255, 255, 255), width=3)

    # Twin Heavy Autocannon Barrels pointing up
    for bx in [CX - 45, CX + 20]:
        # Gun barrel
        d.rectangle([bx, CY - 150, bx + 25, CY], fill=(50, 58, 65), outline=(170, 185, 200), width=4)
        # Recoil jacket sleeve
        d.rectangle([bx - 4, CY - 75, bx + 29, CY - 20], fill=(75, 88, 98), outline=(20, 20, 20), width=2)
        # Slotted Muzzle Brake
        d.rectangle([bx - 6, CY - 170, bx + 31, CY - 145], fill=(30, 35, 40), outline=(255, 200, 50), width=3)

def draw_gold_mine_solid(d):
    # Gold Mine / 金矿采掘场 (Heavy A-frame headframe, hoisting sheave wheel, minecart overflowing with gold)
    # Mine entrance cave opening
    d.arc([CX - 130, CY + 10, CX + 130, CY + 155], 180, 360, fill=(20, 22, 25))
    d.rectangle([CX - 130, CY + 80, CX + 130, CY + 150], fill=(20, 22, 25))

    # Heavy Steel A-Frame Derrick Hoist
    d.line([(CX - 110, CY + 140), (CX - 30, CY - 120)], fill=(185, 90, 40), width=10)
    d.line([(CX + 110, CY + 140), (CX + 30, CY - 120)], fill=(185, 90, 40), width=10)
    d.line([(CX - 75, CY + 20), (CX + 75, CY + 20)], fill=(185, 90, 40), width=7)
    d.line([(CX - 50, CY - 50), (CX + 50, CY - 50)], fill=(185, 90, 40), width=7)

    # Top Sheave Hoist Wheel
    d.ellipse([CX - 45, CY - 165, CX + 45, CY - 75], fill=(80, 90, 100), outline=(230, 240, 250), width=7)
    d.ellipse([CX - 15, CY - 135, CX + 15, CY - 105], fill=(40, 45, 50))

    # Minecart with Gold Ore Nuggets
    cart_pts = [
        (CX - 85, CY + 75), (CX + 85, CY + 75),
        (CX + 65, CY + 135), (CX - 65, CY + 135)
    ]
    d.polygon(cart_pts, fill=(65, 72, 78), outline=(130, 140, 150), width=4)
    # Cart wheels
    d.ellipse([CX - 60, CY + 125, CX - 30, CY + 155], fill=(30, 30, 30), outline=(200, 200, 200), width=3)
    d.ellipse([CX + 30, CY + 125, CX + 60, CY + 155], fill=(30, 30, 30), outline=(200, 200, 200), width=3)

    # Sparkling Gold Nuggets & Ingots Heap
    for gx, gy in [(CX - 50, CY + 60), (CX - 20, CY + 50), (CX + 15, CY + 52), (CX + 45, CY + 62), (CX, CY + 35)]:
        d.ellipse([gx - 20, gy - 16, gx + 20, gy + 16], fill=(255, 215, 0), outline=(180, 130, 0), width=3)
        d.ellipse([gx - 10, gy - 8, gx + 2, gy + 2], fill=(255, 255, 220)) # Specular gleam

def draw_power_plant_solid(d):
    # Power Plant / 发电厂 (Nuclear hyperbolic cooling tower, fusion core, electric lightning bolts)
    # Hyperbolic Cooling Tower Silhouette
    tower_pts = [
        (CX - 125, CY + 145), (CX + 125, CY + 145),
        (CX + 75, CY - 20), (CX + 95, CY - 135),
        (CX - 95, CY - 135), (CX - 75, CY - 20)
    ]
    d.polygon(tower_pts, fill=(85, 95, 108), outline=(150, 168, 185), width=5)
    # Cooling tower rim
    d.ellipse([CX - 95, CY - 150, CX + 95, CY - 120], fill=(50, 60, 70), outline=(200, 215, 230), width=4)

    # Radiation Warning Hazard Bands on tower
    d.polygon([(CX - 85, CY - 40), (CX + 85, CY - 40), (CX + 80, CY - 15), (CX - 80, CY - 15)], fill=(245, 185, 25))
    d.polygon([(CX - 105, CY + 40), (CX + 105, CY + 40), (CX + 100, CY + 65), (CX - 100, CY + 65)], fill=(245, 185, 25))

    # Glowing Fusion Core Reactor Window in center
    d.ellipse([CX - 45, CY - 5, CX + 45, CY + 85], fill=(20, 30, 45), outline=(180, 120, 255), width=4)
    # Electric Lightning Bolt Energy Arc
    bolt_pts = [
        (CX + 18, CY - 85),
        (CX - 35, CY + 5),
        (CX + 10, CY + 5),
        (CX - 18, CY + 115),
        (CX + 45, CY + 15),
        (CX, CY + 15)
    ]
    d.polygon(bolt_pts, fill=(255, 240, 80), outline=(255, 255, 255), width=3)

# -------------------------------------------------------------------------
# TECH ICONS (128x128 for cards & HUD)
# -------------------------------------------------------------------------
def generate_tech_icon(path, title, draw_fn, primary_col=(20, 30, 42), accent_col=(70, 210, 255)):
    img = create_canvas()
    d = ImageDraw.Draw(img)
    # Solid tech badge
    draw_badge_plate(d, primary_col=primary_col, accent_col=accent_col, r=236, chamfer=60)
    # Custom solid artwork
    draw_fn(d)
    out = downsample(img, 128)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    out.save(path)
    print(f"Generated solid tech icon: {title} -> {path}")

def draw_tech_speed_solid(d):
    # Speed Boost / 机动强化 (Supersonic swept wings, rocket thrust flames, forward chevrons)
    # Rocket exhaust plume glow
    for r in range(70, 10, -8):
        alpha = int(220 * (1.0 - r / 70.0))
        d.ellipse([CX - r, CY + 50 - r, CX + r, CY + 50 + r], fill=(40, 180, 255, alpha))
    # Fiery exhaust flame
    d.polygon([(CX - 25, CY + 50), (CX, CY + 155), (CX + 25, CY + 50)], fill=(255, 240, 120), outline=(255, 140, 20), width=3)

    # Supersonic Swept Fighter Wings
    body_pts = [
        (CX, CY - 140),          # Nose cone
        (CX + 35, CY - 20),
        (CX + 155, CY + 45),     # Wingtip right
        (CX + 140, CY + 80),
        (CX + 40, CY + 60),
        (CX + 25, CY + 90),
        (CX - 25, CY + 90),
        (CX - 40, CY + 60),
        (CX - 140, CY + 80),
        (CX - 155, CY + 45),     # Wingtip left
        (CX - 35, CY - 20),
    ]
    d.polygon(body_pts, fill=(225, 240, 255), outline=(30, 140, 220), width=5)
    # Wing highlights
    d.polygon([(CX, CY - 125), (CX + 25, CY - 20), (CX, CY + 45), (CX - 25, CY - 20)], fill=(60, 190, 255))
    
    # Forward Speed Chevrons above
    for cy_off in [-15, -65]:
        d.polygon([(CX, CY + cy_off - 35), (CX + 75, CY + cy_off + 15), (CX + 50, CY + cy_off + 25), (CX, CY + cy_off - 10), (CX - 50, cy_off + CY + 25), (CX - 75, CY + cy_off + 15)], fill=(255, 255, 255), outline=(40, 210, 255), width=2)

def draw_tech_armor_solid(d):
    # Armor Boost / 装甲强化 (Heavy composite ballistic shield with layered steel armor plates and rivets)
    # Master tactical shield
    shield_pts = [
        (CX, CY - 155),
        (CX + 145, CY - 95),
        (CX + 120, CY + 45),
        (CX, CY + 165),
        (CX - 120, CY + 45),
        (CX - 145, CY - 95)
    ]
    d.polygon(shield_pts, fill=(65, 85, 110), outline=(180, 215, 255), width=7)

    # Inner armor plate layer
    inner_pts = [
        (CX, CY - 120),
        (CX + 115, CY - 75),
        (CX + 95, CY + 30),
        (CX, CY + 125),
        (CX - 95, CY + 30),
        (CX - 115, CY - 75)
    ]
    d.polygon(inner_pts, fill=(95, 120, 150), outline=(40, 55, 75), width=4)

    # Core high-tensile titanium cross plate
    d.polygon([(CX, CY - 100), (CX + 75, CY - 60), (CX + 55, CY + 20), (CX, CY + 95), (CX - 55, CY + 20), (CX - 75, CY - 60)], fill=(195, 225, 255), outline=(40, 140, 220), width=3)

    # Heavy steel perimeter rivets
    for pt in inner_pts:
        d.ellipse([pt[0] - 6, pt[1] - 6, pt[0] + 6, pt[1] + 6], fill=(255, 255, 255), outline=(30, 40, 50), width=2)

def draw_tech_firepower_solid(d):
    # Firepower Boost / 火力强化 (Explosive shell detonation, high-energy starburst, crosshair reticle)
    # Fiery explosive starburst background
    star_pts = []
    for i in range(16):
        r = 165 if i % 2 == 0 else 65
        ang = math.radians(22.5 * i)
        star_pts.append((CX + r * math.cos(ang), CY + r * math.sin(ang)))
    d.polygon(star_pts, fill=(255, 85, 25), outline=(255, 220, 50), width=5)

    # Inner core blast
    inner_star = []
    for i in range(16):
        r = 105 if i % 2 == 0 else 40
        ang = math.radians(22.5 * i + 11.25)
        inner_star.append((CX + r * math.cos(ang), CY + r * math.sin(ang)))
    d.polygon(inner_star, fill=(255, 235, 90), outline=(255, 255, 255), width=3)

    # Precision Targeting Crosshairs
    d.ellipse([CX - 85, CY - 85, CX + 85, CY + 85], outline=(255, 255, 255), width=6)
    d.line([(CX - 125, CY), (CX - 35, CY)], fill=(255, 255, 255), width=6)
    d.line([(CX + 35, CY), (CX + 125, CY)], fill=(255, 255, 255), width=6)
    d.line([(CX, CY - 125), (CX, CY - 35)], fill=(255, 255, 255), width=6)
    d.line([(CX, CY + 35), (CX, CY + 125)], fill=(255, 255, 255), width=6)

    # Center piercing core bullet
    d.ellipse([CX - 18, CY - 18, CX + 18, CY + 18], fill=(255, 255, 255), outline=(255, 60, 20), width=3)

def draw_tech_repair_solid(d):
    # Field Repair / 战地维修 (Heavy monkey wrench & blowtorch crossed behind green medical cross crest)
    # Crossed Monkey Wrench (Left-top to Right-bottom)
    w_pts = [
        (CX - 125, CY - 145), (CX - 75, CY - 145), (CX - 60, CY - 110),
        (CX + 115, CY + 95), (CX + 85, CY + 125), (CX - 80, CY - 75),
    ]
    d.polygon(w_pts, fill=(180, 195, 210), outline=(40, 50, 60), width=4)
    # Wrench open jaw
    d.rectangle([CX - 115, CY - 150, CX - 85, CY - 120], fill=(25, 35, 45))

    # Crossed Blowtorch (Right-top to Left-bottom)
    t_pts = [
        (CX + 120, CY - 135), (CX + 85, CY - 145),
        (CX - 105, CY + 105), (CX - 80, CY + 130)
    ]
    d.polygon(t_pts, fill=(160, 140, 80), outline=(40, 30, 20), width=4)
    # Welding blue flame tip
    d.polygon([(CX + 120, CY - 135), (CX + 155, CY - 165), (CX + 105, CY - 145)], fill=(40, 215, 255), outline=(255, 255, 255), width=2)

    # Green Medical Cross Shield in foreground
    s_pts = [
        (CX, CY - 85), (CX + 85, CY - 50), (CX + 70, CY + 45),
        (CX, CY + 115), (CX - 70, CY + 45), (CX - 85, CY - 50)
    ]
    d.polygon(s_pts, fill=(30, 135, 65), outline=(140, 255, 175), width=6)
    # Bold White Medical Cross
    d.rectangle([CX - 18, CY - 55, CX + 18, CY + 75], fill=(255, 255, 255))
    d.rectangle([CX - 55, CY - 10, CX + 55, CY + 25], fill=(255, 255, 255))

def draw_tech_radar_solid(d):
    # Radar Recon / 雷达扫描 (Parabolic dish antenna, rotary scan sweep, circular grids, target blips)
    # Circular radar scope display
    d.ellipse([CX - 155, CY - 155, CX + 155, CY + 155], fill=(15, 35, 25), outline=(60, 225, 120), width=6)
    # Range circles
    d.ellipse([CX - 110, CY - 110, CX + 110, CY + 110], outline=(40, 160, 80), width=3)
    d.ellipse([CX - 65, CY - 65, CX + 65, CY + 65], outline=(40, 160, 80), width=3)
    d.line([(CX - 155, CY), (CX + 155, CY)], fill=(40, 160, 80), width=2)
    d.line([(CX, CY - 155), (CX, CY + 155)], fill=(40, 160, 80), width=2)

    # Rotating Gold/Amber Radar Sweep Pie
    sweep_pts = [(CX, CY)]
    for a in range(20, 80, 5):
        rad = math.radians(a)
        sweep_pts.append((CX + 150 * math.cos(rad), CY - 150 * math.sin(rad)))
    d.polygon(sweep_pts, fill=(255, 215, 60, 140))
    d.line([(CX, CY), (CX + 150 * math.cos(math.radians(80)), CY - 150 * math.sin(math.radians(80)))], fill=(255, 255, 200), width=4)

    # Identified Target Blips (Red & Yellow)
    for bx, by in [(CX + 60, CY - 75), (CX - 75, CY - 40), (CX + 85, CY + 55)]:
        d.ellipse([bx - 9, by - 9, bx + 9, by + 9], fill=(255, 50, 40), outline=(255, 255, 255), width=2)
        d.ellipse([bx - 16, by - 16, bx + 16, by + 16], outline=(255, 120, 50), width=2)

    # Radar Emitter Mast at Center
    d.ellipse([CX - 20, CY - 20, CX + 20, CY + 20], fill=(255, 255, 255), outline=(20, 60, 30), width=3)

def draw_tech_rapid_solid(d):
    # Rapid Fire / 急促射击 (Triple heavy high-velocity armor-piercing cartridges with speed streaks)
    for off_x, off_y, scale in [(-65, 25, 0.88), (0, -25, 1.05), (65, 25, 0.88)]:
        bx, by = CX + off_x, CY + off_y
        w = int(22 * scale)
        h = int(95 * scale)
        # Bullet Brass Casing
        d.rectangle([bx - w, by - int(20 * scale), bx + w, by + h], fill=(215, 165, 45), outline=(90, 65, 15), width=3)
        # Rim extractor groove
        d.rectangle([bx - w - 3, by + h - int(12 * scale), bx + w + 3, by + h], fill=(170, 125, 25))
        # Copper/Steel Armor-Piercing Bullet Tip
        tip_pts = [
            (bx - w, by - int(20 * scale)),
            (bx, by - int(85 * scale)),
            (bx + w, by - int(20 * scale))
        ]
        d.polygon(tip_pts, fill=(235, 95, 60), outline=(255, 220, 180), width=3)

    # High-speed tracer velocity streaks
    for sx in [-80, -30, 20, 75]:
        d.line([(CX + sx, CY - 130), (CX + sx, CY - 60)], fill=(255, 240, 150), width=4)

def draw_tech_hold_solid(d):
    # Fortify / 阵地坚守 (Heavy reinforced pillbox bunker with gun slits and anti-tank steel hedgehogs)
    # Heavy Bunker Hexagonal Concrete Shell
    bunker_pts = [
        (CX - 150, CY + 110), (CX + 150, CY + 110),
        (CX + 110, CY - 65), (CX, CY - 120), (CX - 110, CY - 65)
    ]
    d.polygon(bunker_pts, fill=(90, 102, 114), outline=(170, 185, 200), width=6)
    
    # Sloped roof armor plates
    d.polygon([(CX - 110, CY - 65), (CX, CY - 120), (CX, CY - 10), (CX - 90, CY - 10)], fill=(65, 75, 85))
    d.polygon([(CX + 110, CY - 65), (CX, CY - 120), (CX, CY - 10), (CX + 90, CY - 10)], fill=(115, 128, 140))

    # Armored Firing Embrasure Slit
    d.rectangle([CX - 95, CY - 5, CX + 95, CY + 35], fill=(20, 24, 28), outline=(255, 195, 45), width=4)
    # Heavy machinegun barrel protruding
    d.rectangle([CX - 12, CY + 5, CX + 12, CY + 45], fill=(190, 205, 220), outline=(20, 20, 20), width=2)

    # Anti-Tank Steel Hedgehog Obstacles in front
    for hx in [CX - 85, CX + 85]:
        d.line([(hx - 30, CY + 135), (hx + 30, CY + 75)], fill=(60, 68, 75), width=8)
        d.line([(hx + 30, CY + 135), (hx - 30, CY + 75)], fill=(60, 68, 75), width=8)
        d.line([(hx, CY + 140), (hx, CY + 70)], fill=(90, 100, 110), width=8)

def draw_tech_assault_solid(d):
    # Assault Charge / 突击冲锋 (Triple heavy tactical vanguard attack chevrons piercing defense lines)
    # Broken defense barrier line
    d.line([(CX - 165, CY + 80), (CX - 65, CY + 80)], fill=(120, 130, 140), width=6)
    d.line([(CX + 65, CY + 80), (CX + 165, CY + 80)], fill=(120, 130, 140), width=6)

    # Triple Heavy Attack Chevrons (Vibrant Crimson & Gold)
    for cy_off, col, rim in [
        (55, (175, 25, 35), (255, 120, 100)),
        (-5, (225, 40, 50), (255, 190, 120)),
        (-65, (255, 70, 70), (255, 255, 200))
    ]:
        pts = [
            (CX, CY + cy_off - 55),
            (CX + 125, CY + cy_off + 25),
            (CX + 85, CY + cy_off + 50),
            (CX, CY + cy_off),
            (CX - 85, CY + cy_off + 50),
            (CX - 125, CY + cy_off + 25)
        ]
        d.polygon(pts, fill=col, outline=rim, width=4)

def draw_tech_air_refit_solid(d):
    # Aerial Refit / 航空改装 (Delta-wing stealth fighter, refueling boom & wingtip pods)
    # Delta-wing fighter
    jet_pts = [
        (CX, CY - 130),
        (CX + 140, CY + 65),
        (CX + 60, CY + 55),
        (CX, CY + 115),
        (CX - 60, CY + 55),
        (CX - 140, CY + 65),
    ]
    d.polygon(jet_pts, fill=(185, 220, 235), outline=(30, 120, 160), width=5)
    # Fuselage center line & cockpit
    d.polygon([(CX, CY - 110), (CX + 20, CY - 20), (CX, CY + 30), (CX - 20, CY - 20)], fill=(30, 160, 220))

    # Aerial Refueling Boom Reticle
    d.ellipse([CX - 40, CY - 165, CX + 40, CY - 85], outline=(255, 210, 45), width=5)
    d.line([(CX, CY - 165), (CX, CY - 85)], fill=(255, 210, 45), width=4)
    d.line([(CX - 40, CY - 125), (CX + 40, CY - 125)], fill=(255, 210, 45), width=4)

    # Advanced Wingtip Pods
    d.rectangle([CX - 150, CY + 45, CX - 130, CY + 85], fill=(255, 80, 50), outline=(255, 255, 255), width=2)
    d.rectangle([CX + 130, CY + 45, CX + 150, CY + 85], fill=(255, 80, 50), outline=(255, 255, 255), width=2)

def draw_tech_overdrive_solid(d):
    # Overdrive / 过载运转 (Plasma singularity core with explosive arcing lightning bursts)
    # Overload energy bursts
    for a in range(0, 360, 45):
        rad = math.radians(a)
        x1 = CX + 50 * math.cos(rad)
        y1 = CY + 50 * math.sin(rad)
        x2 = CX + 160 * math.cos(rad + 0.12)
        y2 = CY + 160 * math.sin(rad + 0.12)
        d.line([(x1, y1), (x2, y2)], fill=(255, 80, 230), width=8)
        d.line([(x1, y1), (x2, y2)], fill=(255, 255, 255), width=3)

    # Core Plasma Sphere with radiant purple corona
    for r in range(95, 30, -5):
        alpha = int(220 * (1.0 - (r - 30) / 65.0))
        d.ellipse([CX - r, CY - r, CX + r, CY + r], fill=(220, 40, 255, alpha))
    d.ellipse([CX - 50, CY - 50, CX + 50, CY + 50], fill=(255, 120, 255), outline=(255, 255, 255), width=5)
    d.ellipse([CX - 25, CY - 25, CX + 5, CY + 5], fill=(255, 255, 255))

# -------------------------------------------------------------------------
# MASTER GENERATOR
# -------------------------------------------------------------------------
def generate_all():
    print("=== START GENERATING SOLID RTS ICONS ===")
    
    # 1. Action Icons on HUD
    generate_hud_build_icon("assets/unity_migrated/Assets/Resources/icons_final/icon_hud_build.png")
    generate_hud_tech_icon("assets/unity_migrated/Assets/Resources/icons_final/icon_hud_tech.png")

    # 2. Building Icons (96x96)
    bld_root = "assets/unity_migrated/Assets/Resources/UI/ButtonBackdrops/"
    buildings = [
        ("build_barracks_icon.png", "兵营", draw_barracks_solid, (30, 42, 32), (100, 225, 130)),
        ("build_tank_factory_icon.png", "坦克工厂", draw_tank_factory_solid, (38, 42, 34), (225, 205, 70)),
        ("build_special_factory_icon.png", "特勤工厂", draw_special_factory_solid, (42, 34, 28), (245, 150, 60)),
        ("build_airfield_icon.png", "停机坪", draw_airfield_solid, (28, 38, 46), (110, 195, 255)),
        ("build_air_factory_icon.png", "飞机工厂", draw_air_factory_solid, (26, 36, 48), (80, 210, 255)),
        ("build_naval_yard_icon.png", "船坞", draw_naval_yard_solid, (20, 36, 48), (60, 220, 240)),
        ("build_turret_icon.png", "防御炮台", draw_turret_solid, (45, 28, 28), (255, 95, 80)),
        ("build_gold_mine_icon.png", "金矿采掘场", draw_gold_mine_solid, (44, 38, 22), (255, 215, 60)),
        ("build_power_plant_icon.png", "发电厂", draw_power_plant_solid, (38, 28, 48), (200, 130, 255)),
    ]
    for filename, title, fn, primary_c, accent_c in buildings:
        generate_building_icon(os.path.join(bld_root, filename), title, fn, primary_c, accent_c)

    # 3. Tech Icons (128x128)
    tech_root = "assets/unity_migrated/Assets/Resources/BattleHud/TechIcons/"
    techs = [
        ("tech_icon_speed.png", "机动强化", draw_tech_speed_solid, (18, 36, 48), (60, 225, 255)),
        ("tech_icon_armor.png", "装甲强化", draw_tech_armor_solid, (22, 34, 52), (110, 190, 255)),
        ("tech_icon_firepower.png", "火力强化", draw_tech_firepower_solid, (48, 24, 18), (255, 130, 50)),
        ("tech_icon_repair.png", "战地维修", draw_tech_repair_solid, (20, 44, 28), (80, 240, 140)),
        ("tech_icon_radar.png", "雷达扫描", draw_tech_radar_solid, (44, 38, 18), (255, 220, 65)),
        ("tech_icon_rapid.png", "急促射击", draw_tech_rapid_solid, (48, 32, 18), (255, 175, 55)),
        ("tech_icon_hold.png", "阵地坚守", draw_tech_hold_solid, (26, 42, 34), (130, 235, 170)),
        ("tech_icon_assault.png", "突击冲锋", draw_tech_assault_solid, (48, 20, 24), (255, 80, 95)),
        ("tech_icon_air_refit.png", "航空改装", draw_tech_air_refit_solid, (18, 42, 44), (70, 235, 215)),
        ("tech_icon_overdrive.png", "过载运转", draw_tech_overdrive_solid, (42, 18, 44), (235, 80, 220)),
    ]
    for filename, title, fn, primary_c, accent_c in techs:
        generate_tech_icon(os.path.join(tech_root, filename), title, fn, primary_c, accent_c)

    print("=== ALL SOLID RTS ICONS GENERATED SUCCESSFULLY ===")

if __name__ == "__main__":
    generate_all()

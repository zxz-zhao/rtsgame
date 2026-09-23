"""
Procedural Modern Kitchen Generator for Blender
------------------------------------------------
Generates a complete, high-fidelity modern kitchen with:
- L-shaped walls and porcelain tile floor
- Custom modern base cabinets (sink cabinet, drawer units, cooktop cabinet, spice pullout)
- Seamless quartz/marble countertop with undermount double sink & modern gooseneck faucet
- Built-in black crystal gas cooktop & matching range hood
- Upper wall cabinets with hidden recessed handles & under-cabinet LED warm light strip
- Tall cabinet tower with built-in oven and refrigerator
- Full PBR materials, soft interior lighting, sun lighting, and framed camera
"""

import bpy
import bmesh
import math
import os

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        bpy.data.materials.remove(block)

def create_mat(name, color, roughness=0.3, metallic=0.0, emission_color=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = color
        bsdf.inputs['Roughness'].default_value = roughness
        bsdf.inputs['Metallic'].default_value = metallic
        if emission_color and emission_strength > 0:
            if 'Emission Color' in bsdf.inputs:
                bsdf.inputs['Emission Color'].default_value = emission_color
                bsdf.inputs['Emission Strength'].default_value = emission_strength
            elif 'Emission' in bsdf.inputs:
                bsdf.inputs['Emission'].default_value = emission_color
    return mat

def create_box(name, size, pos, mat=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
    
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    
    obj = bpy.data.objects.new(name, mesh)
    obj.location = pos
    if mat:
        obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj

def create_cylinder(name, radius, height, pos, rot=(0,0,0), segments=32, mat=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius, radius2=radius, depth=height)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    
    obj = bpy.data.objects.new(name, mesh)
    obj.location = pos
    obj.rotation_euler = rot
    if mat:
        obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj

def build_modern_kitchen():
    clear_scene()
    
    # ─── 1. Materials ──────────────────────────────────────────────
    mat_wall = create_mat("Mat_Wall", (0.92, 0.91, 0.89, 1.0), roughness=0.75)
    mat_floor = create_mat("Mat_Floor", (0.82, 0.82, 0.84, 1.0), roughness=0.18, metallic=0.05)
    mat_backsplash = create_mat("Mat_Backsplash", (0.94, 0.94, 0.95, 1.0), roughness=0.12)
    mat_countertop = create_mat("Mat_Countertop", (0.96, 0.96, 0.97, 1.0), roughness=0.10, metallic=0.02)
    mat_cabinet_base = create_mat("Mat_CabinetBase", (0.22, 0.23, 0.25, 1.0), roughness=0.45) # 高级哑光黑灰地柜
    mat_cabinet_upper = create_mat("Mat_CabinetUpper", (0.93, 0.90, 0.86, 1.0), roughness=0.40) # 奶油白吊柜
    mat_wood_accent = create_mat("Mat_WoodAccent", (0.68, 0.48, 0.32, 1.0), roughness=0.55) # 暖木纹开放格
    mat_metal_black = create_mat("Mat_MetalBlack", (0.10, 0.10, 0.11, 1.0), roughness=0.25, metallic=0.90)
    mat_stainless = create_mat("Mat_StainlessSteel", (0.85, 0.86, 0.88, 1.0), roughness=0.20, metallic=0.95)
    mat_glass = create_mat("Mat_BlackGlass", (0.05, 0.05, 0.06, 1.0), roughness=0.05, metallic=0.1)
    mat_led = create_mat("Mat_LedWarm", (1.0, 0.88, 0.65, 1.0), roughness=1.0, emission_color=(1.0, 0.88, 0.65, 1.0), emission_strength=8.0)
    
    # ─── 2. Walls & Floor ──────────────────────────────────────────
    # Floor (3.8m x 3.6m)
    create_box("Kitchen_Floor", (4.0, 3.8, 0.02), (0.0, 0.0, -0.01), mat_floor)
    
    # Back Wall (Main cooking & sink wall along X: 3.8m wide, 2.7m high, 0.15m thick)
    create_box("Wall_Back", (3.8, 0.15, 2.7), (0.0, 1.8, 1.35), mat_wall)
    
    # Left Wall (Side wall along Y: 3.6m deep, 2.7m high)
    create_box("Wall_Left", (0.15, 3.6, 2.7), (-1.9, 0.0, 1.35), mat_wall)
    
    # Backsplash (Kitchen wall tile between countertop and upper cabinets)
    create_box("Backsplash_Back", (3.6, 0.02, 0.65), (0.0, 1.72, 1.15), mat_backsplash)
    create_box("Backsplash_Left", (0.02, 2.8, 0.65), (-1.82, 0.35, 1.15), mat_backsplash)
    
    # ─── 3. Base Cabinets (L-Shape) ────────────────────────────────
    # Main Back Run: Depth = 0.60m, Height = 0.82m (Toe kick 0.08m + cabinet 0.74m)
    # Toe Kick Back
    create_box("ToeKick_Back", (3.4, 0.52, 0.08), (0.1, 1.48, 0.04), mat_metal_black)
    create_box("ToeKick_Left", (0.52, 2.2, 0.08), (-1.58, 0.4, 0.04), mat_metal_black)
    
    # Base Cabinets (Main Back Wall from X = -1.2 to X = 1.8)
    base_sections = [
        ("Base_SinkCabinet", 0.90, -0.75, "double_door"),
        ("Base_DrawerUnit_1", 0.60, 0.00, "3_drawers"),
        ("Base_CooktopUnit", 0.80, 0.70, "cooktop_doors"),
        ("Base_SpicePullout", 0.30, 1.25, "pullout"),
        ("Base_DrawerUnit_2", 0.40, 1.60, "2_drawers")
    ]
    
    for name, width, posX, style in base_sections:
        # Cabinet Carcass
        create_box(name + "_Body", (width - 0.004, 0.58, 0.72), (posX, 1.42, 0.44), mat_cabinet_base)
        
        # Front Doors / Drawers
        if style == "3_drawers":
            dh = 0.72 / 3.0
            for d in range(3):
                create_box(f"{name}_Drawer_{d}", (width - 0.008, 0.02, dh - 0.006), (posX, 1.12, 0.08 + dh * d + dh * 0.5), mat_cabinet_base)
                # Recessed Slim Black Handle
                create_box(f"{name}_Handle_{d}", (width * 0.55, 0.015, 0.012), (posX, 1.105, 0.08 + dh * (d + 1) - 0.03), mat_metal_black)
        elif style == "2_drawers":
            dh = 0.72 / 2.0
            for d in range(2):
                create_box(f"{name}_Drawer_{d}", (width - 0.008, 0.02, dh - 0.006), (posX, 1.12, 0.08 + dh * d + dh * 0.5), mat_cabinet_base)
                create_box(f"{name}_Handle_{d}", (width * 0.6, 0.015, 0.012), (posX, 1.105, 0.08 + dh * (d + 1) - 0.03), mat_metal_black)
        elif style in ("double_door", "cooktop_doors"):
            hw = width * 0.5 - 0.004
            create_box(f"{name}_Door_L", (hw - 0.004, 0.02, 0.71), (posX - hw * 0.5 - 0.002, 1.12, 0.44), mat_cabinet_base)
            create_box(f"{name}_Door_R", (hw - 0.004, 0.02, 0.71), (posX + hw * 0.5 + 0.002, 1.12, 0.44), mat_cabinet_base)
            create_box(f"{name}_Handle_L", (0.015, 0.015, 0.16), (posX - 0.03, 1.105, 0.65), mat_metal_black)
            create_box(f"{name}_Handle_R", (0.015, 0.015, 0.16), (posX + 0.03, 1.105, 0.65), mat_metal_black)
        elif style == "pullout":
            create_box(f"{name}_Front", (width - 0.008, 0.02, 0.71), (posX, 1.12, 0.44), mat_cabinet_base)
            create_box(f"{name}_Handle", (0.015, 0.015, 0.22), (posX, 1.105, 0.62), mat_metal_black)

    # Left Corner Base Cabinet Run (from Y = 0.0 to Y = 1.1)
    create_box("Base_Left_Corner", (0.58, 0.90, 0.72), (-1.52, 0.65, 0.44), mat_cabinet_base)
    create_box("Base_Left_Door", (0.02, 0.88, 0.71), (-1.22, 0.65, 0.44), mat_cabinet_base)
    create_box("Base_Left_Handle", (0.015, 0.16, 0.015), (-1.205, 0.65, 0.65), mat_metal_black)

    # ─── 4. Countertop (Seamless Quartz / Marble) ──────────────────
    # Main Back Countertop: 3.45m x 0.62m, thickness = 0.04m at Z = 0.82
    create_box("Countertop_Main", (3.50, 0.62, 0.04), (0.05, 1.42, 0.82), mat_countertop)
    create_box("Countertop_Left", (0.62, 1.10, 0.04), (-1.52, 0.55, 0.82), mat_countertop)

    # ─── 5. Appliances: Undermount Sink, Faucet & Cooktop ──────────
    # Sink Bowl (Undermount Double Basin at X = -0.75, Y = 1.42, Z = 0.78)
    create_box("Sink_Basin_L", (0.42, 0.38, 0.20), (-0.88, 1.42, 0.72), mat_stainless)
    create_box("Sink_Basin_R", (0.32, 0.38, 0.18), (-0.49, 1.42, 0.73), mat_stainless)
    
    # Modern Gooseneck Faucet
    create_cylinder("Faucet_Base", 0.022, 0.06, (-0.75, 1.63, 0.86), mat=mat_metal_black)
    create_cylinder("Faucet_Stem", 0.014, 0.30, (-0.75, 1.63, 1.02), mat=mat_metal_black)
    create_cylinder("Faucet_Neck", 0.014, 0.20, (-0.75, 1.55, 1.16), rot=(math.radians(65), 0, 0), mat=mat_metal_black)
    create_cylinder("Faucet_Spout", 0.016, 0.06, (-0.75, 1.48, 1.10), mat=mat_metal_black)
    create_cylinder("Faucet_Handle", 0.008, 0.08, (-0.70, 1.63, 0.90), rot=(0, math.radians(45), 0), mat=mat_metal_black)

    # Modern Gas / Induction Cooktop at X = 0.70, Y = 1.42, Z = 0.84
    create_box("Cooktop_Glass_Panel", (0.75, 0.50, 0.012), (0.70, 1.42, 0.842), mat_glass)
    # Burner Grates
    create_cylinder("Burner_L_Outer", 0.09, 0.02, (0.54, 1.42, 0.855), mat=mat_metal_black)
    create_cylinder("Burner_R_Outer", 0.09, 0.02, (0.86, 1.42, 0.855), mat=mat_metal_black)
    create_cylinder("Burner_L_Inner", 0.04, 0.025, (0.54, 1.42, 0.858), mat=mat_metal_black)
    create_cylinder("Burner_R_Inner", 0.04, 0.025, (0.86, 1.42, 0.858), mat=mat_metal_black)

    # ─── 6. Upper Wall Cabinets & LED Light Strip ──────────────────
    # Height Z = 1.50 to 2.25 (0.75m tall), Depth = 0.36m, Y = 1.55
    upper_sections = [
        ("Upper_Left_1", 0.80, -1.0),
        ("Upper_Left_2", 0.60, -0.30),
        ("Upper_Center_OpenShelf", 0.40, 0.20), # 开放式木质展示格
        ("Upper_RangeHood_Enclosure", 0.80, 0.80), # 油烟机柜
        ("Upper_Right_1", 0.50, 1.45)
    ]
    
    for name, width, posX in upper_sections:
        if "OpenShelf" in name:
            create_box(name + "_Box", (width - 0.004, 0.34, 0.74), (posX, 1.55, 1.88), mat_wood_accent)
            create_box(name + "_Shelf", (width - 0.04, 0.32, 0.02), (posX, 1.55, 1.88), mat_wood_accent)
        elif "RangeHood" in name:
            create_box(name + "_Cabinet", (width - 0.004, 0.35, 0.45), (posX, 1.55, 2.02), mat_cabinet_upper)
            # Modern Stainless Slim Range Hood Body
            create_box("Range_Hood_Body", (0.76, 0.46, 0.05), (posX, 1.48, 1.55), mat_metal_black)
            create_box("Range_Hood_Chimney", (0.30, 0.28, 0.40), (posX, 1.55, 1.76), mat_stainless)
            create_box("Range_Hood_Glass", (0.74, 0.44, 0.008), (posX, 1.48, 1.52), mat_glass)
        else:
            create_box(name + "_Body", (width - 0.004, 0.35, 0.74), (posX, 1.55, 1.88), mat_cabinet_upper)
            hw = width * 0.5 - 0.004
            create_box(f"{name}_Door_L", (hw - 0.003, 0.02, 0.73), (posX - hw * 0.5 - 0.002, 1.365, 1.88), mat_cabinet_upper)
            create_box(f"{name}_Door_R", (hw - 0.003, 0.02, 0.73), (posX + hw * 0.5 + 0.002, 1.365, 1.88), mat_cabinet_upper)

    # Continuous Under-Cabinet Warm LED Light Strip
    create_box("UnderCabinet_LED_Strip", (3.2, 0.02, 0.008), (0.2, 1.40, 1.498), mat_led)

    # ─── 7. Tall Refrigerator & Oven Tower (Right Side) ────────────
    # Tall Tower at X = 1.95, Y = 1.35, Z = 1.15 (0.65m wide, 0.60m deep, 2.30m tall)
    create_box("TallTower_Oven_Body", (0.65, 0.58, 2.30), (1.95, 1.42, 1.15), mat_cabinet_base)
    # Built-in Smart Black Glass Oven
    create_box("BuiltIn_Oven_Glass", (0.58, 0.03, 0.52), (1.95, 1.12, 1.10), mat_glass)
    create_box("BuiltIn_Oven_Handle", (0.48, 0.02, 0.015), (1.95, 1.09, 1.32), mat_stainless)
    # Top and Bottom Storage Doors on Tower
    create_box("TallTower_TopDoor", (0.63, 0.02, 0.65), (1.95, 1.12, 1.80), mat_cabinet_base)
    create_box("TallTower_BotDoor", (0.63, 0.02, 0.70), (1.95, 1.12, 0.40), mat_cabinet_base)

    # ─── 8. Lighting & Camera Setup ────────────────────────────────
    # Key Ceiling Recessed Downlights
    for lx in [-1.0, 0.0, 1.0]:
        for ly in [0.2, 1.2]:
            light_data = bpy.data.lights.new(name=f"SpotLight_{lx}_{ly}", type='SPOT')
            light_data.energy = 45.0
            light_data.spot_size = math.radians(65)
            light_data.spot_blend = 0.4
            light_data.color = (1.0, 0.96, 0.90)
            light_obj = bpy.data.objects.new(name=f"Light_{lx}_{ly}", object_data=light_data)
            light_obj.location = (lx, ly, 2.65)
            light_obj.rotation_euler = (0, 0, 0)
            bpy.context.scene.collection.objects.link(light_obj)

    # Warm LED Under-Cabinet Area Light
    strip_light_data = bpy.data.lights.new(name="UnderCabinetLight", type='AREA')
    strip_light_data.energy = 35.0
    strip_light_data.size = 2.8
    strip_light_data.size_y = 0.15
    strip_light_data.color = (1.0, 0.88, 0.68)
    strip_light_obj = bpy.data.objects.new("UnderCabinetAreaLight", strip_light_data)
    strip_light_obj.location = (0.2, 1.40, 1.48)
    strip_light_obj.rotation_euler = (math.radians(180), 0, 0)
    bpy.context.scene.collection.objects.link(strip_light_obj)

    # Sun / Ambient Window Light
    sun_data = bpy.data.lights.new(name="SunLight", type='SUN')
    sun_data.energy = 2.8
    sun_data.color = (0.95, 0.98, 1.0)
    sun_obj = bpy.data.objects.new("Sun", sun_data)
    sun_obj.rotation_euler = (math.radians(45), math.radians(25), math.radians(-50))
    bpy.context.scene.collection.objects.link(sun_obj)

    # Camera (Wide angle 24mm framing the kitchen L-shape)
    cam_data = bpy.data.cameras.new(name="Kitchen_Camera")
    cam_data.lens = 22.0
    cam_data.sensor_width = 36.0
    cam_obj = bpy.data.objects.new("Camera", cam_data)
    cam_obj.location = (0.25, -1.85, 1.45)
    cam_obj.rotation_euler = (math.radians(78), 0, math.radians(6))
    bpy.context.scene.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj

    print("[SUCCESS] Modern Kitchen with Base Cabinets, Countertops, Sinks, Cooktop, Upper Cabinets & Lighting Generated!")

if __name__ == "__main__":
    build_modern_kitchen()

"""
Custom 4m x 2m x 3m U-Shaped Kitchen Generator for Blender
------------------------------------------------------------
Exact Specifications:
1. Room Dimensions: Width (Door & North Wall) = 4.0m, Depth (Sides) = 2.0m, Height = 3.0m
2. North Wall (Facing Door, 4m wide):
   - Large Sunny Window (1.8m x 1.2m)
   - Center Undermount Double Sink & Matte Black Gooseneck Faucet right under the window
   - Ergonomic High Countertop (88cm) for comfortable dishwashing & prep without bending
3. West Wall (Left Side, 2m deep):
   - Seamlessly connected U-cabinet corner
   - Ergonomic Low Countertop (80cm) for Cooktop & Wok cooking without shoulder fatigue
   - Black crystal gas cooktop & deep pot drawers
   - Upper cabinets with integrated slim range hood
4. East Wall (Right Side, 2m deep):
   - Seamlessly connected U-cabinet corner
   - Double-Door Refrigerator near the entrance door
   - Spice pullout drawers & prep countertop north of fridge
   - Upper storage cabinets + multi-tier warm wood open spice shelves (调料架)
5. Continuous 3000K Warm LED under-cabinet task lighting
6. PBR materials, natural window sunlight, ceiling spots, and framed doorway camera
"""

import bpy
import bmesh
import math

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.meshes):
        bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
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

def build_4x2x3_u_kitchen():
    clear_scene()
    print("[INFO] Building 4.0m x 2.0m x 3.0m U-Shaped Kitchen...")
    
    # ── 1. Materials ──────────────────────────────────────────────
    mat_wall = create_mat("Mat_Wall", (0.93, 0.92, 0.90, 1.0), roughness=0.75)
    mat_floor = create_mat("Mat_Floor_GlazedTile", (0.82, 0.83, 0.85, 1.0), roughness=0.14, metallic=0.06)
    mat_backsplash = create_mat("Mat_Backsplash_Marble", (0.95, 0.95, 0.96, 1.0), roughness=0.10)
    mat_quartz = create_mat("Mat_Quartz_Countertop", (0.97, 0.97, 0.98, 1.0), roughness=0.08, metallic=0.02)
    mat_cab_base = create_mat("Mat_BaseCabinet_SlateGrey", (0.22, 0.23, 0.25, 1.0), roughness=0.45) # 哑光板岩深灰地柜
    mat_cab_upper = create_mat("Mat_UpperCabinet_CreamWhite", (0.94, 0.93, 0.90, 1.0), roughness=0.40) # 奶油白极简吊柜
    mat_wood = create_mat("Mat_SpiceRack_WarmOak", (0.72, 0.50, 0.34, 1.0), roughness=0.55) # 暖橡木调料格
    mat_metal_black = create_mat("Mat_MatteBlack", (0.10, 0.10, 0.11, 1.0), roughness=0.25, metallic=0.85)
    mat_stainless = create_mat("Mat_StainlessSteel", (0.86, 0.87, 0.89, 1.0), roughness=0.18, metallic=0.95)
    mat_glass = create_mat("Mat_BlackGlass", (0.05, 0.05, 0.06, 1.0), roughness=0.04, metallic=0.15)
    mat_window_glass = create_mat("Mat_ClearGlass", (0.95, 0.98, 1.0, 0.3), roughness=0.02, metallic=0.1)
    mat_fridge = create_mat("Mat_Fridge_BrushedTitanium", (0.42, 0.44, 0.47, 1.0), roughness=0.22, metallic=0.85)
    mat_led = create_mat("Mat_LED_Warm3000K", (1.0, 0.88, 0.65, 1.0), roughness=1.0, emission_color=(1.0, 0.88, 0.65, 1.0), emission_strength=12.0)
    
    # ── 2. Room Structure: 4.0m (X) x 2.0m (Y) x 3.0m (Z) ────────
    # Floor (4.0m x 2.0m)
    create_box("Kitchen_Floor", (4.4, 2.4, 0.02), (0.0, 0.0, -0.01), mat_floor)
    
    # North Wall (4.0m wide, 3.0m high, at Y = 1.0) with Window Cutout in Center
    # Left wall section of North wall
    create_box("Wall_North_Left", (1.05, 0.18, 3.00), (-1.475, 1.00, 1.50), mat_wall)
    # Right wall section of North wall
    create_box("Wall_North_Right", (1.05, 0.18, 3.00), (1.475, 1.00, 1.50), mat_wall)
    # Bottom section under window (Height Z = 0 to 1.15m)
    create_box("Wall_North_WindowSill", (1.90, 0.18, 1.15), (0.0, 1.00, 0.575), mat_wall)
    # Top section above window (Height Z = 2.45 to 3.0m)
    create_box("Wall_North_WindowTop", (1.90, 0.18, 0.55), (0.0, 1.00, 2.725), mat_wall)

    # Large Sunny North Window Frame & Glass (Facing Door: 1.80m wide x 1.30m high, Z = 1.15 to 2.45)
    create_box("Window_North_Frame", (1.82, 0.06, 1.30), (0.0, 1.00, 1.80), mat_metal_black)
    create_box("Window_North_Glass_L", (0.88, 0.01, 1.24), (-0.44, 1.00, 1.80), mat_window_glass)
    create_box("Window_North_Glass_R", (0.88, 0.01, 1.24), (0.44, 1.00, 1.80), mat_window_glass)

    # West Wall (Left 2.0m deep, 3.0m high, at X = -2.0)
    create_box("Wall_West_Left", (0.18, 2.00, 3.00), (-2.00, 0.0, 1.50), mat_wall)
    
    # East Wall (Right 2.0m deep, 3.0m high, at X = +2.0)
    create_box("Wall_East_Right", (0.18, 2.00, 3.00), (2.00, 0.0, 1.50), mat_wall)
    
    # South Wall (Doorway Wall at Y = -1.0 with 1.4m entry opening in center)
    create_box("Wall_South_Left", (1.30, 0.18, 3.00), (-1.35, -1.00, 1.50), mat_wall)
    create_box("Wall_South_Right", (1.30, 0.18, 3.00), (1.35, -1.00, 1.50), mat_wall)
    create_box("Wall_South_Top", (1.40, 0.18, 0.80), (0.0, -1.00, 2.60), mat_wall)

    # Backsplash Panels along the 3 U-sides
    create_box("Backsplash_North_L", (1.05, 0.02, 0.65), (-1.475, 0.89, 1.20), mat_backsplash)
    create_box("Backsplash_North_R", (1.05, 0.02, 0.65), (1.475, 0.89, 1.20), mat_backsplash)
    create_box("Backsplash_West", (0.02, 1.40, 0.65), (-1.89, 0.0, 1.15), mat_backsplash)
    create_box("Backsplash_East", (0.02, 0.80, 0.65), (1.89, 0.40, 1.20), mat_backsplash)

    # ── 3. U-Shaped Seamless Base Cabinets & Split-Level Counters ───
    # ── A. North Run (4m Facing Door - High Counter 88cm for Sink & Prep) ──
    create_box("ToeKick_North", (3.96, 0.52, 0.08), (0.0, 0.62, 0.04), mat_metal_black)
    
    # 1. Center Double Sink Base Cabinet (under window: width 1.0m)
    create_box("Base_North_SinkCarcass", (1.00, 0.58, 0.76), (0.0, 0.60, 0.46), mat_cab_base)
    create_box("Base_North_SinkDoor_L", (0.49, 0.02, 0.75), (-0.25, 0.30, 0.46), mat_cab_base)
    create_box("Base_North_SinkDoor_R", (0.49, 0.02, 0.75), (0.25, 0.30, 0.46), mat_cab_base)
    create_box("Base_North_Handle_L", (0.015, 0.015, 0.18), (-0.04, 0.285, 0.70), mat_metal_black)
    create_box("Base_North_Handle_R", (0.015, 0.015, 0.18), (0.04, 0.285, 0.70), mat_metal_black)

    # 2. Flanking Left Prep Drawers (width 0.85m)
    create_box("Base_North_PrepL_Carcass", (0.85, 0.58, 0.76), (-0.925, 0.60, 0.46), mat_cab_base)
    dh = 0.76 / 3.0
    for d in range(3):
        create_box(f"Base_North_PrepL_Draw_{d}", (0.84, 0.02, dh - 0.006), (-0.925, 0.30, 0.08 + dh * d + dh * 0.5), mat_cab_base)
        create_box(f"Base_North_PrepL_Hdl_{d}", (0.55, 0.015, 0.012), (-0.925, 0.285, 0.08 + dh * (d + 1) - 0.03), mat_metal_black)

    # 3. Flanking Right Prep Drawers (width 0.85m)
    create_box("Base_North_PrepR_Carcass", (0.85, 0.58, 0.76), (0.925, 0.60, 0.46), mat_cab_base)
    for d in range(3):
        create_box(f"Base_North_PrepR_Draw_{d}", (0.84, 0.02, dh - 0.006), (0.925, 0.30, 0.08 + dh * d + dh * 0.5), mat_cab_base)
        create_box(f"Base_North_PrepR_Hdl_{d}", (0.55, 0.015, 0.012), (0.925, 0.285, 0.08 + dh * (d + 1) - 0.03), mat_metal_black)

    # North High Countertop: Full 4.0m span, Depth 0.62m, Height 88cm (Z = 0.86m center)
    create_box("Countertop_North_High", (4.00, 0.62, 0.04), (0.0, 0.60, 0.86), mat_quartz)

    # Undermount Double Sink & Faucet right under the North window
    create_box("Sink_Basin_Large", (0.46, 0.40, 0.22), (-0.26, 0.60, 0.76), mat_stainless)
    create_box("Sink_Basin_Small", (0.34, 0.40, 0.20), (0.24, 0.60, 0.77), mat_stainless)
    create_box("Sink_Divider", (0.02, 0.40, 0.18), (-0.02, 0.60, 0.78), mat_stainless)
    create_box("Sink_Rim_Lip", (0.90, 0.46, 0.008), (0.0, 0.60, 0.895), mat_stainless)
    
    # Modern Black Gooseneck Faucet
    create_cylinder("Faucet_Base", 0.024, 0.06, (0.0, 0.84, 0.92), mat=mat_metal_black)
    create_cylinder("Faucet_Stem", 0.014, 0.28, (0.0, 0.84, 1.08), mat=mat_metal_black)
    create_cylinder("Faucet_Neck", 0.014, 0.18, (0.0, 0.76, 1.22), rot=(math.radians(65), 0, 0), mat=mat_metal_black)
    create_cylinder("Faucet_Spout", 0.018, 0.06, (0.0, 0.68, 1.16), mat=mat_metal_black)
    create_cylinder("Faucet_Lever", 0.008, 0.08, (0.06, 0.84, 0.95), rot=(0, math.radians(45), 0), mat=mat_metal_black)

    # ── B. West Run (Left Side Cooking Zone - Low Counter 80cm) ────
    create_box("ToeKick_West", (0.52, 1.40, 0.08), (-1.68, -0.20, 0.04), mat_metal_black)
    
    # Low Base Cabinets (Z = 0.08 to 0.76, height 0.68m, Countertop at 80cm)
    # Cooktop Unit in Center of West wall (Y = -0.20)
    create_box("Base_West_CooktopUnit", (0.58, 0.85, 0.68), (-1.68, -0.20, 0.42), mat_cab_base)
    dh_pot = 0.68 / 2.0
    for d in range(2):
        create_box(f"Base_West_PotDrawer_{d}", (0.02, 0.84, dh_pot - 0.006), (-1.38, -0.20, 0.08 + dh_pot * d + dh_pot * 0.5), mat_cab_base)
        create_box(f"Base_West_PotHandle_{d}", (0.015, 0.55, 0.012), (-1.365, -0.20, 0.08 + dh_pot * (d + 1) - 0.03), mat_metal_black)

    # South Corner Pullout on West wall
    create_box("Base_West_SouthPullout", (0.58, 0.45, 0.68), (-1.68, -0.725, 0.42), mat_cab_base)
    create_box("Base_West_SouthDoor", (0.02, 0.44, 0.67), (-1.38, -0.725, 0.42), mat_cab_base)
    create_box("Base_West_SouthHdl", (0.015, 0.25, 0.012), (-1.365, -0.725, 0.70), mat_metal_black)

    # West Low Countertop (Seamlessly meeting North High Counter via Step transition)
    create_box("Countertop_West_Low", (0.62, 1.40, 0.04), (-1.68, -0.20, 0.78), mat_quartz)

    # Built-in Black Crystal Gas Cooktop on West Wall
    create_box("Cooktop_Glass", (0.50, 0.76, 0.012), (-1.68, -0.20, 0.806), mat_glass)
    create_cylinder("Burner_W1", 0.09, 0.02, (-1.68, 0.0, 0.82), mat=mat_metal_black)
    create_cylinder("Burner_W2", 0.09, 0.02, (-1.68, -0.40, 0.82), mat=mat_metal_black)
    for ky in [-0.28, -0.20, -0.12]:
        create_cylinder(f"Cooktop_Knob_{ky}", 0.016, 0.016, (-1.48, ky, 0.82), mat=mat_metal_black)

    # ── C. East Run (Right Side - Refrigerator near door + Spice Prep) ─
    # 1. Double Door Smart Refrigerator near the door (Y = -1.0 to -0.25)
    create_box("Fridge_Cabinet_Housing", (0.75, 0.85, 2.30), (1.60, -0.575, 1.15), mat_cab_base)
    create_box("Fridge_Door_L", (0.03, 0.39, 1.95), (1.20, -0.77, 1.05), mat_fridge)
    create_box("Fridge_Door_R", (0.03, 0.39, 1.95), (1.20, -0.37, 1.05), mat_fridge)
    create_box("Fridge_Handle_L", (0.02, 0.015, 0.85), (1.17, -0.59, 1.15), mat_stainless)
    create_box("Fridge_Handle_R", (0.02, 0.015, 0.85), (1.17, -0.55, 1.15), mat_stainless)
    create_box("Fridge_TopStorage", (0.68, 0.83, 0.28), (1.60, -0.575, 2.14), mat_cab_base)

    # 2. Spice Pullout & Prep Base Cabinets (North of fridge: Y = -0.15 to +0.30)
    create_box("ToeKick_East", (0.52, 0.60, 0.08), (1.68, 0.05, 0.04), mat_metal_black)
    create_box("Base_East_SpicePrepCarcass", (0.58, 0.60, 0.76), (1.68, 0.05, 0.46), mat_cab_base)
    create_box("Base_East_SpiceFront", (0.02, 0.58, 0.75), (1.38, 0.05, 0.46), mat_cab_base)
    create_box("Base_East_SpiceHandle", (0.015, 0.35, 0.012), (1.365, 0.05, 0.75), mat_metal_black)
    create_box("Countertop_East_High", (0.62, 0.60, 0.04), (1.68, 0.05, 0.86), mat_quartz)

    # ── 4. Upper Wall Storage & Spice Cabinets (Up to Height Z = 2.65m) ──
    # ── A. West Upper Wall (Cooking Wall: 2m deep, Z = 1.60 to 2.50) ──
    # Center Range Hood Cabinet
    create_box("Upper_West_HoodCabinet", (0.38, 0.80, 0.45), (-1.80, -0.20, 2.25), mat_cab_upper)
    create_box("RangeHood_Body", (0.48, 0.78, 0.06), (-1.74, -0.20, 1.62), mat_metal_black)
    create_box("RangeHood_GlassVisor", (0.46, 0.76, 0.008), (-1.74, -0.20, 1.58), mat_glass)
    
    # Flanking Storage Upper Cabinets on Left Wall
    create_box("Upper_West_NorthStorage", (0.38, 0.55, 0.88), (-1.80, 0.35, 2.05), mat_cab_upper)
    create_box("Upper_West_SouthStorage", (0.38, 0.55, 0.88), (-1.80, -0.70, 2.05), mat_cab_upper)
    create_box("Upper_West_Door_N", (0.02, 0.53, 0.86), (-1.60, 0.35, 2.05), mat_cab_upper)
    create_box("Upper_West_Door_S", (0.02, 0.53, 0.86), (-1.60, -0.70, 2.05), mat_cab_upper)

    # ── B. East Upper Wall (Spice & Pantry Storage Wall: Z = 1.60 to 2.50) ──
    # Multi-Tier Warm Wood Open Spice Shelves (调料架)
    create_box("Upper_East_SpiceBox", (0.36, 0.65, 0.88), (1.80, 0.05, 2.05), mat_wood)
    create_box("Upper_East_Shelf_1", (0.34, 0.61, 0.02), (1.80, 0.05, 1.80), mat_wood)
    create_box("Upper_East_Shelf_2", (0.34, 0.61, 0.02), (1.80, 0.05, 2.10), mat_wood)
    create_box("Upper_East_Shelf_3", (0.34, 0.61, 0.02), (1.80, 0.05, 2.35), mat_wood)
    # Storage Cabinet north of spice shelf
    create_box("Upper_East_NorthStorage", (0.38, 0.50, 0.88), (1.80, 0.55, 2.05), mat_cab_upper)
    create_box("Upper_East_NorthDoor", (0.02, 0.48, 0.86), (1.60, 0.55, 2.05), mat_cab_upper)

    # ── 5. Continuous 3000K Warm LED Under-Cabinet Task Lights ─────
    create_box("LED_Strip_West", (0.02, 1.90, 0.008), (-1.60, -0.15, 1.55), mat_led)
    create_box("LED_Strip_East", (0.02, 1.15, 0.008), (1.60, 0.30, 1.55), mat_led)

    # ── 6. High Ceiling Downlights & Window Daylight (3.0m High Room) ─
    ceiling_spots = [(-1.0, 0.5), (0.0, 0.5), (1.0, 0.5), (-1.0, -0.4), (0.0, -0.4), (1.0, -0.4)]
    for idx, (cx, cy) in enumerate(ceiling_spots):
        sdata = bpy.data.lights.new(name=f"Spot_{idx}", type='SPOT')
        sdata.energy = 85.0
        sdata.spot_size = math.radians(72)
        sdata.spot_blend = 0.45
        sdata.color = (1.0, 0.96, 0.90)
        sobj = bpy.data.objects.new(f"Spot_{idx}", sdata)
        sobj.location = (cx, cy, 2.95)
        bpy.context.scene.collection.objects.link(sobj)

    # Natural Window Sun Beam streaming through the North window
    sun_data = bpy.data.lights.new(name="NorthWindow_Sun", type='SUN')
    sun_data.energy = 3.5
    sun_data.color = (0.95, 0.98, 1.0)
    sun_obj = bpy.data.objects.new("NorthWindow_Sun", sun_data)
    sun_obj.location = (0.0, 3.0, 2.2)
    sun_obj.rotation_euler = (math.radians(-35), math.radians(10), math.radians(175))
    bpy.context.scene.collection.objects.link(sun_obj)

    # ── 7. Doorway Viewport Perspective Camera ─────────────────────
    cam_data = bpy.data.cameras.new(name="Doorway_Camera")
    cam_data.lens = 18.0 # Ultra-wide 18mm to capture the entire 4x2x3 U-shape from the doorway
    cam_data.sensor_width = 36.0
    cam_obj = bpy.data.objects.new("Kitchen_Doorway_Camera", cam_data)
    cam_obj.location = (0.0, -1.85, 1.55)
    cam_obj.rotation_euler = (math.radians(80), 0, 0)
    bpy.context.scene.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj

    print("==================================================================")
    print(" [SUCCESS] 4.0m x 2.0m x 3.0m U-Shaped Kitchen Generated!")
    print("   ✓ Room: 4m (Door/North) x 2m (Sides) x 3m (Height)")
    print("   ✓ North Wall (Facing Door): Large Window + Center Double Sink (88cm High Counter)")
    print("   ✓ West Wall (Left): Cooktop, Deep Drawers & Slim Range Hood (80cm Low Counter)")
    print("   ✓ East Wall (Right): Double-Door Refrigerator near door + Spice Pullouts & Wall Rack")
    print("   ✓ Continuous U-Shaped Connected Cabinetry with Step Transition")
    print("   ✓ 3000K Warm Under-Cabinet LED Lighting & 18mm Wide-Angle Doorway View")
    print("==================================================================")

if __name__ == "__main__":
    build_4x2x3_u_kitchen()

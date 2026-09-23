import bpy
import bmesh
import math
import os

def reset_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh, do_unlink=True)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat, do_unlink=True)

def create_camo_pbr_material():
    mat = bpy.data.materials.new(name="PBR_Tank_Camouflage")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    output = nodes.new(type='ShaderNodeOutputMaterial')
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')

    tex_coord = nodes.new(type='ShaderNodeTexCoord')
    mapping = nodes.new(type='ShaderNodeMapping')
    if 'Scale' in mapping.inputs:
        mapping.inputs['Scale'].default_value = (1.8, 1.8, 1.8)
    links.new(tex_coord.outputs['Object'], mapping.inputs['Vector'])

    noise = nodes.new(type='ShaderNodeTexNoise')
    if 'Scale' in noise.inputs:
        noise.inputs['Scale'].default_value = 2.2
    if 'Detail' in noise.inputs:
        noise.inputs['Detail'].default_value = 6.0
    if 'Roughness' in noise.inputs:
        noise.inputs['Roughness'].default_value = 0.55
    links.new(mapping.outputs['Vector'], noise.inputs['Vector'])

    camo_ramp = nodes.new(type='ShaderNodeValToRGB')
    camo_ramp.color_ramp.elements[0].position = 0.32
    camo_ramp.color_ramp.elements[0].color = (0.10, 0.24, 0.08, 1.0) # NATO Green
    camo_ramp.color_ramp.elements.new(0.55)
    camo_ramp.color_ramp.elements[1].color = (0.16, 0.11, 0.06, 1.0) # Earth Brown
    camo_ramp.color_ramp.elements[2].position = 0.72
    camo_ramp.color_ramp.elements[2].color = (0.04, 0.04, 0.05, 1.0) # Charcoal Black
    links.new(noise.outputs['Fac'], camo_ramp.inputs['Fac'])

    bump_noise = nodes.new(type='ShaderNodeTexNoise')
    if 'Scale' in bump_noise.inputs:
        bump_noise.inputs['Scale'].default_value = 50.0
    links.new(mapping.outputs['Vector'], bump_noise.inputs['Vector'])

    bump = nodes.new(type='ShaderNodeBump')
    if 'Strength' in bump.inputs:
        bump.inputs['Strength'].default_value = 0.06
    links.new(bump_noise.outputs['Fac'], bump.inputs['Height'])

    if 'Base Color' in bsdf.inputs:
        links.new(camo_ramp.outputs['Color'], bsdf.inputs['Base Color'])
    else:
        links.new(camo_ramp.outputs['Color'], bsdf.inputs[0])

    if 'Metallic' in bsdf.inputs:
        bsdf.inputs['Metallic'].default_value = 0.25
    if 'Roughness' in bsdf.inputs:
        bsdf.inputs['Roughness'].default_value = 0.4
    if 'Normal' in bsdf.inputs:
        links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    return mat

def create_dark_metal_pbr_material():
    mat = bpy.data.materials.new(name="PBR_Dark_GunMetal")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    output = nodes.new(type='ShaderNodeOutputMaterial')
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')

    tex_coord = nodes.new(type='ShaderNodeTexCoord')
    noise = nodes.new(type='ShaderNodeTexNoise')
    if 'Scale' in noise.inputs:
        noise.inputs['Scale'].default_value = 35.0
    links.new(tex_coord.outputs['Object'], noise.inputs['Vector'])

    bump = nodes.new(type='ShaderNodeBump')
    if 'Strength' in bump.inputs:
        bump.inputs['Strength'].default_value = 0.10
    links.new(noise.outputs['Fac'], bump.inputs['Height'])

    if 'Base Color' in bsdf.inputs:
        bsdf.inputs['Base Color'].default_value = (0.04, 0.04, 0.05, 1.0)
    else:
        bsdf.inputs[0].default_value = (0.04, 0.04, 0.05, 1.0)

    if 'Metallic' in bsdf.inputs:
        bsdf.inputs['Metallic'].default_value = 0.85
    if 'Roughness' in bsdf.inputs:
        bsdf.inputs['Roughness'].default_value = 0.3
    if 'Normal' in bsdf.inputs:
        links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    return mat

def create_rubber_material():
    mat = bpy.data.materials.new(name="PBR_Tread_Rubber")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        if 'Base Color' in bsdf.inputs:
            bsdf.inputs['Base Color'].default_value = (0.03, 0.03, 0.035, 1.0)
        if 'Metallic' in bsdf.inputs:
            bsdf.inputs['Metallic'].default_value = 0.05
        if 'Roughness' in bsdf.inputs:
            bsdf.inputs['Roughness'].default_value = 0.75
    return mat

def create_optics_pbr_material():
    mat = bpy.data.materials.new(name="PBR_Optics_Glass")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        if 'Base Color' in bsdf.inputs:
            bsdf.inputs['Base Color'].default_value = (0.02, 0.6, 0.95, 1.0)
        if 'Metallic' in bsdf.inputs:
            bsdf.inputs['Metallic'].default_value = 0.95
        if 'Roughness' in bsdf.inputs:
            bsdf.inputs['Roughness'].default_value = 0.05
    return mat

def add_beveled_cube(name, location, scale=(1, 1, 1), rotation=(0, 0, 0), material=None, bevel_width=0.03):
    mesh = bpy.data.meshes.new(name + "_mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bm.to_mesh(mesh)
    bm.free()

    obj.location = location
    obj.scale = (scale[0] * 0.5, scale[1] * 0.5, scale[2] * 0.5)
    obj.rotation_euler = rotation

    bevel = obj.modifiers.new(name="Bevel", type='BEVEL')
    bevel.width = bevel_width
    bevel.segments = 2

    if material:
        obj.data.materials.append(material)

    for poly in mesh.polygons:
        poly.use_smooth = True
    return obj

def add_smooth_cylinder(name, location, radius=0.5, depth=1.0, rotation=(0, 0, 0), material=None, segments=32):
    mesh = bpy.data.meshes.new(name + "_mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius, radius2=radius, depth=depth)
    bm.to_mesh(mesh)
    bm.free()

    obj.location = location
    obj.rotation_euler = rotation
    if material:
        obj.data.materials.append(material)

    for poly in mesh.polygons:
        poly.use_smooth = True
    return obj

def build_high_detail_tracks(side_x, mat_metal, mat_rubber):
    # Generates a physical array of individual track shoes around sprockets & wheels
    # Side X: -1.65 (Left) or +1.65 (Right)
    sprocket_y, sprocket_z = 2.8, 0.68
    idler_y, idler_z = -2.8, 0.68
    radius = 0.44

    # Track shoe path points (Loop around drive sprocket, top return, idler, bottom ground)
    pts = []
    
    # Top straight segment (Y: -2.8 to +2.8, Z: 1.12)
    steps = 30
    for i in range(steps):
        y = -2.8 + (i / steps) * 5.6
        pts.append((y, 1.12, 0.0)) # (Y, Z, Tangent Angle rad)

    # Front Sprocket Arc (around +2.8, 0.68)
    arc_steps = 15
    for i in range(arc_steps):
        angle = math.pi / 2 - (i / arc_steps) * math.pi
        y = sprocket_y + radius * math.sin(angle)
        z = sprocket_z + radius * math.cos(angle)
        pts.append((y, z, angle - math.pi/2))

    # Bottom straight segment (Y: +2.8 to -2.8, Z: 0.24)
    for i in range(steps):
        y = 2.8 - (i / steps) * 5.6
        pts.append((y, 0.24, math.pi))

    # Rear Idler Arc (around -2.8, 0.68)
    for i in range(arc_steps):
        angle = -math.pi / 2 - (i / arc_steps) * math.pi
        y = idler_y + radius * math.sin(angle)
        z = idler_z + radius * math.cos(angle)
        pts.append((y, z, angle - math.pi/2))

    # Place individual physical Track Link Shoes along the path
    for idx, (py, pz, tang_angle) in enumerate(pts):
        link_name = f"TrackLink_{'L' if side_x < 0 else 'R'}_{idx}"
        
        # Steel Track Shoe Body
        add_beveled_cube(link_name + "_Steel", location=(side_x, py, pz), scale=(0.65, 0.14, 0.06), rotation=(tang_angle, 0, 0), material=mat_metal, bevel_width=0.01)
        
        # Center Guide Tooth (Slots between dual road wheels)
        add_beveled_cube(link_name + "_Tooth", location=(side_x, py, pz + 0.08), scale=(0.08, 0.10, 0.12), rotation=(tang_angle, 0, 0), material=mat_metal, bevel_width=0.01)
        
        # Rubber Ground Pad (Bottom of shoe)
        add_beveled_cube(link_name + "_Rubber", location=(side_x, py, pz - 0.04), scale=(0.58, 0.10, 0.04), rotation=(tang_angle, 0, 0), material=mat_rubber, bevel_width=0.01)
        
        # Left & Right Steel Connection Pins
        for pin_side in [-0.34, 0.34]:
            add_smooth_cylinder(link_name + "_Pin", location=(side_x + pin_side, py, pz), radius=0.02, depth=0.14, rotation=(tang_angle + math.radians(90), 0, 0), material=mat_metal, segments=12)

def build_sprocket_with_teeth(side_x, loc_y, loc_z, mat_metal):
    # Drive Sprocket with 18 physical gear teeth
    radius = 0.44
    add_smooth_cylinder("Sprocket_Core_Inner", location=(side_x, loc_y, loc_z), radius=radius, depth=0.38, rotation=(0, math.radians(90), 0), material=mat_metal)
    add_smooth_cylinder("Sprocket_Core_Hub", location=(side_x + (0.22 if side_x > 0 else -0.22), loc_y, loc_z), radius=0.22, depth=0.08, rotation=(0, math.radians(90), 0), material=mat_metal)

    # Sprocket Gear Teeth
    teeth_count = 18
    for i in range(teeth_count):
        a = (i / teeth_count) * math.tau
        ty = loc_y + (radius + 0.06) * math.sin(a)
        tz = loc_z + (radius + 0.06) * math.cos(a)
        add_beveled_cube(f"Sprocket_Tooth_{i}", location=(side_x, ty, tz), scale=(0.36, 0.06, 0.10), rotation=(a, 0, 0), material=mat_metal, bevel_width=0.01)

def build_realistic_modern_tank():
    reset_scene()

    mat_camo = create_camo_pbr_material()
    mat_metal = create_dark_metal_pbr_material()
    mat_rubber = create_rubber_material()
    mat_optics = create_optics_pbr_material()

    # 1. Main Hull Structure
    hull = add_beveled_cube("Main_Hull", location=(0, 0, 0.95), scale=(3.7, 6.8, 0.75), material=mat_camo, bevel_width=0.04)

    # Upper Sloped Front Glacis Plate
    glacis_upper = add_beveled_cube("Glacis_Upper", location=(0, 2.7, 1.05), scale=(3.6, 1.8, 0.45), rotation=(math.radians(-28), 0, 0), material=mat_camo, bevel_width=0.04)

    # Lower Front Glacis Plate
    glacis_lower = add_beveled_cube("Glacis_Lower", location=(0, 3.25, 0.65), scale=(3.5, 0.8, 0.4), rotation=(math.radians(35), 0, 0), material=mat_camo, bevel_width=0.03)

    # Driver Hatch & Vision Periscope
    add_beveled_cube("Driver_Hatch", location=(0, 2.2, 1.3), scale=(0.8, 0.6, 0.12), material=mat_metal)
    add_beveled_cube("Driver_Periscope", location=(0, 2.45, 1.38), scale=(0.4, 0.12, 0.15), material=mat_optics)

    # Front Towing Shackles (Left & Right)
    for side in [-1, 1]:
        add_smooth_cylinder("Tow_Shackle_Ring", location=(side * 1.4, 3.48, 0.6), radius=0.18, depth=0.08, rotation=(0, math.radians(90), 0), material=mat_metal)
        add_beveled_cube("Tow_Shackle_Mount", location=(side * 1.4, 3.42, 0.6), scale=(0.15, 0.2, 0.25), material=mat_metal)

    # Rear Engine Deck & Louvers
    engine_deck = add_beveled_cube("Engine_Deck", location=(0, -2.5, 1.15), scale=(3.5, 1.8, 0.35), material=mat_camo)
    for offset_y in [-2.1, -2.5, -2.9]:
        for offset_x in [-0.9, 0.9]:
            add_beveled_cube("Engine_Louver", location=(offset_x, offset_y, 1.34), scale=(1.1, 0.32, 0.05), material=mat_metal)

    # Rear Exhaust Vents
    for side in [-1, 1]:
        add_beveled_cube("Exhaust_Vent", location=(side * 1.3, -3.42, 1.0), scale=(0.6, 0.12, 0.4), material=mat_metal)

    # 2. Side Skirts & ERA Panels
    for side in [-1, 1]:
        add_beveled_cube(f"Side_Skirt_{'L' if side==-1 else 'R'}", location=(side * 2.05, 0, 0.78), scale=(0.18, 6.6, 0.85), material=mat_camo, bevel_width=0.03)

        # Front Mudguards
        add_beveled_cube("Front_Mudguard", location=(side * 2.05, 3.35, 0.65), scale=(0.22, 0.5, 0.6), rotation=(math.radians(20), 0, 0), material=mat_metal)

        # Reactive Armor ERA Tiles (7 Modules)
        for i in range(7):
            add_beveled_cube("ERA_Tile", location=(side * 2.12, -2.2 + i * 0.72, 0.8), scale=(0.08, 0.6, 0.72), material=mat_camo, bevel_width=0.02)

    # 3. High-Detail Suspension, Sprockets with Teeth & Physical Track Links
    for side in [-1.65, 1.65]:
        # Build High-Detail Physical Tracks Loop
        build_high_detail_tracks(side, mat_metal, mat_rubber)

        # Build Teethed Drive Sprocket (Front) & Idler Wheel (Rear)
        build_sprocket_with_teeth(side, 2.8, 0.68, mat_metal)
        build_sprocket_with_teeth(side, -2.8, 0.68, mat_metal)

        # Dual Road Wheels (7 Pairs per side) with Rubber Tires
        for w in range(7):
            wheel_y = -2.4 + w * 0.8
            # Dual Rim Outer & Inner Wheels
            for w_offset in [-0.12, 0.12]:
                add_smooth_cylinder("Road_Wheel_Steel_Rim", location=(side + w_offset, wheel_y, 0.48), radius=0.38, depth=0.16, rotation=(0, math.radians(90), 0), material=mat_metal)
                add_smooth_cylinder("Road_Wheel_Rubber_Tire", location=(side + w_offset, wheel_y, 0.48), radius=0.40, depth=0.12, rotation=(0, math.radians(90), 0), material=mat_rubber)

            # Center Hub Grease Cap
            add_smooth_cylinder("Road_Wheel_Center_Hub", location=(side + (0.24 if side > 0 else -0.24), wheel_y, 0.48), radius=0.18, depth=0.08, rotation=(0, math.radians(90), 0), material=mat_metal)

        # Top Return Rollers
        for r in range(4):
            add_smooth_cylinder("Return_Roller", location=(side, -1.8 + r * 1.2, 0.95), radius=0.14, depth=0.4, rotation=(0, math.radians(90), 0), material=mat_metal)

    # 4. Turret Assembly (Wedge Composite Armor Turret)
    turret_base = add_beveled_cube("Turret_Base", location=(0, -0.1, 1.75), scale=(2.9, 3.4, 0.75), material=mat_camo, bevel_width=0.05)

    # Sloped Wedge Armor Cheeks
    for side in [-1, 1]:
        wedge = add_beveled_cube(f"Turret_Wedge_{'L' if side==-1 else 'R'}", location=(side * 0.85, 1.25, 1.75), scale=(1.35, 1.5, 0.72), rotation=(0, 0, math.radians(side * -30)), material=mat_camo, bevel_width=0.05)

    # Turret Bustle Rack Frame & Gear Boxes
    bustle_rack = add_beveled_cube("Bustle_Rack_Frame", location=(0, -2.0, 1.7), scale=(2.8, 0.9, 0.65), material=mat_metal, bevel_width=0.02)
    add_beveled_cube("Gear_Box_L", location=(-0.8, -2.0, 1.7), scale=(0.9, 0.7, 0.5), material=mat_camo)
    add_beveled_cube("Gear_Box_R", location=(0.8, -2.0, 1.7), scale=(0.9, 0.7, 0.5), material=mat_camo)

    # Rear Antenna Masts
    for side in [-1, 1]:
        add_smooth_cylinder("Antenna_Base", location=(side * 1.1, -2.3, 2.1), radius=0.08, depth=0.2, material=mat_metal)
        add_smooth_cylinder("Antenna_Rod", location=(side * 1.1, -2.3, 3.2), radius=0.015, depth=2.0, material=mat_metal)

    # 5. Main Cannon Assembly (120mm L/55 Smoothbore)
    mantlet = add_beveled_cube("Gun_Mantlet", location=(0, 1.9, 1.75), scale=(1.1, 0.65, 0.62), material=mat_metal, bevel_width=0.04)

    # Long Cannon Barrel with Thermal Sleeves
    barrel_main = add_smooth_cylinder("Cannon_Barrel", location=(0, 4.0, 1.75), radius=0.11, depth=4.2, rotation=(math.radians(90), 0, 0), material=mat_metal)

    # Thermal Sleeve Clamps
    add_smooth_cylinder("Thermal_Sleeve_1", location=(0, 2.8, 1.75), radius=0.14, depth=1.0, rotation=(math.radians(90), 0, 0), material=mat_camo)
    add_smooth_cylinder("Thermal_Sleeve_2", location=(0, 4.4, 1.75), radius=0.13, depth=1.2, rotation=(math.radians(90), 0, 0), material=mat_camo)

    # Central Bore Evacuator
    add_smooth_cylinder("Bore_Evacuator", location=(0, 3.6, 1.75), radius=0.17, depth=0.85, rotation=(math.radians(90), 0, 0), material=mat_metal)

    # Muzzle Reference Sensor Mirror
    add_smooth_cylinder("Muzzle_Sensor", location=(0, 6.1, 1.75), radius=0.13, depth=0.25, rotation=(math.radians(90), 0, 0), material=mat_metal)
    add_beveled_cube("Muzzle_Mirror", location=(0.14, 6.05, 1.75), scale=(0.08, 0.12, 0.12), material=mat_optics)

    # 6. Panoramic CITV Optics, GPS Sight, Commander Hatch & RWS HMG
    add_smooth_cylinder("Commander_Hatch_Ring", location=(0.7, -0.4, 2.18), radius=0.48, depth=0.12, material=mat_metal)
    add_smooth_cylinder("Commander_Hatch_Lid", location=(0.7, -0.4, 2.26), radius=0.44, depth=0.08, material=mat_camo)

    # Gunner Primary Sight Housing
    add_beveled_cube("GPS_Housing", location=(0.7, 0.8, 2.25), scale=(0.6, 0.5, 0.4), material=mat_metal)
    add_beveled_cube("GPS_Lens", location=(0.7, 0.98, 2.25), scale=(0.45, 0.08, 0.25), material=mat_optics)

    # CITV Commander Thermal Viewer
    add_smooth_cylinder("CITV_Base", location=(-0.7, -0.2, 2.3), radius=0.24, depth=0.45, material=mat_metal)
    add_beveled_cube("CITV_Hood", location=(-0.7, -0.05, 2.42), scale=(0.38, 0.28, 0.22), material=mat_metal)
    add_beveled_cube("CITV_Glass", location=(-0.7, 0.06, 2.42), scale=(0.32, 0.06, 0.16), material=mat_optics)

    # Dual 6-Barrel Smoke Grenade Launchers
    for side in [-1, 1]:
        for row in range(2):
            for col in range(3):
                smk_x = side * 1.45
                smk_y = 0.2 + col * 0.14
                smk_z = 2.05 + row * 0.14
                add_smooth_cylinder("Smoke_Discharger", location=(smk_x, smk_y, smk_z), radius=0.055, depth=0.32, rotation=(math.radians(30), math.radians(side * 42), 0), material=mat_metal)

    # Remote Weapon Station (RWS) HMG
    rws_mount = add_beveled_cube("RWS_Mount", location=(0.7, -0.4, 2.4), scale=(0.3, 0.3, 0.25), material=mat_metal)
    add_beveled_cube("HMG_Receiver", location=(0.7, -0.3, 2.6), scale=(0.18, 0.8, 0.22), material=mat_metal)
    add_smooth_cylinder("HMG_Heavy_Barrel", location=(0.7, 0.3, 2.62), radius=0.04, depth=0.9, rotation=(math.radians(90), 0, 0), material=mat_metal)
    add_beveled_cube("HMG_Ammo_Box", location=(0.92, -0.3, 2.6), scale=(0.2, 0.35, 0.28), material=mat_camo)

    # Front Headlight Assemblies
    for side in [-1, 1]:
        add_beveled_cube("Headlight_Box", location=(side * 1.35, 3.3, 1.05), scale=(0.35, 0.18, 0.25), material=mat_metal)

def setup_studio_lighting_and_camera(output_img_path):
    # Camera setup with Track To constraint targeting tank center (0, 0, 1.2)
    cam_data = bpy.data.cameras.new("Hero_Camera_Data")
    cam_data.lens = 45
    cam_obj = bpy.data.objects.new("Hero_Camera", cam_data)
    bpy.context.scene.collection.objects.link(cam_obj)
    cam_obj.location = (8.5, -8.5, 5.0)

    target_obj = bpy.data.objects.new("Cam_Target", None)
    target_obj.location = (0, 0, 1.2)
    bpy.context.scene.collection.objects.link(target_obj)

    track_to = cam_obj.constraints.new(type='TRACK_TO')
    track_to.target = target_obj
    track_to.track_axis = 'TRACK_NEGATIVE_Z'
    track_to.up_axis = 'UP_Y'

    bpy.context.scene.camera = cam_obj

    # Key Direct Sunlight (Warm)
    key_light_data = bpy.data.lights.new("Key_Sun", type='SUN')
    key_light_data.energy = 5.5
    key_light_data.color = (1.0, 0.96, 0.88)
    key_light_obj = bpy.data.objects.new("Key_Sun", key_light_data)
    bpy.context.scene.collection.objects.link(key_light_obj)
    key_light_obj.location = (10, -10, 12)

    # Fill Area Light (Sky Blue)
    fill_light_data = bpy.data.lights.new("Fill_Area", type='AREA')
    fill_light_data.energy = 280.0
    fill_light_data.color = (0.55, 0.75, 1.0)
    fill_light_obj = bpy.data.objects.new("Fill_Area", fill_light_data)
    bpy.context.scene.collection.objects.link(fill_light_obj)
    fill_light_obj.location = (-10, 8, 10)

    # Studio World
    world = bpy.data.worlds.new("HighDetail_Track_Studio_World")
    world.use_nodes = True
    bg_node = world.node_tree.nodes.get("Background")
    if bg_node:
        bg_node.inputs['Color'].default_value = (0.12, 0.14, 0.16, 1.0)
    bpy.context.scene.world = world

    scene = bpy.context.scene
    scene.render.resolution_x = 1440
    scene.render.resolution_y = 1080
    scene.render.filepath = output_img_path

    print(f"[BLENDER] Rendering high-detail track tank image to {output_img_path}...")
    bpy.ops.render.render(write_still=True)

def export_glb(output_glb_path):
    for obj in bpy.data.objects:
        obj.select_set(True)
    try:
        bpy.ops.export_scene.gltf(filepath=output_glb_path, export_format='GLB')
        print(f"[BLENDER] Exported AAA-Track GLB Model: {output_glb_path}")
    except Exception as e:
        print(f"[ERROR] gltf export failed: {e}")

if __name__ == "__main__":
    os.makedirs("PreviewOutput", exist_ok=True)
    os.makedirs("assets/models", exist_ok=True)

    img_path = os.path.abspath("PreviewOutput/realistic_tank_preview.png")
    glb_path = os.path.abspath("assets/models/realistic_tank.glb")
    blend_path = os.path.abspath("assets/models/realistic_tank.blend")

    print("[BLENDER] Generating Modern Main Battle Tank with High-Detail Physical Tracks...")
    build_realistic_modern_tank()

    # Save Blend file
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[BLENDER] Saved blend file: {blend_path}")

    # Export GLB
    export_glb(glb_path)

    # Render High-Res Studio Preview PNG
    setup_studio_lighting_and_camera(img_path)
    print("[BLENDER] High-Detail Track Tank Generation Complete!")

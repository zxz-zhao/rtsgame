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

def create_material(name, diffuse_color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = None
    for n in nodes:
        if n.type == 'BSDF_PRINCIPLED':
            bsdf = n
            break
    if bsdf:
        if 'Base Color' in bsdf.inputs:
            bsdf.inputs['Base Color'].default_value = diffuse_color
        else:
            bsdf.inputs[0].default_value = diffuse_color

        if 'Metallic' in bsdf.inputs:
            bsdf.inputs['Metallic'].default_value = metallic
        if 'Roughness' in bsdf.inputs:
            bsdf.inputs['Roughness'].default_value = roughness
    return mat

def add_cube(name, location, scale=(1, 1, 1), rotation=(0, 0, 0), material=None):
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
    if material:
        obj.data.materials.append(material)
    return obj

def add_cylinder(name, location, radius=0.5, depth=1.0, rotation=(0, 0, 0), material=None):
    mesh = bpy.data.meshes.new(name + "_mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=24, radius1=radius, radius2=radius, depth=depth)
    bm.to_mesh(mesh)
    bm.free()

    obj.location = location
    obj.rotation_euler = rotation
    if material:
        obj.data.materials.append(material)
    return obj

def build_modern_tank():
    reset_scene()

    # Colors & Materials (NATO Green, Dark Composite Armor, Gunmetal, Track Steel, Cyber Blue Optics)
    mat_camo = create_material("TankArmor_CamoGreen", (0.12, 0.28, 0.10, 1.0), metallic=0.15, roughness=0.35)
    mat_armor_dark = create_material("TankArmor_DarkComp", (0.05, 0.08, 0.05, 1.0), metallic=0.3, roughness=0.3)
    mat_track = create_material("TankTrack_Steel", (0.08, 0.08, 0.09, 1.0), metallic=0.85, roughness=0.5)
    mat_metal_dark = create_material("GunMetal_Dark", (0.02, 0.02, 0.03, 1.0), metallic=0.95, roughness=0.2)
    mat_glass = create_material("Optics_Glass", (0.05, 0.5, 0.95, 1.0), metallic=0.9, roughness=0.1)
    mat_light = create_material("Headlight_Emission", (1.0, 0.95, 0.7, 1.0), metallic=0.0, roughness=0.1)

    # 1. Main Hull
    add_cube("Tank_Hull", location=(0, 0, 0.9), scale=(3.6, 6.4, 0.8), material=mat_camo)

    # Sloped Front Glacis Armor
    add_cube("Front_Glacis", location=(0, 2.6, 1.0), scale=(3.5, 1.6, 0.5), rotation=(math.radians(-25), 0, 0), material=mat_camo)

    # Rear Engine Deck
    add_cube("Engine_Deck", location=(0, -2.4, 1.1), scale=(3.4, 1.6, 0.4), material=mat_armor_dark)

    # Engine Ventilation Grilles
    for offset_y in [-2.2, -2.6]:
        for offset_x in [-0.8, 0.8]:
            add_cube("Engine_Vent", location=(offset_x, offset_y, 1.32), scale=(1.0, 0.6, 0.06), material=mat_metal_dark)

    # 2. Side Skirts (ERA Armor Modules)
    for side in [-1, 1]:
        add_cube(f"Side_Skirt_{'L' if side==-1 else 'R'}", location=(side * 1.95, 0, 0.75), scale=(0.2, 6.2, 0.9), material=mat_armor_dark)

        # Reactive Armor Modules
        for i in range(7):
            add_cube("ERA_Module", location=(side * 2.03, -2.1 + i * 0.7, 0.75), scale=(0.1, 0.56, 0.7), material=mat_camo)

    # 3. Tracks & Road Wheels
    for side in [-1, 1]:
        add_cube("Track_Block", location=(side * 1.6, 0, 0.6), scale=(0.7, 6.4, 0.9), material=mat_track)

        # 6 Road Wheels per side
        for w in range(6):
            add_cylinder("Road_Wheel", location=(side * 1.6, -2.2 + w * 0.88, 0.48), radius=0.38, depth=0.4, rotation=(0, math.radians(90), 0), material=mat_metal_dark)

        # Sprocket & Idler Wheels
        add_cylinder("Sprocket", location=(side * 1.6, 2.7, 0.65), radius=0.42, depth=0.4, rotation=(0, math.radians(90), 0), material=mat_metal_dark)
        add_cylinder("Idler", location=(side * 1.6, -2.7, 0.65), radius=0.42, depth=0.4, rotation=(0, math.radians(90), 0), material=mat_metal_dark)

    # 4. Turret Assembly (Composite Armor Wedge Turret)
    add_cube("Turret_Main", location=(0, -0.2, 1.7), scale=(2.8, 3.2, 0.8), material=mat_camo)

    # Wedge Front Armor (Left & Right)
    for side in [-1, 1]:
        add_cube(f"Turret_Wedge_{'L' if side==-1 else 'R'}", location=(side * 0.8, 1.1, 1.7), scale=(1.3, 1.4, 0.76), rotation=(0, 0, math.radians(side * -28)), material=mat_camo)

    # Turret Rear Storage Basket
    add_cube("Turret_Basket", location=(0, -1.9, 1.65), scale=(2.6, 0.8, 0.6), material=mat_metal_dark)

    # 5. Main Cannon Barrel (120mm Smoothbore) & Mantlet
    add_cube("Gun_Mantlet", location=(0, 1.75, 1.7), scale=(1.0, 0.6, 0.6), material=mat_armor_dark)

    # Cannon Barrel
    add_cylinder("Main_Cannon_Barrel", location=(0, 3.6, 1.7), radius=0.1, depth=3.8, rotation=(math.radians(90), 0, 0), material=mat_metal_dark)

    # Bore Evacuator
    add_cylinder("Bore_Evacuator", location=(0, 3.4, 1.7), radius=0.15, depth=0.8, rotation=(math.radians(90), 0, 0), material=mat_armor_dark)

    # Muzzle Tip
    add_cylinder("Muzzle_Tip", location=(0, 5.4, 1.7), radius=0.12, depth=0.2, rotation=(math.radians(90), 0, 0), material=mat_metal_dark)

    # 6. Commander Hatch, CITV Thermal Optics, Headlights & Smoke Grenade Launchers
    add_cylinder("Commander_Hatch", location=(0.6, -0.4, 2.15), radius=0.45, depth=0.15, material=mat_armor_dark)

    # CITV Optics
    add_cylinder("CITV_Optics", location=(-0.6, -0.2, 2.3), radius=0.22, depth=0.45, material=mat_metal_dark)
    add_cube("CITV_Lens", location=(-0.6, -0.08, 2.35), scale=(0.36, 0.1, 0.3), material=mat_glass)

    # Headlights
    for side in [-1, 1]:
        add_cube("Headlight", location=(side * 1.3, 3.1, 1.05), scale=(0.3, 0.16, 0.24), material=mat_light)

    # Smoke Grenade Launchers
    for side in [-1, 1]:
        for i in range(4):
            add_cylinder("Smoke_Launcher", location=(side * 1.35, 0.3 + i * 0.15, 2.0), radius=0.06, depth=0.35, rotation=(math.radians(25), math.radians(side * 40), 0), material=mat_metal_dark)

    # Heavy Machine Gun on Commander Hatch
    add_cube("HMG_Body", location=(0.6, -0.4, 2.45), scale=(0.16, 0.9, 0.2), material=mat_metal_dark)
    add_cylinder("HMG_Barrel", location=(0.6, 0.1, 2.48), radius=0.03, depth=0.8, rotation=(math.radians(90), 0, 0), material=mat_metal_dark)

def setup_camera_and_lighting(output_img_path):
    # Camera setup
    cam_data = bpy.data.cameras.new("Hero_Camera_Data")
    cam_obj = bpy.data.objects.new("Hero_Camera", cam_data)
    bpy.context.scene.collection.objects.link(cam_obj)
    cam_obj.location = (8.5, -9.5, 6.5)
    cam_obj.rotation_euler = (math.radians(62), 0, math.radians(42))
    bpy.context.scene.camera = cam_obj

    # Key Sun Light
    key_light_data = bpy.data.lights.new("Key_Sun", type='SUN')
    key_light_data.energy = 5.0
    key_light_data.color = (1.0, 0.95, 0.85)
    key_light_obj = bpy.data.objects.new("Key_Sun", key_light_data)
    bpy.context.scene.collection.objects.link(key_light_obj)
    key_light_obj.location = (10, -10, 12)
    key_light_obj.rotation_euler = (math.radians(45), math.radians(25), math.radians(30))

    # Fill Area Light
    fill_light_data = bpy.data.lights.new("Fill_Area", type='AREA')
    fill_light_data.energy = 200.0
    fill_light_data.color = (0.5, 0.7, 1.0)
    fill_light_obj = bpy.data.objects.new("Fill_Area", fill_light_data)
    bpy.context.scene.collection.objects.link(fill_light_obj)
    fill_light_obj.location = (-8, 6, 8)
    fill_light_obj.rotation_euler = (math.radians(-45), math.radians(-30), 0)

    # World background
    world = bpy.data.worlds.new("TankWorld")
    world.use_nodes = True
    bg_node = world.node_tree.nodes.get("Background")
    if bg_node:
        bg_node.inputs['Color'].default_value = (0.12, 0.15, 0.18, 1.0)
    bpy.context.scene.world = world

    # Render Settings
    scene = bpy.context.scene
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 960
    scene.render.filepath = output_img_path

    print(f"[BLENDER] Rendering image to {output_img_path}...")
    bpy.ops.render.render(write_still=True)

def export_glb(output_glb_path):
    for obj in bpy.data.objects:
        obj.select_set(True)
    try:
        bpy.ops.export_scene.gltf(filepath=output_glb_path, export_format='GLB')
        print(f"[BLENDER] Exported GLB: {output_glb_path}")
    except Exception as e:
        print(f"[ERROR] gltf export failed: {e}")

if __name__ == "__main__":
    os.makedirs("PreviewOutput", exist_ok=True)
    os.makedirs("assets/models", exist_ok=True)

    img_path = os.path.abspath("PreviewOutput/modern_tank_preview.png")
    glb_path = os.path.abspath("assets/models/modern_tank.glb")
    blend_path = os.path.abspath("assets/models/modern_tank.blend")

    print("[BLENDER] Generating Modern Tank Model using bmesh API...")
    build_modern_tank()

    # Save Blend file
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"[BLENDER] Saved blend file: {blend_path}")

    # Export GLB
    export_glb(glb_path)

    # Render Preview PNG
    setup_camera_and_lighting(img_path)
    print("[BLENDER] Modern Tank Generation Complete!")

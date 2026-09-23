---
name: game-asset-reverse
description: >-
  Use this skill when inspecting, extracting, repairing, or reverse engineering 3D models (FBX, GLB, OBJ),
  bone animation hierarchies, and texture channels for Godot 4 RTS units, buildings, and terrain.
---

# Game Asset Reverse Engineering & Inspection Skill

This skill guides the agent in analyzing proprietary or legacy 3D game models, fixing coordinate inversions, retargeting bone animations, and optimizing meshes for Godot 4 C#.

## 1. 3D Model Inspection & Mesh Hierarchy Analysis

### Python Inspection Scripts
When encountering external or legacy models with missing textures, broken pivots, or unknown node hierarchies:
```python
# Inspect all nodes and mesh names inside GLB/GLTF
import json
with open("model.gltf", "r", encoding="utf-8") as f:
    data = json.load(f)
nodes = data.get("nodes", [])
meshes = data.get("meshes", [])
print(f"Nodes: {len(nodes)}, Meshes: {len(meshes)}")
for idx, node in enumerate(nodes):
    print(f"Node[{idx}]: {node.get('name')} (Mesh: {node.get('mesh')})")
```

### Common Coordinate & Scaling Gotchas in Godot 4
- **Unit Scale Inconsistency**: Legacy Unity assets often have 0.01x or 100x scale factors embedded in the root node. Bake the scale transform into vertex data or configure `.import` options.
- **Pivot Offset**: If a ship or vehicle pivots around its bow or cabin instead of its geometric center, calculate the Axis-Aligned Bounding Box (AABB) center and offset the child `MeshInstance3D`:
  $$\text{Offset} = -(\text{AABB.Position} + \text{AABB.Size} \times 0.5)$$

---

## 2. Bone Animation Retargeting (e.g. Mixamo $\rightarrow$ Godot)

Refer to [`InfantryAnimationBridge.cs`](../../scripts/battle/InfantryAnimationBridge.cs) as the reference implementation.

### Mapping Principles
1. **Bone Name Aliasing**:
   - Unity/Mixamo convention: `mixamorig:Hips`, `mixamorig:Spine`, `mixamorig:RightArm`.
   - Godot standard humanoid: `Hips`, `Spine`, `RightUpperArm`.
2. **Animation Injection Pattern**:
   - Load animation libraries from standalone FBX/GLB files at runtime or import time.
   - Inject animation tracks into the model's `AnimationPlayer`:
     ```csharp
     var library = new AnimationLibrary();
     library.AddAnimation("idle", idleAnim);
     library.AddAnimation("walk", walkAnim);
     library.AddAnimation("fire", fireAnim);
     animPlayer.AddAnimationLibrary("combat", library);
     ```

---

## 3. Texture Channel Extraction & Conversion

### Normal Map Green Channel (Y) Inversion
- **DirectX (Unity default)**: Normal map Y is **inverted** (-Y / Green channel points down).
- **OpenGL (Godot 4 default)**: Normal map Y points **up** (+Y).
- **Fix**: Invert the Green channel in shader or preprocess with Python:
  ```python
  from PIL import Image
  img = Image.open("normal_dx.png").convert("RGBA")
  r, g, b, a = img.split()
  g = g.point(lambda i: 255 - i) # Invert green channel
  Image.merge("RGBA", (r, g, b, a)).save("normal_gl.png")
  ```

### Channel Packing
- Godot 4 StandardMaterial3D Roughness/Metallic texture packing:
  - **Red Channel**: Ambient Occlusion (AO)
  - **Green Channel**: Roughness
  - **Blue Channel**: Metallic

---

## 4. Visual Verification

Run the Godot engine executable in non-headless preview mode to capture visual confirmation:
```powershell
& "path/to/godot.exe" --path "e:/code/c++/UnityRTS/GodotRTS" --capture-preview "scratch/model_preview.png"
```
Ensure models load with valid materials and textures before marking the task complete.

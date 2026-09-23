---
name: core-rts-architecture
description: >-
  Use this skill when developing or debugging RTS camera controls (180° Y-yaw inversion, 4-corner frustum ground clamping),
  minimap screen-to-world mapping, zero-GC physics tick simulation, and state synchronization.
---

# Godot 4 RTS Core Architecture & Math Rules Skill

This skill enforces strict coordinate mappings, frustum boundary checks, and memory management rules for the Godot 4 RTS codebase.

---

## 1. Camera 180° Yaw Inversion & Coordinate Mapping Rules

### Yaw Inversion Rule
The 3D RTS camera (`Camera3D`) has a 180° Y-yaw rotation (`Basis.X = (-1, 0, 0)`):
- **Screen Left** = World `+X`
- **Screen Right** = World `-X`
- **Screen Top** = World `+Z`
- **Screen Bottom** = World `-Z`

### Minimap Coordinate Inversion (`BattleMinimap.cs`)
The minimap must reflect this inversion so dragging and clicking on the minimap aligns with 3D viewport navigation:
```csharp
// Screen-to-world mapping with 180° inversion:
float normalizedX = 0.5f - (worldPosition.X / mapWidth);
float normalizedZ = 0.5f - (worldPosition.Z / mapHeight);
```

---

## 2. Full Viewport 4-Corner Ground Clamping (`RtsCamera.cs`)

When clamping camera position within the terrain bounds, clamping only the camera target center (`GetGroundCenter()`) allows the tilted camera frustum to look past terrain edges into empty skybox void.

`ClampVisibleAreaToMap()` **MUST** clamp all 4 ground intersection corners:
```csharp
if (TryGetViewportGroundPolygon(out Vector3[] corners))
{
    // Clamp min and max X/Z across all 4 ground intersection points
    foreach (var corner in corners)
    {
        // Enforce boundary constraints
    }
}
```

---

## 3. Zero-GC Memory Management & High-Frequency Process Rules

- **Strictly Avoid Allocations in Tick Loops**: Never allocate reference types (`new List<T>()`, `new object()`, closures/lambdas) inside `_Process(double delta)` or `_PhysicsProcess(double delta)`.
- **Preallocated Buffers & Object Pools**: Use static reusable arrays, preallocated buffers, and unit/projectile object pools to maintain 0 B garbage collection spikes during 60 FPS lockstep simulation.
- **Signal Safety**: Cache callable delegate references instead of binding anonymous lambdas on every frame.

---
name: csharp-decompile-restore
description: >-
  Use this skill when decompiling C# assemblies (.dll), analyzing legacy compiled game logic,
  reconstructing combat formulas and unit stat catalogs, and porting legacy Unity C# logic into modern Godot 4 C# code.
---

# C# Assembly Decompilation & Logic Restoration Skill

This skill guides the agent through inspecting, decompiling, cleaning, and reconstructing C# logic from compiled binaries (`.dll`) or legacy decompiled sources for the Godot 4 RTS engine.

## 1. Decompilation Tooling & Workflow

### Tools Available
- **`ilspycmd`**: CLI version of ILSpy (.NET Global Tool).
  ```bash
  # Decompile whole assembly to a target folder
  ilspycmd -p -o ./scratch/decompiled_src ./path/to/Assembly-CSharp.dll

  # Decompile specific type or namespace
  ilspycmd -t Namespace.ClassName ./path/to/Assembly-CSharp.dll
  ```
- **Python AST / Regex Sanitizer**: Clean up decompiler artifacts (e.g. `<>c__DisplayClass`, explicit interface implementations, compiler-generated backing fields).

---

## 2. Unity to Godot 4 C# Translation Patterns

When restoring decompiled Unity C# code into Godot 4 RTS:

| Unity C# Element | Godot 4 C# Equivalent | Notes & Conventions |
| :--- | :--- | :--- |
| `MonoBehaviour` | `Node` / `Node3D` / `CharacterBody3D` | Use `CharacterBody3D` for movable units, `StaticBody3D` for buildings. |
| `Start()` / `Awake()` | `_Ready()` | Call `base._Ready()` if overriding. |
| `Update()` | `_Process(double delta)` | Delta is in seconds (`float` or `double`). |
| `FixedUpdate()` | `_PhysicsProcess(double delta)` | Used for lockstep sync, physics movement, and collision checks. |
| `Vector3` (Left-Handed, +Z forward) | `Vector3` (Right-Handed, -Z forward) | Negate Z coordinates during transform mapping. |
| `Quaternion` | `Quaternion` / `Basis` | Godot uses `Transform3D.Basis` for 3D rotations and orientation. |
| `GameObject.Instantiate(prefab)` | `PackedScene.Instantiate<T>()` | Call `AddChild(instance)` on the appropriate node container. |
| `Destroy(gameObject)` | `QueueFree()` | Never use direct object deletion on engine nodes. |
| `GetComponent<T>()` | `GetNode<T>("Path")` or child query | Cache references in `_Ready()`. |
| `SerializeField` | `[Export]` | Exports fields to Godot Inspector. |

---

## 3. Restoring RTS Core Formulas

When decompiling combat systems, locate and isolate:

1. **Armor & Damage Attenuation**:
   ```csharp
   // Standard RTS damage formula pattern
   public static float CalculateDamage(float rawDamage, DamageType type, ArmorType armor, float defense)
   {
       float multiplier = DamageMatrix.GetMultiplier(type, armor);
       float reduction = defense / (defense + 100f);
       return MathF.Max(1f, (rawDamage * multiplier) * (1f - reduction));
   }
   ```
2. **Deterministic Tick Rates**:
   Ensure state machine timers and cooldowns decrement using fixed step ticks rather than variable delta time for multiplayer lockstep safety.

---

## 4. Verification

Always verify restored C# files compile cleanly in the current project:
```bash
dotnet build GodotRTS.csproj
```
Ensure `0 Errors` and minimal warnings before committing changes.

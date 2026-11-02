# Art pipeline: dropping in real 3D models

The game builds every model from code so it always runs. **Any model can be replaced with real art
one at a time** by putting a prefab in the right folder; no code changes needed.

## Where prefabs go

```
Assets/Resources/Models/
  Locomotives/<locomotive id>.prefab   e.g. lark.prefab, ironbison.prefab, silverarrow.prefab
  Wagons/<wagon id>.prefab             e.g. flatcar.prefab, hopper.prefab, tankcar.prefab
  Buildings/<building id>.prefab       e.g. saloon.prefab, sawmill.prefab, grandhotel.prefab
```

The ids are in `Assets/Scripts/Core/GameDatabase.cs`. Missing prefabs simply fall back to the
procedural model.

## Conventions

| | Locomotives & wagons | Buildings |
|---|---|---|
| **Scale** | 1 unit = 1 meter. Track gauge 1.5 m, rail top at y = 0 | 1 unit = 1 meter. Must fit a **12 × 12 m** lot |
| **Facing** | Front of the vehicle points **+Z** | Front door faces **−Z** (toward the street) |
| **Pivot** | Anywhere; the game centers it on the model's bounds | Center of the footprint, on the ground |
| **Length** | Taken from the model's bounds (Z size) | – |
| **Smoke** | Child named `Smoke…` at the stack or exhaust | Children named `Smoke…` at each chimney |
| **Spinning parts** | Children named `Wheel…` spin with speed; pivot on the axle, axle along X | Children named `Spin…` turn while the factory works (around local Z) |
| **Cargo** | Optional child named `Cargo…`; shown only when loaded | – |
| **Colliders** | Removed automatically; the game adds its own | Same |

## Materials

- The project uses **URP**. Use URP materials, `Universal Render Pipeline/Lit` or Simple Lit. If a
  pack ships built-in materials, run **Edit → Rendering → Materials → Convert…**.
- For mobile, keep textures at 1024–2048 px, use texture atlases where packs provide them, and
  aim for about 5–25k triangles per locomotive and 2–15k per building.
- Enable **GPU Instancing** on materials used by many objects (trees, crates).

## Mixing packs

Pick packs that share one art style (all stylized low-poly, or all semi-realistic). Mixed styles
look cheap faster than low polygon counts do. When in doubt, match the colors of the procedural
palette (`Palette` in `Materials.cs`): warm sandstone, sage, pine and brass.

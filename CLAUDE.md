# Ironhorse Frontier: working notes for coding agents

Unity **6000.0.85f1**, URP 17. The project is the repo root. Read `README.md` and `docs/` for the
architecture. The simulation (`Assets/Scripts/Core`) has no Unity dependency; everything visual is
built from code at runtime (`Assets/Scripts/Game`), so `Assets/Scenes/Main.unity` is empty.

## Verify every change in the real Editor

The `unity` CLI drives a running Editor through the **Pipeline** package. That package is a
local dev tool: it is **not** in the committed `Packages/manifest.json`.

```bash
unity pipeline install --project-path .     # once per clone (adds com.unity.pipeline locally)
unity open . --until-ready                  # first open imports for a few minutes
Tools/unity-recompile.sh                    # leave Play mode, refresh, recompile, list errors (exit 1 on errors)
unity command --project-path . test run editor --timeout 600 --result-only   # 62 EditMode tests
Tools/unity-capture.sh out.png 6 'Ironhorse.Game.UIRoot.Instance.OpenDepot("locos"); return 0;'
```

`unity-capture.sh <png> [warmup-seconds] [C# to eval first]` enters Play mode, waits, optionally runs
C# (open a window, move the camera with `CameraRig.Instance.FlyTo(pos, distance)`, set
`DayNightCycle.Instance.Phase`), saves an iPhone-aspect screenshot and prints Console errors.
Look at the screenshot before calling a visual change done.

The simulation tests also run without Unity: `dotnet test Tools/CoreTests` (needs the .NET 8 SDK;
install it with `dotnet-install.sh --channel 8.0` if `dotnet --list-sdks` lacks 8.x).

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`, and don't commit
  `Packages/packages-lock.json` or `ProjectSettings/Packages/com.unity.pipeline/`.
- Recompile with 0 errors, 62/62 tests passing, Play mode with 0 Console errors.
- Commit `.meta` files with their assets.

## Gotchas

- `GetComponent<T>() ?? AddComponent<T>()` never adds: Unity's missing component is a "fake null".
- Unfocused Editors stop ticking; the capture script turns on auto-tick and Run In Background.
- The Game view must be maximized for full-resolution captures (the script does it), and async
  shader compilation is turned off so captures don't show cyan placeholder shaders.
- After a recompile the Pipeline server is briefly down during the domain reload; the scripts wait
  for `unity status --until-ready`.
- Shaders: sample inside dynamic branches with `SAMPLE_TEXTURE2D_ARRAY_GRAD` and derivatives taken
  outside the branch.
- Labels using the bundled bold faces (`UIKit.Display`, `Heading`) must use `FontStyle.Normal`, or
  Unity synthesizes a smeared extra bold.

## Art

- `Tools/art/pack_textures.py` rebuilds `Assets/Resources/Textures/*.png` from CC0 ambientCG
  materials; `Assets/Editor/ArtImport.cs` imports them as Texture2DArrays. Layer order must match
  `SurfaceKind` (`Materials.cs`) and the terrain layer list in `Terrain.shader` / `Terrain.cs`.
- `MeshBuilder` picks a surface detail layer from the palette color (`Surfaces.Classify`); set
  `mb.Surf` to override it.
- Drop-in 3D models: `docs/ART_PIPELINE.md`. Fonts are Barlow (OFL) in `Assets/Resources/Fonts`.

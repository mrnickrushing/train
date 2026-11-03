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
unity command --project-path . test run editor --timeout 600 --result-only   # 85 EditMode tests
Tools/unity-capture.sh out.png 6 'Ironhorse.Game.UIRoot.Instance.OpenDepot("locos"); return 0;'
```

`unity-capture.sh <png> [warmup-seconds] [C# to eval first]` enters Play mode, waits, optionally runs
C# (open a window, move the camera with `CameraRig.Instance.FlyTo(pos, distance)`, set
`DayNightCycle.Instance.Phase`), saves an iPhone-aspect screenshot and prints Console errors.
Look at the screenshot before calling a visual change done.

The simulation tests also run without Unity: `dotnet test Tools/CoreTests` (needs the .NET 8 SDK;
install it with `dotnet-install.sh --channel 8.0` if `dotnet --list-sdks` lacks 8.x).

## iPhone builds (TestFlight)

`Tools/ios-export.sh` exports the Xcode project from the running Editor (iOS Build Support module,
local Unity Personal license) and uploads it as the `ios-latest` GitHub release asset; then start the
Codemagic workflow `ios-testflight` (app **train**), which signs with the App Store Connect API key in
the Codemagic variable group `ios_credentials` and uploads to TestFlight. See README → "Building for iPhone with Codemagic".

## Before committing

- Keep `com.unity.pipeline` out of `Packages/manifest.json`, and don't commit
  `Packages/packages-lock.json` or `ProjectSettings/Packages/com.unity.pipeline/`.
- Recompile with 0 errors, all EditMode tests passing, Play mode with 0 Console errors.
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

- **AI models** (every locomotive, wagon, building, the station and freight shed):
  `Tools/art/ai_assets.py` holds the original prompts and runs Higgsfield (GPT Image 2.5 concept ->
  Tripo H3.1 3D) then `Tools/blender/import_ai.py` (orient, scale to game size, decimate, pack PBR maps).
  Raw files live in `art-src/` (gitignored). Paid steps skip ids that already have output; review a
  concept image before paying for its 3D model. Check facing **in Unity**, not Blender: Unity reads the exporter's +Y forward as -Z, so
  `import_ai.py` turns every model 180° before export (`turn_fbx.py` fixes older files). Verify with
  a Play-mode capture or by checking that a locomotive's Smoke anchor has positive Z.
  Budgets: locomotives 40k tris / 2K maps, wagons 15k / 1K, buildings 20k / 1K.
  `import` welds the mesh before decimating (otherwise it cracks) and then runs
  `Tools/blender/rebake.py`, which gives the low-poly mesh fresh UVs and bakes colour + normals from
  the full-detail GLB (decimation wrecks the original UVs: patchy colours, white specks). Models get
  one even finish (no per-patch metal maps). `A[id]["hero"]` (the station) gets 40k tris and 2K maps.
  `Tools/blender/trim_base.py` cuts the square dirt slab Tripo puts under buildings so they stand on
  the terrain (run it after re-importing a town building; the station, freight shed, fountain and
  garden keep their bases). `Tools/blender/lod.py` writes `<id>_far.fbx` (same textures) for models
  drawn in bulk: parked yard wagons go through `Vegetation.AddModel` (GPU instanced, near/far).
- **Horses**: `A["horse"]` (animal=True) is imported like a vehicle, then `Tools/blender/horse.py` rigs it
  (four-leg armature, weights from the body plan) and authors a four-beat Walk loop into
  `Resources/Models/Animals/` (Legacy clips, like People). `Life` hitches a team of two to the
  `Props/covered_wagon` model.
- **Townsfolk**: rigged AI characters (Higgsfield Multi-Image to 3D, rigging on) turned into Walk/Idle
  loops by `Tools/blender/townsfolk.py`; check strides with `walk_strip.py`. They snap to whatever is
  underfoot via colliders on layer 2 (`Station.Walkable`).
- **Key art and cinematics**: `Tools/art/ai_media.py` (stills + Seedance clips) then `install`, which
  writes `Assets/StreamingAssets/Videos/*.mp4`, fallback stills in `Resources/UI/Cinematics`, the
  loading art, and VP8 copies in `Library/EditorVideos` because the Linux Editor can't decode H.264.
  `Celebrations` plays them (contract, level up, new locomotive, project, golden age).
- Higgsfield credits are the user's money: check `higgsfield generate cost` first and never retry a
  paid job without checking `higgsfield account transactions` for a charge.

- **UI kit**: `Tools/art/ui_kit.py` draws the shaded UI sprites (glossy buttons with a 3D lip, framed
  window, cards, hero panels, pills, ribbons, round brass-rimmed buttons) into `Resources/UI/Kit`, free.
  `UIKit.Kit(name)` 9-slices them (borders in `UIKit.KitBorder`); `UIKit.Btn` picks the button art from
  the colour (`KitFor`), and `UIKit.Stroke` gives text the thick game-style outline. Destination postcards
  (`ai_media.py postcard`) live in `Resources/UI/Destinations/<id>.jpg`; HUD icons are `nav_*` in Icons.
- **Boards**: the main screens (dispatch, fleet, contracts, build, depot, warehouse) are fixed-layout `ShowBoard`
  windows (`UIRoot.Boards.cs` widgets, `UIRoot.Screens.cs`; one building in `UIRoot.Building.cs`; foreman, territory,
  ledger, settings and land office in `UIRoot.Pages.cs`). The UI renders in linear colour space, so a dim overlay
  needs a high alpha (about 0.9) to look about two-thirds dark; `Tools/art/ui_cutouts.py` (free) cuts the white
  backdrop off the concept renders into `Resources/UI/{Locos,Wagons,Buildings}` for their art. `UIRoot.Coach.cs` is
  Hank's first-launch tutorial (goals g1-g5, `settings.tutorialDone`).
- **Project sites**: `World/ProjectSites.cs` builds territory projects in the valley from ordered pieces (trestle
  cut-off across the river gorge, Fort Calder telegraph through the southern badlands; `ProjectSites.More.cs`: the Deep
  Shaft by the west mesa, the resupply depot north of the yard, Silver Lake Dam above the lake, the Golden Spike plaza); the share of goods delivered
  decides how many stand, new ones rise when the player is back on the map. `ProjectSites.Blocks` keeps scenery off them.
  Project cards show unfinished projects as a blueprint filled in colour by `ProjectSites.Built`.
- **Daily hooks**: `Core/GameSession.Hooks.cs` (tuning and content in `Core/Hooks.cs`) runs the payday streak (7-day calendar,
  local midnight via `UtcOffsetSeconds`, day 7 pays blueprints), the away summary (`Away`, `CollectAll`), mystery crates
  (rolled at dispatch, opened from a marker on the parked train), express telegrams (a `ContractState` with `express`, triple
  pay, two hours, outside the contract slots), map events (traveler, tumbleweed, gold strike, bandits; none during the
  tutorial) and the Roundhouse album (sets of owned stock give permanent perks through `CivicBonus`). Screens are in
  `UI/UIRoot.Hooks.cs`; launch queues the away card, then payday, then any featured offer (`QueueWelcome`).
- **Audio**: recorded music, day/night ambience beds and effects (Higgsfield Sonilo, Mirelo, Seed Audio; raw takes in
  `art-src/audio`) are trimmed, levelled and looped by `Tools/art/audio.py` (free, ffmpeg) into `Resources/Audio/*.ogg`.
  `AudioManager` loads them and falls back to its synthesized sounds for any that are missing. Seed Audio's quoted
  cost is 0.1 but it charges about 1 credit a take: check `higgsfield account transactions`.
- `Tools/art/pack_textures.py` rebuilds `Assets/Resources/Textures/*.png` from CC0 ambientCG
  materials; `Assets/Editor/ArtImport.cs` imports them as Texture2DArrays. Layer order must match
  `SurfaceKind` (`Materials.cs`) and the terrain layer list in `Terrain.shader` / `Terrain.cs`.
- `MeshBuilder` picks a surface detail layer from the palette color (`Surfaces.Classify`); set
  `mb.Surf` to override it.
- Drop-in 3D models: `docs/ART_PIPELINE.md`. Fonts are Barlow (OFL) in `Assets/Resources/Fonts`.

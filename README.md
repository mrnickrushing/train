# Ironhorse Frontier

An original 3D railroad-tycoon game for iPhone and iPad, built in **Unity 6**.
Grow a dusty railhead town in the Sundown Territory into a Golden Age rail empire. You send
locomotives out to frontier outposts for timber, coal, cattle and gold, turn raw goods into
steel and tools, fill orders on the contract board, and build out your town.

> Status: **playable prototype of the core loop.** Every model, sound and UI element is generated
> from code, so the game runs as soon as you open it with no art assets. The art pipeline is built
> so hand-made models, music and UI art can replace the procedural versions one piece at a time.
> See [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md).

## What's in the box

| Area | What you get |
|---|---|
| **World** | Procedural valley (prairie to the north, desert to the south, red-rock mountains on the rim), a river and lake, layered sandstone mesas with stone tunnel portals, pines, cottonwoods, saguaros, sagebrush, rocks, telegraph lines, windmills with spinning wheels, drifting clouds |
| **Station** | Six-platform depot with clock tower, island platforms with canopies, yard throats merging into the main line, water cranes, coal stage, freight shed, lamps that glow at night |
| **Trains** | 8 locomotive types: a saddle-tank switcher, 4-4-0, 2-6-0, 4-6-0, 2-8-2, an art-deco streamlined 4-6-4 and two streamlined diesels. Spoked drivers spin and side rods animate; smoke scales with speed; trains leave through the east tunnel and return through the west. 10 wagon types show visible loads: log stacks, ore heaps, cattle |
| **Town** | 22 building types on 36 lots: cabins to a grand hotel, a sawmill with a spinning blade, a smelter with a tall stack, a windmill grist mill, refinery, water tower, saloon, schoolhouse, bank, clock tower, gardens, fountain. Scaffolding and build-up animation, chimney smoke, production animations |
| **Systems** | Dispatch, cargo classes, warehouse limits, production chains, rent, civic bonuses, contracts, a goal chain, XP/levels with unlocks, land purchase, locomotive and building upgrades, rush timers with gold nuggets, offline progress, autosave |
| **Graphics** | URP renderer with bloom, ambient occlusion, warm color grading, vignette and tilt-shift depth of field (built-in renderer kept as a fallback); a high map-like camera; a freight yard with parked wagon strings and gantry cranes; a roundhouse with a working turntable; townsfolk and horse-drawn wagons; drop-in slots for real 3D models (see [docs/ART_PIPELINE.md](docs/ART_PIPELINE.md) and [docs/ART_SHOPPING_LIST.md](docs/ART_SHOPPING_LIST.md)) |
| **Atmosphere** | Day/night cycle with golden hour, stars, glowing windows and a sky-reflection probe; soft shadows; animated water; synthesized audio (chuffs, whistles, coins, birds, crickets) and a plucked-guitar music loop |
| **Depth** | Crew-run factories with conveyor queues (buy slots and hire hands), 6 multi-stage territory projects with permanent bonuses, passenger service to 3 towns, the Foreman's endless task sets, workshop parts for overhauls, and blueprints to assemble top-tier locomotives |
| **Monetization** | Coal & Water fuel, gold-nugget packs, starter pack, weekly featured bundles, a permanent fuel-tank upgrade, a 30-tier Rail Season Pass, rewarded ads, and RevenueCat-ready purchases. See [docs/MONETIZATION.md](docs/MONETIZATION.md) |
| **UI** | Full-screen sheets with big condensed titles, chunky 3D buttons, ribbon banners, job-card grids with notepad delivery slips, "Rewards" rows, a locomotive depot card grid, offer seals, a foreman with a speech bubble; wood-and-brass frontier palette; safe-area aware for notched iPhones |

## Opening the project (Windows or Mac)

1. Install **Unity Hub**, then **Unity 6 (6000.0 LTS)** with the **iOS Build Support** module.
   (iOS Build Support works on Windows: you can export the Xcode project there, you just can't
   compile it without a Mac. That's what Codemagic is for.)
2. In Unity Hub choose **Add → Add project from disk** and pick this folder.
   If Hub asks to change the editor version, any 6000.0.x works.
3. On first open, the `Ironhorse → Set Up Project` step runs automatically. It creates
   `Assets/Scenes/Main.unity`, adds it to Build Settings, sets iOS player settings, configures
   **URP graphics** (`Assets/Settings/IronhorseURP.asset`) and generates a placeholder app icon.
   If the graphics look flat (no bloom or soft shadows), run **Ironhorse → Set Up URP Graphics** once.
4. Open `Assets/Scenes/Main.unity` and press **Play**. The whole world builds itself.

Controls in the editor: left-drag to pan, right-drag to rotate, scroll to zoom, click to select.
On a phone: drag, pinch, two-finger twist, tap.

Developer tools (debug builds only) are under **••• → Developer tools**: coins, resources, levels,
and skipping time.

## Building for iPhone with Codemagic

`codemagic.yaml` defines the `ios-testflight` workflow. It installs Unity on a cloud Mac, runs the
tests, exports the Xcode project via `Ironhorse.EditorTools.BuildScript.BuildIos`, signs it and
uploads it to **TestFlight**.

One-time setup (the app is already added in Codemagic as **train**, in the same team as the other apps):

1. **App Store Connect API key:** the workflow reads `APP_STORE_CONNECT_PRIVATE_KEY`,
   `APP_STORE_CONNECT_KEY_IDENTIFIER` and `APP_STORE_CONNECT_ISSUER_ID` from the team variable group
   `ios_credentials` (shared with the other apps). Xcode uses the key to register the bundle id
   `com.rushingtech.ironhorsefrontier` and create the certificate and profile itself.
2. **Unity:** create a variable group `unity_credentials` with `UNITY_EMAIL` and `UNITY_PASSWORD`
   (secure), plus `UNITY_SERIAL` if you have Unity Plus/Pro. Without a serial the build signs in with
   your Unity ID (Unity Personal).
3. **App Store Connect:** create the app record (My Apps → + → New App) with that bundle id, so
   builds can be uploaded to TestFlight. The signed `.ipa` is kept as a build artifact either way.
4. Start the `ios-testflight` workflow (it also runs on every push to `main`). When it finishes,
   install from TestFlight on your iPhone.

## Tests

The simulation (`Assets/Scripts/Core`) is plain C# with no Unity dependency, so it's fully
unit-tested, including a bot that plays the economy to level 5 to catch dead ends:

```bash
dotnet test Tools/CoreTests        # anywhere with the .NET 8 SDK
```

The same tests appear in Unity's **Window → General → Test Runner → EditMode**, and GitHub
Actions runs them on every push.

## Project layout

```
Assets/
  Scripts/Core/      Simulation: content database, balance, state, GameSession (no Unity refs)
  Scripts/Game/
    App/             GameRoot bootstrap, save system, clock, quality, clouds
    World/           Mesh builder, terrain, track, scenery, station, effects, day/night, camera
    Trains/          Locomotive/wagon model factory, train views, ambient excursion train
    Town/            Building model factory (22 styles), town & plot views
    UI/              UI kit, HUD, world markers, all windows
    Audio/           Synthesized SFX + music
  Resources/Shaders/ VertexLit (world), Water, Smoke, Sky
  Editor/            Project setup, iOS build script, placeholder icon
  Tests/EditMode/    NUnit tests for the simulation
Tools/CoreTests/     Runs the same tests with `dotnet test`
docs/                Game design document & roadmap
codemagic.yaml       iOS CI/CD → TestFlight
```

## Originality

Ironhorse Frontier is an original game in the railroad-tycoon genre. Its setting, characters,
locations, content, numbers and visual design are its own.

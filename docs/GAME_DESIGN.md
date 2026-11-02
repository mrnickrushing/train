# Ironhorse Frontier: Game Design Document

## Pitch

It's 1869 in the Sundown Territory. Your railhead town, **Dry Creek**, is one dusty platform and
two log cabins. Send your trains out to logging camps, mines, ranches and goldfields, haul the
riches home, build a town worth living in, and carry your railroad from the **Frontier era** of
diamond-stack steamers into the **Golden Age** of chrome streamliners.

**Pillars**

1. **A living diorama.** The home valley is always moving: trains roll in and out, chimneys smoke,
   mills spin, the sun sets over the mesas. Looking at your railroad should feel rewarding by itself.
2. **Short, satisfying loops.** Every session has quick wins (collect, dispatch, deliver) and a
   longer goal (the next locomotive, the next building, the next era).
3. **Readable depth.** Cargo classes, production chains and civic bonuses give players real
   decisions without spreadsheets.

## Core loop

```
 Dispatch trains ──► trains travel (real time) ──► unload raw goods into the warehouse
       ▲                                                   │
       │                                                   ▼
 Buy/upgrade locos & wagons ◄── coins & XP ◄── contracts / rent ◄── factories turn raw goods into products
```

| Loop | Length | Player action |
|---|---|---|
| Micro | 1–5 min | Tap arrival bubbles, re-dispatch, start production |
| Session | 10–20 min | Fill contracts, build or upgrade a building, buy a wagon |
| Meta | days | Level up, unlock outposts and locomotives, reach the Golden Age, own all 36 lots |

## Systems (as implemented)

### Trains
- **Locomotives** have rarity, era, *power* (max wagons) and *speed*. Trip time =
  `destination base time × 40 / effective speed`.
- **Wagons** belong to a **cargo class** (Flat, Bulk, Box, Livestock, Coach, Tank). Only
  matching wagons load at an outpost, so the player has to plan each train's consist.
- **Upgrades** (to level 10): +12% cargo and +4% speed per level, paid in coins + planks, then steel, then tools.
- **Platforms** cap the fleet at 6 trains.
- **Rush**: finish any timer for gold nuggets (≈1 per 90 s remaining).

### Outposts (destinations)
9 outposts unlock with level, each producing one raw resource: Pinecrest (timber), Black Hollow
(coal), Willow Flats (grain), Rattlesnake Ranch (cattle), Ironback Ridge (iron ore), Fort Calder
(mail), Coppertop (copper ore), Eldorado Gulch (gold ore), Black Gold Fields (crude oil).

### Town
- **36 lots**, 10 owned at start; more are surveyed with coins as you level.
- **Residential** buildings add population, which pays rent into storage (2 h cap, raised by the Bank).
- **Production** buildings run recipes in batches (more batches and speed per level):
  Sawmill (timber → planks), Grist Mill, Packing House, Smelter (ore + coal → steel), Tool Works,
  Wire Works, Assay Office, Refinery.
- **Civic** buildings give global bonuses: train speed, income, contract slots, XP, rent storage, cargo yield.
- **Decorations** add small income bonuses.
- Construction takes real time, shown with scaffolding.

### Contracts
3 slots (+1 from the Telegraph Office). Orders ask for 1–2 resources the player can currently
obtain; rewards scale with the goods' value, and 15% of orders also pay nuggets. Discarded or
completed orders are replaced on a timer.

### Progression
- XP comes from cargo, production, construction, upgrades and contracts. The curve is `120 × level^1.55`.
- Level-ups grant coins and nuggets and unlock content; the level-up popup lists what's new.
- A **goal chain** (15 goals) teaches the game and steers the early hours.
- **Golden Age** at level 15 brings new locomotives, wagons and buildings, and the ambient
  excursion train switches to Pullman coaches.

### Economy guards
- The warehouse limit forces trade-offs (upgrade it, sell surplus at 60%, or turn goods into products).
- An automated bot test makes sure a new player can always reach level 5 without getting stuck.

## Art direction

- **Look:** stylized, saturated, toy-like realism. Flat-shaded low-poly forms with clean
  silhouettes, warm late-afternoon light, crisp soft shadows.
- **Palette:** red sandstone, sage, wheat gold, pine green, weathered wood, brass, enamel paint.
- **Frontier → Golden Age:** clapboard and logs give way to brick, stone, chrome and art deco.
- **UI:** dark walnut frames, parchment cards, brass accents, slab-serif headings.

## Technical architecture

- **Simulation** (`Ironhorse.Core`): deterministic, engine-free C#, timestamp-based (offline
  progress for free), event-driven (`Changed`, `TrainArrived`, `LeveledUp`…), fully unit-tested.
- **Presentation** (`Ironhorse.Game`): reads state, raises actions, never owns game rules.
  Visual state comes straight from timestamps, so it can't drift from the simulation.
- **Rendering:** built-in pipeline, one vertex-colored surface shader for nearly all geometry,
  so the whole world batches into few draw calls (iPhone friendly). Separate water, smoke and sky shaders.
- **Saves:** JSON in `persistentDataPath`, written atomically with a `.bak` fallback, autosaved
  every 20 s and when the app is backgrounded.

## Roadmap: toward a full "1 GB" release

The prototype proves the systems and the feel. A shippable, premium-looking game mostly needs
**content and art production** from here:

1. **Art pass.** Replace procedural meshes with authored models (Blender, or Asset Store packs),
   one category at a time; `BuildingFactory`/`TrainFactory` are the seams. Add PBR textures and
   LODs. Consider moving to **URP** for post-processing (bloom, color grading, SSAO) and
   Shader Graph water.
2. **Audio.** Recorded locomotive sounds per class, ambience beds, a licensed or composed soundtrack.
3. **More content.** 40+ locomotives with collectible rarities and parts, a second region (the
   Golden Age coast), passenger services, special events.
4. **Social.** Guilds or co-op rail companies with shared goals and seasonal leaderboards
   (needs a backend; the save model already serializes cleanly).
5. **Live ops.** Remote-configurable `GameDatabase` and `Balance`, analytics, cloud saves (iCloud/Game Center).
6. **Monetization (optional).** Cosmetic liveries and nugget packs via StoreKit/RevenueCat, kept fair.
7. **Polish.** Tutorial character, haptics, accessibility (text scaling, colorblind-safe cargo icons),
   localization.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>
    /// All static game content: resources, rolling stock, destinations, buildings and goals.
    /// Content is data so it can later be moved to JSON / remote config without touching logic.
    /// </summary>
    public static class GameDatabase
    {
        public static readonly List<ResourceDef> Resources = new List<ResourceDef>
        {
            // Raw — hauled in from outposts
            R("timber",     "Timber",       CargoClass.Flat,      3,  false, 0x8B5A2B, "T"),
            R("coal",       "Coal",         CargoClass.Bulk,      4,  false, 0x2B2B2E, "C"),
            R("grain",      "Grain",        CargoClass.Box,       4,  false, 0xE3C565, "G"),
            R("cattle",     "Cattle",       CargoClass.Livestock, 6,  false, 0x9C5B3B, "Ca"),
            R("iron_ore",   "Iron Ore",     CargoClass.Bulk,      5,  false, 0x8E4B3A, "Fe"),
            R("mail",       "Mail",         CargoClass.Coach,     5,  false, 0xF2EEDC, "M"),
            R("copper_ore", "Copper Ore",   CargoClass.Bulk,      7,  false, 0xC8743A, "Cu"),
            R("gold_ore",   "Gold Ore",     CargoClass.Bulk,      12, false, 0xE6B422, "Au"),
            R("oil",        "Crude Oil",    CargoClass.Tank,      9,  false, 0x1A1410, "O"),
            // Manufactured — made in town
            R("planks",     "Planks",       CargoClass.Flat,      9,  true,  0xD9A066, "P"),
            R("flour",      "Flour",        CargoClass.Box,       11, true,  0xF5F0E1, "Fl"),
            R("provisions", "Provisions",   CargoClass.Box,       16, true,  0xB5523B, "Pr"),
            R("steel",      "Steel",        CargoClass.Flat,      20, true,  0x9AA5B1, "St"),
            R("tools",      "Tools",        CargoClass.Box,       38, true,  0x5E6B78, "To"),
            R("copper_wire","Copper Wire",  CargoClass.Box,       24, true,  0xD98B4A, "W"),
            R("gold_bars",  "Gold Bars",    CargoClass.Box,       60, true,  0xFFD34D, "GB"),
            R("fuel",       "Fuel",         CargoClass.Tank,      28, true,  0x6B4F2A, "Fu"),
            // Passengers ride coaches to towns and pay fares on arrival.
            Pax(),
            // Workshop parts for locomotive overhauls.
            Part("rivets",   "Rivets",          0x8A8F96, "Ri"),
            Part("bearings", "Bearings",        0xC0C6CC, "Be"),
            Part("rods",     "Drive Rods",      0x5B6470, "Ro"),
            Part("gauges",   "Pressure Gauges", 0xD4A64A, "Ga"),
        };

        public static readonly List<LocomotiveDef> Locomotives = new List<LocomotiveDef>
        {
            L("dusty",       "Dusty 0-4-0 Switcher",  Era.Frontier,  Rarity.Common,    LocoStyle.SteamSwitcher,     2, 30, 1,  0,     0,   0x3B5D3A, 0xC9A227, "A stubborn little yard engine. Every empire starts somewhere."),
            L("lark",        "Prairie Lark 4-4-0",    Era.Frontier,  Rarity.Common,    LocoStyle.SteamAmerican,     3, 40, 2,  1500,  0,   0x7A1F1F, 0xD4AF37, "Brass-trimmed and quick across open grassland."),
            L("redmesa",     "Red Mesa 2-6-0",        Era.Frontier,  Rarity.Uncommon,  LocoStyle.SteamMogul,        4, 42, 5,  4200,  0,   0x2F3E59, 0xB87333, "Six drivers for the long grades up to the mesas."),
            L("copperqueen", "Copper Queen 4-6-0",    Era.Frontier,  Rarity.Rare,      LocoStyle.SteamTenWheeler,   5, 52, 9,  9500,  0,   0x14532D, 0xE0A458, "Pride of the mining districts, polished every dawn."),
            L("ironbison",   "Iron Bison 2-8-2",      Era.Frontier,  Rarity.Rare,      LocoStyle.SteamHeavy,        7, 46, 12, 17000, 0,   0x1C1C1C, 0xA33B20, "Hauls a mountain of ore without breaking a sweat."),
            L("sundown",     "Sundown Express 4-6-4", Era.GoldenAge, Rarity.Epic,      LocoStyle.SteamStreamlined,  6, 72, 16, 0,     180, 0x1F3A68, 0xE8C26A, "Shrouded steam, art-deco lines, chasing the sunset."),
            L("silverarrow", "Silver Arrow",          Era.GoldenAge, Rarity.Epic,      LocoStyle.DieselStreamliner, 8, 82, 18, 42000, 0,   0xC0C6CC, 0xB22222, "Stainless steel and a twin-prime-mover heart."),
            L("thunderhead", "Thunderhead Twin",      Era.GoldenAge, Rarity.Legendary, LocoStyle.DieselTwin,       10, 78, 22, 0,     420, 0x5B2A86, 0xF2C14E, "Two units, one roar. The territory's mightiest hauler."),
        };

        static GameDatabase()
        {
            Locomotive("sundown").Blueprints = 12;
            Locomotive("silverarrow").Blueprints = 14;
            Locomotive("thunderhead").Blueprints = 20;
        }

        public static readonly List<WagonDef> Wagons = new List<WagonDef>
        {
            W("flatcar",      "Flatcar",          CargoClass.Flat,      10, 1,  200,  0x6B4226),
            W("hopper",       "Ore Hopper",       CargoClass.Bulk,      10, 1,  220,  0x3A3A3A),
            W("boxcar",       "Boxcar",           CargoClass.Box,       10, 2,  260,  0x8C2F1B),
            W("stockcar",     "Stock Car",        CargoClass.Livestock, 8,  3,  320,  0xA0522D),
            W("mailcoach",    "Mail Coach",       CargoClass.Coach,     8,  6,  480,  0x1F4E3D),
            W("longflat",     "Long Flatcar",     CargoClass.Flat,      16, 8,  900,  0x5A3A1E),
            W("steelhopper",  "Steel Hopper",     CargoClass.Bulk,      16, 9,  1000, 0x55606B),
            W("reefer",       "Insulated Boxcar", CargoClass.Box,       16, 10, 1100, 0xE8E2D0),
            W("tankcar",      "Tank Car",         CargoClass.Tank,      12, 15, 1400, 0x202020),
            W("pullman",      "Pullman Coach",    CargoClass.Coach,     14, 16, 1800, 0x2E2A5A),
        };

        public static readonly List<DestinationDef> Destinations = new List<DestinationDef>
        {
            D("pinecrest",   "Pinecrest Logging Camp", "timber",     1,  90,   1.0f, "Sawdust and pine sap on the high ridges.",           0.18f, 0.72f),
            D("blackhollow", "Black Hollow Mine",      "coal",       1,  120,  1.0f, "A dark seam beneath the badlands.",                   0.30f, 0.40f),
            D("willowflats", "Willow Flats Farms",     "grain",      2,  150,  1.0f, "Golden wheat as far as the eye can see.",             0.52f, 0.78f),
            D("rattlesnake", "Rattlesnake Ranch",      "cattle",     3,  240,  1.0f, "Longhorns, dust and a cook who never sleeps.",        0.70f, 0.62f),
            D("ironback",    "Ironback Ridge",         "iron_ore",   4,  300,  1.0f, "Red rock streaked with rich iron ore.",               0.40f, 0.18f),
            D("fortcalder",  "Fort Calder",            "mail",       6,  420,  1.1f, "Cavalry outpost hungry for news from back east.",     0.84f, 0.30f),
            D("coppertop",   "Coppertop Diggings",     "copper_ore", 8,  600,  1.0f, "Green-stained hills and a boomtown of tents.",        0.62f, 0.12f),
            D("eldorado",    "Eldorado Gulch",         "gold_ore",   12, 1200, 0.9f, "Where fortunes are made... and lost.",                0.12f, 0.20f),
            D("blackgold",   "Black Gold Fields",      "oil",        15, 1500, 1.0f, "Derricks rising where the buffalo roamed.",           0.90f, 0.82f),
            // Passenger towns
            // The scenic loop north of town: sightseers ride a few laps and pay their fares back at the station.
            D("excursion",     "Scenic Excursion",     "passengers", 6,  210,  1.25f, "Sightseers ride the loop past the meadows, the river and the mesas.", 0.47f, 0.58f),
            D("silversprings", "Silver Springs",       "passengers", 6,  360,  1.0f, "A spa town where folks take the waters.",            0.30f, 0.90f),
            D("prosperity",    "Prosperity City",      "passengers", 11, 720,  1.3f, "Brick, bustle and a brass band at every arrival.",   0.75f, 0.92f),
            D("capitaljct",    "Capital Junction",     "passengers", 16, 1080, 1.7f, "The territorial capital. Everyone wants a ticket.",  0.95f, 0.50f),
        };

        public static readonly List<BuildingDef> Buildings = new List<BuildingDef>
        {
            // Residential
            B("cabin",       "Settler's Cabin",   BuildingCategory.Residential, BuildingStyle.Cabin,          1,  300,   C(("timber", 10)), pop: 6,  max: 8),
            B("clapboard",   "Clapboard House",   BuildingCategory.Residential, BuildingStyle.ClapboardHouse, 3,  900,   C(("planks", 15)), pop: 14, max: 8),
            B("boarding",    "Boarding House",    BuildingCategory.Residential, BuildingStyle.BoardingHouse,  6,  2600,  C(("planks", 30), ("steel", 6)), pop: 28, max: 6),
            B("rowhouse",    "Brick Rowhouse",    BuildingCategory.Residential, BuildingStyle.BrickRowhouse,  10, 6500,  C(("steel", 20), ("tools", 4)), pop: 48, max: 6, era: Era.GoldenAge),
            B("grandhotel",  "Grand Hotel",       BuildingCategory.Residential, BuildingStyle.GrandHotel,     16, 18000, C(("steel", 40), ("copper_wire", 20), ("tools", 10)), pop: 110, max: 2, era: Era.GoldenAge),
            // Production
            P("sawmill",     "Sawmill",           BuildingStyle.Sawmill,     1,  500,   C(("timber", 15)),                  In(("timber", 6)),                     Out("planks", 3),      45),
            P("gristmill",   "Grist Mill",        BuildingStyle.GristMill,   2,  900,   C(("timber", 20), ("planks", 5)),   In(("grain", 6)),                      Out("flour", 3),       60),
            P("packing",     "Packing House",     BuildingStyle.PackingHouse,3,  1400,  C(("planks", 20)),                  In(("cattle", 4), ("grain", 2)),       Out("provisions", 3),  90),
            P("smelter",     "Smelter",           BuildingStyle.Smelter,     4,  2200,  C(("planks", 25), ("coal", 20)),    In(("iron_ore", 6), ("coal", 4)),      Out("steel", 3),       120),
            P("toolworks",   "Tool Works",        BuildingStyle.ToolWorks,   7,  4800,  C(("planks", 30), ("steel", 15)),   In(("steel", 3), ("planks", 3)),       Out("tools", 2),       180),
            P("wireworks",   "Wire Works",        BuildingStyle.WireWorks,   8,  6200,  C(("steel", 20), ("tools", 4)),     In(("copper_ore", 6)),                 Out("copper_wire", 3), 200),
            P("assay",       "Assay Office",      BuildingStyle.AssayOffice, 12, 12000, C(("steel", 30), ("tools", 10)),    In(("gold_ore", 4), ("coal", 4)),      Out("gold_bars", 2),   300),
            P("refinery",    "Refinery",          BuildingStyle.Refinery,    15, 20000, C(("steel", 50), ("copper_wire", 15)), In(("oil", 6)),                     Out("fuel", 3),        360, Era.GoldenAge),
            // Civic
            V("watertower",  "Water Tower",       BuildingStyle.WaterTower,      2,  800,   C(("timber", 25)),                CivicEffect.TrainSpeed,   0.05f, "Topped-up tenders: +5% train speed."),
            V("saloon",      "Saloon",            BuildingStyle.Saloon,          3,  1500,  C(("planks", 20)),                CivicEffect.Income,       0.10f, "Folks linger and spend: +10% town income."),
            V("telegraph",   "Telegraph Office",  BuildingStyle.TelegraphOffice, 5,  2400,  C(("planks", 20), ("steel", 5)),  CivicEffect.ContractSlot, 1f,    "Word travels fast: +1 contract slot."),
            V("schoolhouse", "Schoolhouse",       BuildingStyle.Schoolhouse,     6,  3000,  C(("planks", 30)),                CivicEffect.Xp,           0.08f, "An educated town learns faster: +8% XP."),
            V("bank",        "Territorial Bank",  BuildingStyle.Bank,            9,  8000,  C(("steel", 25), ("tools", 5)),   CivicEffect.CoinCap,      1f,    "Vaults keep rent safe: +2h income storage."),
            V("clocktower",  "Clock Tower",       BuildingStyle.ClockTower,      14, 15000, C(("steel", 35), ("copper_wire", 10)), CivicEffect.Yield,   0.10f, "Railway time: +10% cargo from every run.", Era.GoldenAge),
            // Decoration
            V("lamppost",    "Gas Lamp",          BuildingStyle.LampPost,        2,  150,   C(("steel", 0)),                  CivicEffect.Income,       0.01f, "Lights up the night: +1% income.", Era.Frontier, BuildingCategory.Decoration, 12),
            V("garden",      "Flower Garden",     BuildingStyle.Garden,          3,  250,   C(("timber", 5)),                 CivicEffect.Income,       0.01f, "A little color on the prairie: +1% income.", Era.Frontier, BuildingCategory.Decoration, 12),
            V("fountain",    "Town Fountain",     BuildingStyle.Fountain,        8,  2000,  C(("steel", 8)),                  CivicEffect.Income,       0.03f, "The pride of Main Street: +3% income.", Era.Frontier, BuildingCategory.Decoration, 3),
        };

        public static readonly List<GoalDef> Goals = new List<GoalDef>
        {
            G("g1",  "Dispatch Dusty to Pinecrest Logging Camp", GoalType.DispatchTrains,    null,       1,   100,  2,  20),
            G("g2",  "Collect 20 Timber",                        GoalType.CollectResource,   "timber",   20,  150,  0,  30),
            G("g3",  "Build a Sawmill",                          GoalType.ConstructBuilding, "sawmill",  1,   200,  3,  40),
            G("g4",  "Produce 6 Planks",                         GoalType.ProduceResource,   "planks",   6,   250,  0,  50),
            G("g5",  "Complete a contract",                      GoalType.CompleteContracts, null,       1,   300,  5,  60),
            G("g6",  "Reach level 3",                            GoalType.ReachLevel,        null,       3,   500,  5,  0),
            G("g7",  "Own 2 locomotives",                        GoalType.OwnLocomotives,    null,       2,   600,  5,  80),
            G("g8",  "Grow the town to 40 people",               GoalType.ReachPopulation,   null,       40,  800,  5,  100),
            G("g9",  "Build a Smelter",                          GoalType.ConstructBuilding, "smelter",  1,   1000, 8,  120),
            G("g10", "Produce 15 Steel",                         GoalType.ProduceResource,   "steel",    15,  1500, 8,  150),
            G("g11", "Complete 10 contracts",                    GoalType.CompleteContracts, null,       10,  2500, 10, 250),
            G("g12", "Reach level 10",                           GoalType.ReachLevel,        null,       10,  4000, 15, 0),
            G("g13", "Own 5 locomotives",                        GoalType.OwnLocomotives,    null,       5,   6000, 15, 400),
            G("g14", "Grow the town to 300 people",              GoalType.ReachPopulation,   null,       300, 9000, 20, 600),
            G("g15", "Reach the Golden Age (level 15)",          GoalType.ReachLevel,        null,       15,  15000,30, 0),
        };

        // ---- Lookups -------------------------------------------------------

        static Dictionary<string, ResourceDef> _res;
        static Dictionary<string, LocomotiveDef> _locos;
        static Dictionary<string, WagonDef> _wagons;
        static Dictionary<string, DestinationDef> _dests;
        static Dictionary<string, BuildingDef> _buildings;

        public static ResourceDef Resource(string id) { return Get(ref _res, Resources, r => r.Id, id); }
        public static LocomotiveDef Locomotive(string id) { return Get(ref _locos, Locomotives, r => r.Id, id); }
        public static WagonDef Wagon(string id) { return Get(ref _wagons, Wagons, r => r.Id, id); }
        public static DestinationDef Destination(string id) { return Get(ref _dests, Destinations, r => r.Id, id); }
        public static BuildingDef Building(string id) { return Get(ref _buildings, Buildings, r => r.Id, id); }

        static T Get<T>(ref Dictionary<string, T> cache, List<T> list, Func<T, string> key, string id) where T : class
        {
            if (cache == null) cache = list.ToDictionary(key);
            if (id == null) return null;
            T v;
            return cache.TryGetValue(id, out v) ? v : null;
        }

        // ---- Builders ------------------------------------------------------

        static ResourceDef R(string id, string name, CargoClass c, int value, bool made, uint rgb, string glyph)
        { return new ResourceDef { Id = id, Name = name, Cargo = c, Value = value, Manufactured = made, ColorRgb = rgb, Glyph = glyph }; }

        static LocomotiveDef L(string id, string name, Era era, Rarity r, LocoStyle s, int power, int speed, int lvl, int coins, int nuggets, uint primary, uint trim, string blurb)
        { return new LocomotiveDef { Id = id, Name = name, Era = era, Rarity = r, Style = s, Power = power, Speed = speed, UnlockLevel = lvl, PriceCoins = coins, PriceNuggets = nuggets, PrimaryRgb = primary, TrimRgb = trim, Blurb = blurb }; }

        static ResourceDef Pax()
        { return new ResourceDef { Id = "passengers", Name = "Passengers", Cargo = CargoClass.Coach, Value = 6, ColorRgb = 0x2E4A7A, Glyph = "Px", IsPassenger = true }; }

        static ResourceDef Part(string id, string name, uint rgb, string glyph)
        { return new ResourceDef { Id = id, Name = name, Cargo = CargoClass.Box, Value = 25, Manufactured = true, IsPart = true, ColorRgb = rgb, Glyph = glyph }; }

        static WagonDef W(string id, string name, CargoClass c, int cap, int lvl, int price, uint rgb)
        { return new WagonDef { Id = id, Name = name, Cargo = c, Capacity = cap, UnlockLevel = lvl, PriceCoins = price, ColorRgb = rgb }; }

        static DestinationDef D(string id, string name, string res, int lvl, int secs, float yield, string desc, float x, float y)
        { return new DestinationDef { Id = id, Name = name, ResourceId = res, UnlockLevel = lvl, BaseSeconds = secs, Yield = yield, Description = desc, MapX = x, MapY = y }; }

        static ResourceAmount[] C(params (string id, int amt)[] items)
        { return items.Where(i => i.amt > 0).Select(i => new ResourceAmount(i.id, i.amt)).ToArray(); }

        static ResourceAmount[] In(params (string id, int amt)[] items) { return C(items); }
        static ResourceAmount Out(string id, int amt) { return new ResourceAmount(id, amt); }

        static BuildingDef B(string id, string name, BuildingCategory cat, BuildingStyle style, int lvl, int coins, ResourceAmount[] cost, int pop, int max, Era era = Era.Frontier)
        {
            return new BuildingDef { Id = id, Name = name, Category = cat, Style = style, UnlockLevel = lvl, CostCoins = coins, CostResources = cost, Population = pop, MaxCount = max, Era = era,
                Blurb = "Houses " + pop + " settlers who pay rent." };
        }

        static BuildingDef P(string id, string name, BuildingStyle style, int lvl, int coins, ResourceAmount[] cost, ResourceAmount[] input, ResourceAmount output, int secs, Era era = Era.Frontier)
        {
            return new BuildingDef { Id = id, Name = name, Category = BuildingCategory.Production, Style = style, UnlockLevel = lvl, CostCoins = coins, CostResources = cost, MaxCount = 3, Era = era,
                Recipe = new Recipe { Inputs = input, Output = output, Seconds = secs },
                Blurb = "Turns raw goods into " + Resource(output.id)?.Name + "." };
        }

        static BuildingDef V(string id, string name, BuildingStyle style, int lvl, int coins, ResourceAmount[] cost, CivicEffect fx, float val, string blurb,
            Era era = Era.Frontier, BuildingCategory cat = BuildingCategory.Civic, int max = 1)
        {
            return new BuildingDef { Id = id, Name = name, Category = cat, Style = style, UnlockLevel = lvl, CostCoins = coins, CostResources = cost, Effect = fx, EffectValue = val, Blurb = blurb, MaxCount = max, Era = era };
        }

        static GoalDef G(string id, string text, GoalType t, string target, int count, int coins, int nuggets, int xp)
        { return new GoalDef { Id = id, Text = text, Type = t, Target = target, Count = count, RewardCoins = coins, RewardNuggets = nuggets, RewardXp = xp }; }
    }
}

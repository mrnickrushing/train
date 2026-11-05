using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>A motor truck: carries any town good to a local customer.</summary>
    public sealed class TruckDef
    {
        public string Id;
        public string Name;
        /// <summary>Units of goods it carries at level 1.</summary>
        public int Capacity;
        /// <summary>Road speed. Drive time scales with Balance.ReferenceSpeed / Speed, like trains.</summary>
        public int Speed;
        public int UnlockLevel;
        public int PriceCoins;
        public int PriceNuggets;
        public Rarity Rarity;
        public uint PrimaryRgb;
        public uint TrimRgb;
        public string Blurb;
    }

    /// <summary>Someone around the valley who orders goods by truck.</summary>
    public sealed class CustomerDef
    {
        public string Id;
        public string Name;
        /// <summary>Short name for buttons ("the General Store").</summary>
        public string Short;
        /// <summary>Goods they order, in the order they prefer them.</summary>
        public string[] Wants;
        /// <summary>Round trip in seconds for a truck at reference speed.</summary>
        public int Seconds;
        public int UnlockLevel;
    }

    /// <summary>Content and tuning for the Motor Age: the garage, trucks and the local delivery board (docs/CHAPTER_2.md).</summary>
    public static class Motor
    {
        /// <summary>Level the Motor Garage unlocks at (Chapter 6, Silver Lake).</summary>
        public const int UnlockLevel = 12;
        public const string GarageId = "garage";
        /// <summary>The truck that comes free with the first garage.</summary>
        public const string StarterTruck = "runabout";

        /// <summary>Truck bays at a garage level: 2 at level 1, one more per level.</summary>
        public static int Bays(int garageLevel) { return 1 + garageLevel; }

        public const int DeliverySlots = 4;
        public const double DeliveryRefreshSeconds = 90;
        public const int MaxTruckLevel = 5;
        public const double PartsChance = 0.25;

        /// <summary>Load multiplier from truck level (+15% a level).</summary>
        public static float LoadMultiplier(int level) { return 1f + 0.15f * (level - 1); }
        /// <summary>Speed multiplier from truck level (+5% a level).</summary>
        public static float SpeedMultiplier(int level) { return 1f + 0.05f * (level - 1); }

        public static int UpgradeCoins(TruckDef d, int level)
        {
            int basePrice = System.Math.Max(3000, d.PriceCoins > 0 ? d.PriceCoins : d.PriceNuggets * 100);
            return (int)(basePrice * 0.3 * level);
        }

        public static ResourceAmount[] UpgradeResources(int level)
        {
            if (level < 3) return new[] { new ResourceAmount("tools", 3 * level) };
            return new[] { new ResourceAmount("tools", 3 * level), new ResourceAmount("copper_wire", 2 * level) };
        }

        /// <summary>Coins a delivery pays: about a fifth more per unit than a town order, plus a little for the drive.</summary>
        public static int Reward(int value, int seconds) { return (int)System.Math.Round(value * 2.2 + 25 + seconds / 4.0); }
        public static int RewardXp(int value) { return System.Math.Max(4, (int)System.Math.Round(value * 0.5)); }

        public static readonly List<TruckDef> Trucks = new List<TruckDef>
        {
            T("runabout",  "Dart Runabout",      6,  40, 12, 3000,  0,   Rarity.Common,   0x2F4F3A, 0xC9A227, "Brass lamps, a crank start and a lot of nerve."),
            T("stakebed",  "Teamster Stake Bed", 10, 44, 13, 6500,  0,   Rarity.Common,   0x6B2E1F, 0xD9B26A, "Wooden stakes, solid tyres, never late."),
            T("bulldog",   "Bulldog Two-Ton",    16, 40, 15, 12000, 0,   Rarity.Uncommon, 0x2E3B4E, 0xB87333, "Chain drive and a snub nose. Hauls like a team of mules."),
            T("cometvan",  "Silver Comet Van",   14, 64, 16, 0,     240, Rarity.Epic,     0xC0C6CC, 0x1F3A68, "Polished aluminium and a six-cylinder purr. Nothing on the road catches it."),
            T("hauler",    "Highway Hauler",     22, 46, 18, 24000, 0,   Rarity.Rare,     0x7A1F1F, 0xE8C26A, "Twin rear axles for the new paved highway."),
        };

        public static readonly List<CustomerDef> Customers = new List<CustomerDef>
        {
            C("generalstore", "Main Street General Store", "the General Store", 60,  12, "flour", "provisions", "tools"),
            C("lumberyard",   "Halvorsen Lumber Yard",     "the Lumber Yard",   90,  12, "planks", "steel"),
            C("hotel",        "Miss Adelaide's Hotel",     "the Hotel",         75,  12, "provisions", "flour"),
            C("coop",         "Willow Flats Co-op",        "the Co-op",         150, 12, "tools", "planks", "flour"),
            C("ranch",        "Rattlesnake Ranch",         "the Ranch",         180, 13, "provisions", "tools", "planks"),
            C("minecamp",     "Black Hollow Miners' Camp", "the Miners' Camp",  240, 14, "tools", "provisions", "steel"),
            C("damcrews",     "Silver Lake Dam Crews",     "the Dam Crews",     300, 15, "steel", "copper_wire", "tools"),
            C("power",        "Valley Power Company",      "the Power Company", 360, 16, "copper_wire", "fuel"),
        };

        static Dictionary<string, TruckDef> _trucks;
        static Dictionary<string, CustomerDef> _customers;

        public static TruckDef Truck(string id)
        {
            if (_trucks == null) _trucks = Trucks.ToDictionary(t => t.Id);
            TruckDef d;
            return id != null && _trucks.TryGetValue(id, out d) ? d : null;
        }

        public static CustomerDef Customer(string id)
        {
            if (_customers == null) _customers = Customers.ToDictionary(c => c.Id);
            CustomerDef d;
            return id != null && _customers.TryGetValue(id, out d) ? d : null;
        }

        static TruckDef T(string id, string name, int cap, int speed, int lvl, int coins, int nuggets, Rarity r, uint primary, uint trim, string blurb)
        {
            return new TruckDef { Id = id, Name = name, Capacity = cap, Speed = speed, UnlockLevel = lvl, PriceCoins = coins, PriceNuggets = nuggets, Rarity = r,
                                  PrimaryRgb = primary, TrimRgb = trim, Blurb = blurb };
        }

        static CustomerDef C(string id, string name, string shortName, int secs, int lvl, params string[] wants)
        {
            return new CustomerDef { Id = id, Name = name, Short = shortName, Seconds = secs, UnlockLevel = lvl, Wants = wants };
        }
    }
}

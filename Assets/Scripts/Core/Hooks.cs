using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>A Roundhouse album set: own every engine or car in it once for a permanent bonus.</summary>
    public sealed class AlbumSetDef
    {
        public string Id;
        public string Name;
        public string Description;
        public string[] Locos = new string[0];
        public string[] Wagons = new string[0];
        public CivicEffect Perk;
        public float PerkValue;
        public string PerkText;
        public int Nuggets;

        public IEnumerable<string> Members { get { return Locos.Concat(Wagons); } }
        public int Count { get { return Locos.Length + Wagons.Length; } }
    }

    /// <summary>Tuning and content for the daily hooks: payday, crates, telegrams, map events and the album.</summary>
    public static class Hooks
    {
        // Payday
        public const int PaydayDays = 7;
        public const int PaydayBlueprints = 1;

        // Away summary: shown when the player was gone at least this long and something is waiting.
        public const double AwayMinSeconds = 10 * 60;

        // Mystery crates
        public const double CrateChance = 0.16;
        /// <summary>After this many crate-less trips the next one always brings a crate.</summary>
        public const int CrateDrySpell = 7;
        /// <summary>The first few trips (the tutorial) never bring one.</summary>
        public const int CrateMinDispatches = 4;
        /// <summary>At most this many crates a day, however many trains run (keeps crates a treat, not a farm).</summary>
        public const int CratesPerDay = 4;

        // Express telegrams
        public const int ExpressLevel = 3;
        public const double ExpressSeconds = 2 * 3600;
        public const double ExpressGap = 4 * 3600;
        public const double ExpressFirstDelay = 5 * 60;
        public const int ExpressPayMultiplier = 3;

        // Map events
        public const int EventLevel = 2;
        public const int MaxEvents = 3;
        public const double EventFirstDelay = 2 * 60;
        public const double EventGapMin = 10 * 60, EventGapMax = 20 * 60;
        public const double BanditChance = 0.5;
        public const float BanditLoss = 0.3f;

        public static double EventSeconds(MapEventKind k)
        {
            switch (k)
            {
                case MapEventKind.Tumbleweed: return 8 * 60;
                case MapEventKind.Traveler: return 30 * 60;
                case MapEventKind.Windfall: return 40 * 60;
                default: return 3600;
            }
        }

        public static int EventLevelFor(MapEventKind k)
        {
            switch (k)
            {
                case MapEventKind.GoldStrike: return 3;
                case MapEventKind.Bandits: return 4;
                default: return EventLevel;
            }
        }

        public static string EventName(MapEventKind k)
        {
            switch (k)
            {
                case MapEventKind.Traveler: return "Stranded Traveler";
                case MapEventKind.GoldStrike: return "Gold Strike!";
                case MapEventKind.Tumbleweed: return "Tumbleweeds";
                case MapEventKind.Windfall: return "Windfall";
                default: return "Bandits!";
            }
        }

        public static int TravelerCoins(int level) { return 60 + 40 * level; }
        public static int TravelerXp(int level) { return 10 + 3 * level; }
        public static int TumbleweedCoins(int level) { return 20 + 12 * level; }
        /// <summary>Goods in a windfall cache at an outpost (logs, coal, ore...).</summary>
        public static int WindfallAmount(int level) { return 12 + 3 * level; }
        public static int EscortCoins(int level) { return 80 + 50 * level; }
        public static int EscortXp(int level) { return 15 + 4 * level; }

        static readonly string[] BlueprintLocos = { "sundown", "silverarrow", "thunderhead" };

        public static string PaydayBlueprintLoco(int weeks) { return BlueprintLocos[((weeks % BlueprintLocos.Length) + BlueprintLocos.Length) % BlueprintLocos.Length]; }

        /// <summary>The reward for day 1..7 of the payday calendar; coins grow with level, day 7 is a locomotive blueprint.</summary>
        public static Grant PaydayReward(int day, int level, int weeks)
        {
            switch (day)
            {
                case 1: return new Grant { Coins = 300 + 100 * level };
                case 2: return new Grant { Fuel = 30 };
                case 3: return new Grant { Nuggets = 5 };
                case 4: return new Grant { Resources = new[] { new ResourceAmount(level < 6 ? "rivets" : "bearings", 8 + level / 2) } };
                case 5: return new Grant { Coins = 600 + 200 * level };
                case 6: return new Grant { Nuggets = 10, Fuel = 40 };
                default: return new Grant { Nuggets = 15, BlueprintLoco = PaydayBlueprintLoco(weeks), Blueprints = PaydayBlueprints };
            }
        }

        public static readonly List<AlbumSetDef> AlbumSets = new List<AlbumSetDef>
        {
            new AlbumSetDef { Id = "frontier", Name = "Frontier Steam", Description = "Every engine of the Frontier era.",
                Locos = new[] { "dusty", "lark", "redmesa", "copperqueen", "ironbison" }, Perk = CivicEffect.TrainSpeed, PerkValue = 0.05f, PerkText = "+5% train speed", Nuggets = 50 },
            new AlbumSetDef { Id = "goldenage", Name = "Golden Age Streamliners", Description = "The art-deco giants of the Golden Age.",
                Locos = new[] { "sundown", "silverarrow", "thunderhead" }, Perk = CivicEffect.Yield, PerkValue = 0.10f, PerkText = "+10% cargo from every run", Nuggets = 150 },
            new AlbumSetDef { Id = "freight", Name = "Freight Yard", Description = "One of every everyday freight car.",
                Wagons = new[] { "flatcar", "hopper", "boxcar", "stockcar", "tankcar" }, Perk = CivicEffect.Yield, PerkValue = 0.05f, PerkText = "+5% cargo from every run", Nuggets = 30 },
            new AlbumSetDef { Id = "heavy", Name = "Heavy Haulers", Description = "The big cars for long, heavy runs.",
                Wagons = new[] { "longflat", "steelhopper", "reefer" }, Perk = CivicEffect.Xp, PerkValue = 0.05f, PerkText = "+5% XP", Nuggets = 40 },
            new AlbumSetDef { Id = "passenger", Name = "Passenger Service", Description = "Mail by day, sleepers by night.",
                Wagons = new[] { "mailcoach", "pullman" }, Perk = CivicEffect.Income, PerkValue = 0.05f, PerkText = "+5% town income", Nuggets = 40 },
        };

        public static AlbumSetDef AlbumSet(string id) { return AlbumSets.FirstOrDefault(a => a.Id == id); }
    }

    /// <summary>What happened while the player was away, and what is waiting to be collected.</summary>
    public sealed class AwayReport
    {
        public double Seconds;
        public int Trains;
        public List<ResourceAmount> Cargo = new List<ResourceAmount>();
        public int Fares;
        public int Rent;
        public List<ResourceAmount> Goods = new List<ResourceAmount>();
        public int Crates;
        public int Robbed;
        public List<CurioDef> Curios = new List<CurioDef>();

        public bool HasAnything { get { return Trains > 0 || Rent > 0 || Goods.Count > 0 || Curios.Count > 0; } }
    }
}

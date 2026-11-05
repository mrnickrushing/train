using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>How a hauler travels: wagons and trucks take the roads, boats the lake and the river.</summary>
    public enum HaulKind { Wagon, Boat, Truck }

    /// <summary>A road or river vehicle that carries goods from the freight yard out to territory projects.</summary>
    public sealed class HaulerDef
    {
        public string Id;
        public string Name;
        public HaulKind Kind;
        public Era Era;
        public Rarity Rarity;
        /// <summary>Goods carried per trip at level 1.</summary>
        public int Capacity;
        /// <summary>Travel speed in world metres per second (the vehicle on the map moves at this speed).</summary>
        public float Speed;
        public int UnlockLevel;
        public int PriceCoins;
        public int PriceNuggets;
        public string Blurb;
        public bool OnWater { get { return Kind == HaulKind.Boat; } }
    }

    /// <summary>How the haulers reach one project: road length and, for sites on the lake or river, the water route.</summary>
    public sealed class SiteRoute
    {
        public string ProjectId;
        /// <summary>One-way road distance from the freight yard, metres (0 = no road).</summary>
        public float RoadMeters;
        /// <summary>One-way water distance from the town landing, metres (0 = boats can't get there).</summary>
        public float WaterMeters;
        /// <summary>Character who asks for the goods (portrait UI/Story/char_{id}).</summary>
        public string Character;
        public string CharacterName;
    }

    public static class Haulage
    {
        /// <summary>Haulers unlock with the first territory project.</summary>
        public const int UnlockLevel = 3;
        public const int MaxLevel = 5;
        /// <summary>The freight wagon every railroad gets when haulage opens.</summary>
        public const string StarterId = "freightwagon";

        public static readonly List<HaulerDef> Haulers = new List<HaulerDef>
        {
            H("freightwagon", "Freight Wagon",         HaulKind.Wagon, Era.Frontier,  Rarity.Common,   20,  1.6f, 3,  0,     0,  "Two draft horses and a canvas top. It gets there."),
            H("muletrain",    "Mule Pack Train",       HaulKind.Wagon, Era.Frontier,  Rarity.Common,   12,  2.4f, 4,  2500,  0,  "Sure-footed mules that trot where wagons crawl."),
            H("sternwheeler", "River Belle Sternwheeler", HaulKind.Boat, Era.Frontier, Rarity.Uncommon, 60, 2.2f, 5,  7500,  0,  "Paddles thrashing, she hauls a barge-load up the river."),
            H("conestoga",    "Conestoga Freighter",   HaulKind.Wagon, Era.Frontier,  Rarity.Uncommon, 45,  1.3f, 8,  9000,  0,  "A six-horse giant. Slow, steady, and carries a mountain."),
            H("steamer",      "Silver Lake Steamer",   HaulKind.Boat,  Era.Frontier,  Rarity.Rare,     120, 3.0f, 11, 20000, 0,  "Twin stacks and a full hold, the pride of the lake."),
            H("motortruck",   "Motor Truck",           HaulKind.Truck, Era.GoldenAge, Rarity.Rare,     60,  5.0f, 15, 30000, 0,  "Rubber tyres and a gasoline roar. The future arrives."),
            H("expressvan",   "Express Delivery Van",  HaulKind.Truck, Era.GoldenAge, Rarity.Epic,     90,  6.0f, 18, 0,     220, "Polished brass, painted gold: the fastest freight in the territory."),
        };

        public static readonly List<SiteRoute> Routes = new List<SiteRoute>
        {
            new SiteRoute { ProjectId = "telegraph",   RoadMeters = 240, WaterMeters = 0,   Character = "engineer", CharacterName = "Clara Whitlock" },
            new SiteRoute { ProjectId = "trestle",     RoadMeters = 180, WaterMeters = 14, Character = "foreman",  CharacterName = "Hank" },
            new SiteRoute { ProjectId = "mineshaft",   RoadMeters = 140, WaterMeters = 0,   Character = "rival",    CharacterName = "Cyrus Blackwell" },
            new SiteRoute { ProjectId = "resupply",    RoadMeters = 190, WaterMeters = 0,   Character = "foreman",  CharacterName = "Hank" },
            new SiteRoute { ProjectId = "lighthouse",  RoadMeters = 0,   WaterMeters = 350, Character = "engineer", CharacterName = "Clara Whitlock" },
            new SiteRoute { ProjectId = "lodge",       RoadMeters = 515, WaterMeters = 375, Character = "investor", CharacterName = "Mr. Pemberton" },
            new SiteRoute { ProjectId = "dam",         RoadMeters = 30, WaterMeters = 0,  Character = "engineer", CharacterName = "Clara Whitlock" },
            new SiteRoute { ProjectId = "winery",      RoadMeters = 520, WaterMeters = 0,   Character = "foreman",  CharacterName = "Hank" },
            new SiteRoute { ProjectId = "mission",     RoadMeters = 370, WaterMeters = 0,   Character = "engineer", CharacterName = "Clara Whitlock" },
            new SiteRoute { ProjectId = "opera",       RoadMeters = 365, WaterMeters = 0,   Character = "investor", CharacterName = "Mr. Pemberton" },
            new SiteRoute { ProjectId = "capitol",     RoadMeters = 385, WaterMeters = 0,   Character = "investor", CharacterName = "Mr. Pemberton" },
            new SiteRoute { ProjectId = "goldenspike", RoadMeters = 205, WaterMeters = 0,   Character = "investor", CharacterName = "Mr. Pemberton" },
        };

        static HaulerDef H(string id, string name, HaulKind kind, Era era, Rarity r, int cap, float speed, int lvl, int coins, int nuggets, string blurb)
        { return new HaulerDef { Id = id, Name = name, Kind = kind, Era = era, Rarity = r, Capacity = cap, Speed = speed, UnlockLevel = lvl, PriceCoins = coins, PriceNuggets = nuggets, Blurb = blurb }; }

        public static HaulerDef Hauler(string id) { return Haulers.FirstOrDefault(h => h.Id == id); }
        public static SiteRoute Route(string projectId) { return Routes.FirstOrDefault(r => r.ProjectId == projectId); }

        /// <summary>Goods per trip: +25% a level.</summary>
        public static int CapacityAt(HaulerDef d, int level) { return (int)(d.Capacity * (1 + 0.25f * (level - 1))); }

        /// <summary>One-way distance for this hauler to the project, or 0 when it can't get there.</summary>
        public static float Distance(HaulerDef d, SiteRoute r)
        {
            if (r == null) return 0;
            return d.OnWater ? r.WaterMeters : r.RoadMeters;
        }

        /// <summary>Round trip, seconds: out with the goods and back to the yard.</summary>
        public static double TripSeconds(HaulerDef d, SiteRoute r)
        {
            float m = Distance(d, r);
            return m <= 0 ? 0 : 2 * m / d.Speed;
        }

        public static int UpgradeCoins(HaulerDef d, int level)
        {
            int basePrice = System.Math.Max(800, d.PriceCoins > 0 ? d.PriceCoins : d.PriceNuggets * 200);
            return (int)(basePrice * 0.3 * level);
        }

        public static int SellPrice(HaulerDef d, int level)
        {
            int price = d.PriceCoins > 0 ? d.PriceCoins : d.PriceNuggets * 200;
            if (price <= 0) price = 400;
            long ups = 0;
            for (int l = 1; l < level; l++) ups += UpgradeCoins(d, l);
            return (int)(price / 2 + ups / 4);
        }
    }
}

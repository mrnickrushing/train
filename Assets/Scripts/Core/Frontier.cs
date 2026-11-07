using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public enum EngineerPerk { Cargo, Speed, Fares, Fuel }

    /// <summary>A collectible engineer who rides one locomotive and improves its trips.</summary>
    public sealed class EngineerDef
    {
        public string Id;
        public string Name;
        public string Title;
        public EngineerPerk Perk;
        /// <summary>For <see cref="EngineerPerk.Cargo"/>: the wagon classes the bonus applies to.</summary>
        public CargoClass[] Cargo = new CargoClass[0];
        /// <summary>Bonus at level 1 and per extra level (0.10 = +10%).</summary>
        public float Base, PerLevel;
        public Rarity Rarity;
        public string Blurb;

        public float Value(int level) { return level <= 0 ? 0 : Base + PerLevel * (level - 1); }

        public string PerkText(int level)
        {
            int pct = (int)Math.Round(Value(Math.Max(1, level)) * 100);
            switch (Perk)
            {
                case EngineerPerk.Speed: return "+" + pct + "% train speed";
                case EngineerPerk.Fares: return "+" + pct + "% passenger fares";
                case EngineerPerk.Fuel: return "-" + pct + "% fuel per trip";
                default: return "+" + pct + "% " + string.Join(" & ", Cargo.Select(CargoName)) + " cargo";
            }
        }

        static string CargoName(CargoClass c)
        {
            switch (c)
            {
                case CargoClass.Bulk: return "ore & coal";
                case CargoClass.Flat: return "lumber & steel";
                case CargoClass.Box: return "boxcar";
                case CargoClass.Livestock: return "cattle";
                case CargoClass.Tank: return "oil";
                default: return "mail";
            }
        }
    }

    /// <summary>A rare find from one outpost or town, shown in the town museum.</summary>
    public sealed class CurioDef
    {
        public string Id;
        public string Name;
        public string DestinationId;
        public string ShelfId;
        public string Blurb;
    }

    /// <summary>A museum shelf of curios; filling it gives a permanent bonus.</summary>
    public sealed class ShelfDef
    {
        public string Id;
        public string Name;
        public CivicEffect Perk;
        public float PerkValue;
        public string PerkText;
        public int Nuggets;
        public IEnumerable<CurioDef> Curios { get { return Frontier.Curios.Where(c => c.ShelfId == Id); } }
    }

    /// <summary>Tuning and content for the dispatcher's board, engineers, the peddler, Full Steam, curios and charters.</summary>
    public static class Frontier
    {
        // ---------------------------------------------------------------- Dispatcher's board
        public const int DailyTaskCount = 4;
        public const int DailyTaskPoints = 25;
        public static readonly int[] DailyChestAt = { 50, 100 };
        public static readonly int[] WeeklyChestAt = { 250, 500, 700 };

        public static int DailyCount(DailyKind k, int level)
        {
            switch (k)
            {
                case DailyKind.Dispatch: return 4 + level / 2;
                case DailyKind.Contracts: return 2 + level / 6;
                case DailyKind.Produce: return 6 + level * 2;
                case DailyKind.Collect: return 60 + level * 20;
                case DailyKind.Passengers: return 20 + level * 6;
                case DailyKind.Crates: return 1;
                case DailyKind.Events: return 2;
                case DailyKind.Rent: return 6;
                case DailyKind.EarnCoins: return (1000 + level * 400) / 100 * 100;
                case DailyKind.TruckDeliveries: return 3 + level / 6;
                default: return 20 + level * 5;
            }
        }

        public static string DailyText(DailyTask t)
        {
            switch (t.kind)
            {
                case DailyKind.Dispatch: return "Send out " + t.count + " trains";
                case DailyKind.Contracts: return "Deliver " + t.count + " orders";
                case DailyKind.Produce: return "Make " + t.count + " goods in factories";
                case DailyKind.Collect: return "Haul " + t.count + " cargo home";
                case DailyKind.Passengers: return "Carry " + t.count + " passengers";
                case DailyKind.Crates: return t.count == 1 ? "Open a mystery crate" : "Open " + t.count + " mystery crates";
                case DailyKind.Events: return "Help with " + t.count + " map events";
                case DailyKind.Rent: return "Collect rent " + t.count + " times";
                case DailyKind.EarnCoins: return "Earn " + t.count.ToString("#,0") + " coins";
                case DailyKind.TruckDeliveries: return "Make " + t.count + " truck deliveries";
                default: return "Deliver " + t.count + " goods to projects";
            }
        }

        public static Grant DailyChest(int at, int level)
        {
            if (at <= DailyChestAt[0])
                return new Grant { Coins = 400 + 150 * level, Resources = new[] { new ResourceAmount(level < 6 ? "rivets" : "bearings", 6 + level / 2) } };
            return new Grant { Nuggets = 8, Fuel = 30 };
        }

        /// <summary>Engineer shards that come with a daily or weekly chest.</summary>
        public static int ChestShards(int at, bool weekly) { return weekly ? (at >= WeeklyChestAt[2] ? 10 : at >= WeeklyChestAt[1] ? 0 : 5) : (at >= DailyChestAt[1] ? 3 : 0); }

        public static Grant WeeklyChest(int at, string blueprintLoco)
        {
            if (at <= WeeklyChestAt[0]) return new Grant { Nuggets = 15 };
            if (at <= WeeklyChestAt[1]) return new Grant { Nuggets = 25, BlueprintLoco = blueprintLoco, Blueprints = 1 };
            return new Grant { Nuggets = 40, BlueprintLoco = blueprintLoco, Blueprints = 2 };
        }

        // ---------------------------------------------------------------- Engineers
        public const int CrewLevel = 4;
        public const int RecruitShards = 10;
        public const int MaxEngineerLevel = 5;
        public static int LevelUpShards(int level) { return 10 * level; }

        public static readonly List<EngineerDef> Engineers = new List<EngineerDef>
        {
            E("bigjim", "Big Jim Calloway", "Coal heaver", EngineerPerk.Cargo, new[] { CargoClass.Bulk }, 0.10f, 0.05f, Rarity.Common, "Shovels coal like the boiler owes him money."),
            E("ada", "Ada Whitfield", "Timber boss", EngineerPerk.Cargo, new[] { CargoClass.Flat }, 0.10f, 0.05f, Rarity.Common, "Can stack a flatcar so tight not a plank rattles."),
            E("kit", "Kit Two-Rivers", "Drover", EngineerPerk.Cargo, new[] { CargoClass.Box, CargoClass.Livestock }, 0.08f, 0.04f, Rarity.Uncommon, "Grain, cattle or crates: they all ride easy with Kit."),
            E("rosa", "Rosa Delgado", "Hogger", EngineerPerk.Speed, new CargoClass[0], 0.05f, 0.025f, Rarity.Rare, "Knows every grade and curve between here and the coast."),
            E("doc", "Doc Holloway", "Conductor", EngineerPerk.Fares, new CargoClass[0], 0.10f, 0.05f, Rarity.Uncommon, "Tells such good stories the passengers tip double."),
            E("smitty", "Smitty O'Shea", "Fireman", EngineerPerk.Fuel, new CargoClass[0], 0.10f, 0.05f, Rarity.Rare, "Gets a full run out of half a bunker of coal."),
        };

        public static EngineerDef Engineer(string id) { return Engineers.FirstOrDefault(e => e.Id == id); }

        static EngineerDef E(string id, string name, string title, EngineerPerk perk, CargoClass[] cargo, float b, float per, Rarity r, string blurb)
        { return new EngineerDef { Id = id, Name = name, Title = title, Perk = perk, Cargo = cargo, Base = b, PerLevel = per, Rarity = r, Blurb = blurb }; }

        // ---------------------------------------------------------------- Traveling peddler
        public const int PeddlerLevel = 5;
        public const double PeddlerStay = 3600;
        public const double PeddlerFirstDelay = 10 * 60;
        public const double PeddlerGapMin = 3 * 3600, PeddlerGapMax = 5 * 3600;

        // ---------------------------------------------------------------- Full Steam combo
        public const int ComboLevel = 3;
        public const double ComboWindow = 60;
        public const float ComboStep = 0.05f;
        public const int ComboMax = 6;

        /// <summary>Cargo multiplier for the n-th train sent in a row (1 = no bonus).</summary>
        public static float ComboMultiplier(int n) { return 1 + ComboStep * (Math.Min(n, ComboMax) - 1); }

        // ---------------------------------------------------------------- Curios
        public const double CurioChance = 0.10;

        public static readonly List<ShelfDef> Shelves = new List<ShelfDef>
        {
            new ShelfDef { Id = "mountain", Name = "Mountain Finds", Perk = CivicEffect.Yield, PerkValue = 0.03f, PerkText = "+3% cargo from every run", Nuggets = 40 },
            new ShelfDef { Id = "prairie", Name = "Prairie Keepsakes", Perk = CivicEffect.Income, PerkValue = 0.03f, PerkText = "+3% town income", Nuggets = 40 },
            new ShelfDef { Id = "towns", Name = "Town Treasures", Perk = CivicEffect.Xp, PerkValue = 0.03f, PerkText = "+3% XP", Nuggets = 60 },
        };

        public static readonly List<CurioDef> Curios = new List<CurioDef>
        {
            K("curio_eagle", "Carved Pine Eagle", "pinecrest", "mountain", "Whittled by a logger on a long winter night."),
            K("curio_fossil", "Ammonite Fossil", "ironback", "mountain", "A sea shell, found a mile up a mountain."),
            K("curio_bell", "Copper Mission Bell", "coppertop", "mountain", "Cast from the first copper out of the diggings."),
            K("curio_watch", "Gold Pocket Watch", "eldorado", "mountain", "Engraved to a prospector who struck it rich."),
            K("curio_doll", "Corn-Husk Doll", "willowflats", "prairie", "A farm girl's doll, dressed in calico."),
            K("curio_spurs", "Silver Spurs", "rattlesnake", "prairie", "Worn by the best bronc rider in the territory."),
            K("curio_lamp", "Miner's Carbide Lamp", "blackhollow", "prairie", "Still smells of the deep seam."),
            K("curio_bottle", "First Oil Bottle", "blackgold", "prairie", "Crude from the first gusher, corked and labelled."),
            K("curio_water", "Mineral Water Flask", "silversprings", "towns", "Guaranteed to cure whatever ails you."),
            K("curio_bugle", "Cavalry Bugle", "fortcalder", "towns", "It sounded the charge at Calder Pass."),
            K("curio_cornet", "Brass Band Cornet", "prosperity", "towns", "Played at every train that ever pulled into Prosperity."),
            K("curio_seal", "Territorial Seal", "capitaljct", "towns", "The great seal, pressed on the first statehood petition."),
        };

        public static CurioDef Curio(string id) { return Curios.FirstOrDefault(c => c.Id == id); }
        public static ShelfDef Shelf(string id) { return Shelves.FirstOrDefault(s => s.Id == id); }

        static CurioDef K(string id, string name, string dest, string shelf, string blurb)
        { return new CurioDef { Id = id, Name = name, DestinationId = dest, ShelfId = shelf, Blurb = blurb }; }

        // ---------------------------------------------------------------- New territory charters
        /// <summary>Permanent bonus per charter to income, cargo and XP.</summary>
        public const float CharterBonus = 0.10f;
        public const int CharterNuggets = 300;

        static readonly string[] Towns = { "Dry Creek", "Red Bluff", "Copper Gap", "Silver Ridge", "Cedar Crossing", "Juniper Flats", "Eagle Pass", "Lone Pine" };

        public static string TownName(int territory) { return territory <= Towns.Length ? Towns[territory - 1] : "Frontier " + territory; }
    }
}

using System;
using System.Collections.Generic;

namespace Ironhorse.Core
{
    /// <summary>The kind of wagon a resource must ride in.</summary>
    public enum CargoClass { Flat, Bulk, Box, Livestock, Coach, Tank }

    public enum Era { Frontier, GoldenAge }

    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }

    /// <summary>Visual archetype used by the Unity layer to procedurally build a locomotive.</summary>
    public enum LocoStyle { SteamSwitcher, SteamAmerican, SteamMogul, SteamTenWheeler, SteamHeavy, SteamStreamlined, DieselStreamliner, DieselTwin }

    public enum BuildingCategory { Residential, Production, Civic, Decoration }

    /// <summary>Visual archetype used by the Unity layer to procedurally build a building.</summary>
    public enum BuildingStyle
    {
        Cabin, ClapboardHouse, BoardingHouse, BrickRowhouse, GrandHotel,
        Sawmill, Smelter, GristMill, PackingHouse, ToolWorks, WireWorks, AssayOffice, Refinery,
        WaterTower, Saloon, Schoolhouse, TelegraphOffice, Bank, ClockTower,
        LampPost, Garden, Fountain
    }

    public enum CivicEffect { None, TrainSpeed, Income, ContractSlot, CoinCap, Xp, Yield }

    public enum GoalType { DispatchTrains, CollectResource, ConstructBuilding, ReachLevel, CompleteContracts, OwnLocomotives, ProduceResource, ReachPopulation }

    [Serializable]
    public struct ResourceAmount
    {
        public string id;
        public int amount;
        public ResourceAmount(string id, int amount) { this.id = id; this.amount = amount; }
        public override string ToString() { return amount + " " + id; }
    }

    public sealed class ResourceDef
    {
        public string Id;
        public string Name;
        public CargoClass Cargo;
        /// <summary>Coin value of one unit, used for contract rewards and XP.</summary>
        public int Value;
        public bool Manufactured;
        /// <summary>Locomotive workshop part: stored outside the warehouse limit, never requested by orders.</summary>
        public bool IsPart;
        /// <summary>Passengers: unloaded as fares, never stored.</summary>
        public bool IsPassenger;
        public uint ColorRgb;
        public string Glyph;
    }

    public sealed class LocomotiveDef
    {
        public string Id;
        public string Name;
        public Era Era;
        public Rarity Rarity;
        public LocoStyle Style;
        /// <summary>How many wagons it can pull.</summary>
        public int Power;
        /// <summary>Top speed. Trip time scales with Balance.ReferenceSpeed / Speed.</summary>
        public int Speed;
        public int UnlockLevel;
        public int PriceCoins;
        public int PriceNuggets;
        /// <summary>When &gt; 0 the locomotive can be assembled from this many blueprints.</summary>
        public int Blueprints;
        public uint PrimaryRgb;
        public uint TrimRgb;
        public string Blurb;
    }

    public sealed class WagonDef
    {
        public string Id;
        public string Name;
        public CargoClass Cargo;
        public int Capacity;
        public int UnlockLevel;
        public int PriceCoins;
        public uint ColorRgb;
    }

    public sealed class DestinationDef
    {
        public string Id;
        public string Name;
        public string ResourceId;
        public int UnlockLevel;
        /// <summary>Trip duration in seconds for a train at reference speed.</summary>
        public int BaseSeconds;
        /// <summary>Multiplier on cargo loaded.</summary>
        public float Yield;
        public string Description;
        /// <summary>Normalised position on the territory map (0..1).</summary>
        public float MapX, MapY;
        public bool IsTown { get { return ResourceId == "passengers"; } }
    }

    public sealed class Recipe
    {
        public ResourceAmount[] Inputs;
        public ResourceAmount Output;
        public int Seconds;
    }

    public sealed class BuildingDef
    {
        public string Id;
        public string Name;
        public BuildingCategory Category;
        public BuildingStyle Style;
        public int UnlockLevel;
        public int CostCoins;
        public ResourceAmount[] CostResources;
        public int Population;
        public Recipe Recipe;
        public CivicEffect Effect;
        /// <summary>Effect strength, e.g. 0.05 = +5%, or 1 = +1 contract slot.</summary>
        public float EffectValue;
        public int MaxCount;
        public string Blurb;
        public Era Era;
    }

    public sealed class GoalDef
    {
        public string Id;
        public string Text;
        public GoalType Type;
        public string Target;
        public int Count;
        public int RewardCoins;
        public int RewardNuggets;
        public int RewardXp;
    }
}

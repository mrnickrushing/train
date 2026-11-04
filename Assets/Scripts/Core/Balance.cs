using System;

namespace Ironhorse.Core
{
    /// <summary>Tunable numbers and formulas. Everything economy-related lives here.</summary>
    public static class Balance
    {
        public const int StartCoins = 1200;
        public const int StartNuggets = 30;
        /// <summary>Most platforms the station holds.</summary>
        public const int Platforms = 6;
        /// <summary>Platforms a new railroad starts with; the rest are laid one at a time (<see cref="PlatformCoins"/>).</summary>
        public const int StartPlatforms = 3;
        public const int TotalPlots = 36;
        public const int StartPlots = 10;
        public const int ReferenceSpeed = 40;
        public const int BaseContractSlots = 3;
        public const double ContractRefreshSeconds = 120;
        public const double ConstructionSecondsPerLevel = 6;
        public const double BaseIncomeStorageSeconds = 2 * 3600;
        /// <summary>Coins per settler per minute.</summary>
        public const double RentPerSettlerPerMinute = 0.6;
        public const int MaxLocoLevel = 10;
        public const int MaxWarehouseLevel = 20;
        public const int MaxBuildingLevel = 5;
        public const int GoldenAgeLevel = 15;

        public static int XpForNextLevel(int level)
        {
            return (int)Math.Round(120 * Math.Pow(level, 1.55));
        }

        public static int WarehouseCapacity(int warehouseLevel)
        {
            return 250 + (warehouseLevel - 1) * 150;
        }

        public static int WarehouseUpgradeCoins(int warehouseLevel)
        {
            return (int)(600 * Math.Pow(warehouseLevel, 1.4));
        }

        public static ResourceAmount[] WarehouseUpgradeResources(int warehouseLevel)
        {
            if (warehouseLevel < 3) return new[] { new ResourceAmount("timber", 20 * warehouseLevel) };
            if (warehouseLevel < 7) return new[] { new ResourceAmount("planks", 12 * warehouseLevel) };
            return new[] { new ResourceAmount("planks", 10 * warehouseLevel), new ResourceAmount("steel", 4 * warehouseLevel) };
        }

        /// <summary>Level needed to lay the next platform when <paramref name="built"/> are already laid.</summary>
        public static int PlatformUnlockLevel(int built) { return built <= 3 ? 6 : built == 4 ? 10 : 14; }

        public static int PlatformCoins(int built) { return built <= 3 ? 4000 : built == 4 ? 12000 : 25000; }

        /// <summary>Goods from the town's mills and works that the track crew needs for the next platform.</summary>
        public static ResourceAmount[] PlatformResources(int built)
        {
            if (built <= 3) return new[] { new ResourceAmount("planks", 40), new ResourceAmount("steel", 10) };
            if (built == 4) return new[] { new ResourceAmount("steel", 30), new ResourceAmount("tools", 10) };
            return new[] { new ResourceAmount("steel", 50), new ResourceAmount("tools", 20), new ResourceAmount("copper_wire", 15) };
        }

        /// <summary>How long the track crew takes to lay the next platform.</summary>
        public static double PlatformBuildSeconds(int built) { return built <= 3 ? 5 * 60 : built == 4 ? 15 * 60 : 30 * 60; }

        public static int PlotCost(int unlockedPlots)
        {
            int n = unlockedPlots - StartPlots + 1;
            return (int)(400 * Math.Pow(n, 1.5));
        }

        public static int PlotUnlockLevel(int unlockedPlots)
        {
            return 1 + (unlockedPlots - StartPlots) / 2;
        }

        public static int LocoUpgradeCoins(LocomotiveDef def, int level)
        {
            int basePrice = Math.Max(800, def.PriceCoins > 0 ? def.PriceCoins : def.PriceNuggets * 200);
            return (int)(basePrice * 0.35 * level);
        }

        /// <summary>Workshop parts needed for the next overhaul.</summary>
        public static ResourceAmount[] LocoUpgradeResources(int level)
        {
            if (level < 3) return new[] { new ResourceAmount("rivets", 6 * level) };
            if (level < 6) return new[] { new ResourceAmount("rivets", 4 * level), new ResourceAmount("bearings", 3 * level) };
            return new[] { new ResourceAmount("bearings", 3 * level), new ResourceAmount("rods", 2 * level), new ResourceAmount("gauges", level) };
        }

        // ---------------------------------------------------------------- Crew & conveyors

        public const int BaseCrew = 3;
        public const int SettlersPerCrew = 60;
        public const int MaxHiredCrew = 10;
        public static int HireCrewNuggets(int hired) { return 50 + 30 * hired; }
        public const int MaxExtraSlots = 3;
        public static int ExtraSlotNuggets(int bought) { return 100 * (bought + 1); }

        // ---------------------------------------------------------------- Passengers

        /// <summary>Coins per passenger before the town's fare multiplier and income bonuses.</summary>
        public const double FarePerPassenger = 7;
        /// <summary>Seats you can fill per trip: your town sends out this share of its population.</summary>
        public static int PassengerDemand(int population) { return Math.Max(12, population / 2); }

        public const double ContractPartsChance = 0.35;

        /// <summary>Cargo multiplier from locomotive level (+12% per level).</summary>
        public static float LocoCapacityMultiplier(int level) { return 1f + 0.12f * (level - 1); }

        /// <summary>Speed multiplier from locomotive level (+4% per level).</summary>
        public static float LocoSpeedMultiplier(int level) { return 1f + 0.04f * (level - 1); }

        public static int BuildingUpgradeCoins(BuildingDef def, int level)
        {
            return (int)(Math.Max(200, def.CostCoins) * 0.8 * level);
        }

        public static double ConstructionSeconds(BuildingDef def)
        {
            return 4 + ConstructionSecondsPerLevel * def.UnlockLevel;
        }

        /// <summary>Residential output and production speed grow 25% per building level.</summary>
        public static float BuildingLevelMultiplier(int level) { return 1f + 0.25f * (level - 1); }

        /// <summary>Nugget cost to finish a timer immediately.</summary>
        public static int RushCost(double remainingSeconds)
        {
            if (remainingSeconds <= 0) return 0;
            return Math.Max(1, (int)Math.Ceiling(remainingSeconds / 90.0));
        }

        public static int XpForCargo(ResourceDef res, int amount)
        {
            return Math.Max(1, (int)Math.Round(amount * res.Value * 0.35));
        }

        public static int LevelUpNuggets(int newLevel) { return 3 + newLevel / 3; }

        // ---------------------------------------------------------------- Fuel (Coal & Water)

        public const int FuelTankBonus = 50;
        public const double FuelRegenSeconds = 45;
        public const int FuelPerNugget = 8;
        public const int AdFuel = 25;

        public static int MaxFuel(int level, bool tank) { return 60 + 4 * (level - 1) + (tank ? FuelTankBonus : 0); }

        /// <summary>Fuel burned by a dispatch: heavier consists and farther outposts cost more.</summary>
        public static int FuelCost(int wagons, DestinationDef d) { return 2 + wagons + d.UnlockLevel / 4; }

        public static int RefillNuggets(int missing) { return missing <= 0 ? 0 : (int)Math.Ceiling(missing / (double)FuelPerNugget); }

        // ---------------------------------------------------------------- Rail Season Pass

        public const int PassTiers = 30;
        public const int PassXpPerTier = 500;
        public const double SeasonSeconds = 28 * 24 * 3600;
        /// <summary>2026-01-05 00:00 UTC (a Monday). Seasons and weekly offers count from here.</summary>
        public const double SeasonEpoch = 1767571200;
        public const double WeekSeconds = 7 * 24 * 3600;

        public const int PassXpDispatch = 15, PassXpContract = 60, PassXpProduction = 10, PassXpConstruct = 25;

        // ---------------------------------------------------------------- Ads

        public static int AdDailyLimit(AdReward r)
        {
            switch (r)
            {
                case AdReward.Fuel: return 5;
                case AdReward.Nuggets: return 3;
                default: return 5;
            }
        }

        public const int AdNuggets = 3;
        /// <summary>A rush ad knocks this many seconds off a trip.</summary>
        public const double AdRushSeconds = 30 * 60;
        public static int LevelUpCoins(int newLevel) { return 150 * newLevel; }
    }
}

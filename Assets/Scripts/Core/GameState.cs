using System;
using System.Collections.Generic;

namespace Ironhorse.Core
{
    // All persistent state. Kept to plain public fields and Lists so Unity's JsonUtility
    // (and any other serializer) can round-trip it. No dictionaries, no properties.

    [Serializable]
    public class GameState
    {
        public int version = 1;
        public string townName = "Dry Creek";
        /// <summary>Story beats already shown (see StoryContent), so each plays once per save.</summary>
        public List<string> storySeen = new List<string>();
        /// <summary>Chapter rewards already claimed (ChapterContent ids).</summary>
        public List<string> chaptersClaimed = new List<string>();
        public int coins;
        public int nuggets;
        public int xp;
        public int level = 1;
        public int warehouseLevel = 1;
        /// <summary>Platforms laid at the station (0 in saves from before platforms were laid one at a time).</summary>
        public int platforms;
        /// <summary>When the track crew finishes the next platform (0 = no work under way).</summary>
        public double platformReadyAt;
        public int unlockedPlots;
        public int nextUid = 1;
        public int rngSeed;
        public int rngCounter;
        public double createdAt;
        public double lastSavedAt;
        public double nextContractAt;

        public List<ResourceAmount> inventory = new List<ResourceAmount>();
        public List<TrainState> trains = new List<TrainState>();
        public List<WagonState> wagons = new List<WagonState>();
        public List<BuildingState> buildings = new List<BuildingState>();
        public List<ContractState> contracts = new List<ContractState>();
        public List<GoalProgress> goals = new List<GoalProgress>();
        // Monetization
        public int fuel = -1;               // -1 = fill to max on first load
        public double fuelAt;               // last time fuel was settled
        public List<string> ownedProducts = new List<string>();
        public List<string> transactions = new List<string>();
        /// <summary>Store purchases opened on this device whose reward isn't saved yet (see ReconcilePurchases).</summary>
        public List<PendingPurchase> pendingPurchases = new List<PendingPurchase>();
        public List<string> offerPurchases = new List<string>();
        public int passSeason = -1;
        public int passXp;
        public bool passPremium;
        public List<int> passClaimedFree = new List<int>();
        public List<int> passClaimedPremium = new List<int>();
        public int adDay = -1;
        public List<ResourceAmount> adCounts = new List<ResourceAmount>();
        public double lastOfferShownAt;

        // Gameplay depth
        public int hiredCrew;
        public List<ResourceAmount> blueprints = new List<ResourceAmount>();
        public List<ProjectProgress> projects = new List<ProjectProgress>();
        /// <summary>Road and river vehicles that carry goods out to territory projects (see GameSession.Haulage.cs).</summary>
        public List<HaulerState> haulers = new List<HaulerState>();
        /// <summary>The free starter freight wagon has been handed out.</summary>
        public bool haulerGranted;
        public int taskSet;
        public List<int> taskBaselines = new List<int>();
        /// <summary>Per task in the current set: true when it was swapped for a repeatable fallback at set start.</summary>
        public List<bool> taskFallback = new List<bool>();

        // Daily hooks (see GameSession.Hooks.cs)
        /// <summary>Consecutive paydays claimed; missing a day starts again at day 1.</summary>
        public int paydayStreak;
        /// <summary>Local day number of the last payday claimed (-1 = never).</summary>
        public int paydayLastDay = -1;
        /// <summary>Full 7-day weeks completed, which picks the next day-7 blueprint.</summary>
        public int paydayWeeks;
        public double nextExpressAt;
        public double nextEventAt;
        public List<MapEventState> events = new List<MapEventState>();
        /// <summary>Dispatches since the last mystery crate (a dry spell guarantees one).</summary>
        public int crateDry;
        public int cratesOpened;
        /// <summary>Local day of the last crate and how many have come that day (crates are capped per day).</summary>
        public int crateDay = -1;
        public int cratesToday;
        /// <summary>Every locomotive and wagon ever owned, for the Roundhouse album.</summary>
        public List<string> album = new List<string>();
        public List<string> albumClaimed = new List<string>();

        // Dispatcher's board (see GameSession.Daily.cs)
        public int dailyDay = -1;
        public List<DailyTask> dailyTasks = new List<DailyTask>();
        public int dailyPoints;
        public List<int> dailyChests = new List<int>();
        public int weeklyWeek = -1;
        public int weeklyPoints;
        public List<int> weeklyChests = new List<int>();
        // Engineers
        public List<EngineerState> engineers = new List<EngineerState>();
        // Traveling peddler
        public double peddlerUntil;
        public double nextPeddlerAt;
        public List<DealState> deals = new List<DealState>();
        // Full Steam combo
        public int comboCount;
        public double comboUntil;
        // Frontier curios
        public List<string> curios = new List<string>();
        public List<string> shelvesClaimed = new List<string>();
        // New territory charters: 1 for the first town, +1 for each charter
        public int territory = 1;

        public Stats stats = new Stats();
        public Settings settings = new Settings();
    }

    public enum DailyKind { Dispatch, Contracts, Produce, Collect, Passengers, Crates, Events, Rent, EarnCoins, ProjectDeliveries }

    [Serializable]
    public class DailyTask
    {
        public DailyKind kind;
        public int count;
        /// <summary>The metric's value when the day started.</summary>
        public int baseline;
        public bool claimed;
    }

    [Serializable]
    public class EngineerState
    {
        public string id;
        /// <summary>0 until recruited.</summary>
        public int level;
        public int shards;
        /// <summary>Locomotive they ride (0 = in the bunkhouse).</summary>
        public int trainUid;
    }

    public enum DealKind { Goods, Parts, Blueprint, Shards, Fuel }

    /// <summary>One of the traveling peddler's offers.</summary>
    [Serializable]
    public class DealState
    {
        public DealKind kind;
        public string id = "";
        public int amount;
        public int coins;
        public int nuggets;
        public bool sold;
    }

    public enum MapEventKind { Traveler, GoldStrike, Tumbleweed, Bandits }

    /// <summary>Something happening on the map for a while: a traveler, a gold strike, tumbleweeds, bandits.</summary>
    [Serializable]
    public class MapEventState
    {
        public int uid;
        public MapEventKind kind;
        /// <summary>Outpost the gold strike or the bandits are at.</summary>
        public string destinationId = "";
        /// <summary>Random number the world uses to place it.</summary>
        public int spot;
        public double startsAt;
        public double expiresAt;
    }

    [Serializable]
    public class TrainState
    {
        public int uid;
        public string defId;
        public int level = 1;
        public string nickname;
        /// <summary>Empty when idle at the station.</summary>
        public string destinationId = "";
        public double departedAt;
        public double arrivesAt;
        public string cargoId = "";
        public int cargoAmount;
        /// <summary>Platform the train parks at (0..platforms-1).</summary>
        public int platform;
        /// <summary>A mystery crate rode home with this trip; it stays until tapped.</summary>
        public bool crate;
        /// <summary>Cargo bandits took on this trip (shown when it arrives).</summary>
        public int robbed;
        /// <summary>This trip went to a gold strike and brought home double.</summary>
        public bool boom;
        /// <summary>Full Steam bonus on this trip, in percent (0 = none).</summary>
        public int combo;
        /// <summary>A curio found on this trip, collected when the train gets home.</summary>
        public string curio = "";
        /// <summary>Cargo on this trip that earns XP: the normal load, without Full Steam, engineer or gold-strike
        /// extras (those pay in goods, not levels). -1 for trips from older saves: all of it counts.</summary>
        public int xpCargo = -1;

        public bool IsIdle { get { return string.IsNullOrEmpty(destinationId); } }
    }

    [Serializable]
    public class WagonState
    {
        public int uid;
        public string defId;
        /// <summary>0 when sitting in the depot.</summary>
        public int trainUid;
        /// <summary>Order in the consist.</summary>
        public int slot;
    }

    [Serializable]
    public class BuildingState
    {
        public int uid;
        public string defId;
        public int plot;
        public int level = 1;
        public double builtAt;
        /// <summary>Construction finishes at this time.</summary>
        public double readyAt;
        /// <summary>Residential: time rent was last collected.</summary>
        public double lastCollectAt;
        /// <summary>Production: 0 when idle.</summary>
        /// <summary>Legacy single-batch production (migrated into <see cref="queue"/> on load).</summary>
        public double productionEndsAt;
        public int productionBatches;
        /// <summary>Conveyor of production jobs, one batch each, running back to back.</summary>
        public List<ProductionJob> queue = new List<ProductionJob>();
        /// <summary>Conveyor slots bought with nuggets.</summary>
        public int extraSlots;
    }

    [Serializable]
    public class ProductionJob
    {
        public double startsAt;
        public double endsAt;
    }

    [Serializable]
    public class HaulerState
    {
        public int uid;
        public string defId;
        public int level = 1;
        /// <summary>Project the current load is bound for (empty when the vehicle is in the yard).</summary>
        public string projectId = "";
        public string resourceId = "";
        public int amount;
        /// <summary>Which stage the load is for: it only counts toward that stage.</summary>
        public int stage;
        public double departedAt;
        public double arrivesAt;

        public bool IsIdle { get { return string.IsNullOrEmpty(projectId); } }
    }

    [Serializable]
    public class ProjectProgress
    {
        public string id;
        public int stage;
        public int delivered;
        public bool done;
    }

    [Serializable]
    public class ContractState
    {
        public int uid;
        public string client;
        public List<ResourceAmount> requirements = new List<ResourceAmount>();
        public int rewardCoins;
        public int rewardXp;
        public int rewardNuggets;
        public List<ResourceAmount> rewardParts = new List<ResourceAmount>();
        /// <summary>Express telegram: a rush order that pays triple and disappears at <see cref="expiresAt"/>.</summary>
        public bool express;
        public double expiresAt;
    }

    [Serializable]
    public class GoalProgress
    {
        public string goalId;
        public bool claimed;
    }

    [Serializable]
    public class PendingPurchase
    {
        public string productId;
        public double startedAt;
    }

    [Serializable]
    public class Stats
    {
        public int dispatches;
        public int contractsCompleted;
        public int coinsEarned;
        public int passengers;
        public int projectDeliveries;
        public int eventsDone;
        public int rentCollections;
        public List<ResourceAmount> collected = new List<ResourceAmount>();
        public List<ResourceAmount> produced = new List<ResourceAmount>();
    }

    [Serializable]
    public class Settings
    {
        public bool music = true;
        public bool sfx = true;
        public bool haptics = true;
        public bool notifications = true; // local reminders (train arrivals, fuel, production)
        public bool tutorialDone;
    }

    public static class ResourceListExt
    {
        public static int Get(this List<ResourceAmount> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i].amount;
            return 0;
        }

        public static void Change(this List<ResourceAmount> list, string id, int delta)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == id)
                {
                    var r = list[i];
                    r.amount += delta;
                    if (r.amount <= 0) list.RemoveAt(i); else list[i] = r;
                    return;
                }
            }
            if (delta > 0) list.Add(new ResourceAmount(id, delta));
        }

        public static int Total(this List<ResourceAmount> list)
        {
            int t = 0;
            for (int i = 0; i < list.Count; i++) t += list[i].amount;
            return t;
        }
    }
}

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
        public int taskSet;
        public List<int> taskBaselines = new List<int>();
        /// <summary>Per task in the current set: true when it was swapped for a repeatable fallback at set start.</summary>
        public List<bool> taskFallback = new List<bool>();

        public Stats stats = new Stats();
        public Settings settings = new Settings();
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
        /// <summary>Platform the train parks at (0..Balance.Platforms-1).</summary>
        public int platform;

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
    }

    [Serializable]
    public class GoalProgress
    {
        public string goalId;
        public bool claimed;
    }

    [Serializable]
    public class Stats
    {
        public int dispatches;
        public int contractsCompleted;
        public int coinsEarned;
        public int passengers;
        public int projectDeliveries;
        public List<ResourceAmount> collected = new List<ResourceAmount>();
        public List<ResourceAmount> produced = new List<ResourceAmount>();
    }

    [Serializable]
    public class Settings
    {
        public bool music = true;
        public bool sfx = true;
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

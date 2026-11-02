using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public sealed partial class GameSession
    {
        public BuildingState Building(int uid) { return State.buildings.FirstOrDefault(b => b.uid == uid); }
        public BuildingState BuildingOnPlot(int plot) { return State.buildings.FirstOrDefault(b => b.plot == plot); }

        public bool IsReady(BuildingState b) { return Now >= b.readyAt; }
        public double ConstructionRemaining(BuildingState b) { return Math.Max(0, b.readyAt - Now); }

        public float ConstructionProgress(BuildingState b)
        {
            double total = b.readyAt - b.builtAt;
            if (total <= 0) return 1;
            return (float)Math.Min(1, Math.Max(0, (Now - b.builtAt) / total));
        }

        public int CountBuilt(string defId) { return State.buildings.Count(b => b.defId == defId); }

        public bool IsBuildingUnlocked(BuildingDef d) { return State.level >= d.UnlockLevel; }

        public bool IsPlotUnlocked(int plot) { return plot >= 0 && plot < State.unlockedPlots; }

        public ActionResult CanConstruct(string defId)
        {
            var d = GameDatabase.Building(defId);
            if (d == null) return ActionResult.Fail("Unknown building");
            if (!IsBuildingUnlocked(d)) return ActionResult.Fail("Unlocks at level " + d.UnlockLevel);
            if (CountBuilt(d.Id) >= d.MaxCount) return ActionResult.Fail("You've built the maximum of " + d.MaxCount);
            if (!CanAfford(d.CostCoins, d.CostResources)) return ActionResult.Fail(MissingText(d.CostCoins, d.CostResources));
            if (FreePlots().Count == 0) return ActionResult.Fail("No free plots. Buy more land!");
            return ActionResult.Success();
        }

        public List<int> FreePlots()
        {
            var list = new List<int>();
            for (int i = 0; i < State.unlockedPlots; i++) if (BuildingOnPlot(i) == null) list.Add(i);
            return list;
        }

        public ActionResult Construct(string defId, int plot)
        {
            var check = CanConstruct(defId);
            if (!check) return check;
            if (!IsPlotUnlocked(plot)) return ActionResult.Fail("That land isn't yours yet");
            if (BuildingOnPlot(plot) != null) return ActionResult.Fail("Plot is occupied");
            var d = GameDatabase.Building(defId);
            Pay(d.CostCoins, d.CostResources);
            double ready = Now + Balance.ConstructionSeconds(d);
            var b = new BuildingState { uid = NewUid(), defId = d.Id, plot = plot, builtAt = Now, readyAt = ready, lastCollectAt = ready };
            State.buildings.Add(b);
            AddXp(10 + d.UnlockLevel * 8);
            AddPassXp(Balance.PassXpConstruct);
            RaiseChanged();
            return ActionResult.Success("Construction of " + d.Name + " started");
        }

        public ActionResult MoveBuilding(int uid, int plot)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            if (!IsPlotUnlocked(plot)) return ActionResult.Fail("That land isn't yours yet");
            var other = BuildingOnPlot(plot);
            if (other != null && other != b) { other.plot = b.plot; }
            b.plot = plot;
            RaiseChanged();
            return ActionResult.Success();
        }

        public ActionResult RushConstruction(int uid)
        {
            var b = Building(uid);
            if (b == null || IsReady(b)) return ActionResult.Fail("Nothing to rush");
            var r = SpendNuggets(Balance.RushCost(ConstructionRemaining(b)));
            if (!r) return r;
            b.readyAt = Now;
            b.lastCollectAt = Now;
            RaiseChanged();
            return ActionResult.Success();
        }

        // ---------------------------------------------------------------- Upgrades

        public int BuildingUpgradeCoins(BuildingState b) { return Balance.BuildingUpgradeCoins(GameDatabase.Building(b.defId), b.level); }

        public ResourceAmount[] BuildingUpgradeResources(BuildingState b)
        {
            var d = GameDatabase.Building(b.defId);
            if (d.CostResources == null || d.CostResources.Length == 0) return new ResourceAmount[0];
            return d.CostResources.Select(r => new ResourceAmount(r.id, (int)Math.Ceiling(r.amount * 0.6 * b.level))).ToArray();
        }

        public bool CanUpgradeBuilding(BuildingState b)
        {
            var d = GameDatabase.Building(b.defId);
            return d.Category != BuildingCategory.Decoration && b.level < Balance.MaxBuildingLevel;
        }

        public ActionResult UpgradeBuilding(int uid)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            if (!IsReady(b)) return ActionResult.Fail("Still under construction");
            if (!CanUpgradeBuilding(b)) return ActionResult.Fail("Already at max level");
            if (b.queue.Count > 0) return ActionResult.Fail("Finish and collect the conveyor first");
            // Bank any rent before the rate changes.
            if (GameDatabase.Building(b.defId).Category == BuildingCategory.Residential) CollectRent(uid);
            int coins = BuildingUpgradeCoins(b);
            var res = BuildingUpgradeResources(b);
            if (!CanAfford(coins, res)) return ActionResult.Fail(MissingText(coins, res));
            Pay(coins, res);
            b.level++;
            AddXp(15 * b.level);
            RaiseChanged();
            return ActionResult.Success(GameDatabase.Building(b.defId).Name + " is now level " + b.level);
        }

        // ---------------------------------------------------------------- Rent

        public double RentStorageSeconds { get { return Balance.BaseIncomeStorageSeconds * (1 + CivicBonus(CivicEffect.CoinCap)); } }

        public double RentPerMinute(BuildingState b)
        {
            var d = GameDatabase.Building(b.defId);
            if (d.Category != BuildingCategory.Residential) return 0;
            return d.Population * Balance.BuildingLevelMultiplier(b.level) * Balance.RentPerSettlerPerMinute * (1 + CivicBonus(CivicEffect.Income));
        }

        public int PendingRent(BuildingState b)
        {
            if (!IsReady(b)) return 0;
            double secs = Math.Min(RentStorageSeconds, Math.Max(0, Now - b.lastCollectAt));
            return (int)Math.Floor(secs / 60.0 * RentPerMinute(b) + 1e-6);
        }

        public float RentFill(BuildingState b)
        {
            if (!IsReady(b)) return 0;
            return (float)Math.Min(1, Math.Max(0, (Now - b.lastCollectAt) / RentStorageSeconds));
        }

        public ActionResult CollectRent(int uid)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            int coins = PendingRent(b);
            if (coins <= 0) return ActionResult.Fail("No rent yet");
            // Keep the fractional remainder so short collection intervals aren't lossy.
            double perSecond = RentPerMinute(b) / 60.0;
            double usedSeconds = perSecond > 0 ? coins / perSecond : 0;
            double elapsed = Math.Min(RentStorageSeconds, Now - b.lastCollectAt);
            b.lastCollectAt = Now - Math.Max(0, elapsed - usedSeconds);
            EarnCoins(coins);
            AddXp(Math.Max(1, coins / 20));
            RaiseChanged();
            return ActionResult.Success("+" + coins + " coins");
        }

        public int CollectAllRent()
        {
            int total = 0;
            foreach (var b in State.buildings.ToList())
            {
                int p = PendingRent(b);
                if (p > 0 && CollectRent(b.uid)) total += p;
            }
            return total;
        }

        // ---------------------------------------------------------------- Production (crew + conveyor)

        /// <summary>Crew available for factory jobs: a base crew, hired hands, and settlers looking for work.</summary>
        public int CrewTotal { get { return Balance.BaseCrew + State.hiredCrew + Population / Balance.SettlersPerCrew; } }
        /// <summary>Each queued job (running or waiting to be collected) occupies one crew member.</summary>
        public int CrewBusy { get { int n = 0; foreach (var b in State.buildings) n += b.queue.Count; return n; } }
        public int CrewFree { get { return Math.Max(0, CrewTotal - CrewBusy); } }
        public int HireCrewCost { get { return Balance.HireCrewNuggets(State.hiredCrew); } }

        public ActionResult HireCrew()
        {
            if (State.hiredCrew >= Balance.MaxHiredCrew) return ActionResult.Fail("The bunkhouse is full");
            var r = SpendNuggets(HireCrewCost);
            if (!r) return r;
            State.hiredCrew++;
            RaiseChanged();
            return ActionResult.Success("A new crew member signs on!");
        }

        public int QueueSlots(BuildingState b) { return 1 + b.level + b.extraSlots; }
        public int ExtraSlotCost(BuildingState b) { return Balance.ExtraSlotNuggets(b.extraSlots); }

        public ActionResult BuySlot(int uid)
        {
            var b = Building(uid);
            if (b == null || GameDatabase.Building(b.defId).Recipe == null) return ActionResult.Fail("Not a factory");
            if (b.extraSlots >= Balance.MaxExtraSlots) return ActionResult.Fail("The conveyor is as long as it gets");
            var r = SpendNuggets(ExtraSlotCost(b));
            if (!r) return r;
            b.extraSlots++;
            RaiseChanged();
            return ActionResult.Success("Conveyor extended");
        }

        /// <summary>Free conveyor slots (kept under its old name for callers that count batches).</summary>
        public int MaxBatches(BuildingState b) { return Math.Max(0, QueueSlots(b) - b.queue.Count); }

        public bool IsProducing(BuildingState b) { foreach (var j in b.queue) if (Now < j.endsAt) return true; return false; }
        public bool IsProductionDone(BuildingState b) { foreach (var j in b.queue) if (Now >= j.endsAt) return true; return false; }
        public int FinishedJobs(BuildingState b) { int n = 0; foreach (var j in b.queue) if (Now >= j.endsAt) n++; return n; }

        public double ProductionSeconds(BuildingState b, int batches)
        {
            var d = GameDatabase.Building(b.defId);
            return d.Recipe.Seconds * batches / Balance.BuildingLevelMultiplier(b.level);
        }

        ProductionJob RunningJob(BuildingState b) { foreach (var j in b.queue) if (Now < j.endsAt) return j; return null; }

        /// <summary>Progress of the job currently on the belt (0 when idle).</summary>
        public float ProductionProgress(BuildingState b)
        {
            var j = RunningJob(b);
            if (j == null) return 0;
            double total = j.endsAt - j.startsAt;
            return total <= 0 ? 1 : (float)Math.Min(1, Math.Max(0, (Now - j.startsAt) / total));
        }

        /// <summary>Seconds until the whole queue is done.</summary>
        public double QueueRemaining(BuildingState b) { return b.queue.Count == 0 ? 0 : Math.Max(0, b.queue[b.queue.Count - 1].endsAt - Now); }

        public int AffordableBatches(BuildingState b)
        {
            var d = GameDatabase.Building(b.defId);
            if (d.Recipe == null) return 0;
            int n = Math.Min(MaxBatches(b), CrewFree);
            foreach (var i in d.Recipe.Inputs) n = Math.Min(n, Amount(i.id) / i.amount);
            return Math.Max(0, n);
        }

        /// <summary>Puts one batch on the conveyor. Jobs run back to back; each needs a crew member and its inputs.</summary>
        public ActionResult Enqueue(int uid)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            var d = GameDatabase.Building(b.defId);
            if (d.Recipe == null) return ActionResult.Fail("This building doesn't produce goods");
            if (!IsReady(b)) return ActionResult.Fail("Still under construction");
            if (b.queue.Count >= QueueSlots(b)) return ActionResult.Fail("The conveyor is full");
            if (CrewFree <= 0) return ActionResult.Fail("No crew free. Collect finished goods or hire hands");
            if (!CanAfford(0, d.Recipe.Inputs)) return ActionResult.Fail(MissingText(0, d.Recipe.Inputs));
            Pay(0, d.Recipe.Inputs);
            double start = b.queue.Count > 0 ? Math.Max(Now, b.queue[b.queue.Count - 1].endsAt) : Now;
            b.queue.Add(new ProductionJob { startsAt = start, endsAt = start + ProductionSeconds(b, 1) });
            RaiseChanged();
            return ActionResult.Success(d.Name + " is working");
        }

        public ActionResult StartProduction(int uid, int batches)
        {
            if (batches < 1) return ActionResult.Fail("Nothing to start");
            ActionResult last = ActionResult.Fail("Nothing started");
            int started = 0;
            for (int i = 0; i < batches; i++)
            {
                last = Enqueue(uid);
                if (!last) break;
                started++;
            }
            return started > 0 ? ActionResult.Success(started + " batch" + (started > 1 ? "es" : "") + " on the conveyor") : last;
        }

        /// <summary>Collects every finished job on the conveyor; their crew returns.</summary>
        public ActionResult CollectProduction(int uid)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            int done = FinishedJobs(b);
            if (done == 0) return ActionResult.Fail("Not finished yet");
            var d = GameDatabase.Building(b.defId);
            int amount = d.Recipe.Output.amount * done;
            if (WarehouseFree < amount) return ActionResult.Fail("Warehouse is full!");
            b.queue.RemoveAll(j => Now >= j.endsAt);
            State.inventory.Change(d.Recipe.Output.id, amount);
            State.stats.produced.Change(d.Recipe.Output.id, amount);
            var res = GameDatabase.Resource(d.Recipe.Output.id);
            AddXp(Balance.XpForCargo(res, amount));
            AddPassXp(Balance.PassXpProduction * done);
            _productionSeen.Remove(b.uid);
            RaiseChanged();
            return ActionResult.Success("+" + amount + " " + res.Name);
        }

        public int RushProductionCost(BuildingState b) { return Balance.RushCost(QueueRemaining(b)); }

        /// <summary>Finishes everything on the conveyor now.</summary>
        public ActionResult RushProduction(int uid)
        {
            var b = Building(uid);
            if (b == null || !IsProducing(b)) return ActionResult.Fail("Nothing to rush");
            var r = SpendNuggets(RushProductionCost(b));
            if (!r) return r;
            foreach (var j in b.queue) if (j.endsAt > Now) { j.endsAt = Now; if (j.startsAt > Now) j.startsAt = Now; }
            RaiseChanged();
            return ActionResult.Success();
        }

        void MigrateProduction()
        {
            foreach (var b in State.buildings)
            {
                if (b.queue == null) b.queue = new List<ProductionJob>();
                if (b.productionEndsAt > 0 && b.productionBatches > 0)
                {
                    var d = GameDatabase.Building(b.defId);
                    double per = d.Recipe != null ? ProductionSeconds(b, 1) : 0;
                    for (int i = 0; i < b.productionBatches; i++)
                    {
                        double end = b.productionEndsAt - per * (b.productionBatches - 1 - i);
                        b.queue.Add(new ProductionJob { startsAt = end - per, endsAt = end });
                    }
                }
                b.productionEndsAt = 0;
                b.productionBatches = 0;
            }
        }

        // ---------------------------------------------------------------- Land & warehouse

        public bool CanBuyPlot { get { return State.unlockedPlots < Balance.TotalPlots; } }
        public int NextPlotCost { get { return Balance.PlotCost(State.unlockedPlots); } }
        public int NextPlotLevel { get { return Balance.PlotUnlockLevel(State.unlockedPlots); } }

        public ActionResult BuyPlot()
        {
            if (!CanBuyPlot) return ActionResult.Fail("You own the whole valley!");
            if (State.level < NextPlotLevel) return ActionResult.Fail("Unlocks at level " + NextPlotLevel);
            int cost = NextPlotCost;
            if (!CanAfford(cost, null)) return ActionResult.Fail(MissingText(cost, null));
            Pay(cost, null);
            State.unlockedPlots++;
            RaiseChanged();
            return ActionResult.Success("New land surveyed!");
        }

        public ActionResult UpgradeWarehouse()
        {
            if (State.warehouseLevel >= Balance.MaxWarehouseLevel) return ActionResult.Fail("Warehouse is maxed");
            int coins = Balance.WarehouseUpgradeCoins(State.warehouseLevel);
            var res = Balance.WarehouseUpgradeResources(State.warehouseLevel);
            if (!CanAfford(coins, res)) return ActionResult.Fail(MissingText(coins, res));
            Pay(coins, res);
            State.warehouseLevel++;
            AddXp(25 * State.warehouseLevel);
            RaiseChanged();
            return ActionResult.Success("Warehouse holds " + WarehouseCapacity + " now");
        }

        /// <summary>Sell surplus goods for coins at 60% of value.</summary>
        public ActionResult Sell(string resourceId, int amount)
        {
            var r = GameDatabase.Resource(resourceId);
            if (r == null || amount <= 0) return ActionResult.Fail("Nothing to sell");
            if (Amount(resourceId) < amount) return ActionResult.Fail("Not enough " + r.Name);
            State.inventory.Change(resourceId, -amount);
            int coins = Math.Max(1, (int)(r.Value * amount * 0.6));
            EarnCoins(coins);
            RaiseChanged();
            return ActionResult.Success("Sold for " + coins + " coins");
        }
    }
}

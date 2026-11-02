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
            if (b.productionEndsAt > 0) return ActionResult.Fail("Collect production first");
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

        // ---------------------------------------------------------------- Production

        public int MaxBatches(BuildingState b) { return 1 + b.level; }

        public bool IsProducing(BuildingState b) { return b.productionEndsAt > 0 && Now < b.productionEndsAt; }
        public bool IsProductionDone(BuildingState b) { return b.productionEndsAt > 0 && Now >= b.productionEndsAt; }

        public double ProductionSeconds(BuildingState b, int batches)
        {
            var d = GameDatabase.Building(b.defId);
            return d.Recipe.Seconds * batches / Balance.BuildingLevelMultiplier(b.level);
        }

        public float ProductionProgress(BuildingState b)
        {
            if (b.productionEndsAt <= 0) return 0;
            double total = ProductionSeconds(b, Math.Max(1, b.productionBatches));
            return (float)Math.Min(1, Math.Max(0, 1 - (b.productionEndsAt - Now) / total));
        }

        public int AffordableBatches(BuildingState b)
        {
            var d = GameDatabase.Building(b.defId);
            if (d.Recipe == null) return 0;
            int n = MaxBatches(b);
            foreach (var i in d.Recipe.Inputs) n = Math.Min(n, Amount(i.id) / i.amount);
            return Math.Max(0, n);
        }

        public ActionResult StartProduction(int uid, int batches)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            var d = GameDatabase.Building(b.defId);
            if (d.Recipe == null) return ActionResult.Fail("This building doesn't produce goods");
            if (!IsReady(b)) return ActionResult.Fail("Still under construction");
            if (b.productionEndsAt > 0) return ActionResult.Fail(IsProducing(b) ? "Already working" : "Collect finished goods first");
            if (batches < 1 || batches > MaxBatches(b)) return ActionResult.Fail("Upgrade to run " + batches + " batches");
            var cost = d.Recipe.Inputs.Select(i => new ResourceAmount(i.id, i.amount * batches)).ToArray();
            if (!CanAfford(0, cost)) return ActionResult.Fail(MissingText(0, cost));
            Pay(0, cost);
            b.productionBatches = batches;
            b.productionEndsAt = Now + ProductionSeconds(b, batches);
            _productionSeen.Remove(b.uid);
            RaiseChanged();
            return ActionResult.Success(d.Name + " is working");
        }

        public ActionResult CollectProduction(int uid)
        {
            var b = Building(uid);
            if (b == null) return ActionResult.Fail("Unknown building");
            if (!IsProductionDone(b)) return ActionResult.Fail("Not finished yet");
            var d = GameDatabase.Building(b.defId);
            int amount = d.Recipe.Output.amount * b.productionBatches;
            if (WarehouseFree < amount) return ActionResult.Fail("Warehouse is full!");
            State.inventory.Change(d.Recipe.Output.id, amount);
            State.stats.produced.Change(d.Recipe.Output.id, amount);
            var res = GameDatabase.Resource(d.Recipe.Output.id);
            AddXp(Balance.XpForCargo(res, amount));
            AddPassXp(Balance.PassXpProduction * b.productionBatches);
            b.productionEndsAt = 0;
            b.productionBatches = 0;
            RaiseChanged();
            return ActionResult.Success("+" + amount + " " + res.Name);
        }

        public ActionResult RushProduction(int uid)
        {
            var b = Building(uid);
            if (b == null || !IsProducing(b)) return ActionResult.Fail("Nothing to rush");
            var r = SpendNuggets(Balance.RushCost(b.productionEndsAt - Now));
            if (!r) return r;
            b.productionEndsAt = Now;
            RaiseChanged();
            return ActionResult.Success();
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

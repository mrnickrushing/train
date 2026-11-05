using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>
    /// Haulage: territory projects are supplied by road and river. The player picks a hauler on the project's site,
    /// it loads the stage's goods from the warehouse (up to its capacity), travels out and back, and the load counts
    /// toward the stage when it arrives. Loads already on the way are committed, so the last batch is never oversent.
    /// Content and tuning are in <see cref="Haulage"/>.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>A hauler set off (for the map animation).</summary>
        public event Action<HaulerState> HaulerSent;
        /// <summary>A hauler's load reached its project and the vehicle is back in the yard.</summary>
        public event Action<HaulerState> HaulerArrived;
        public event Action<HaulerState> HaulerAdded;

        public bool HaulageUnlocked { get { return State.level >= Haulage.UnlockLevel; } }
        public HaulerState Hauler(int uid) { return State.haulers.FirstOrDefault(h => h.uid == uid); }
        public int HaulersOwned(string defId) { return State.haulers.Count(h => h.defId == defId); }
        public bool IsHaulerUnlocked(HaulerDef d) { return State.level >= d.UnlockLevel; }
        public bool IsDelivering(HaulerState h) { return !h.IsIdle; }
        public double HaulRemaining(HaulerState h) { return h.IsIdle ? 0 : Math.Max(0, h.arrivesAt - Now); }
        public float HaulProgress(HaulerState h)
        {
            if (h.IsIdle) return 0;
            double total = h.arrivesAt - h.departedAt;
            return total <= 0 ? 1 : (float)Math.Max(0, Math.Min(1, (Now - h.departedAt) / total));
        }
        public int HaulCapacity(HaulerState h) { return Haulage.CapacityAt(Haulage.Hauler(h.defId), h.level); }
        public int HaulRushCost(HaulerState h) { return Balance.RushCost(HaulRemaining(h)); }

        /// <summary>Goods of the current stage already on the way to the project.</summary>
        public int InFlight(string projectId)
        {
            var pr = Progress(projectId);
            return State.haulers.Where(h => h.projectId == projectId && h.stage == pr.stage).Sum(h => h.amount);
        }

        /// <summary>Goods the current stage still needs beyond what has been delivered or is on the way.</summary>
        public int StillToSend(string projectId)
        {
            var def = DepthContent.Project(projectId);
            var pr = Progress(projectId);
            if (def == null || pr.done) return 0;
            return Math.Max(0, def.Stages[pr.stage].Amount - pr.delivered - InFlight(projectId));
        }

        public HaulerState[] HaulersTo(string projectId) { return State.haulers.Where(h => h.projectId == projectId).ToArray(); }

        /// <summary>Why this hauler can't take a load to the project right now, or null (and the load and trip time).</summary>
        public string CantHaul(HaulerState h, string projectId, out int load, out double seconds)
        {
            load = 0; seconds = 0;
            var def = DepthContent.Project(projectId);
            if (h == null || def == null) return "Unknown hauler or project";
            var hd = Haulage.Hauler(h.defId);
            if (!IsProjectUnlocked(def)) return def.Name + " opens at level " + def.UnlockLevel;
            var pr = Progress(projectId);
            if (pr.done) return def.Name + " is finished";
            if (!h.IsIdle) return hd.Name + " is out on a delivery";
            var route = Haulage.Route(projectId);
            seconds = Haulage.TripSeconds(hd, route);
            if (seconds <= 0) return hd.OnWater ? def.Name + " isn't on the water: send a wagon" : "No road reaches " + def.Name;
            int need = StillToSend(projectId);
            if (need <= 0) return "The last batch is on its way";
            var stage = def.Stages[pr.stage];
            int have = Amount(stage.ResourceId);
            if (have <= 0) return "No " + GameDatabase.Resource(stage.ResourceId).Name + " in the warehouse";
            load = Math.Min(Math.Min(have, need), HaulCapacity(h));
            return null;
        }

        /// <summary>Loads the project's current goods onto the hauler and sends it out.</summary>
        public ActionResult SendHauler(int haulerUid, string projectId)
        {
            var h = Hauler(haulerUid);
            int load; double seconds;
            string why = CantHaul(h, projectId, out load, out seconds);
            if (why != null) return ActionResult.Fail(why);
            var pr = Progress(projectId);
            var stage = DepthContent.Project(projectId).Stages[pr.stage];
            State.inventory.Change(stage.ResourceId, -load);
            h.projectId = projectId;
            h.resourceId = stage.ResourceId;
            h.amount = load;
            h.stage = pr.stage;
            h.departedAt = Now;
            h.arrivesAt = Now + seconds;
            HaulerSent?.Invoke(h);
            RaiseChanged();
            return ActionResult.Success(Haulage.Hauler(h.defId).Name + " is hauling " + load + " " + GameDatabase.Resource(stage.ResourceId).Name);
        }

        public ActionResult RushHauler(int haulerUid)
        {
            var h = Hauler(haulerUid);
            if (h == null || h.IsIdle) return ActionResult.Fail("That hauler is in the yard");
            var r = SpendNuggets(HaulRushCost(h));
            if (!r) return r;
            ArriveHauler(h);
            RaiseChanged();
            return ActionResult.Success();
        }

        /// <summary>Counts the load at its project and brings the vehicle home. Goods the stage no longer needs go back
        /// to the warehouse.</summary>
        void ArriveHauler(HaulerState h)
        {
            var def = DepthContent.Project(h.projectId);
            string msg = null;
            if (def != null)
            {
                var pr = Progress(h.projectId);
                int give = 0;
                if (!pr.done && pr.stage == h.stage)
                {
                    give = Math.Min(h.amount, def.Stages[pr.stage].Amount - pr.delivered);
                    if (give > 0) msg = CreditProject(def, pr, give);
                }
                if (h.amount > give) State.inventory.Change(h.resourceId, h.amount - give);
            }
            else if (GameDatabase.Resource(h.resourceId) != null) State.inventory.Change(h.resourceId, h.amount);
            h.projectId = "";
            h.resourceId = "";
            h.amount = 0;
            h.departedAt = h.arrivesAt = 0;
            if (msg != null) Notify?.Invoke(msg);
            HaulerArrived?.Invoke(h);
        }

        bool TickHaulage()
        {
            bool changed = false;
            if (!State.haulerGranted && HaulageUnlocked)
            {
                GrantStarterHauler();
                Notify?.Invoke("A freight wagon joins the railroad: send goods to territory projects by road!");
                changed = true;
            }
            foreach (var h in State.haulers)
                if (!h.IsIdle && Now >= h.arrivesAt) { ArriveHauler(h); changed = true; }
            return changed;
        }

        void GrantStarterHauler()
        {
            State.haulerGranted = true;
            var h = new HaulerState { uid = NewUid(), defId = Haulage.StarterId };
            State.haulers.Add(h);
            HaulerAdded?.Invoke(h);
        }

        void RepairHaulage()
        {
            if (State.haulers == null) State.haulers = new List<HaulerState>();
            State.haulers.RemoveAll(h => Haulage.Hauler(h.defId) == null);
            foreach (var h in State.haulers)
            {
                if (h.projectId == null) h.projectId = "";
                if (h.resourceId == null) h.resourceId = "";
                h.level = Math.Max(1, Math.Min(Haulage.MaxLevel, h.level));
                // A load for a project this version doesn't know: unload it back into the warehouse.
                if (!h.IsIdle && DepthContent.Project(h.projectId) == null) ArriveHauler(h);
            }
            // Railroads already past the first project get their wagon on load (quietly).
            if (!State.haulerGranted && HaulageUnlocked) GrantStarterHauler();
        }

        /// <summary>A new territory: projects start over, so loads on the way come back to the (emptied) warehouse.</summary>
        void ResetHaulers()
        {
            foreach (var h in State.haulers)
            {
                h.projectId = ""; h.resourceId = ""; h.amount = 0; h.departedAt = h.arrivesAt = 0;
            }
        }

        // ---------------------------------------------------------------- the yard

        public ActionResult BuyHauler(string defId)
        {
            var d = Haulage.Hauler(defId);
            if (d == null) return ActionResult.Fail("Unknown hauler");
            if (!IsHaulerUnlocked(d)) return ActionResult.Fail("Unlocks at level " + d.UnlockLevel);
            if (d.PriceCoins == 0 && d.PriceNuggets == 0 && HaulersOwned(d.Id) > 0) return ActionResult.Fail("You already have one");
            if (!CanAfford(d.PriceCoins, null, d.PriceNuggets)) return ActionResult.Fail(MissingText(d.PriceCoins, null, d.PriceNuggets));
            Pay(d.PriceCoins, null, d.PriceNuggets);
            var h = new HaulerState { uid = NewUid(), defId = d.Id };
            State.haulers.Add(h);
            HaulerAdded?.Invoke(h);
            RaiseChanged();
            return ActionResult.Success(d.Name + " joins the freight yard!");
        }

        public int HaulerUpgradeCoins(HaulerState h) { return Haulage.UpgradeCoins(Haulage.Hauler(h.defId), h.level); }
        public ResourceAmount[] HaulerUpgradeResources(HaulerState h) { return Balance.LocoUpgradeResources(h.level); }

        public ActionResult UpgradeHauler(int haulerUid)
        {
            var h = Hauler(haulerUid);
            if (h == null) return ActionResult.Fail("Unknown hauler");
            if (h.level >= Haulage.MaxLevel) return ActionResult.Fail("Fully upgraded");
            int coins = HaulerUpgradeCoins(h);
            var parts = HaulerUpgradeResources(h);
            if (!CanAfford(coins, parts)) return ActionResult.Fail(MissingText(coins, parts));
            Pay(coins, parts);
            h.level++;
            RaiseChanged();
            return ActionResult.Success(Haulage.Hauler(h.defId).Name + " now carries " + HaulCapacity(h));
        }

        public string CantSellHauler(HaulerState h)
        {
            if (h == null) return "Unknown hauler";
            if (!h.IsIdle) return "It's out on a delivery: sell it when it's back";
            if (State.haulers.Count <= 1) return "Keep at least one hauler for the projects";
            return null;
        }

        public ActionResult SellHauler(int haulerUid)
        {
            var h = Hauler(haulerUid);
            string why = CantSellHauler(h);
            if (why != null) return ActionResult.Fail(why);
            var d = Haulage.Hauler(h.defId);
            int coins = Haulage.SellPrice(d, h.level);
            State.haulers.Remove(h);
            EarnCoins(coins);
            RaiseChanged();
            return ActionResult.Success("Sold the " + d.Name + " for " + coins.ToString("#,0") + " coins");
        }
    }
}

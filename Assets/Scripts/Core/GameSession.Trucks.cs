using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    // The Motor Age: a Motor Garage, its trucks and the local delivery board. Content and tuning live in Motor.cs.
    public sealed partial class GameSession
    {
        /// <summary>A truck is back from a delivery with its pay (shown once per trip).</summary>
        public event Action<TruckState> TruckReturned;
        readonly HashSet<int> _truckSeen = new HashSet<int>();

        // ---------------------------------------------------------------- garage

        public BuildingState Garage { get { return State.buildings.FirstOrDefault(b => b.defId == Motor.GarageId); } }
        /// <summary>A finished Motor Garage stands in town.</summary>
        public bool HasGarage { get { var g = Garage; return g != null && IsReady(g); } }
        public int GarageBays { get { return HasGarage ? Motor.Bays(Garage.level) : 0; } }
        public bool GarageFull { get { return State.trucks.Count >= GarageBays; } }

        // ---------------------------------------------------------------- trucks

        public TruckState Truck(int uid) { return State.trucks.FirstOrDefault(t => t.uid == uid); }
        public static string TruckName(TruckState t) { var d = Motor.Truck(t.defId); return d != null ? d.Name : t.defId; }
        public bool IsTruckUnlocked(TruckDef d) { return State.level >= d.UnlockLevel; }
        public int TrucksOwned(string defId) { return State.trucks.Count(t => t.defId == defId); }

        public bool TruckIdle(TruckState t) { return !t.OnJob; }
        public bool TruckBack(TruckState t) { return t.OnJob && Now >= t.returnsAt; }
        public bool TruckOut(TruckState t) { return t.OnJob && Now < t.returnsAt; }
        public double TruckRemaining(TruckState t) { return t.OnJob ? Math.Max(0, t.returnsAt - Now) : 0; }

        public float TruckProgress(TruckState t)
        {
            if (!t.OnJob) return 0;
            double total = t.returnsAt - t.departedAt;
            return total <= 0 ? 1 : (float)Math.Min(1, Math.Max(0, (Now - t.departedAt) / total));
        }

        /// <summary>Units this truck carries at its level.</summary>
        public int TruckLoad(TruckState t)
        {
            var d = Motor.Truck(t.defId);
            return d == null ? 0 : (int)Math.Round(d.Capacity * Motor.LoadMultiplier(t.level));
        }

        public double TruckTripSeconds(TruckState t, CustomerDef c)
        {
            var d = Motor.Truck(t.defId);
            double speed = d.Speed * Motor.SpeedMultiplier(t.level);
            return Math.Max(10, c.Seconds * Balance.ReferenceSpeed / speed);
        }

        /// <summary>Why this truck can't take this order right now, or success.</summary>
        public ActionResult CanSendTruck(TruckState t, DeliveryState job)
        {
            if (t == null || job == null) return ActionResult.Fail("Unknown truck or order");
            if (t.OnJob) return ActionResult.Fail(TruckBack(t) ? "Collect its pay first" : "Out on a delivery");
            if (!HasGarage) return ActionResult.Fail("Build a Motor Garage first");
            if (TruckLoad(t) < job.amount) return ActionResult.Fail("Carries " + TruckLoad(t) + ", order is " + job.amount);
            int have = Amount(job.resourceId);
            if (have < job.amount) return ActionResult.Fail("Need " + (job.amount - have) + " more " + GameDatabase.Resource(job.resourceId).Name);
            return ActionResult.Success();
        }

        public ActionResult SendTruck(int truckUid, int deliveryUid)
        {
            var t = Truck(truckUid);
            var job = State.deliveries.FirstOrDefault(d => d.uid == deliveryUid);
            var r = CanSendTruck(t, job);
            if (!r) return r;
            var c = Motor.Customer(job.customerId);
            State.inventory.Change(job.resourceId, -job.amount);
            State.deliveries.Remove(job);
            t.job = job;
            t.departedAt = Now;
            t.returnsAt = Now + TruckTripSeconds(t, c);
            _truckSeen.Remove(t.uid);
            // An empty slot fills again soon.
            State.nextDeliveryAt = Math.Min(State.nextDeliveryAt, Now + 15);
            RaiseChanged();
            return ActionResult.Success(TruckName(t) + " is off to " + c.Short);
        }

        /// <summary>Pays a truck that's back from its delivery.</summary>
        public ActionResult CollectTruck(int truckUid)
        {
            var t = Truck(truckUid);
            if (t == null) return ActionResult.Fail("Unknown truck");
            if (!t.OnJob) return ActionResult.Fail("Nothing to collect");
            if (!TruckBack(t)) return ActionResult.Fail("Still on the road");
            var job = t.job;
            EarnCoins(job.rewardCoins);
            AddXp(job.rewardXp);
            if (job.rewardParts != null) foreach (var p in job.rewardParts) State.inventory.Change(p.id, p.amount);
            State.stats.truckDeliveries++;
            AddPassXp(Balance.PassXpContract / 2);
            var c = Motor.Customer(job.customerId);
            t.job = new DeliveryState();
            t.departedAt = t.returnsAt = 0;
            RaiseChanged();
            return ActionResult.Success((c != null ? c.Name : "The customer") + " paid " + job.rewardCoins.ToString("#,0") + " coins");
        }

        public int TruckRushCost(TruckState t) { return Balance.RushCost(TruckRemaining(t)); }

        public ActionResult RushTruck(int truckUid)
        {
            var t = Truck(truckUid);
            if (t == null || !TruckOut(t)) return ActionResult.Fail("Truck is not on the road");
            var r = SpendNuggets(TruckRushCost(t));
            if (!r) return r;
            double shift = t.returnsAt - Now;
            t.returnsAt = Now;
            t.departedAt -= shift;
            RaiseChanged();
            return ActionResult.Success();
        }

        public ActionResult BuyTruck(string defId)
        {
            var d = Motor.Truck(defId);
            if (d == null) return ActionResult.Fail("Unknown truck");
            if (!HasGarage) return ActionResult.Fail(Garage == null ? "Build a Motor Garage first" : "The garage is still being built");
            if (!IsTruckUnlocked(d)) return ActionResult.Fail("Unlocks at level " + d.UnlockLevel);
            if (GarageFull) return ActionResult.Fail(Garage.level < Balance.MaxBuildingLevel ? "Every bay is taken: upgrade the garage" : "Every bay is taken");
            if (!CanAfford(d.PriceCoins, null, d.PriceNuggets)) return ActionResult.Fail(MissingText(d.PriceCoins, null, d.PriceNuggets));
            Pay(d.PriceCoins, null, d.PriceNuggets);
            AddTruck(d.Id);
            RaiseChanged();
            return ActionResult.Success(d.Name + " joins your garage!");
        }

        TruckState AddTruck(string defId)
        {
            var t = new TruckState { uid = NewUid(), defId = defId, bay = FreeBay() };
            State.trucks.Add(t);
            return t;
        }

        int FreeBay()
        {
            for (int i = 0; ; i++) if (State.trucks.All(t => t.bay != i)) return i;
        }

        public int TruckUpgradeCoins(TruckState t) { return Motor.UpgradeCoins(Motor.Truck(t.defId), t.level); }
        public ResourceAmount[] TruckUpgradeResources(TruckState t) { return Motor.UpgradeResources(t.level); }

        public ActionResult UpgradeTruck(int truckUid)
        {
            var t = Truck(truckUid);
            if (t == null) return ActionResult.Fail("Unknown truck");
            if (t.level >= Motor.MaxTruckLevel) return ActionResult.Fail("Already at max level");
            int coins = TruckUpgradeCoins(t);
            var res = TruckUpgradeResources(t);
            if (!CanAfford(coins, res)) return ActionResult.Fail(MissingText(coins, res));
            Pay(coins, res);
            t.level++;
            AddXp(15 * t.level);
            RaiseChanged();
            return ActionResult.Success(TruckName(t) + " upgraded to level " + t.level);
        }

        // ---------------------------------------------------------------- delivery board

        public double NextDeliveryIn { get { return Math.Max(0, State.nextDeliveryAt - Now); } }

        /// <summary>The biggest load any truck in the garage carries (the Runabout's before the first truck arrives).</summary>
        public int BiggestTruckLoad
        {
            get
            {
                int best = 0;
                foreach (var t in State.trucks) best = Math.Max(best, TruckLoad(t));
                return best > 0 ? best : Motor.Truck(Motor.StarterTruck).Capacity;
            }
        }

        bool FillDeliveries(bool force)
        {
            if (!HasGarage || State.deliveries.Count >= Motor.DeliverySlots) return false;
            if (!force && Now < State.nextDeliveryAt) return false;
            bool added = false;
            do
            {
                var d = GenerateDelivery();
                if (d == null) break;
                State.deliveries.Add(d);
                added = true;
            } while (force && State.deliveries.Count < Motor.DeliverySlots);
            State.nextDeliveryAt = Now + Motor.DeliveryRefreshSeconds;
            return added;
        }

        DeliveryState GenerateDelivery()
        {
            var rng = NextRng();
            var obtainable = ObtainableResources().Where(id => { var r = GameDatabase.Resource(id); return r != null && !r.IsPart && !r.IsPassenger; }).ToList();
            if (obtainable.Count == 0) return null;
            // Customers who want something the player can make; a fresh customer is preferred over one already waiting.
            var open = Motor.Customers.Where(c => State.level >= c.UnlockLevel && c.Wants.Any(obtainable.Contains)).ToList();
            var fresh = open.Where(c => State.deliveries.All(d => d.customerId != c.Id)).ToList();
            if (fresh.Count > 0) open = fresh;
            CustomerDef cust;
            string resId;
            if (open.Count > 0)
            {
                cust = open[rng.Next(open.Count)];
                var wants = cust.Wants.Where(obtainable.Contains).ToList();
                resId = wants[rng.Next(wants.Count)];
            }
            else
            {
                // Nobody's goods are made yet: the lumber yard takes whatever the railroad hauls.
                cust = Motor.Customer("lumberyard");
                resId = obtainable[rng.Next(obtainable.Count)];
            }
            var res = GameDatabase.Resource(resId);
            int cap = BiggestTruckLoad;
            int amount = Math.Max(2, (int)Math.Round(cap * (0.5 + rng.NextDouble() * 0.5)));
            amount = Math.Min(amount, Math.Min(cap, Math.Max(2, WarehouseCapacity / 4)));
            int value = res.Value * amount;
            var job = new DeliveryState
            {
                uid = NewUid(), customerId = cust.Id, resourceId = resId, amount = amount,
                rewardCoins = Motor.Reward(value, cust.Seconds), rewardXp = Motor.RewardXp(value),
            };
            if (rng.NextDouble() < Motor.PartsChance)
            {
                string[] parts = State.level < 14 ? new[] { "bearings", "rods" } : new[] { "rods", "gauges" };
                job.rewardParts.Add(new ResourceAmount(parts[rng.Next(parts.Length)], 1 + rng.Next(2 + State.level / 6)));
            }
            return job;
        }

        public ActionResult SkipDelivery(int deliveryUid)
        {
            var d = State.deliveries.FirstOrDefault(x => x.uid == deliveryUid);
            if (d == null) return ActionResult.Fail("Unknown order");
            State.deliveries.Remove(d);
            State.nextDeliveryAt = Now + Motor.DeliveryRefreshSeconds;
            RaiseChanged();
            return ActionResult.Success("Order skipped. A new one arrives in " + Fmt(Motor.DeliveryRefreshSeconds));
        }

        static string Fmt(double seconds) { return seconds >= 60 ? (int)Math.Round(seconds / 60) + " min" : (int)seconds + " s"; }

        /// <summary>The best order some idle truck can take now (most pay first), with the truck; null when none.</summary>
        public DeliveryState BestDelivery(out TruckState truck)
        {
            truck = null;
            foreach (var job in State.deliveries.OrderByDescending(d => d.rewardCoins))
            {
                // The smallest truck that fits leaves the big ones for big orders.
                truck = State.trucks.Where(t => CanSendTruck(t, job).Ok).OrderBy(TruckLoad).FirstOrDefault();
                if (truck != null) return job;
            }
            return null;
        }

        // ---------------------------------------------------------------- tick and repair

        bool TickTrucks()
        {
            bool changed = false;
            if (HasGarage && !State.starterTruck)
            {
                State.starterTruck = true;
                if (State.trucks.Count == 0)
                {
                    AddTruck(Motor.StarterTruck);
                    Notify?.Invoke("The Motor Garage is open, and your first truck has arrived!");
                }
                FillDeliveries(force: true);
                changed = true;
            }
            if (FillDeliveries(force: false)) changed = true;
            foreach (var t in State.trucks)
                if (TruckBack(t) && _truckSeen.Add(t.uid))
                {
                    var c = Motor.Customer(t.job.customerId);
                    Notify?.Invoke(TruckName(t) + " is back from " + (c != null ? c.Short : "its delivery") + "!");
                    TruckReturned?.Invoke(t);
                    changed = true;
                }
            return changed;
        }

        void RepairTrucks()
        {
            if (State.trucks == null) State.trucks = new List<TruckState>();
            if (State.deliveries == null) State.deliveries = new List<DeliveryState>();
            State.trucks.RemoveAll(t => t == null || Motor.Truck(t.defId) == null);
            State.deliveries.RemoveAll(d => d == null || d.uid == 0 || Motor.Customer(d.customerId) == null || GameDatabase.Resource(d.resourceId) == null || d.amount <= 0);
            var seen = new HashSet<int>();
            State.trucks.RemoveAll(t => !seen.Add(t.uid));
            var bays = new HashSet<int>();
            foreach (var t in State.trucks)
            {
                if (t.level < 1) t.level = 1;
                if (t.level > Motor.MaxTruckLevel) t.level = Motor.MaxTruckLevel;
                if (t.job == null) t.job = new DeliveryState();
                if (t.job.rewardParts == null) t.job.rewardParts = new List<ResourceAmount>();
                // A delivery to someone this version doesn't know: the truck comes home empty.
                if (t.OnJob && (Motor.Customer(t.job.customerId) == null || GameDatabase.Resource(t.job.resourceId) == null))
                {
                    t.job = new DeliveryState();
                    t.departedAt = t.returnsAt = 0;
                }
                if (t.bay < 0 || !bays.Add(t.bay)) t.bay = -1;
            }
            foreach (var t in State.trucks)
                if (t.bay < 0) { for (int i = 0; ; i++) if (bays.Add(i)) { t.bay = i; break; } }
            foreach (var d in State.deliveries) if (d.rewardParts == null) d.rewardParts = new List<ResourceAmount>();
            int maxUid = 0;
            foreach (var t in State.trucks) maxUid = Math.Max(maxUid, Math.Max(t.uid, t.job.uid));
            foreach (var d in State.deliveries) maxUid = Math.Max(maxUid, d.uid);
            if (State.nextUid <= maxUid) State.nextUid = maxUid + 1;
            // Trucks already back when the save loads are not news.
            foreach (var t in State.trucks) if (TruckBack(t)) _truckSeen.Add(t.uid);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public struct DispatchPreview
    {
        public bool Possible;
        public string Reason;
        public int Amount;
        public double Seconds;
        public int Xp;
        public int Fuel;
        /// <summary>A gold strike doubled the load.</summary>
        public bool Boom;
        /// <summary>Bandits are on this line.</summary>
        public bool Bandits;
        /// <summary>Full Steam multiplier this dispatch would get (1 = none).</summary>
        public float Combo;
    }

    public sealed partial class GameSession
    {
        public TrainState Train(int uid) { return State.trains.FirstOrDefault(t => t.uid == uid); }

        public static string Name(TrainState t)
        {
            if (!string.IsNullOrEmpty(t.nickname)) return t.nickname;
            var d = GameDatabase.Locomotive(t.defId);
            return d != null ? d.Name : t.defId;
        }

        public List<WagonState> WagonsOf(TrainState t)
        {
            return State.wagons.Where(w => w.trainUid == t.uid).OrderBy(w => w.slot).ToList();
        }

        public List<WagonState> DepotWagons()
        {
            return State.wagons.Where(w => w.trainUid == 0).ToList();
        }

        public bool HasArrived(TrainState t) { return !t.IsIdle && Now >= t.arrivesAt; }
        public bool IsEnRoute(TrainState t) { return !t.IsIdle && Now < t.arrivesAt; }

        public double Remaining(TrainState t) { return t.IsIdle ? 0 : Math.Max(0, t.arrivesAt - Now); }

        public float TripProgress(TrainState t)
        {
            if (t.IsIdle) return 0;
            double total = t.arrivesAt - t.departedAt;
            if (total <= 0) return 1;
            return (float)Math.Min(1, Math.Max(0, (Now - t.departedAt) / total));
        }

        public bool IsDestinationUnlocked(DestinationDef d) { return State.level >= d.UnlockLevel; }

        public DispatchPreview PreviewDispatch(TrainState t, DestinationDef d)
        {
            var p = new DispatchPreview();
            var loco = GameDatabase.Locomotive(t.defId);
            var res = GameDatabase.Resource(d.ResourceId);
            p.Seconds = TripSeconds(t, d);
            int capacity = 0;
            foreach (var w in WagonsOf(t))
            {
                var wd = GameDatabase.Wagon(w.defId);
                if (wd.Cargo == res.Cargo) capacity += wd.Capacity;
            }
            p.Combo = NextCombo;
            p.Amount = (int)Math.Round(capacity * Balance.LocoCapacityMultiplier(t.level) * d.Yield * (1 + CivicBonus(CivicEffect.Yield) + EngineerValue(t, EngineerPerk.Cargo, res.Cargo)) * p.Combo);
            if (d.IsTown) p.Amount = Math.Min((int)Math.Round(capacity * Balance.LocoCapacityMultiplier(t.level)), Balance.PassengerDemand(Population));
            else if (BoomAt(d.Id)) { p.Amount *= 2; p.Boom = true; }
            p.Bandits = BanditsOn(d.Id);
            p.Xp = Balance.XpForCargo(res, p.Amount);
            p.Fuel = FuelCost(t, d);

            if (!IsDestinationUnlocked(d)) p.Reason = "Unlocks at level " + d.UnlockLevel;
            else if (!t.IsIdle) p.Reason = "Train is busy";
            else if (capacity == 0) p.Reason = "Needs a " + CargoWagonName(res.Cargo);
            else if (Fuel < p.Fuel) p.Reason = "Needs " + p.Fuel + " fuel (have " + Fuel + ")";
            else p.Possible = true;
            return p;
        }

        public static string CargoWagonName(CargoClass c)
        {
            switch (c)
            {
                case CargoClass.Flat: return "flatcar";
                case CargoClass.Bulk: return "hopper";
                case CargoClass.Box: return "boxcar";
                case CargoClass.Livestock: return "stock car";
                case CargoClass.Coach: return "coach";
                case CargoClass.Tank: return "tank car";
            }
            return "wagon";
        }

        public double TripSeconds(TrainState t, DestinationDef d)
        {
            var loco = GameDatabase.Locomotive(t.defId);
            double speed = loco.Speed * Balance.LocoSpeedMultiplier(t.level) * (1 + CivicBonus(CivicEffect.TrainSpeed) + EngineerValue(t, EngineerPerk.Speed));
            return Math.Max(10, d.BaseSeconds * Balance.ReferenceSpeed / speed);
        }

        public ActionResult Dispatch(int trainUid, string destinationId)
        {
            var t = Train(trainUid);
            var d = GameDatabase.Destination(destinationId);
            if (t == null || d == null) return ActionResult.Fail("Unknown train or destination");
            var p = PreviewDispatch(t, d);
            if (!p.Possible) return ActionResult.Fail(p.Reason);
            if (!TrySpendFuel(p.Fuel)) return ActionResult.Fail("Not enough fuel");
            AddPassXp(Balance.PassXpDispatch);
            string crate = t.crate ? OpenCrateOn(t) : null; // never lose an unopened crate

            t.destinationId = d.Id;
            t.departedAt = Now;
            t.arrivesAt = Now + p.Seconds;
            t.cargoId = d.ResourceId;
            t.cargoAmount = p.Amount;
            _arrivalSeen.Remove(t.uid);
            State.stats.dispatches++;
            RollTrip(t, p);
            ChainCombo(t);
            TrainDeparted?.Invoke(t);
            RaiseChanged();
            return ActionResult.Success(Name(t) + " departs for " + d.Name + (crate != null ? ". " + crate : ""));
        }

        /// <summary>Unload an arrived train. If the warehouse is short on space, the rest stays aboard.</summary>
        public ActionResult Unload(int trainUid)
        {
            var t = Train(trainUid);
            if (t == null) return ActionResult.Fail("Unknown train");
            if (t.IsIdle) return ActionResult.Fail("Nothing to unload");
            if (!HasArrived(t)) return ActionResult.Fail("Still on the line");

            var cargoDef = GameDatabase.Resource(t.cargoId);
            if (cargoDef.IsPassenger) return UnloadPassengers(t);

            int moved = Math.Min(t.cargoAmount, WarehouseFree);
            if (moved <= 0 && t.cargoAmount > 0) return ActionResult.Fail("Warehouse is full! Upgrade it or use goods.");

            var res = GameDatabase.Resource(t.cargoId);
            State.inventory.Change(t.cargoId, moved);
            State.stats.collected.Change(t.cargoId, moved);
            t.cargoAmount -= moved;
            AddXp(Balance.XpForCargo(res, moved));

            string msg = "+" + moved + " " + res.Name;
            if (t.cargoAmount <= 0)
            {
                t.destinationId = "";
                t.cargoId = "";
                t.cargoAmount = 0;
            }
            else msg += " (warehouse full, " + t.cargoAmount + " left aboard)";
            RaiseChanged();
            return ActionResult.Success(msg);
        }

        public int FareFor(TrainState t)
        {
            var d = GameDatabase.Destination(t.destinationId);
            float mult = d != null ? d.Yield : 1;
            return (int)Math.Round(t.cargoAmount * Balance.FarePerPassenger * mult * (1 + CivicBonus(CivicEffect.Income) + EngineerValue(t, EngineerPerk.Fares)) * (1 + t.combo / 100f));
        }

        ActionResult UnloadPassengers(TrainState t)
        {
            int fare = FareFor(t);
            int pax = t.cargoAmount;
            EarnCoins(fare);
            AddXp(Balance.XpForCargo(GameDatabase.Resource("passengers"), pax));
            State.stats.passengers += pax;
            t.destinationId = "";
            t.cargoId = "";
            t.cargoAmount = 0;
            RaiseChanged();
            return ActionResult.Success(pax + " passengers paid " + fare + " coins in fares");
        }

        public int TrainRushCost(TrainState t) { return Balance.RushCost(Remaining(t)); }

        public ActionResult RushTrain(int trainUid)
        {
            var t = Train(trainUid);
            if (t == null || !IsEnRoute(t)) return ActionResult.Fail("Train is not on a trip");
            var r = SpendNuggets(TrainRushCost(t));
            if (!r) return r;
            double shift = t.arrivesAt - Now;
            t.arrivesAt = Now;
            t.departedAt -= shift;
            RaiseChanged();
            return ActionResult.Success();
        }

        // ---------------------------------------------------------------- Rolling stock

        public bool IsLocoUnlocked(LocomotiveDef d) { return State.level >= d.UnlockLevel; }
        public int FleetCapacity { get { return Balance.Platforms; } }

        public ActionResult BuyLocomotive(string defId)
        {
            var d = GameDatabase.Locomotive(defId);
            if (d == null) return ActionResult.Fail("Unknown locomotive");
            if (!IsLocoUnlocked(d)) return ActionResult.Fail("Unlocks at level " + d.UnlockLevel);
            if (State.trains.Count >= FleetCapacity) return ActionResult.Fail("All platforms are full");
            if (!CanAfford(d.PriceCoins, null, d.PriceNuggets)) return ActionResult.Fail(MissingText(d.PriceCoins, null, d.PriceNuggets));
            Pay(d.PriceCoins, null, d.PriceNuggets);

            int platform = Enumerable.Range(0, Balance.Platforms).First(p => State.trains.All(t => t.platform != p));
            var train = new TrainState { uid = NewUid(), defId = d.Id, platform = platform };
            State.trains.Add(train);
            RaiseLocomotiveAdded(train);
            RaiseChanged();
            return ActionResult.Success(d.Name + " joins your fleet!");
        }

        public bool IsWagonUnlocked(WagonDef d) { return State.level >= d.UnlockLevel; }

        public ActionResult BuyWagon(string defId, int attachToTrainUid = 0)
        {
            var d = GameDatabase.Wagon(defId);
            if (d == null) return ActionResult.Fail("Unknown wagon");
            if (!IsWagonUnlocked(d)) return ActionResult.Fail("Unlocks at level " + d.UnlockLevel);
            if (!CanAfford(d.PriceCoins, null)) return ActionResult.Fail(MissingText(d.PriceCoins, null));
            Pay(d.PriceCoins, null);
            var w = new WagonState { uid = NewUid(), defId = d.Id };
            State.wagons.Add(w);
            if (attachToTrainUid != 0)
            {
                var r = AttachWagon(w.uid, attachToTrainUid);
                if (!r) { RaiseChanged(); return ActionResult.Success(d.Name + " bought and sent to the depot (" + r.Message + ")"); }
            }
            RaiseChanged();
            return ActionResult.Success(d.Name + " bought");
        }

        public ActionResult AttachWagon(int wagonUid, int trainUid)
        {
            var w = State.wagons.FirstOrDefault(x => x.uid == wagonUid);
            var t = Train(trainUid);
            if (w == null || t == null) return ActionResult.Fail("Unknown wagon or train");
            if (!t.IsIdle) return ActionResult.Fail("Train must be at the station");
            if (w.trainUid != 0)
            {
                var other = Train(w.trainUid);
                if (other != null && !other.IsIdle) return ActionResult.Fail("That wagon is out on a trip");
            }
            var consist = WagonsOf(t);
            if (consist.Count >= GameDatabase.Locomotive(t.defId).Power) return ActionResult.Fail("Locomotive can't pull more wagons");
            w.trainUid = t.uid;
            w.slot = consist.Count == 0 ? 0 : consist.Max(x => x.slot) + 1;
            RaiseChanged();
            return ActionResult.Success();
        }

        public ActionResult DetachWagon(int wagonUid)
        {
            var w = State.wagons.FirstOrDefault(x => x.uid == wagonUid);
            if (w == null) return ActionResult.Fail("Unknown wagon");
            var t = Train(w.trainUid);
            if (t != null && !t.IsIdle) return ActionResult.Fail("Train must be at the station");
            w.trainUid = 0;
            w.slot = 0;
            RaiseChanged();
            return ActionResult.Success();
        }

        public int LocoUpgradeCoins(TrainState t) { return Balance.LocoUpgradeCoins(GameDatabase.Locomotive(t.defId), t.level); }
        public ResourceAmount[] LocoUpgradeResources(TrainState t) { return Balance.LocoUpgradeResources(t.level); }

        public ActionResult UpgradeLocomotive(int trainUid)
        {
            var t = Train(trainUid);
            if (t == null) return ActionResult.Fail("Unknown train");
            if (t.level >= Balance.MaxLocoLevel) return ActionResult.Fail("Already at max level");
            int coins = LocoUpgradeCoins(t);
            var res = LocoUpgradeResources(t);
            if (!CanAfford(coins, res)) return ActionResult.Fail(MissingText(coins, res));
            Pay(coins, res);
            t.level++;
            AddXp(20 * t.level);
            RaiseChanged();
            return ActionResult.Success(Name(t) + " upgraded to level " + t.level);
        }
    }
}

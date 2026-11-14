using System;

namespace Ironhorse.Core
{
    /// <summary>
    /// Wear: every engine, truck and hauler (wagons, boats, work trains) has a durability rating, 100 when fresh. A trip
    /// takes 25 of it, less with each durability upgrade (21, 18, ...). Without enough left for a trip it stays home
    /// until it is repaired with a wrench (back to 100) or has rested: time mends it by itself, 0 to 100 in twelve
    /// hours. Wrenches come with each new level, for nuggets, or one for a watched advert.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Wear starts once the railroad is running, not in the first levels.</summary>
        public bool WearOn { get { return State.level >= Balance.WearFromLevel; } }
        public int Wrenches { get { return State.wrenches; } }

        /// <summary>The durability rating the player reads: 100 fresh, 0 worn out.</summary>
        public static int Rating(float wear) { return (int)Math.Round((1 - Math.Max(0, Math.Min(1, wear))) * 100); }

        /// <summary>Durability one trip takes at this many upgrades (25 with none).</summary>
        public static int TripCost(int durability) { return Balance.WearTripCost(durability); }

        /// <summary>Trips a fresh engine or truck makes before it has too little durability left for another.</summary>
        public static int TripsWhenFresh(int durability) { return 100 / TripCost(durability); }

        /// <summary>Wear as it stands now: time mends a little by itself, all the way in <see cref="Balance.WearMendSeconds"/>.</summary>
        float Eased(float wear, double wearAt)
        {
            if (wearAt <= 0 || wear <= 0) return wear;
            return (float)Math.Max(0, wear - Math.Max(0, Now - wearAt) / Balance.WearMendSeconds);
        }

        public float WearOf(TrainState t) { return Eased(t.wear, t.wearAt); }
        public float WearOf(TruckState t) { return Eased(t.wear, t.wearAt); }
        public float WearOf(HaulerState h) { return Eased(h.wear, h.wearAt); }

        /// <summary>Trips left at this wear: a trip needs its whole cost in durability.</summary>
        public int TripsLeft(float wear, int durability)
        {
            return WearOn ? Rating(wear) / TripCost(durability) : TripsWhenFresh(durability);
        }

        public int TripsLeft(TrainState t) { return TripsLeft(WearOf(t), t.durability); }
        public int TripsLeft(TruckState t) { return TripsLeft(WearOf(t), t.durability); }
        public int TripsLeft(HaulerState h) { return TripsLeft(WearOf(h), h.durability); }

        /// <summary>Seconds of rest until it has the durability for one more trip (0: it can go now).</summary>
        public double RestNeeded(float wear, int durability)
        {
            if (TripsLeft(wear, durability) > 0) return 0;
            return Math.Max(1, (TripCost(durability) / 100.0 - (1 - wear)) * Balance.WearMendSeconds);
        }

        /// <summary>Why it can't go out (worn out), or null.</summary>
        string WornOut(float wear, int durability, string name)
        {
            if (TripsLeft(wear, durability) > 0) return null;
            return name + " is worn down (" + Rating(wear) + " of the " + TripCost(durability) + " a trip takes): use a wrench, or rest it " + RestText(RestNeeded(wear, durability));
        }

        static string RestText(double seconds)
        {
            int m = (int)Math.Ceiling(seconds / 60);
            return m >= 60 ? (m / 60) + "h " + (m % 60).ToString("00") + "m" : m + "m";
        }

        /// <summary>Folds the mending done by time into the stored wear, then adds one trip's worth.</summary>
        void WearTrip(ref float wear, ref double wearAt, int durability)
        {
            wear = Eased(wear, wearAt); wearAt = Now;
            if (WearOn) wear = (float)Math.Min(1, wear + TripCost(durability) / 100.0);
        }

        ActionResult Mend(ref float wear, ref double wearAt, string name)
        {
            wear = Eased(wear, wearAt); wearAt = Now;
            if (wear < 0.02f) return ActionResult.Fail(name + " is in fine shape");
            if (State.wrenches <= 0) return ActionResult.Fail("No wrench in the toolbox");
            State.wrenches--; wear = 0; State.stats.repairs++;
            RaiseChanged();
            return ActionResult.Success(name + " is good as new");
        }

        public ActionResult RepairTrain(int trainUid)
        {
            var t = Train(trainUid);
            return t == null ? ActionResult.Fail("Unknown train") : Mend(ref t.wear, ref t.wearAt, Name(t));
        }

        public ActionResult RepairTruck(int truckUid)
        {
            var t = Truck(truckUid);
            return t == null ? ActionResult.Fail("Unknown truck") : Mend(ref t.wear, ref t.wearAt, TruckName(t));
        }

        public ActionResult RepairHauler(int haulerUid)
        {
            var h = Hauler(haulerUid);
            return h == null ? ActionResult.Fail("Unknown hauler") : Mend(ref h.wear, ref h.wearAt, Haulage.Hauler(h.defId).Name);
        }

        // ---- durability: an upgrade of its own, one more trip before worn out for each step

        public int DurabilityCost(int durability) { return Balance.DurabilityCoins(durability); }

        ActionResult Toughen(ref int durability, string name)
        {
            if (durability >= Balance.MaxDurability) return ActionResult.Fail(name + " is as tough as it gets");
            int cost = DurabilityCost(durability);
            if (State.coins < cost) return ActionResult.Fail("Need " + cost.ToString("#,0") + " coins");
            State.coins -= cost; durability++;
            RaiseChanged();
            return ActionResult.Success(name + ": a trip now takes " + TripCost(durability) + " durability");
        }

        public ActionResult ToughenTrain(int trainUid)
        {
            var t = Train(trainUid);
            return t == null ? ActionResult.Fail("Unknown train") : Toughen(ref t.durability, Name(t));
        }

        public ActionResult ToughenTruck(int truckUid)
        {
            var t = Truck(truckUid);
            return t == null ? ActionResult.Fail("Unknown truck") : Toughen(ref t.durability, TruckName(t));
        }

        public ActionResult ToughenHauler(int haulerUid)
        {
            var h = Hauler(haulerUid);
            return h == null ? ActionResult.Fail("Unknown hauler") : Toughen(ref h.durability, Haulage.Hauler(h.defId).Name);
        }

        public void AddWrenches(int n) { State.wrenches = Math.Min(Balance.WrenchesHeld, State.wrenches + n); }

        public ActionResult BuyWrench()
        {
            if (State.wrenches >= Balance.WrenchesHeld) return ActionResult.Fail("The toolbox is full");
            if (State.nuggets < Balance.WrenchNuggets) return ActionResult.Fail("Not enough nuggets");
            State.nuggets -= Balance.WrenchNuggets; AddWrenches(1);
            RaiseChanged();
            return ActionResult.Success("+1 wrench");
        }
    }
}

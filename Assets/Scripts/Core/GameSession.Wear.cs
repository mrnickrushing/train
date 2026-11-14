using System;

namespace Ironhorse.Core
{
    /// <summary>
    /// Wear: engines, trucks and haulers (wagons, boats, work trains) wear with every trip. A fresh one makes six
    /// trips (one more for each level past the first); worn out, it stays home until it is repaired with a wrench or
    /// has rested: time mends it by itself, all the way in twelve hours. Wrenches come with each new level, for
    /// nuggets, or one for a watched advert.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Wear starts once the railroad is running, not in the first levels.</summary>
        public bool WearOn { get { return State.level >= Balance.WearFromLevel; } }
        public int Wrenches { get { return State.wrenches; } }

        /// <summary>Trips a fresh engine or truck of this level makes before it is worn out.</summary>
        public static int TripsWhenFresh(int level) { return Balance.WearTrips(level); }

        /// <summary>Wear as it stands now: time mends a little by itself, all the way in <see cref="Balance.WearMendSeconds"/>.</summary>
        float Eased(float wear, double wearAt)
        {
            if (wearAt <= 0 || wear <= 0) return wear;
            return (float)Math.Max(0, wear - Math.Max(0, Now - wearAt) / Balance.WearMendSeconds);
        }

        public float WearOf(TrainState t) { return Eased(t.wear, t.wearAt); }
        public float WearOf(TruckState t) { return Eased(t.wear, t.wearAt); }
        public float WearOf(HaulerState h) { return Eased(h.wear, h.wearAt); }

        /// <summary>Trips left at this wear before it has to be repaired or rested.</summary>
        public int TripsLeft(float wear, int level)
        {
            int trips = TripsWhenFresh(level);
            return WearOn ? Math.Max(0, (int)Math.Floor((1 - wear) * trips + 1e-3)) : trips;
        }

        public int TripsLeft(TrainState t) { return TripsLeft(WearOf(t), t.level); }
        public int TripsLeft(TruckState t) { return TripsLeft(WearOf(t), t.level); }
        public int TripsLeft(HaulerState h) { return TripsLeft(WearOf(h), h.level); }

        /// <summary>Seconds of rest until it can make one more trip (0: it can go now).</summary>
        public double RestNeeded(float wear, int level)
        {
            if (TripsLeft(wear, level) > 0) return 0;
            return Math.Max(1, (wear - (1 - 1.0 / TripsWhenFresh(level))) * Balance.WearMendSeconds);
        }

        /// <summary>Why it can't go out (worn out), or null.</summary>
        string WornOut(float wear, int level, string name)
        {
            if (TripsLeft(wear, level) > 0) return null;
            return name + " is worn out: repair it with a wrench, or rest it " + RestText(RestNeeded(wear, level));
        }

        static string RestText(double seconds)
        {
            int m = (int)Math.Ceiling(seconds / 60);
            return m >= 60 ? (m / 60) + "h " + (m % 60).ToString("00") + "m" : m + "m";
        }

        /// <summary>Folds the mending done by time into the stored wear, then adds one trip's worth.</summary>
        void WearTrip(ref float wear, ref double wearAt, int level)
        {
            wear = Eased(wear, wearAt); wearAt = Now;
            if (WearOn) wear = (float)Math.Min(1, wear + 1.0 / TripsWhenFresh(level));
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

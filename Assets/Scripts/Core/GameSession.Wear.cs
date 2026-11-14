using System;

namespace Ironhorse.Core
{
    /// <summary>
    /// Wear: engines, trucks and haulers (wagons, boats, work trains) wear with every trip. Past half worn they run slower (up to three tenths longer on
    /// the road when worn out) but never refuse to go. Time mends a little by itself; a wrench puts one right at once. Wrenches come with each new level,
    /// for nuggets, or one for a watched advert; a higher-level engine or truck is built to last longer.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Wear starts once the railroad is running, not in the first levels.</summary>
        public bool WearOn { get { return State.level >= Balance.WearFromLevel; } }
        public int Wrenches { get { return State.wrenches; } }

        public static bool Worn(float wear) { return wear > Balance.WornAt; }

        /// <summary>How much longer a trip takes at this wear: 1 up to half worn, then up to <see cref="Balance.WornSlow"/>.</summary>
        public static double WearSlow(float wear)
        {
            if (wear <= Balance.WornAt) return 1;
            return 1 + (Balance.WornSlow - 1) * Math.Min(1, (wear - Balance.WornAt) / (1 - Balance.WornAt));
        }

        /// <summary>Condition as the player reads it: 100 fresh, 0 worn out.</summary>
        public static int Condition(float wear) { return (int)Math.Round((1 - Math.Max(0, Math.Min(1, wear))) * 100); }

        /// <summary>Wear as it stands now: time mends a little by itself, all the way in <see cref="Balance.WearMendSeconds"/>.</summary>
        float Eased(float wear, double wearAt)
        {
            if (wearAt <= 0 || wear <= 0) return wear;
            return (float)Math.Max(0, wear - Math.Max(0, Now - wearAt) / Balance.WearMendSeconds);
        }

        public float WearOf(TrainState t) { return Eased(t.wear, t.wearAt); }
        public float WearOf(TruckState t) { return Eased(t.wear, t.wearAt); }
        public float WearOf(HaulerState h) { return Eased(h.wear, h.wearAt); }

        /// <summary>Folds the mending done by time into the stored wear, then adds a trip's worth.</summary>
        void WearTrip(ref float wear, ref double wearAt, double tripSeconds, int level)
        {
            wear = Eased(wear, wearAt); wearAt = Now;
            if (WearOn) wear = (float)Math.Min(1, wear + tripSeconds / (Balance.WearSeconds * Balance.Durability(level)));
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

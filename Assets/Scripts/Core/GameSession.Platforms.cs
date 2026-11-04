using System;
using System.Linq;

namespace Ironhorse.Core
{
    /// <summary>
    /// The station grows one platform at a time: a new railroad has <see cref="Balance.StartPlatforms"/>, and each
    /// further platform costs coins plus goods from the town's works and takes the track crew a while to lay.
    /// Every locomotive parks at its own platform, so platforms cap the fleet; selling an engine frees one.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>A platform has opened (its index).</summary>
        public event Action<int> PlatformOpened;

        public int PlatformsBuilt { get { return State.platforms; } }
        /// <summary>One locomotive per platform.</summary>
        public int FleetCapacity { get { return State.platforms; } }
        public bool StationComplete { get { return State.platforms >= Balance.Platforms; } }
        public bool LayingPlatform { get { return State.platformReadyAt > 0; } }
        public double PlatformRemaining { get { return LayingPlatform ? Math.Max(0, State.platformReadyAt - Now) : 0; } }
        /// <summary>0..1 share of the platform under construction that is laid.</summary>
        public float PlatformProgress
        {
            get
            {
                if (!LayingPlatform) return 0;
                double total = Balance.PlatformBuildSeconds(State.platforms);
                return (float)Math.Max(0, Math.Min(1, 1 - PlatformRemaining / total));
            }
        }
        public int NextPlatformLevel { get { return Balance.PlatformUnlockLevel(State.platforms); } }
        public int NextPlatformCoins { get { return Balance.PlatformCoins(State.platforms); } }
        public ResourceAmount[] NextPlatformResources { get { return Balance.PlatformResources(State.platforms); } }
        public double NextPlatformSeconds { get { return Balance.PlatformBuildSeconds(State.platforms); } }
        public int PlatformRushCost { get { return Balance.RushCost(PlatformRemaining); } }

        string FullText
        {
            get { return StationComplete ? "All platforms are full: sell an engine to make room" : "All platforms are full: lay a new platform or sell an engine"; }
        }

        public ActionResult CanLayPlatform()
        {
            if (StationComplete) return ActionResult.Fail("The station has every platform it can hold");
            if (LayingPlatform) return ActionResult.Fail("The track crew is already at work");
            if (State.level < NextPlatformLevel) return ActionResult.Fail("Platform " + (State.platforms + 1) + " can be laid at level " + NextPlatformLevel);
            if (!CanAfford(NextPlatformCoins, NextPlatformResources)) return ActionResult.Fail(MissingText(NextPlatformCoins, NextPlatformResources));
            return ActionResult.Success();
        }

        /// <summary>Pays for the next platform and sets the track crew to work.</summary>
        public ActionResult LayPlatform()
        {
            var ok = CanLayPlatform();
            if (!ok) return ok;
            Pay(NextPlatformCoins, NextPlatformResources);
            State.platformReadyAt = Now + NextPlatformSeconds;
            RaiseChanged();
            return ActionResult.Success("The track crew is laying platform " + (State.platforms + 1));
        }

        public ActionResult RushPlatform()
        {
            if (!LayingPlatform) return ActionResult.Fail("No platform is being laid");
            var r = SpendNuggets(PlatformRushCost);
            if (!r) return r;
            OpenPlatform();
            RaiseChanged();
            return ActionResult.Success();
        }

        bool TickPlatforms()
        {
            if (!LayingPlatform || Now < State.platformReadyAt) return false;
            OpenPlatform();
            return true;
        }

        void OpenPlatform()
        {
            State.platformReadyAt = 0;
            State.platforms = Math.Min(Balance.Platforms, State.platforms + 1);
            Notify?.Invoke("Platform " + State.platforms + " is open: room for another locomotive!");
            PlatformOpened?.Invoke(State.platforms - 1);
        }

        /// <summary>Lowest platform no locomotive is parked at (call only with room in the fleet).</summary>
        int FreePlatform() { return Enumerable.Range(0, FleetCapacity).First(p => State.trains.All(t => t.platform != p)); }

        void RepairPlatforms()
        {
            // Saves from before platforms were laid one at a time keep a platform for every engine they park.
            if (State.platforms <= 0)
                State.platforms = Math.Max(Balance.StartPlatforms, State.trains.Count == 0 ? 0 : State.trains.Max(t => t.platform) + 1);
            // A damaged save with more engines than the station can ever hold keeps the first ones.
            if (State.trains.Count > Balance.Platforms)
            {
                var extra = State.trains.OrderBy(t => t.uid).Skip(Balance.Platforms).Select(t => t.uid).ToList();
                foreach (var w in State.wagons) if (extra.Contains(w.trainUid)) { w.trainUid = 0; w.slot = 0; }
                foreach (var e in State.engineers) if (extra.Contains(e.trainUid)) e.trainUid = 0;
                State.trains.RemoveAll(t => extra.Contains(t.uid));
            }
            State.platforms = Math.Max(State.platforms, State.trains.Count);
            State.platforms = Math.Min(State.platforms, Balance.Platforms);
            if (State.platforms >= Balance.Platforms) State.platformReadyAt = 0;
            foreach (var t in State.trains)
                if (t.platform < 0 || t.platform >= State.platforms || State.trains.Any(o => o != t && o.platform == t.platform && o.uid < t.uid))
                    t.platform = Enumerable.Range(0, State.platforms).First(p => State.trains.All(o => o == t || o.platform != p));
        }

        // ---------------------------------------------------------------- Selling engines

        /// <summary>Coins for selling a locomotive: half what it cost (nugget engines at 200 coins a nugget) and a
        /// quarter of the coins spent overhauling it.</summary>
        public static int LocoSellPrice(TrainState t)
        {
            var d = GameDatabase.Locomotive(t.defId);
            int price = d.PriceCoins > 0 ? d.PriceCoins : d.PriceNuggets * 200;
            if (price <= 0) price = 800;
            long overhauls = 0;
            for (int l = 1; l < t.level; l++) overhauls += Balance.LocoUpgradeCoins(d, l);
            return (int)Math.Min(int.MaxValue, price / 2 + overhauls / 4);
        }

        /// <summary>Why this locomotive can't be sold right now, or null when it can.</summary>
        public string CantSellReason(TrainState t)
        {
            if (t == null) return "Unknown locomotive";
            if (State.trains.Count <= 1) return "It's your only engine: buy another before selling it";
            if (HasArrived(t)) return "Unload " + Name(t) + " first";
            if (!t.IsIdle) return Name(t) + " is out on the line: sell it when it's back";
            if (t.crate) return "Open the mystery crate on " + Name(t) + " first";
            return null;
        }

        /// <summary>Sells a parked locomotive: its wagons go back to the depot, its engineer to the bunkhouse,
        /// and its platform is free for a better engine.</summary>
        public ActionResult SellLocomotive(int trainUid)
        {
            var t = Train(trainUid);
            string why = CantSellReason(t);
            if (why != null) return ActionResult.Fail(why);
            int coins = LocoSellPrice(t);
            int wagons = 0;
            foreach (var w in State.wagons.Where(x => x.trainUid == t.uid)) { w.trainUid = 0; w.slot = 0; wagons++; }
            foreach (var e in State.engineers.Where(x => x.trainUid == t.uid)) e.trainUid = 0;
            string name = Name(t);
            State.trains.Remove(t);
            _arrivalSeen.Remove(t.uid);
            EarnCoins(coins);
            RaiseChanged();
            return ActionResult.Success("Sold " + name + " for " + coins.ToString("#,0") + " coins" + (wagons > 0 ? ", its wagons went to the depot" : ""));
        }
    }
}

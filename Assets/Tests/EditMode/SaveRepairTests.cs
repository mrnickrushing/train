using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    /// <summary>Damaged or outdated saves load without crashing and come back playable.</summary>
    public class SaveRepairTests
    {
        ManualClock _clock;

        [SetUp]
        public void SetUp() { _clock = new ManualClock(); }

        GameState Fresh() { return GameSession.NewGame(_clock.Now, 9); }

        [Test]
        public void UnknownCargoOrDestination_TrainComesHomeEmpty()
        {
            var st = Fresh();
            var t = st.trains[0];
            t.destinationId = "atlantis";
            t.cargoId = "unobtainium";
            t.cargoAmount = 20;
            t.arrivesAt = _clock.Now - 1;
            var s = new GameSession(st, _clock);
            Assert.IsTrue(s.Train(t.uid).IsIdle);
            Assert.AreEqual(0, s.Train(t.uid).cargoAmount);
        }

        [Test]
        public void Unload_UnknownCargo_DoesNotThrow()
        {
            var s = new GameSession(Fresh(), _clock);
            var t = s.State.trains[0];
            t.destinationId = "pinecrest";
            t.cargoId = "unobtainium"; // e.g. a resource removed after the trip started
            t.cargoAmount = 5;
            t.arrivesAt = _clock.Now - 1;
            var r = s.Unload(t.uid);
            Assert.IsTrue(r.Ok);
            Assert.IsTrue(t.IsIdle);
        }

        [Test]
        public void DuplicateUids_NegativeMoney_BadInventory_AreRepaired()
        {
            var st = Fresh();
            var dup = st.trains[0];
            st.trains.Add(new TrainState { uid = dup.uid, defId = "lark", platform = 1 });
            st.wagons.Add(new WagonState { uid = st.wagons[0].uid, defId = "boxcar" });
            st.wagons.Add(new WagonState { uid = 777, defId = "boxcar", trainUid = 4242 }); // coupled to a missing train
            st.coins = -50;
            st.nuggets = -3;
            st.inventory.Add(new ResourceAmount("timber", -10));
            st.inventory.Add(new ResourceAmount("unobtainium", 5));
            st.nextUid = 1;
            var s = new GameSession(st, _clock);

            Assert.AreEqual(1, s.State.trains.Count(t => t.uid == dup.uid));
            Assert.AreEqual(s.State.wagons.Count, s.State.wagons.Select(w => w.uid).Distinct().Count());
            Assert.AreEqual(0, s.State.wagons.First(w => w.uid == 777).trainUid);
            Assert.AreEqual(0, s.Coins);
            Assert.AreEqual(0, s.Nuggets);
            Assert.IsTrue(s.State.inventory.All(r => r.amount > 0 && GameDatabase.Resource(r.id) != null));
            int max = s.State.trains.Select(t => t.uid).Concat(s.State.wagons.Select(w => w.uid)).Concat(s.State.buildings.Select(b => b.uid)).Max();
            Assert.Greater(s.State.nextUid, max);
        }

        [Test]
        public void MoreEnginesThanPlatforms_LoadsAndKeepsSix()
        {
            var st = Fresh();
            for (int i = 0; i < 8; i++) st.trains.Add(new TrainState { uid = 600 + i, defId = "lark", platform = 0 });
            st.wagons.Add(new WagonState { uid = 900, defId = "boxcar", trainUid = 607 });
            var s = new GameSession(st, _clock);
            Assert.AreEqual(Balance.Platforms, s.State.trains.Count);
            Assert.AreEqual(Balance.Platforms, s.FleetCapacity);
            Assert.AreEqual(Balance.Platforms, s.State.trains.Select(t => t.platform).Distinct().Count());
            Assert.IsTrue(s.State.trains.All(t => t.platform >= 0 && t.platform < Balance.Platforms));
            Assert.AreEqual(0, s.State.wagons.First(w => w.uid == 900).trainUid, "wagons of a dropped engine go to the depot");
        }

        [Test]
        public void GrantPurchase_SameTransactionTwice_GrantsOnce()
        {
            var s = new GameSession(Fresh(), _clock);
            int n = s.Nuggets;
            Assert.IsTrue(s.GrantPurchase("ironhorse.nuggets_100", "tx-1").Ok);
            int after = s.Nuggets;
            Assert.Greater(after, n);
            Assert.IsFalse(s.GrantPurchase("ironhorse.nuggets_100", "tx-1").Ok);
            Assert.AreEqual(after, s.Nuggets);
        }
    }
}
